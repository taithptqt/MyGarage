import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  applyCustomerQuotationDecision,
  getQuotationById,
} from "@features/customerPortal/api/customerPortalQuotationApi";
import "./CustomerPortalShell.css";
import "./QuotationDetailPage.css";

function formatDate(value) {
  if (!value) return "—";
  try {
    return new Date(value).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" });
  } catch {
    return String(value);
  }
}

function formatCurrency(value) {
  const amount = Number(value);
  if (!Number.isFinite(amount)) return "—";
  return `${amount.toLocaleString("vi-VN")} ₫`;
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

function statusClassName(status) {
  const normalized = normalizeStatus(status);
  if (normalized.includes("APPROV") || normalized.includes("APPLIED")) return "cp-chip--success";
  if (normalized.includes("REJECT") || normalized.includes("CANCEL")) return "cp-chip--insurance";
  if (normalized.includes("PEND") || normalized.includes("WAIT") || normalized.includes("SENT")) return "cp-chip--survey";
  return "cp-chip--soft";
}

function payerLabel(payerType) {
  const normalized = normalizeStatus(payerType);
  if (normalized.includes("INSURANCE")) return "Bảo hiểm";
  if (normalized.includes("MIX")) return "Hỗn hợp";
  if (normalized.includes("CUSTOMER")) return "Khách hàng";
  return payerType || "—";
}

function normalizeItemType(rawType) {
  const normalized = String(rawType || "").trim().toUpperCase();
  if (["LABOR", "SERVICE", "SERVICES", "WORK"].includes(normalized)) return "SERVICE";
  if (["PART", "PARTS", "PRODUCT", "MATERIAL", "INVENTORY"].includes(normalized)) return "PART";
  return "OTHER";
}

function readNonEmpty(value, fallback = "—") {
  const text = String(value ?? "").trim();
  return text.length > 0 ? text : fallback;
}

function resolveCurrentUserId(user) {
  const candidates = [user?.id, user?.customerId, user?.profile?.id, user?.profile?.customerId];
  for (const candidate of candidates) {
    const parsed = Number(candidate);
    if (Number.isFinite(parsed) && parsed > 0) return parsed;
  }
  return null;
}

export default function QuotationDetailPage() {
  const { quotationId } = useParams();
  const { user } = useAuth();
  const numericQuotationId = Number(quotationId);
  const currentUserId = useMemo(() => resolveCurrentUserId(user), [user]);

  const [quotation, setQuotation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [collapsedPartsByGroup, setCollapsedPartsByGroup] = useState({});

  useEffect(() => {
    if (!Number.isFinite(numericQuotationId) || numericQuotationId <= 0) {
      setLoading(false);
      setError("Mã báo giá không hợp lệ.");
      return;
    }

    let cancelled = false;
    (async () => {
      setLoading(true);
      setError("");
      setMessage("");
      try {
        const nextData = await getQuotationById(numericQuotationId);
        if (!cancelled) setQuotation(nextData || null);
      } catch (e) {
        if (!cancelled) setError(e?.message || "Không tải được báo giá.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [numericQuotationId]);

  const details = useMemo(() => (Array.isArray(quotation?.details) ? quotation.details : []), [quotation]);
  const groupedDetails = useMemo(() => {
    const groups = [];
    let currentGroupKey = "";
    let serviceCounter = 0;

    details.forEach((detail, index) => {
      const type = normalizeItemType(detail?.itemType);
      if (type === "SERVICE") {
        serviceCounter += 1;
        currentGroupKey = `svc-${serviceCounter}`;
        groups.push({
          key: currentGroupKey,
          title: readNonEmpty(detail?.itemName, `Dịch vụ #${serviceCounter}`),
          serviceRows: [detail],
          partRows: [],
        });
        return;
      }

      if (!currentGroupKey) {
        currentGroupKey = "__parts__";
        if (!groups.some((group) => group.key === currentGroupKey)) {
          groups.push({
            key: currentGroupKey,
            title: "Phụ tùng lẻ",
            serviceRows: [],
            partRows: [],
          });
        }
      }

      const target = groups.find((group) => group.key === currentGroupKey);
      if (target) target.partRows.push(detail);
      else {
        groups.push({
          key: `misc-${index}`,
          title: "Hạng mục khác",
          serviceRows: [],
          partRows: [detail],
        });
      }
    });

    return groups;
  }, [details]);

  useEffect(() => {
    setCollapsedPartsByGroup((prev) => {
      const next = { ...prev };
      const activeKeys = new Set();
      groupedDetails.forEach((group) => {
        if (group.partRows.length > 0) {
          activeKeys.add(group.key);
          if (!(group.key in next)) next[group.key] = true;
        }
      });
      Object.keys(next).forEach((key) => {
        if (!activeKeys.has(key)) delete next[key];
      });
      return next;
    });
  }, [groupedDetails]);
  const quotationStatus = normalizeStatus(quotation?.status);
  const canCustomerDecide = ["APPROVED", "SENT_TO_CUSTOMER"].includes(quotationStatus);

  const handleCustomerDecision = async (approved) => {
    if (!numericQuotationId || !currentUserId) {
      setError("Không xác định được tài khoản khách hàng để xác nhận báo giá.");
      return;
    }

    setSaving(true);
    setError("");
    setMessage("");
    try {
      const updated = await applyCustomerQuotationDecision(numericQuotationId, {
        approved,
        customerId: currentUserId,
        note: approved ? "Khách xác nhận đồng ý báo giá" : "Khách xác nhận từ chối báo giá",
      });
      setQuotation(updated || quotation);
      setMessage(approved
        ? "Bạn đã duyệt báo giá thành công. Xưởng sẽ bắt đầu triển khai công việc."
        : "Bạn đã từ chối báo giá. Cố vấn dịch vụ sẽ liên hệ để tư vấn phương án khác.");
    } catch (e) {
      setError(e?.message || "Không thể cập nhật quyết định báo giá.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-quote-hero">
            <div className="cp-quote-hero-copy">
              <p className="cp-eyebrow">Garage của tôi</p>
              <h1 className="cp-title">Xem báo giá dịch vụ</h1>
              <p className="cp-desc">
                Chi tiết hạng mục sửa chữa, phần bảo hiểm hỗ trợ và phần khách hàng cần thanh toán được hiển thị rõ ràng.
              </p>
            </div>
            <div className="cp-quote-hero-actions">
              <Link className="cp-btn cp-btn-ghost" to="/my-notifications">Về thông báo</Link>
              <Link className="cp-btn" to="/my-garage-dashboard">Theo dõi xe</Link>
            </div>
          </section>

          {error ? <div className="cp-error">{error}</div> : null}
          {message ? <div className="cp-success">{message}</div> : null}
          {loading ? <p className="cp-muted">Đang tải…</p> : null}

          {!loading && !error && quotation ? (
            <>
              <section className="cp-card cp-card--primary cp-quote-summary">
                <div className="cp-quote-summary-head">
                  <div>
                    <h2 className="cp-quote-code">{readNonEmpty(quotation.code, `#${numericQuotationId}`)}</h2>
                    <p className="cp-muted cp-quote-title">{readNonEmpty(quotation.title, "Báo giá dịch vụ")}</p>
                  </div>
                  <span className={`cp-chip ${statusClassName(quotation.status)}`}>{readNonEmpty(quotation.status)}</span>
                </div>

                <div className="cp-quote-meta-grid">
                  <div className="cp-quote-meta-item"><dt>Mã phiếu dịch vụ</dt><dd>{readNonEmpty(quotation.serviceOrderId)}</dd></div>
                  <div className="cp-quote-meta-item"><dt>Loại báo giá</dt><dd>{readNonEmpty(quotation.quotationType)}</dd></div>
                  <div className="cp-quote-meta-item"><dt>Phiên bản</dt><dd>{readNonEmpty(quotation.versionNo)}</dd></div>
                  <div className="cp-quote-meta-item"><dt>Hiệu lực đến</dt><dd>{formatDate(quotation.validUntil)}</dd></div>
                  <div className="cp-quote-meta-item"><dt>Áp dụng vào phiếu</dt><dd>{quotation.isAppliedToServiceOrder ? "Đã áp dụng" : "Chưa áp dụng"}</dd></div>
                  <div className="cp-quote-meta-item"><dt>Thời điểm áp dụng</dt><dd>{formatDate(quotation.appliedAt)}</dd></div>
                </div>

                <div className="cp-quote-decision-row">
                  <span>Trạng thái quyết định của khách: <b>{readNonEmpty(quotation.customerDecisionStatus)}</b></span>
                  {canCustomerDecide ? (
                    <div className="cp-quote-decision-actions">
                      <button type="button" className="cp-btn" onClick={() => handleCustomerDecision(true)} disabled={saving}>
                        Đồng ý báo giá
                      </button>
                      <button type="button" className="cp-btn cp-btn-danger" onClick={() => handleCustomerDecision(false)} disabled={saving}>
                        Từ chối báo giá
                      </button>
                    </div>
                  ) : null}
                </div>
              </section>

              <section className="cp-card cp-card--secondary cp-quote-money-grid">
                <div className="cp-quote-money-item"><span>Tạm tính</span><strong>{formatCurrency(quotation.subTotalAmount)}</strong></div>
                <div className="cp-quote-money-item"><span>Giảm giá</span><strong>{formatCurrency(quotation.totalDiscount)}</strong></div>
                <div className="cp-quote-money-item"><span>Thuế</span><strong>{formatCurrency(quotation.taxAmount)}</strong></div>
                <div className="cp-quote-money-item cp-quote-money-item--total"><span>Tổng thanh toán</span><strong>{formatCurrency(quotation.finalAmount)}</strong></div>
                <div className="cp-quote-money-item"><span>Bảo hiểm chi trả</span><strong>{formatCurrency(quotation.totalInsuranceAmount)}</strong></div>
                <div className="cp-quote-money-item"><span>Khách hàng chi trả</span><strong>{formatCurrency(quotation.totalCustomerAmount)}</strong></div>
              </section>

              <section className="cp-card cp-quote-detail-card">
                <div className="cp-section-head">
                  <div>
                    <p className="cp-eyebrow">Chi tiết</p>
                    <h2 className="cp-section-title">Hạng mục báo giá</h2>
                  </div>
                  <span className="cp-muted">{details.length} hạng mục</span>
                </div>

                {details.length === 0 ? (
                  <p className="cp-muted">Báo giá này chưa có hạng mục chi tiết.</p>
                ) : (
                  <div className="cp-quote-group-list">
                    {groupedDetails.map((group) => {
                      const isCollapsed = Boolean(collapsedPartsByGroup[group.key]);
                      const rows = [...group.serviceRows, ...(isCollapsed ? [] : group.partRows)];
                      const groupTotal = [...group.serviceRows, ...group.partRows]
                        .reduce((sum, row) => sum + Number(row?.finalAmount || 0), 0);

                      return (
                        <article key={group.key} className="cp-quote-group">
                          <header className="cp-quote-group-head">
                            <h3>{group.title}</h3>
                            <div className="cp-quote-group-head-actions">
                              {group.partRows.length > 0 ? (
                                <button
                                  type="button"
                                  className="cp-quote-group-toggle"
                                  onClick={() =>
                                    setCollapsedPartsByGroup((prev) => ({
                                      ...prev,
                                      [group.key]: !prev[group.key],
                                    }))
                                  }
                                >
                                  {isCollapsed ? `Hiện phụ tùng (${group.partRows.length})` : "Thu gọn phụ tùng"}
                                </button>
                              ) : null}
                              <strong>{formatCurrency(groupTotal)}</strong>
                            </div>
                          </header>
                          <div className="cp-quote-table-wrap">
                            <table className="cp-quote-table">
                              <thead>
                                <tr>
                                  <th>Hạng mục</th>
                                  <th>Số lượng</th>
                                  <th>Đơn giá</th>
                                  <th>Giảm giá</th>
                                  <th>Thuế</th>
                                  <th>Thành tiền</th>
                                  <th>Người thanh toán</th>
                                  <th>Duyệt</th>
                                </tr>
                              </thead>
                              <tbody>
                                {rows.map((detail) => (
                                  <tr key={readNonEmpty(detail.id, `${detail.itemId}-${detail.itemName}`)}>
                                    <td>
                                      <div className="cp-quote-item-name">{readNonEmpty(detail.itemName)}</div>
                                      <div className="cp-muted cp-quote-item-desc">{readNonEmpty(detail.description)}</div>
                                    </td>
                                    <td>{readNonEmpty(detail.quantity)}</td>
                                    <td>{formatCurrency(detail.unitPriceAtTime)}</td>
                                    <td>{formatCurrency(detail.discountValue)}</td>
                                    <td>{formatCurrency(detail.taxAmount)}</td>
                                    <td>{formatCurrency(detail.finalAmount)}</td>
                                    <td><span className={`cp-chip ${statusClassName(detail.payerType)}`}>{payerLabel(detail.payerType)}</span></td>
                                    <td><span className={`cp-chip ${statusClassName(detail.approvalStatus)}`}>{readNonEmpty(detail.approvalStatus)}</span></td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </article>
                      );
                    })}
                  </div>
                )}
              </section>
            </>
          ) : null}
        </div>
      </div>
      <Footer />
    </div>
  );
}
