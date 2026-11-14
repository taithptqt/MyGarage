import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
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

function normalizeBooleanLike(value) {
  const normalized = String(value || "").trim().toLowerCase();
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

function isExpiredPublicQuotationLinkMessage(rawMessage) {
  const text = String(rawMessage || "").trim().toLowerCase();
  if (!text) return false;
  return (
    text.includes("không còn hiệu lực") ||
    text.includes("khong con hieu luc") ||
    text.includes("hết hạn") ||
    text.includes("het han") ||
    text.includes("đã bị thu hồi") ||
    text.includes("da bi thu hoi")
  );
}

function formatCountdown(seconds) {
  const safe = Math.max(0, Number(seconds) || 0);
  const mm = Math.floor(safe / 60);
  const ss = safe % 60;
  return `${String(mm).padStart(2, "0")}:${String(ss).padStart(2, "0")}`;
}

const SERVICE_ROW_TOOLTIP_EXPAND = "Nhấn để xem phụ tùng.";
const SERVICE_ROW_TOOLTIP_COLLAPSE = "Nhấn để thu gọn phụ tùng.";
const SERVICE_ROW_TOOLTIP_STATIC = "Dòng dịch vụ chính trong báo giá.";

export default function PublicQuotationAccessPage() {
  const { token } = useParams();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [note, setNote] = useState("");
  const [otpCode, setOtpCode] = useState("");
  const [sendingOtp, setSendingOtp] = useState(false);
  const [otpSentAt, setOtpSentAt] = useState(null);
  const [otpExpiresAt, setOtpExpiresAt] = useState(null);
  const [otpRemainingSeconds, setOtpRemainingSeconds] = useState(0);
  const [pendingDecision, setPendingDecision] = useState(null);
  const [quotation, setQuotation] = useState(null);
  const [collapsedPartGroups, setCollapsedPartGroups] = useState({});
  const isAutoSubmittingOtpRef = useRef(false);
  const lastAutoSubmittedOtpRef = useRef("");

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
  const isOtpStepVisible = pendingDecision !== null;
  const isExpiredLink = useMemo(() => isExpiredPublicQuotationLinkMessage(error), [error]);

  const lineItems = useMemo(
    () => (Array.isArray(quotation?.details) ? quotation.details : []),
    [quotation?.details],
  );

  const groupRowsByService = useCallback((rows) => {
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
  }, []);

  const detailRowsByServiceType = useMemo(() => {
    const initialRows = [];
    const supplementaryRows = [];
    lineItems.forEach((row) => {
      if (isSupplementaryRow(row)) supplementaryRows.push(row);
      else initialRows.push(row);
    });
    return { initialRows, supplementaryRows };
  }, [lineItems]);

  const groupedRowsByServiceType = useMemo(() => ({
    initialGroups: groupRowsByService(detailRowsByServiceType.initialRows),
    supplementaryGroups: groupRowsByService(detailRowsByServiceType.supplementaryRows),
  }), [detailRowsByServiceType.initialRows, detailRowsByServiceType.supplementaryRows, groupRowsByService]);

  const vatSummary = useMemo(() => {
    const groups = new Map();
    let baseBeforeVat = 0;
    let totalVat = 0;

    lineItems.forEach((row) => {
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
  }, [lineItems, quotation?.finalAmount, quotation?.subTotalAmount, quotation?.taxAmount]);

  const executionCostSummary = useMemo(() => {
    let laborBeforeVat = 0;
    let partsAndMaterialBeforeVat = 0;

    lineItems.forEach((row) => {
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
  }, [lineItems]);

  const paymentSummary = useMemo(() => {
    const totalRepairCost = Number(vatSummary.totalWithVat || 0);
    const insuranceCompensation = Number(quotation?.totalInsuranceAmount || 0);
    const baselineCustomerPayable = Math.max(totalRepairCost - insuranceCompensation, 0);
    const customerPayable = Number(
      quotation?.totalCustomerAmount ?? baselineCustomerPayable,
    );
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
        {groups.length === 0 ? (
          <tbody>
            <tr>
              <td colSpan={5} className="quote-detail-empty">{emptyMessage}</td>
            </tr>
          </tbody>
        ) : groups.map((group) => {
            const collapseKey = `${sectionKey}-${group.key}`;
            const canToggle = group.partRows.length > 0;
            const isCollapsed = canToggle ? Boolean(collapsedPartGroups[collapseKey]) : false;
            const visibleRows = [
              ...group.serviceRows.map((row) => ({ row, isService: true })),
              ...(isCollapsed ? [] : group.partRows.map((row) => ({ row, isService: false }))),
            ];
            return (
              <tbody key={`${title}-${group.key}`} className="item-group-body">
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
                      <td className={isService ? "quote-portal-item-service" : "quote-portal-item-part"}>
                        <div className={isService ? "service-name-cell" : "part-name-cell"}>
                          {!isService ? <span className="quote-portal-item-prefix">└─</span> : null}
                          {row.itemName}
                        </div>
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
              </tbody>
            );
          })}
      </table>
    </div>
  );

  const submitDecision = useCallback(async (approved, otpOverride = null) => {
    if (!token) return;
    const normalizedOtp = String(otpOverride ?? otpCode ?? "").trim();
    if (!/^\d{6}$/.test(normalizedOtp)) {
      setError("Vui lòng nhập đúng mã OTP gồm 6 số trước khi xác nhận.");
      return;
    }

    setSaving(true);
    setError("");
    setMessage("");
    try {
      const payload = {
        approved,
        note: (note || "").trim() || null,
        otpCode: normalizedOtp,
      };
      await serviceOrderApi.applyQuotationPublicDecision(token, payload);
      setMessage(approved ? "Bạn đã xác nhận duyệt báo giá thành công." : "Bạn đã từ chối báo giá.");
      setPendingDecision(null);
      setOtpCode("");
      setOtpSentAt(null);
      setOtpExpiresAt(null);
      setOtpRemainingSeconds(0);
      await loadQuotation();
    } catch (err) {
      setError(err?.message || "Không thể ghi nhận quyết định.");
    } finally {
      setSaving(false);
      isAutoSubmittingOtpRef.current = false;
    }
  }, [loadQuotation, note, otpCode, token]);

  async function startOtpFlow(approved) {
    if (!token) return;
    if (approved && !quotation?.canApprove) return;
    if (!approved && !quotation?.canReject) return;

    setPendingDecision(approved);
    setOtpCode("");
    lastAutoSubmittedOtpRef.current = "";
    isAutoSubmittingOtpRef.current = false;
    setSendingOtp(true);
    setError("");
    setMessage("");
    try {
      const data = await serviceOrderApi.sendQuotationPublicDecisionOtp(token, {});
      const sentAt = new Date();
      const expiresRaw = readField(data, "otpExpiresAt", "OtpExpiresAt");
      const parsedExpiresAt = expiresRaw ? new Date(expiresRaw) : null;
      const resolvedExpiresAt = parsedExpiresAt && !Number.isNaN(parsedExpiresAt.getTime())
        ? parsedExpiresAt
        : new Date(sentAt.getTime() + 60 * 1000);

      setOtpSentAt(sentAt);
      setOtpExpiresAt(resolvedExpiresAt);
      setOtpRemainingSeconds(Math.max(0, Math.ceil((resolvedExpiresAt.getTime() - Date.now()) / 1000)));
      const masked = data?.maskedEmail ? ` (${data.maskedEmail})` : "";
      setMessage(`Mã OTP đã được gửi${masked}. Vui lòng nhập mã để ${approved ? "duyệt" : "từ chối"} báo giá.`);
      setTimeout(() => {
        const wrap = document.querySelector(".quote-portal-otp-wrap");
        if (wrap) wrap.scrollIntoView({ behavior: "smooth", block: "center" });
        const otpInput = document.querySelector(".quote-portal-otp-input");
        if (otpInput) otpInput.focus();
      }, 50);
    } catch (err) {
      setPendingDecision(null);
      setError(err?.message || "Không thể gửi mã OTP.");
    } finally {
      setSendingOtp(false);
    }
  }

  useEffect(() => {
    if (!isOtpStepVisible || !otpExpiresAt) {
      setOtpRemainingSeconds(0);
      return undefined;
    }

    const updateRemaining = () => {
      const next = Math.max(0, Math.ceil((otpExpiresAt.getTime() - Date.now()) / 1000));
      setOtpRemainingSeconds(next);
    };

    updateRemaining();
    const timerId = setInterval(updateRemaining, 1000);
    return () => clearInterval(timerId);
  }, [isOtpStepVisible, otpExpiresAt]);

  useEffect(() => {
    if (pendingDecision === null) return;
    if (sendingOtp || saving) return;
    if (otpRemainingSeconds <= 0) return;
    if (!/^\d{6}$/.test(otpCode)) return;
    if (isAutoSubmittingOtpRef.current) return;
    if (lastAutoSubmittedOtpRef.current === otpCode) return;

    isAutoSubmittingOtpRef.current = true;
    lastAutoSubmittedOtpRef.current = otpCode;
    void submitDecision(pendingDecision, otpCode);
  }, [otpCode, otpRemainingSeconds, pendingDecision, saving, sendingOtp, submitDecision]);

  if (loading) {
    return <div className="quote-portal-loading">Đang tải báo giá...</div>;
  }

  if (!quotation) {
    return (
      <div className="quote-portal">
        <div className="quote-portal-shell">
          <section className="quote-portal-card quote-portal-empty-state">
            <h2>Không thể truy cập báo giá</h2>
            <p className="quote-portal-empty-hint">
              {error || "Không tìm thấy báo giá công khai tương ứng với liên kết này."}
            </p>
            {isExpiredLink ? (
              <p className="quote-portal-alert quote-portal-alert-expired">
                Liên kết báo giá đã hết hạn hoặc không còn hiệu lực. Vui lòng liên hệ cố vấn dịch vụ để được cấp liên kết mới.
              </p>
            ) : null}
          </section>
        </div>
      </div>
    );
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

        <section className="quote-portal-card">
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

            <div className="summary-subtitle">Phân tích giá trị (Chưa VAT):</div>
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
              <span className="summary-line-label">TỔNG TIỀN HÀNG (CHƯA VAT)</span>
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
              <span className="summary-line-label">TỔNG CHI PHÍ SỬA CHỮA</span>
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
          {isOtpStepVisible ? (
            <div className="quote-portal-otp-wrap">
              <input
                type="text"
                inputMode="numeric"
                maxLength={6}
                className="quote-portal-otp-input"
                value={otpCode}
                onChange={(e) => {
                  const nextOtp = (e.target.value || "").replace(/\D/g, "").slice(0, 6);
                  setOtpCode(nextOtp);
                }}
                placeholder="Nhập OTP 6 số"
                disabled={saving}
              />
              <button
                type="button"
                className="quote-portal-btn quote-portal-btn-outline"
                onClick={() => {
                  if (pendingDecision !== null) {
                    void startOtpFlow(pendingDecision);
                  }
                }}
                disabled={saving || sendingOtp || pendingDecision === null}
              >
                {sendingOtp ? "Đang gửi OTP..." : "Gửi lại OTP"}
              </button>
              {otpSentAt ? (
                <span className="quote-portal-otp-hint">
                  OTP đã gửi lúc {otpSentAt.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}.{" "}
                  {otpRemainingSeconds > 0
                    ? `Còn hiệu lực ${formatCountdown(otpRemainingSeconds)}. Nhập đủ 6 số để xác nhận tự động.`
                    : "OTP đã hết hạn. Vui lòng bấm Gửi lại OTP."}
                </span>
              ) : null}
            </div>
          ) : null}
          <button
            type="button"
            className="quote-portal-btn quote-portal-btn-approve"
            onClick={() => startOtpFlow(true)}
            disabled={saving || sendingOtp || !quotation.canApprove}
          >
            Đồng ý toàn bộ
          </button>
          <button
            type="button"
            className="quote-portal-btn quote-portal-btn-reject"
            onClick={() => startOtpFlow(false)}
            disabled={saving || sendingOtp || !quotation.canReject}
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

      {quotation?.canApprove && (
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
                onClick={() => startOtpFlow(true)}
                disabled={saving || sendingOtp || !quotation?.canApprove}
              >
                Đồng ý duyệt
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

