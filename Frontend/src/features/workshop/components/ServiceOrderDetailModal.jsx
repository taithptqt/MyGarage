import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { Modal } from "@features/admin/components/AdminModals";
import { MdAssignment, MdCheckCircle, MdDirectionsCar, MdEdit, MdHistory, MdPerson } from "react-icons/md";
import { insuranceCompanyApi } from "@features/workshop/api/insuranceCompanyApi";
import InsuranceBillingOverview from "@features/workshop/components/InsuranceBillingOverview";
import { formatOrderDateTime, parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";
import { getVehicleBrands, getVehicleModels } from "@features/appointments/api/appointmentApi";
import { servicesApi } from "@features/services/api/servicesApi";
import FilterDropdown from "@shared/components/FilterDropdown";
import { summarizeInsuranceOrderDetails } from "@features/workshop/utils/insuranceOrderUi";
import "../pages/Workshop.css";

const STATUS_LABELS = {
  NEW: "Mới tiếp nhận",
  RECEPTION: "Lễ tân tiếp nhận",
  DIAGNOSING: "Đang chẩn đoán",
  WAITING_APPROVAL: "Chờ duyệt",
  QUOTING: "Đang làm báo giá",
  SENT_TO_CUSTOMER: "Đã gửi khách hàng",
  APPROVED: "Quản đốc đã duyệt",
  CUSTOMER_APPROVED: "Khách hàng đã duyệt",
  CUSTOMER_REJECTED: "Khách hàng từ chối",
  REPAIRING: "Đang sửa chữa",
  QC_PENDING: "Chờ kiểm tra chất lượng",
  READY: "Sẵn sàng giao xe",
  COMPLETED: "Hoàn tất",
  CANCELED: "Đã hủy",
};

const ORDER_CATEGORY_LABELS = {
  MAINTENANCE: "Bảo dưỡng",
  WARRANTLY: "Bảo hành",
  WARRANTY: "Bảo hành",
  QUICK_SERVICE: "Dịch vụ nhanh",
  INSURANCE_REPAIR: "Sửa chữa bảo hiểm",
  GENERAL_REPAIR: "Sửa chữa chung",
  REWORK: "Sửa chữa lại",
  REPAIR: "Sửa chữa",
};

const DISPATCH_TAG_OPTIONS = [
  { value: "ENGINE_CHASSIS", label: "Máy - Gầm", description: "Động cơ, hộp số, hệ thống gầm." },
  { value: "ELECTRICAL_AC", label: "Điện - Máy lạnh", description: "Điện thân xe, điện động cơ, điều hòa." },
  { value: "BODY_PAINT", label: "Đồng sơn - Chăm sóc xe", description: "Gò, hàn, sơn và chăm sóc ngoại thất." },
  { value: "MAINTENANCE", label: "Bảo dưỡng nhanh", description: "Bảo dưỡng định kỳ và hạng mục nhanh." },
];
const AUTO_DISPATCH_WARNING_PREFIX = "[AUTO_DISPATCH_WARNING]:";
const DIAGNOSIS_DISPATCH_REVIEW_STORAGE_KEY = "mygarage.diagnosisDispatchReviewOrders";
const MOJIBAKE_MARKER_REGEX = /(?:\u00C3\u0192|\u00C3\u201A|\u00C3\u0090|\u00C3\u2020|\u00C3\u201E|\u00C3\u00A1\u00C2[\u00BA-\u00BF]|\u00C3\u00A2\u00E2\u201A\u00AC|\uFFFD)/;
const MOJIBAKE_MARKER_GLOBAL_REGEX = /(?:\u00C3\u0192|\u00C3\u201A|\u00C3\u0090|\u00C3\u2020|\u00C3\u201E|\u00C3\u00A1\u00C2[\u00BA-\u00BF]|\u00C3\u00A2\u00E2\u201A\u00AC|\uFFFD)/g;
const VIETNAMESE_MARK_REGEX = /[À-ỹĐđ]/g;

function parseDispatchReviewOrderIds() {
  if (typeof window === "undefined") return new Set();
  try {
    const raw = window.localStorage.getItem(DIAGNOSIS_DISPATCH_REVIEW_STORAGE_KEY);
    const list = JSON.parse(raw || "[]");
    if (!Array.isArray(list)) return new Set();
    return new Set(
      list
        .map((value) => Number(value))
        .filter((value) => Number.isFinite(value) && value > 0)
    );
  } catch {
    return new Set();
  }
}

function persistDispatchReviewOrderIds(idSet) {
  if (typeof window === "undefined") return;
  const values = Array.from(idSet).filter((value) => Number.isFinite(value) && value > 0);
  window.localStorage.setItem(DIAGNOSIS_DISPATCH_REVIEW_STORAGE_KEY, JSON.stringify(values));
}

function markOrderNeedsDispatchReview(orderId) {
  const normalizedOrderId = Number(orderId);
  if (!Number.isFinite(normalizedOrderId) || normalizedOrderId <= 0) return;
  const ids = parseDispatchReviewOrderIds();
  ids.add(normalizedOrderId);
  persistDispatchReviewOrderIds(ids);
}

function clearOrderDispatchReviewMark(orderId) {
  const normalizedOrderId = Number(orderId);
  if (!Number.isFinite(normalizedOrderId) || normalizedOrderId <= 0) return;
  const ids = parseDispatchReviewOrderIds();
  if (!ids.has(normalizedOrderId)) return;
  ids.delete(normalizedOrderId);
  persistDispatchReviewOrderIds(ids);
}

function formatStatusLabel(status) {
  const key = String(status || "").toUpperCase();
  return STATUS_LABELS[key] || key || "Không xác định";
}

function formatOrderCategoryLabel(orderCategory) {
  const key = String(orderCategory || "").toUpperCase();
  if (!key) return "Sửa chữa";
  return ORDER_CATEGORY_LABELS[key] || key.replace(/_/g, " ");
}

function firstText(...values) {
  for (const value of values) {
    const text = String(value || "").trim();
    if (text) return text;
  }
  return "";
}

function toPositiveNumber(...values) {
  for (const value of values) {
    const parsed = Number(value);
    if (Number.isFinite(parsed) && parsed > 0) return parsed;
  }
  return 0;
}

function formatMoney(amount) {
  const value = Number(amount || 0);
  return `${value.toLocaleString("vi-VN")}d`;
}

function parseApiDate(value) {
  if (!value) return null;
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value;
  }

  const raw = String(value).trim();
  if (!raw) return null;
  const hasTimezone = /(?:Z|[+\-]\d{2}:\d{2})$/i.test(raw);
  const normalized = hasTimezone ? raw : `${raw}Z`;
  const parsed = new Date(normalized);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function toApiDateMillis(value) {
  return parseApiDate(value)?.getTime() ?? 0;
}

function formatDateTimeValue(value) {
  const parsed = parseApiDate(value);
  if (!parsed) return "--";
  return parsed.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function extractAutoDispatchWarning(note) {
  const raw = String(note || "").trim();
  if (!raw || !raw.startsWith(AUTO_DISPATCH_WARNING_PREFIX)) return "";
  return raw.slice(AUTO_DISPATCH_WARNING_PREFIX.length).trim();
}

function buildIntakePackageLabel(item) {
  const code = String(item?.code || item?.Code || "").trim().toUpperCase();
  const name = String(item?.name || item?.Name || "").trim();
  const nominalMinutes = Number(item?.nominalMinutes ?? item?.NominalMinutes ?? 0);
  if (name && nominalMinutes > 0 && !name.includes(`(${nominalMinutes}p)`)) {
    return `${name} (${nominalMinutes}p)`;
  }
  return name || code || "--";
}

function buildAutoDiagnosisAssignmentNote({
  selectedServiceNames,
}) {
  const services = Array.isArray(selectedServiceNames)
    ? selectedServiceNames.map((value) => String(value || "").trim()).filter(Boolean)
    : [];

  const serviceText = services.length > 0 ? services.join(", ") : "chưa có tên dịch vụ cụ thể";
  return `👉 [Lễ tân] Ghi chú gửi thợ: ${serviceText}.`;
}

function normalizeImageUrls(value) {
  if (Array.isArray(value)) {
    return value
      .map((item) => String(item || "").trim())
      .filter(Boolean);
  }

  const text = String(value || "").trim();
  if (!text) return [];

  try {
    const parsed = JSON.parse(text);
    if (Array.isArray(parsed)) {
      return parsed
        .map((item) => String(item || "").trim())
        .filter(Boolean);
    }
  } catch {
    // Ignore JSON parse errors and fallback to split.
  }

  return text
    .split(/[,\n]/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

function tryRepairMojibakeText(value) {
  const raw = String(value || "");
  if (!raw || !MOJIBAKE_MARKER_REGEX.test(raw) || typeof TextDecoder === "undefined") {
    return raw;
  }

  const cp1252Map = new Map([
    [0x20AC, 0x80], [0x201A, 0x82], [0x0192, 0x83], [0x201E, 0x84],
    [0x2026, 0x85], [0x2020, 0x86], [0x2021, 0x87], [0x02C6, 0x88],
    [0x2030, 0x89], [0x0160, 0x8A], [0x2039, 0x8B], [0x0152, 0x8C],
    [0x017D, 0x8E], [0x2018, 0x91], [0x2019, 0x92], [0x201C, 0x93],
    [0x201D, 0x94], [0x2022, 0x95], [0x2013, 0x96], [0x2014, 0x97],
    [0x02DC, 0x98], [0x2122, 0x99], [0x0161, 0x9A], [0x203A, 0x9B],
    [0x0153, 0x9C], [0x017E, 0x9E], [0x0178, 0x9F],
  ]);
  const toCp1252Bytes = (text) => Uint8Array.from(Array.from(text, (char) => {
    const code = char.codePointAt(0) || 0;
    if (code <= 0xff) return code;
    if (cp1252Map.has(code)) return cp1252Map.get(code);
    return 0x3f;
  }));

  const repairChunk = (chunk) => {
    if (!chunk || !MOJIBAKE_MARKER_REGEX.test(chunk)) return chunk;
    try {
      const bytes = toCp1252Bytes(chunk);
      const repairedChunk = new TextDecoder("utf-8").decode(bytes);
      if (!repairedChunk || repairedChunk === chunk) return chunk;
      const chunkReplacementCount = (chunk.match(/\uFFFD/g) || []).length;
      const repairedChunkReplacementCount = (repairedChunk.match(/\uFFFD/g) || []).length;
      if (repairedChunkReplacementCount > chunkReplacementCount) return chunk;
      return repairedChunk;
    } catch {
      return chunk;
    }
  };

  const tokenLevelRepaired = raw
    .split(/(\s+)/)
    .map((chunk) => repairChunk(chunk))
    .join("");
  if (tokenLevelRepaired !== raw) {
    return tokenLevelRepaired;
  }

  try {
    const bytes = toCp1252Bytes(raw);
    const repaired = new TextDecoder("utf-8").decode(bytes).trim();
    if (!repaired || repaired === raw) return raw;

    const rawMarkerCount = (raw.match(MOJIBAKE_MARKER_GLOBAL_REGEX) || []).length;
    const repairedMarkerCount = (repaired.match(MOJIBAKE_MARKER_GLOBAL_REGEX) || []).length;
    const rawVietnameseCount = (raw.match(VIETNAMESE_MARK_REGEX) || []).length;
    const repairedVietnameseCount = (repaired.match(VIETNAMESE_MARK_REGEX) || []).length;
    const rawReplacementCount = (raw.match(/\uFFFD/g) || []).length;
    const repairedReplacementCount = (repaired.match(/\uFFFD/g) || []).length;

    if (repairedReplacementCount > rawReplacementCount) return raw;

    if (
      repairedMarkerCount < rawMarkerCount
      || (repairedMarkerCount === rawMarkerCount && repairedVietnameseCount > rawVietnameseCount)
    ) {
      return repaired;
    }
  } catch {
    // Keep original string if decode fails.
  }

  return raw;
}

function normalizeIntakeSuggestion(response) {
  if (!response || typeof response !== "object") return response || null;

  const normalizeReasonText = (value) => {
    const repaired = tryRepairMojibakeText(value);
    return String(repaired || "")
      .replace(/\bLe tan chon goi\s*/i, "Lễ tân chọn gói ")
      .replace(/\bBooking\s+tiep\s+theo\s+luc\s*/i, "Booking tiếp theo lúc ")
      .replace(/\bBooking\s+ti(?:ế|ế|e)p\s+theo\s+l(?:u|ú|u)c\s*/i, "Booking tiếp theo lúc ")
      .replace(/T.?ng\s+th.?i\s+l.{0,6}ng\s+d.?ch\s+v.?ch\s+ch.?nh\s+.{0,6}m.?c\s+trung\s+b.?nh\.?/i, "Tăng thời lượng dịch vụ chính ở mức trung bình.");
  };

  const reasons = Array.isArray(response.reasons)
    ? response.reasons.map((reason) => normalizeReasonText(reason))
    : response.reasons;

  return {
    ...response,
    reasons,
  };
}

function getCollectionLength(value) {
  if (Array.isArray(value)) return value.length;
  if (!value || typeof value !== "object") return 0;
  const candidates = [
    value.items,
    value.Items,
    value.data,
    value.Data,
    value.rows,
    value.Rows,
    value.results,
    value.Results,
  ];
  for (const item of candidates) {
    if (Array.isArray(item)) return item.length;
  }
  return 0;
}

export default function ServiceOrderDetailModal({
  order,
  onClose,
  onRefresh,
  customers = [],
  vehicles = [],
  staff = [],
}) {
  const navigate = useNavigate();
  const location = useLocation();

  const customer = customers.find(
    (item) => Number(item?.id ?? item?.Id) === Number(order?.customerId ?? order?.CustomerId)
  );
  const vehicle = vehicles.find(
    (item) => Number(item?.id ?? item?.Id ?? item?.vehicleId ?? item?.VehicleId) === Number(order?.vehicleId ?? order?.VehicleId)
  );
  const advisor = staff.find(
    (item) => Number(item?.id ?? item?.Id) === Number(order?.serviceAdvisorId ?? order?.ServiceAdvisorId)
  );

  const parsedNotes = parseCustomerNotes(order?.customerNotes);

  const [orderDetails, setOrderDetails] = useState([]);
  const [insuranceCompanies, setInsuranceCompanies] = useState([]);
  const [vehicleModels, setVehicleModels] = useState([]);
  const [vehicleBrands, setVehicleBrands] = useState([]);
  const [standardServiceNameById, setStandardServiceNameById] = useState(() => new Map());
  const [quotationDetailNameById, setQuotationDetailNameById] = useState(() => new Map());
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [diagnoses, setDiagnoses] = useState([]);
  const [orderSnapshot, setOrderSnapshot] = useState(order || null);
  const [liveOrderStatus, setLiveOrderStatus] = useState(() =>
    String(order?.status || "").trim().toUpperCase()
  );
  const [hasSignedCheckIn, setHasSignedCheckIn] = useState(false);
  const [showDispatchPopup, setShowDispatchPopup] = useState(false);
  const [submittingDiagnosisRequest, setSubmittingDiagnosisRequest] = useState(false);
  const [loadingIntakeSuggestion, setLoadingIntakeSuggestion] = useState(false);
  const [intakeSuggestion, setIntakeSuggestion] = useState(null);
  const [intakePackages, setIntakePackages] = useState([]);
  const [quotationCount, setQuotationCount] = useState(0);
  const [bufferMinutesDraft] = useState(15);
  const [intakePackageDraft, setIntakePackageDraft] = useState("");
  const [bypassConflictGuard, setBypassConflictGuard] = useState(false);
  const [customerConsentAccepted, setCustomerConsentAccepted] = useState(false);
  const [customerConsentChannel, setCustomerConsentChannel] = useState("APP");
  const [customerConsentNote, setCustomerConsentNote] = useState("");
  const [receptionDiagnosisNote, setReceptionDiagnosisNote] = useState("");

  const resolveDetailServiceName = useCallback((detail) => {
    const itemType = String(detail?.itemType ?? detail?.ItemType ?? "").trim().toUpperCase();
    const isGenericName = (value) => /^c[oô]ng\s+đ[ạa]i\s+t[uư]/i.test(String(value || "").trim());
    const quotationDetailId = toPositiveNumber(detail?.quotationDetailId, detail?.QuotationDetailId);
    if (quotationDetailId > 0 && quotationDetailNameById.has(quotationDetailId)) {
      const quotationName = quotationDetailNameById.get(quotationDetailId) || "";
      if (!isGenericName(quotationName)) {
        return quotationName;
      }
    }

    const standardServiceId = toPositiveNumber(detail?.standardServiceId, detail?.StandardServiceId);
    if (standardServiceId > 0 && standardServiceNameById.has(standardServiceId)) {
      return standardServiceNameById.get(standardServiceId) || "";
    }

    const serviceItemId = toPositiveNumber(detail?.itemId, detail?.ItemId);
    if (
      serviceItemId > 0 &&
      (itemType === "SERVICE" || itemType === "LABOR" || itemType === "STANDARD_SERVICE") &&
      standardServiceNameById.has(serviceItemId)
    ) {
      return standardServiceNameById.get(serviceItemId) || "";
    }

    const itemName = firstText(detail?.itemName, detail?.ItemName);
    const description = firstText(detail?.description, detail?.Description);
    const looksGeneric = isGenericName(itemName);
    return looksGeneric ? (description || itemName) : (itemName || description);
  }, [quotationDetailNameById, standardServiceNameById]);

  const categorizedServiceDetails = useMemo(() => {
    const isLaborOrService = (detail) => {
      const itemType = String(detail?.itemType ?? detail?.ItemType ?? "").trim().toUpperCase();
      const standardServiceId = toPositiveNumber(detail?.standardServiceId, detail?.StandardServiceId);
      if (itemType === "PART" || itemType === "PRODUCT") return false;
      return itemType === "LABOR" || itemType === "SERVICE" || itemType === "STANDARD_SERVICE" || standardServiceId > 0;
    };

    const isSupplementaryDetail = (detail) => {
      const isSupplementary = Boolean(detail?.isSupplementary ?? detail?.IsSupplementary);
      const source = String(detail?.serviceSource ?? detail?.ServiceSource ?? "").trim().toUpperCase();
      return isSupplementary || source === "SUPPLEMENTARY_TECHNICIAN" || source === "SUPPLEMENTARY_CUSTOMER";
    };

    const uniqueByGroup = (details) => {
      const uniqueMap = new Map();
      const isGenericName = (value) => /^c[oô]ng\s+đ[ạa]i\s+t[uư]/i.test(String(value || "").trim());
      const scoreName = (value) => {
        const text = String(value || "").trim();
        if (!text) return 0;
        const nonGenericBonus = isGenericName(text) ? 0 : 1000;
        return nonGenericBonus + text.length;
      };
      details.forEach((detail) => {
        const standardServiceId = toPositiveNumber(detail?.standardServiceId, detail?.StandardServiceId);
        const itemId = toPositiveNumber(detail?.itemId, detail?.ItemId);
        const resolvedName = resolveDetailServiceName(detail);
        const nameKey = String(resolvedName || "").trim().toLowerCase();
        const key = standardServiceId
          ? `std-${standardServiceId}`
          : itemId
            ? `item-${itemId}`
            : `name-${nameKey}`;
        const existing = uniqueMap.get(key);
        if (!existing) {
          uniqueMap.set(key, detail);
          return;
        }

        const existingName = resolveDetailServiceName(existing);
        if (scoreName(resolvedName) > scoreName(existingName)) {
          uniqueMap.set(key, detail);
        }
      });
      return Array.from(uniqueMap.values());
    };

    const candidates = (orderDetails || []).filter((detail) => isLaborOrService(detail));
    const initial = uniqueByGroup(candidates.filter((detail) => !isSupplementaryDetail(detail)));
    const supplementary = candidates.filter((detail) => isSupplementaryDetail(detail));

    return { initial, supplementary };
  }, [orderDetails, resolveDetailServiceName]);

  const customerChosenDetails = categorizedServiceDetails.initial;
  const supplementaryServiceDetails = categorizedServiceDetails.supplementary;
  const toDisplayServiceItems = useCallback((details) => {
    const rows = new Map();
    details.forEach((detail) => {
      const standardServiceId = toPositiveNumber(detail?.standardServiceId, detail?.StandardServiceId);
      const itemId = toPositiveNumber(detail?.itemId, detail?.ItemId);
      const name = resolveDetailServiceName(detail) || "Dịch vụ";
      const nameKey = String(name || "").trim().toLowerCase();
      const key = standardServiceId
        ? `std-${standardServiceId}`
        : itemId
          ? `item-${itemId}`
          : `name-${nameKey}`;
      const amount = Number(detail?.finalAmount ?? detail?.FinalAmount ?? detail?.unitPriceAtTime ?? detail?.UnitPriceAtTime ?? 0);
      const existing = rows.get(key);
      if (!existing) {
        rows.set(key, { key, name, amount: Number.isFinite(amount) ? amount : 0 });
        return;
      }
      existing.amount += Number.isFinite(amount) ? amount : 0;
      if (String(name || "").trim().length > String(existing.name || "").trim().length) {
        existing.name = name;
      }
    });
    return Array.from(rows.values());
  }, [resolveDetailServiceName]);

  const customerChosenDisplayItems = useMemo(
    () => toDisplayServiceItems(customerChosenDetails),
    [customerChosenDetails, toDisplayServiceItems]
  );
  const supplementaryDisplayItems = useMemo(() => {
    // Supplementary services must be shown line-by-line to reflect all approved additions.
    return (supplementaryServiceDetails || []).map((detail, index) => {
      const amount = Number(detail?.finalAmount ?? detail?.FinalAmount ?? detail?.unitPriceAtTime ?? detail?.UnitPriceAtTime ?? 0);
      const detailId = toPositiveNumber(detail?.id, detail?.Id);
      const quotationDetailId = toPositiveNumber(detail?.quotationDetailId, detail?.QuotationDetailId);
      return {
        key: detailId > 0
          ? `supp-detail-${detailId}`
          : quotationDetailId > 0
            ? `supp-qd-${quotationDetailId}-${index}`
            : `supp-idx-${index}`,
        name: resolveDetailServiceName(detail) || "Dịch vụ phát sinh",
        amount: Number.isFinite(amount) ? amount : 0,
      };
    });
  }, [supplementaryServiceDetails, resolveDetailServiceName]);
  const customerChosenServiceNames = useMemo(
    () =>
      customerChosenDisplayItems
        .map((item) => item.name)
        .filter(Boolean),
    [customerChosenDisplayItems]
  );

  useEffect(() => {
    setLiveOrderStatus(String(order?.status || "").trim().toUpperCase());
    setOrderSnapshot(order || null);
  }, [order?.id, order?.status]);

  useEffect(() => {
    const fetchDetailData = async () => {
      if (!order?.id) return;
      setLoadingDetails(true);
      try {
        const [orderRes, detailsRes, diagnosesRes, conditionLogsRes, insuranceRes, intakePackagesRes, modelRes, brandRes, quotationsRes, standardServicesRes] = await Promise.all([
          serviceOrderApi.getById(order.id),
          serviceOrderApi.getDetails(order.id),
          serviceOrderApi.getDiagnoses(order.id),
          vehicleConditionLogApi.getByServiceOrderId(order.id),
          insuranceCompanyApi.getAll().catch(() => []),
          serviceOrderApi.getDiagnosisIntakePackages().catch(() => []),
          getVehicleModels().catch(() => []),
          getVehicleBrands().catch(() => []),
          serviceOrderApi.getQuotationsByServiceOrder(order.id).catch(() => []),
          servicesApi.getStandardServices().catch(() => []),
        ]);

        setLiveOrderStatus(String(orderRes?.status || order?.status || "").trim().toUpperCase());
        setOrderSnapshot(orderRes || order || null);
        setOrderDetails(Array.isArray(detailsRes) ? detailsRes : []);
        setDiagnoses(Array.isArray(diagnosesRes) ? diagnosesRes : []);
        setInsuranceCompanies(Array.isArray(insuranceRes) ? insuranceRes : []);
        setIntakePackages(Array.isArray(intakePackagesRes) ? intakePackagesRes : []);
        setVehicleModels(Array.isArray(modelRes) ? modelRes : []);
        setVehicleBrands(Array.isArray(brandRes) ? brandRes : []);
        setQuotationCount(getCollectionLength(quotationsRes));
        const quotationDetailNameMap = new Map();
        (Array.isArray(quotationsRes) ? quotationsRes : []).forEach((quotation) => {
          const details = Array.isArray(quotation?.details ?? quotation?.Details)
            ? (quotation?.details ?? quotation?.Details)
            : [];
          details.forEach((detail) => {
            const detailId = toPositiveNumber(detail?.id, detail?.Id);
            const detailName = firstText(
              detail?.itemName,
              detail?.ItemName,
              detail?.description,
              detail?.Description
            );
            if (detailId > 0 && detailName) {
              quotationDetailNameMap.set(detailId, detailName);
            }
          });
        });
        setQuotationDetailNameById(quotationDetailNameMap);
        const serviceNameMap = new Map();
        (Array.isArray(standardServicesRes) ? standardServicesRes : []).forEach((service) => {
          const id = toPositiveNumber(service?.id, service?.Id);
          const name = firstText(service?.name, service?.Name);
          if (id > 0 && name) {
            serviceNameMap.set(id, name);
          }
        });
        setStandardServiceNameById(serviceNameMap);

        const signedCheckInExists = Array.isArray(conditionLogsRes) && conditionLogsRes.some((log) => {
          const logType = String(log?.logType || log?.LogType || "").toUpperCase();
          const signatureUrl = String(log?.customerSignatureUrl || log?.CustomerSignatureUrl || "").trim();
          return logType === "CHECK_IN" && signatureUrl.length > 0;
        });
        setHasSignedCheckIn(signedCheckInExists);
      } catch (error) {
        console.error("Lỗi tải chi tiết lệnh sửa chữa:", error);
        setHasSignedCheckIn(false);
      } finally {
        setLoadingDetails(false);
      }
    };

    void fetchDetailData();
  }, [order?.id]);

  const intakePackageOptions = useMemo(() => {
    const dbOptions = (Array.isArray(intakePackages) ? intakePackages : [])
      .filter((item) => String(item?.code || item?.Code || "").trim())
      .map((item) => ({
        value: String(item?.code || item?.Code || "").trim().toUpperCase(),
        label: buildIntakePackageLabel(item),
      }));

    return [{ value: "", label: "Để hệ thống tự chọn" }, ...dbOptions];
  }, [intakePackages]);

  const intakePackageNameByCode = useMemo(() => {
    const map = new Map();
    (Array.isArray(intakePackages) ? intakePackages : []).forEach((item) => {
      const code = String(item?.code || item?.Code || "").trim().toUpperCase();
      if (!code) return;
      map.set(code, buildIntakePackageLabel(item));
    });
    return map;
  }, [intakePackages]);

  const latestDiagnosis = useMemo(() => {
    return [...diagnoses]
      .sort((a, b) => {
        const timeA = toApiDateMillis(a?.createdAt || a?.updatedAt);
        const timeB = toApiDateMillis(b?.createdAt || b?.updatedAt);
        return timeB - timeA;
      })[0] || null;
  }, [diagnoses]);

  const diagnosisStatus = String(latestDiagnosis?.status || "").toUpperCase();
  const finalApprovalStatus = String(
    latestDiagnosis?.finalApprovalStatus ?? latestDiagnosis?.FinalApprovalStatus ?? ""
  ).trim().toUpperCase();
  const isFinalDecisionMade = finalApprovalStatus === "APPROVED" || finalApprovalStatus === "REJECTED";
  const isForemanApproved = finalApprovalStatus === "APPROVED"
    || toPositiveNumber(latestDiagnosis?.approvedByForemanId, latestDiagnosis?.ApprovedByForemanId) > 0
    || Boolean(latestDiagnosis?.approvedByForemanAt ?? latestDiagnosis?.ApprovedByForemanAt);
  const isDiagnosisCompleted = diagnosisStatus === "COMPLETED";
  const canCancelDiagnosis = Boolean(latestDiagnosis) && !isFinalDecisionMade && diagnosisStatus !== "CANCELED";
  const hasDiagnosisReport = isForemanApproved;
  const canShowQuoteButton = isForemanApproved || hasDiagnosisReport || quotationCount > 0;

  const customerIssueText = useMemo(() => {
    return parsedNotes.mainNotes || "Không có ghi chú gì";
  }, [parsedNotes.mainNotes]);

  const autoDispatchWarning = useMemo(
    () => extractAutoDispatchWarning(firstText(latestDiagnosis?.supportRequestNote, latestDiagnosis?.SupportRequestNote)),
    [latestDiagnosis?.SupportRequestNote, latestDiagnosis?.supportRequestNote]
  );
  const diagnosisAssignedTeamLabel = useMemo(() => {
    const explicitTeamName = firstText(
      latestDiagnosis?.assignedTeamName,
      latestDiagnosis?.AssignedTeamName,
      latestDiagnosis?.teamName,
      latestDiagnosis?.TeamName
    );
    if (explicitTeamName) return explicitTeamName;

    const teamId = Number(
      latestDiagnosis?.assignedTeamId ?? latestDiagnosis?.AssignedTeamId ?? latestDiagnosis?.teamId ?? latestDiagnosis?.TeamId ?? 0
    );
    if (Number.isFinite(teamId) && teamId > 0) return `tổ #${teamId}`;

    return "";
  }, [
    latestDiagnosis?.AssignedTeamId,
    latestDiagnosis?.AssignedTeamName,
    latestDiagnosis?.TeamId,
    latestDiagnosis?.TeamName,
    latestDiagnosis?.assignedTeamId,
    latestDiagnosis?.assignedTeamName,
    latestDiagnosis?.teamId,
    latestDiagnosis?.teamName,
  ]);
  const diagnosisAssignedLeaderLabel = useMemo(() => {
    const explicitLeaderName = firstText(
      latestDiagnosis?.assignedTeamLeaderName,
      latestDiagnosis?.AssignedTeamLeaderName
    );
    if (explicitLeaderName) return explicitLeaderName;

    const leaderId = Number(
      latestDiagnosis?.assignedTeamLeaderId ?? latestDiagnosis?.AssignedTeamLeaderId ?? latestDiagnosis?.technicianId ?? latestDiagnosis?.TechnicianId ?? 0
    );
    if (!Number.isFinite(leaderId) || leaderId <= 0) return "";

    const leader = (Array.isArray(staff) ? staff : []).find((item) => Number(item?.id) === leaderId);
    return firstText(leader?.fullName, "");
  }, [
    latestDiagnosis?.AssignedTeamLeaderId,
    latestDiagnosis?.AssignedTeamLeaderName,
    latestDiagnosis?.TechnicianId,
    latestDiagnosis?.assignedTeamLeaderId,
    latestDiagnosis?.assignedTeamLeaderName,
    latestDiagnosis?.technicianId,
    staff,
  ]);
  const diagnosisRecallTargetLabel = useMemo(() => {
    if (diagnosisAssignedLeaderLabel) {
      if (!diagnosisAssignedTeamLabel) {
        return `tổ của ${diagnosisAssignedLeaderLabel}`;
      }
      return `${diagnosisAssignedTeamLabel} (tổ trưởng: ${diagnosisAssignedLeaderLabel})`;
    }
    return diagnosisAssignedTeamLabel;
  }, [diagnosisAssignedLeaderLabel, diagnosisAssignedTeamLabel]);

  const isFromAppointment = Boolean(order?.appointmentId);
  const isGuest = !order?.appointmentId && customer && customer.isRegistered === false;

  const sourceMeta = useMemo(() => {
    if (isFromAppointment) {
      return {
        text: "Từ lịch hẹn trước",
        tone: "source-appt",
        Icon: MdHistory,
      };
    }
    if (isGuest) {
      return {
        text: "Khách vãng lai (tạo nhanh)",
        tone: "source-guest",
        Icon: MdPerson,
      };
    }
    return {
      text: "Lễ tân tiếp nhận trực tiếp",
      tone: "source-reception",
      Icon: MdAssignment,
    };
  }, [isFromAppointment, isGuest]);

  const customerName = firstText(order?.customerName, customer?.fullName, "-");
  const customerPhone = firstText(order?.customerPhone, customer?.phone, "-");
  const vehiclePlate = firstText(order?.vehicleLicensePlate, vehicle?.licensePlate, "N/A");
  const modelById = useMemo(() => {
    const map = new Map();
    (Array.isArray(vehicleModels) ? vehicleModels : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [vehicleModels]);

  const brandById = useMemo(() => {
    const map = new Map();
    (Array.isArray(vehicleBrands) ? vehicleBrands : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [vehicleBrands]);

  const resolvedVehicleModelId = toPositiveNumber(
    order?.vehicleModelId,
    order?.VehicleModelId,
    orderSnapshot?.vehicleModelId,
    orderSnapshot?.VehicleModelId,
    vehicle?.vehicleModelId,
    vehicle?.VehicleModelId,
    vehicle?.modelId,
    vehicle?.ModelId,
    vehicle?.model?.id,
    vehicle?.Model?.Id
  );
  const resolvedModel = resolvedVehicleModelId > 0 ? modelById.get(resolvedVehicleModelId) : null;
  const resolvedVehicleBrandId = toPositiveNumber(
    order?.vehicleBrandId,
    order?.VehicleBrandId,
    orderSnapshot?.vehicleBrandId,
    orderSnapshot?.VehicleBrandId,
    vehicle?.vehicleBrandId,
    vehicle?.VehicleBrandId,
    vehicle?.brandId,
    vehicle?.BrandId,
    resolvedModel?.brandId,
    resolvedModel?.BrandId
  );
  const resolvedBrand = resolvedVehicleBrandId > 0 ? brandById.get(resolvedVehicleBrandId) : null;
  const vehicleBrand = firstText(
    order?.vehicleBrandName,
    order?.VehicleBrandName,
    orderSnapshot?.vehicleBrandName,
    orderSnapshot?.VehicleBrandName,
    vehicle?.brandName,
    vehicle?.BrandName,
    resolvedBrand?.name,
    resolvedBrand?.Name
  );

  const vehicleModel = firstText(
    order?.vehicleModelName,
    order?.VehicleModelName,
    orderSnapshot?.vehicleModelName,
    orderSnapshot?.VehicleModelName,
    vehicle?.modelName,
    vehicle?.ModelName,
    resolvedModel?.modelName,
    resolvedModel?.ModelName,
    resolvedModel?.name,
    resolvedModel?.Name,
    resolvedBrand?.name,
    resolvedBrand?.Name,
      "Chưa rõ dòng xe"
  );
  const vehicleDisplayName = useMemo(() => {
    const brand = String(vehicleBrand || "").trim();
    const model = String(vehicleModel || "").trim();
  if (!brand) return model || "Chưa rõ dòng xe";
    if (!model) return brand;
    if (model.toLowerCase().includes(brand.toLowerCase())) return model;
    return `${brand} ${model}`.trim();
  }, [vehicleBrand, vehicleModel]);
  const vehicleColor = firstText(
    order?.vehicleColor,
    order?.VehicleColor,
    orderSnapshot?.vehicleColor,
    orderSnapshot?.VehicleColor,
    vehicle?.color,
    vehicle?.Color
  );
  const vehicleVin = firstText(
    order?.vehicleVin,
    order?.VehicleVin,
    orderSnapshot?.vehicleVin,
    orderSnapshot?.VehicleVin,
    vehicle?.vin,
    vehicle?.Vin
  );
  const advisorName = firstText(advisor?.fullName, "-");

  const statusLabel = formatStatusLabel(liveOrderStatus || order?.status);
  const categoryLabel = formatOrderCategoryLabel(order?.orderCategory);
  const insuranceSummary = useMemo(
    () => summarizeInsuranceOrderDetails(orderDetails),
    [orderDetails]
  );
  const insuranceCompanyNames = useMemo(() => {
    if (!insuranceSummary.hasInsurance) return [];

    const companyNameById = new Map(
      (Array.isArray(insuranceCompanies) ? insuranceCompanies : []).map((company) => [
        Number(company?.id || company?.Id || 0),
        String(company?.name || company?.Name || "").trim(),
      ])
    );

    return insuranceSummary.companyIds
      .map((companyId) => companyNameById.get(Number(companyId)) || `Công ty #${companyId}`)
      .filter(Boolean);
  }, [insuranceCompanies, insuranceSummary]);

  const insuranceCardImageUrls = useMemo(
    () =>
      normalizeImageUrls(
        orderSnapshot?.insuranceCardImageUrls ?? orderSnapshot?.InsuranceCardImageUrls ?? order?.insuranceCardImageUrls ?? order?.InsuranceCardImageUrls
      ),
    [order?.InsuranceCardImageUrls, order?.insuranceCardImageUrls, orderSnapshot?.InsuranceCardImageUrls, orderSnapshot?.insuranceCardImageUrls]
  );

  const insuranceDamageImageUrls = useMemo(
    () =>
      normalizeImageUrls(
        orderSnapshot?.insuranceDamageImageUrls ?? orderSnapshot?.InsuranceDamageImageUrls ?? order?.insuranceDamageImageUrls ?? order?.InsuranceDamageImageUrls
      ),
    [order?.InsuranceDamageImageUrls, order?.insuranceDamageImageUrls, orderSnapshot?.InsuranceDamageImageUrls, orderSnapshot?.insuranceDamageImageUrls]
  );

  const shouldShowImageSection =
    insuranceCardImageUrls.length > 0 || insuranceDamageImageUrls.length > 0;

  const handleOpenPage = (path) => {
    onClose?.();
    const resolvedPath = location.pathname.startsWith("/admin")
      ? path.replace("/workshop/reception/orders", "/admin/service-orders")
      : path;
    navigate(resolvedPath);
  };

  const refreshAfterDiagnosisChange = async () => {
    if (onRefresh) await onRefresh();
    const [orderRes, diagnosisRes] = await Promise.all([
      serviceOrderApi.getById(order.id),
      serviceOrderApi.getDiagnoses(order.id),
    ]);
    setLiveOrderStatus(String(orderRes?.status || "").trim().toUpperCase());
    setDiagnoses(Array.isArray(diagnosisRes) ? diagnosisRes : []);
  };

  const openVehicleLogPage = () => {
    handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`);
  };

  useEffect(() => {
    if (!showDispatchPopup || !order?.id) return;

    let isCancelled = false;
    const normalizedBuffer = Number(bufferMinutesDraft);
    const safeBuffer = Number.isFinite(normalizedBuffer) ? Math.max(0, Math.min(120, normalizedBuffer)) : 15;

    const run = async () => {
      setLoadingIntakeSuggestion(true);
      try {
        const response = await serviceOrderApi.getDiagnosisIntakeSuggestion(order.id, {
          bufferMinutes: safeBuffer,
          intakePackageCode: intakePackageDraft || undefined,
        });

        if (isCancelled) return;
        setIntakeSuggestion(normalizeIntakeSuggestion(response));
        if (!intakePackageDraft && response?.finalPackageCode) {
          setIntakePackageDraft(String(response.finalPackageCode).toUpperCase());
        }
      } catch {
        if (!isCancelled) {
          setIntakeSuggestion(null);
        }
      } finally {
        if (!isCancelled) {
          setLoadingIntakeSuggestion(false);
        }
      }
    };

    void run();

    return () => {
      isCancelled = true;
    };
  }, [showDispatchPopup, order?.id, bufferMinutesDraft, intakePackageDraft]);

  const isConflictBlocked = useMemo(() => {
    const conflictLevel = String(intakeSuggestion?.conflictLevel || "").toUpperCase();
    return conflictLevel === "RED" && !bypassConflictGuard;
  }, [intakeSuggestion?.conflictLevel, bypassConflictGuard]);

  const intakeConflictLevel = String(intakeSuggestion?.conflictLevel || "").toUpperCase();
  const requiresCustomerConsent = Boolean(intakeSuggestion?.requiresCustomerConsent);
  const isConsentBlocked = requiresCustomerConsent && !customerConsentAccepted;

  const handleRequestDiagnosis = async () => {
    if (!hasSignedCheckIn) {
      try {
        await serviceOrderApi.createCheckInRequest(order.id);
      } catch {
        // Ignore duplicated marker creation and continue redirecting.
      }

      alert("Vui lòng hoàn tất Nhật ký xe và ký CHECK-IN trước khi gửi yêu cầu chẩn đoán.");
      openVehicleLogPage();
      return;
    }

    setIntakePackageDraft("");
    setBypassConflictGuard(false);
    setCustomerConsentAccepted(false);
    setCustomerConsentChannel("APP");
    setCustomerConsentNote("");
    setReceptionDiagnosisNote(
      buildAutoDiagnosisAssignmentNote({
        selectedServiceNames: customerChosenServiceNames,
      })
    );
    setIntakeSuggestion(null);
    setShowDispatchPopup(true);
  };

  const handleConfirmDiagnosisRequest = async () => {
    const suggestionDispatchTag = String(intakeSuggestion?.dispatchTag || "").trim().toUpperCase();
    const normalizedTag = DISPATCH_TAG_OPTIONS.some((item) => item.value === suggestionDispatchTag)
      ? suggestionDispatchTag
      : undefined;
    const normalizedBuffer = Number(bufferMinutesDraft);
    const safeBuffer = Number.isFinite(normalizedBuffer) ? Math.max(0, Math.min(120, normalizedBuffer)) : 15;
    const selectedPackageCode = String(intakePackageDraft || intakeSuggestion?.finalPackageCode || "").trim().toUpperCase();
    const autoAssignmentNote = buildAutoDiagnosisAssignmentNote({
      selectedServiceNames: customerChosenServiceNames,
    });
    const finalReceptionNote = String(receptionDiagnosisNote || "").trim() || autoAssignmentNote;
    if (requiresCustomerConsent && !customerConsentAccepted) {
      alert("Cần xác nhận đồng ý của khách trước khi tạo yêu cầu gói chẩn đoán này.");
      return;
    }

    setSubmittingDiagnosisRequest(true);
    try {
      const requestResult = await serviceOrderApi.requestDiagnosisSafe(order.id, {
        dispatchTag: normalizedTag,
        autoAssign: true,
        enforceCapacityGuard: true,
        bypassConflictGuard,
        bufferMinutes: safeBuffer,
        intakePackageCode: selectedPackageCode || undefined,
        customerConsentAccepted: requiresCustomerConsent ? customerConsentAccepted : undefined,
        customerConsentChannel: requiresCustomerConsent ? customerConsentChannel : undefined,
        customerConsentNote: requiresCustomerConsent ? customerConsentNote : undefined,
        assignmentNote: finalReceptionNote,
      });
      if (!requestResult?.ok) {
        const message = String(requestResult?.data?.message || "Không xác định");
        const requiresDispatchReview = Boolean(requestResult?.data?.requiresDispatchReview);
        if (requiresDispatchReview) {
          markOrderNeedsDispatchReview(order?.id);
        }
        alert(`Lỗi khi yêu cầu chẩn đoán: ${message}`);
        return;
      }
      clearOrderDispatchReviewMark(order?.id);
      setShowDispatchPopup(false);
      setIntakeSuggestion(null);
      await refreshAfterDiagnosisChange();
    } catch (error) {
      alert(`Lỗi khi yêu cầu chẩn đoán: ${error?.message || "Không xác định"}`);
    } finally {
      setSubmittingDiagnosisRequest(false);
    }
  };

  const handleRecallDiagnosis = async () => {
    const targetSuffix = diagnosisRecallTargetLabel
      ? ` đang xử lý bởi ${diagnosisRecallTargetLabel}`
      : "";
    const confirmed = window.confirm(`Hủy phiếu chẩn đoán${targetSuffix}?`);
    if (!confirmed) return;
    try {
      await serviceOrderApi.recallDiagnosisRequest(order.id);
      await refreshAfterDiagnosisChange();
    } catch (error) {
      alert(`Lỗi khi hủy chẩn đoán: ${error?.message || "Không xác định"}`);
    }
  };

  const handleOpenDiagnosisReport = () => {
    if (!hasDiagnosisReport) return;
    handleOpenPage(`/workshop/reception/orders/${order.id}/diagnosis-report`);
  };

  return (
    <>
      <Modal
        title={(
          <div className="so-detail-title-wrap">
            <span className="so-detail-title-text">{`Chi tiết lệnh sửa chữa: ${order?.code || ""}`}</span>
            <button
              type="button"
              className="so-detail-edit-btn"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/edit`)}
            >
              <MdEdit aria-hidden="true" />
              <span>Chỉnh sửa</span>
            </button>
          </div>
        )}
        onClose={onClose}
        className="modal-lg service-order-detail-modal"
      >
        <div className="so-detail-shell">
          <section className="so-detail-overview-card">
            <div className="so-detail-chip-row">
              <span className="so-detail-chip so-detail-chip-status">{statusLabel}</span>
              <span className={`so-detail-chip ${sourceMeta.tone}`}>
                <sourceMeta.Icon aria-hidden="true" />
                {sourceMeta.text}
              </span>
              <span className="so-detail-chip so-detail-chip-category">{categoryLabel}</span>
            </div>

            <div className="so-detail-party-grid">
              <article className="so-detail-party-card">
                <p className="so-detail-card-label">Khách hàng</p>
                <h4>{customerName}</h4>
                <p>{customerPhone}</p>
              </article>

              <article className="so-detail-party-card">
                <div className="so-detail-party-head">
                  <p className="so-detail-card-label">Phương tiện</p>
                  <button
                    type="button"
                    className="so-detail-link-btn"
                    onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`)}
                  >
                    Nhật ký xe
                  </button>
                </div>
                <h4>{vehiclePlate}</h4>
                  <p>{vehicleColor ? `${vehicleDisplayName} - ${vehicleColor}` : vehicleDisplayName}</p>
                {vehicleVin ? <p className="so-detail-vin">VIN: {vehicleVin}</p> : null}
              </article>
            </div>
          </section>

          <section className="so-detail-time-grid">
            <article className="so-detail-time-item">
              <p>Thời điểm vào</p>
              <strong>{formatOrderDateTime(order?.checkInTime)}</strong>
            </article>
            <article className="so-detail-time-item">
              <p>Hẹn trả xe</p>
              <strong>{formatOrderDateTime(order?.promisedHandoverTime)}</strong>
            </article>
            <article className="so-detail-time-item">
              <p>Dự kiến hoàn thành</p>
              <strong>{formatOrderDateTime(order?.estimatedHandover)}</strong>
            </article>
            <article className="so-detail-time-item">
              <p>Cố vấn dịch vụ</p>
              <strong>{advisorName}</strong>
            </article>
          </section>

          <InsuranceBillingOverview
            summary={insuranceSummary}
            companyNames={insuranceCompanyNames}
            note="Các khoản khách trả trong phần này đã bao gồm miễn thường, hồ sơ, khấu hao hoặc các dòng dịch vụ đang để payer là khách hàng."
          />

          {shouldShowImageSection ? (
            <section className="so-detail-section">
              <h3 className="so-detail-section-title">
                <MdDirectionsCar aria-hidden="true" />
                <span>Ảnh từ lịch hẹn</span>
              </h3>

              <div className="so-detail-image-block-grid">
                <article className="so-detail-image-block">
                  <div className="so-detail-image-head">
                    <p>Ảnh bảo hiểm</p>
                    <span>{insuranceCardImageUrls.length} ảnh</span>
                  </div>
                  {insuranceCardImageUrls.length > 0 ? (
                    <div className="so-detail-image-grid">
                      {insuranceCardImageUrls.map((url, index) => (
                        <a
                          key={`ins-card-${url}-${index}`}
                          href={url}
                          target="_blank"
                          rel="noreferrer"
                          className="so-detail-image-link"
                          title="Mở ảnh bảo hiểm"
                        >
                          <img src={url} alt={`Ảnh bảo hiểm ${index + 1}`} loading="lazy" />
                        </a>
                      ))}
                    </div>
                  ) : (
                    <p className="so-detail-empty">Không có ảnh bảo hiểm.</p>
                  )}
                </article>

                <article className="so-detail-image-block">
                  <div className="so-detail-image-head">
                    <p>Ảnh hiện trạng</p>
                    <span>{insuranceDamageImageUrls.length} ảnh</span>
                  </div>
                  {insuranceDamageImageUrls.length > 0 ? (
                    <div className="so-detail-image-grid">
                      {insuranceDamageImageUrls.map((url, index) => (
                        <a
                          key={`ins-damage-${url}-${index}`}
                          href={url}
                          target="_blank"
                          rel="noreferrer"
                          className="so-detail-image-link"
                          title="Mở ảnh hiện trạng"
                        >
                          <img src={url} alt={`Ảnh hiện trạng ${index + 1}`} loading="lazy" />
                        </a>
                      ))}
                    </div>
                  ) : (
                    <p className="so-detail-empty">Không có ảnh hiện trạng.</p>
                  )}
                </article>
              </div>
            </section>
          ) : null}

          <section className="so-detail-section">
            <h3 className="so-detail-section-title">
              <MdAssignment aria-hidden="true" />
              <span>Dịch vụ và mô tả</span>
            </h3>

            <div className="so-detail-note-grid">
              <article className="so-detail-panel">
                <p className="so-detail-panel-title">Dịch vụ theo luồng thực thi</p>
                {loadingDetails ? (
                  <p className="so-detail-empty">Đang tải dữ liệu dịch vụ...</p>
                ) : (
                  <>
                    <p
                      className="so-detail-panel-title"
                      style={{ marginTop: 2, marginBottom: 2, fontSize: 10, color: "#5f769f" }}
                    >
                      Dịch vụ khách chọn ban đầu
                    </p>
                    {customerChosenDisplayItems.length > 0 ? (
                      <div className="so-detail-service-list">
                        {customerChosenDisplayItems.map((item) => (
                          <div
                            key={item.key}
                            className="so-detail-service-row"
                          >
                            <div className="so-detail-service-copy">
                              <span>{item.name || "Dịch vụ"}</span>
                            </div>
                            <strong>{formatMoney(item.amount)}</strong>
                          </div>
                        ))}
                      </div>
                    ) : parsedNotes.requestedServices ? (
                      <p className="so-detail-body-text">{parsedNotes.requestedServices}</p>
                    ) : (
                      <p className="so-detail-empty">Chưa có dịch vụ ban đầu.</p>
                    )}

                    <p
                      className="so-detail-panel-title"
                      style={{ marginTop: 8, marginBottom: 2, fontSize: 10, color: "#5f769f" }}
                    >
                      Dịch vụ phát sinh (đã duyệt báo giá bổ sung)
                    </p>
                    {supplementaryDisplayItems.length > 0 ? (
                      <div className="so-detail-service-list">
                        {supplementaryDisplayItems.map((item) => (
                          <div key={item.key} className="so-detail-service-row">
                            <div className="so-detail-service-copy">
                              <span>{item.name || "Dịch vụ phát sinh"}</span>
                            </div>
                            <strong>{formatMoney(item.amount)}</strong>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <p className="so-detail-empty">Chưa có dịch vụ phát sinh.</p>
                    )}
                  </>
                )}
              </article>

              <article className="so-detail-panel so-detail-panel-note">
                <p className="so-detail-panel-title">Ghi chú</p>
                <p className="so-detail-body-text so-detail-note-content">
                  {String(customerIssueText || "").toLowerCase().includes("không có")
                    ? "Không có ghi chú gì"
                    : customerIssueText || "Không có ghi chú gì"}
                </p>
              </article>
            </div>
          </section>

          <section className="so-detail-section">
            <h3 className="so-detail-section-title">
              <MdCheckCircle aria-hidden="true" />
              <span>Xem chi tiết theo từng mục</span>
            </h3>
            <p className="so-detail-section-note">Chọn mục cần xem để mở trang chi tiết tương ứng.</p>

            <div className="so-detail-action-row">
              {hasDiagnosisReport ? (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-primary"
                  onClick={handleOpenDiagnosisReport}
                >
                  Xem chẩn đoán
                </button>
              ) : canCancelDiagnosis ? (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-warn"
                  onClick={handleRecallDiagnosis}
                >
                  Hủy chẩn đoán
                </button>
              ) : (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-ghost"
                  onClick={handleRequestDiagnosis}
                  title={!hasSignedCheckIn ? "Cần vào Nhật ký xe để hoàn tất CHECK-IN trước." : ""}
                >
                  Chẩn đoán
                </button>
              )}

              <button
                type="button"
                className="so-detail-action-btn so-detail-action-teal"
                onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/items`)}
              >
                Xem hạng mục & phụ tùng
              </button>

              {canShowQuoteButton ? (
                <button
                  type="button"
                  className="so-detail-action-btn so-detail-action-purple"
                  onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/quote`)}
                >
                  Xem báo giá
                </button>
              ) : null}
            </div>

            {autoDispatchWarning ? <p className="so-detail-diagnosis-warning">{autoDispatchWarning}</p> : null}
          </section>
        </div>
      </Modal>

      {showDispatchPopup ? (
        <Modal
          title="Chọn gói chẩn đoán"
          onClose={() => {
            if (!submittingDiagnosisRequest) setShowDispatchPopup(false);
          }}
          className="so-diagnosis-request-modal"
        >
          <div className="so-dispatch-popup">
            <p className="so-dispatch-popup-note">
              Chọn gói chẩn đoán cho xe <strong>{vehiclePlate}</strong>. Chuyên môn, tổ thực hiện và khoang sửa chữa sẽ tự động gán theo luồng chẩn đoán tự động.
            </p>

            <div className="so-dispatch-config-row">
              <label className="so-dispatch-config-item">
                <span>Gói chẩn đoán</span>
                <FilterDropdown
                  id={`intake-package-${order?.id || "draft"}`}
                  value={intakePackageDraft}
                  options={intakePackageOptions}
                  onChange={(nextValue) => setIntakePackageDraft(String(nextValue || "").toUpperCase())}
                  minWidth={0}
                  menuMinWidth={560}
                  disabled={submittingDiagnosisRequest}
                  ariaLabel="Chọn gói chẩn đoán"
                  fullWidth
                />
              </label>
            </div>

            <label className="so-dispatch-config-item so-dispatch-note-item">
              <span>Ghi chú lễ tân cho chẩn đoán</span>
              <textarea
                rows={2}
                value={receptionDiagnosisNote}
                onChange={(event) => setReceptionDiagnosisNote(event.target.value)}
                placeholder="Nhập ghi chú của lễ tân để gửi cho thợ (nếu để trống sẽ dùng mẫu tự động theo dịch vụ khách chọn)."
                disabled={submittingDiagnosisRequest}
              />
            </label>

            <div className="so-intake-card">
              {loadingIntakeSuggestion ? (
                <p className="so-intake-loading">Đang tính gói đề xuất...</p>
              ) : intakeSuggestion ? (
                <>
                  <div className="so-intake-row">
                    <span>Gói đề xuất</span>
                    <strong>
                      {intakePackageNameByCode.get(String(intakeSuggestion?.finalPackageCode || "").trim().toUpperCase())
                        || String(intakeSuggestion?.finalPackageName || "").trim()
                        || String(intakeSuggestion?.finalPackageCode || "").trim().toUpperCase()
                        || "--"}
                    </strong>
                  </div>
                  <div className="so-intake-row">
                    <span>T1 dự kiến</span>
                    <strong>{formatDateTimeValue(intakeSuggestion.t1EstimatedHandover)}</strong>
                  </div>
                  <div className="so-intake-row">
                    <span>Công thức thời gian</span>
                    <strong>
                      {`${intakeSuggestion.mainServiceMinutes || 0}p dịch vụ + ${intakeSuggestion.bufferMinutes || 0}p buffer + ${intakeSuggestion.packageEffectiveMinutes || 0}p gói`}
                    </strong>
                  </div>
                  <div className="so-intake-row">
                    <span>Xung đột booking</span>
                    <strong className={`so-intake-badge ${String(intakeConflictLevel || "").toLowerCase()}`}>
                      {`${intakeConflictLevel || "GREEN"}${intakeSuggestion.conflictMinutes > 0 ? ` (${intakeSuggestion.conflictMinutes}p)` : ""}`}
                    </strong>
                  </div>
                  {Array.isArray(intakeSuggestion.reasons) && intakeSuggestion.reasons.length > 0 ? (
                    <ul className="so-intake-reason-list">
                      {intakeSuggestion.reasons.slice(0, 3).map((reason, idx) => (
                        <li key={`${reason}-${idx}`}>{reason}</li>
                      ))}
                    </ul>
                  ) : null}
                  {intakeSuggestion.suggestedLeadTechnicianName ? (
                    <p className="so-intake-assign-text">
                      Auto-assign: {intakeSuggestion.suggestedLeadTechnicianName}
                      {intakeSuggestion.suggestedAssistantTechnicianName ? ` + ${intakeSuggestion.suggestedAssistantTechnicianName}` : ""}
                      {intakeSuggestion.suggestedTeamName ? ` (${intakeSuggestion.suggestedTeamName})` : ""}
                    </p>
                  ) : null}
                </>
              ) : (
                <p className="so-intake-loading">Không lấy được gói đề xuất. Bạn vẫn có thể gửi yêu cầu thủ công.</p>
              )}
            </div>

            {requiresCustomerConsent ? (
              <div className="so-consent-card">
                <label className="so-consent-check">
                  <input
                    type="checkbox"
                    checked={customerConsentAccepted}
                    onChange={(event) => setCustomerConsentAccepted(Boolean(event.target.checked))}
                    disabled={submittingDiagnosisRequest}
                  />
                  <span>Khách đã đồng ý thực hiện gói chẩn đoán cộng thêm thời gian.</span>
                </label>
                <div className="so-consent-grid">
                  <label className="so-dispatch-config-item">
                    <span>Kênh xác nhận</span>
                    <select
                      value={customerConsentChannel}
                      onChange={(event) => setCustomerConsentChannel(String(event.target.value || "APP").toUpperCase())}
                      disabled={submittingDiagnosisRequest}
                    >
                      <option value="APP">Ứng dụng</option>
                      <option value="ZALO">Zalo</option>
                      <option value="PHONE">Điện thoại</option>
                      <option value="DIRECT">Trực tiếp tại quầy</option>
                    </select>
                  </label>
                  <label className="so-dispatch-config-item">
                    <span>Ghi chú xác nhận</span>
                    <input
                      type="text"
                      value={customerConsentNote}
                      onChange={(event) => setCustomerConsentNote(event.target.value)}
                      placeholder="Ví dụ: Khách đồng ý cộng thêm 30 phút"
                      disabled={submittingDiagnosisRequest}
                    />
                  </label>
                </div>
              </div>
            ) : null}

            {intakeConflictLevel === "RED" ? (
              <label className="so-bypass-toggle">
                <input
                  type="checkbox"
                  checked={bypassConflictGuard}
                  onChange={(event) => setBypassConflictGuard(Boolean(event.target.checked))}
                  disabled={submittingDiagnosisRequest}
                />
                <span>Bypass cảnh báo đỏ (chỉ dùng khi khách rất vội)</span>
              </label>
            ) : null}

            <div className="so-dispatch-popup-actions">
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-ghost"
                onClick={() => setShowDispatchPopup(false)}
                disabled={submittingDiagnosisRequest}
              >
                Hủy
              </button>
              <button
                type="button"
                className="so-detail-action-btn so-detail-action-primary"
                onClick={handleConfirmDiagnosisRequest}
                disabled={submittingDiagnosisRequest || loadingIntakeSuggestion || isConflictBlocked || isConsentBlocked}
              >
                {submittingDiagnosisRequest
                  ? "Đang gửi..."
                  : isConflictBlocked
                    ? "Đang bị chặn bởi conflict đỏ"
                    : isConsentBlocked
                      ? "Chờ khách xác nhận"
                      : "Gửi yêu cầu"}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}

    </>
  );
}



