export const INSURANCE_DEDUCTIBLE_OPTIONS = [
  { value: "500K", label: "500.000đ" },
  { value: "1000K", label: "1.000.000đ" },
  { value: "CONSULT", label: "Tôi không nhớ / cần tư vấn" },
];

export const INSURANCE_CLAIM_MODE_OPTIONS = [
  { value: "LINKED", label: "Gara liên kết / bảo lãnh" },
  { value: "SELF_PAY", label: "Khách tự túc, gara hỗ trợ hồ sơ" },
  { value: "ADVISOR", label: "Chưa rõ, cần tư vấn" },
];

export const INSURANCE_QUOTE_STATE = {
  NEEDS_ADVISOR: "NEEDS_ADVISOR",
  PENDING_REVIEW: "PENDING_REVIEW",
};

export const INSURANCE_ASSET_TYPE = {
  CARD: "CARD",
  DAMAGE: "DAMAGE",
};

export const INSURANCE_CARD_IMAGE_MAX = 2;
export const INSURANCE_DAMAGE_IMAGE_MAX = 6;

const INSURANCE_PACKAGE_PREFIX = "PKG-BH";
const INSURANCE_FORM_MODE = {
  BOOKING: "BOOKING",
  INTERNAL: "INTERNAL",
};

function toPositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeImageUrls(rawUrls, maxCount) {
  if (!Array.isArray(rawUrls)) return [];

  const seen = new Set();
  const result = [];
  rawUrls.forEach((item) => {
    if (result.length >= maxCount) return;
    const url = String(item || "").trim();
    if (!url) return;
    const dedupeKey = url.toLowerCase();
    if (seen.has(dedupeKey)) return;
    seen.add(dedupeKey);
    result.push(url);
  });

  return result;
}

function normalizeInsuranceFormMode(options) {
  const rawMode = String(options?.mode || "").trim().toUpperCase();
  return rawMode === INSURANCE_FORM_MODE.BOOKING
    ? INSURANCE_FORM_MODE.BOOKING
    : INSURANCE_FORM_MODE.INTERNAL;
}

function buildInsuranceIntakeNote(mode) {
  return mode === INSURANCE_FORM_MODE.BOOKING
    ? "Mức miễn thường và hình thức xử lý sẽ được cố vấn xác nhận khi tiếp nhận xe."
    : "Thông tin bảo hiểm đã đủ để lễ tân chốt tiếp nhận và tư vấn.";
}

function formatImageCount(count, singularLabel) {
  if (!Number.isFinite(count) || count <= 0) return "";
  return `${count} ${singularLabel}`;
}

export function formatInsuranceCurrency(value) {
  const amount = Number(value);
  if (!Number.isFinite(amount) || amount <= 0) return "Chờ tư vấn";
  return `${amount.toLocaleString("vi-VN")}đ`;
}

export function createEmptyInsuranceSnapshot(overrides = {}) {
  return {
    insuranceCompanyId: "",
    insuranceDeductibleOption: "",
    insuranceDeductibleAmount: null,
    insuranceClaimMode: "",
    insuranceQuoteState: "",
    insuranceCardImageUrls: [],
    insuranceDamageImageUrls: [],
    ...overrides,
  };
}

export function isInsuranceBookingService(service) {
  const categoryId = Number(service?.categoryId ?? service?.CategoryId ?? 0);
  if (Number.isFinite(categoryId) && categoryId === 7) return true;

  const code = String(service?.code ?? service?.Code ?? "").trim().toUpperCase();
  return code.startsWith(INSURANCE_PACKAGE_PREFIX);
}

export function hasInsuranceServices(serviceIds, serviceById) {
  return (Array.isArray(serviceIds) ? serviceIds : []).some((serviceId) =>
    isInsuranceBookingService(serviceById?.get(Number(serviceId)))
  );
}

export function getInsuranceBookingItems(serviceIds, serviceById) {
  return (Array.isArray(serviceIds) ? serviceIds : [])
    .map((serviceId) => serviceById?.get(Number(serviceId)))
    .filter((service) => Boolean(service) && isInsuranceBookingService(service));
}

export function getInsuranceBookingItemNames(serviceIds, serviceById) {
  return Array.from(
    new Set(
      getInsuranceBookingItems(serviceIds, serviceById)
        .map((service) => String(service?.name ?? service?.Name ?? "").trim())
        .filter(Boolean)
    )
  );
}

export function resolveInsuranceVehicleKeys(vehicleKeys, serviceIdsByVehicleKey, serviceById) {
  return (Array.isArray(vehicleKeys) ? vehicleKeys : []).filter((vehicleKey) =>
    hasInsuranceServices(serviceIdsByVehicleKey?.get(vehicleKey) || [], serviceById)
  );
}

export function resolveInsuranceDeductibleAmount(option) {
  const normalized = String(option || "").trim().toUpperCase();
  if (normalized === "500K") return 500000;
  if (normalized === "1000K") return 1000000;
  return null;
}

export function resolveInsuranceQuoteState(deductibleOption, claimMode) {
  const normalizedDeductible = String(deductibleOption || "").trim().toUpperCase();
  const normalizedClaimMode = String(claimMode || "").trim().toUpperCase();

  if (normalizedDeductible === "CONSULT" || normalizedClaimMode === "ADVISOR") {
    return INSURANCE_QUOTE_STATE.NEEDS_ADVISOR;
  }

  if (!normalizedDeductible || !normalizedClaimMode) {
    return "";
  }

  return INSURANCE_QUOTE_STATE.PENDING_REVIEW;
}

export function normalizeInsuranceSnapshot(snapshot) {
  const insuranceCompanyId = toPositiveId(snapshot?.insuranceCompanyId);
  const insuranceDeductibleOption = String(snapshot?.insuranceDeductibleOption || "")
    .trim()
    .toUpperCase();
  const insuranceClaimMode = String(snapshot?.insuranceClaimMode || "")
    .trim()
    .toUpperCase();
  const derivedAmount = resolveInsuranceDeductibleAmount(insuranceDeductibleOption);
  const explicitAmount = Number(snapshot?.insuranceDeductibleAmount);
  const insuranceDeductibleAmount =
    derivedAmount !== null
      ? derivedAmount
      : Number.isFinite(explicitAmount) && explicitAmount >= 0
        ? explicitAmount
        : null;
  const insuranceQuoteState =
    String(snapshot?.insuranceQuoteState || "").trim().toUpperCase() ||
    resolveInsuranceQuoteState(insuranceDeductibleOption, insuranceClaimMode);

  return createEmptyInsuranceSnapshot({
    insuranceCompanyId: insuranceCompanyId || "",
    insuranceDeductibleOption,
    insuranceDeductibleAmount,
    insuranceClaimMode,
    insuranceQuoteState,
    insuranceCardImageUrls: normalizeImageUrls(snapshot?.insuranceCardImageUrls, INSURANCE_CARD_IMAGE_MAX),
    insuranceDamageImageUrls: normalizeImageUrls(snapshot?.insuranceDamageImageUrls, INSURANCE_DAMAGE_IMAGE_MAX),
  });
}

export function getInsuranceDeductibleLabel(option) {
  const normalized = String(option || "").trim().toUpperCase();
  const matched = INSURANCE_DEDUCTIBLE_OPTIONS.find((item) => item.value === normalized);
  return matched?.label || "Chưa chọn";
}

export function getInsuranceClaimModeLabel(mode) {
  const normalized = String(mode || "").trim().toUpperCase();
  const matched = INSURANCE_CLAIM_MODE_OPTIONS.find((item) => item.value === normalized);
  return matched?.label || "Chưa chọn";
}

export function validateInsuranceSnapshot(snapshot) {
  return validateInsuranceSnapshotByMode(snapshot, {});
}

export function validateInsuranceSnapshotByMode(snapshot, options = {}) {
  const normalized = normalizeInsuranceSnapshot(snapshot);
  const mode = normalizeInsuranceFormMode(options);

  if (!toPositiveId(normalized.insuranceCompanyId)) {
    return "Vui lòng chọn công ty bảo hiểm.";
  }

  if (mode === INSURANCE_FORM_MODE.BOOKING) {
    return "";
  }

  if (!normalized.insuranceDeductibleOption) {
    return "Vui lòng chọn mức miễn thường hoặc tư vấn.";
  }

  if (!normalized.insuranceClaimMode) {
    return "Vui lòng chọn hình thức xử lý bảo hiểm.";
  }

  return "";
}

export function buildInsuranceSummary(snapshot, companyNameById, options = {}) {
  const normalized = normalizeInsuranceSnapshot(snapshot);
  const mode = normalizeInsuranceFormMode(options);
  const companyId = toPositiveId(normalized.insuranceCompanyId);
  const companyName = companyId > 0 ? companyNameById?.get(companyId) || `Công ty #${companyId}` : "Chưa chọn";
  const estimatedCustomerPayAmount = Number(normalized.insuranceDeductibleAmount);
  const cardImageCount = normalized.insuranceCardImageUrls.length;
  const damageImageCount = normalized.insuranceDamageImageUrls.length;
  const assetSummaryLabel = [
    formatImageCount(cardImageCount, cardImageCount > 1 ? "ảnh thẻ bảo hiểm" : "ảnh thẻ bảo hiểm"),
    formatImageCount(damageImageCount, damageImageCount > 1 ? "ảnh hiện trạng" : "ảnh hiện trạng"),
  ]
    .filter(Boolean)
    .join(", ") || "Chưa tải ảnh";

  return {
    companyName,
    intakeNote: buildInsuranceIntakeNote(mode),
    intakeStatusLabel:
      mode === INSURANCE_FORM_MODE.BOOKING
        ? "Xác nhận khi ti?p nh?n"
        : normalized.insuranceQuoteState === INSURANCE_QUOTE_STATE.NEEDS_ADVISOR
          ? "Chờ tư vấn"
          : "Đã có snapshot chi tiết",
    deductibleLabel: getInsuranceDeductibleLabel(normalized.insuranceDeductibleOption),
    claimModeLabel: getInsuranceClaimModeLabel(normalized.insuranceClaimMode),
    estimatedCustomerPayAmount: Number.isFinite(estimatedCustomerPayAmount) ? estimatedCustomerPayAmount : null,
    estimatedCustomerPayLabel: formatInsuranceCurrency(estimatedCustomerPayAmount),
    cardImageCount,
    damageImageCount,
    assetSummaryLabel,
    insuranceCardImageUrls: normalized.insuranceCardImageUrls,
    insuranceDamageImageUrls: normalized.insuranceDamageImageUrls,
  };
}

export function buildInsuranceSnapshotPayload(snapshot, options = {}) {
  const normalized = normalizeInsuranceSnapshot(snapshot);
  const mode = normalizeInsuranceFormMode(options);
  const insuranceCompanyId = toPositiveId(normalized.insuranceCompanyId);

  if (mode === INSURANCE_FORM_MODE.BOOKING) {
    return {
      insuranceCompanyId: insuranceCompanyId > 0 ? insuranceCompanyId : null,
      insuranceDeductibleAmount: null,
      insuranceDeductibleOption: null,
      insuranceClaimMode: null,
      insuranceQuoteState: insuranceCompanyId > 0 ? INSURANCE_QUOTE_STATE.NEEDS_ADVISOR : null,
      insuranceCardImageUrls: normalized.insuranceCardImageUrls,
      insuranceDamageImageUrls: normalized.insuranceDamageImageUrls,
    };
  }

  return {
    insuranceCompanyId: insuranceCompanyId > 0 ? insuranceCompanyId : null,
    insuranceDeductibleAmount:
      Number.isFinite(Number(normalized.insuranceDeductibleAmount)) &&
      Number(normalized.insuranceDeductibleAmount) >= 0
        ? Number(normalized.insuranceDeductibleAmount)
        : null,
    insuranceDeductibleOption: normalized.insuranceDeductibleOption || null,
    insuranceClaimMode: normalized.insuranceClaimMode || null,
    insuranceQuoteState: normalized.insuranceQuoteState || null,
    insuranceCardImageUrls: normalized.insuranceCardImageUrls,
    insuranceDamageImageUrls: normalized.insuranceDamageImageUrls,
  };
}

