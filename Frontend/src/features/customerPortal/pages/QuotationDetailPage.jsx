import { Fragment, useEffect, useMemo, useState } from "react";
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

function formatMoney(value) {
  const amount = Number(value || 0);
  return `${amount.toLocaleString("vi-VN")} đ`;
}

function formatQty(value) {
  return Number(value || 0).toLocaleString("vi-VN");
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

function normalizeBooleanLike(value) {
  const normalized = String(value ?? "").trim().toLowerCase();
  return normalized === "true" || normalized === "1" || normalized === "yes";
}

function readField(source, ...keys) {
  for (const key of keys) {
    if (source && source[key] !== undefined && source[key] !== null) {
      return source[key];
    }
  }
  return null;
}

function isSupplementaryRow(row) {
  const serviceSource = String(readField(row, "serviceSource", "ServiceSource") || "").trim().toUpperCase();
  const isSupplementaryRaw = readField(row, "isSupplementary", "IsSupplementary");
  return (
    normalizeBooleanLike(isSupplementaryRaw) ||
    serviceSource === "SUPPLEMENTARY_TECHNICIAN" ||
    serviceSource === "SUPPLEMENTARY_CUSTOMER"
  );
}

function groupRowsByService(rows) {
  const groups = [];
  const byServiceName = new Map();
  let currentServiceGroupKey = "";

  const ensureServiceGroup = (serviceName) => {
    const normalizedName = String(serviceName || "").trim();
    if (!normalizedName) return "";
    if (byServiceName.has(normalizedName)) return byServiceName.get(normalizedName);

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

  rows.forEach((row, index) => {
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

const SERVICE_ROW_TOOLTIP_EXPAND = "Nhấn để xem phụ tùng thuộc dịch vụ này.";
const SERVICE_ROW_TOOLTIP_COLLAPSE = "Nhấn để thu gọn phụ tùng thuộc dịch vụ này.";
const SERVICE_ROW_TOOLTIP_STATIC = "Dòng dịch vụ chính trong báo giá.";

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
  const [collapsedPartGroups, setCollapsedPartGroups] = useState({});

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

  const detailRowsByServiceType = useMemo(() => {
    const initialRows = [];
    const supplementaryRows = [];
    details.forEach((row) => {
      if (isSupplementaryRow(row)) supplementaryRows.push(row);
      else initialRows.push(row);
    });
    return { initialRows, supplementaryRows };
  }, [details]);

  const groupedRowsByServiceType = useMemo(() => ({
    initialGroups: groupRowsByService(detailRowsByServiceType.initialRows),
    supplementaryGroups: groupRowsByService(detailRowsByServiceType.supplementaryRows),
  }), [detailRowsByServiceType.initialRows, detailRowsByServiceType.supplementaryRows]);

  const vatSummary = useMemo(() => {
    const groups = new Map();
    let baseBeforeVat = 0;
    let totalVat = 0;

    details.forEach((row) => {
      const quantity = Number(row?.quantity || 0);
      const unitPrice = Number(row?.unitPriceAtTime || 0);
      const subTotalRaw = Number(row?.subTotal);
      const discountValue = Math.max(0, Number(row?.discountValue || 0));
      const taxRateRaw = Number(row?.taxRate || 0);

      const subTotal = Number.isFinite(subTotalRaw) ? Math.max(0, subTotalRaw) : Math.max(0, quantity * unitPrice);
      const taxableBase = Math.max(0, subTotal - discountValue);
      const taxRate = Number.isFinite(taxRateRaw) && taxRateRaw > 0 ? taxRateRaw : 0;
      const taxAmountRaw = Number(row?.taxAmount);
      const taxAmount = Number.isFinite(taxAmountRaw)
        ? Math.max(0, taxAmountRaw)
        : Number(((taxableBase * taxRate) / 100).toFixed(2));

      baseBeforeVat += taxableBase;
      totalVat += taxAmount;

      const key = taxRate.toFixed(4);
      if (!groups.has(key)) groups.set(key, { taxRate, taxableBase: 0, taxAmount: 0 });
      const target = groups.get(key);
      target.taxableBase += taxableBase;
      target.taxAmount += taxAmount;
    });

    const normalizedGroups = [...groups.values()]
      .map((group) => ({
        taxRate: Number(group.taxRate.toFixed(4)),
        taxableBase: Number(group.taxableBase.toFixed(2)),
        taxAmount: Number(group.taxAmount.toFixed(2)),
      }))
      .sort((a, b) => a.taxRate - b.taxRate);

    const fallbackSubTotal = Math.max(0, Number(quotation?.subTotalAmount || 0));
    const fallbackTax = Math.max(0, Number(quotation?.taxAmount || 0));
    const resolvedBase = normalizedGroups.length > 0 ? Number(baseBeforeVat.toFixed(2)) : fallbackSubTotal;
    const resolvedVat = normalizedGroups.length > 0 ? Number(totalVat.toFixed(2)) : fallbackTax;
    const resolvedWithVat = Math.max(0, Number(quotation?.finalAmount || resolvedBase + resolvedVat));

    return {
      baseBeforeVat: resolvedBase,
      totalVat: resolvedVat,
      totalWithVat: resolvedWithVat,
      groups: normalizedGroups,
    };
  }, [details, quotation?.finalAmount, quotation?.subTotalAmount, quotation?.taxAmount]);

  const executionCostSummary = useMemo(() => {
    let laborBeforeVat = 0;
    let partsAndMaterialBeforeVat = 0;

    details.forEach((row) => {
      const quantity = Number(row?.quantity || 0);
      const unitPrice = Number(row?.unitPriceAtTime || 0);
      const subTotalRaw = Number(row?.subTotal);
      const discountValue = Math.max(0, Number(row?.discountValue || 0));
      const subTotal = Number.isFinite(subTotalRaw) ? Math.max(0, subTotalRaw) : Math.max(0, quantity * unitPrice);
      const taxableBase = Math.max(0, subTotal - discountValue);
      const rowType = normalizeItemType(row?.itemType);
      if (rowType === "SERVICE") laborBeforeVat += taxableBase;
      else partsAndMaterialBeforeVat += taxableBase;
    });

    return {
      laborBeforeVat: Number(laborBeforeVat.toFixed(2)),
      partsAndMaterialBeforeVat: Number(partsAndMaterialBeforeVat.toFixed(2)),
    };
  }, [details]);

  const paymentSummary = useMemo(() => {
    const totalRepairCost = Number(vatSummary.totalWithVat || 0);
    const insuranceCompensation = Number(quotation?.totalInsuranceAmount || 0);
    const baselineCustomerPayable = Math.max(totalRepairCost - insuranceCompensation, 0);
    const customerPayable = Number(quotation?.totalCustomerAmount ?? baselineCustomerPayable);
    const deductibleOrPenalty = customerPayable - baselineCustomerPayable;

    return {
      totalLabor: executionCostSummary.laborBeforeVat,
      totalParts: executionCostSummary.partsAndMaterialBeforeVat,
      beforeVat: vatSummary.baseBeforeVat,
      totalVat: vatSummary.totalVat,
      vatGroups: Array.isArray(vatSummary.groups) ? vatSummary.groups : [],
      totalRepairCost,
      insuranceCompensation,
      deductibleOrPenalty,
      customerPayable,
    };
  }, [
    executionCostSummary.laborBeforeVat,
    executionCostSummary.partsAndMaterialBeforeVat,
    quotation?.totalCustomerAmount,
    quotation?.totalInsuranceAmount,
    vatSummary.baseBeforeVat,
    vatSummary.groups,
    vatSummary.totalVat,
    vatSummary.totalWithVat,
  ]);

  const renderGroupedTableSection = (sectionKey, title, groups, emptyMessage, sectionClassName = "") => (
    <div className={`quote-detail-section ${sectionClassName}`.trim()}>
      <div className="quote-detail-section-title">{title}</div>
      <table className="premium-table">
        <thead>
          <tr>
            <th style={{ width: "50%", textAlign: "left" }}>TÊN HẠNG MỤC / DESCRIPTION</th>
            <th className="quote-portal-col-priority text-center" style={{ width: "15%" }}>ƯU TIÊN</th>
            <th className="text-center" style={{ width: "5%" }}>SL</th>
            <th className="text-right" style={{ width: "15%" }}>ĐƠN GIÁ</th>
            <th className="text-right" style={{ width: "15%" }}>THÀNH TIỀN</th>
          </tr>
        </thead>
        <tbody>
          {groups.length === 0 ? (
            <tr>
              <td colSpan={5} className="quote-detail-empty">{emptyMessage}</td>
            </tr>
          ) : groups.map((group) => {
            const collapseKey = `${sectionKey}-${group.key}`;
            const canToggle = group.partRows.length > 0;
            const isCollapsed = canToggle ? Boolean(collapsedPartGroups[collapseKey]) : false;
            const visibleRows = [
              ...group.serviceRows.map((row) => ({ row, isService: true })),
              ...(isCollapsed ? [] : group.partRows.map((row) => ({ row, isService: false }))),
            ];
            return (
              <Fragment key={`${title}-${group.key}`}>
                {visibleRows.map(({ row, isService }, rowIndex) => {
                  const isFirst = rowIndex === 0;
                  const isLast = rowIndex === visibleRows.length - 1;
                  const serviceRowTooltip = isService
                    ? (canToggle ? (isCollapsed ? SERVICE_ROW_TOOLTIP_EXPAND : SERVICE_ROW_TOOLTIP_COLLAPSE) : SERVICE_ROW_TOOLTIP_STATIC)
                    : "";
                  const rowClass = [
                    "group-row",
                    "item-row",
                    isService ? "group-service-row quote-portal-item-service-row" : "group-part-row",
                    isFirst ? "group-start" : "group-middle",
                    isLast ? "group-end" : "",
                    canToggle && isFirst ? "clickable-service-row" : "",
                  ].join(" ").trim();

                  return (
                    <tr
                      key={`${isService ? "svc" : "part"}-${title}-${row.id || rowIndex}`}
                      className={rowClass}
                      title={serviceRowTooltip}
                      onClick={canToggle && isFirst ? () => setCollapsedPartGroups((prev) => ({ ...prev, [collapseKey]: !prev[collapseKey] })) : undefined}
                    >
                      <td className={isService ? "service-name-cell quote-portal-item-service" : "part-name-cell quote-portal-item-part"}>
                        {!isService ? <span className="quote-portal-item-prefix">└─</span> : null}
                        {row.itemName}
                      </td>
                      <td className="quote-portal-col-priority text-center">
                        <span className={`priority-badge ${row.isMandatory ? "priority-URGENT" : "priority-NORMAL"}`}>
                          {getPriorityLabel(row)}
                        </span>
                      </td>
                      <td className="text-center">{formatQty(row.quantity)}</td>
                      <td className="text-right">{formatMoney(row.unitPriceAtTime)}</td>
                      <td className="text-right">{formatMoney(row.finalAmount)}</td>
                    </tr>
                  );
                })}
                <tr className="group-spacer-row"><td colSpan="5"></td></tr>
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );

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

              <section className="cp-card cp-quote-detail-card">
                <div className="premium-table-wrapper quote-detail-wrapper">
                  {renderGroupedTableSection(
                    "initial",
                    "Dịch vụ khách chọn ban đầu",
                    groupedRowsByServiceType.initialGroups,
                    "Không có dịch vụ ban đầu.",
                  )}
                  {renderGroupedTableSection(
                    "supplementary",
                    "Dịch vụ phát sinh (đã duyệt báo giá bổ sung)",
                    groupedRowsByServiceType.supplementaryGroups,
                    "Chưa có dịch vụ phát sinh.",
                    "quote-detail-section-supplementary",
                  )}
                </div>
              </section>

              <div className="quote-summary-footer">
                <div className="summary-details">
                  <h3 className="summary-title">TỔNG KẾT CHI PHÍ SỬA CHỮA / QUOTATION SUMMARY</h3>

                  <div className="summary-subtitle">Phân tách giá trị (Chưa VAT):</div>
                  <div className="summary-line">
                    <span className="summary-line-label">
                      <span className="summary-marker marker-labor" />
                      Tổng chi phí Nhân công
                    </span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value">{formatMoney(paymentSummary.totalLabor)}</span>
                  </div>
                  <div className="summary-line">
                    <span className="summary-line-label">
                      <span className="summary-marker marker-parts" />
                      Tổng chi phí Phụ tùng / Vật tư
                    </span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value">{formatMoney(paymentSummary.totalParts)}</span>
                  </div>
                  <div className="summary-line summary-line-strong">
                    <span className="summary-line-label">TỔNG TIỀN HÀNG (CHƯA THUẾ)</span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value">{formatMoney(paymentSummary.beforeVat)}</span>
                  </div>

                  <div className="summary-subtitle">Thuế Giá trị gia tăng (VAT):</div>
                  {paymentSummary.vatGroups.length > 0
                    ? paymentSummary.vatGroups.map((group) => (
                      <div key={`vat-${group.taxRate}`} className="summary-line">
                        <span className="summary-line-label">
                          <span className="summary-marker marker-vat" />
                          Thuế VAT ({group.taxRate.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%)
                        </span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">{formatMoney(group.taxAmount)}</span>
                      </div>
                    ))
                    : (
                      <div className="summary-line">
                        <span className="summary-line-label">
                          <span className="summary-marker marker-vat" />
                          Thuế VAT
                        </span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">{formatMoney(paymentSummary.totalVat)}</span>
                      </div>
                    )}
                  <div className="summary-line summary-line-strong">
                    <span className="summary-line-label">TỔNG CHI PHÍ SỬA CHỮA (BAO GỒM THUẾ)</span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value">{formatMoney(paymentSummary.totalRepairCost)}</span>
                  </div>

                  <div className="summary-divider" />
                  <div className="summary-subtitle">Phân bổ thanh toán:</div>
                  <div className="summary-line">
                    <span className="summary-line-label">
                      <span className="summary-marker marker-insurance" />
                      Bảo hiểm phê duyệt
                    </span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value text-insurance">-{formatMoney(paymentSummary.insuranceCompensation)}</span>
                  </div>
                  <div className="summary-line">
                    <span className="summary-line-label">
                      <span className="summary-marker marker-deductible" />
                      Phí Miễn thường / Chế tài
                    </span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value">+{formatMoney(paymentSummary.deductibleOrPenalty)}</span>
                  </div>

                  <div className="summary-divider" />

                  <div className="summary-line payable-row">
                    <span className="summary-line-label">TỔNG KHÁCH HÀNG THANH TOÁN</span>
                    <span className="summary-line-dots" />
                    <span className="summary-line-value final-payable">{formatMoney(paymentSummary.customerPayable)}</span>
                  </div>
                </div>
              </div>

              {canCustomerDecide && (
                <div className="floating-bottom-bar">
                  <div className="floating-bar-content">
                    <div className="floating-bar-left">
                      <span className="floating-bar-label">Tổng thanh toán</span>
                      <span className="floating-bar-value">{formatMoney(paymentSummary.customerPayable)}</span>
                    </div>
                    <div className="floating-bar-right">
                      <button
                        type="button"
                        className="floating-bar-btn"
                        onClick={() => handleCustomerDecision(true)}
                        disabled={saving}
                      >
                        Đồng ý duyệt
                      </button>
                    </div>
                  </div>
                </div>
              )}
            </>
          ) : null}
        </div>
      </div>
      <Footer />
    </div>
  );
}
