import { useCallback, useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getStatusLabelVi } from "@shared/core/utils/statusLabelUtils";
import "./PublicQuotationAccessPage.css";

function formatMoney(value) {
  const amount = Number(value || 0);
  return `${amount.toLocaleString("vi-VN")} đ`;
}

function formatQty(value) {
  return Number(value || 0).toLocaleString("vi-VN");
}

function normalizeItemType(rawType) {
  const normalized = String(rawType || "").trim().toUpperCase();
  if (["LABOR", "SERVICE", "SERVICES", "WORK"].includes(normalized)) return "SERVICE";
  if (["PART", "PARTS", "PRODUCT", "MATERIAL", "INVENTORY"].includes(normalized)) return "PART";
  return "OTHER";
}

function extractBomParentServiceName(description) {
  const text = String(description || "").trim();
  if (!text) return "";
  const marker = "Phụ tùng BOM cho dịch vụ:";
  if (!text.startsWith(marker)) return "";
  return text.slice(marker.length).trim();
}

function getPriorityLabel(row) {
  return row?.isMandatory ? "Bắt buộc" : "Tùy chọn";
}

export default function PublicQuotationAccessPage() {
  const { token } = useParams();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [note, setNote] = useState("");
  const [quotation, setQuotation] = useState(null);
  const [expandedGroups, setExpandedGroups] = useState({});

  const loadQuotation = useCallback(async () => {
    if (!token) return;
    setLoading(true);
    setError("");
    try {
      const data = await serviceOrderApi.getQuotationByPublicToken(token);
      setQuotation(data || null);
    } catch (err) {
      setError(err?.message || "Không thể tải báo giá.");
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void loadQuotation();
  }, [loadQuotation]);

  const statusLabel = useMemo(
    () => getStatusLabelVi(quotation?.status, quotation?.status || "-"),
    [quotation?.status],
  );

  const lineItems = useMemo(
    () => (Array.isArray(quotation?.details) ? quotation.details : []),
    [quotation?.details],
  );

  const summary = useMemo(() => {
    let services = 0;
    let parts = 0;
    let mandatoryCount = 0;
    lineItems.forEach((item) => {
      const type = normalizeItemType(item?.itemType);
      if (type === "SERVICE") services += Number(item?.finalAmount || 0);
      else if (type === "PART") parts += Number(item?.finalAmount || 0);
      if (item?.isMandatory) mandatoryCount += 1;
    });
    return { services, parts, mandatoryCount };
  }, [lineItems]);

  const groupedRows = useMemo(() => {
    const groups = [];
    const byServiceName = new Map();
    let currentServiceGroupKey = "";

    const ensureServiceGroup = (serviceName) => {
      const normalizedName = String(serviceName || "").trim();
      if (!normalizedName) return "";
      if (byServiceName.has(normalizedName)) {
        return byServiceName.get(normalizedName);
      }

      const key = `svc-${groups.length + 1}`;
      groups.push({
        key,
        title: normalizedName,
        kind: "SERVICE",
        serviceRows: [],
        partRows: [],
      });
      byServiceName.set(normalizedName, key);
      return key;
    };

    lineItems.forEach((row, index) => {
      const rowType = normalizeItemType(row?.itemType);
      const itemName = String(row?.itemName || "").trim() || `Hạng mục #${index + 1}`;
      if (rowType === "SERVICE") {
        const groupKey = ensureServiceGroup(itemName);
        currentServiceGroupKey = groupKey || currentServiceGroupKey;
        const target = groups.find((g) => g.key === groupKey);
        if (target) target.serviceRows.push(row);
        return;
      }

      const bomServiceName = extractBomParentServiceName(row?.description);
      let targetKey = bomServiceName ? ensureServiceGroup(bomServiceName) : "";
      if (!targetKey && currentServiceGroupKey) targetKey = currentServiceGroupKey;
      if (!targetKey) {
        const fallbackKey = "__parts__";
        let fallback = groups.find((g) => g.key === fallbackKey);
        if (!fallback) {
          fallback = {
            key: fallbackKey,
            title: "Phụ tùng lẻ",
            kind: "PARTS_ONLY",
            serviceRows: [],
            partRows: [],
          };
          groups.push(fallback);
        }
        fallback.partRows.push(row);
        return;
      }

      const target = groups.find((g) => g.key === targetKey);
      if (target) target.partRows.push(row);
    });

    return groups.filter((group) => group.serviceRows.length > 0 || group.partRows.length > 0);
  }, [lineItems]);

  const paymentSummary = useMemo(() => {
    const totalRepairCost = Number(quotation?.finalAmount || 0);
    const insuranceCompensation = Number(quotation?.totalInsuranceAmount || 0);
    const baselineCustomerPayable = Math.max(totalRepairCost - insuranceCompensation, 0);
    const customerPayable = Number(
      quotation?.totalCustomerAmount ?? baselineCustomerPayable,
    );
    const deductibleOrPenalty = customerPayable - baselineCustomerPayable;

    return {
      totalRepairCost,
      insuranceCompensation,
      deductibleOrPenalty,
      customerPayable,
    };
  }, [quotation?.finalAmount, quotation?.totalCustomerAmount, quotation?.totalInsuranceAmount]);

  useEffect(() => {
    setExpandedGroups((prev) => {
      const next = { ...prev };
      const activeKeys = new Set();
      groupedRows.forEach((group) => {
        activeKeys.add(group.key);
        if (!(group.key in next)) next[group.key] = false;
      });
      Object.keys(next).forEach((key) => {
        if (!activeKeys.has(key)) delete next[key];
      });
      return next;
    });
  }, [groupedRows]);

  async function submitDecision(approved) {
    if (!token) return;
    setSaving(true);
    setError("");
    setMessage("");
    try {
      const payload = {
        approved,
        note: (note || "").trim() || null,
      };
      await serviceOrderApi.applyQuotationPublicDecision(token, payload);
      setMessage(approved ? "Bạn đã xác nhận duyệt báo giá thành công." : "Bạn đã từ chối báo giá.");
      await loadQuotation();
    } catch (err) {
      setError(err?.message || "Không thể ghi nhận quyết định.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <div className="quote-portal-loading">Đang tải báo giá...</div>;
  }

  if (!quotation) {
    return <div className="quote-portal-loading">{error || "Không tìm thấy báo giá."}</div>;
  }

  return (
    <div className="quote-portal">
      <div className="quote-portal-shell">
        <header className="quote-portal-header">
          <div>
            <p className="quote-portal-eyebrow">MyGarage - Cổng duyệt báo giá</p>
            <h1>Báo giá sửa chữa xe {quotation.vehicleLicensePlate || "-"}</h1>
            <p className="quote-portal-subtitle">
              Kính chào <strong>{quotation.customerName || "Quý khách"}</strong>, vui lòng xem chi tiết và chọn phương án xử lý phù hợp.
            </p>
          </div>
          <div className="quote-portal-status">
            <span>Trạng thái</span>
            <strong>{statusLabel}</strong>
          </div>
        </header>

        {error ? <p className="quote-portal-alert quote-portal-alert-error">{error}</p> : null}
        {message ? <p className="quote-portal-alert quote-portal-alert-success">{message}</p> : null}

        <section className="quote-portal-summary">
          <article>
            <span>Tổng dự kiến</span>
            <strong>{formatMoney(quotation.finalAmount)}</strong>
          </article>
          <article>
            <span>Tiền công dịch vụ</span>
            <strong>{formatMoney(summary.services)}</strong>
          </article>
          <article>
            <span>Phụ tùng vật tư</span>
            <strong>{formatMoney(summary.parts)}</strong>
          </article>
          <article>
            <span>Hạng mục bắt buộc</span>
            <strong>{summary.mandatoryCount}</strong>
          </article>
        </section>

        <section className="quote-portal-card">
          <h2>Chi tiết chi phí</h2>
          {groupedRows.map((group, groupIndex) => {
            const groupSubtotal = [...group.serviceRows, ...group.partRows]
              .reduce((sum, row) => sum + Number(row?.finalAmount || 0), 0);
            const isExpanded = Boolean(expandedGroups[group.key]);
            const headerTip = isExpanded ? "Bấm để thu gọn hạng mục" : "Bấm để xem chi tiết hạng mục";

            return (
              <article key={group.key} className={`quote-portal-group${isExpanded ? " is-expanded" : ""}`}>
                <button
                  type="button"
                  className="quote-portal-group-header quote-portal-group-header-clickable"
                  title={headerTip}
                  onClick={() =>
                    setExpandedGroups((prev) => ({
                      ...prev,
                      [group.key]: !prev[group.key],
                    }))
                  }
                >
                  <div className="quote-portal-group-title-wrap">
                    <span className="quote-portal-group-index">{groupIndex + 1}</span>
                    <h3>{group.title}</h3>
                  </div>
                  <div className="quote-portal-group-actions">
                    <strong>{formatMoney(groupSubtotal)}</strong>
                  </div>
                </button>

                {isExpanded ? (
                  <div className="quote-portal-group-body">
                    <div className="quote-portal-service-scope">
                      <section className="quote-portal-subsection">
                        <p className="quote-portal-subsection-title">Chi phí nhân công:</p>
                        <div className="quote-portal-column-header quote-portal-column-header-service">
                          <span>Hạng mục</span>
                          <span>Ưu tiên</span>
                          <span>SL</span>
                          <span>Đơn giá</span>
                          <span>Thành tiền</span>
                        </div>
                        {group.serviceRows.map((row, index) => (
                          <div
                            key={`labor-${row.id ?? index}`}
                            className="quote-portal-line-item quote-portal-line-item-service"
                            title={`Nhân công | SL: ${formatQty(row.quantity)} | Đơn giá: ${formatMoney(row.unitPriceAtTime)}`}
                          >
                            <div className="quote-portal-line-main quote-portal-col-item">
                              <span className="quote-portal-line-icon" aria-hidden="true">•</span>
                              <span className="quote-portal-item-name">{row.itemName}</span>
                            </div>
                            <div className="quote-portal-line-col quote-portal-col-priority quote-portal-col-priority-empty">-</div>
                            <div className="quote-portal-line-col quote-portal-col-qty">{formatQty(row.quantity)}</div>
                            <div className="quote-portal-line-col quote-portal-col-price">{formatMoney(row.unitPriceAtTime)}</div>
                            <div className="quote-portal-line-col quote-portal-line-amount">{formatMoney(row.finalAmount)}</div>
                          </div>
                        ))}
                      </section>

                      <section className="quote-portal-subsection">
                        <p className="quote-portal-subsection-title">{`Chi phí phụ tùng (${group.partRows.length} món):`}</p>
                        <div className="quote-portal-column-header quote-portal-column-header-part">
                          <span>Hạng mục</span>
                          <span>Ưu tiên</span>
                          <span>SL</span>
                          <span>Đơn giá</span>
                          <span>Thành tiền</span>
                        </div>
                        {group.partRows.length === 0 ? (
                          <p className="quote-portal-empty-note">Không có phụ tùng trong hạng mục này.</p>
                        ) : group.partRows.map((row, index) => (
                          <div
                            key={`part-${row.id ?? index}`}
                            className="quote-portal-line-item quote-portal-line-item-part"
                            title={`Phụ tùng | SL: ${formatQty(row.quantity)} | Đơn giá: ${formatMoney(row.unitPriceAtTime)}`}
                          >
                            <div className="quote-portal-line-main quote-portal-col-item">
                              <span className="quote-portal-line-icon" aria-hidden="true">⚙️</span>
                              <span className="quote-portal-item-name">{row.itemName}</span>
                            </div>
                            <div className="quote-portal-line-col quote-portal-col-priority">
                              <span
                                title={row.isMandatory ? "Mục bắt buộc phải thực hiện" : "Mục tùy chọn, có thể bỏ qua"}
                                className={
                                  row.isMandatory
                                    ? "quote-portal-priority-badge quote-portal-priority-badge-mandatory"
                                    : "quote-portal-priority-badge quote-portal-priority-badge-optional"
                                }
                              >
                                {getPriorityLabel(row)}
                              </span>
                            </div>
                            <div className="quote-portal-line-col quote-portal-col-qty">{formatQty(row.quantity)}</div>
                            <div className="quote-portal-line-col quote-portal-col-price">{formatMoney(row.unitPriceAtTime)}</div>
                            <div className="quote-portal-line-col quote-portal-line-amount">{formatMoney(row.finalAmount)}</div>
                          </div>
                        ))}
                      </section>
                    </div>
                  </div>
                ) : null}
              </article>
            );
          })}
        </section>

        <section className="quote-portal-summary-block">
          <h3>Tổng kết tính tiền</h3>
          <div className="quote-portal-summary-line">
            <span>Tổng chi phí sửa chữa</span>
            <strong>{formatMoney(paymentSummary.totalRepairCost)}</strong>
          </div>
          <div className="quote-portal-summary-line">
            <span>− Bảo hiểm đền bù</span>
            <strong>{formatMoney(paymentSummary.insuranceCompensation)}</strong>
          </div>
          <div className="quote-portal-summary-line">
            <span>+ Miễn thường</span>
            <strong>{formatMoney(paymentSummary.deductibleOrPenalty)}</strong>
          </div>
          <div className="quote-portal-summary-divider" />
          <div className="quote-portal-summary-line quote-portal-summary-line-total">
            <span>TỔNG KHÁCH HÀNG THANH TOÁN</span>
            <strong>{formatMoney(paymentSummary.customerPayable)}</strong>
          </div>
        </section>

        <section className="quote-portal-card">
          <h2>Ghi chú của bạn</h2>
          <textarea
            id="customer-note"
            rows={4}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="Ví dụ: Vui lòng giữ lại phụ tùng cũ, hoặc gọi lại cho tôi trước khi làm thêm."
            className="quote-portal-note-input"
            disabled={saving}
          />
        </section>

        <section className="quote-portal-action-area">
          <button
            type="button"
            className="quote-portal-btn quote-portal-btn-approve"
            onClick={() => submitDecision(true)}
            disabled={saving || !quotation.canApprove}
          >
            Đồng ý toàn bộ
          </button>
          <button
            type="button"
            className="quote-portal-btn quote-portal-btn-reject"
            onClick={() => submitDecision(false)}
            disabled={saving || !quotation.canReject}
          >
            Từ chối sửa chữa
          </button>
          <button
            type="button"
            className="quote-portal-btn quote-portal-btn-outline"
            onClick={() => setMessage("Đã ghi nhận yêu cầu tư vấn lại. CVDV sẽ liên hệ với bạn qua email hoặc điện thoại trong thời gian sớm nhất.")}
            disabled={saving}
          >
            Cần CVDV tư vấn lại
          </button>
        </section>
      </div>
    </div>
  );
}
