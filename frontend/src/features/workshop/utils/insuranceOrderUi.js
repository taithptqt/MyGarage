export const INSURANCE_ORDER_CATEGORY = "INSURANCE_REPAIR";

const CUSTOMER_CHARGE_SERVICE_CODES = new Set([
  "BH-MT500",
  "BH-MT1000",
  "BH-KHAUHAO",
  "BH-HOSO",
  "BH-LUUBAI",
]);

const INSURANCE_STATUS_LABELS = {
  NOT_APPLICABLE: "Không áp dụng",
  PENDING_SUBMISSION: "Chờ gửi bảo hiểm",
  SUBMITTED: "Đã gửi bảo hiểm",
  UNDER_REVIEW: "Đang duyệt",
  APPROVED: "Đã duyệt",
  PARTIALLY_APPROVED: "Duyệt một phần",
  REJECTED: "Từ chối",
  SETTLED: "Đã đối soát",
};

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function toMoney(value, fallback = 0) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(0, parsed);
}

export function normalizeServiceCode(service) {
  return String(service?.code || service?.Code || "").trim().toUpperCase();
}

export function isInsuranceSignalService(service) {
  const code = normalizeServiceCode(service);
  const categoryId = toPositiveNumber(service?.categoryId || service?.CategoryId);
  return categoryId === 7 || code.startsWith("BH-") || code.startsWith("PKG-BH");
}

export function isInsuranceCustomerChargeService(service) {
  return CUSTOMER_CHARGE_SERVICE_CODES.has(normalizeServiceCode(service));
}

export function isInsuranceOrderActive({ insuranceEnabled, orderCategory, insuranceCompanyId, services }) {
  if (Boolean(insuranceEnabled)) return true;
  if (String(orderCategory || "").trim().toUpperCase() === INSURANCE_ORDER_CATEGORY) return true;
  if (toPositiveNumber(insuranceCompanyId) > 0) return true;
  return Array.isArray(services) && services.some(isInsuranceSignalService);
}

export function buildInsuranceDraftBreakdown({ services, enabled }) {
  const normalizedServices = Array.isArray(services) ? services.filter(Boolean) : [];
  const totalAmount = normalizedServices.reduce(
    (sum, service) => sum + toMoney(service?.price ?? service?.basePrice ?? service?.BasePrice),
    0
  );

  if (!enabled) {
    return {
      totalAmount,
      insuranceRequestedAmount: 0,
      customerPayAmount: totalAmount,
      insuranceServiceCount: 0,
      customerChargeServiceCount: 0,
    };
  }

  return normalizedServices.reduce(
    (summary, service) => {
      const amount = toMoney(service?.price ?? service?.basePrice ?? service?.BasePrice);
      if (isInsuranceCustomerChargeService(service)) {
        summary.customerPayAmount += amount;
        summary.customerChargeServiceCount += 1;
      } else {
        summary.insuranceRequestedAmount += amount;
        summary.insuranceServiceCount += 1;
      }
      return summary;
    },
    {
      totalAmount,
      insuranceRequestedAmount: 0,
      customerPayAmount: 0,
      insuranceServiceCount: 0,
      customerChargeServiceCount: 0,
    }
  );
}

function resolveDetailAmount(detail) {
  const finalAmount = toMoney(detail?.finalAmount ?? detail?.FinalAmount, -1);
  if (finalAmount >= 0) return finalAmount;

  const quantity = toMoney(detail?.quantity ?? detail?.Quantity, 1) || 1;
  const unitPrice = toMoney(detail?.unitPrice ?? detail?.UnitPrice ?? detail?.unitPriceAtTime ?? detail?.UnitPriceAtTime);
  return quantity * unitPrice;
}

export function summarizeInsuranceOrderDetails(details) {
  const normalizedDetails = Array.isArray(details) ? details.filter(Boolean) : [];
  const baseSummary = {
    hasInsurance: false,
    totalAmount: 0,
    insuranceRequestedAmount: 0,
    insuranceApprovedAmount: 0,
    customerPayAmount: 0,
    garageAbsorbedAmount: 0,
    pendingInsuranceItemCount: 0,
    companyIds: [],
  };

  if (normalizedDetails.length === 0) {
    return baseSummary;
  }

  const companyIdSet = new Set();

  const summary = normalizedDetails.reduce((accumulator, detail) => {
    const amount = resolveDetailAmount(detail);
    const payerType = String(detail?.payerType || detail?.PayerType || "").trim().toUpperCase();
    const insuranceCompanyId = toPositiveNumber(detail?.insuranceCompanyId || detail?.InsuranceCompanyId);
    const insuranceRequestedAmount = toMoney(
      detail?.insuranceRequestedAmount ?? detail?.InsuranceRequestedAmount
    );
    const insuranceApprovedAmount = toMoney(
      detail?.insuranceApprovedAmount ?? detail?.InsuranceApprovedAmount
    );
    const customerCoPayAmount = toMoney(detail?.customerCoPayAmount ?? detail?.CustomerCoPayAmount);
    const garageAbsorbedAmount = toMoney(detail?.garageAbsorbedAmount ?? detail?.GarageAbsorbedAmount);
    const insuranceStatus = String(detail?.insuranceStatus || detail?.InsuranceStatus || "")
      .trim()
      .toUpperCase();

    accumulator.totalAmount += amount;
    accumulator.insuranceRequestedAmount += insuranceRequestedAmount;
    accumulator.insuranceApprovedAmount += insuranceApprovedAmount;
    accumulator.customerPayAmount += customerCoPayAmount;
    accumulator.garageAbsorbedAmount += garageAbsorbedAmount;

    if (insuranceCompanyId > 0) {
      companyIdSet.add(insuranceCompanyId);
    }

    const isInsuranceLine =
      payerType === "INSURANCE" ||
      insuranceCompanyId > 0 ||
      insuranceRequestedAmount > 0 ||
      insuranceApprovedAmount > 0;

    if (isInsuranceLine) {
      accumulator.hasInsurance = true;
      if (!insuranceStatus || insuranceStatus === "PENDING_SUBMISSION" || insuranceStatus === "UNDER_REVIEW" || insuranceStatus === "SUBMITTED") {
        accumulator.pendingInsuranceItemCount += 1;
      }
    }

    if (payerType === "CUSTOMER" && customerCoPayAmount <= 0) {
      accumulator.customerPayAmount += amount;
    }

    return accumulator;
  }, baseSummary);

  summary.companyIds = Array.from(companyIdSet.values());
  return summary;
}

export function formatInsuranceStatusLabel(status) {
  const normalizedStatus = String(status || "").trim().toUpperCase();
  return INSURANCE_STATUS_LABELS[normalizedStatus] || normalizedStatus || "--";
}

export function formatDetailPayerLabel(detail) {
  const payerType = String(detail?.payerType || detail?.PayerType || "").trim().toUpperCase();
  if (payerType === "INSURANCE") return "Bảo hiểm";
  if (payerType === "CUSTOMER") return "Khách hàng";
  return "Chưa phân loại";
}
