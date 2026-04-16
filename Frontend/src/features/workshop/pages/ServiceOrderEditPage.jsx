import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { insuranceCompanyApi } from "@features/workshop/api/insuranceCompanyApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllBranchOperatingHours, getAllPackages, getVehicleModels } from "@features/appointments/api/appointmentApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";
import {
  buildFlexibleCompletionWarningMessage,
  buildFlexibleTriggerServiceIdSet,
  buildInsuranceFlexiblePackageIdSet,
  buildSlotSelectionGuide,
  findUnresolvedPackageIds,
  resolveBookingFlowByItemsAndDuration,
  WORKING_DAY_MINUTES,
} from "@features/appointments/utils/bookingFlowRules";
import {
  INSURANCE_ASSET_TYPE,
  resolveInsuranceDeductibleAmount,
  resolveInsuranceQuoteState,
} from "@features/appointments/utils/bookingInsurance";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import WorkshopInsurancePanel from "@features/workshop/components/WorkshopInsurancePanel";
import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";
import {
  buildWorkshopEditPayload,
  calculateRoundedOccupancyMinutes,
} from "@features/workshop/utils/serviceOrderWorkflow";
import {
  buildInsuranceDraftBreakdown,
  INSURANCE_ORDER_CATEGORY,
  isInsuranceOrderActive,
  isInsuranceSignalService,
} from "@features/workshop/utils/insuranceOrderUi";
import { SERVICE_PRICE_DISCLAIMER, SERVICE_PRICE_LABEL } from "@shared/constants/servicePricing";
import { getInspectionTriadDisplayName, sumServiceMinutes } from "@shared/utils/serviceDurationUtils";
import {
  collectCompatibleCategoryIds,
  filterBookableItemsForVehicleModels,
} from "@shared/utils/serviceEngineCompatibility";
import { getBranchClosedMessage, isBranchClosedBySchedule } from "@shared/utils/branchOperatingSchedule";
import { addMinutesWithinWorkingHours } from "@shared/utils/workingHoursSchedule";
import { mergeBookableServicesAndPackages, splitBookingItemIds, toPackageSelectableId } from "@features/appointments/utils/bookingPackages";
import "./Workshop.css";

const DEFAULT_FORM_DATA = {
  orderCategory: "GENERAL_REPAIR",
  insuranceEnabled: false,
  insuranceCompanyId: "",
  insuranceDeductibleAmount: null,
  insuranceDeductibleOption: "",
  insuranceClaimMode: "",
  insuranceQuoteState: "",
  insuranceCardImageUrls: [],
  insuranceDamageImageUrls: [],
  serviceAdvisorId: "",
  oldPartsReturnOption: "DISPOSE_ALL",
  requiredDepositAmount: 0,
  checkInTime: "",
  estimatedHandover: "",
  promisedHandoverTime: "",
  status: "NEW",
  serviceIds: [],
};

const EDIT_WINDOW_PASSED_MESSAGE = "Không thể sửa service order vì đã qua mốc vận hành cho phép chỉnh sửa.";
const REQUEST_EDIT_REASON_REQUIRED = "Vui lòng nhập lý do yêu cầu sửa trước khi gửi.";
const REQUEST_EDIT_ERROR = "Không thể gửi yêu cầu sửa service order.";
const REQUEST_EDIT_SUCCESS = "Đã gửi yêu cầu sửa service order đến cấp phê duyệt.";
const REQUEST_DECISION_ERROR = "Không thể xử lý yêu cầu sửa service order.";

const SUPPLEMENTARY_SOURCE_TECHNICIAN = "SUPPLEMENTARY_TECHNICIAN";
const SUPPLEMENTARY_SOURCE_CUSTOMER = "SUPPLEMENTARY_CUSTOMER";
const SUPPLEMENTARY_REASON_TECH = "TECH_FINDING";
const SUPPLEMENTARY_REASON_CUSTOMER = "CUSTOMER_REQUEST";
const SUPPLEMENTARY_DEFAULT_STATUS = "APPROVED";

function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id !== 0)
    )
  );
}

function parsePositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeSearchText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

function normalizeRoleToken(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .trim()
    .toUpperCase()
    .replace(/-/g, "_")
    .replace(/\s+/g, "_");
}

function containsRoleKeyword(roleCode, roleName, keywords) {
  const normalizedCode = normalizeRoleToken(roleCode);
  const normalizedName = normalizeRoleToken(roleName);
  return keywords.some((keyword) => {
    const token = normalizeRoleToken(keyword);
    return normalizedCode.includes(token) || normalizedName.includes(token);
  });
}

function canApproveServiceOrderSection(roleCode, roleName, section) {
  const normalizedSection = String(section || "WORKSHOP").trim().toUpperCase();
  const isAdmin = containsRoleKeyword(roleCode, roleName, ["ADMIN", "OWNER", "DIRECTOR", "CHU_XUONG", "SUPERADMIN", "SUPER_ADMIN"]);
  const isManager = containsRoleKeyword(roleCode, roleName, ["MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC"]);
  const isWarehouse = containsRoleKeyword(roleCode, roleName, ["WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO"]);
  const isAccounting = containsRoleKeyword(roleCode, roleName, ["ACCOUNTANT", "FINANCE", "KE_TOAN", "CHIEF_ACCOUNTANT", "CFO"]);

  if (["PARTS"].includes(normalizedSection)) return isWarehouse || isManager || isAdmin;
  if (["BILLING", "FINANCE"].includes(normalizedSection)) return isAccounting || isAdmin;
  if (["CLOSED", "GATE_PASS"].includes(normalizedSection)) return isAdmin;
  return isManager || isAdmin;
}

function normalizeServiceOrderEditRequestItem(raw) {
  const source = raw || {};
  const read = (...keys) => {
    for (const key of keys) {
      const value = source?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };

  const requestId = Number(read("requestId", "RequestId", "id", "Id") || 0);
  return {
    requestId: Number.isFinite(requestId) && requestId > 0 ? requestId : 0,
    section: String(read("section", "Section") || "WORKSHOP").trim().toUpperCase(),
    reason: String(read("reason", "Reason") || "").trim(),
    status: String(read("status", "Status") || "PENDING").trim().toUpperCase(),
    requestedByUserId: Number(read("requestedByUserId", "RequestedByUserId", "userId", "UserId") || 0),
    requestedAtUtc: read("requestedAtUtc", "RequestedAtUtc", "timestamp", "Timestamp"),
    decidedByUserId: Number(read("decidedByUserId", "DecidedByUserId") || 0),
    decidedAtUtc: read("decidedAtUtc", "DecidedAtUtc"),
    decisionNote: String(read("decisionNote", "DecisionNote") || "").trim(),
    decidedByRoleCode: String(read("decidedByRoleCode", "DecidedByRoleCode") || "").trim(),
    overrideUntilUtc: read("overrideUntilUtc", "OverrideUntilUtc"),
  };
}

function getRequestStatusLabel(status) {
  const normalized = String(status || "").trim().toUpperCase();
  if (normalized === "APPROVED") return "Đã duyệt";
  if (normalized === "REJECTED") return "Đã từ chối";
  return "Chờ duyệt";
}

function getRequestStatusColor(status) {
  const normalized = String(status || "").trim().toUpperCase();
  if (normalized === "APPROVED") return "#166534";
  if (normalized === "REJECTED") return "#b91c1c";
  return "#9a3412";
}
function normalizeService(service) {
  const id = Number(readField(service, ["id", "Id"], 0));
  const categoryId = Number(readField(service, ["categoryId", "CategoryId"], 0));
  const rawName = String(readField(service, ["name", "Name"], "") || "").trim();
  return {
    id,
    categoryId,
    code: String(readField(service, ["code", "Code"], "") || ""),
    name: getInspectionTriadDisplayName(service, rawName),
    engineType: String(readField(service, ["engineType", "EngineType"], "") || ""),
    minutes: Math.max(0, Number(readField(service, ["standardMinutes", "StandardMinutes"], 0) || 0)),
    price: Math.max(0, Number(readField(service, ["basePrice", "BasePrice"], 0) || 0)),
    items: readField(service, ["items", "Items"], []),
  };
}

function normalizeServiceOrderDetail(detail) {
  const source = String(readField(detail, ["serviceSource", "ServiceSource"], "INITIAL") || "INITIAL")
    .trim()
    .toUpperCase();
  const reason = String(readField(detail, ["supplementaryReasonCode", "SupplementaryReasonCode"], "") || "")
    .trim()
    .toUpperCase();
  const isSupplementary = Boolean(readField(detail, ["isSupplementary", "IsSupplementary"], false))
    || source === SUPPLEMENTARY_SOURCE_CUSTOMER
    || source === SUPPLEMENTARY_SOURCE_TECHNICIAN
    || Boolean(reason);

  return {
    id: Number(readField(detail, ["id", "Id"], 0)),
    itemType: String(readField(detail, ["itemType", "ItemType"], "") || "").trim().toUpperCase(),
    quotationDetailId: readField(detail, ["quotationDetailId", "QuotationDetailId"], null),
    standardServiceId: Number(readField(detail, ["standardServiceId", "StandardServiceId"], 0)),
    itemId: Number(readField(detail, ["itemId", "ItemId"], 0)),
    itemName: String(readField(detail, ["itemName", "ItemName"], "") || "").trim(),
    note: String(readField(detail, ["note", "Note"], "") || "").trim(),
    status: String(readField(detail, ["status", "Status"], "") || "").trim().toUpperCase(),
    unitPriceAtTime: Number(readField(detail, ["unitPriceAtTime", "UnitPriceAtTime"], 0) || 0),
    quantity: Number(readField(detail, ["quantity", "Quantity"], 0) || 0),
    isSupplementary,
    serviceSource: source || "INITIAL",
    supplementaryReasonCode: reason,
  };
}

function isLaborOrServiceDetail(detail) {
  const itemType = String(detail?.itemType || "").toUpperCase();
  return itemType === "LABOR" || itemType === "SERVICE";
}

function isInitialEditableDetail(detail) {
  if (!isLaborOrServiceDetail(detail)) return false;
  if (detail?.quotationDetailId) return false;
  return !detail?.isSupplementary;
}

function getSupplementarySourceLabel(source) {
  if (source === SUPPLEMENTARY_SOURCE_TECHNICIAN) return "Phát sinh sau chẩn đoán";
  if (source === SUPPLEMENTARY_SOURCE_CUSTOMER) return "Khách yêu cầu thêm";
  return "Phát sinh";
}

function buildCategoryOptions(serviceCategories, normalizedServices) {
  const map = new Map();

  (serviceCategories || []).forEach((category) => {
    const id = Number(readField(category, ["id", "Id"], 0));
    if (!Number.isFinite(id) || id <= 0) return;
    if (category?.isDeleted === true || category?.isActive === false) return;
    const name = String(readField(category, ["categoryName", "name", "Name"], "") || "").trim();
    if (!name) return;
    map.set(id, { id, name });
  });

  (normalizedServices || []).forEach((service) => {
    if (!Number.isFinite(service.categoryId) || service.categoryId <= 0) return;
    if (!map.has(service.categoryId)) {
      map.set(service.categoryId, { id: service.categoryId, name: `Nhóm ${service.categoryId}` });
    }
  });

  return Array.from(map.values()).sort((a, b) => String(a.name).localeCompare(String(b.name), "vi"));
}

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

function formatDateTimeValue(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";
  return parsed.toLocaleString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function toLocalDateTimeValue(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  const hour = String(parsed.getHours()).padStart(2, "0");
  const minute = String(parsed.getMinutes()).padStart(2, "0");
  return `${year}-${month}-${day}T${hour}:${minute}:00`;
}

function hasServiceOrderEditWindowPassed(order) {
  const normalizedStatus = String(readField(order, ["status", "Status"], "") || "")
    .trim()
    .toUpperCase()
    .replace(/-/g, "_")
    .replace(/\s+/g, "_");
  const actualHandoverRaw = readField(order, ["actualHandover", "ActualHandover"], null);
  const hasActualHandover = Boolean(actualHandoverRaw);
  return hasActualHandover
    || ["APPROVED", "IN_PROGRESS", "REPAIRING", "READY", "WAITING_PAYMENT", "CLOSED", "CANCELED", "CANCELED"].includes(normalizedStatus);
}

function normalizeServiceOrderEditCapability(payload) {
  if (!payload || typeof payload !== "object") return null;
  const read = (...keys) => {
    for (const key of keys) {
      const value = payload?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };

  return {
    canEditWorkshop: Boolean(read("canEditWorkshop", "CanEditWorkshop")),
    isWindowPassed: Boolean(read("isWindowPassed", "IsWindowPassed")),
    isQuoteLocked: Boolean(read("isQuoteLocked", "IsQuoteLocked")),
    isAssignmentLocked: Boolean(read("isAssignmentLocked", "IsAssignmentLocked")),
    isPartsLocked: Boolean(read("isPartsLocked", "IsPartsLocked")),
    isBillingLocked: Boolean(read("isBillingLocked", "IsBillingLocked")),
    isClosedLocked: Boolean(read("isClosedLocked", "IsClosedLocked")),
    isOverrideActive: Boolean(read("isOverrideActive", "IsOverrideActive")),
    canOverrideWorkshop: Boolean(read("canOverrideWorkshop", "CanOverrideWorkshop")),
    canOverrideQuote: Boolean(read("canOverrideQuote", "CanOverrideQuote")),
    canOverrideAssignment: Boolean(read("canOverrideAssignment", "CanOverrideAssignment")),
    canOverrideParts: Boolean(read("canOverrideParts", "CanOverrideParts")),
    canOverrideBilling: Boolean(read("canOverrideBilling", "CanOverrideBilling")),
    canOverrideClosed: Boolean(read("canOverrideClosed", "CanOverrideClosed")),
    overrideRequestId: Number(read("overrideRequestId", "OverrideRequestId")) > 0 ? Number(read("overrideRequestId", "OverrideRequestId")) : null,
    overrideUntilUtc: read("overrideUntilUtc", "OverrideUntilUtc"),
    blockingReason: String(read("blockingReason", "BlockingReason") || "").trim(),
  };
}

function parseCustomerNote(rawCustomerNotes) {
  const rawText = String(rawCustomerNotes || "").trim();
  if (!rawText) return "";

  try {
    const parsed = JSON.parse(rawText);
    const fromObject =
      parsed && typeof parsed === "object" && Array.isArray(parsed.symptoms) ? parsed.symptoms : Array.isArray(parsed) ? parsed : null;
    if (fromObject) {
      return fromObject
        .map((value) => String(value || "").trim())
        .filter((line) => line && normalizeSearchText(line) !== "khong co ghi chu gi" && normalizeSearchText(line) !== "khong co ghi chu")
        .join("; ");
    }
    if (typeof parsed === "string") {
      const line = parsed.trim();
      return line || "";
    }
  } catch {
    return rawText;
  }

  return "";
}

function getApiErrorMessage(err) {
  const fallback = "Không thể cập nhật lệnh sửa chữa. Vui lòng kiểm tra dữ liệu và thử lại.";
  const data = err?.response?.data;
  const serverErrors = data?.errors && typeof data.errors === "object" ? Object.values(data.errors).flat() : [];
  if (serverErrors.length > 0) return String(serverErrors[0] || fallback);
  const raw = data?.message || data?.error || data?.detail || data?.title || err?.message || "";
  const text = String(raw);
  return text || fallback;
}

function calculateEstimatedFinish(checkInText, totalMinutes) {
  if (!checkInText || totalMinutes <= 0) return { finishIso: "", finishText: "" };
  const finishDate = addMinutesWithinWorkingHours(checkInText, totalMinutes, {
    openHour: 8,
    closeHour: 20,
  });
  if (!finishDate) return { finishIso: "", finishText: "" };

  const finishIso = toLocalDateTimeValue(finishDate);
  const finishText = formatDateTimeValue(finishIso);
  return { finishIso, finishText };
}

export default function ServiceOrderEditPage() {
  const { orderId } = useParams();
  const navigate = useNavigate();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [requestingEdit, setRequestingEdit] = useState(false);
  const [loadingEditRequests, setLoadingEditRequests] = useState(false);
  const [decidingRequestId, setDecidingRequestId] = useState(0);
  const [error, setError] = useState("");
  const [requestEditReason, setRequestEditReason] = useState("");
  const [requestEditNotice, setRequestEditNotice] = useState("");
  const [editRequests, setEditRequests] = useState([]);
  const [decisionNotesByRequestId, setDecisionNotesByRequestId] = useState({});
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});
  const scrollToPageTop = useCallback(() => {
    if (typeof window === "undefined") return;
    window.scrollTo({ top: 0, behavior: "smooth" });
  }, []);

  const [order, setOrder] = useState(null);
  const [serviceDetails, setServiceDetails] = useState([]);
  const [editCapability, setEditCapability] = useState(null);
  const [staff, setStaff] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [insuranceCompanies, setInsuranceCompanies] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [models, setModels] = useState([]);
  const [branchOperatingHours, setBranchOperatingHours] = useState([]);

  const [serviceSearch, setServiceSearch] = useState("");
  const [activeCategoryId, setActiveCategoryId] = useState("");
  const [supplementaryReason, setSupplementaryReason] = useState(SUPPLEMENTARY_REASON_TECH);
  const [supplementaryServiceId, setSupplementaryServiceId] = useState("");
  const [supplementaryNote, setSupplementaryNote] = useState("");
  const [addingSupplementary, setAddingSupplementary] = useState(false);
  const [formData, setFormData] = useState(DEFAULT_FORM_DATA);
  const [insuranceUploadState, setInsuranceUploadState] = useState({});
  const [customerNote, setCustomerNote] = useState("");
  const pickerMinDate = useMemo(() => new Date(2000, 0, 1), []);
  const roleCode = String(user?.roleCode ?? user?.RoleCode ?? "").trim();
  const roleName = String(user?.roleName ?? user?.RoleName ?? "").trim();

  useEffect(() => {
    if (!String(error || "").trim()) return;
    scrollToPageTop();
  }, [error, scrollToPageTop]);

  const effectiveBranchId = useMemo(() => {
    const fromContext = parsePositiveId(selectedBranchId);
    if (fromContext > 0) return fromContext;
    return parsePositiveId(user?.branchId ?? user?.BranchId);
  }, [selectedBranchId, user?.BranchId, user?.branchId]);
  const fallbackEditWindowPassed = hasServiceOrderEditWindowPassed(order);
  const isEditLocked = editCapability
    ? !editCapability.canEditWorkshop
    : fallbackEditWindowPassed;
  const isSupplementaryLocked = editCapability
    ? Boolean(
      (editCapability.isClosedLocked && !editCapability.canOverrideClosed)
      || (editCapability.isBillingLocked && !editCapability.canOverrideBilling)
      || (editCapability.isPartsLocked && !editCapability.canOverrideParts)
      || (editCapability.isAssignmentLocked && !editCapability.canOverrideAssignment)
      || (editCapability.isQuoteLocked && !editCapability.canOverrideQuote)
      || (editCapability.isWindowPassed && !editCapability.canOverrideWorkshop)
    )
    : fallbackEditWindowPassed;
  const editLockedMessage = editCapability?.blockingReason || EDIT_WINDOW_PASSED_MESSAGE;
  const overrideUntilText = useMemo(() => formatDateTimeValue(editCapability?.overrideUntilUtc), [editCapability?.overrideUntilUtc]);
  const hasPendingRequest = useMemo(
    () => editRequests.some((item) => String(item?.status || "").toUpperCase() === "PENDING"),
    [editRequests]
  );
  const actorStaffId = useMemo(
    () => parsePositiveId(user?.staffId ?? user?.StaffId ?? user?.id ?? user?.Id),
    [user?.Id, user?.StaffId, user?.id, user?.staffId]
  );
  const initialServiceDetails = useMemo(
    () => (Array.isArray(serviceDetails) ? serviceDetails.filter((detail) => isInitialEditableDetail(detail)) : []),
    [serviceDetails]
  );
  const supplementaryServiceDetails = useMemo(
    () => (Array.isArray(serviceDetails) ? serviceDetails.filter((detail) => detail?.isSupplementary && isLaborOrServiceDetail(detail)) : []),
    [serviceDetails]
  );

  const normalizedServices = useMemo(() => {
    return (services || []).map(normalizeService).filter((service) => service.id !== 0 && service.name);
  }, [services]);

  const categoryOptions = useMemo(
    () => buildCategoryOptions(serviceCategories, normalizedServices),
    [serviceCategories, normalizedServices]
  );
  const categoryNameById = useMemo(() => {
    const map = new Map();
    categoryOptions.forEach((category) => {
      const id = Number(category?.id || 0);
      if (!Number.isFinite(id) || id <= 0) return;
      map.set(id, String(category?.name || "").trim() || `Nhóm ${id}`);
    });
    return map;
  }, [categoryOptions]);

  const serviceById = useMemo(
    () => new Map(normalizedServices.map((service) => [Number(service.id), service])),
    [normalizedServices]
  );
  const flexibleTriggerServiceIdSet = useMemo(
    () => buildFlexibleTriggerServiceIdSet(normalizedServices),
    [normalizedServices]
  );
  const insuranceFlexiblePackageIdSet = useMemo(
    () => buildInsuranceFlexiblePackageIdSet(normalizedServices),
    [normalizedServices]
  );

  const customerById = useMemo(() => {
    const map = new Map();
    (customers || []).forEach((item) => {
      const id = Number(readField(item, ["id", "Id"], 0));
      if (id > 0) map.set(id, item);
    });
    return map;
  }, [customers]);

  const vehicleById = useMemo(() => {
    const map = new Map();
    (vehicles || []).forEach((item) => {
      const id = Number(readField(item, ["id", "Id"], 0));
      if (id > 0) map.set(id, item);
    });
    return map;
  }, [vehicles]);

  const modelInfoById = useMemo(() => {
    const map = new Map();
    (models || []).forEach((item) => {
      const id = Number(readField(item, ["id", "Id"], 0));
      if (!Number.isFinite(id) || id <= 0) return;
      map.set(id, {
        fuelType: String(readField(item, ["fuelType", "FuelType"], "") || "").trim(),
      });
    });
    return map;
  }, [models]);

  const currentVehicleModelIds = useMemo(() => {
    const vehicleId = parsePositiveId(order?.vehicleId ?? order?.VehicleId);
    const vehicle = vehicleById.get(vehicleId) || null;
    const modelId = Number(
      readField(vehicle, ["modelId", "ModelId", "vehicleModelId", "VehicleModelId"], 0)
        || readField(readField(vehicle, ["model", "Model", "vehicleModel", "VehicleModel"], null), ["id", "Id"], 0)
    );
    return Number.isFinite(modelId) && modelId > 0 ? [modelId] : [];
  }, [order?.VehicleId, order?.vehicleId, vehicleById]);

  const selectedServices = useMemo(() => {
    return toNumberList(formData.serviceIds)
      .map((serviceId) => serviceById.get(serviceId))
      .filter(Boolean);
  }, [formData.serviceIds, serviceById]);
  const serviceGroupsByCategory = useMemo(
    () =>
      Array.from(
        selectedServices.reduce((map, service) => {
          const categoryId = Number(service?.categoryId || 0);
          const categoryName = categoryNameById.get(categoryId) || `Nhóm ${categoryId || "Khác"}`;
          const serviceName = String(service?.name || "").trim();
          if (!serviceName) return map;
          if (!map.has(categoryName)) map.set(categoryName, []);
          map.get(categoryName).push(serviceName);
          return map;
        }, new Map())
      ).map(([categoryName, serviceNames]) => ({
        categoryName,
        serviceNames: Array.from(new Set(serviceNames)),
      })),
    [categoryNameById, selectedServices]
  );

  const hasInsuranceSignalServiceSelected = useMemo(
    () => selectedServices.some((service) => isInsuranceSignalService(service)),
    [selectedServices]
  );

  const isInsuranceOrderEnabled = useMemo(
    () =>
      isInsuranceOrderActive({
        insuranceEnabled: formData.insuranceEnabled,
        orderCategory: formData.orderCategory,
        insuranceCompanyId: formData.insuranceCompanyId,
        services: selectedServices,
      }),
    [formData.insuranceCompanyId, formData.insuranceEnabled, formData.orderCategory, selectedServices]
  );

  const insuranceDraftBreakdown = useMemo(
    () =>
      buildInsuranceDraftBreakdown({
        services: selectedServices,
        enabled: isInsuranceOrderEnabled,
      }),
    [isInsuranceOrderEnabled, selectedServices]
  );

  const filteredServices = useMemo(() => {
    const term = normalizeSearchText(serviceSearch);
    const activeCategory = parsePositiveId(activeCategoryId);
    const compatibleServices = filterBookableItemsForVehicleModels(
      normalizedServices,
      currentVehicleModelIds,
      modelInfoById,
      serviceById
    );

    return compatibleServices
      .filter((service) => (activeCategory > 0 ? Number(service.categoryId) === activeCategory : true))
      .filter((service) => (term ? normalizeSearchText(service.name).includes(term) : true))
      .sort((a, b) => String(a.name || "").localeCompare(String(b.name || ""), "vi"));
  }, [activeCategoryId, currentVehicleModelIds, modelInfoById, normalizedServices, serviceById, serviceSearch]);

  const compatibleCategoryIds = useMemo(
    () =>
      collectCompatibleCategoryIds(
        normalizedServices,
        currentVehicleModelIds,
        modelInfoById,
        serviceById
      ),
    [currentVehicleModelIds, modelInfoById, normalizedServices, serviceById]
  );

  const visibleCategoryOptions = useMemo(
    () => categoryOptions.filter((category) => compatibleCategoryIds.has(Number(category.id))),
    [categoryOptions, compatibleCategoryIds]
  );

  const {
    totalMinutes,
    totalPrice,
    bookingFlow,
    suggestedFinish,
    isLongDurationFlow,
    isServiceDrivenFlexibleFlow,
  } = useMemo(() => {
    const serviceMinutes = sumServiceMinutes(selectedServices);
    const executionMinutesValue = calculateRoundedOccupancyMinutes(serviceMinutes);
    const flowState = resolveBookingFlowByItemsAndDuration({
      itemIds: selectedServices.map((service) => Number(service?.id || 0)),
      flexibleTriggerServiceIdSet,
      insuranceFlexiblePackageIdSet,
      executionMinutes: executionMinutesValue,
      workingDayMinutes: WORKING_DAY_MINUTES,
    });
    const price = selectedServices.reduce((sum, service) => sum + Number(service?.price || 0), 0);
    return {
      totalMinutes: serviceMinutes,
      totalPrice: price,
      bookingFlow: flowState.bookingFlow,
      isLongDurationFlow: flowState.isLongDurationFlow,
      isServiceDrivenFlexibleFlow: flowState.isServiceDrivenFlexibleFlow,
      suggestedFinish: calculateEstimatedFinish(formData.checkInTime, executionMinutesValue),
    };
  }, [flexibleTriggerServiceIdSet, formData.checkInTime, insuranceFlexiblePackageIdSet, selectedServices]);

  const customerDisplay = useMemo(() => {
    const customerId = parsePositiveId(order?.customerId ?? order?.CustomerId);
    const customer = customerById.get(customerId);
    const fullName = String(readField(customer, ["fullName", "FullName"], "") || "").trim();
    const phone = String(readField(customer, ["phone", "Phone"], "") || "").trim();
    return {
      fullName: fullName || (customerId > 0 ? `Khách hàng #${customerId}` : "Chưa xác định"),
      phone: phone || "Chưa có số điện thoại",
    };
  }, [customerById, order?.CustomerId, order?.customerId]);

  const vehicleDisplay = useMemo(() => {
    const vehicleId = parsePositiveId(order?.vehicleId ?? order?.VehicleId);
    const vehicle = vehicleById.get(vehicleId);
    const plate = String(readField(vehicle, ["licensePlate", "LicensePlate"], "") || "").trim();
    const modelName = String(readField(vehicle, ["modelName", "ModelName", "vehicleModelName", "VehicleModelName"], "") || "").trim();
    const color = String(readField(vehicle, ["color", "Color"], "") || "").trim();
    const descriptor = [modelName, color].filter(Boolean).join(" - ");
    return {
      plate: plate ? formatLicensePlate(plate) : "Chưa có biển số",
      descriptor: descriptor || "Chưa rõ dòng xe",
    };
  }, [order?.VehicleId, order?.vehicleId, vehicleById]);

  const slotSelectionGuide = useMemo(
    () =>
      buildSlotSelectionGuide({
        bookingFlow,
        isLongDurationFlow,
        isServiceDrivenFlexibleFlow,
        inProgress: true,
      }),
    [bookingFlow, isLongDurationFlow, isServiceDrivenFlexibleFlow]
  );

  const completionWarningText = useMemo(() => {
    if (bookingFlow !== BOOKING_FLOW.FLEXIBLE) return "";
    if (!suggestedFinish?.finishIso) return "";
    return buildFlexibleCompletionWarningMessage({
      vehicleLabel: vehicleDisplay.plate,
      completionValue: suggestedFinish.finishIso,
    });
  }, [bookingFlow, suggestedFinish?.finishIso, vehicleDisplay.plate]);

  useEffect(() => {
    const nextOrderCategory = isInsuranceOrderEnabled ? INSURANCE_ORDER_CATEGORY : "GENERAL_REPAIR";
    if (String(formData.orderCategory || "").trim().toUpperCase() === nextOrderCategory) return;
    setFormData((prev) => ({ ...prev, orderCategory: nextOrderCategory }));
  }, [formData.orderCategory, isInsuranceOrderEnabled]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setLoadingEditRequests(true);
    setError("");
    try {
      const [
        orderRes,
        capabilityRes,
        requestRes,
        staffRes,
        roleRes,
        categoryRes,
        serviceRes,
        packageRes,
        detailsRes,
        insuranceRes,
        customerRes,
        vehicleRes,
        modelRes,
        operatingHoursRes
      ] = await Promise.all([
        serviceOrderApi.getById(orderId),
        serviceOrderApi.getEditCapabilities(orderId).catch(() => null),
        serviceOrderApi.getEditRequests(orderId).catch(() => []),
        getAllStaff(),
        getAllRoles(),
        getAllServiceCategories(),
        getAllStandardServices(),
        getAllPackages().catch(() => []),
        serviceOrderApi.getDetails(orderId).catch(() => []),
        insuranceCompanyApi.getAll().catch(() => []),
        getAllCustomers(),
        getAllVehicles(),
        getVehicleModels().catch(() => []),
        getAllBranchOperatingHours().catch(() => []),
      ]);

      if (!orderRes) {
        throw new Error("Không tìm thấy lệnh sửa chữa.");
      }

      const staffList = Array.isArray(staffRes) ? staffRes : [];
      const roleList = Array.isArray(roleRes) ? roleRes : [];
      const roleCodeById = new Map(
        roleList.map((role) => [
          Number(readField(role, ["id", "Id"], 0)),
          String(readField(role, ["roleCode", "RoleCode"], "") || "").toUpperCase(),
        ])
      );

      const filteredStaff = staffList.filter((item) => {
        const roleId = Number(readField(item, ["roleId", "RoleId"], 0));
        const roleCode = roleCodeById.get(roleId) || "";
        const isMatchedRole = roleCode === "RECEPTIONIST" || roleCode === "MANAGER";
        const staffBranchId = Number(readField(item, ["branchId", "BranchId"], 0));
        const isMatchedBranch = !effectiveBranchId || staffBranchId === effectiveBranchId;
        return isMatchedRole && isMatchedBranch;
      });

      const normalizedDetails = (Array.isArray(detailsRes) ? detailsRes : [])
        .map((detail) => normalizeServiceOrderDetail(detail))
        .filter((detail) => Number.isFinite(detail.id) && detail.id > 0);

      const currentServiceIds = normalizedDetails
        .filter((detail) => isInitialEditableDetail(detail))
        .map((detail) => Number(detail.standardServiceId || detail.itemId || 0))
        .filter((id) => Number.isFinite(id) && id > 0);

      const initialFormData = {
        ...DEFAULT_FORM_DATA,
        orderCategory: String(readField(orderRes, ["orderCategory", "OrderCategory"], "GENERAL_REPAIR")),
        insuranceEnabled: Boolean(readField(orderRes, ["insuranceEnabled", "InsuranceEnabled"])),
        insuranceCompanyId: readField(orderRes, ["insuranceCompanyId", "InsuranceCompanyId"], "") || "",
        insuranceDeductibleAmount: readField(orderRes, ["insuranceDeductibleAmount", "InsuranceDeductibleAmount"], null),
        insuranceDeductibleOption: String(readField(orderRes, ["insuranceDeductibleOption", "InsuranceDeductibleOption"], "") || ""),
        insuranceClaimMode: String(readField(orderRes, ["insuranceClaimMode", "InsuranceClaimMode"], "") || ""),
        insuranceQuoteState: String(readField(orderRes, ["insuranceQuoteState", "InsuranceQuoteState"], "") || ""),
        insuranceCardImageUrls: readField(orderRes, ["insuranceCardImageUrls", "InsuranceCardImageUrls"], []) || [],
        insuranceDamageImageUrls: readField(orderRes, ["insuranceDamageImageUrls", "InsuranceDamageImageUrls"], []) || [],
        serviceAdvisorId: readField(orderRes, ["serviceAdvisorId", "ServiceAdvisorId"], "") || "",
        oldPartsReturnOption: String(readField(orderRes, ["oldPartsReturnOption", "OldPartsReturnOption"], "DISPOSE_ALL")),
        requiredDepositAmount: Number(readField(orderRes, ["requiredDepositAmount", "RequiredDepositAmount"], 0)),
        checkInTime: toLocalDateTimeValue(readField(orderRes, ["checkInTime", "CheckInTime"])),
        estimatedHandover: toLocalDateTimeValue(readField(orderRes, ["estimatedHandover", "EstimatedHandover"])),
        promisedHandoverTime: toLocalDateTimeValue(readField(orderRes, ["promisedHandoverTime", "PromisedHandoverTime"])),
        status: String(readField(orderRes, ["status", "Status"], "NEW")),
        serviceIds: currentServiceIds,
      };

      setOrder(orderRes);
      setServiceDetails(normalizedDetails);
      setEditCapability(normalizeServiceOrderEditCapability(capabilityRes));
      setEditRequests((Array.isArray(requestRes) ? requestRes : []).map(normalizeServiceOrderEditRequestItem).filter(r => r.requestId > 0));
      setStaff(filteredStaff);
      setServiceCategories(Array.isArray(categoryRes) ? categoryRes : []);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setInsuranceCompanies(Array.isArray(insuranceRes) ? insuranceRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setModels(Array.isArray(modelRes) ? modelRes : []);
      setBranchOperatingHours(Array.isArray(operatingHoursRes) ? operatingHoursRes : []);
      setFormData(initialFormData);
      setCustomerNote(parseCustomerNote(readField(orderRes, ["customerNotes", "CustomerNotes"])));

    } catch (err) {
      setError(getApiErrorMessage(err) || "Không thể tải dữ liệu lệnh sửa chữa.");
    } finally {
      setLoading(false);
      setLoadingEditRequests(false);
    }
  }, [effectiveBranchId, orderId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const refreshServiceOrderDetails = useCallback(async () => {
    const details = await serviceOrderApi.getDetails(orderId).catch(() => []);
    const normalizedDetails = (Array.isArray(details) ? details : [])
      .map((detail) => normalizeServiceOrderDetail(detail))
      .filter((detail) => Number.isFinite(detail.id) && detail.id > 0);
    setServiceDetails(normalizedDetails);
  }, [orderId]);

  const handleAddSupplementaryService = useCallback(async () => {
    const serviceId = parsePositiveId(supplementaryServiceId);
    if (!serviceId) {
      setError("Vui lòng chọn dịch vụ phát sinh cần thêm.");
      return;
    }

    const selectedService = serviceById.get(serviceId);
    if (!selectedService) {
      setError("Không tìm thấy dịch vụ phát sinh đã chọn.");
      return;
    }

    const isDuplicate = supplementaryServiceDetails.some((detail) => Number(detail.standardServiceId || detail.itemId || 0) === serviceId);
    if (isDuplicate) {
      setError("Dịch vụ phát sinh này đã tồn tại trong SO.");
      return;
    }

    const normalizedReason = String(supplementaryReason || SUPPLEMENTARY_REASON_TECH).trim().toUpperCase();
    const serviceSource = normalizedReason === SUPPLEMENTARY_REASON_CUSTOMER
      ? SUPPLEMENTARY_SOURCE_CUSTOMER
      : SUPPLEMENTARY_SOURCE_TECHNICIAN;
    const quantity = 1;
    const unitPrice = Math.max(0, Number(selectedService.price || 0));
    const subTotal = quantity * unitPrice;
    const nowIso = new Date().toISOString();

    setAddingSupplementary(true);
    setError("");
    try {
      await serviceOrderApi.createDetail({
        serviceOrderId: Number(orderId),
        serviceSource,
        isSupplementary: true,
        supplementaryReasonCode: normalizedReason,
        supplementaryRequestedByStaffId: actorStaffId > 0 ? actorStaffId : null,
        supplementaryRequestedAt: nowIso,
        customerApprovedAt: nowIso,
        standardServiceId: serviceId,
        itemType: "LABOR",
        itemId: serviceId,
        itemName: selectedService.name,
        description: normalizedReason === SUPPLEMENTARY_REASON_CUSTOMER
          ? "Dịch vụ khách yêu cầu bổ sung."
          : "Dịch vụ phát sinh sau chẩn đoán.",
        quantity,
        unitPriceAtTime: unitPrice,
        unit: "Lần",
        subTotal,
        taxRate: 0,
        taxAmount: 0,
        finalAmount: subTotal,
        payerType: "CUSTOMER",
        status: SUPPLEMENTARY_DEFAULT_STATUS,
        note: String(supplementaryNote || "").trim() || null,
        createdBy: actorStaffId > 0 ? actorStaffId : null,
        updatedBy: actorStaffId > 0 ? actorStaffId : null,
      });

      await refreshServiceOrderDetails();
      setSupplementaryServiceId("");
      setSupplementaryNote("");
    } catch (err) {
      setError(getApiErrorMessage(err) || "Không thể thêm dịch vụ phát sinh.");
    } finally {
      setAddingSupplementary(false);
    }
  }, [
    actorStaffId,
    orderId,
    refreshServiceOrderDetails,
    serviceById,
    supplementaryNote,
    supplementaryReason,
    supplementaryServiceDetails,
    supplementaryServiceId,
  ]);

  const validateForm = useCallback(() => {
    const errors = {};
    if (!formData.checkInTime) errors.checkInTime = "Vui lòng chọn thời điểm xe vào xưởng.";
    if (!formData.serviceAdvisorId) errors.serviceAdvisorId = "Vui lòng chọn cố vấn dịch vụ.";
    if (toNumberList(formData.serviceIds).length === 0) errors.serviceIds = "Vui lòng chọn ít nhất một dịch vụ.";

    if (isInsuranceOrderEnabled) {
      if (!formData.insuranceCompanyId) errors.insuranceCompanyId = "Vui lòng chọn công ty bảo hiểm.";
      if (!formData.insuranceDeductibleOption) errors.insuranceDeductibleOption = "Vui lòng chọn tùy chọn mức miễn thường.";
      if (!formData.insuranceClaimMode) errors.insuranceClaimMode = "Vui lòng chọn hình thức bồi thường.";
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }, [formData, isInsuranceOrderEnabled]);

  const showError = useCallback((field) => submitAttempted && Boolean(fieldErrors[field]), [fieldErrors, submitAttempted]);

  const checkInSelectionGuardMessage = useMemo(() => {
    if (isBranchClosedBySchedule(formData.checkInTime, branchOperatingHours)) {
      return getBranchClosedMessage(branchOperatingHours);
    }
    return "";
  }, [branchOperatingHours, formData.checkInTime]);

  const handleFieldValueChange = useCallback((field, value) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (submitAttempted) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next[field];
        return next;
      });
    }
  }, [submitAttempted]);

  const handleInsuranceToggle = useCallback(() => {
    if (hasInsuranceSignalServiceSelected) return;
    setFormData((prev) => ({ ...prev, insuranceEnabled: !prev.insuranceEnabled }));
  }, [hasInsuranceSignalServiceSelected]);

  const handleInsuranceDeductibleOptionChange = useCallback((option) => {
    const amount = resolveInsuranceDeductibleAmount(option);
    setFormData((prev) => ({
      ...prev,
      insuranceDeductibleOption: option,
      insuranceDeductibleAmount: amount,
    }));
  }, []);

  const handleInsuranceClaimModeChange = useCallback((mode) => {
    const state = resolveInsuranceQuoteState(mode);
    setFormData((prev) => ({
      ...prev,
      insuranceClaimMode: mode,
      insuranceQuoteState: state,
    }));
  }, []);

  const handleInsuranceAssetUpload = useCallback((type, url) => {
    setFormData((prev) => {
      const field = type === INSURANCE_ASSET_TYPE.CARD ? "insuranceCardImageUrls" : "insuranceDamageImageUrls";
      const current = Array.isArray(prev[field]) ? prev[field] : [];
      if (current.includes(url)) return prev;
      return { ...prev, [field]: [...current, url] };
    });
  }, []);

  const handleInsuranceAssetRemove = useCallback((type, url) => {
    setFormData((prev) => {
      const field = type === INSURANCE_ASSET_TYPE.CARD ? "insuranceCardImageUrls" : "insuranceDamageImageUrls";
      const current = Array.isArray(prev[field]) ? prev[field] : [];
      return { ...prev, [field]: current.filter((item) => item !== url) };
    });
  }, []);

  const handleChange = useCallback((event) => {
    const { name, value, type, checked } = event.target;
    handleFieldValueChange(name, type === "checkbox" ? checked : value);
  }, [handleFieldValueChange]);

  const handleServiceToggle = useCallback((serviceId) => {
    const id = Number(serviceId);
    if (!id) return;
    setFormData((prev) => {
      const current = toNumberList(prev.serviceIds);
      const next = current.includes(id) ? current.filter((item) => item !== id) : [...current, id];
      return { ...prev, serviceIds: next };
    });
  }, []);

  const handleSubmit = useCallback(
    async (event) => {
      event.preventDefault();
      setSubmitAttempted(true);
      setError("");

      if (!validateForm()) {
        scrollToPageTop();
        return;
      }

      setSaving(true);
      try {
        const payload = buildWorkshopEditPayload({
          formData,
          suggestedFinishIso: suggestedFinish.finishIso,
          insuranceActive: isInsuranceOrderEnabled,
          customerNote,
        });

        await serviceOrderApi.updateWorkshop(orderId, payload);
        navigate(`/workshop/reception?viewOrderId=${orderId}`);
      } catch (err) {
        setError(getApiErrorMessage(err));
      } finally {
        setSaving(false);
      }
    },
    [customerNote, formData, isInsuranceOrderEnabled, navigate, orderId, suggestedFinish.finishIso, validateForm, scrollToPageTop]
  );

  const handleRequestEdit = useCallback(async () => {
    if (!isEditLocked) {
      return;
    }

    const reason = String(requestEditReason || "").trim();
    if (!reason) {
      setError(REQUEST_EDIT_REASON_REQUIRED);
      return;
    }

    setRequestingEdit(true);
    setError("");
    setRequestEditNotice("");
    try {
      const response = await serviceOrderApi.requestEdit(orderId, {
        section: "WORKSHOP",
        reason,
      });
      const [latestCapabilityRaw, latestRequestsRaw] = await Promise.all([
        serviceOrderApi.getEditCapabilities(orderId).catch(() => null),
        serviceOrderApi.getEditRequests(orderId).catch(() => []),
      ]);
      setEditCapability(normalizeServiceOrderEditCapability(latestCapabilityRaw));
      setEditRequests(
        (Array.isArray(latestRequestsRaw) ? latestRequestsRaw : [])
          .map(normalizeServiceOrderEditRequestItem)
          .filter((item) => item.requestId > 0)
      );
      setRequestEditNotice(String(response?.message || REQUEST_EDIT_SUCCESS));
      setRequestEditReason("");
    } catch (err) {
      setError(getApiErrorMessage(err) || REQUEST_EDIT_ERROR);
    } finally {
      setRequestingEdit(false);
    }
  }, [isEditLocked, orderId, requestEditReason]);

  const handleDecisionNoteChange = useCallback((requestId, value) => {
    const safeId = Number(requestId);
    if (!Number.isFinite(safeId) || safeId <= 0) return;
    setDecisionNotesByRequestId((prev) => ({
      ...prev,
      [safeId]: value,
    }));
  }, []);

  const handleDecideRequest = useCallback(async (request, approve) => {
    const safeRequestId = Number(request?.requestId || 0);
    if (!Number.isFinite(safeRequestId) || safeRequestId <= 0) return;
    if (!canApproveServiceOrderSection(roleCode, roleName, request?.section)) return;

    const note = String(decisionNotesByRequestId[safeRequestId] || "").trim();
    if (!approve && !note) {
      setError("Vui lòng nhập lý do từ chối.");
      return;
    }

    setDecidingRequestId(safeRequestId);
    setError("");
    setRequestEditNotice("");
    try {
      const response = await serviceOrderApi.decideEditRequest(orderId, safeRequestId, {
        decision: approve ? "APPROVE" : "REJECT",
        note: note || null,
      });

      const [latestCapabilityRaw, latestRequestsRaw] = await Promise.all([
        serviceOrderApi.getEditCapabilities(orderId).catch(() => null),
        serviceOrderApi.getEditRequests(orderId).catch(() => []),
      ]);
      setEditCapability(normalizeServiceOrderEditCapability(latestCapabilityRaw));
      setEditRequests(
        (Array.isArray(latestRequestsRaw) ? latestRequestsRaw : [])
          .map(normalizeServiceOrderEditRequestItem)
          .filter((item) => item.requestId > 0)
      );
      setRequestEditNotice(String(response?.message || (approve ? "Đã duyệt yêu cầu sửa." : "Đã từ chối yêu cầu sửa.")));
      setDecisionNotesByRequestId((prev) => ({
        ...prev,
        [safeRequestId]: "",
      }));
    } catch (err) {
      setError(getApiErrorMessage(err) || REQUEST_DECISION_ERROR);
    } finally {
      setDecidingRequestId(0);
    }
  }, [canApproveServiceOrderSection, decisionNotesByRequestId, orderId, roleCode, roleName]);

  if (loading) {
    return (
      <div className="create-order-page reception-create-appointment-page">
        <div className="reception-appt-section">
          <p className="reception-muted-note">Đang tải dữ liệu lệnh sửa chữa...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="create-order-page reception-create-appointment-page">
      <div className="page-header-actions reception-create-header">
        <button type="button" className="btn-back" onClick={() => navigate(-1)}>
          Quay lại Dashboard
        </button>
        <div className="reception-create-header-copy">
          <h2 className="page-title">{`Chỉnh sửa lệnh sửa chữa ${order?.code ? `#${order.code}` : ""}`}</h2>
          <p className="reception-create-header-note">
            Cập nhật dịch vụ và thông tin tiếp nhận theo luồng mới. Hệ thống chỉ lưu khi toàn bộ dữ liệu hợp lệ.
          </p>
        </div>
      </div>

      <div className="reception-appt-topbar">
        <div className="reception-appt-topbar-copy">
          <p className="reception-appt-topbar-kicker">SERVICE ORDER</p>
          <h3 className="reception-appt-topbar-title">Cập nhật thông tin tiếp nhận dịch vụ</h3>
          <p className="reception-appt-topbar-note">
            Luồng lưu theo cơ chế all-or-nothing: nếu có lỗi ở bất kỳ bước nào, hệ thống sẽ không ghi dữ liệu xuống cơ sở dữ liệu.
          </p>
        </div>
      </div>

      {error ? (
        <div className="reception-error-banner" role="alert">
          {error}
        </div>
      ) : null}

      {requestEditNotice ? (
        <div className="validation-summary" role="status">
          {requestEditNotice}
        </div>
      ) : null}

      {editCapability?.isOverrideActive ? (
        <div className="validation-summary" role="status">
          {`Đang mở khóa tạm thời đến: ${overrideUntilText}`}
        </div>
      ) : null}

      {submitAttempted && Object.keys(fieldErrors).length > 0 ? (
        <div className="validation-summary" role="alert">
          <strong>Vui lòng xử lý các lỗi sau:</strong>
          <ul>
            {Object.values(fieldErrors).map((message, index) => (
              <li key={`${message}-${index}`}>{message}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {isEditLocked ? (
        <div className="validation-summary" role="alert">
          {editLockedMessage}
        </div>
      ) : null}

      {isEditLocked ? (
        <section className="reception-appt-section" aria-live="polite">
          <div className="reception-appt-section-head">
            <h4 className="reception-appt-section-title">Yêu cầu mở khóa chỉnh sửa</h4>
          </div>
          <div className="form-group">
            <label htmlFor="service-order-edit-request-reason">Lý do yêu cầu sửa</label>
            <textarea
              id="service-order-edit-request-reason"
              rows={3}
              className="input-field"
              value={requestEditReason}
              onChange={(event) => setRequestEditReason(event.target.value)}
              placeholder="Nhập lý do để quản lý phê duyệt mở khóa"
            />
          </div>
          <div className="form-footer" style={{ marginTop: 12 }}>
            <button
              type="button"
              className="btn-primary-large"
              onClick={handleRequestEdit}
              disabled={requestingEdit || hasPendingRequest}
            >
              {requestingEdit ? "Đang gửi yêu cầu..." : hasPendingRequest ? "Đang có yêu cầu chờ duyệt" : "Gửi yêu cầu sửa"}
            </button>
          </div>
        </section>
      ) : null}

      <section className="reception-appt-section" aria-live="polite">
        <div className="reception-appt-section-head">
          <h4 className="reception-appt-section-title">Lịch sử yêu cầu mở khóa</h4>
        </div>
        {loadingEditRequests ? (
          <p className="reception-muted-note">Đang tải yêu cầu sửa...</p>
        ) : editRequests.length === 0 ? (
          <p className="reception-muted-note">Chưa có yêu cầu nào.</p>
        ) : (
          <div className="reception-plan-preview-list">
            {editRequests.map((request) => {
              const status = String(request.status || "").toUpperCase();
              const deciding = decidingRequestId === request.requestId;
              const canApprove = canApproveServiceOrderSection(roleCode, roleName, request.section);
              const isPending = status === "PENDING";
              const note = decisionNotesByRequestId[request.requestId] || "";

              return (
                <article key={request.requestId} className="reception-plan-preview-item">
                  <div className="reception-plan-preview-head">
                    <strong>{`Yêu cầu #${request.requestId}`}</strong>
                    <span style={{ color: getRequestStatusColor(status), fontWeight: 700 }}>
                      {getRequestStatusLabel(status)}
                    </span>
                  </div>
                  <p>{`Phạm vi: ${request.section || "WORKSHOP"}`}</p>
                  <p>{`Lý do: ${request.reason || "Không có lý do"}`}</p>
                  <div className="reception-plan-preview-meta">
                    <span>{`Gửi lúc: ${formatDateTimeValue(request.requestedAtUtc)}`}</span>
                    <span>{`Xử lý lúc: ${formatDateTimeValue(request.decidedAtUtc)}`}</span>
                  </div>
                  {request.decisionNote ? <p>{`Ghi chú xử lý: ${request.decisionNote}`}</p> : null}

                  {canApprove && isPending ? (
                    <div style={{ marginTop: 12 }}>
                      <textarea
                        rows={2}
                        className="input-field"
                        value={note}
                        onChange={(event) => handleDecisionNoteChange(request.requestId, event.target.value)}
                        placeholder="Ghi chú phê duyệt/từ chối..."
                      />
                      <div className="form-footer" style={{ marginTop: 10 }}>
                        <button
                          type="button"
                          className="btn-secondary-outline"
                          onClick={() => handleDecideRequest(request, false)}
                          disabled={deciding}
                        >
                          Từ chối
                        </button>
                        <button
                          type="button"
                          className="btn-primary-large"
                          onClick={() => handleDecideRequest(request, true)}
                          disabled={deciding}
                        >
                          {deciding ? "Đang xử lý..." : "Duyệt"}
                        </button>
                      </div>
                    </div>
                  ) : null}
                </article>
              );
            })}
          </div>
        )}
      </section>

      <form className="admin-form reception-appt-form" onSubmit={handleSubmit}>
        <fieldset
          disabled={saving || isEditLocked}
          style={{ border: 0, margin: 0, minWidth: 0, padding: 0 }}
        >
          <div className="reception-appt-layout">
          <div className="reception-appt-left-column">
            <section className="reception-appt-section">
              <div className="reception-appt-step-head">
                <span className="reception-step-badge">BƯỚC 1</span>
              </div>
              <h4 className="reception-appt-section-title">Thông tin khách hàng và xe</h4>

              <div className="reception-customer-selected-card">
                <div className="reception-customer-selected-main">
                  <small className="reception-customer-selected-tag">KHÁCH HÀNG</small>
                  <strong>{customerDisplay.fullName}</strong>
                  <span className="reception-customer-selected-contact">{customerDisplay.phone}</span>
                </div>
              </div>

              <div className="reception-customer-selected-card" style={{ marginTop: 10 }}>
                <div className="reception-customer-selected-main">
                  <small className="reception-customer-selected-tag">XE TIẾP NHẬN</small>
                  <strong>{vehicleDisplay.plate}</strong>
                  <span className="reception-customer-selected-contact">{vehicleDisplay.descriptor}</span>
                </div>
              </div>

              <div className="reception-plan-preview">
                <h5 className="reception-plan-preview-title">Thông tin lệnh hiện tại</h5>
                <div className="reception-plan-preview-list">
                  <article className="reception-plan-preview-item">
                    <div className="reception-plan-preview-head">
                      <strong>Mã lệnh</strong>
                      <span>{order?.code || "--"}</span>
                    </div>
                    <p>{`Trạng thái: ${String(order?.status || order?.Status || "--")}`}</p>
                    <div className="reception-plan-preview-meta">
                      <span>{`Check-in: ${formatDateTimeValue(formData.checkInTime)}`}</span>
                    </div>
                  </article>
                </div>
              </div>
            </section>

            <section className="reception-appt-section">
              <div className="reception-appt-step-head">
                <span className="reception-step-badge">BƯỚC 2</span>
              </div>
              <h4 className="reception-appt-section-title">Dịch vụ yêu cầu</h4>

              <div className="reception-service-layout">
                <div className="reception-service-type-col">
                  <h5 className="reception-service-col-title">Lọc theo loại dịch vụ</h5>
                  <div className="reception-category-grid">
                    {visibleCategoryOptions.map((category) => (
                      <button
                        key={category.id}
                        type="button"
                        className={`reception-category-chip ${String(activeCategoryId) === String(category.id) ? "selected" : ""}`}
                        onClick={() => setActiveCategoryId(String(category.id))}
                      >
                        {category.name}
                      </button>
                    ))}
                  </div>

                </div>

                <div className="reception-service-detail-col">
                  <div className="form-group reception-service-search">
                    <label>Tìm dịch vụ</label>
                    <input
                      type="text"
                      placeholder="Tìm theo tên dịch vụ..."
                      value={serviceSearch}
                      onChange={(event) => setServiceSearch(event.target.value)}
                    />
                  </div>

                  <div className="reception-service-list">
                    {filteredServices.length === 0 ? (
                      <div className="reception-muted-note">Không tìm thấy dịch vụ phù hợp theo bộ lọc hiện tại.</div>
                    ) : (
                      filteredServices.map((service) => {
                        const selected = toNumberList(formData.serviceIds).includes(service.id);
                        return (
                          <button
                            key={service.id}
                            type="button"
                            className={`reception-service-card ${selected ? "selected" : ""}`}
                            onClick={() => handleServiceToggle(service.id)}
                          >
                            <div className="reception-service-card-main">
                              <strong>{service.name}</strong>
                              <small>{service.minutes > 0 ? `${service.minutes} phút` : "Chưa có thời lượng chuẩn"}</small>
                            </div>
                            <span>{formatCurrency(service.price)}</span>
                          </button>
                        );
                      })
                    )}
                  </div>

                </div>
              </div>

              {showError("serviceIds") ? <div className="field-error">{fieldErrors.serviceIds}</div> : null}

              <div className="reception-plan-preview" style={{ marginTop: 14 }}>
                <h5 className="reception-plan-preview-title">Dịch vụ phát sinh (bổ sung sau SO gốc)</h5>
                <div className="reception-plan-preview-list">
                  {supplementaryServiceDetails.length === 0 ? (
                    <article className="reception-plan-preview-item">
                      <p>Chưa có dịch vụ phát sinh.</p>
                    </article>
                  ) : (
                    supplementaryServiceDetails.map((detail) => (
                      <article key={`supplementary-${detail.id}`} className="reception-plan-preview-item">
                        <div className="reception-plan-preview-head">
                          <strong>{detail.itemName || `Dịch vụ #${detail.standardServiceId || detail.itemId || detail.id}`}</strong>
                          <span>{formatCurrency(detail.unitPriceAtTime)}</span>
                        </div>
                        <p>{`Nguồn: ${getSupplementarySourceLabel(detail.serviceSource)}`}</p>
                        <p>{`Trạng thái: ${detail.status || SUPPLEMENTARY_DEFAULT_STATUS}`}</p>
                        {detail.note ? <p>{`Ghi chú: ${detail.note}`}</p> : null}
                      </article>
                    ))
                  )}
                </div>

                <div style={{ marginTop: 12 }}>
                  <p className="reception-muted-note" style={{ marginBottom: 8 }}>
                    {`Dịch vụ gốc hiện tại: ${initialServiceDetails.length} hạng mục.`}
                  </p>
                  <p className="reception-muted-note" style={{ marginBottom: 8 }}>
                    Dịch vụ gốc chỉnh sửa khi SO còn trong cửa sổ workshop. Dịch vụ phát sinh chỉ được thêm khi SO chưa bị khóa quote/parts/billing.
                  </p>
                  {isSupplementaryLocked ? (
                    <div className="field-error">Không thể thêm dịch vụ phát sinh: SO đã bị khóa theo mốc vận hành.</div>
                  ) : null}
                </div>

                <div className="timeline-grid timeline-grid-vertical" style={{ marginTop: 10 }}>
                  <div className="form-group">
                    <label>Nguồn phát sinh</label>
                    <select
                      value={supplementaryReason}
                      onChange={(event) => setSupplementaryReason(event.target.value)}
                      disabled={saving || isSupplementaryLocked || addingSupplementary}
                    >
                      <option value={SUPPLEMENTARY_REASON_TECH}>Phát sinh sau chẩn đoán</option>
                      <option value={SUPPLEMENTARY_REASON_CUSTOMER}>Khách yêu cầu thêm</option>
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Dịch vụ phát sinh</label>
                    <select
                      value={supplementaryServiceId}
                      onChange={(event) => setSupplementaryServiceId(event.target.value)}
                      disabled={saving || isSupplementaryLocked || addingSupplementary}
                    >
                      <option value="">-- Chọn dịch vụ --</option>
                      {normalizedServices.map((service) => (
                        <option key={`supp-service-${service.id}`} value={service.id}>
                          {`${service.name} - ${formatCurrency(service.price)}`}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Ghi chú phát sinh</label>
                    <textarea
                      rows={2}
                      className="input-field"
                      value={supplementaryNote}
                      onChange={(event) => setSupplementaryNote(event.target.value)}
                      placeholder="Nhập mô tả ngắn cho hạng mục phát sinh"
                      disabled={saving || isSupplementaryLocked || addingSupplementary}
                    />
                  </div>
                </div>

                <div className="form-footer" style={{ marginTop: 8 }}>
                  <button
                    type="button"
                    className="btn-secondary-outline"
                    onClick={handleAddSupplementaryService}
                    disabled={saving || isSupplementaryLocked || addingSupplementary}
                  >
                    {addingSupplementary ? "Đang thêm..." : "Thêm dịch vụ phát sinh"}
                  </button>
                </div>
              </div>

              <WorkshopInsurancePanel
                enabled={isInsuranceOrderEnabled}
                locked={hasInsuranceSignalServiceSelected}
                companies={insuranceCompanies}
                selectedCompanyId={formData.insuranceCompanyId}
                onToggle={handleInsuranceToggle}
                onCompanyChange={(value) => handleFieldValueChange("insuranceCompanyId", value)}
                companyError={showError("insuranceCompanyId") ? fieldErrors.insuranceCompanyId : ""}
                insuranceDeductibleOption={formData.insuranceDeductibleOption}
                insuranceClaimMode={formData.insuranceClaimMode}
                insuranceQuoteState={formData.insuranceQuoteState}
                onDeductibleOptionChange={handleInsuranceDeductibleOptionChange}
                onClaimModeChange={handleInsuranceClaimModeChange}
                deductibleError={showError("insuranceDeductibleOption") ? fieldErrors.insuranceDeductibleOption : ""}
                claimModeError={showError("insuranceClaimMode") ? fieldErrors.insuranceClaimMode : ""}
                draftBreakdown={insuranceDraftBreakdown}
                insuranceCardImageUrls={formData.insuranceCardImageUrls}
                insuranceDamageImageUrls={formData.insuranceDamageImageUrls}
                uploadingState={insuranceUploadState}
                onUploadAsset={handleInsuranceAssetUpload}
                onRemoveAsset={handleInsuranceAssetRemove}
              />
            </section>
          </div>
          <div className="reception-appt-right-column">
            <section className="reception-appt-section">
              <div className="reception-appt-step-head">
                <span className="reception-step-badge">BƯỚC 3</span>
              </div>
              <h4 className="reception-appt-section-title">Thông tin nghiệp vụ</h4>

              <div className="form-group">
                <label>Cố vấn dịch vụ *</label>
                <select
                  name="serviceAdvisorId"
                  className={showError("serviceAdvisorId") ? "input-invalid" : ""}
                  value={formData.serviceAdvisorId}
                  onChange={handleChange}
                >
                  <option value="">-- Chọn cố vấn --</option>
                  {staff.map((item) => {
                    const staffId = Number(readField(item, ["id", "Id"], 0));
                    const staffName = String(readField(item, ["fullName", "FullName"], "") || "").trim();
                    return (
                      <option key={staffId} value={staffId}>
                        {staffName || `Nhân sự #${staffId}`}
                      </option>
                    );
                  })}
                </select>
                {showError("serviceAdvisorId") ? <div className="field-error">{fieldErrors.serviceAdvisorId}</div> : null}
              </div>

              <div className="form-group">
                <label>Xử lý phụ tùng cũ</label>
                <select name="oldPartsReturnOption" value={formData.oldPartsReturnOption} onChange={handleChange}>
                  <option value="DISPOSE_ALL">Garage tự thanh lý/hủy</option>
                  <option value="RETURN_ALL">Trả lại cho khách hàng</option>
                  <option value="CUSTOM">Cho khách kiểm tra rồi quyết định</option>
                </select>
              </div>

              <div className="form-group">
                <label>Số tiền cọc (VNĐ)</label>
                <input
                  type="number"
                  min="0"
                  step="10000"
                  name="requiredDepositAmount"
                  className={showError("requiredDepositAmount") ? "input-invalid" : ""}
                  value={formData.requiredDepositAmount}
                  onChange={handleChange}
                />
                {showError("requiredDepositAmount") ? <div className="field-error">{fieldErrors.requiredDepositAmount}</div> : null}
              </div>

              <div className="form-group">
                <label>Ghi chú</label>
                <div className="reception-note-editor">
                  <textarea
                    rows={3}
                    className="reception-note-input"
                    value={customerNote}
                    placeholder="Nhập ghi chú..."
                    onChange={(event) => setCustomerNote(event.target.value)}
                  />
                </div>
              </div>

              <div className="timeline-grid timeline-grid-vertical">
                <div className="form-group">
                  <label>Thời điểm xe vào xưởng *</label>
                  <p className={`slot-flow-note ${bookingFlow === BOOKING_FLOW.FLEXIBLE ? "checkin" : "repair"}`}>
                    {slotSelectionGuide}
                  </p>
                  <DateTimePicker
                    value={formData.checkInTime}
                    onChange={(value) => handleFieldValueChange("checkInTime", value)}
                    placeholder="Chọn ngày và giờ..."
                    minDate={pickerMinDate}
                    onOpenRequest={() => {
                      if (checkInSelectionGuardMessage) {
                        setFieldErrors((prev) => ({ ...prev, checkInTime: checkInSelectionGuardMessage }));
                        return false;
                      }
                      return true;
                    }}
                  />
                  {fieldErrors.checkInTime ? <div className="field-error">{fieldErrors.checkInTime}</div> : null}
                </div>

                <div className="form-group">
                   <label>Dự kiến hoàn thành</label>
                  <DateTimePicker
                    value={formData.estimatedHandover || suggestedFinish.finishIso}
                    onChange={(value) => handleFieldValueChange("estimatedHandover", value)}
                    placeholder="Chọn ngày giờ dự kiến..."
                    minDate={pickerMinDate}
                  />
                  {showError("estimatedHandover") ? <div className="field-error">{fieldErrors.estimatedHandover}</div> : null}
                </div>

                <div className="form-group">
                  <label>Ngày giờ cam kết trả xe</label>
                  <DateTimePicker
                    value={formData.promisedHandoverTime}
                    onChange={(value) => handleFieldValueChange("promisedHandoverTime", value)}
                    placeholder="Chọn ngày giờ cam kết..."
                    minDate={pickerMinDate}
                  />
                  {showError("promisedHandoverTime") ? <div className="field-error">{fieldErrors.promisedHandoverTime}</div> : null}
                </div>
              </div>

              <div className="reception-summary-inline" role="status" aria-live="polite">
                <div className="reception-summary-inline-row">
                  <span>Tổng số dịch vụ</span>
                  <strong>{selectedServices.length}</strong>
                </div>
                <div className="reception-plan-summary-block">
                  <p className="reception-plan-summary-block-label">DỊCH VỤ ĐÃ CHỌN</p>
                  <p className="reception-plan-summary-text reception-plan-summary-text-bulleted">
                    {serviceGroupsByCategory.length > 0
                      ? serviceGroupsByCategory
                        .map((group) => `- ${group.categoryName}\n  ${(group.serviceNames || []).map((name) => `+ ${name}`).join("\n  ")}`)
                        .join("\n")
                      : "Chưa có dịch vụ"}
                  </p>
                </div>
                <div className="reception-summary-inline-row">
                  <span>Tổng thời lượng</span>
                  <strong>{`${totalMinutes} phút`}</strong>
                </div>
                <div className="reception-summary-inline-row">
                  <span>{SERVICE_PRICE_LABEL}</span>
                  <strong>{formatCurrency(totalPrice)}</strong>
                </div>
                <p className="reception-plan-summary-warning reception-summary-inline-warning">{`* ${SERVICE_PRICE_DISCLAIMER}`}</p>
                <div className="reception-summary-inline-row">
                  <span>Thời gian dự kiến hoàn tất toàn bộ xe</span>
                  <strong>{formatDateTimeValue(formData.estimatedHandover || suggestedFinish.finishIso)}</strong>
                </div>
                {isInsuranceOrderEnabled ? (
                  <>
                    <div className="reception-summary-inline-row">
                      <span>Bảo hiểm dự kiến trả</span>
                      <strong>{formatCurrency(insuranceDraftBreakdown.insuranceRequestedAmount)}</strong>
                    </div>
                    <div className="reception-summary-inline-row">
                      <span>Khách dự kiến trả</span>
                      <strong>{formatCurrency(insuranceDraftBreakdown.customerPayAmount)}</strong>
                    </div>
                  </>
                ) : null}
                <div className="reception-summary-inline-row">
                  <span>Luồng tiếp nhận</span>
                  <strong>{bookingFlow === BOOKING_FLOW.FLEXIBLE ? "Check-in linh hoạt" : "Khung giờ sửa chữa"}</strong>
                </div>
                {completionWarningText ? (
                  <p className="reception-plan-summary-warning reception-summary-inline-warning">
                    {`* ${completionWarningText}`}
                  </p>
                ) : null}
              </div>
            </section>
          </div>
          </div>
        </fieldset>

        <div className="form-footer">
          <button type="button" className="btn-secondary-outline" onClick={() => navigate(-1)}>
            Hủy và quay lại
          </button>
          <button type="submit" className="btn-primary-large" disabled={saving || isEditLocked}>
            {saving ? "Đang cập nhật..." : "Lưu thay đổi"}
          </button>
        </div>
      </form>
    </div>
  );
}
