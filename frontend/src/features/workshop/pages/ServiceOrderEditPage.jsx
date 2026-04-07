import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";
import "./Workshop.css";

const DEFAULT_FORM_DATA = {
  orderCategory: "GENERAL_REPAIR",
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
        .filter((id) => Number.isFinite(id) && id > 0)
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
  return {
    id,
    categoryId,
    name: String(readField(service, ["name", "Name"], "") || "").trim(),
    minutes: Math.max(0, Number(readField(service, ["standardMinutes", "StandardMinutes"], 0) || 0)),
    price: Math.max(0, Number(readField(service, ["basePrice", "BasePrice"], 0) || 0)),
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
    || ["APPROVED", "IN_PROGRESS", "REPAIRING", "READY", "WAITING_PAYMENT", "CLOSED", "CANCELED", "CANCELLED"].includes(normalizedStatus);
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

function parseSymptomNotes(rawCustomerNotes) {
  const rawText = String(rawCustomerNotes || "").trim();
  if (!rawText) return [];

  try {
    const parsed = JSON.parse(rawText);
    const fromObject =
      parsed && typeof parsed === "object" && Array.isArray(parsed.symptoms) ? parsed.symptoms : Array.isArray(parsed) ? parsed : null;
    if (fromObject) {
      return fromObject
        .map((value) => String(value || "").trim())
        .filter((line) => line && normalizeSearchText(line) !== "khong co ghi chu gi" && normalizeSearchText(line) !== "khong co ghi chu");
    }
    if (typeof parsed === "string") {
      const line = parsed.trim();
      return line ? [line] : [];
    }
  } catch {
    return rawText ? [rawText] : [];
  }

  return [];
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
  const current = new Date(checkInText);
  if (Number.isNaN(current.getTime())) return { finishIso: "", finishText: "" };

  const alignToWindow = (value) => {
    const next = new Date(value);
    while (next.getDay() === 0) {
      next.setDate(next.getDate() + 1);
      next.setHours(8, 0, 0, 0);
    }
    const openMinutes = 8 * 60;
    const closeMinutes = 20 * 60;
    const currentMinutes = next.getHours() * 60 + next.getMinutes();
    if (currentMinutes < openMinutes) next.setHours(8, 0, 0, 0);
    if (currentMinutes >= closeMinutes) {
      next.setDate(next.getDate() + 1);
      next.setHours(8, 0, 0, 0);
      while (next.getDay() === 0) {
        next.setDate(next.getDate() + 1);
        next.setHours(8, 0, 0, 0);
      }
    }
    return next;
  };

  let cursor = alignToWindow(current);
  let remaining = Number(totalMinutes);
  while (remaining > 0) {
    cursor = alignToWindow(cursor);
    const closeToday = new Date(cursor);
    closeToday.setHours(20, 0, 0, 0);
    const available = Math.max(0, Math.floor((closeToday.getTime() - cursor.getTime()) / 60000));
    if (available <= 0) {
      cursor.setDate(cursor.getDate() + 1);
      cursor.setHours(8, 0, 0, 0);
      continue;
    }
    const consume = Math.min(remaining, available);
    cursor.setMinutes(cursor.getMinutes() + consume);
    remaining -= consume;
    if (remaining > 0) {
      cursor.setDate(cursor.getDate() + 1);
      cursor.setHours(8, 0, 0, 0);
    }
  }

  const finishIso = toLocalDateTimeValue(cursor);
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

  const [order, setOrder] = useState(null);
  const [editCapability, setEditCapability] = useState(null);
  const [staff, setStaff] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);

  const [serviceSearch, setServiceSearch] = useState("");
  const [activeCategoryId, setActiveCategoryId] = useState("");
  const [formData, setFormData] = useState(DEFAULT_FORM_DATA);
  const [symptomInputs, setSymptomInputs] = useState([""]);
  const pickerMinDate = useMemo(() => new Date(2000, 0, 1), []);
  const roleCode = String(user?.roleCode ?? user?.RoleCode ?? "").trim();
  const roleName = String(user?.roleName ?? user?.RoleName ?? "").trim();

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
    return (services || []).map(normalizeService).filter((service) => service.id > 0 && service.name);
  }, [services]);

  const categoryOptions = useMemo(
    () => buildCategoryOptions(serviceCategories, normalizedServices),
    [serviceCategories, normalizedServices]
  );

  const serviceById = useMemo(
    () => new Map(normalizedServices.map((service) => [Number(service.id), service])),
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

  const selectedServices = useMemo(() => {
    return toNumberList(formData.serviceIds)
      .map((serviceId) => serviceById.get(serviceId))
      .filter(Boolean);
  }, [formData.serviceIds, serviceById]);

  const filteredServices = useMemo(() => {
    const term = normalizeSearchText(serviceSearch);
    const activeCategory = parsePositiveId(activeCategoryId);

    return normalizedServices
      .filter((service) => (activeCategory > 0 ? Number(service.categoryId) === activeCategory : true))
      .filter((service) => (term ? normalizeSearchText(service.name).includes(term) : true))
      .sort((a, b) => String(a.name || "").localeCompare(String(b.name || ""), "vi"));
  }, [activeCategoryId, normalizedServices, serviceSearch]);

  const { totalMinutes, totalPrice, suggestedFinish } = useMemo(() => {
    const minutes = selectedServices.reduce((sum, service) => sum + Number(service?.minutes || 0), 0);
    const price = selectedServices.reduce((sum, service) => sum + Number(service?.price || 0), 0);
    return {
      totalMinutes: minutes,
      totalPrice: price,
      suggestedFinish: calculateEstimatedFinish(formData.checkInTime, minutes),
    };
  }, [formData.checkInTime, selectedServices]);

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

  const fetchData = useCallback(async () => {
    setLoading(true);
    setLoadingEditRequests(true);
    setError("");
    try {
      const [orderRes, capabilityRes, requestRes, staffRes, roleRes, categoryRes, serviceRes, detailsRes, customerRes, vehicleRes] = await Promise.all([
        serviceOrderApi.getById(orderId),
        serviceOrderApi.getEditCapabilities(orderId).catch(() => null),
        serviceOrderApi.getEditRequests(orderId).catch(() => []),
        getAllStaff(),
        getAllRoles(),
        getAllServiceCategories(),
        getAllStandardServices(),
        serviceOrderApi.getDetails(orderId).catch(() => []),
        getAllCustomers(),
        getAllVehicles(),
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

      const parsedSymptoms = parseSymptomNotes(readField(orderRes, ["customerNotes", "CustomerNotes"], ""));
      const checkInTime = toLocalDateTimeValue(readField(orderRes, ["checkInTime", "CheckInTime"], null));
      const estimatedHandover = toLocalDateTimeValue(readField(orderRes, ["estimatedHandover", "EstimatedHandover"], null));
      const promisedHandoverTime = toLocalDateTimeValue(readField(orderRes, ["promisedHandoverTime", "PromisedHandoverTime"], null));
      const normalizedCategory = String(readField(orderRes, ["orderCategory", "OrderCategory"], "GENERAL_REPAIR") || "GENERAL_REPAIR").toUpperCase();
      const normalizedStatus = String(readField(orderRes, ["status", "Status"], "NEW") || "NEW").toUpperCase();

      setOrder(orderRes);
      setEditCapability(normalizeServiceOrderEditCapability(capabilityRes));
      setStaff(filteredStaff);
      setServiceCategories(Array.isArray(categoryRes) ? categoryRes : []);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setEditRequests(
        (Array.isArray(requestRes) ? requestRes : [])
          .map(normalizeServiceOrderEditRequestItem)
          .filter((item) => item.requestId > 0)
      );

      setFormData({
        orderCategory: normalizedCategory,
        serviceAdvisorId: parsePositiveId(readField(orderRes, ["serviceAdvisorId", "ServiceAdvisorId"], 0)) || "",
        oldPartsReturnOption: String(readField(orderRes, ["oldPartsReturnOption", "OldPartsReturnOption"], "DISPOSE_ALL") || "DISPOSE_ALL"),
        requiredDepositAmount: Number(readField(orderRes, ["requiredDepositAmount", "RequiredDepositAmount"], 0) || 0),
        checkInTime: checkInTime || toLocalDateTimeValue(new Date()),
        estimatedHandover,
        promisedHandoverTime,
        status: normalizedStatus,
        serviceIds: initialServiceIds,
      });

      setSymptomInputs(parsedSymptoms.length > 0 ? [...parsedSymptoms, ""] : [""]);

      const firstService = initialServiceIds.length > 0 ? Number(initialServiceIds[0]) : 0;
      const firstNormalized = (Array.isArray(serviceRes) ? serviceRes : [])
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
    if (!activeCategoryId && categoryOptions.length > 0) {
      setActiveCategoryId(String(categoryOptions[0].id));
    }
  }, [activeCategoryId, categoryOptions]);

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

      if (!data.serviceAdvisorId) errors.serviceAdvisorId = "Vui lòng chọn cố vấn dịch vụ.";
      if (!checkIn || Number.isNaN(checkIn.getTime())) {
        errors.checkInTime = "Vui lòng nhập thời điểm xe vào xưởng hợp lệ.";
      }

      if (estimated && checkIn && !Number.isNaN(estimated.getTime()) && estimated < checkIn) {
        errors.estimatedHandover = "Dự kiến hoàn thành phải lớn hơn hoặc bằng thời điểm xe vào xưởng.";
      }

      if (promised && checkIn && !Number.isNaN(promised.getTime()) && promised < checkIn) {
        errors.promisedHandoverTime = "Ngày giờ cam kết trả xe phải lớn hơn hoặc bằng thời điểm xe vào xưởng.";
      }

      if (Number(data.requiredDepositAmount || 0) < 0) {
        errors.requiredDepositAmount = "Số tiền cọc không được nhỏ hơn 0.";
      }

      if (toNumberList(data.serviceIds).length === 0) {
        errors.serviceIds = "Vui lòng chọn ít nhất một dịch vụ.";
      }

      return errors;
    },
    [suggestedFinish.finishIso]
  );

  const handleFieldValueChange = useCallback((fieldName, value) => {
    setFormData((prev) => ({ ...prev, [fieldName]: value }));
  }, []);

  const handleChange = useCallback((event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  }, []);

  const handleServiceToggle = useCallback((serviceId) => {
    const normalizedId = Number(serviceId);
    if (!Number.isFinite(normalizedId) || normalizedId <= 0) return;

    setFormData((prev) => {
      const current = toNumberList(prev.serviceIds);
      const has = current.includes(normalizedId);
      return {
        ...prev,
        serviceIds: has ? current.filter((id) => id !== normalizedId) : [...current, normalizedId],
      };
    });
  }, []);

  const handleSymptomChange = useCallback((index, value) => {
    setSymptomInputs((prev) => {
      const next = [...prev];
      next[index] = value;
      const isLast = index === next.length - 1;
      if (isLast && String(value || "").trim() && next.length < 12) {
        next.push("");
      }
      while (next.length > 1) {
        const last = next[next.length - 1];
        const secondLast = next[next.length - 2];
        if (String(last || "").trim() || !String(secondLast || "").trim()) break;
        next.pop();
      }
      return next;
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
      setFieldErrors(errors);
      if (Object.keys(errors).length > 0) {
        setError("Vui lòng xử lý các lỗi trong biểu mẫu trước khi lưu.");
        return;
      }

      setSaving(true);
      try {
        const normalizedSymptoms = symptomInputs
          .map((line) => String(line || "").trim())
          .filter(Boolean);

        const payload = {
          orderCategory: formData.orderCategory || null,
          serviceAdvisorId: parsePositiveId(formData.serviceAdvisorId) || null,
          oldPartsReturnOption: formData.oldPartsReturnOption || "DISPOSE_ALL",
          requiredDepositAmount: Math.max(0, Number(formData.requiredDepositAmount || 0)),
          checkInTime: formData.checkInTime || null,
          estimatedHandover: formData.estimatedHandover || suggestedFinish.finishIso || null,
          promisedHandoverTime: formData.promisedHandoverTime || null,
          status: formData.status || "NEW",
          serviceIds: toNumberList(formData.serviceIds),
          symptomNotes: normalizedSymptoms,
        };

        await serviceOrderApi.updateWorkshop(orderId, payload);
        navigate(`/workshop/reception?viewOrderId=${orderId}`);
      } catch (err) {
        setError(getApiErrorMessage(err));
      } finally {
        setSaving(false);
      }
    },
    [formData, navigate, order, orderId, saving, suggestedFinish.finishIso, symptomInputs, validateForm]
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
                    {categoryOptions.map((category) => (
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
                <label>Mô tả triệu chứng</label>
                <div className="reception-note-editor">
                  {symptomInputs.map((symptom, index) => (
                    <textarea
                      key={`symptom-${index}`}
                      rows={1}
                      className="reception-note-input"
                      value={symptom}
                      placeholder={`Nhập mô tả ${index + 1}...`}
                      onChange={(event) => handleSymptomChange(index, event.target.value)}
                    />
                  ))}
                </div>
              </div>

              <div className="timeline-grid timeline-grid-vertical">
                <div className="form-group">
                  <label>Thời điểm xe vào xưởng *</label>
                  <DateTimePicker
                    value={formData.checkInTime}
                    onChange={(value) => handleFieldValueChange("checkInTime", value)}
                    placeholder="Chọn ngày và giờ..."
                    minDate={pickerMinDate}
                  />
                  {showError("checkInTime") ? <div className="field-error">{fieldErrors.checkInTime}</div> : null}
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
                <div className="reception-summary-inline-row">
                  <span>Tổng thời lượng</span>
                  <strong>{`${totalMinutes} phút`}</strong>
                </div>
                <div className="reception-summary-inline-row">
                  <span>Tổng giá tạm tính</span>
                  <strong>{formatCurrency(totalPrice)}</strong>
                </div>
                <div className="reception-summary-inline-row">
                  <span>Dự kiến hoàn thành</span>
                  <strong>{suggestedFinish.finishText || "--"}</strong>
                </div>
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


