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

const EDIT_WINDOW_PASSED_MESSAGE = "KhÃ´ng thá»ƒ sá»­a service order vÃ¬ Ä‘Ã£ qua má»‘c váº­n hÃ nh cho phÃ©p chá»‰nh sá»­a.";
const REQUEST_EDIT_REASON_REQUIRED = "Vui lÃ²ng nháº­p lÃ½ do yÃªu cáº§u sá»­a trÆ°á»›c khi gá»­i.";
const REQUEST_EDIT_ERROR = "KhÃ´ng thá»ƒ gá»­i yÃªu cáº§u sá»­a service order.";
const REQUEST_EDIT_SUCCESS = "ÄÃ£ gá»­i yÃªu cáº§u sá»­a service order Ä‘áº¿n cáº¥p phÃª duyá»‡t.";
const REQUEST_DECISION_ERROR = "KhÃ´ng thá»ƒ xá»­ lÃ½ yÃªu cáº§u sá»­a service order.";

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
    .replace(/Ä‘/g, "d");
}

function normalizeRoleToken(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/Ä‘/gi, "d")
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
  if (normalized === "APPROVED") return "ÄÃ£ duyá»‡t";
  if (normalized === "REJECTED") return "ÄÃ£ tá»« chá»‘i";
  return "Chá» duyá»‡t";
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
      map.set(service.categoryId, { id: service.categoryId, name: `NhÃ³m ${service.categoryId}` });
    }
  });

  return Array.from(map.values()).sort((a, b) => String(a.name).localeCompare(String(b.name), "vi"));
}

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}Ä‘`;
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
  const fallback = "KhÃ´ng thá»ƒ cáº­p nháº­t lá»‡nh sá»­a chá»¯a. Vui lÃ²ng kiá»ƒm tra dá»¯ liá»‡u vÃ  thá»­ láº¡i.";
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
  const editLockedMessage = editCapability?.blockingReason || EDIT_WINDOW_PASSED_MESSAGE;
  const overrideUntilText = useMemo(() => formatDateTimeValue(editCapability?.overrideUntilUtc), [editCapability?.overrideUntilUtc]);
  const hasPendingRequest = useMemo(
    () => editRequests.some((item) => String(item?.status || "").toUpperCase() === "PENDING"),
    [editRequests]
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
      map.set(id, String(category?.name || "").trim() || `NhÃ³m ${id}`);
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
          const categoryName = categoryNameById.get(categoryId) || `NhÃ³m ${categoryId || "KhÃ¡c"}`;
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
      fullName: fullName || (customerId > 0 ? `KhÃ¡ch hÃ ng #${customerId}` : "ChÆ°a xÃ¡c Ä‘á»‹nh"),
      phone: phone || "ChÆ°a cÃ³ sá»‘ Ä‘iá»‡n thoáº¡i",
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
      plate: plate ? formatLicensePlate(plate) : "Chua c? bi?n s?",
      descriptor: descriptor || "Chua r? d?ng xe",
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
      const [orderRes, capabilityRes, requestRes, staffRes, roleRes, categoryRes, serviceRes, packageRes, detailsRes, insuranceRes, customerRes, vehicleRes, modelRes, operatingHoursRes] = await Promise.all([
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
        throw new Error("KhÃ´ng tÃ¬m tháº¥y lá»‡nh sá»­a chá»¯a.");
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

      const editableDetails = (Array.isArray(detailsRes) ? detailsRes : []).filter((detail) => {
        const itemType = String(readField(detail, ["itemType", "ItemType"], "") || "").toUpperCase();
        const isLaborOrService = itemType === "LABOR" || itemType === "SERVICE";
        const hasDiagnosisRef = Boolean(readField(detail, ["diagnoseRecommendationId", "DiagnoseRecommendationId"], null));
        return isLaborOrService && !hasDiagnosisRef;
      });

      const initialServiceIds = Array.from(
        new Set(
          editableDetails
            .map((detail) => Number(readField(detail, ["standardServiceId", "StandardServiceId"], 0)))
            .filter((serviceId) => Number.isFinite(serviceId) && serviceId > 0)
        )
      );

      const parsedNote = parseCustomerNote(readField(orderRes, ["customerNotes", "CustomerNotes"], ""));
      const checkInTime = toLocalDateTimeValue(readField(orderRes, ["checkInTime", "CheckInTime"], null));
      const estimatedHandover = toLocalDateTimeValue(readField(orderRes, ["estimatedHandover", "EstimatedHandover"], null));
      const promisedHandoverTime = toLocalDateTimeValue(readField(orderRes, ["promisedHandoverTime", "PromisedHandoverTime"], null));
      const normalizedCategory = String(readField(orderRes, ["orderCategory", "OrderCategory"], "GENERAL_REPAIR") || "GENERAL_REPAIR").toUpperCase();
      const normalizedStatus = String(readField(orderRes, ["status", "Status"], "NEW") || "NEW").toUpperCase();
      const insuranceCompanyId = parsePositiveId(
        readField(orderRes, ["insuranceCompanyId", "InsuranceCompanyId"], 0)
          || readField(
            editableDetails.find((detail) => parsePositiveId(readField(detail, ["insuranceCompanyId", "InsuranceCompanyId"], 0)) > 0),
            ["insuranceCompanyId", "InsuranceCompanyId"],
            0
          )
      );
      const insuranceCardImageUrls = Array.isArray(readField(orderRes, ["insuranceCardImageUrls", "InsuranceCardImageUrls"], []))
        ? readField(orderRes, ["insuranceCardImageUrls", "InsuranceCardImageUrls"], [])
        : [];
      const insuranceDamageImageUrls = Array.isArray(readField(orderRes, ["insuranceDamageImageUrls", "InsuranceDamageImageUrls"], []))
        ? readField(orderRes, ["insuranceDamageImageUrls", "InsuranceDamageImageUrls"], [])
        : [];
      const insuranceDeductibleOption = String(
        readField(orderRes, ["insuranceDeductibleOption", "InsuranceDeductibleOption"], "") || ""
      ).trim();
      const insuranceClaimMode = String(
        readField(orderRes, ["insuranceClaimMode", "InsuranceClaimMode"], "") || ""
      ).trim();
      const insuranceQuoteState = String(
        readField(orderRes, ["insuranceQuoteState", "InsuranceQuoteState"], "") || ""
      ).trim();
      const insuranceDeductibleAmountRaw = Number(
        readField(orderRes, ["insuranceDeductibleAmount", "InsuranceDeductibleAmount"], null)
      );
      const initialPackageIds = String(readField(orderRes, ["requestedPackageIds", "RequestedPackageIds"], "") || "")
        .split(",")
        .map((item) => Number(item))
        .filter((packageId) => Number.isFinite(packageId) && packageId > 0)
        .map(toPackageSelectableId);
      const insuranceEnabled = normalizedCategory === INSURANCE_ORDER_CATEGORY || insuranceCompanyId > 0;

      setOrder(orderRes);
      setEditCapability(normalizeServiceOrderEditCapability(capabilityRes));
      setStaff(filteredStaff);
      setServiceCategories(Array.isArray(categoryRes) ? categoryRes : []);
      setServices(mergeBookableServicesAndPackages(Array.isArray(serviceRes) ? serviceRes : [], Array.isArray(packageRes) ? packageRes : []));
      setInsuranceCompanies(Array.isArray(insuranceRes) ? insuranceRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setModels(Array.isArray(modelRes) ? modelRes : []);
      setBranchOperatingHours(Array.isArray(operatingHoursRes) ? operatingHoursRes : []);
      setEditRequests(
        (Array.isArray(requestRes) ? requestRes : [])
          .map(normalizeServiceOrderEditRequestItem)
          .filter((item) => item.requestId > 0)
      );

      setFormData({
        orderCategory: normalizedCategory,
        insuranceEnabled,
        insuranceCompanyId: insuranceCompanyId || "",
        insuranceDeductibleAmount: Number.isFinite(insuranceDeductibleAmountRaw) ? insuranceDeductibleAmountRaw : null,
        insuranceDeductibleOption,
        insuranceClaimMode,
        insuranceQuoteState,
        insuranceCardImageUrls,
        insuranceDamageImageUrls,
        serviceAdvisorId: parsePositiveId(readField(orderRes, ["serviceAdvisorId", "ServiceAdvisorId"], 0)) || "",
        oldPartsReturnOption: String(readField(orderRes, ["oldPartsReturnOption", "OldPartsReturnOption"], "DISPOSE_ALL") || "DISPOSE_ALL"),
        requiredDepositAmount: Number(readField(orderRes, ["requiredDepositAmount", "RequiredDepositAmount"], 0) || 0),
        checkInTime: checkInTime || toLocalDateTimeValue(new Date()),
        estimatedHandover,
        promisedHandoverTime,
        status: normalizedStatus,
        serviceIds: [...initialServiceIds, ...initialPackageIds],
      });
      setInsuranceUploadState({});

      setCustomerNote(parsedNote);

      const firstService = [...initialServiceIds, ...initialPackageIds].length > 0 ? Number([...initialServiceIds, ...initialPackageIds][0]) : 0;
      const firstNormalized = mergeBookableServicesAndPackages(Array.isArray(serviceRes) ? serviceRes : [], Array.isArray(packageRes) ? packageRes : [])
        .map(normalizeService)
        .find((service) => service.id === firstService);
      setActiveCategoryId(firstNormalized?.categoryId ? String(firstNormalized.categoryId) : "");
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
      setLoadingEditRequests(false);
    }
  }, [effectiveBranchId, orderId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (!activeCategoryId && visibleCategoryOptions.length > 0) {
      setActiveCategoryId(String(visibleCategoryOptions[0].id));
      return;
    }
    if (
      activeCategoryId &&
      !visibleCategoryOptions.some((category) => String(category.id) === String(activeCategoryId))
    ) {
      setActiveCategoryId(visibleCategoryOptions[0] ? String(visibleCategoryOptions[0].id) : "");
    }
  }, [activeCategoryId, visibleCategoryOptions]);

  useEffect(() => {
    const compatibleServiceIds = new Set(
      filterBookableItemsForVehicleModels(
        normalizedServices,
        currentVehicleModelIds,
        modelInfoById,
        serviceById
      ).map((service) => Number(service.id))
    );
    const nextServiceIds = toNumberList(formData.serviceIds).filter((serviceId) => compatibleServiceIds.has(Number(serviceId)));
    const nextPackageIds = toNumberList(formData.requestedPackageIds).filter((packageId) => compatibleServiceIds.has(Number(packageId)));
    if (
      nextServiceIds.length === toNumberList(formData.serviceIds).length &&
      nextPackageIds.length === toNumberList(formData.requestedPackageIds).length
    ) {
      return;
    }
    setFormData((prev) => ({
      ...prev,
      serviceIds: nextServiceIds,
      requestedPackageIds: nextPackageIds,
    }));
  }, [currentVehicleModelIds, formData.requestedPackageIds, formData.serviceIds, modelInfoById, normalizedServices, serviceById]);

  const showError = useCallback(
    (fieldName) => submitAttempted && Boolean(fieldErrors[fieldName]),
    [fieldErrors, submitAttempted]
  );

  const validateForm = useCallback(
    (data) => {
      const errors = {};
      const checkIn = data.checkInTime ? new Date(data.checkInTime) : null;
      const estimated = data.estimatedHandover || suggestedFinish.finishIso ? new Date(data.estimatedHandover || suggestedFinish.finishIso) : null;
      const promised = data.promisedHandoverTime ? new Date(data.promisedHandoverTime) : null;

      if (!data.serviceAdvisorId) errors.serviceAdvisorId = "Vui lÃ²ng chá»n cá»‘ váº¥n dá»‹ch vá»¥.";
      if (!checkIn || Number.isNaN(checkIn.getTime())) {
        errors.checkInTime = "Vui lÃ²ng nháº­p thá»i Ä‘iá»ƒm xe vÃ o xÆ°á»Ÿng há»£p lá»‡.";
      } else if (isBranchClosedBySchedule(branchOperatingHours, effectiveBranchId, checkIn)) {
        errors.checkInTime = getBranchClosedMessage(checkIn);
      }

      if (estimated && checkIn && !Number.isNaN(estimated.getTime()) && estimated < checkIn) {
        errors.estimatedHandover = "Dá»± kiáº¿n hoÃ n thÃ nh pháº£i lá»›n hÆ¡n hoáº·c báº±ng thá»i Ä‘iá»ƒm xe vÃ o xÆ°á»Ÿng.";
      }

      if (promised && checkIn && !Number.isNaN(promised.getTime()) && promised < checkIn) {
        errors.promisedHandoverTime = "NgÃ y giá» cam káº¿t tráº£ xe pháº£i lá»›n hÆ¡n hoáº·c báº±ng thá»i Ä‘iá»ƒm xe vÃ o xÆ°á»Ÿng.";
      }

      if (Number(data.requiredDepositAmount || 0) < 0) {
        errors.requiredDepositAmount = "Sá»‘ tiá»n cá»c khÃ´ng Ä‘Æ°á»£c nhá» hÆ¡n 0.";
      }

      if (isInsuranceOrderEnabled && !parsePositiveId(data.insuranceCompanyId)) {
        errors.insuranceCompanyId = "Vui lÃ²ng chá»n cÃ´ng ty báº£o hiá»ƒm trÆ°á»›c khi lÆ°u thay Ä‘á»•i.";
      }

      if (isInsuranceOrderEnabled && !String(data.insuranceDeductibleOption || "").trim()) {
        errors.insuranceDeductibleOption = "Vui lÃ²ng chá»n má»©c miá»…n thÆ°á»ng hoáº·c tÆ° váº¥n.";
      }

      if (isInsuranceOrderEnabled && !String(data.insuranceClaimMode || "").trim()) {
        errors.insuranceClaimMode = "Vui lÃ²ng chá»n hÃ¬nh thá»©c xá»­ lÃ½ báº£o hiá»ƒm.";
      }

      if (toNumberList(data.serviceIds).length === 0) {
        errors.serviceIds = "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t dá»‹ch vá»¥.";
      }

      return errors;
    },
    [branchOperatingHours, effectiveBranchId, isInsuranceOrderEnabled, suggestedFinish.finishIso]
  );

  const checkInSelectionGuardMessage = useMemo(() => {
    if (toNumberList(formData.serviceIds).length === 0) {
      return "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t dá»‹ch vá»¥ cá»¥ thá»ƒ trÆ°á»›c khi chá»n giá».";
    }
    const unresolvedPackageIds = findUnresolvedPackageIds(toNumberList(formData.serviceIds), serviceById);
    if (unresolvedPackageIds.length > 0) {
      return `Vui lÃ²ng chá»n láº¡i gÃ³i dá»‹ch vá»¥ (mÃ£ khÃ´ng há»£p lá»‡: #${unresolvedPackageIds.join(", #")}) trÆ°á»›c khi chá»n giá».`;
    }
    return "";
  }, [formData.serviceIds, serviceById]);

  const handleFieldValueChange = useCallback((fieldName, value) => {
    if (fieldName === "checkInTime" && checkInSelectionGuardMessage) {
      setFieldErrors((prev) => ({ ...prev, checkInTime: checkInSelectionGuardMessage }));
      return;
    }
    if (fieldName === "checkInTime" && value && isBranchClosedBySchedule(branchOperatingHours, effectiveBranchId, value)) {
      setFieldErrors((prev) => ({ ...prev, checkInTime: getBranchClosedMessage(value) }));
      return;
    }
    setFormData((prev) => ({ ...prev, [fieldName]: value }));
    if (fieldName === "checkInTime") {
      setFieldErrors((prev) => {
        if (!prev?.checkInTime) return prev;
        const next = { ...prev };
        delete next.checkInTime;
        return next;
      });
    }
  }, [branchOperatingHours, checkInSelectionGuardMessage, effectiveBranchId]);

  const handleInsuranceToggle = useCallback((enabled) => {
    setFormData((prev) => ({
      ...prev,
      insuranceEnabled: Boolean(enabled),
      insuranceCompanyId: enabled ? prev.insuranceCompanyId : "",
      insuranceDeductibleAmount: enabled ? prev.insuranceDeductibleAmount : null,
      insuranceDeductibleOption: enabled ? prev.insuranceDeductibleOption : "",
      insuranceClaimMode: enabled ? prev.insuranceClaimMode : "",
      insuranceQuoteState: enabled ? prev.insuranceQuoteState : "",
      insuranceCardImageUrls: enabled ? prev.insuranceCardImageUrls : [],
      insuranceDamageImageUrls: enabled ? prev.insuranceDamageImageUrls : [],
    }));
    if (!enabled) {
      setInsuranceUploadState({});
    }
  }, []);

  const handleInsuranceDeductibleOptionChange = useCallback((value) => {
    setFormData((prev) => ({
      ...prev,
      insuranceDeductibleOption: value,
      insuranceDeductibleAmount: resolveInsuranceDeductibleAmount(value),
      insuranceQuoteState: resolveInsuranceQuoteState(value, prev.insuranceClaimMode),
    }));
  }, []);

  const handleInsuranceClaimModeChange = useCallback((value) => {
    setFormData((prev) => ({
      ...prev,
      insuranceClaimMode: value,
      insuranceQuoteState: resolveInsuranceQuoteState(prev.insuranceDeductibleOption, value),
    }));
  }, []);

  const handleInsuranceAssetUpload = useCallback(async (assetType, files) => {
    if (!Array.isArray(files) || files.length === 0) return;

    setInsuranceUploadState((prev) => ({
      ...prev,
      [assetType]: {
        uploading: true,
        successMessage: "",
      },
    }));

    try {
      const uploadedUrls = [];
      for (const file of files) {
        const response = await serviceOrderApi.uploadInsuranceAsset(file, assetType);
        const url = String(response?.url || response?.Url || "").trim();
        if (url) {
          uploadedUrls.push(url);
        }
      }

      if (uploadedUrls.length > 0) {
        const targetField =
          assetType === INSURANCE_ASSET_TYPE.CARD ? "insuranceCardImageUrls" : "insuranceDamageImageUrls";
        setFormData((prev) => ({
          ...prev,
          [targetField]: [...(Array.isArray(prev?.[targetField]) ? prev[targetField] : []), ...uploadedUrls],
        }));
      }

      setInsuranceUploadState((prev) => ({
        ...prev,
        [assetType]: {
          uploading: false,
          successMessage: uploadedUrls.length > 0 ? "?? t?i ?nh b?o hi?m." : "",
        },
      }));
      setError("");
    } catch (uploadError) {
      setInsuranceUploadState((prev) => ({
        ...prev,
        [assetType]: {
          uploading: false,
          successMessage: "",
        },
      }));
      setError(uploadError?.response?.data?.message || uploadError?.message || "Kh?ng th? t?i ?nh b?o hi?m l?n m?y ch?.");
    }
  }, []);

  const handleInsuranceAssetRemove = useCallback((assetType, index) => {
    if (index < 0) return;
    const targetField =
      assetType === INSURANCE_ASSET_TYPE.CARD ? "insuranceCardImageUrls" : "insuranceDamageImageUrls";
    setFormData((prev) => ({
      ...prev,
      [targetField]: Array.isArray(prev?.[targetField])
        ? prev[targetField].filter((_, currentIndex) => currentIndex !== index)
        : [],
    }));
    setInsuranceUploadState((prev) => ({
      ...prev,
      [assetType]: {
        uploading: false,
        successMessage: "",
      },
    }));
  }, []);

  const handleChange = useCallback((event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  }, []);

  const handleServiceToggle = useCallback((serviceId) => {
    const normalizedId = Number(serviceId);
    if (!Number.isFinite(normalizedId) || normalizedId === 0) return;

    setFormData((prev) => {
      const current = toNumberList(prev.serviceIds);
      const has = current.includes(normalizedId);
      return {
        ...prev,
        serviceIds: has ? current.filter((id) => id !== normalizedId) : [...current, normalizedId],
      };
    });
  }, []);

  const handleSubmit = useCallback(
    async (event) => {
      event.preventDefault();
      if (saving) return;

      const latestCapabilityRaw = await serviceOrderApi.getEditCapabilities(orderId).catch(() => null);
      const latestCapability = normalizeServiceOrderEditCapability(latestCapabilityRaw);
      if (latestCapability && !latestCapability.canEditWorkshop) {
        setEditCapability(latestCapability);
        setError(latestCapability.blockingReason || EDIT_WINDOW_PASSED_MESSAGE);
        return;
      }

      if (!latestCapability && hasServiceOrderEditWindowPassed(order)) {
        setError(EDIT_WINDOW_PASSED_MESSAGE);
        return;
      }

      setSubmitAttempted(true);
      setError("");

      const errors = validateForm(formData);
      if (isInsuranceOrderEnabled && !parsePositiveId(formData.insuranceCompanyId)) {
        errors.insuranceCompanyId = "Vui l?ng ch?n c?ng ty b?o hi?m tru?c khi luu thay d?i.";
      }
      setFieldErrors(errors);
      if (Object.keys(errors).length > 0) {
        setError("Vui lÃ²ng xá»­ lÃ½ cÃ¡c lá»—i trong biá»ƒu máº«u trÆ°á»›c khi lÆ°u.");
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
    [customerNote, formData, isInsuranceOrderEnabled, navigate, order, orderId, saving, suggestedFinish.finishIso, validateForm]
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
      setError("Vui lÃ²ng nháº­p lÃ½ do tá»« chá»‘i.");
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
      setRequestEditNotice(String(response?.message || (approve ? "ÄÃ£ duyá»‡t yÃªu cáº§u sá»­a." : "ÄÃ£ tá»« chá»‘i yÃªu cáº§u sá»­a.")));
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
          <p className="reception-muted-note">Äang táº£i dá»¯ liá»‡u lá»‡nh sá»­a chá»¯a...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="create-order-page reception-create-appointment-page">
      <div className="page-header-actions reception-create-header">
        <button type="button" className="btn-back" onClick={() => navigate(-1)}>
          Quay láº¡i Dashboard
        </button>
        <div className="reception-create-header-copy">
          <h2 className="page-title">{`Chá»‰nh sá»­a lá»‡nh sá»­a chá»¯a ${order?.code ? `#${order.code}` : ""}`}</h2>
          <p className="reception-create-header-note">
            Cáº­p nháº­t dá»‹ch vá»¥ vÃ  thÃ´ng tin tiáº¿p nháº­n theo luá»“ng má»›i. Há»‡ thá»‘ng chá»‰ lÆ°u khi toÃ n bá»™ dá»¯ liá»‡u há»£p lá»‡.
          </p>
        </div>
      </div>

      <div className="reception-appt-topbar">
        <div className="reception-appt-topbar-copy">
          <p className="reception-appt-topbar-kicker">SERVICE ORDER</p>
          <h3 className="reception-appt-topbar-title">Cáº­p nháº­t thÃ´ng tin tiáº¿p nháº­n dá»‹ch vá»¥</h3>
          <p className="reception-appt-topbar-note">
            Luá»“ng lÆ°u theo cÆ¡ cháº¿ all-or-nothing: náº¿u cÃ³ lá»—i á»Ÿ báº¥t ká»³ bÆ°á»›c nÃ o, há»‡ thá»‘ng sáº½ khÃ´ng ghi dá»¯ liá»‡u xuá»‘ng cÆ¡ sá»Ÿ dá»¯ liá»‡u.
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
          {`Äang má»Ÿ khÃ³a táº¡m thá»i Ä‘áº¿n: ${overrideUntilText}`}
        </div>
      ) : null}

      {submitAttempted && Object.keys(fieldErrors).length > 0 ? (
        <div className="validation-summary" role="alert">
          <strong>Vui lÃ²ng xá»­ lÃ½ cÃ¡c lá»—i sau:</strong>
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
            <h4 className="reception-appt-section-title">YÃªu cáº§u má»Ÿ khÃ³a chá»‰nh sá»­a</h4>
          </div>
          <div className="form-group">
            <label htmlFor="service-order-edit-request-reason">LÃ½ do yÃªu cáº§u sá»­a</label>
            <textarea
              id="service-order-edit-request-reason"
              rows={3}
              className="input-field"
              value={requestEditReason}
              onChange={(event) => setRequestEditReason(event.target.value)}
              placeholder="Nháº­p lÃ½ do Ä‘á»ƒ quáº£n lÃ½ phÃª duyá»‡t má»Ÿ khÃ³a"
            />
          </div>
          <div className="form-footer" style={{ marginTop: 12 }}>
            <button
              type="button"
              className="btn-primary-large"
              onClick={handleRequestEdit}
              disabled={requestingEdit || hasPendingRequest}
            >
              {requestingEdit ? "Äang gá»­i yÃªu cáº§u..." : hasPendingRequest ? "Äang cÃ³ yÃªu cáº§u chá» duyá»‡t" : "Gá»­i yÃªu cáº§u sá»­a"}
            </button>
          </div>
        </section>
      ) : null}

      <section className="reception-appt-section" aria-live="polite">
        <div className="reception-appt-section-head">
          <h4 className="reception-appt-section-title">Lá»‹ch sá»­ yÃªu cáº§u má»Ÿ khÃ³a</h4>
        </div>
        {loadingEditRequests ? (
          <p className="reception-muted-note">Äang táº£i yÃªu cáº§u sá»­a...</p>
        ) : editRequests.length === 0 ? (
          <p className="reception-muted-note">ChÆ°a cÃ³ yÃªu cáº§u nÃ o.</p>
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
                    <strong>{`YÃªu cáº§u #${request.requestId}`}</strong>
                    <span style={{ color: getRequestStatusColor(status), fontWeight: 700 }}>
                      {getRequestStatusLabel(status)}
                    </span>
                  </div>
                  <p>{`Pháº¡m vi: ${request.section || "WORKSHOP"}`}</p>
                  <p>{`LÃ½ do: ${request.reason || "KhÃ´ng cÃ³ lÃ½ do"}`}</p>
                  <div className="reception-plan-preview-meta">
                    <span>{`Gá»­i lÃºc: ${formatDateTimeValue(request.requestedAtUtc)}`}</span>
                    <span>{`Xá»­ lÃ½ lÃºc: ${formatDateTimeValue(request.decidedAtUtc)}`}</span>
                  </div>
                  {request.decisionNote ? <p>{`Ghi chÃº xá»­ lÃ½: ${request.decisionNote}`}</p> : null}

                  {canApprove && isPending ? (
                    <div style={{ marginTop: 12 }}>
                      <textarea
                        rows={2}
                        className="input-field"
                        value={note}
                        onChange={(event) => handleDecisionNoteChange(request.requestId, event.target.value)}
                        placeholder="Ghi chÃº phÃª duyá»‡t/tá»« chá»‘i..."
                      />
                      <div className="form-footer" style={{ marginTop: 10 }}>
                        <button
                          type="button"
                          className="btn-secondary-outline"
                          onClick={() => handleDecideRequest(request, false)}
                          disabled={deciding}
                        >
                          Tá»« chá»‘i
                        </button>
                        <button
                          type="button"
                          className="btn-primary-large"
                          onClick={() => handleDecideRequest(request, true)}
                          disabled={deciding}
                        >
                          {deciding ? "Äang xá»­ lÃ½..." : "Duyá»‡t"}
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
                <span className="reception-step-badge">BÆ¯á»šC 1</span>
              </div>
              <h4 className="reception-appt-section-title">ThÃ´ng tin khÃ¡ch hÃ ng vÃ  xe</h4>

              <div className="reception-customer-selected-card">
                <div className="reception-customer-selected-main">
                  <small className="reception-customer-selected-tag">KHÃCH HÃ€NG</small>
                  <strong>{customerDisplay.fullName}</strong>
                  <span className="reception-customer-selected-contact">{customerDisplay.phone}</span>
                </div>
              </div>

              <div className="reception-customer-selected-card" style={{ marginTop: 10 }}>
                <div className="reception-customer-selected-main">
                  <small className="reception-customer-selected-tag">XE TIáº¾P NHáº¬N</small>
                  <strong>{vehicleDisplay.plate}</strong>
                  <span className="reception-customer-selected-contact">{vehicleDisplay.descriptor}</span>
                </div>
              </div>

              <div className="reception-plan-preview">
                <h5 className="reception-plan-preview-title">ThÃ´ng tin lá»‡nh hiá»‡n táº¡i</h5>
                <div className="reception-plan-preview-list">
                  <article className="reception-plan-preview-item">
                    <div className="reception-plan-preview-head">
                      <strong>MÃ£ lá»‡nh</strong>
                      <span>{order?.code || "--"}</span>
                    </div>
                    <p>{`Tráº¡ng thÃ¡i: ${String(order?.status || order?.Status || "--")}`}</p>
                    <div className="reception-plan-preview-meta">
                      <span>{`Check-in: ${formatDateTimeValue(formData.checkInTime)}`}</span>
                    </div>
                  </article>
                </div>
              </div>
            </section>

            <section className="reception-appt-section">
              <div className="reception-appt-step-head">
                <span className="reception-step-badge">BÆ¯á»šC 2</span>
              </div>
              <h4 className="reception-appt-section-title">Dá»‹ch vá»¥ yÃªu cáº§u</h4>

              <div className="reception-service-layout">
                <div className="reception-service-type-col">
                  <h5 className="reception-service-col-title">Lá»c theo loáº¡i dá»‹ch vá»¥</h5>
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
                    <label>TÃ¬m dá»‹ch vá»¥</label>
                    <input
                      type="text"
                      placeholder="TÃ¬m theo tÃªn dá»‹ch vá»¥..."
                      value={serviceSearch}
                      onChange={(event) => setServiceSearch(event.target.value)}
                    />
                  </div>

                  <div className="reception-service-list">
                    {filteredServices.length === 0 ? (
                      <div className="reception-muted-note">KhÃ´ng tÃ¬m tháº¥y dá»‹ch vá»¥ phÃ¹ há»£p theo bá»™ lá»c hiá»‡n táº¡i.</div>
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
                              <small>{service.minutes > 0 ? `${service.minutes} phÃºt` : "ChÆ°a cÃ³ thá»i lÆ°á»£ng chuáº©n"}</small>
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
                <span className="reception-step-badge">BÆ¯á»šC 3</span>
              </div>
              <h4 className="reception-appt-section-title">ThÃ´ng tin nghiá»‡p vá»¥</h4>

              <div className="form-group">
                <label>Cá»‘ váº¥n dá»‹ch vá»¥ *</label>
                <select
                  name="serviceAdvisorId"
                  className={showError("serviceAdvisorId") ? "input-invalid" : ""}
                  value={formData.serviceAdvisorId}
                  onChange={handleChange}
                >
                  <option value="">-- Chá»n cá»‘ váº¥n --</option>
                  {staff.map((item) => {
                    const staffId = Number(readField(item, ["id", "Id"], 0));
                    const staffName = String(readField(item, ["fullName", "FullName"], "") || "").trim();
                    return (
                      <option key={staffId} value={staffId}>
                        {staffName || `NhÃ¢n sá»± #${staffId}`}
                      </option>
                    );
                  })}
                </select>
                {showError("serviceAdvisorId") ? <div className="field-error">{fieldErrors.serviceAdvisorId}</div> : null}
              </div>

              <div className="form-group">
                <label>Xá»­ lÃ½ phá»¥ tÃ¹ng cÅ©</label>
                <select name="oldPartsReturnOption" value={formData.oldPartsReturnOption} onChange={handleChange}>
                  <option value="DISPOSE_ALL">Garage tá»± thanh lÃ½/há»§y</option>
                  <option value="RETURN_ALL">Tráº£ láº¡i cho khÃ¡ch hÃ ng</option>
                  <option value="CUSTOM">Cho khÃ¡ch kiá»ƒm tra rá»“i quyáº¿t Ä‘á»‹nh</option>
                </select>
              </div>

              <div className="form-group">
                <label>Sá»‘ tiá»n cá»c (VNÄ)</label>
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
                  <label>Thá»i Ä‘iá»ƒm xe vÃ o xÆ°á»Ÿng *</label>
                  <p className={`slot-flow-note ${bookingFlow === BOOKING_FLOW.FLEXIBLE ? "checkin" : "repair"}`}>
                    {slotSelectionGuide}
                  </p>
                  <DateTimePicker
                    value={formData.checkInTime}
                    onChange={(value) => handleFieldValueChange("checkInTime", value)}
                    placeholder="Chá»n ngÃ y vÃ  giá»..."
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
                  <label>Dá»± kiáº¿n hoÃ n thÃ nh</label>
                  <DateTimePicker
                    value={formData.estimatedHandover || suggestedFinish.finishIso}
                    onChange={(value) => handleFieldValueChange("estimatedHandover", value)}
                    placeholder="Chá»n ngÃ y giá» dá»± kiáº¿n..."
                    minDate={pickerMinDate}
                  />
                  {showError("estimatedHandover") ? <div className="field-error">{fieldErrors.estimatedHandover}</div> : null}
                </div>

                <div className="form-group">
                  <label>NgÃ y giá» cam káº¿t tráº£ xe</label>
                  <DateTimePicker
                    value={formData.promisedHandoverTime}
                    onChange={(value) => handleFieldValueChange("promisedHandoverTime", value)}
                    placeholder="Chá»n ngÃ y giá» cam káº¿t..."
                    minDate={pickerMinDate}
                  />
                  {showError("promisedHandoverTime") ? <div className="field-error">{fieldErrors.promisedHandoverTime}</div> : null}
                </div>
              </div>

              <div className="reception-summary-inline" role="status" aria-live="polite">
                <div className="reception-summary-inline-row">
                  <span>Tá»•ng sá»‘ dá»‹ch vá»¥</span>
                  <strong>{selectedServices.length}</strong>
                </div>
                <div className="reception-plan-summary-block">
                  <p className="reception-plan-summary-block-label">Dá»ŠCH Vá»¤ ÄÃƒ CHá»ŒN</p>
                  <p className="reception-plan-summary-text reception-plan-summary-text-bulleted">
                    {serviceGroupsByCategory.length > 0
                      ? serviceGroupsByCategory
                        .map((group) => `- ${group.categoryName}\n  ${(group.serviceNames || []).map((name) => `+ ${name}`).join("\n  ")}`)
                        .join("\n")
                      : "ChÆ°a cÃ³ dá»‹ch vá»¥"}
                  </p>
                </div>
                <div className="reception-summary-inline-row">
                  <span>Tá»•ng thá»i lÆ°á»£ng</span>
                  <strong>{`${totalMinutes} phÃºt`}</strong>
                </div>
                <div className="reception-summary-inline-row">
                  <span>{SERVICE_PRICE_LABEL}</span>
                  <strong>{formatCurrency(totalPrice)}</strong>
                </div>
                <p className="reception-plan-summary-warning reception-summary-inline-warning">{`* ${SERVICE_PRICE_DISCLAIMER}`}</p>
                <div className="reception-summary-inline-row">
                  <span>Thá»i gian dá»± kiáº¿n hoÃ n táº¥t toÃ n bá»™ xe</span>
                  <strong>{formatDateTimeValue(formData.estimatedHandover || suggestedFinish.finishIso)}</strong>
                </div>
                {isInsuranceOrderEnabled ? (
                  <>
                    <div className="reception-summary-inline-row">
                      <span>Báº£o hiá»ƒm dá»± kiáº¿n tráº£</span>
                      <strong>{formatCurrency(insuranceDraftBreakdown.insuranceRequestedAmount)}</strong>
                    </div>
                    <div className="reception-summary-inline-row">
                      <span>KhÃ¡ch dá»± kiáº¿n tráº£</span>
                      <strong>{formatCurrency(insuranceDraftBreakdown.customerPayAmount)}</strong>
                    </div>
                  </>
                ) : null}
                <div className="reception-summary-inline-row">
                  <span>Luá»“ng tiáº¿p nháº­n</span>
                  <strong>{bookingFlow === BOOKING_FLOW.FLEXIBLE ? "Check-in linh hoáº¡t" : "Khung giá» sá»­a chá»¯a"}</strong>
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
            Há»§y vÃ  quay láº¡i
          </button>
          <button type="submit" className="btn-primary-large" disabled={saving || isEditLocked}>
            {saving ? "Äang cáº­p nháº­t..." : "LÆ°u thay Ä‘á»•i"}
          </button>
        </div>
      </form>
    </div>
  );
}



