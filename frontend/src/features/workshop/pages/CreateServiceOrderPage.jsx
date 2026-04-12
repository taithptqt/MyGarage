import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { insuranceCompanyApi } from "@features/workshop/api/insuranceCompanyApi";
import { getAllCustomers, getAllCustomerVehicles, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { getAllBranchOperatingHours, getAllPackages, getVehicleBrands, getVehicleModels } from "@features/appointments/api/appointmentApi";
import { getNearestSlotSuggestion } from "@features/appointments/api/appointmentSlotsApi";
import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import WorkshopInsurancePanel from "@features/workshop/components/WorkshopInsurancePanel";
import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";
import {
  buildWorkshopBatchCreatePayload,
  calculateRoundedOccupancyMinutes,
} from "@features/workshop/utils/serviceOrderWorkflow";
import {
  buildInsuranceDraftBreakdown,
  INSURANCE_ORDER_CATEGORY,
  isInsuranceOrderActive,
  isInsuranceSignalService,
} from "@features/workshop/utils/insuranceOrderUi";
import {
  buildFlexibleCompletionWarningMessage,
  buildFlexibleTriggerServiceIdSet,
  buildInsuranceFlexiblePackageIdSet,
  buildSlotSelectionGuide,
  findUnresolvedPackageIds,
  formatCompletionWarningTime,
  resolveBookingFlowByItemsAndDuration,
  WORKING_DAY_MINUTES,
} from "@features/appointments/utils/bookingFlowRules";
import {
  buildInsuranceSummary,
  createEmptyInsuranceSnapshot,
  INSURANCE_ASSET_TYPE,
  normalizeInsuranceSnapshot,
  resolveInsuranceDeductibleAmount,
  resolveInsuranceQuoteState,
  validateInsuranceSnapshotByMode,
} from "@features/appointments/utils/bookingInsurance";
import { VIETNAM_LICENSE_PLATE_PATTERN, VIETNAM_LICENSE_PLATE_TITLE, isValidVietnameseLicensePlate } from "@shared/utils/licensePlateValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import { VIETNAM_VIN_PATTERN, VIETNAM_VIN_TITLE, formatVinInput, isValidVin } from "@shared/utils/vinValidation";
import {
  buildInspectionTriadTypeByServiceId,
  getInspectionTriadDisplayName,
  getConflictingInspectionTriadServiceIds,
  sumServiceMinutes,
} from "@shared/utils/serviceDurationUtils";
import { addMinutesWithinWorkingHours } from "@shared/utils/workingHoursSchedule";
import { mergeBookableServicesAndPackages, splitBookingItemIds } from "@features/appointments/utils/bookingPackages";
import {
  FLEET_SERVICE_PRICE_LABEL,
  SERVICE_PRICE_DISCLAIMER,
  SERVICE_PRICE_LABEL,
} from "@shared/constants/servicePricing";
import {
  arePlanSelectionsEqual,
  collectCompatibleCategoryIds,
  filterBookableItemsForVehicleModels,
  prunePlanByVehicleCompatibility,
} from "@shared/utils/serviceEngineCompatibility";
import { getBranchClosedMessage, isBranchClosedBySchedule } from "@shared/utils/branchOperatingSchedule";
import "./Workshop.css";

const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM",
};

const EMPTY_PLAN = {
  categoryIds: [],
  serviceIds: [],
  excludedSharedCategoryIds: [],
  excludedSharedServiceIds: [],
};

const VEHICLE_KEY_GUEST = "guest";
const VEHICLE_KEY_NEW = "new-customer";
const VN_PHONE_PATTERN = "^(0[0-9]{9}|\\+84[0-9]{9})$";

const EMPTY_FORM_DATA = {
  customerId: "",
  selectedVehicleIds: [],
  vehicleLicensePlate: "",
  vehicleModelId: "",
  vehicleColor: "",
  vehicleVin: "",
  vehicleYear: "",
  guestName: "",
  guestPhone: "",
  guestVehiclePlate: "",
  guestVehicleModelId: "",
  guestVehicleColor: "",
  guestVehicleVin: "",
  guestVehicleYear: "",
  serviceAdvisorId: "",
  orderCategory: "GENERAL_REPAIR",
  insuranceEnabled: false,
  insuranceCompanyId: "",
  insuranceDeductibleAmount: null,
  insuranceDeductibleOption: "",
  insuranceClaimMode: "",
  insuranceQuoteState: "",
  insuranceCardImageUrls: [],
  insuranceDamageImageUrls: [],
  oldPartsReturnOption: "DISPOSE_ALL",
  requiredDepositAmount: 0,
  checkInTime: "",
  estimatedHandover: "",
  promisedHandoverTime: "",
  isQuickOrder: false,
  isNewVehicle: false,
};

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

function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

function normalizePhone(phone) {
  return String(phone || "").replace(/\s+/g, "");
}

function normalizeSearchText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

function validateVietnamesePhone(phone) {
  return /^(0\d{9}|\+84\d{9})$/.test(normalizePhone(phone));
}

function getVehicleKeyFromId(vehicleId) {
  return `existing-${Number(vehicleId)}`;
}

function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : null;
}

function normalizePlan(plan) {
  const categoryIds = toNumberList(plan?.categoryIds || []);
  const serviceIds = toNumberList(plan?.serviceIds || []);
  const excludedSharedCategoryIds = toNumberList(plan?.excludedSharedCategoryIds || []);
  const excludedSharedServiceIds = toNumberList(plan?.excludedSharedServiceIds || []);
  return { categoryIds, serviceIds, excludedSharedCategoryIds, excludedSharedServiceIds };
}

function clonePlan(plan) {
  return {
    categoryIds: [...(plan?.categoryIds || [])],
    serviceIds: [...(plan?.serviceIds || [])],
    excludedSharedCategoryIds: [...(plan?.excludedSharedCategoryIds || [])],
    excludedSharedServiceIds: [...(plan?.excludedSharedServiceIds || [])],
  };
}

function getMissingSpecificServiceByVehicle(planSummaries, serviceCategoryMap, categoryNameById) {
  return (planSummaries || [])
    .map((plan) => {
      const categoryIds = toNumberList(plan?.categoryIds || []);
      if (categoryIds.length === 0) return null;

      const coveredCategoryIds = new Set(
        toNumberList(plan?.serviceIds || [])
          .map((serviceId) => Number(serviceCategoryMap.get(Number(serviceId)) || 0))
          .filter((categoryId) => Number.isFinite(categoryId) && categoryId > 0)
      );

      const missingCategoryNames = categoryIds
        .filter((categoryId) => !coveredCategoryIds.has(Number(categoryId)))
        .map((categoryId) => categoryNameById.get(Number(categoryId)) || `Nhóm ${categoryId}`);

      if (missingCategoryNames.length === 0) return null;

      return {
        vehicleLabel: String(plan?.vehicleLabel || plan?.vehicleKey || "Xe chưa xác định"),
        missingCategoryNames,
      };
    })
    .filter(Boolean);
}

function buildMissingSpecificServiceMessage(missingByVehicle) {
  if (!Array.isArray(missingByVehicle) || missingByVehicle.length === 0) return "";

  const details = missingByVehicle.map((item) => {
    const categories = (item.missingCategoryNames || []).map((name) => `'${name}'`).join(", ");
    return `${item.vehicleLabel}: ${categories}`;
  });

  return `Vui lòng chọn dịch vụ cụ thể cho từng loại dịch vụ trước khi chọn khung giờ. Thiếu trên các xe: ${details.join("; ")}.`;
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
    const name = String(readField(category, ["categoryName", "name", "Name"], "") || "").trim();
    if (!name) return;
    if (category?.isDeleted === true || category?.isActive === false) return;
    map.set(id, { id, name });
  });

  (normalizedServices || []).forEach((service) => {
    if (!Number.isFinite(service.categoryId) || service.categoryId <= 0) return;
    if (!map.has(service.categoryId)) {
      map.set(service.categoryId, { id: service.categoryId, name: `Nhóm ${service.categoryId}` });
    }
  });

  return Array.from(map.values());
}

function formatSavedVehicleDescriptor(vehicle, brandNameById = new Map(), modelInfoById = new Map()) {
  const text = (value) => String(value || "").trim();

  const brandObject = readField(vehicle, ["brand", "Brand"], null);
  const modelObject = readField(vehicle, ["model", "Model", "vehicleModel", "VehicleModel"], null);

  const modelId = Number(
    readField(vehicle, ["modelId", "ModelId", "vehicleModelId", "VehicleModelId"], readField(modelObject, ["id", "Id"], 0))
  );
  const brandId = Number(
    readField(vehicle, ["brandId", "BrandId", "vehicleBrandId", "VehicleBrandId"], readField(brandObject, ["id", "Id"], 0))
  );

  const modelInfo = modelInfoById.get(modelId) || null;
  const lookupBrandNameFromModel = modelInfo?.brandId ? text(brandNameById.get(Number(modelInfo.brandId))) : "";
  const lookupBrandName = text(brandNameById.get(brandId));

  const brandName =
    [
      text(readField(vehicle, ["brandName", "BrandName", "vehicleBrandName", "VehicleBrandName"], "")),
      text(readField(brandObject, ["name", "Name", "brandName", "BrandName"], "")),
      text(modelInfo?.brandName),
      lookupBrandNameFromModel,
      lookupBrandName,
    ].find(Boolean) || "";

  const modelName =
    [
      text(readField(vehicle, ["modelName", "ModelName", "vehicleModelName", "VehicleModelName"], "")),
      text(readField(modelObject, ["modelName", "ModelName", "name", "Name"], "")),
      text(modelInfo?.name),
    ].find(Boolean) || "";

  if (brandName && modelName) return `${brandName} ${modelName}`;
  if (modelName) return modelName;
  if (brandName) return brandName;
  return "Chưa rõ hãng - chưa rõ dòng xe";
}

function toVehicleTabLabel(vehicleLabel, fallback = "") {
  const normalizedLabel = String(vehicleLabel || "").trim();
  if (!normalizedLabel) {
    const normalizedFallback = String(fallback || "").trim();
    return normalizedFallback || "Kh\u00f4ng bi\u1ec3n s\u1ed1";
  }

  const [platePart] = normalizedLabel.split(" - ");
  const plate = String(platePart || "").trim();
  return plate || normalizedLabel;
}

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

function parsePositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
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
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function formatTimeWithDateInBrackets(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";

  const time = parsed.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Asia/Ho_Chi_Minh",
  });
  const date = parsed.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    timeZone: "Asia/Ho_Chi_Minh",
  });

  return `${time} (${date})`;
}



function toMinuteEpoch(value) {
  if (!value) return null;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;
  parsed.setSeconds(0, 0);
  return parsed.getTime();
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

function formatSlotTime(value) {
  if (!value) return "--:--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--:--";
  return parsed.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function toDateKey(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function buildCheckInTimeWindowMap(now, rangeMinutes = 30) {
  const dayStart = new Date(now);
  dayStart.setHours(0, 0, 0, 0);

  const dayEnd = new Date(now);
  dayEnd.setHours(23, 59, 0, 0);

  const start = new Date(now);
  start.setSeconds(0, 0);
  start.setMinutes(start.getMinutes() - rangeMinutes);

  const end = new Date(now);
  end.setSeconds(0, 0);
  end.setMinutes(end.getMinutes() + rangeMinutes);

  if (start < dayStart) start.setTime(dayStart.getTime());
  if (end > dayEnd) end.setTime(dayEnd.getTime());

  const timeMap = {};
  const cursor = new Date(start);
  while (cursor <= end) {
    const hour = String(cursor.getHours()).padStart(2, "0");
    const minute = String(cursor.getMinutes()).padStart(2, "0");
    if (!Array.isArray(timeMap[hour])) timeMap[hour] = [];
    timeMap[hour].push(minute);
    cursor.setMinutes(cursor.getMinutes() + 1);
  }

  return timeMap;
}

function hasPlanCompletionWarning(plan) {
  if (!plan) return false;
  return Boolean(plan.bookingFlow === BOOKING_FLOW.FLEXIBLE && Number(plan.executionMinutes || 0) > 0 && plan.completionWarningText);
}

export default function CreateServiceOrderPage() {
  const navigate = useNavigate();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});

  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [staff, setStaff] = useState([]);
  const [orderCategories, setOrderCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [insuranceCompanies, setInsuranceCompanies] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [branchOperatingHours, setBranchOperatingHours] = useState([]);

  const [search, setSearch] = useState("");
  const [serviceSearch, setServiceSearch] = useState("");
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [guestSelectedBrandId, setGuestSelectedBrandId] = useState("");
  const [isSavedVehiclePopupOpen, setIsSavedVehiclePopupOpen] = useState(false);

  const [formData, setFormData] = useState(() => ({
    ...EMPTY_FORM_DATA,
    serviceAdvisorId: user?.id || "",
  }));
  const [insuranceUploadState, setInsuranceUploadState] = useState({});
  const [insuranceByVehicleKey, setInsuranceByVehicleKey] = useState({});
  const [activeInsuranceVehicleKey, setActiveInsuranceVehicleKey] = useState("");

  const [serviceMode, setServiceMode] = useState(SERVICE_MODE.SHARED);
  const [sharedPlan, setSharedPlan] = useState({ ...EMPTY_PLAN });
  const [vehiclePlans, setVehiclePlans] = useState({});
  const [activePlanVehicleKey, setActivePlanVehicleKey] = useState("");
  const [activeCategoryId, setActiveCategoryId] = useState("");
  const [isServicePickerCollapsed, setIsServicePickerCollapsed] = useState(false);

  const [vehicleNotesByKey, setVehicleNotesByKey] = useState({});
  const [activeNoteVehicleKey, setActiveNoteVehicleKey] = useState("");

  const years = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const result = [];
    for (let year = currentYear; year >= 1980; year -= 1) result.push(year);
    return result;
  }, []);

  const [checkInNow, setCheckInNow] = useState(() => new Date());
  const [nearestSlotSuggestion, setNearestSlotSuggestion] = useState(null);
  const [nearestSlotLoading, setNearestSlotLoading] = useState(false);
  const [nearestSlotError, setNearestSlotError] = useState("");
  const [selectedSuggestedSlotStart, setSelectedSuggestedSlotStart] = useState("");
  const [successOrderSummary, setSuccessOrderSummary] = useState(null);
  const lastAutoScrolledSlotStartRef = useRef("");

  const scrollToPageTop = useCallback(() => {
    if (typeof window === "undefined") return;
    window.scrollTo({ top: 0, behavior: "smooth" });
  }, []);

  useEffect(() => {
    if (!String(error || "").trim()) return;
    scrollToPageTop();
  }, [error, scrollToPageTop]);

  useEffect(() => {
    const timer = setInterval(() => setCheckInNow(new Date()), 30000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!successOrderSummary) return undefined;
    const timeoutId = setTimeout(() => {
      navigate("/workshop/reception", { replace: true });
    }, 2000);
    return () => clearTimeout(timeoutId);
  }, [navigate, successOrderSummary]);

  const checkInDayStart = useMemo(() => {
    const date = new Date(checkInNow);
    date.setHours(0, 0, 0, 0);
    return date;
  }, [checkInNow]);

  const checkInDayEnd = useMemo(() => {
    const date = new Date(checkInNow);
    date.setHours(23, 59, 59, 999);
    return date;
  }, [checkInNow]);

  const checkInAvailableTimeMap = useMemo(() => buildCheckInTimeWindowMap(checkInNow, 30), [checkInNow]);

  const effectiveBranchId = useMemo(() => {
    const fromContext = parsePositiveId(selectedBranchId);
    if (fromContext > 0) return fromContext;
    return parsePositiveId(user?.branchId ?? user?.BranchId);
  }, [selectedBranchId, user?.BranchId, user?.branchId]);

  const getApiErrorMessage = useCallback((err) => {
    const fallback = "Không thể tạo lệnh sửa chữa. Vui lòng kiểm tra dữ liệu và thử lại.";
    const data = err?.response?.data;
    const serverErrors = data?.errors && typeof data.errors === "object" ? Object.values(data.errors).flat() : [];
    if (serverErrors.length > 0) return String(serverErrors[0] || fallback);

    const raw = data?.message || data?.error || data?.detail || data?.title || err?.message || "";
    const text = String(raw);
    const lowerText = text.toLowerCase();

    if (/duplicate|already exists|đã tồn tại|unique/i.test(text)) {
      return "Dữ liệu bị trùng (biển số hoặc mã lệnh đã tồn tại). Vui lòng kiểm tra lại.";
    }
    if (/foreign key|constraint|ràng buộc/i.test(text)) {
      if (lowerText.includes("customerid")) return "Khách hàng đã chọn không còn hợp lệ. Vui lòng chọn lại khách hàng.";
      if (lowerText.includes("vehicleid")) return "Xe đã chọn không còn hợp lệ. Vui lòng chọn lại xe.";
      if (lowerText.includes("serviceadvisorid")) return "Cố vấn dịch vụ đã chọn không còn hợp lệ. Vui lòng chọn lại.";
      if (lowerText.includes("executebranchid") || lowerText.includes("branchid")) {
        return "Chi nhánh thực hiện không hợp lệ. Vui lòng chọn lại chi nhánh làm việc.";
      }
      return `Dữ liệu liên kết chưa hợp lệ. ${text}`;
    }
    if (/timeout|network|failed to fetch/i.test(text)) {
      return "Không kết nối được tới máy chủ. Vui lòng thử lại sau.";
    }

    return text || fallback;
  }, []);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [cRes, vRes, cvRes, sRes, rRes, catRes, srvRes, packageRes, insuranceRes, brandsRes, modelsRes, operatingHoursRes] = await Promise.all([
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStaff(),
        getAllRoles(),
        getAllServiceCategories(),
        getAllStandardServices(),
        getAllPackages().catch(() => []),
        insuranceCompanyApi.getAll().catch(() => []),
        getVehicleBrands(),
        getVehicleModels(),
        getAllBranchOperatingHours().catch(() => []),
      ]);

      setCustomers(Array.isArray(cRes) ? cRes : []);
      setVehicles(Array.isArray(vRes) ? vRes : []);
      setCustomerVehicles(Array.isArray(cvRes) ? cvRes : []);
      setOrderCategories(Array.isArray(catRes) ? catRes : []);
      setServices(mergeBookableServicesAndPackages(Array.isArray(srvRes) ? srvRes : [], Array.isArray(packageRes) ? packageRes : []));
      setInsuranceCompanies(Array.isArray(insuranceRes) ? insuranceRes : []);
      setBrands(Array.isArray(brandsRes) ? brandsRes : []);
      setModels(Array.isArray(modelsRes) ? modelsRes : []);
      setBranchOperatingHours(Array.isArray(operatingHoursRes) ? operatingHoursRes : []);

      const filteredStaff = (Array.isArray(sRes) ? sRes : []).filter((item) => {
        const role = (Array.isArray(rRes) ? rRes : []).find((r) => Number(r.id) === Number(item.roleId));
        const isMatchedRole = role && (role.roleCode === "RECEPTIONIST" || role.roleCode === "MANAGER");
        const isMatchedBranch = !effectiveBranchId || Number(item.branchId) === Number(effectiveBranchId);
        return isMatchedRole && isMatchedBranch;
      });
      setStaff(filteredStaff);
    } catch (err) {
      setError(`Không thể tải dữ liệu ban đầu: ${err?.message || "Lỗi không xác định"}`);
    } finally {
      setLoading(false);
    }
  }, [effectiveBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const resetPlanningState = useCallback(() => {
    setServiceMode(SERVICE_MODE.SHARED);
    setSharedPlan({ ...EMPTY_PLAN });
    setVehiclePlans({});
    setActivePlanVehicleKey("");
    setActiveCategoryId("");
    setIsServicePickerCollapsed(false);
    setVehicleNotesByKey({});
    setActiveNoteVehicleKey("");
    setServiceSearch("");
  }, []);

  const brandNameById = useMemo(() => {
    const map = new Map();
    brands.forEach((brand) => {
      const id = Number(readField(brand, ["id", "Id"], 0));
      if (!Number.isFinite(id) || id <= 0) return;
      const name = String(readField(brand, ["name", "Name", "brandName", "BrandName"], "") || "").trim();
      if (!name) return;
      map.set(id, name);
    });
    return map;
  }, [brands]);

  const modelInfoById = useMemo(() => {
    const map = new Map();
    models.forEach((model) => {
      const id = Number(readField(model, ["id", "Id"], 0));
      if (!Number.isFinite(id) || id <= 0) return;
      const name = String(readField(model, ["modelName", "ModelName", "name", "Name"], "") || "").trim();
      const brandId = Number(readField(model, ["brandId", "BrandId"], 0));
      const brandName = String(readField(model, ["brandName", "BrandName"], "") || "").trim();
      const fuelType = String(readField(model, ["fuelType", "FuelType"], "") || "").trim();
      map.set(id, {
        name,
        brandId: Number.isFinite(brandId) && brandId > 0 ? brandId : 0,
        brandName,
        fuelType,
      });
    });
    return map;
  }, [models]);

  const vehicleModelIdByKey = useMemo(() => {
    const map = new Map();
    if (Number(formData.guestVehicleModelId || 0) > 0) {
      map.set(VEHICLE_KEY_GUEST, Number(formData.guestVehicleModelId));
    }
    if (Number(formData.vehicleModelId || 0) > 0) {
      map.set(VEHICLE_KEY_NEW, Number(formData.vehicleModelId));
    }
    const customerId = Number(formData.customerId || 0);
    if (customerId > 0) {
      const linkedVehicleIds = new Set(
        customerVehicles
          .filter((item) => Number(item.customerId) === customerId)
          .map((item) => Number(item.vehicleId))
      );
      vehicles
        .filter((vehicle) => linkedVehicleIds.has(Number(vehicle.id)))
        .forEach((vehicle) => {
          const key = getVehicleKeyFromId(vehicle.id);
          const modelId = Number(readField(vehicle, ["modelId", "ModelId", "vehicleModelId", "VehicleModelId"], 0));
          if (Number.isFinite(modelId) && modelId > 0) {
            map.set(key, modelId);
          }
        });
    }
    return map;
  }, [customerVehicles, formData.customerId, formData.guestVehicleModelId, formData.vehicleModelId, vehicles]);

  const describeSavedVehicle = useCallback(
    (vehicle) => formatSavedVehicleDescriptor(vehicle, brandNameById, modelInfoById),
    [brandNameById, modelInfoById]
  );

  const filteredCustomers = useMemo(() => {
    const term = normalizeSearchText(search);
    if (!term) return [];
    return customers
      .filter((customer) => {
        const name = normalizeSearchText(customer.fullName || customer.name || "");
        const phone = normalizePhone(customer.phone || "");
        return name.includes(term) || phone.includes(term);
      })
      .slice(0, 8);
  }, [customers, search]);

  const customerVehiclesFiltered = useMemo(() => {
    if (!formData.customerId) return [];
    const customerId = Number(formData.customerId);
    const linkedVehicleIds = new Set(
      customerVehicles.filter((item) => Number(item.customerId) === customerId).map((item) => Number(item.vehicleId))
    );
    return vehicles.filter((vehicle) => linkedVehicleIds.has(Number(vehicle.id)));
  }, [customerVehicles, formData.customerId, vehicles]);

  const customerHasNoVehicles = Boolean(formData.customerId) && customerVehiclesFiltered.length === 0;

  const selectedVehicleIdSet = useMemo(() => new Set(toNumberList(formData.selectedVehicleIds)), [formData.selectedVehicleIds]);

  const selectedCustomerVehicles = useMemo(
    () => customerVehiclesFiltered.filter((vehicle) => selectedVehicleIdSet.has(Number(vehicle.id))),
    [customerVehiclesFiltered, selectedVehicleIdSet]
  );

  const selectableCustomerVehicles = useMemo(
    () => customerVehiclesFiltered.filter((vehicle) => !selectedVehicleIdSet.has(Number(vehicle.id))),
    [customerVehiclesFiltered, selectedVehicleIdSet]
  );

  const selectedVehicleKeys = useMemo(() => {
    if (formData.isQuickOrder) return [VEHICLE_KEY_GUEST];
    if (formData.isNewVehicle) return [VEHICLE_KEY_NEW];
    return toNumberList(formData.selectedVehicleIds).map((vehicleId) => getVehicleKeyFromId(vehicleId));
  }, [formData.isNewVehicle, formData.isQuickOrder, formData.selectedVehicleIds]);

  const hasMultipleSelectedVehicles = selectedVehicleKeys.length > 1;
  const showServiceModeSwitch = hasMultipleSelectedVehicles && !formData.isQuickOrder;
  const effectiveServiceMode = hasMultipleSelectedVehicles ? serviceMode : SERVICE_MODE.SHARED;
  const selectedVehicleModelIds = useMemo(
    () =>
      selectedVehicleKeys
        .map((vehicleKey) => Number(vehicleModelIdByKey.get(vehicleKey) || 0))
        .filter((modelId) => Number.isFinite(modelId) && modelId > 0),
    [selectedVehicleKeys, vehicleModelIdByKey]
  );

  const vehicleLabelByKey = useMemo(() => {
    const map = new Map();

    const guestPlate = formatLicensePlate(formData.guestVehiclePlate || "").trim();
    const newPlate = formatLicensePlate(formData.vehicleLicensePlate || "").trim();

    map.set(VEHICLE_KEY_GUEST, guestPlate ? `${guestPlate} - Xe khách vãng lai` : "Xe khách vãng lai");
    map.set(VEHICLE_KEY_NEW, newPlate ? `${newPlate} - Xe mới của khách` : "Xe mới của khách");

    customerVehiclesFiltered.forEach((vehicle) => {
      const key = getVehicleKeyFromId(vehicle.id);
      const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
      const descriptor = describeSavedVehicle(vehicle);
      map.set(key, descriptor ? `${plate} - ${descriptor}` : plate);
    });

    return map;
  }, [customerVehiclesFiltered, describeSavedVehicle, formData.guestVehiclePlate, formData.vehicleLicensePlate]);

  useEffect(() => {
    setVehiclePlans((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        next[key] = normalizePlan(prev[key] || EMPTY_PLAN);
      });
      return next;
    });

    setVehicleNotesByKey((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        if (Array.isArray(prev[key]) && prev[key].length > 0) {
          next[key] = [...prev[key]];
        } else {
          next[key] = [""];
        }
      });
      return next;
    });

    if (selectedVehicleKeys.length <= 1) {
      setServiceMode(SERVICE_MODE.SHARED);
    }

    setActivePlanVehicleKey((prev) => (selectedVehicleKeys.includes(prev) ? prev : selectedVehicleKeys[0] || ""));
    setActiveNoteVehicleKey((prev) => (selectedVehicleKeys.includes(prev) ? prev : selectedVehicleKeys[0] || ""));
    setInsuranceByVehicleKey((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        const fallbackSnapshot = createEmptyInsuranceSnapshot({
          insuranceCompanyId: formData.insuranceCompanyId,
          insuranceDeductibleOption: formData.insuranceDeductibleOption,
          insuranceDeductibleAmount: formData.insuranceDeductibleAmount,
          insuranceClaimMode: formData.insuranceClaimMode,
          insuranceQuoteState: formData.insuranceQuoteState,
          insuranceCardImageUrls: formData.insuranceCardImageUrls,
          insuranceDamageImageUrls: formData.insuranceDamageImageUrls,
        });
        next[key] = normalizeInsuranceSnapshot(prev[key] || fallbackSnapshot);
      });
      return next;
    });
    setActiveInsuranceVehicleKey((prev) => (selectedVehicleKeys.includes(prev) ? prev : selectedVehicleKeys[0] || ""));
  }, [selectedVehicleKeys]);

  useEffect(() => {
    if (!formData.customerId || formData.isQuickOrder || formData.isNewVehicle) {
      setIsSavedVehiclePopupOpen(false);
    }
  }, [formData.customerId, formData.isNewVehicle, formData.isQuickOrder]);

  useEffect(() => {
    if (!activeInsuranceVehicleKey) return;
    const snapshot = normalizeInsuranceSnapshot(
      insuranceByVehicleKey[activeInsuranceVehicleKey] || createEmptyInsuranceSnapshot()
    );

    setFormData((prev) => ({
      ...prev,
      insuranceCompanyId: snapshot.insuranceCompanyId || "",
      insuranceDeductibleAmount: snapshot.insuranceDeductibleAmount,
      insuranceDeductibleOption: snapshot.insuranceDeductibleOption || "",
      insuranceClaimMode: snapshot.insuranceClaimMode || "",
      insuranceQuoteState: snapshot.insuranceQuoteState || "",
      insuranceCardImageUrls: Array.isArray(snapshot.insuranceCardImageUrls) ? snapshot.insuranceCardImageUrls : [],
      insuranceDamageImageUrls: Array.isArray(snapshot.insuranceDamageImageUrls) ? snapshot.insuranceDamageImageUrls : [],
    }));
  }, [activeInsuranceVehicleKey, insuranceByVehicleKey]);

  useEffect(() => {
    if (effectiveServiceMode !== SERVICE_MODE.CUSTOM) return;
    setVehiclePlans((prev) => {
      const next = { ...prev };
      selectedVehicleKeys.forEach((key) => {
        if (!next[key]) next[key] = clonePlan(EMPTY_PLAN);
      });
      return next;
    });
  }, [effectiveServiceMode, selectedVehicleKeys]);

  const normalizedServices = useMemo(
    () =>
      services
        .map(normalizeService)
        .filter(
          (service) =>
            Number.isFinite(service.id) &&
            service.id !== 0 &&
            Number.isFinite(service.categoryId) &&
            service.categoryId > 0
        ),
    [services]
  );

  const serviceById = useMemo(() => {
    const map = new Map();
    normalizedServices.forEach((service) => map.set(Number(service.id), service));
    return map;
  }, [normalizedServices]);

  const flexibleTriggerServiceIdSet = useMemo(
    () => buildFlexibleTriggerServiceIdSet(normalizedServices),
    [normalizedServices]
  );
  const insuranceFlexiblePackageIdSet = useMemo(
    () => buildInsuranceFlexiblePackageIdSet(normalizedServices),
    [normalizedServices]
  );
  const insuranceCompanyNameById = useMemo(() => {
    const map = new Map();
    (insuranceCompanies || []).forEach((company) => {
      const id = Number(readField(company, ["id", "Id"], 0));
      const name = String(readField(company, ["name", "Name"], "") || "").trim();
      if (!Number.isFinite(id) || id <= 0 || !name) return;
      map.set(id, name);
    });
    return map;
  }, [insuranceCompanies]);

  const serviceCategoryMap = useMemo(() => {
    const map = new Map();
    normalizedServices.forEach((service) => map.set(Number(service.id), Number(service.categoryId)));
    return map;
  }, [normalizedServices]);

  const inspectionTriadTypeByServiceId = useMemo(
    () => buildInspectionTriadTypeByServiceId(normalizedServices),
    [normalizedServices]
  );

  const getConflictingInspectionServiceIds = useCallback(
    (serviceIds, targetServiceId) =>
      toNumberList(
        getConflictingInspectionTriadServiceIds(
          serviceIds,
          targetServiceId,
          inspectionTriadTypeByServiceId
        )
      ),
    [inspectionTriadTypeByServiceId]
  );

  const categoryOptions = useMemo(
    () => buildCategoryOptions(orderCategories, normalizedServices),
    [orderCategories, normalizedServices]
  );

  const categoryNameById = useMemo(() => {
    const map = new Map();
    categoryOptions.forEach((category) => map.set(Number(category.id), String(category.name || "").trim()));
    return map;
  }, [categoryOptions]);

  const activePlanKey = useMemo(() => {
    if (effectiveServiceMode === SERVICE_MODE.SHARED) return "shared";
    return activePlanVehicleKey || selectedVehicleKeys[0] || "";
  }, [effectiveServiceMode, activePlanVehicleKey, selectedVehicleKeys]);
  const activePlanVehicleModelIds = useMemo(() => {
    if (effectiveServiceMode === SERVICE_MODE.SHARED) {
      return selectedVehicleModelIds;
    }
    const modelId = Number(vehicleModelIdByKey.get(activePlanKey || "") || 0);
    return Number.isFinite(modelId) && modelId > 0 ? [modelId] : [];
  }, [activePlanKey, effectiveServiceMode, selectedVehicleModelIds, vehicleModelIdByKey]);

  const resolveEffectivePlan = useCallback(
    (rawVehiclePlan, sharedSource = sharedPlan) => {
      const sharedNormalized = normalizePlan(sharedSource || EMPTY_PLAN);
      const raw = normalizePlan(rawVehiclePlan || EMPTY_PLAN);

      const excludedCategorySet = new Set(raw.excludedSharedCategoryIds || []);
      const excludedServiceSet = new Set(raw.excludedSharedServiceIds || []);

      const sharedCategoryIds = sharedNormalized.categoryIds.filter((id) => !excludedCategorySet.has(id));
      const nextCategoryIds = toNumberList([...sharedCategoryIds, ...(raw.categoryIds || [])]);

      const sharedServiceIds = sharedNormalized.serviceIds.filter((id) => !excludedServiceSet.has(id));
      const nextServiceIds = toNumberList([...sharedServiceIds, ...(raw.serviceIds || [])]).filter((serviceId) => {
        const categoryId = Number(serviceCategoryMap.get(serviceId) || 0);
        return nextCategoryIds.includes(categoryId);
      });

      return normalizePlan({
        categoryIds: nextCategoryIds,
        serviceIds: nextServiceIds,
        excludedSharedCategoryIds: (raw.excludedSharedCategoryIds || []).filter((id) =>
          sharedNormalized.categoryIds.includes(id)
        ),
        excludedSharedServiceIds: (raw.excludedSharedServiceIds || []).filter((id) =>
          sharedNormalized.serviceIds.includes(id)
        ),
      });
    },
    [serviceCategoryMap, sharedPlan]
  );

  const deriveRawVehiclePlanFromEffective = useCallback(
    (effectivePlan, sharedSource = sharedPlan) => {
      const sharedNormalized = normalizePlan(sharedSource || EMPTY_PLAN);
      const effective = normalizePlan(effectivePlan || EMPTY_PLAN);
      const sharedCategorySet = new Set(sharedNormalized.categoryIds || []);
      const sharedServiceSet = new Set(sharedNormalized.serviceIds || []);

      const rawCategoryIds = (effective.categoryIds || []).filter((id) => !sharedCategorySet.has(id));
      const excludedSharedCategoryIds = (sharedNormalized.categoryIds || []).filter(
        (id) => !effective.categoryIds.includes(id)
      );

      const rawServiceIds = (effective.serviceIds || [])
        .filter((id) => !sharedServiceSet.has(id))
        .filter((serviceId) => {
          const categoryId = Number(serviceCategoryMap.get(serviceId) || 0);
          return effective.categoryIds.includes(categoryId);
        });
      const excludedSharedServiceIds = (sharedNormalized.serviceIds || []).filter(
        (id) => !effective.serviceIds.includes(id)
      );

      return normalizePlan({
        categoryIds: rawCategoryIds,
        serviceIds: rawServiceIds,
        excludedSharedCategoryIds,
        excludedSharedServiceIds,
      });
    },
    [serviceCategoryMap, sharedPlan]
  );

  const activePlan = useMemo(() => {
    if (effectiveServiceMode === SERVICE_MODE.SHARED) return normalizePlan(sharedPlan);
    return resolveEffectivePlan(vehiclePlans[activePlanKey] || EMPTY_PLAN, sharedPlan);
  }, [effectiveServiceMode, sharedPlan, vehiclePlans, activePlanKey, resolveEffectivePlan]);

  useEffect(() => {
    setSharedPlan((prev) => {
      const next = prunePlanByVehicleCompatibility(
        normalizePlan(prev),
        normalizedServices,
        selectedVehicleModelIds,
        modelInfoById,
        serviceById
      );
      return arePlanSelectionsEqual(prev, next) ? prev : normalizePlan(next);
    });

    setVehiclePlans((prev) => {
      let hasChanges = false;
      const next = { ...prev };

      selectedVehicleKeys.forEach((vehicleKey) => {
        const modelId = Number(vehicleModelIdByKey.get(vehicleKey) || 0);
        const nextPlan = prunePlanByVehicleCompatibility(
          normalizePlan(prev[vehicleKey] || EMPTY_PLAN),
          normalizedServices,
          modelId > 0 ? [modelId] : [],
          modelInfoById,
          serviceById
        );
        if (!arePlanSelectionsEqual(prev[vehicleKey] || EMPTY_PLAN, nextPlan)) {
          next[vehicleKey] = normalizePlan(nextPlan);
          hasChanges = true;
        }
      });

      return hasChanges ? next : prev;
    });
  }, [
    modelInfoById,
    normalizedServices,
    selectedVehicleKeys,
    selectedVehicleModelIds,
    serviceById,
    vehicleModelIdByKey,
  ]);

  useEffect(() => {
    const selectedCategories = activePlan.categoryIds || [];
    if (selectedCategories.length === 0) {
      setActiveCategoryId("");
      return;
    }
    if (!activeCategoryId || !selectedCategories.includes(Number(activeCategoryId))) {
      setActiveCategoryId(String(selectedCategories[0]));
    }
  }, [activeCategoryId, activePlan.categoryIds]);

  const compatibleCategoryIds = useMemo(
    () =>
      collectCompatibleCategoryIds(
        normalizedServices,
        activePlanVehicleModelIds,
        modelInfoById,
        serviceById
      ),
    [activePlanVehicleModelIds, modelInfoById, normalizedServices, serviceById]
  );

  const visibleCategoryOptions = useMemo(
    () => categoryOptions.filter((category) => compatibleCategoryIds.has(Number(category.id))),
    [categoryOptions, compatibleCategoryIds]
  );

  const compatibleServicesForActivePlan = useMemo(
    () =>
      filterBookableItemsForVehicleModels(
        normalizedServices,
        activePlanVehicleModelIds,
        modelInfoById,
        serviceById
      ),
    [activePlanVehicleModelIds, modelInfoById, normalizedServices, serviceById]
  );

  const filteredServices = useMemo(() => {
    const term = normalizeSearchText(serviceSearch);
    const selectedCategories = new Set(activePlan.categoryIds || []);
    if (selectedCategories.size === 0) return [];

    return compatibleServicesForActivePlan
      .filter((service) => selectedCategories.has(Number(service.categoryId)))
      .filter((service) => (!activeCategoryId ? true : String(service.categoryId) === String(activeCategoryId)))
      .filter((service) => (term ? normalizeSearchText(service.name).includes(term) : true))
      .sort((a, b) => String(a.name || "").localeCompare(String(b.name || ""), "vi"));
  }, [activePlan.categoryIds, activeCategoryId, compatibleServicesForActivePlan, serviceSearch]);

  const plansByVehicleKey = useMemo(() => {
    const map = new Map();
    selectedVehicleKeys.forEach((key) => {
      if (effectiveServiceMode === SERVICE_MODE.SHARED) {
        map.set(key, normalizePlan(sharedPlan));
      } else {
        map.set(key, resolveEffectivePlan(vehiclePlans[key] || EMPTY_PLAN, sharedPlan));
      }
    });
    return map;
  }, [selectedVehicleKeys, effectiveServiceMode, sharedPlan, vehiclePlans, resolveEffectivePlan]);

  const calculateEstimatedFinish = useCallback((startText, totalMinutes) => {
    if (!startText || !totalMinutes || totalMinutes <= 0) return { finishText: "", finishIso: "" };

    const finishDate = addMinutesWithinWorkingHours(startText, totalMinutes, {
      openHour: 8,
      closeHour: 20,
    });
    if (!finishDate) return { finishText: "", finishIso: "" };

    const hh = String(finishDate.getHours()).padStart(2, "0");
    const mm = String(finishDate.getMinutes()).padStart(2, "0");
    const dd = String(finishDate.getDate()).padStart(2, "0");
    const mo = String(finishDate.getMonth() + 1).padStart(2, "0");
    const yy = finishDate.getFullYear();

    return {
      finishText: formatTimeWithDateInBrackets(finishDate),
      finishIso: `${yy}-${mo}-${dd}T${hh}:${mm}:00`,
    };
  }, []);

  const calculationStartTime = useMemo(
    () => selectedSuggestedSlotStart || "",
    [selectedSuggestedSlotStart]
  );

  const planSummaries = useMemo(() => {
    return selectedVehicleKeys.map((vehicleKey) => {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      const categoryIds = toNumberList(plan.categoryIds);
      const serviceIds = toNumberList(plan.serviceIds);
      const selectedServices = serviceIds.map((serviceId) => serviceById.get(Number(serviceId))).filter(Boolean);
      const serviceGroupsByCategory = Array.from(
        selectedServices.reduce((map, service) => {
          const resolvedCategoryId = Number(service?.categoryId || 0);
          const categoryName = categoryNameById.get(resolvedCategoryId) || `Nhóm ${resolvedCategoryId || "Khác"}`;
          const serviceName = String(service?.name || "").trim();
          if (!serviceName) return map;
          if (!map.has(categoryName)) map.set(categoryName, []);
          map.get(categoryName).push(serviceName);
          return map;
        }, new Map())
      ).map(([categoryName, serviceNames]) => ({
        categoryName,
        serviceNames: Array.from(new Set(serviceNames)),
      }));
      const serviceMinutes = sumServiceMinutes(selectedServices);
      const executionMinutes = calculateRoundedOccupancyMinutes(serviceMinutes);
      const flowState = resolveBookingFlowByItemsAndDuration({
        itemIds: serviceIds,
        flexibleTriggerServiceIdSet,
        insuranceFlexiblePackageIdSet,
        executionMinutes,
      });
      const { bookingFlow, isLongDurationFlow, isServiceDrivenFlexibleFlow, isInsurancePackageFlexibleFlow } = flowState;
      const totalPrice = selectedServices.reduce((sum, service) => sum + Number(service?.price || 0), 0);
      const categoryNames = categoryIds.map((id) => categoryNameById.get(id) || `Nhóm ${id}`);
      const { finishText, finishIso } = calculateEstimatedFinish(calculationStartTime, executionMinutes);
      const resolvedEndIso = finishIso || "";
      const resolvedEndLabel = resolvedEndIso
        ? formatTimeWithDateInBrackets(resolvedEndIso)
        : (finishText || "Chưa chọn");

      return {
        vehicleKey,
        vehicleLabel: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
        selectedServices,
        serviceIds,
        serviceNames: selectedServices.map((service) => String(service?.name || "").trim()).filter(Boolean),
        serviceGroupsByCategory,
        categoryIds,
        categoryNames,
        startTimeLabel: calculationStartTime ? formatTimeWithDateInBrackets(calculationStartTime) : "Chưa chọn",
        endTimeLabel: resolvedEndLabel,
        endTimeIso: resolvedEndIso,
        completionWarningTimeLabel: formatCompletionWarningTime(resolvedEndIso),
        completionWarningText: bookingFlow === BOOKING_FLOW.FLEXIBLE
          ? buildFlexibleCompletionWarningMessage({
            vehicleLabel: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
            completionValue: resolvedEndIso,
          })
          : "",
        bookingFlow,
        isLongDurationFlow,
        isServiceDrivenFlexibleFlow,
        isInsurancePackageFlexibleFlow,
        serviceMinutes,
        executionMinutes,
        totalPrice,
      };
    });
  }, [calculateEstimatedFinish, calculationStartTime, categoryNameById, flexibleTriggerServiceIdSet, insuranceFlexiblePackageIdSet, plansByVehicleKey, selectedVehicleKeys, serviceById, vehicleLabelByKey]);

  const missingSpecificServiceByVehicle = useMemo(
    () => getMissingSpecificServiceByVehicle(planSummaries, serviceCategoryMap, categoryNameById),
    [categoryNameById, planSummaries, serviceCategoryMap]
  );

  const missingSpecificServiceMessage = useMemo(
    () => buildMissingSpecificServiceMessage(missingSpecificServiceByVehicle),
    [missingSpecificServiceByVehicle]
  );
  const checkInSelectionGuardMessage = useMemo(() => {
    if (selectedVehicleKeys.length === 0) {
      return "Vui lòng chọn xe trước khi chọn giờ check-in.";
    }

    for (const vehicleKey of selectedVehicleKeys) {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      const vehicleLabel = vehicleLabelByKey.get(vehicleKey) || vehicleKey;
      const categoryCount = toNumberList(plan.categoryIds).length;
      const serviceCount = toNumberList(plan.serviceIds).length;
      const unresolvedPackageIds = findUnresolvedPackageIds(toNumberList(plan.serviceIds), serviceById);
      if (unresolvedPackageIds.length > 0) {
        return `Vui lòng chọn lại gói dịch vụ cho ${vehicleLabel} (mã không hợp lệ: #${unresolvedPackageIds.join(", #")}) trước khi chọn giờ check-in.`;
      }
      if (categoryCount === 0) {
        return `Vui lòng chọn loại dịch vụ cho ${vehicleLabel} trước khi chọn giờ check-in.`;
      }
      if (serviceCount === 0) {
        return `Vui lòng chọn dịch vụ cụ thể cho ${vehicleLabel} trước khi chọn giờ check-in.`;
      }
    }

    if (missingSpecificServiceByVehicle.length > 0) {
      return missingSpecificServiceMessage;
    }

    return "";
  }, [missingSpecificServiceByVehicle.length, missingSpecificServiceMessage, plansByVehicleKey, selectedVehicleKeys, serviceById, vehicleLabelByKey]);
  const hasCheckInInput = useMemo(
    () => Boolean(String(formData.checkInTime || "").trim()),
    [formData.checkInTime]
  );
  const slotSuggestionBaseTime = useMemo(() => {
    if (!hasCheckInInput) return "";
    return toLocalDateTimeValue(formData.checkInTime);
  }, [formData.checkInTime, hasCheckInInput]);

  const totalPrice = useMemo(
    () => planSummaries.reduce((sum, plan) => sum + Number(plan.totalPrice || 0), 0),
    [planSummaries]
  );
  const selectedServicesForInsurance = useMemo(
    () => planSummaries.flatMap((plan) => (Array.isArray(plan.selectedServices) ? plan.selectedServices : [])),
    [planSummaries]
  );
  const hasInsuranceSignalServiceSelected = useMemo(
    () => selectedServicesForInsurance.some((service) => isInsuranceSignalService(service)),
    [selectedServicesForInsurance]
  );
  const isInsuranceOrderEnabled = useMemo(
    () =>
      isInsuranceOrderActive({
        insuranceEnabled: formData.insuranceEnabled,
        orderCategory: formData.orderCategory,
        insuranceCompanyId: formData.insuranceCompanyId,
        services: selectedServicesForInsurance,
      }),
    [formData.insuranceCompanyId, formData.insuranceEnabled, formData.orderCategory, selectedServicesForInsurance]
  );
  const insuranceDraftBreakdownBase = useMemo(
    () =>
      buildInsuranceDraftBreakdown({
        services: selectedServicesForInsurance,
        enabled: isInsuranceOrderEnabled,
      }),
    [isInsuranceOrderEnabled, selectedServicesForInsurance]
  );
  const insuranceVehicleKeys = useMemo(() => {
    if (!isInsuranceOrderEnabled) return [];
    return selectedVehicleKeys;
  }, [isInsuranceOrderEnabled, selectedVehicleKeys]);

  const insuranceVehicleOptions = useMemo(
    () =>
      insuranceVehicleKeys.map((vehicleKey) => ({
        key: vehicleKey,
        label: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
      })),
    [insuranceVehicleKeys, vehicleLabelByKey]
  );
  const insuranceSnapshotCustomerPayAmount = useMemo(() => {
    if (!isInsuranceOrderEnabled || insuranceVehicleKeys.length === 0) return 0;
    return insuranceVehicleKeys.reduce((sum, vehicleKey) => {
      const snapshot = normalizeInsuranceSnapshot(
        insuranceByVehicleKey[vehicleKey] || createEmptyInsuranceSnapshot()
      );
      const amountFromOption = resolveInsuranceDeductibleAmount(snapshot.insuranceDeductibleOption);
      const amount = Number.isFinite(Number(amountFromOption))
        ? Number(amountFromOption)
        : Number(snapshot.insuranceDeductibleAmount || 0);
      return sum + (Number.isFinite(amount) && amount > 0 ? amount : 0);
    }, 0);
  }, [insuranceByVehicleKey, insuranceVehicleKeys, isInsuranceOrderEnabled]);
  const insuranceDraftBreakdown = useMemo(() => {
    if (!isInsuranceOrderEnabled || insuranceVehicleKeys.length === 0) {
      return insuranceDraftBreakdownBase;
    }
    const totalAmount = Number(insuranceDraftBreakdownBase.totalAmount || 0);
    const customerPayAmount = Math.max(0, Number(insuranceSnapshotCustomerPayAmount || 0));
    return {
      ...insuranceDraftBreakdownBase,
      customerPayAmount,
      insuranceRequestedAmount: Math.max(0, totalAmount - customerPayAmount),
    };
  }, [
    insuranceDraftBreakdownBase,
    insuranceSnapshotCustomerPayAmount,
    insuranceVehicleKeys.length,
    isInsuranceOrderEnabled,
  ]);

  const effectiveActiveInsuranceVehicleKey = useMemo(
    () => activeInsuranceVehicleKey || insuranceVehicleKeys[0] || "",
    [activeInsuranceVehicleKey, insuranceVehicleKeys]
  );

  const activeInsuranceSnapshot = useMemo(
    () =>
      normalizeInsuranceSnapshot(
        insuranceByVehicleKey[effectiveActiveInsuranceVehicleKey] || createEmptyInsuranceSnapshot()
      ),
    [effectiveActiveInsuranceVehicleKey, insuranceByVehicleKey]
  );
  const insuranceSummaryByVehicleKey = useMemo(() => {
    const map = new Map();
    if (!isInsuranceOrderEnabled) return map;

    selectedVehicleKeys.forEach((vehicleKey) => {
      const snapshot = normalizeInsuranceSnapshot(
        insuranceByVehicleKey[vehicleKey] || createEmptyInsuranceSnapshot()
      );
      map.set(vehicleKey, {
        summary: buildInsuranceSummary(snapshot, insuranceCompanyNameById, { mode: "workshop" }),
      });
    });

    return map;
  }, [
    insuranceByVehicleKey,
    insuranceCompanyNameById,
    isInsuranceOrderEnabled,
    selectedVehicleKeys,
  ]);

  useEffect(() => {
    if (insuranceVehicleKeys.length === 0) {
      setActiveInsuranceVehicleKey("");
      return;
    }
    setActiveInsuranceVehicleKey((prev) =>
      insuranceVehicleKeys.includes(prev) ? prev : insuranceVehicleKeys[0]
    );
  }, [insuranceVehicleKeys]);
  const completionMinutes = useMemo(
    () => Math.max(...planSummaries.map((plan) => Number(plan.executionMinutes || 0)), 0),
    [planSummaries]
  );

  useEffect(() => {
    const nextOrderCategory = isInsuranceOrderEnabled ? INSURANCE_ORDER_CATEGORY : "GENERAL_REPAIR";
    if (String(formData.orderCategory || "").trim().toUpperCase() === nextOrderCategory) return;
    setFormData((prev) => ({ ...prev, orderCategory: nextOrderCategory }));
  }, [formData.orderCategory, isInsuranceOrderEnabled]);

  const suggestedFinish = useMemo(
    () => calculateEstimatedFinish(calculationStartTime, completionMinutes),
    [calculateEstimatedFinish, calculationStartTime, completionMinutes]
  );

  const hasAnyServiceDrivenFlexibleFlow = useMemo(
    () => planSummaries.some((plan) => Boolean(plan.isServiceDrivenFlexibleFlow)),
    [planSummaries]
  );
  const hasAnyInsurancePackageFlexibleFlow = useMemo(
    () => planSummaries.some((plan) => Boolean(plan.isInsurancePackageFlexibleFlow)),
    [planSummaries]
  );
  const hasAnyLongDurationVehicle = useMemo(
    () => planSummaries.some((plan) => Number(plan.executionMinutes || 0) > WORKING_DAY_MINUTES),
    [planSummaries]
  );

  const effectiveSlotFlow = useMemo(
    () =>
      (hasAnyServiceDrivenFlexibleFlow || hasAnyInsurancePackageFlexibleFlow || hasAnyLongDurationVehicle
        ? BOOKING_FLOW.FLEXIBLE
        : BOOKING_FLOW.FIXED),
    [hasAnyInsurancePackageFlexibleFlow, hasAnyLongDurationVehicle, hasAnyServiceDrivenFlexibleFlow]
  );

  const slotSelectionGuide = useMemo(
    () =>
      buildSlotSelectionGuide({
        bookingFlow: effectiveSlotFlow,
        isLongDurationFlow: hasAnyLongDurationVehicle,
        isServiceDrivenFlexibleFlow: hasAnyServiceDrivenFlexibleFlow,
        inProgress: true,
      }),
    [effectiveSlotFlow, hasAnyLongDurationVehicle, hasAnyServiceDrivenFlexibleFlow]
  );

  const suggestedFleetCompletionIso = useMemo(
    () => suggestedFinish.finishIso || "",
    [suggestedFinish.finishIso]
  );

  const requestedServiceIdsForSlotSuggestion = useMemo(() => {
    const groups = selectedVehicleKeys.map((vehicleKey) => {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      return splitBookingItemIds(toNumberList(plan.serviceIds)).serviceIds;
    });

    if (groups.length === 0) return "";
    if (groups.length > 1) {
      return groups.map((group) => group.join(",")).join("|");
    }

    return (groups[0] || []).join(",");
  }, [plansByVehicleKey, selectedVehicleKeys]);

  const requestedPackageIdsForSlotSuggestion = useMemo(() => {
    const groups = selectedVehicleKeys.map((vehicleKey) => {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      return splitBookingItemIds(toNumberList(plan.serviceIds)).packageIds;
    });

    if (groups.length === 0) return "";
    if (groups.length > 1) {
      return groups.map((group) => group.join(",")).join("|");
    }

    return (groups[0] || []).join(",");
  }, [plansByVehicleKey, selectedVehicleKeys]);

  useEffect(() => {
    if (!effectiveBranchId || !slotSuggestionBaseTime) {
      setNearestSlotSuggestion(null);
      setNearestSlotError("");
      setNearestSlotLoading(false);
      return undefined;
    }

    let cancelled = false;
    setNearestSlotError("");

    const timer = setTimeout(async () => {
      if (cancelled) return;
      setNearestSlotLoading(true);
      try {
        const suggestion = await getNearestSlotSuggestion(
          effectiveBranchId,
          slotSuggestionBaseTime,
          {
            requestedServiceIds: requestedServiceIdsForSlotSuggestion || undefined,
            requestedPackageIds: requestedPackageIdsForSlotSuggestion || undefined,
            bookingFlow: effectiveSlotFlow,
            vehicleCount: Math.max(1, selectedVehicleKeys.length),
            isParallel: selectedVehicleKeys.length > 1,
            searchDays: 3,
          }
        );

        if (!cancelled) {
          setNearestSlotSuggestion(suggestion || null);
        }
      } catch (err) {
        if (!cancelled) {
          setNearestSlotError(err?.message || "Không thể gợi ý khung giờ gần nhất.");
        }
      } finally {
        if (!cancelled) {
          setNearestSlotLoading(false);
        }
      }
    }, 350);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [
    effectiveBranchId,
    effectiveSlotFlow,
    requestedPackageIdsForSlotSuggestion,
    requestedServiceIdsForSlotSuggestion,
    selectedVehicleKeys.length,
    slotSuggestionBaseTime,
  ]);

  const activeNoteKey = useMemo(
    () => activeNoteVehicleKey || selectedVehicleKeys[0] || "",
    [activeNoteVehicleKey, selectedVehicleKeys]
  );

  const activeNoteLines = useMemo(() => {
    if (!activeNoteKey) return [""];
    const current = vehicleNotesByKey?.[activeNoteKey];
    return Array.isArray(current) && current.length > 0 ? current : [""];
  }, [activeNoteKey, vehicleNotesByKey]);

  const validateForm = useCallback(
    (data) => {
      const errors = {};

      const now = new Date();
      const checkIn = data.checkInTime ? new Date(data.checkInTime) : null;
      const checkInWindowStart = new Date(now.getTime() - 30 * 60 * 1000);
      const checkInWindowEnd = new Date(now.getTime() + 30 * 60 * 1000);

      if (false && isInsuranceOrderEnabled && !parsePositiveId(data.insuranceCompanyId)) {
        errors.insuranceCompanyId = "Vui lòng chọn công ty bảo hiểm trước khi lưu lệnh.";
      }

      if (false && isInsuranceOrderEnabled && !String(data.insuranceDeductibleOption || "").trim()) {
        errors.insuranceDeductibleOption = "Vui lòng chọn mức miễn thường hoặc tư vấn.";
      }

      if (false && isInsuranceOrderEnabled && !String(data.insuranceClaimMode || "").trim()) {
        errors.insuranceClaimMode = "Vui lòng chọn hình thức xử lý bảo hiểm.";
      }

      if (isInsuranceOrderEnabled) {
        for (const vehicleKey of insuranceVehicleKeys) {
          const snapshot = normalizeInsuranceSnapshot(
            insuranceByVehicleKey[vehicleKey] ||
              (vehicleKey === effectiveActiveInsuranceVehicleKey
                ? {
                    insuranceCompanyId: data.insuranceCompanyId,
                    insuranceDeductibleOption: data.insuranceDeductibleOption,
                    insuranceDeductibleAmount: data.insuranceDeductibleAmount,
                    insuranceClaimMode: data.insuranceClaimMode,
                    insuranceQuoteState: data.insuranceQuoteState,
                    insuranceCardImageUrls: data.insuranceCardImageUrls,
                    insuranceDamageImageUrls: data.insuranceDamageImageUrls,
                  }
                : createEmptyInsuranceSnapshot())
          );
          const issue = validateInsuranceSnapshotByMode(snapshot);
          if (issue) {
            errors.insuranceCompanyId = `${vehicleLabelByKey.get(vehicleKey) || vehicleKey}: ${issue}`;
            break;
          }
        }
      }

      if (!data.serviceAdvisorId) errors.serviceAdvisorId = "Vui lòng chọn cố vấn dịch vụ.";
      if (!data.orderCategory) errors.orderCategory = "Vui lòng chọn phân loại lệnh sửa chữa.";

      if (!checkIn || Number.isNaN(checkIn.getTime())) {
        errors.checkInTime = "Vui lòng nhập thời điểm xe vào xưởng hợp lệ.";
      } else if (checkIn < checkInWindowStart || checkIn > checkInWindowEnd) {
        errors.checkInTime = "Thời điểm xe vào xưởng chỉ được trong khoảng ±30 phút so với hiện tại.";
      } else if (isBranchClosedBySchedule(branchOperatingHours, effectiveBranchId, checkIn)) {
        errors.checkInTime = getBranchClosedMessage(checkIn);
      }

      if (data.estimatedHandover && checkIn) {
        const estimated = new Date(data.estimatedHandover);
        if (!Number.isNaN(estimated.getTime()) && estimated <= checkIn) {
          errors.estimatedHandover = "Dự kiến hoàn thành phải sau thời điểm xe vào xưởng.";
        }
      }

      if (data.promisedHandoverTime && checkIn) {
        const promised = new Date(data.promisedHandoverTime);
        if (!Number.isNaN(promised.getTime()) && promised <= checkIn) {
          errors.promisedHandoverTime = "Ngày giờ cam kết trả xe phải sau thời điểm xe vào xưởng.";
        }
      }

      if (Number(data.requiredDepositAmount || 0) < 0) {
        errors.requiredDepositAmount = "Số tiền cọc không được nhỏ hơn 0.";
      }

      if (data.isQuickOrder) {
        if (!String(data.guestName || "").trim()) errors.guestName = "Vui lòng nhập họ tên khách vãng lai.";
        if (!String(data.guestPhone || "").trim()) {
          errors.guestPhone = "Vui lòng nhập số điện thoại khách vãng lai.";
        } else if (!validateVietnamesePhone(data.guestPhone)) {
          errors.guestPhone = "Số điện thoại không đúng định dạng Việt Nam.";
        }
        if (!String(data.guestVehiclePlate || "").trim()) errors.guestVehiclePlate = "Vui lòng nhập biển số xe.";
        else if (!isValidVietnameseLicensePlate(data.guestVehiclePlate)) errors.guestVehiclePlate = "Biển số xe không đúng định dạng Việt Nam.";
        if (!guestSelectedBrandId) errors.guestBrandId = "Vui lòng chọn hãng xe.";
        if (!data.guestVehicleModelId) errors.guestVehicleModelId = "Vui lòng chọn dòng xe.";
        if (!data.guestVehicleYear) errors.guestVehicleYear = "Vui lòng chọn năm sản xuất.";
        if (String(data.guestVehicleVin || "").trim() && !isValidVin(data.guestVehicleVin)) errors.guestVehicleVin = "Số VIN không hợp lệ (17 ký tự, không chứa I, O, Q).";
      } else {
        if (!data.customerId) errors.customerId = "Vui lòng chọn khách hàng.";

        if (!data.isNewVehicle) {
          if (toNumberList(data.selectedVehicleIds).length === 0) {
            errors.selectedVehicleIds = "Vui lòng chọn ít nhất 1 xe của khách.";
          }
        } else {
          if (!String(data.vehicleLicensePlate || "").trim()) errors.vehicleLicensePlate = "Vui lòng nhập biển số xe mới.";
          else if (!isValidVietnameseLicensePlate(data.vehicleLicensePlate)) errors.vehicleLicensePlate = "Biển số xe mới không đúng định dạng Việt Nam.";
          if (!selectedBrandId) errors.brandId = "Vui lòng chọn hãng xe.";
          if (!data.vehicleModelId) errors.vehicleModelId = "Vui lòng chọn dòng xe.";
          if (!data.vehicleYear) errors.vehicleYear = "Vui lòng chọn năm sản xuất.";
          if (String(data.vehicleVin || "").trim() && !isValidVin(data.vehicleVin)) errors.vehicleVin = "Số VIN không hợp lệ (17 ký tự, không chứa I, O, Q).";
        }
      }

      if (!effectiveBranchId) {
        errors.executeBranchId = "Vui lòng chọn chi nhánh làm việc trước khi tạo lệnh.";
      }

      if (selectedVehicleKeys.length === 0) {
        errors.selectedVehicleIds = "Vui lòng chọn xe trước khi chọn dịch vụ.";
      } else {
        const invalidPlans = selectedVehicleKeys.filter((vehicleKey) => {
          const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
          const categoryCount = toNumberList(plan.categoryIds).length;
          const serviceCount = toNumberList(plan.serviceIds).length;
          return categoryCount === 0 || serviceCount === 0;
        });
        if (invalidPlans.length > 0) {
          errors.serviceIds =
            selectedVehicleKeys.length === 1
              ? "Vui lòng chọn loại dịch vụ và ít nhất một dịch vụ."
              : "Mỗi xe cần có loại dịch vụ và ít nhất một dịch vụ trước khi tạo lệnh.";
        } else {
          const missingSpecificByVehicle = getMissingSpecificServiceByVehicle(
            selectedVehicleKeys.map((vehicleKey) => {
              const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
              return {
                vehicleKey,
                vehicleLabel: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
                categoryIds: toNumberList(plan.categoryIds),
                serviceIds: toNumberList(plan.serviceIds),
              };
            }),
            serviceCategoryMap,
            categoryNameById
          );

          if (missingSpecificByVehicle.length > 0) {
            errors.serviceIds = buildMissingSpecificServiceMessage(missingSpecificByVehicle);
          }
        }
      }

      return errors;
    },
    [
      branchOperatingHours,
      categoryNameById,
      effectiveActiveInsuranceVehicleKey,
      effectiveBranchId,
      guestSelectedBrandId,
      insuranceByVehicleKey,
      insuranceVehicleKeys,
      isInsuranceOrderEnabled,
      plansByVehicleKey,
      selectedBrandId,
      selectedVehicleKeys,
      serviceCategoryMap,
      vehicleLabelByKey,
    ]
  );

  useEffect(() => {
    if (!submitAttempted) return;
    setFieldErrors(validateForm(formData));
  }, [submitAttempted, formData, validateForm, sharedPlan, vehiclePlans, serviceMode]);

  const showError = useCallback((name) => submitAttempted && fieldErrors[name], [fieldErrors, submitAttempted]);

  const handleChange = useCallback((event) => {
    const { name, value, type, checked } = event.target;
    const inputValue = type === "checkbox" ? checked : value;
    const nextValue =
      name === "guestVehicleVin" || name === "vehicleVin"
          ? formatVinInput(inputValue)
        : formatLicensePlateByFieldName(name, inputValue);
    setFormData((prev) => ({ ...prev, [name]: nextValue }));
    setError("");
  }, []);

  const handleFieldValueChange = useCallback((name, value) => {
    if (name === "checkInTime" && checkInSelectionGuardMessage) {
      setFieldErrors((prev) => ({ ...prev, checkInTime: checkInSelectionGuardMessage }));
      return;
    }
    if (name === "checkInTime" && value && isBranchClosedBySchedule(branchOperatingHours, effectiveBranchId, value)) {
      setFieldErrors((prev) => ({ ...prev, checkInTime: getBranchClosedMessage(value) }));
      return;
    }
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (name === "checkInTime") {
      setFieldErrors((prev) => {
        if (!prev?.checkInTime) return prev;
        const next = { ...prev };
        delete next.checkInTime;
        return next;
      });
    }
    setError("");
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
      setInsuranceByVehicleKey((prev) => {
        const next = {};
        Object.keys(prev || {}).forEach((vehicleKey) => {
          next[vehicleKey] = createEmptyInsuranceSnapshot();
        });
        return next;
      });
    }
    if (!enabled) {
      setInsuranceUploadState({});
    }
    setError("");
  }, []);

  const handleInsuranceCompanyChange = useCallback(
    (value) => {
      const normalizedCompanyId = String(value || "");
      setFormData((prev) => ({
        ...prev,
        insuranceCompanyId: normalizedCompanyId,
      }));
      if (effectiveActiveInsuranceVehicleKey) {
        setInsuranceByVehicleKey((prev) => ({
          ...prev,
          [effectiveActiveInsuranceVehicleKey]: normalizeInsuranceSnapshot({
            ...(prev[effectiveActiveInsuranceVehicleKey] || createEmptyInsuranceSnapshot()),
            insuranceCompanyId: normalizedCompanyId,
          }),
        }));
      }
      setError("");
    },
    [effectiveActiveInsuranceVehicleKey]
  );

  const handleInsuranceDeductibleOptionChange = useCallback(
    (value) => {
      setFormData((prev) => ({
        ...prev,
        insuranceDeductibleOption: value,
        insuranceDeductibleAmount: resolveInsuranceDeductibleAmount(value),
        insuranceQuoteState: resolveInsuranceQuoteState(value, prev.insuranceClaimMode),
      }));
      if (effectiveActiveInsuranceVehicleKey) {
        setInsuranceByVehicleKey((prev) => {
          const currentSnapshot = normalizeInsuranceSnapshot(
            prev[effectiveActiveInsuranceVehicleKey] || createEmptyInsuranceSnapshot()
          );
          return {
            ...prev,
            [effectiveActiveInsuranceVehicleKey]: normalizeInsuranceSnapshot({
              ...currentSnapshot,
              insuranceDeductibleOption: value,
              insuranceDeductibleAmount: resolveInsuranceDeductibleAmount(value),
              insuranceQuoteState: resolveInsuranceQuoteState(value, currentSnapshot.insuranceClaimMode),
            }),
          };
        });
      }
      setError("");
    },
    [effectiveActiveInsuranceVehicleKey]
  );

  const handleInsuranceClaimModeChange = useCallback(
    (value) => {
      setFormData((prev) => ({
        ...prev,
        insuranceClaimMode: value,
        insuranceQuoteState: resolveInsuranceQuoteState(prev.insuranceDeductibleOption, value),
      }));
      if (effectiveActiveInsuranceVehicleKey) {
        setInsuranceByVehicleKey((prev) => {
          const currentSnapshot = normalizeInsuranceSnapshot(
            prev[effectiveActiveInsuranceVehicleKey] || createEmptyInsuranceSnapshot()
          );
          return {
            ...prev,
            [effectiveActiveInsuranceVehicleKey]: normalizeInsuranceSnapshot({
              ...currentSnapshot,
              insuranceClaimMode: value,
              insuranceQuoteState: resolveInsuranceQuoteState(currentSnapshot.insuranceDeductibleOption, value),
            }),
          };
        });
      }
      setError("");
    },
    [effectiveActiveInsuranceVehicleKey]
  );

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
        if (effectiveActiveInsuranceVehicleKey) {
          setInsuranceByVehicleKey((prev) => {
            const currentSnapshot = normalizeInsuranceSnapshot(
              prev[effectiveActiveInsuranceVehicleKey] || createEmptyInsuranceSnapshot()
            );
            return {
              ...prev,
              [effectiveActiveInsuranceVehicleKey]: normalizeInsuranceSnapshot({
                ...currentSnapshot,
                [targetField]: [...(Array.isArray(currentSnapshot?.[targetField]) ? currentSnapshot[targetField] : []), ...uploadedUrls],
              }),
            };
          });
        }
      }

      setInsuranceUploadState((prev) => ({
        ...prev,
        [assetType]: {
          uploading: false,
          successMessage: uploadedUrls.length > 0 ? "Đã tải ảnh bảo hiểm." : "",
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
      setError(uploadError?.response?.data?.message || uploadError?.message || "Không thể tải ảnh bảo hiểm lên máy chủ.");
    }
  }, [effectiveActiveInsuranceVehicleKey]);

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
    if (effectiveActiveInsuranceVehicleKey) {
      setInsuranceByVehicleKey((prev) => {
        const currentSnapshot = normalizeInsuranceSnapshot(
          prev[effectiveActiveInsuranceVehicleKey] || createEmptyInsuranceSnapshot()
        );
        return {
          ...prev,
          [effectiveActiveInsuranceVehicleKey]: normalizeInsuranceSnapshot({
            ...currentSnapshot,
            [targetField]: Array.isArray(currentSnapshot?.[targetField])
              ? currentSnapshot[targetField].filter((_, currentIndex) => currentIndex !== index)
              : [],
          }),
        };
      });
    }
    setInsuranceUploadState((prev) => ({
      ...prev,
      [assetType]: {
        uploading: false,
        successMessage: "",
      },
    }));
  }, [effectiveActiveInsuranceVehicleKey]);

  const suggestedAlternativeSlots = useMemo(() => {
    if (!nearestSlotSuggestion || !Array.isArray(nearestSlotSuggestion.alternatives)) {
      return [];
    }

    const normalizedSlots = nearestSlotSuggestion.alternatives
      .filter((slot) => {
        const slotStart = readField(slot, ["startTime", "StartTime"], null);
        return slot && slotStart;
      })
      .sort((a, b) => {
        const firstStart = readField(a, ["startTime", "StartTime"], null);
        const secondStart = readField(b, ["startTime", "StartTime"], null);
        return new Date(firstStart).getTime() - new Date(secondStart).getTime();
      });

    const baselineDateKey = toDateKey(slotSuggestionBaseTime);
    const sameDaySlots = baselineDateKey
      ? normalizedSlots.filter((slot) => toDateKey(readField(slot, ["startTime", "StartTime"], null)) === baselineDateKey)
      : [];

    const source = sameDaySlots.length > 0 ? sameDaySlots : normalizedSlots;
    return source.slice(0, 5);
  }, [nearestSlotSuggestion, slotSuggestionBaseTime]);

  const hasSameDaySuggestedSlots = useMemo(() => {
    const baselineDateKey = toDateKey(slotSuggestionBaseTime);
    if (!baselineDateKey || suggestedAlternativeSlots.length === 0) return true;
    return suggestedAlternativeSlots.some(
      (slot) => toDateKey(readField(slot, ["startTime", "StartTime"], null)) === baselineDateKey
    );
  }, [slotSuggestionBaseTime, suggestedAlternativeSlots]);

  const suggestedTopPickMinute = useMemo(
    () => toMinuteEpoch(nearestSlotSuggestion?.suggestedStartTime),
    [nearestSlotSuggestion?.suggestedStartTime]
  );

  useEffect(() => {
    if (!selectedSuggestedSlotStart) return;
    const stillExists = suggestedAlternativeSlots.some((slot) => {
      const slotStart = readField(slot, ["startTime", "StartTime"], null);
      return toLocalDateTimeValue(slotStart) === selectedSuggestedSlotStart;
    });
    if (!stillExists) {
      setSelectedSuggestedSlotStart("");
    }
  }, [selectedSuggestedSlotStart, suggestedAlternativeSlots]);

  const handleApplySuggestedSlot = useCallback((slot) => {
    if (checkInSelectionGuardMessage) {
      setFieldErrors((prev) => ({ ...prev, checkInTime: checkInSelectionGuardMessage }));
      return;
    }

    const slotStart = readField(slot, ["startTime", "StartTime"], null);
    if (!slotStart) return;
    const normalizedStartTime = toLocalDateTimeValue(slotStart);
    if (!normalizedStartTime) return;
    setSelectedSuggestedSlotStart(normalizedStartTime);
    // Chọn khung giờ mới thì trả "Dự kiến hoàn thành" về chờ để tự tính theo khung giờ.
    setFormData((prev) => ({ ...prev, estimatedHandover: "" }));
  }, [checkInSelectionGuardMessage]);

  useEffect(() => {
    if (!selectedSuggestedSlotStart) return;
    if (lastAutoScrolledSlotStartRef.current === selectedSuggestedSlotStart) return;
    lastAutoScrolledSlotStartRef.current = selectedSuggestedSlotStart;

    if (!planSummaries.some(hasPlanCompletionWarning)) return;
    if (typeof window === "undefined" || typeof document === "undefined") return;

    window.requestAnimationFrame(() => {
      const firstWarning = document.querySelector(".reception-plan-summary-warning");
      if (firstWarning && typeof firstWarning.scrollIntoView === "function") {
        firstWarning.scrollIntoView({ behavior: "smooth", block: "center" });
      }
    });
  }, [planSummaries, selectedSuggestedSlotStart]);

  const handlePlateBlur = useCallback((event) => {
    const { name, value } = event.target;
    if (name !== "guestVehiclePlate" && name !== "vehicleLicensePlate") return;
    setFormData((prev) => ({ ...prev, [name]: formatLicensePlateByFieldName(name, value) }));
  }, []);

  const selectCustomer = useCallback(
    (customer) => {
      const customerId = Number(customer.id);
      const linkedVehicleIds = new Set(
        customerVehicles
          .filter((item) => Number(item.customerId) === customerId)
          .map((item) => Number(item.vehicleId))
      );
      const hasSavedVehicles = vehicles.some((vehicle) => linkedVehicleIds.has(Number(vehicle.id)));

      resetPlanningState();
      setIsSavedVehiclePopupOpen(false);
      setSelectedBrandId("");
      setSearch("");

      setFormData((prev) => ({
        ...prev,
        customerId,
        isQuickOrder: false,
        isNewVehicle: !hasSavedVehicles,
        selectedVehicleIds: [],
        guestName: customer.fullName || "",
        guestPhone: customer.phone || "",
        vehicleLicensePlate: "",
        vehicleModelId: "",
        vehicleColor: "",
        vehicleVin: "",
        vehicleYear: "",
      }));
      setError("");
    },
    [customerVehicles, resetPlanningState, vehicles]
  );

  const toggleQuickOrder = useCallback(() => {
    const nextQuickOrder = !formData.isQuickOrder;
    resetPlanningState();
    setIsSavedVehiclePopupOpen(false);
    setSearch("");
    setSelectedBrandId("");
    setGuestSelectedBrandId("");

    setFormData((prev) => ({
      ...prev,
      customerId: "",
      selectedVehicleIds: [],
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: "",
      guestName: "",
      guestPhone: "",
      guestVehiclePlate: "",
      guestVehicleModelId: "",
      guestVehicleColor: "",
      guestVehicleVin: "",
      guestVehicleYear: "",
      isQuickOrder: nextQuickOrder,
      isNewVehicle: false,
    }));
    setError("");
  }, [formData.isQuickOrder, resetPlanningState]);

  const toggleVehicleSelection = useCallback((vehicleId) => {
    const numericId = Number(vehicleId);
    if (!Number.isFinite(numericId) || numericId <= 0) return;
    setFormData((prev) => {
      const selected = new Set(toNumberList(prev.selectedVehicleIds));
      if (selected.has(numericId)) selected.delete(numericId);
      else selected.add(numericId);
      return { ...prev, selectedVehicleIds: Array.from(selected) };
    });
    setError("");
  }, []);

  const addExistingVehicle = useCallback((vehicleId) => {
    const numericId = Number(vehicleId);
    if (!Number.isFinite(numericId) || numericId <= 0) return;
    setFormData((prev) => {
      const selected = new Set(toNumberList(prev.selectedVehicleIds));
      selected.add(numericId);
      return { ...prev, selectedVehicleIds: Array.from(selected) };
    });
    setError("");
  }, []);

  const switchToSavedVehicleMode = useCallback(() => {
    if (customerHasNoVehicles) return;
    resetPlanningState();
    setIsSavedVehiclePopupOpen(false);
    setFormData((prev) => ({
      ...prev,
      isNewVehicle: false,
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: "",
    }));
    setSelectedBrandId("");
    setError("");
  }, [customerHasNoVehicles, resetPlanningState]);

  const switchToNewVehicleMode = useCallback(() => {
    resetPlanningState();
    setIsSavedVehiclePopupOpen(false);
    setFormData((prev) => ({
      ...prev,
      isNewVehicle: true,
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: "",
    }));
    setSelectedBrandId("");
    setError("");
  }, [resetPlanningState]);

  const confirmNewVehicleMode = useCallback(() => {
    setError("");
  }, []);

  const cancelNewVehicleMode = useCallback(() => {
    switchToSavedVehicleMode();
  }, [switchToSavedVehicleMode]);

  const handleServiceModeChange = useCallback(
    (nextMode) => {
      if (nextMode === SERVICE_MODE.CUSTOM && serviceMode !== SERVICE_MODE.CUSTOM) {
        setVehiclePlans((prev) => {
          const next = { ...prev };
          selectedVehicleKeys.forEach((key) => {
            const existing = normalizePlan(prev[key] || EMPTY_PLAN);
            next[key] = existing;
          });
          return next;
        });
      }
      setServiceMode(nextMode);
      setError("");
    },
    [selectedVehicleKeys, serviceMode]
  );

  const applyPlanUpdate = useCallback(
    (updater) => {
      if (effectiveServiceMode === SERVICE_MODE.SHARED) {
        setSharedPlan((prev) => normalizePlan(updater(normalizePlan(prev))));
        return;
      }
      if (!activePlanKey) return;
      setVehiclePlans((prev) => {
        const currentRaw = normalizePlan(prev[activePlanKey] || EMPTY_PLAN);
        const currentEffective = resolveEffectivePlan(currentRaw, sharedPlan);
        const nextEffective = normalizePlan(updater(currentEffective));
        return {
          ...prev,
          [activePlanKey]: deriveRawVehiclePlanFromEffective(nextEffective, sharedPlan),
        };
      });
    },
    [activePlanKey, deriveRawVehiclePlanFromEffective, effectiveServiceMode, resolveEffectivePlan, sharedPlan]
  );

  const toggleCategory = useCallback(
    (categoryId) => {
      const id = Number(categoryId);
      if (!Number.isFinite(id) || id <= 0) return;
      setError("");

      const isDifferentCategory = (serviceId) => Number(serviceCategoryMap.get(serviceId)) !== id;

      if (effectiveServiceMode === SERVICE_MODE.SHARED) {
        const has = (sharedPlan.categoryIds || []).includes(id);
        setSharedPlan((prev) => {
          const normalized = normalizePlan(prev);
          const nextCategoryIds = has
            ? normalized.categoryIds.filter((item) => Number(item) !== id)
            : toNumberList([...(normalized.categoryIds || []), id]);
          const nextServiceIds = has
            ? (normalized.serviceIds || []).filter(isDifferentCategory)
            : normalized.serviceIds;
          return normalizePlan({
            ...normalized,
            categoryIds: nextCategoryIds,
            serviceIds: nextServiceIds,
          });
        });

        if (has) {
          setVehiclePlans((prev) => {
            const next = { ...prev };
            selectedVehicleKeys.forEach((vehicleKey) => {
              const vehiclePlan = normalizePlan(prev[vehicleKey] || EMPTY_PLAN);
              next[vehicleKey] = normalizePlan({
                ...vehiclePlan,
                categoryIds: (vehiclePlan.categoryIds || []).filter((item) => Number(item) !== id),
                serviceIds: (vehiclePlan.serviceIds || []).filter(isDifferentCategory),
                excludedSharedCategoryIds: (vehiclePlan.excludedSharedCategoryIds || []).filter(
                  (item) => Number(item) !== id
                ),
                excludedSharedServiceIds: (vehiclePlan.excludedSharedServiceIds || []).filter(isDifferentCategory),
              });
            });
            return next;
          });
        }
        return;
      }

      if ((sharedPlan.categoryIds || []).includes(id)) {
        setError('Loại dịch vụ dùng chung chỉ có thể hủy ở tab "Dùng chung tất cả xe".');
        return;
      }

      applyPlanUpdate((plan) => {
        const categorySet = new Set(plan.categoryIds || []);
        if (categorySet.has(id)) categorySet.delete(id);
        else categorySet.add(id);

        const nextCategoryIds = Array.from(categorySet);
        const nextServiceIds = (plan.serviceIds || []).filter((serviceId) =>
          nextCategoryIds.includes(Number(serviceCategoryMap.get(serviceId)))
        );

        return {
          categoryIds: nextCategoryIds,
          serviceIds: nextServiceIds,
        };
      });
    },
    [applyPlanUpdate, effectiveServiceMode, selectedVehicleKeys, serviceCategoryMap, sharedPlan.categoryIds]
  );

  const toggleService = useCallback(
    (serviceId) => {
      const numericId = Number(serviceId);
      const categoryId = Number(serviceCategoryMap.get(numericId) || 0);
      if (!numericId || !categoryId) return;
      setError("");
      let shouldCollapseAfterSelect = false;

      if (effectiveServiceMode === SERVICE_MODE.SHARED) {
        const has = (sharedPlan.serviceIds || []).includes(numericId);
        if (has) {
          setSharedPlan((prev) => {
            const normalized = normalizePlan(prev);
            return normalizePlan({
              ...normalized,
              serviceIds: (normalized.serviceIds || []).filter((item) => Number(item) !== numericId),
            });
          });
        } else {
          const oppositeSharedIds = getConflictingInspectionServiceIds(sharedPlan.serviceIds, numericId);
          setSharedPlan((prev) => {
            const normalized = normalizePlan(prev);
            const nextServiceIds = toNumberList([
              ...(normalized.serviceIds || []).filter((item) => !oppositeSharedIds.includes(Number(item))),
              numericId,
            ]);
            const nextCategoryIds = toNumberList([...(normalized.categoryIds || []), categoryId]);
            return normalizePlan({
              ...normalized,
              categoryIds: nextCategoryIds,
              serviceIds: nextServiceIds,
            });
          });

          setVehiclePlans((prev) => {
            const next = { ...prev };
            let hasAnyPatch = false;
            selectedVehicleKeys.forEach((vehicleKey) => {
              const rawVehiclePlan = normalizePlan(prev[vehicleKey] || EMPTY_PLAN);
              const effectiveVehiclePlan = resolveEffectivePlan(rawVehiclePlan, sharedPlan);
              const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveVehiclePlan.serviceIds, numericId);
              if (oppositeServiceIds.length === 0) return;

              const nextVehicleServiceIds = (rawVehiclePlan.serviceIds || []).filter(
                (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
              );
              const nextExcludedSharedServiceIds = (rawVehiclePlan.excludedSharedServiceIds || []).filter(
                (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
              );

              const shouldPatchServices = nextVehicleServiceIds.length !== (rawVehiclePlan.serviceIds || []).length;
              const shouldPatchExcluded =
                nextExcludedSharedServiceIds.length !== (rawVehiclePlan.excludedSharedServiceIds || []).length;
              if (!shouldPatchServices && !shouldPatchExcluded) return;

              next[vehicleKey] = normalizePlan({
                ...rawVehiclePlan,
                serviceIds: nextVehicleServiceIds,
                excludedSharedServiceIds: nextExcludedSharedServiceIds,
              });
              hasAnyPatch = true;
            });
            return hasAnyPatch ? next : prev;
          });

          shouldCollapseAfterSelect = true;
        }
      } else if (activePlanKey) {
        setVehiclePlans((prev) => {
          const rawVehiclePlan = normalizePlan(prev[activePlanKey] || EMPTY_PLAN);
          const effectiveVehiclePlan = resolveEffectivePlan(rawVehiclePlan, sharedPlan);
          const sharedServiceIds = toNumberList(sharedPlan.serviceIds);
          const sharedServiceSet = new Set(sharedServiceIds);
          const sharedCategorySet = new Set(toNumberList(sharedPlan.categoryIds));
          const nextRawCategorySet = new Set(rawVehiclePlan.categoryIds || []);

          if (!sharedCategorySet.has(categoryId)) {
            nextRawCategorySet.add(categoryId);
          }

          if (sharedServiceSet.has(numericId)) {
            const hasExcluded = (rawVehiclePlan.excludedSharedServiceIds || []).includes(numericId);
            if (!hasExcluded) {
              return {
                ...prev,
                [activePlanKey]: normalizePlan({
                  ...rawVehiclePlan,
                  categoryIds: Array.from(nextRawCategorySet),
                  serviceIds: (rawVehiclePlan.serviceIds || []).filter((item) => Number(item) !== numericId),
                  excludedSharedServiceIds: [...(rawVehiclePlan.excludedSharedServiceIds || []), numericId],
                }),
              };
            }

            const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveVehiclePlan.serviceIds, numericId);
            const nextExcludedSet = new Set(
              (rawVehiclePlan.excludedSharedServiceIds || [])
                .filter((item) => Number(item) !== numericId)
                .map(Number)
            );
            const nextVehicleServiceIds = (rawVehiclePlan.serviceIds || []).filter(
              (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
            );

            oppositeServiceIds
              .filter((serviceItemId) => sharedServiceSet.has(Number(serviceItemId)))
              .forEach((serviceItemId) => nextExcludedSet.add(Number(serviceItemId)));

            shouldCollapseAfterSelect = true;
            return {
              ...prev,
              [activePlanKey]: normalizePlan({
                ...rawVehiclePlan,
                categoryIds: Array.from(nextRawCategorySet),
                serviceIds: nextVehicleServiceIds,
                excludedSharedServiceIds: Array.from(nextExcludedSet),
              }),
            };
          }

          const has = (rawVehiclePlan.serviceIds || []).includes(numericId);
          if (has) {
            return {
              ...prev,
              [activePlanKey]: normalizePlan({
                ...rawVehiclePlan,
                categoryIds: Array.from(nextRawCategorySet),
                serviceIds: (rawVehiclePlan.serviceIds || []).filter((item) => Number(item) !== numericId),
              }),
            };
          }

          const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveVehiclePlan.serviceIds, numericId);
          const nextVehicleServiceIds = toNumberList([
            ...(rawVehiclePlan.serviceIds || []).filter(
              (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
            ),
            numericId,
          ]);
          const nextExcludedSet = new Set((rawVehiclePlan.excludedSharedServiceIds || []).map(Number));
          oppositeServiceIds
            .filter((serviceItemId) => sharedServiceSet.has(Number(serviceItemId)))
            .forEach((serviceItemId) => nextExcludedSet.add(Number(serviceItemId)));

          shouldCollapseAfterSelect = true;
          return {
            ...prev,
            [activePlanKey]: normalizePlan({
              ...rawVehiclePlan,
              categoryIds: Array.from(nextRawCategorySet),
              serviceIds: nextVehicleServiceIds,
              excludedSharedServiceIds: Array.from(nextExcludedSet),
            }),
          };
        });
      }

      if (shouldCollapseAfterSelect) {
        setIsServicePickerCollapsed(true);
      }
      setServiceSearch("");
    },
    [
      activePlanKey,
      effectiveServiceMode,
      getConflictingInspectionServiceIds,
      resolveEffectivePlan,
      selectedVehicleKeys,
      serviceCategoryMap,
      sharedPlan,
    ]
  );

  const handleVehicleNoteChange = useCallback((vehicleKey, index, value) => {
    setVehicleNotesByKey((prev) => {
      const current = Array.isArray(prev?.[vehicleKey]) && prev[vehicleKey].length > 0 ? [...prev[vehicleKey]] : [""];
      current[index] = value;

      const isLast = index === current.length - 1;
      if (isLast && String(value || "").trim() && current.length < 12) current.push("");

      return { ...prev, [vehicleKey]: current };
    });
  }, []);

  const handleSubmit = useCallback(
    async (event) => {
      event.preventDefault();
      if (saving) return;

      setSubmitAttempted(true);
      const errors = validateForm(formData);
      setFieldErrors(errors);
      if (Object.keys(errors).length > 0) {
        setError("");
        scrollToPageTop();
        return;
      }

      setSaving(true);
      setError("");
      const batchPayload = buildWorkshopBatchCreatePayload({
        formData,
        suggestedFleetCompletionIso,
        effectiveBranchId,
        insuranceActive: isInsuranceOrderEnabled,
        insuranceByVehicleKey,
        hasMultipleSelectedVehicles,
        selectedVehicleKeys,
        plansByVehicleKey,
        vehicleNotesByKey,
      });
      const createTargets = Array.isArray(batchPayload.items) ? batchPayload.items : [];

      try {
        const batchResponse = await serviceOrderApi.createWorkshopBatch(batchPayload);
        const createdCount = Number(
          batchResponse?.totalCreated ??
          batchResponse?.TotalCreated ??
          (Array.isArray(batchResponse?.orders) ? batchResponse.orders.length : 0) ??
          (Array.isArray(batchResponse?.Orders) ? batchResponse.Orders.length : 0)
        ) || createTargets.length;
        setSuccessOrderSummary({
          count: createdCount,
          isGroup: createTargets.length > 1,
        });
      } catch (err) {
        const message = getApiErrorMessage(err);
        setError(message);
        scrollToPageTop();
      } finally {
        setSaving(false);
      }
    },
    [
      effectiveBranchId,
      formData,
      getApiErrorMessage,
      hasMultipleSelectedVehicles,
      insuranceByVehicleKey,
      isInsuranceOrderEnabled,
      plansByVehicleKey,
      saving,
      selectedVehicleKeys,
      suggestedFleetCompletionIso,
      scrollToPageTop,
      validateForm,
      vehicleNotesByKey,
    ]
  );

  if (loading) {
    return (
      <div className="loading-state">
        <div className="spinner" />
        <p>Đang tải dữ liệu...</p>
      </div>
    );
  }

  return (
    <div className="create-order-page reception-create-appointment-page">
      <div className="page-header-actions reception-create-header">
        <button className="btn-back" onClick={() => navigate(-1)}>
          Quay lại Dashboard
        </button>
        <div className="reception-create-header-copy">
          <h2 className="page-title">Tiếp nhận xe và tạo Service Order mới</h2>
          <p className="reception-create-header-note">
            Hỗ trợ tiếp nhận xe đơn hoặc nhóm xe, cho phép cấu hình dịch vụ dùng chung hoặc tùy chỉnh riêng theo từng xe.
          </p>
        </div>
      </div>
      {error ? (
        <div className="reception-error-banner" role="alert">
          {error}
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

      <form className="admin-form reception-appt-form" onSubmit={handleSubmit}>
        <div className="reception-appt-layout">
          <div className="reception-appt-left-column">
            <section className="reception-appt-section">
              <div className="reception-appt-step-head">
                <span className="reception-step-badge">BƯỚC 1</span>
                <label className="reception-appt-quick-toggle" htmlFor="service-order-quick-toggle">
                  <input
                    id="service-order-quick-toggle"
                    type="checkbox"
                    checked={formData.isQuickOrder}
                    onChange={toggleQuickOrder}
                  />
                  <span>Tiếp nhận nhanh khách vãng lai</span>
                </label>
              </div>
              <h4 className="reception-appt-section-title">{formData.isQuickOrder ? "Khách vãng lai" : "Khách hàng và xe"}</h4>

              {!formData.isQuickOrder ? (
                <>
                  <div className="form-group">
                    <label>Tìm khách hàng *</label>
                    <div className="search-input-wrapper">
                      <input
                        type="text"
                        className={showError("customerId") ? "input-invalid" : ""}
                        placeholder="Nhập tên hoặc số điện thoại..."
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                        required={!formData.customerId}
                      />
                      {filteredCustomers.length > 0 ? (
                        <div className="dropdown-panel" style={{ position: "absolute", top: "100%", left: 0, right: 0, zIndex: 1200 }}>
                          {filteredCustomers.map((customer) => (
                            <div key={customer.id} className="dropdown-item" onClick={() => selectCustomer(customer)}>
                              <strong>{customer.fullName}</strong> - {customer.phone}
                            </div>
                          ))}
                        </div>
                      ) : null}
                    </div>
                    {showError("customerId") ? <div className="field-error">{fieldErrors.customerId}</div> : null}
                  </div>

                  {formData.customerId ? (
                    <div className="reception-customer-selected-card">
                      <div className="reception-customer-selected-main">
                        <small>KHÁCH ĐÃ CHỌN</small>
                        <strong>{formData.guestName || "Chưa có tên"}</strong>
                        <span>{formData.guestPhone || "Chưa có số điện thoại"}</span>
                      </div>
                    </div>
                  ) : null}

                  {!formData.isNewVehicle ? (
                    <div className="form-group">
                      <label>Xe của khách (có thể chọn nhiều xe) *</label>
                      {!formData.customerId ? (
                        <div className="reception-muted-note">Hãy chọn khách hàng trước.</div>
                      ) : (
                        <>
                          <div className="reception-vehicle-popup-toolbar">
                            <button
                              type="button"
                              className="reception-open-vehicle-popup-btn"
                              onClick={() => setIsSavedVehiclePopupOpen(true)}
                              disabled={customerVehiclesFiltered.length === 0}
                            >
                              Chọn xe đã lưu
                            </button>
                            <button type="button" className="reception-open-vehicle-popup-btn" onClick={switchToNewVehicleMode}>
                              Thêm xe mới
                            </button>
                          </div>

                          {customerVehiclesFiltered.length === 0 ? (
                            <div className="reception-muted-note">Khách hàng chưa có xe đã lưu.</div>
                          ) : (
                            <>
                              {selectedCustomerVehicles.length > 0 ? (
                                <div className="reception-selected-vehicle-list">
                                {selectedCustomerVehicles.map((vehicle) => {
                                  const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
                                  return (
                                    <div
                                      key={`selected-vehicle-${vehicle.id}`}
                                      className="reception-selected-vehicle-chip"
                                    >
                                      <span className="reception-selected-vehicle-chip-main">
                                        <strong>{plate}</strong>
                                        <small>{describeSavedVehicle(vehicle)}</small>
                                      </span>
                                      <button
                                        type="button"
                                        className="reception-selected-vehicle-chip-remove"
                                        onClick={() => toggleVehicleSelection(vehicle.id)}
                                      >
                                        Xóa
                                      </button>
                                    </div>
                                  );
                                })}
                              </div>
                              ) : null}

                              {isSavedVehiclePopupOpen ? (
                                <div className="reception-vehicle-popup-backdrop" onClick={() => setIsSavedVehiclePopupOpen(false)}>
                                  <div
                                    className="reception-vehicle-popup-card"
                                    role="dialog"
                                    aria-modal="true"
                                    aria-label="Chọn xe đã lưu"
                                    onClick={(event) => event.stopPropagation()}
                                  >
                                    <div className="reception-vehicle-popup-head">
                                      <div className="reception-vehicle-popup-head-copy">
                                        <h5>Chọn xe đã lưu</h5>
                                        <p>Chỉ hiển thị các xe chưa chọn.</p>
                                      </div>
                                      <button
                                        type="button"
                                        className="reception-vehicle-popup-close-btn"
                                        onClick={() => setIsSavedVehiclePopupOpen(false)}
                                      >
                                        Đóng
                                      </button>
                                    </div>

                                    <div className="reception-vehicle-popup-list">
                                      {selectableCustomerVehicles.length === 0 ? (
                                        <div className="reception-muted-note">
                                          Bạn đã chọn hết xe đã lưu. Bỏ xe ở danh sách đã chọn để chọn lại.
                                        </div>
                                      ) : (
                                        selectableCustomerVehicles.map((vehicle) => {
                                          const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
                                          return (
                                            <button
                                              key={`popup-vehicle-${vehicle.id}`}
                                              type="button"
                                              className="reception-vehicle-popup-item"
                                              onClick={() => addExistingVehicle(vehicle.id)}
                                            >
                                              <span className="reception-vehicle-popup-item-mark" aria-hidden="true">
                                                +
                                              </span>
                                              <span className="reception-vehicle-popup-item-copy">
                                                <strong>{plate}</strong>
                                                <small>{describeSavedVehicle(vehicle)}</small>
                                              </span>
                                                <span className="reception-vehicle-popup-item-action">Thêm xe</span>
                                            </button>
                                          );
                                        })
                                      )}
                                    </div>
                                  </div>
                                </div>
                              ) : null}
                            </>
                          )}
                        </>
                      )}
                      {showError("selectedVehicleIds") ? <div className="field-error">{fieldErrors.selectedVehicleIds}</div> : null}
                    </div>
                  ) : (
                    <div className="reception-new-vehicle-box">
                      <div className="reception-vehicle-popup-toolbar">
                        <button
                          type="button"
                          className="reception-open-vehicle-popup-btn"
                          onClick={switchToSavedVehicleMode}
                          disabled={customerHasNoVehicles}
                        >
                          Chọn xe đã lưu
                        </button>
                        <button type="button" className="reception-open-vehicle-popup-btn is-active" onClick={switchToNewVehicleMode}>
                          Thêm xe mới
                        </button>
                      </div>
                      {customerHasNoVehicles ? (
                        <div className="reception-muted-note">Khách hàng này chưa có xe, cần nhập xe mới để tiếp nhận.</div>
                      ) : null}
                      <div className="form-group">
                        <label>Biển số xe mới *</label>
                        <input
                          type="text"
                          name="vehicleLicensePlate"
                          className={showError("vehicleLicensePlate") ? "input-invalid" : ""}
                          value={formData.vehicleLicensePlate}
                          onChange={handleChange}
                          onBlur={handlePlateBlur}
                          pattern={VIETNAM_LICENSE_PLATE_PATTERN}
                          title={VIETNAM_LICENSE_PLATE_TITLE}
                          style={{ textTransform: "uppercase" }}
                        />
                        {showError("vehicleLicensePlate") ? <div className="field-error">{fieldErrors.vehicleLicensePlate}</div> : null}
                      </div>
                      <div className="reception-grid reception-grid-2">
                        <div className="form-group">
                          <label>Hãng xe *</label>
                          <select
                            className={showError("brandId") ? "input-invalid" : ""}
                            value={selectedBrandId}
                            onChange={(event) => {
                              setSelectedBrandId(event.target.value);
                              handleFieldValueChange("vehicleModelId", "");
                            }}
                          >
                            <option value="">-- Chọn hãng --</option>
                            {brands.map((brand) => (
                              <option key={brand.id} value={brand.id}>
                                {brand.name}
                              </option>
                            ))}
                          </select>
                          {showError("brandId") ? <div className="field-error">{fieldErrors.brandId}</div> : null}
                        </div>
                        <div className="form-group">
                          <label>Dòng xe *</label>
                          <select
                            name="vehicleModelId"
                            className={showError("vehicleModelId") ? "input-invalid" : ""}
                            value={formData.vehicleModelId}
                            onChange={handleChange}
                            disabled={!selectedBrandId}
                          >
                            <option value="">-- Chọn dòng xe --</option>
                            {models
                              .filter((model) => Number(model.brandId) === Number(selectedBrandId))
                              .map((model) => (
                                <option key={model.id} value={model.id}>
                                  {model.modelName}
                                </option>
                              ))}
                          </select>
                          {showError("vehicleModelId") ? <div className="field-error">{fieldErrors.vehicleModelId}</div> : null}
                        </div>
                      </div>
                      <div className="reception-grid reception-grid-3">
                        <div className="form-group">
                          <label>Màu sắc (tùy chọn)</label>
                          <input name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} />
                        </div>
                        <div className="form-group">
                          <label>Năm sản xuất *</label>
                          <select
                            name="vehicleYear"
                            className={showError("vehicleYear") ? "input-invalid" : ""}
                            value={formData.vehicleYear}
                            onChange={handleChange}
                          >
                            <option value="">-- Chọn năm --</option>
                            {years.map((year) => (
                              <option key={year} value={year}>
                                {year}
                              </option>
                            ))}
                          </select>
                          {showError("vehicleYear") ? <div className="field-error">{fieldErrors.vehicleYear}</div> : null}
                        </div>
                        <div className="form-group">
                          <label>VIN (tùy chọn)</label>
                          <input
                            name="vehicleVin"
                            className={showError("vehicleVin") ? "input-invalid" : ""}
                            value={formData.vehicleVin}
                            onChange={handleChange}
                            pattern={VIETNAM_VIN_PATTERN}
                            title={VIETNAM_VIN_TITLE}
                            maxLength={17}
                            style={{ textTransform: "uppercase" }}
                          />
                          {showError("vehicleVin") ? <div className="field-error">{fieldErrors.vehicleVin}</div> : null}
                        </div>
                        </div>
                      <div className="reception-new-vehicle-actions">
                        <button
                          type="button"
                          className="reception-open-vehicle-popup-btn is-active"
                          onClick={confirmNewVehicleMode}
                        >
                          Xác nhận
                        </button>
                        <button type="button" className="reception-open-vehicle-popup-btn" onClick={cancelNewVehicleMode}>
                          Hủy
                        </button>
                      </div>
                    </div>
                  )}
                </>
              ) : (
                <>
                  <div className="reception-grid reception-grid-2">
                    <div className="form-group">
                      <label>Họ tên khách *</label>
                      <input
                        name="guestName"
                        className={showError("guestName") ? "input-invalid" : ""}
                        value={formData.guestName}
                        onChange={handleChange}
                      />
                      {showError("guestName") ? <div className="field-error">{fieldErrors.guestName}</div> : null}
                    </div>
                    <div className="form-group">
                      <label>Số điện thoại *</label>
                  <input
                    type="tel"
                    inputMode="tel"
                    name="guestPhone"
                    className={showError("guestPhone") ? "input-invalid" : ""}
                    value={formData.guestPhone}
                    onChange={handleChange}
                    pattern={VN_PHONE_PATTERN}
                    title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
                  />
                      {showError("guestPhone") ? <div className="field-error">{fieldErrors.guestPhone}</div> : null}
                    </div>
                  </div>

                  <div className="form-group">
                    <label>Biển số xe *</label>
                    <input
                      name="guestVehiclePlate"
                      className={showError("guestVehiclePlate") ? "input-invalid" : ""}
                      value={formData.guestVehiclePlate}
                      onChange={handleChange}
                      onBlur={handlePlateBlur}
                      pattern={VIETNAM_LICENSE_PLATE_PATTERN}
                      title={VIETNAM_LICENSE_PLATE_TITLE}
                      style={{ textTransform: "uppercase" }}
                    />
                    {showError("guestVehiclePlate") ? <div className="field-error">{fieldErrors.guestVehiclePlate}</div> : null}
                  </div>

                  <div className="reception-grid reception-grid-2">
                    <div className="form-group">
                      <label>Hãng xe *</label>
                      <select
                        className={showError("guestBrandId") ? "input-invalid" : ""}
                        value={guestSelectedBrandId}
                        onChange={(event) => {
                          setGuestSelectedBrandId(event.target.value);
                          handleFieldValueChange("guestVehicleModelId", "");
                        }}
                      >
                        <option value="">-- Chọn hãng --</option>
                        {brands.map((brand) => (
                          <option key={brand.id} value={brand.id}>
                            {brand.name}
                          </option>
                        ))}
                      </select>
                      {showError("guestBrandId") ? <div className="field-error">{fieldErrors.guestBrandId}</div> : null}
                    </div>
                    <div className="form-group">
                      <label>Dòng xe *</label>
                      <select
                        name="guestVehicleModelId"
                        className={showError("guestVehicleModelId") ? "input-invalid" : ""}
                        value={formData.guestVehicleModelId}
                        onChange={handleChange}
                        disabled={!guestSelectedBrandId}
                      >
                        <option value="">-- Chọn dòng xe --</option>
                        {models
                          .filter((model) => Number(model.brandId) === Number(guestSelectedBrandId))
                          .map((model) => (
                            <option key={model.id} value={model.id}>
                              {model.modelName}
                            </option>
                          ))}
                      </select>
                      {showError("guestVehicleModelId") ? <div className="field-error">{fieldErrors.guestVehicleModelId}</div> : null}
                    </div>
                  </div>
                    <div className="reception-grid reception-grid-3">
                      <div className="form-group">
                        <label>Màu sắc (tùy chọn)</label>
                        <input name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} />
                      </div>
                      <div className="form-group">
                        <label>Năm sản xuất *</label>
                        <select
                          name="guestVehicleYear"
                          className={showError("guestVehicleYear") ? "input-invalid" : ""}
                          value={formData.guestVehicleYear}
                          onChange={handleChange}
                        >
                          <option value="">-- Chọn năm --</option>
                          {years.map((year) => (
                            <option key={year} value={year}>
                              {year}
                            </option>
                          ))}
                        </select>
                        {showError("guestVehicleYear") ? <div className="field-error">{fieldErrors.guestVehicleYear}</div> : null}
                      </div>
                      <div className="form-group">
                        <label>VIN (tùy chọn)</label>
                        <input
                          name="guestVehicleVin"
                          className={showError("guestVehicleVin") ? "input-invalid" : ""}
                          value={formData.guestVehicleVin}
                          onChange={handleChange}
                          pattern={VIETNAM_VIN_PATTERN}
                          title={VIETNAM_VIN_TITLE}
                          maxLength={17}
                          style={{ textTransform: "uppercase" }}
                        />
                        {showError("guestVehicleVin") ? <div className="field-error">{fieldErrors.guestVehicleVin}</div> : null}
                      </div>
                    </div>
                </>
              )}
            </section>

            <section className="reception-appt-section">
              <div className="reception-appt-step-head">
                <span className="reception-step-badge">BƯỚC 2</span>
              </div>
              <h4 className="reception-appt-section-title reception-appt-section-title-service">Loại dịch vụ và dịch vụ cụ thể</h4>

              {showServiceModeSwitch ? (
                <div className="reception-scope-switch">
                  <button
                    type="button"
                    className={effectiveServiceMode === SERVICE_MODE.SHARED ? "active" : ""}
                    onClick={() => handleServiceModeChange(SERVICE_MODE.SHARED)}
                  >
                    Dùng chung tất cả xe
                  </button>
                  <button
                    type="button"
                    className={effectiveServiceMode === SERVICE_MODE.CUSTOM ? "active" : ""}
                    onClick={() => handleServiceModeChange(SERVICE_MODE.CUSTOM)}
                    disabled={!hasMultipleSelectedVehicles}
                    title={!hasMultipleSelectedVehicles ? "Cần chọn từ 2 xe trở lên để tùy chỉnh riêng" : undefined}
                  >
                    Tùy chỉnh theo từng xe
                  </button>
                </div>
              ) : null}

              {selectedVehicleKeys.length > 1 ? (
                <div className="reception-selected-tabs reception-vehicle-tabs-layout">
                  {selectedVehicleKeys.map((vehicleKey) => (
                    <div
                      key={vehicleKey}
                      className={`reception-selected-tab ${
                        effectiveServiceMode === SERVICE_MODE.CUSTOM && activePlanVehicleKey === vehicleKey ? "active" : ""
                      }`}
                    >
                      <button
                        type="button"
                        className="reception-selected-tab-main"
                        onClick={() => {
                          setActivePlanVehicleKey(vehicleKey);
                          if (effectiveServiceMode === SERVICE_MODE.SHARED) {
                            handleServiceModeChange(SERVICE_MODE.CUSTOM);
                          }
                        }}
                      >
                        {toVehicleTabLabel(vehicleLabelByKey.get(vehicleKey), vehicleKey)}
                      </button>
                      <button
                        type="button"
                        className="reception-selected-tab-remove"
                        aria-label={`Bỏ ${vehicleLabelByKey.get(vehicleKey) || vehicleKey}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          const vehicleId = getVehicleIdFromKey(vehicleKey);
                          if (vehicleId > 0) {
                            toggleVehicleSelection(vehicleId);
                          }
                        }}
                      >
                        x
                      </button>
                    </div>
                  ))}
                </div>
              ) : null}

              <div className="reception-service-subsection reception-service-type-subsection">
                <h5 className="reception-service-subtitle">Loại dịch vụ</h5>
                <button
                  type="button"
                  className={`reception-section-collapse-btn reception-section-collapse-btn-corner ${
                    isServicePickerCollapsed ? "is-collapsed" : "is-expanded"
                  }`}
                  aria-expanded={!isServicePickerCollapsed}
                  aria-label={isServicePickerCollapsed ? "Mở phần loại dịch vụ" : "Thu gọn phần loại dịch vụ"}
                  onClick={() => setIsServicePickerCollapsed((prev) => !prev)}
                >
                  <span className="reception-section-collapse-glyph" aria-hidden="true" />
                </button>
                {!isServicePickerCollapsed ? (
                  <div className="reception-category-grid">
                    {visibleCategoryOptions.map((category) => {
                      const selected = activePlan.categoryIds.includes(Number(category.id));
                      return (
                        <button
                          key={category.id}
                          type="button"
                          className={`reception-category-chip ${selected ? "selected" : ""}`}
                          onClick={() => toggleCategory(category.id)}
                        >
                          {category.name}
                        </button>
                      );
                    })}
                  </div>
                ) : (
                  <div className="reception-service-collapsed-card">
                    <span className="reception-service-collapsed-label">Loại dịch vụ</span>
                    <span className="reception-service-collapsed-value">
                      {activePlan.categoryIds.length > 0
                        ? activePlan.categoryIds.map((categoryId) => categoryNameById.get(categoryId) || `Nhóm ${categoryId}`).join(", ")
                        : "Chưa chọn loại dịch vụ"}
                    </span>
                  </div>
                )}
              </div>

              <div className="reception-service-subsection reception-service-detail-subsection">
                <h5 className="reception-service-subtitle">Dịch vụ cụ thể</h5>
                {activePlan.categoryIds.length > 0 ? (
                  <>
                    <div className="reception-selected-tabs reception-category-tabs-layout">
                      {activePlan.categoryIds.map((categoryId) => (
                        <div
                          key={`cat-${categoryId}`}
                          className={`reception-selected-tab ${String(activeCategoryId) === String(categoryId) ? "active" : ""}`}
                        >
                          <button
                            type="button"
                            className="reception-selected-tab-main"
                            onClick={() => setActiveCategoryId(String(categoryId))}
                          >
                            {categoryNameById.get(categoryId) || `Nhóm ${categoryId}`}
                          </button>
                          <button
                            type="button"
                            className="reception-selected-tab-remove"
                            aria-label={`Bỏ ${categoryNameById.get(categoryId) || `Nhóm ${categoryId}`}`}
                            onClick={(event) => {
                              event.stopPropagation();
                              toggleCategory(categoryId);
                            }}
                          >
                            x
                          </button>
                        </div>
                      ))}
                    </div>

                    <div className="form-group">
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
                        <div className="reception-muted-note">Không tìm thấy dịch vụ phù hợp trong nhóm đã chọn.</div>
                      ) : (
                        filteredServices.map((service) => {
                          const selected = activePlan.serviceIds.includes(service.id);
                          return (
                            <button
                              key={service.id}
                              type="button"
                              className={`reception-service-card ${selected ? "selected" : ""}`}
                              onClick={() => toggleService(service.id)}
                            >
                              <div className="reception-service-card-main">
                                <strong>{service.name}</strong>
                                <small>{service.minutes > 0 ? `${service.minutes} phút` : "Chưa có thời lượng"}</small>
                              </div>
                              <span>{formatCurrency(service.price)}</span>
                            </button>
                          );
                        })
                      )}
                    </div>
                  </>
                ) : (
                  <div className="reception-muted-note">Hãy chọn ít nhất một loại dịch vụ trước khi chọn dịch vụ cụ thể.</div>
                )}
              </div>

              {showError("serviceIds") ? <div className="field-error">{fieldErrors.serviceIds}</div> : null}

              <WorkshopInsurancePanel
                enabled={isInsuranceOrderEnabled}
                locked={hasInsuranceSignalServiceSelected}
                insuranceVehicleOptions={insuranceVehicleOptions.map((vehicle) => ({
                  key: vehicle.key,
                  label: toVehicleTabLabel(vehicle.label, vehicle.key),
                }))}
                activeInsuranceVehicleKey={effectiveActiveInsuranceVehicleKey}
                onInsuranceVehicleChange={setActiveInsuranceVehicleKey}
                companies={insuranceCompanies}
                selectedCompanyId={activeInsuranceSnapshot.insuranceCompanyId}
                onToggle={handleInsuranceToggle}
                onCompanyChange={handleInsuranceCompanyChange}
                companyError={showError("insuranceCompanyId") ? fieldErrors.insuranceCompanyId : ""}
                insuranceDeductibleOption={activeInsuranceSnapshot.insuranceDeductibleOption}
                insuranceClaimMode={activeInsuranceSnapshot.insuranceClaimMode}
                insuranceQuoteState={activeInsuranceSnapshot.insuranceQuoteState}
                onDeductibleOptionChange={handleInsuranceDeductibleOptionChange}
                onClaimModeChange={handleInsuranceClaimModeChange}
                deductibleError={showError("insuranceDeductibleOption") ? fieldErrors.insuranceDeductibleOption : ""}
                claimModeError={showError("insuranceClaimMode") ? fieldErrors.insuranceClaimMode : ""}
                draftBreakdown={insuranceDraftBreakdown}
                insuranceCardImageUrls={activeInsuranceSnapshot.insuranceCardImageUrls}
                insuranceDamageImageUrls={activeInsuranceSnapshot.insuranceDamageImageUrls}
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
              <h4 className="reception-appt-section-title">Thông tin nghiệp vụ Service Order</h4>

              <div className="form-group">
                <label>Cố vấn dịch vụ *</label>
                <select
                  name="serviceAdvisorId"
                  className={showError("serviceAdvisorId") ? "input-invalid" : ""}
                  value={formData.serviceAdvisorId}
                  onChange={handleChange}
                >
                  <option value="">-- Chọn cố vấn --</option>
                  {staff.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.fullName}
                    </option>
                  ))}
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
                <label>Số tiền cọc (VND)</label>
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

              <div className="timeline-grid timeline-grid-vertical">
                <div className="form-group">
                  <label>Thời điểm xe vào xưởng *</label>
                  <DateTimePicker
                    value={formData.checkInTime}
                    onChange={(value) => handleFieldValueChange("checkInTime", value)}
                    placeholder="Chọn ngày và giờ..."
                    minDate={checkInDayStart}
                    maxDate={checkInDayEnd}
                    availableTimeMap={checkInAvailableTimeMap}
                    onOpenRequest={() => {
                      if (checkInSelectionGuardMessage) {
                        setFieldErrors((prev) => ({ ...prev, checkInTime: checkInSelectionGuardMessage }));
                        return false;
                      }
                      return true;
                    }}
                    autoSelectNowOnOpen
                  />
                  {fieldErrors.checkInTime ? <div className="field-error">{fieldErrors.checkInTime}</div> : null}
                  {hasCheckInInput && suggestedAlternativeSlots.length > 0 ? (
                    <div className="reception-slot-picker">
                      <p className={`slot-flow-note ${effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE ? "checkin" : "repair"}`}>
                        {slotSelectionGuide}
                      </p>
                      <div className="reception-muted-note" style={{ marginTop: 0 }}>
                        Chọn 1 trong 5 khung giờ khả dụng:
                      </div>
                      {!hasSameDaySuggestedSlots ? (
                        <div className="reception-muted-note" style={{ marginTop: 6 }}>
                          Hôm nay không còn khung giờ phù hợp, đang hiển thị các khung giờ khả dụng tiếp theo.
                        </div>
                      ) : null}
                      <div className="reception-slot-grid" aria-busy={nearestSlotLoading ? "true" : "false"}>
                        {suggestedAlternativeSlots.map((slot) => {
                          const slotStart = readField(slot, ["startTime", "StartTime"], null);
                          const slotEnd = readField(slot, ["endTime", "EndTime"], null);
                          const normalizedSlotStart = toLocalDateTimeValue(slotStart);
                          const slotRemaining = Number(readField(slot, ["remainingCapacity", "RemainingCapacity"], 0));
                          const slotMinute = toMinuteEpoch(slotStart);
                          const isSelected = Boolean(normalizedSlotStart && selectedSuggestedSlotStart && normalizedSlotStart === selectedSuggestedSlotStart);
                          const isTopPick =
                            suggestedTopPickMinute !== null &&
                            slotMinute !== null &&
                            slotMinute === suggestedTopPickMinute;
                          const hasCapacity = Number.isFinite(slotRemaining) ? slotRemaining > 0 : true;
                          const slotKey = `${slotStart || "na"}-${slotEnd || "na"}`;

                          return (
                            <button
                              key={slotKey}
                              type="button"
                              className={`reception-slot-card ${isSelected ? "selected" : ""} ${isTopPick ? "top-pick" : ""}`.trim()}
                              onClick={() => handleApplySuggestedSlot(slot)}
                            >
                              <strong className="reception-slot-time">
                                {formatSlotTime(slotStart)} - {formatSlotTime(slotEnd)}
                              </strong>
                              <div className="reception-slot-meta">
                                <span className={`reception-slot-state-dot ${hasCapacity ? "ok" : "full"}`} />
                                <span className={`reception-slot-state-text ${hasCapacity ? "ok" : "full"}`}>
                                  {hasCapacity ? `Còn ${Math.max(0, slotRemaining)} chỗ` : "Đã đầy"}
                                </span>
                              </div>
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ) : null}
                </div>
                <div className="form-group">
                  <label>Ngày giờ cam kết trả xe</label>
                  <DateTimePicker
                    value={formData.promisedHandoverTime}
                    onChange={(value) => handleFieldValueChange("promisedHandoverTime", value)}
                  />
                  {showError("promisedHandoverTime") ? <div className="field-error">{fieldErrors.promisedHandoverTime}</div> : null}
                </div>
                <div className="form-group">
                  <label>Dự kiến hoàn thành</label>
                  <DateTimePicker
                    value={formData.estimatedHandover || suggestedFleetCompletionIso}
                    onChange={(value) => handleFieldValueChange("estimatedHandover", value)}
                  />
                  {showError("estimatedHandover") ? <div className="field-error">{fieldErrors.estimatedHandover}</div> : null}
                </div>
              </div>
              <div className="form-group" style={{ marginTop: 14 }}>
                <label>Mô tả thêm theo từng xe</label>
                {selectedVehicleKeys.length > 0 ? (
                  <div className="reception-note-editor">
                    {selectedVehicleKeys.length > 1 ? (
                      <div className="reception-selected-tabs reception-vehicle-tabs-layout reception-note-vehicle-tabs">
                        {selectedVehicleKeys.map((vehicleKey) => (
                          <div
                            key={`note-tab-${vehicleKey}`}
                            className={`reception-selected-tab ${activeNoteKey === vehicleKey ? "active" : ""}`}
                          >
                            <button
                              type="button"
                              className="reception-selected-tab-main"
                              onClick={() => setActiveNoteVehicleKey(vehicleKey)}
                            >
                              {toVehicleTabLabel(vehicleLabelByKey.get(vehicleKey), vehicleKey)}
                            </button>
                            <button
                              type="button"
                              className="reception-selected-tab-remove"
                              aria-label={`Bỏ ${vehicleLabelByKey.get(vehicleKey) || vehicleKey}`}
                              onClick={(event) => {
                                event.stopPropagation();
                                const vehicleId = getVehicleIdFromKey(vehicleKey);
                                if (vehicleId > 0) {
                                  toggleVehicleSelection(vehicleId);
                                }
                              }}
                            >
                              x
                            </button>
                          </div>
                        ))}
                      </div>
                    ) : null}

                    <div className="reception-note-list">
                      {activeNoteLines.map((note, index) => (
                        <textarea
                          key={`note-${activeNoteKey}-${index}`}
                          className="reception-note-input"
                          rows={1}
                          value={note}
                          placeholder={`Nhập mô tả ${index + 1}...`}
                          onChange={(event) => handleVehicleNoteChange(activeNoteKey, index, event.target.value)}
                        />
                      ))}
                    </div>
                  </div>
                ) : (
                  <div className="reception-muted-note">Hãy chọn xe trước khi nhập mô tả.</div>
                )}
              </div>

              {showError("executeBranchId") ? <div className="field-error">{fieldErrors.executeBranchId}</div> : null}

              <div className="reception-summary-inline" role="status" aria-live="polite">
                <div className="reception-summary-inline-row">
                  <span>{FLEET_SERVICE_PRICE_LABEL}</span>
                  <strong>{formatCurrency(totalPrice)}</strong>
                </div>
                <p className="reception-plan-summary-warning reception-summary-inline-warning">{`* ${SERVICE_PRICE_DISCLAIMER}`}</p>
                <div className="reception-summary-inline-row">
                  <span>Thời gian dự kiến hoàn tất toàn bộ xe</span>
                  <strong>{formatDateTimeValue(formData.estimatedHandover || suggestedFleetCompletionIso)}</strong>
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
                  <span>Số xe</span>
                  <strong>
                    {selectedVehicleKeys.length}
                    {selectedVehicleKeys.length > 1 ? " (lịch nhóm)" : " (lịch thường)"}
                  </strong>
                </div>
              </div>

              {planSummaries.length > 0 ? (
                <div className="reception-plan-summary-grid">
                  {planSummaries.map((plan) => {
                    const insuranceDetails = insuranceSummaryByVehicleKey.get(plan.vehicleKey) || null;
                    const insuranceSummary = insuranceDetails?.summary || null;

                    return (
                    <article key={`plan-summary-${plan.vehicleKey}`} className="reception-plan-summary-card">
                      <div className="reception-plan-summary-head">
                        <h5 className="reception-plan-summary-title">{plan.vehicleLabel}</h5>
                        <span className="reception-plan-summary-badge">
                          {`${plan.categoryIds.length} loại - ${plan.serviceIds.length} dịch vụ`}
                        </span>
                      </div>

                      <div className="reception-plan-summary-block">
                        <p className="reception-plan-summary-block-label">DỊCH VỤ ĐÃ CHỌN</p>
                        {Array.isArray(plan.serviceGroupsByCategory) && plan.serviceGroupsByCategory.length > 0 ? (
                          <ul className="reception-plan-summary-list">
                            {plan.serviceGroupsByCategory.map((group) => (
                              <li key={`${plan.vehicleKey}-${group.categoryName}`} className="reception-plan-summary-list-item">
                                <span className="reception-plan-summary-list-group">{group.categoryName}</span>
                                <ul className="reception-plan-summary-sublist">
                                  {(group.serviceNames || []).map((serviceName) => (
                                    <li
                                      key={`${plan.vehicleKey}-${group.categoryName}-${serviceName}`}
                                      className="reception-plan-summary-sublist-item"
                                    >
                                      {serviceName}
                                    </li>
                                  ))}
                                </ul>
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="reception-plan-summary-text">Chưa có dịch vụ</p>
                        )}
                      </div>

                      {insuranceSummary ? (
                        <div className="reception-plan-summary-block">
                          <p className="reception-plan-summary-block-label">BẢO HIỂM</p>
                          <div className="reception-plan-summary-time-row">
                            <span>Công ty bảo hiểm:</span>
                            <strong>{insuranceSummary.companyName}</strong>
                          </div>
                          <div className="reception-plan-summary-time-row">
                            <span>Mức miễn thường:</span>
                            <strong>{insuranceSummary.deductibleLabel}</strong>
                          </div>
                          <div className="reception-plan-summary-time-row">
                            <span>Hình thức xử lý:</span>
                            <strong>{insuranceSummary.claimModeLabel}</strong>
                          </div>
                          <div className="reception-plan-summary-time-row">
                            <span>Hồ sơ bảo hiểm:</span>
                            <strong>{insuranceSummary.assetSummaryLabel}</strong>
                          </div>
                          <p className="reception-plan-summary-text">
                            {`Ghi chú bảo hiểm: ${insuranceSummary.intakeNote}`}
                          </p>
                        </div>
                      ) : null}

                      <div className="reception-plan-summary-block">
                        <p className="reception-plan-summary-block-label">KHUNG GIỜ</p>
                        <div className="reception-plan-summary-time-row">
                          <span>Giờ bắt đầu:</span>
                          <strong>{plan.startTimeLabel}</strong>
                        </div>
                        <div className="reception-plan-summary-time-row">
                          <span>Giờ kết thúc:</span>
                          <strong>{plan.endTimeLabel}</strong>
                        </div>
                        <div className="reception-plan-summary-time-row">
                          <span>Tổng thời lượng:</span>
                          <strong>{`${plan.serviceMinutes} phút`}</strong>
                        </div>
                      </div>

                      {hasPlanCompletionWarning(plan) ? (
                        <p className="reception-plan-summary-warning">
                          {`* ${plan.completionWarningText}`}
                        </p>
                      ) : null}

                      <div className="reception-plan-summary-footer">
                        <span>{SERVICE_PRICE_LABEL}</span>
                        <strong>{formatCurrency(plan.totalPrice)}</strong>
                      </div>
                    </article>
                  )})}
                </div>
              ) : null}
            </section>
          </div>
        </div>

        <div className="form-footer">
          <button type="button" className="btn-secondary-outline" onClick={() => navigate(-1)}>
            Hủy và quay lại
          </button>
          <button type="submit" className="btn-primary-large" disabled={saving}>
            {saving ? "Đang xử lý..." : hasMultipleSelectedVehicles ? "Tạo lệnh sửa chữa nhóm" : "Xác nhận tạo lệnh sửa chữa"}
          </button>
        </div>
      </form>

      {successOrderSummary ? (
        <div className="reception-create-success-backdrop" role="status" aria-live="polite">
          <div className="reception-create-success-popup">
            <span className="reception-create-success-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" focusable="false">
                <path d="M9.2 16.3 4.9 12l1.4-1.4 2.9 2.9 8.5-8.5 1.4 1.4z" />
              </svg>
            </span>
            <h3>Tạo lệnh thành công</h3>
            <p>
              {successOrderSummary?.isGroup
                ? `Đã tạo ${successOrderSummary.count} lệnh sửa chữa.`
                : "Đã tạo lệnh sửa chữa thành công."}
            </p>
          </div>
        </div>
      ) : null}
    </div>
  );
}



