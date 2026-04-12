import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  createSlotHold,
  getAllBranchOperatingHours,
  getAllInsuranceCompanies,
  getAvailableSlots,
  getVehicleBrands,
  getVehicleModels,
  uploadAppointmentInsuranceAsset,
} from "@features/appointments/api/appointmentApi";
import { BOOKING_FLOW, INITIAL_ODD_SLOT_VISIBILITY, SESSION_TABS } from "@features/appointments/constants/bookingConstants";
import { releaseBookingSlotHold } from "@features/appointments/api/bookingFormApi";
import BookingInsurancePanel from "@features/appointments/components/BookingInsurancePanel";
import {
  buildDisplayedSlots,
  buildSlotsBySession,
  resolveFirstActiveSession
} from "@features/appointments/utils/bookingSlots";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import { timeLabel } from "@features/appointments/utils/bookingCommon";
import { VIETNAM_LICENSE_PLATE_PATTERN, VIETNAM_LICENSE_PLATE_TITLE, isValidVietnameseLicensePlate } from "@shared/utils/licensePlateValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import { VIETNAM_VIN_PATTERN, VIETNAM_VIN_TITLE, formatVinInput, isValidVin } from "@shared/utils/vinValidation";
import {
  buildInspectionTriadTypeByServiceId,
  getInspectionTriadDisplayName,
  getConflictingInspectionTriadServiceIds,
  sumServiceMinutes,
} from "@shared/utils/serviceDurationUtils";
import {
  arePlanSelectionsEqual,
  collectCompatibleCategoryIds,
  filterBookableItemsForVehicleModels,
  prunePlanByVehicleCompatibility,
} from "@shared/utils/serviceEngineCompatibility";
import { splitBookingItemIds } from "@features/appointments/utils/bookingPackages";
import {
  buildFlexibleTriggerServiceIdSet,
  buildInsuranceFlexiblePackageIdSet,
  findUnresolvedPackageIds,
  resolveBookingFlowByItemsAndDuration,
} from "@features/appointments/utils/bookingFlowRules";
import {
  INSURANCE_ASSET_TYPE,
  buildInsuranceSummary,
  buildInsuranceSnapshotPayload,
  createEmptyInsuranceSnapshot,
  getInsuranceBookingItemNames,
  normalizeInsuranceSnapshot,
  resolveInsuranceVehicleKeys,
  validateInsuranceSnapshotByMode,
} from "@features/appointments/utils/bookingInsurance";
import { getBranchClosedMessage, isBranchClosedBySchedule } from "@shared/utils/branchOperatingSchedule";

const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM"
};

const SLOT_MODE = {
  PARALLEL: "PARALLEL",
  INDIVIDUAL: "INDIVIDUAL"
};

const EMPTY_PLAN = {
  categoryIds: [],
  serviceIds: [],
  excludedSharedCategoryIds: [],
  excludedSharedServiceIds: []
};

const EMPTY_FORM_DATA = {
  customerId: "",
  customerName: "",
  customerPhone: "",
  selectedVehicleIds: [],
  isQuickOrder: false,
  isNewVehicle: false,

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

  appointmentDate: "",
  startTime: "",
  customerNotes: ""
};

const VN_PHONE_PATTERN = "^(0[0-9]{9}|\\+84[0-9]{9})$";
const SLOT_DURATION_MINUTES = 15;
const CHECK_IN_SETUP_MINUTES = 15;
const EXECUTION_BUFFER_MINUTES = 15;

function resolveSlotDateTime(rawValue, fallbackDate = "") {
  if (!rawValue) return null;
  const parsed = new Date(rawValue);
  if (!Number.isNaN(parsed.getTime())) return parsed;

  const match = String(rawValue).match(/^(\d{1,2}):(\d{2})/);
  if (!match || !fallbackDate) return null;

  const hours = String(match[1]).padStart(2, "0");
  const minutes = String(match[2]).padStart(2, "0");
  const composed = new Date(`${fallbackDate}T${hours}:${minutes}:00`);
  return Number.isNaN(composed.getTime()) ? null : composed;
}

function buildFixedFlowSchedule(startTimeRaw, serviceMinutes, fallbackDate = "") {
  const startDate = resolveSlotDateTime(startTimeRaw, fallbackDate);
  if (!startDate) {
    return {
      startDate: null,
      endDate: null,
      completionMs: null,
      roundedTotalMinutes: 0
    };
  }

  const normalizedServiceMinutes = Number(serviceMinutes || 0);
  const safeServiceMinutes = Number.isFinite(normalizedServiceMinutes) && normalizedServiceMinutes > 0
    ? normalizedServiceMinutes
    : 0;
  const occupancyMinutes = CHECK_IN_SETUP_MINUTES + safeServiceMinutes + EXECUTION_BUFFER_MINUTES;
  const slotCount = Math.max(1, Math.ceil(occupancyMinutes / SLOT_DURATION_MINUTES));
  const roundedTotalMinutes = slotCount * SLOT_DURATION_MINUTES;
  const completionMs = startDate.getTime() + roundedTotalMinutes * 60 * 1000;

  return {
    startDate,
    endDate: new Date(completionMs),
    completionMs,
    roundedTotalMinutes
  };
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

function isValidPhone(phone) {
  return /^(0\d{9}|\+84\d{9})$/.test(normalizePhone(phone));
}

function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .trim()
    .toLowerCase();
}

function toDateTimeParts(value) {
  if (!value) return { appointmentDate: "", startTime: "" };
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return { appointmentDate: "", startTime: "" };
  const appointmentDate = `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(
    parsed.getDate()
  ).padStart(2, "0")}`;
  const startTime = `${String(parsed.getHours()).padStart(2, "0")}:${String(parsed.getMinutes()).padStart(2, "0")}`;
  return { appointmentDate, startTime };
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

function toSelectableItemIdList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id !== 0)
    )
  );
}

function parseServiceIdsFromInput(value) {
  if (Array.isArray(value)) return toSelectableItemIdList(value);
  if (Number.isFinite(Number(value)) && Number(value) !== 0) return [Number(value)];

  return toSelectableItemIdList(
    String(value || "")
      .split(/[,|]/g)
      .map((item) => Number(String(item || "").trim()))
      .filter((id) => Number.isFinite(id) && id !== 0)
  );
}

function parseInitialNoteLines(value) {
  const raw = String(value || "").trim();
  if (!raw) return [];

  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed?.symptoms)) {
      return parsed.symptoms.map((item) => String(item || "").trim()).filter(Boolean);
    }
    if (Array.isArray(parsed?.symptomsByVehicle)) {
      const firstWithSymptoms = parsed.symptomsByVehicle.find(
        (item) => Array.isArray(item?.symptoms) && item.symptoms.length > 0
      );
      if (firstWithSymptoms) {
        return firstWithSymptoms.symptoms.map((item) => String(item || "").trim()).filter(Boolean);
      }
    }
  } catch {
    // Fallback to plain text parsing.
  }

  return raw
    .split(/\r?\n|;/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

function buildPlanFromServiceIds(serviceIds, serviceCategoryMap) {
  const normalizedServiceIds = parseServiceIdsFromInput(serviceIds);
  const categoryIds = toNumberList(
    normalizedServiceIds
      .map((serviceId) => Number(serviceCategoryMap.get(Number(serviceId)) || 0))
      .filter((categoryId) => Number.isFinite(categoryId) && categoryId > 0)
  );
  return normalizePlan({
    categoryIds,
    serviceIds: normalizedServiceIds,
    excludedSharedCategoryIds: [],
    excludedSharedServiceIds: []
  });
}

function buildInitialFormData(initialValues) {
  if (!initialValues) return { ...EMPTY_FORM_DATA };

  const selectedVehicleIds = Array.isArray(initialValues.selectedVehicleIds)
    ? toNumberList(initialValues.selectedVehicleIds)
    : Number(initialValues.vehicleId) > 0
      ? [Number(initialValues.vehicleId)]
      : [];

  const { appointmentDate, startTime } = toDateTimeParts(initialValues.startTime);

  return {
    ...EMPTY_FORM_DATA,
    customerId: initialValues.customerId ? Number(initialValues.customerId) : "",
    customerName: initialValues.customerName || "",
    customerPhone: initialValues.customerPhone || "",
    selectedVehicleIds,
    appointmentDate,
    startTime,
    customerNotes: initialValues.customerNotes || ""
  };
}

function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

function normalizePlan(plan) {
  const categoryIds = toNumberList(plan?.categoryIds || []);
  const serviceIds = toSelectableItemIdList(plan?.serviceIds || []);
  const excludedSharedCategoryIds = toNumberList(plan?.excludedSharedCategoryIds || []);
  const excludedSharedServiceIds = toSelectableItemIdList(plan?.excludedSharedServiceIds || []);
  return { categoryIds, serviceIds, excludedSharedCategoryIds, excludedSharedServiceIds };
}

function normalizeService(service) {
  const id = Number(readField(service, ["id", "Id"], 0));
  const categoryId = Number(readField(service, ["categoryId", "CategoryId"], 0));
  const rawName = String(readField(service, ["name", "Name"], "") || "");
  return {
    id,
    categoryId,
    code: String(readField(service, ["code", "Code"], "") || ""),
    name: getInspectionTriadDisplayName(service, rawName),
    engineType: String(readField(service, ["engineType", "EngineType"], "") || ""),
    minutes: Math.max(0, Number(readField(service, ["standardMinutes", "StandardMinutes"], 0) || 0)),
    price: Math.max(0, Number(readField(service, ["basePrice", "BasePrice"], 0) || 0))
  };
}

function getVehicleKeyFromId(vehicleId) {
  return `existing-${Number(vehicleId)}`;
}

function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : null;
}

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
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
      lookupBrandName
    ].find(Boolean) || "";

  const modelName =
    [
      text(readField(vehicle, ["modelName", "ModelName", "vehicleModelName", "VehicleModelName"], "")),
      text(readField(modelObject, ["modelName", "ModelName", "name", "Name"], "")),
      text(modelInfo?.name)
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

function toIsoDate(value) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  return `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(
    parsed.getDate()
  ).padStart(2, "0")}`;
}

function toTimeValue(value) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  return `${String(parsed.getHours()).padStart(2, "0")}:${String(parsed.getMinutes()).padStart(2, "0")}`;
}

function formatDateTimeWithFullDate(value) {
  const parsed = value instanceof Date ? value : new Date(value);
  if (!(parsed instanceof Date) || Number.isNaN(parsed.getTime())) return "Chưa chọn";

  const hour = String(parsed.getHours()).padStart(2, "0");
  const minute = String(parsed.getMinutes()).padStart(2, "0");
  const day = String(parsed.getDate());
  const month = String(parsed.getMonth() + 1);
  const year = String(parsed.getFullYear());
  return `${hour}:${minute} (${day}/${month}/${year})`;
}

function formatCompletionWarningTime(value) {
  if (!value) return "chưa xác định";
  const parsed = value instanceof Date ? value : new Date(value);
  if (!(parsed instanceof Date) || Number.isNaN(parsed.getTime())) return "chưa xác định";
  const hour = String(parsed.getHours()).padStart(2, "0");
  const minute = String(parsed.getMinutes()).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const year = String(parsed.getFullYear());
  return `${hour} giờ ${minute} phút ngày ${day}/${month}/${year}`;
}

function normalizeSlotSelection(slot) {
  if (!slot) {
    return {
      startTime: "",
      endTime: "",
      slotDate: "",
      slotTime: ""
    };
  }

  const startTime = readField(slot, ["startTime", "StartTime"], "");
  const endTime = readField(slot, ["endTime", "EndTime"], "");
  return {
    startTime,
    endTime,
    slotDate: toIsoDate(startTime),
    slotTime: toTimeValue(startTime)
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

  normalizedServices.forEach((service) => {
    if (!Number.isFinite(service.categoryId) || service.categoryId <= 0) return;
    if (!map.has(service.categoryId)) {
      map.set(service.categoryId, { id: service.categoryId, name: `Nhóm ${service.categoryId}` });
    }
  });

  return Array.from(map.values());
}

function clonePlan(plan) {
  return {
    categoryIds: [...(plan?.categoryIds || [])],
    serviceIds: [...(plan?.serviceIds || [])],
    excludedSharedCategoryIds: [...(plan?.excludedSharedCategoryIds || [])],
    excludedSharedServiceIds: [...(plan?.excludedSharedServiceIds || [])]
  };
}

function getMissingSpecificServiceByVehicle(planSummaries, serviceCategoryMap, categoryNameById) {
  return (planSummaries || [])
    .map((plan) => {
      const categoryIds = toNumberList(plan?.categoryIds || []);
      if (categoryIds.length === 0) return null;

      const coveredCategoryIds = new Set(
        toSelectableItemIdList(plan?.serviceIds || [])
          .map((serviceId) => Number(serviceCategoryMap.get(Number(serviceId)) || 0))
          .filter((categoryId) => Number.isFinite(categoryId) && categoryId > 0)
      );

      const missingCategoryNames = categoryIds
        .filter((categoryId) => !coveredCategoryIds.has(Number(categoryId)))
        .map((categoryId) => categoryNameById.get(Number(categoryId)) || `Nhóm ${categoryId}`);

      if (missingCategoryNames.length === 0) return null;

      return {
        vehicleLabel: String(plan?.vehicleLabel || plan?.vehicleKey || "Xe chưa xác định"),
        missingCategoryNames
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

export default function ReceptionCreateAppointmentForm({
  customers = [],
  vehicles = [],
  customerVehicles = [],
  serviceCategories = [],
  branchId = 0,
  onSubmit,
  onCancel,
  onErrorMessage,
  loading,
  services = [],
  initialValues = null,
  formId,
  showFooterActions = true,
  submitLabel = "Xác nhận đặt lịch"
}) {
  const [formData, setFormData] = useState(() => buildInitialFormData(initialValues));
  const [search, setSearch] = useState("");
  const [serviceSearch, setServiceSearch] = useState("");
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [insuranceCompanies, setInsuranceCompanies] = useState([]);
  const [branchOperatingHours, setBranchOperatingHours] = useState([]);
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [guestSelectedBrandId, setGuestSelectedBrandId] = useState("");
  const [insuranceByVehicleKey, setInsuranceByVehicleKey] = useState({});
  const [insuranceUploadStateByVehicleKey, setInsuranceUploadStateByVehicleKey] = useState({});
  const [activeInsuranceVehicleKey, setActiveInsuranceVehicleKey] = useState("");
  const [errorText, setErrorText] = useState("");
  const [isSavedVehiclePopupOpen, setIsSavedVehiclePopupOpen] = useState(false);

  const [serviceMode, setServiceMode] = useState(SERVICE_MODE.SHARED);
  const [sharedPlan, setSharedPlan] = useState({ ...EMPTY_PLAN });
  const [vehiclePlans, setVehiclePlans] = useState({});
  const [activePlanVehicleKey, setActivePlanVehicleKey] = useState("");
  const [activeCategoryId, setActiveCategoryId] = useState("");
  const [isServicePickerCollapsed, setIsServicePickerCollapsed] = useState(false);
  const [slotMode, setSlotMode] = useState(SLOT_MODE.PARALLEL);
  const [activeSlotVehicleKey, setActiveSlotVehicleKey] = useState("");
  const [activeNoteVehicleKey, setActiveNoteVehicleKey] = useState("");
  const [vehicleNotesByKey, setVehicleNotesByKey] = useState({});
  const [activeSession, setActiveSession] = useState("MORNING");
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [selectingSlot, setSelectingSlot] = useState(false);
  const [slots, setSlots] = useState([]);
  const [parallelSlot, setParallelSlot] = useState(null);
  const [selectedSlotsByVehicle, setSelectedSlotsByVehicle] = useState({});
  const [slotHoldTokens, setSlotHoldTokens] = useState({});
  const [selectedDatesByVehicle, setSelectedDatesByVehicle] = useState({});
  const [showOddSlots, setShowOddSlots] = useState({ ...INITIAL_ODD_SLOT_VISIBILITY });
  const formRef = useRef(null);

  const years = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const list = [];
    for (let y = currentYear; y >= 1980; y -= 1) list.push(y);
    return list;
  }, []);

  const normalizedServices = useMemo(
    () =>
      (services || [])
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
    normalizedServices.forEach((service) => map.set(service.id, service));
    return map;
  }, [normalizedServices]);
  const insuranceCompanyNameById = useMemo(() => {
    const map = new Map();
    (insuranceCompanies || []).forEach((company) => {
      const companyId = Number(readField(company, ["id", "Id"], 0));
      const companyName = String(readField(company, ["name", "Name"], "") || "").trim();
      if (!Number.isFinite(companyId) || companyId <= 0 || !companyName) return;
      map.set(companyId, companyName);
    });
    return map;
  }, [insuranceCompanies]);

  const flexibleTriggerServiceIdSet = useMemo(
    () => buildFlexibleTriggerServiceIdSet(normalizedServices),
    [normalizedServices]
  );
  const insuranceFlexiblePackageIdSet = useMemo(
    () => buildInsuranceFlexiblePackageIdSet(normalizedServices),
    [normalizedServices]
  );

  const serviceCategoryMap = useMemo(() => {
    const map = new Map();
    normalizedServices.forEach((service) => map.set(service.id, service.categoryId));
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
    () => buildCategoryOptions(serviceCategories, normalizedServices),
    [serviceCategories, normalizedServices]
  );

  const categoryNameById = useMemo(() => {
    const map = new Map();
    categoryOptions.forEach((category) => map.set(category.id, category.name));
    return map;
  }, [categoryOptions]);

  const brandNameById = useMemo(() => {
    const map = new Map();
    (brands || []).forEach((brand) => {
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
    (models || []).forEach((model) => {
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
        fuelType
      });
    });
    return map;
  }, [models]);

  const vehicleModelIdByKey = useMemo(() => {
    const map = new Map();

    if (Number(formData.guestVehicleModelId || 0) > 0) {
      map.set("guest", Number(formData.guestVehicleModelId));
    }
    if (Number(formData.vehicleModelId || 0) > 0) {
      map.set("new-customer", Number(formData.vehicleModelId));
    }

    const customerId = Number(formData.customerId || 0);
    if (customerId > 0) {
      const linkedVehicleIds = new Set(
        (customerVehicles || [])
          .filter((item) => Number(item.customerId) === customerId)
          .map((item) => Number(item.vehicleId))
      );

      (vehicles || [])
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
  }, [
    customerVehicles,
    formData.customerId,
    formData.guestVehicleModelId,
    formData.vehicleModelId,
    vehicles
  ]);

  const describeSavedVehicle = useCallback(
    (vehicle) => formatSavedVehicleDescriptor(vehicle, brandNameById, modelInfoById),
    [brandNameById, modelInfoById]
  );

  useEffect(() => {
    let ignore = false;

    (async () => {
      try {
        const [brandRes, modelRes, insuranceCompanyRes, operatingHoursRes] = await Promise.all([
          getVehicleBrands(),
          getVehicleModels(),
          getAllInsuranceCompanies().catch(() => []),
          getAllBranchOperatingHours().catch(() => []),
        ]);
        if (ignore) return;
        setBrands(Array.isArray(brandRes) ? brandRes : []);
        setModels(Array.isArray(modelRes) ? modelRes : []);
        setInsuranceCompanies(Array.isArray(insuranceCompanyRes) ? insuranceCompanyRes : []);
        setBranchOperatingHours(Array.isArray(operatingHoursRes) ? operatingHoursRes : []);
      } catch {
        if (!ignore) {
          setBrands([]);
          setModels([]);
          setInsuranceCompanies([]);
          setBranchOperatingHours([]);
        }
      }
    })();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    const nextFormData = buildInitialFormData(initialValues);
    const initialVehicleKeys = nextFormData.isQuickOrder
      ? ["guest"]
      : nextFormData.isNewVehicle
        ? ["new-customer"]
        : toNumberList(nextFormData.selectedVehicleIds).map((vehicleId) => getVehicleKeyFromId(vehicleId));

    const requestedServiceIds = parseServiceIdsFromInput(
      Array.isArray(initialValues?.serviceIds) && initialValues.serviceIds.length > 0
        ? initialValues.serviceIds
        : initialValues?.requestedServiceIds
    );
    const nextInsuranceByVehicleKey = {};
    (Array.isArray(initialValues?.vehicleInsurancePlans) ? initialValues.vehicleInsurancePlans : []).forEach((rawPlan) => {
      const vehicleId = Number(rawPlan?.vehicleId || 0);
      const vehicleKey = String(rawPlan?.vehicleKey || "").trim() || (vehicleId > 0 ? getVehicleKeyFromId(vehicleId) : "");
      if (!vehicleKey) return;
      nextInsuranceByVehicleKey[vehicleKey] = normalizeInsuranceSnapshot(rawPlan);
    });

    const planByVehicleKey = {};
    (Array.isArray(initialValues?.vehicleServicePlans) ? initialValues.vehicleServicePlans : []).forEach((rawPlan) => {
      const vehicleId = Number(rawPlan?.vehicleId || 0);
      const vehicleKey = String(rawPlan?.vehicleKey || "").trim() || (vehicleId > 0 ? getVehicleKeyFromId(vehicleId) : "");
      if (!vehicleKey) return;

      const serviceIds = parseServiceIdsFromInput(rawPlan?.serviceIds);
      if (serviceIds.length === 0) return;

      planByVehicleKey[vehicleKey] = buildPlanFromServiceIds(serviceIds, serviceCategoryMap);
    });

    const populatedPlanKeys = Object.keys(planByVehicleKey);
    let nextServiceMode = SERVICE_MODE.SHARED;
    let nextSharedPlan = { ...EMPTY_PLAN };
    let nextVehiclePlans = {};

    if (initialVehicleKeys.length > 1 && populatedPlanKeys.length > 1) {
      nextServiceMode = SERVICE_MODE.CUSTOM;
      nextVehiclePlans = initialVehicleKeys.reduce((acc, vehicleKey) => {
        acc[vehicleKey] = planByVehicleKey[vehicleKey] || { ...EMPTY_PLAN };
        return acc;
      }, {});
    } else {
      const firstVehicleKey = initialVehicleKeys[0] || "";
      const planFromVehicle = firstVehicleKey ? planByVehicleKey[firstVehicleKey] : null;
      const fallbackPlan = populatedPlanKeys.length > 0 ? planByVehicleKey[populatedPlanKeys[0]] : null;
      const resolvedServiceIds = (planFromVehicle || fallbackPlan)?.serviceIds || requestedServiceIds;
      nextSharedPlan = buildPlanFromServiceIds(resolvedServiceIds, serviceCategoryMap);
      nextVehiclePlans = {};
    }

    const initialCategoryForActivePlan =
      nextServiceMode === SERVICE_MODE.SHARED
        ? nextSharedPlan.categoryIds?.[0] || ""
        : (nextVehiclePlans[initialVehicleKeys[0]]?.categoryIds?.[0] || "");

    const initialNoteLines = parseInitialNoteLines(initialValues?.customerNotes || nextFormData.customerNotes);
    const seededVehicleNotes =
      initialVehicleKeys.length === 1 && initialNoteLines.length > 0
        ? { [initialVehicleKeys[0]]: [...initialNoteLines, ""] }
        : {};
    const seededSelectedDates = nextFormData.appointmentDate
      ? initialVehicleKeys.reduce((acc, vehicleKey) => {
        acc[vehicleKey] = nextFormData.appointmentDate;
        return acc;
      }, {})
      : {};

    setErrorText("");
    setSearch("");
    setServiceSearch("");
    setSelectedBrandId("");
    setGuestSelectedBrandId("");
    setServiceMode(nextServiceMode);
    setSlotMode(SLOT_MODE.PARALLEL);
    setActiveSlotVehicleKey("");
    setActiveNoteVehicleKey("");
    setVehicleNotesByKey(seededVehicleNotes);
    setActiveSession("MORNING");
    setSlotsLoading(false);
    setSelectingSlot(false);
    setSlots([]);
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
    setSlotHoldTokens({});
    setSelectedDatesByVehicle(seededSelectedDates);
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });
    setSharedPlan(nextSharedPlan);
    setVehiclePlans(nextVehiclePlans);
    setInsuranceByVehicleKey(nextInsuranceByVehicleKey);
    setInsuranceUploadStateByVehicleKey({});
    setActiveInsuranceVehicleKey("");
    setActivePlanVehicleKey("");
    setActiveCategoryId(initialCategoryForActivePlan ? String(initialCategoryForActivePlan) : "");
    setIsServicePickerCollapsed(false);
    setFormData(nextFormData);
  }, [initialValues, serviceCategoryMap]);

  const filteredCustomers = useMemo(() => {
    const term = normalizeSearchText(search);
    if (!term) return [];
    return (customers || [])
      .filter((customer) => {
        const name = normalizeSearchText(customer.fullName || customer.name || "");
        const phone = normalizePhone(customer.phone || "");
        return name.includes(term) || phone.includes(term);
      })
      .slice(0, 10);
  }, [customers, search]);

  const customerVehiclesFiltered = useMemo(() => {
    if (!formData.customerId) return [];
    const customerId = Number(formData.customerId);
    const linkedVehicleIds = new Set(
      (customerVehicles || [])
        .filter((item) => Number(item.customerId) === customerId)
        .map((item) => Number(item.vehicleId))
    );
    return (vehicles || []).filter((vehicle) => linkedVehicleIds.has(Number(vehicle.id)));
  }, [customerVehicles, vehicles, formData.customerId]);
  const customerHasNoVehicles = Boolean(formData.customerId) && customerVehiclesFiltered.length === 0;

  const hasSavedVehiclesByCustomerId = useCallback(
    (customerId) => {
      const numericCustomerId = Number(customerId);
      if (!Number.isFinite(numericCustomerId) || numericCustomerId <= 0) return false;
      const linkedVehicleIds = new Set(
        (customerVehicles || [])
          .filter((item) => Number(item.customerId) === numericCustomerId)
          .map((item) => Number(item.vehicleId))
      );
      if (linkedVehicleIds.size === 0) return false;
      return (vehicles || []).some((vehicle) => linkedVehicleIds.has(Number(vehicle.id)));
    },
    [customerVehicles, vehicles]
  );

  const selectedVehicleIdSet = useMemo(
    () => new Set(toNumberList(formData.selectedVehicleIds)),
    [formData.selectedVehicleIds]
  );

  const selectedCustomerVehicles = useMemo(
    () => customerVehiclesFiltered.filter((vehicle) => selectedVehicleIdSet.has(Number(vehicle.id))),
    [customerVehiclesFiltered, selectedVehicleIdSet]
  );

  const selectableCustomerVehicles = useMemo(
    () => customerVehiclesFiltered.filter((vehicle) => !selectedVehicleIdSet.has(Number(vehicle.id))),
    [customerVehiclesFiltered, selectedVehicleIdSet]
  );

  const selectedVehicleKeys = useMemo(() => {
    if (formData.isQuickOrder) return ["guest"];
    if (formData.isNewVehicle) return ["new-customer"];
    return toNumberList(formData.selectedVehicleIds).map((vehicleId) => getVehicleKeyFromId(vehicleId));
  }, [formData.isQuickOrder, formData.isNewVehicle, formData.selectedVehicleIds]);

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
    map.set("guest", "Xe khách vãng lai");
    map.set("new-customer", "Xe mới của khách");
    customerVehiclesFiltered.forEach((vehicle) => {
      const key = getVehicleKeyFromId(vehicle.id);
      const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
      const descriptor = describeSavedVehicle(vehicle);
      const label = descriptor ? `${plate} - ${descriptor}` : plate;
      map.set(key, label);
    });
    return map;
  }, [customerVehiclesFiltered, describeSavedVehicle]);

  useEffect(() => {
    setVehiclePlans((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        next[key] = normalizePlan(prev[key] || EMPTY_PLAN);
      });
      return next;
    });

    if (selectedVehicleKeys.length <= 1) {
      setServiceMode(SERVICE_MODE.SHARED);
      setSlotMode(SLOT_MODE.PARALLEL);
    }

    if (selectedVehicleKeys.length > 0) {
      setActivePlanVehicleKey((prev) => {
        if (prev && selectedVehicleKeys.includes(prev)) return prev;
        return selectedVehicleKeys[0];
      });
      setActiveSlotVehicleKey((prev) => {
        if (prev && selectedVehicleKeys.includes(prev)) return prev;
        return selectedVehicleKeys[0];
      });
      setActiveNoteVehicleKey((prev) => {
        if (prev && selectedVehicleKeys.includes(prev)) return prev;
        return selectedVehicleKeys[0];
      });
    } else {
      setActivePlanVehicleKey("");
      setActiveSlotVehicleKey("");
      setActiveNoteVehicleKey("");
    }

    setSelectedDatesByVehicle((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        if (typeof prev[key] === "string" && prev[key]) {
          next[key] = prev[key];
          return;
        }
        if (formData.appointmentDate) {
          next[key] = formData.appointmentDate;
        }
      });
      return next;
    });

    setSelectedSlotsByVehicle((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        if (prev[key]) next[key] = prev[key];
      });
      return next;
    });

    setSlotHoldTokens((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        if (prev[key]) next[key] = prev[key];
      });
      return next;
    });

    setVehicleNotesByKey((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        if (Array.isArray(prev[key]) && prev[key].length > 0) {
          next[key] = [...prev[key]];
          return;
        }

        if (selectedVehicleKeys.length === 1) {
          const legacyNotes = String(formData.customerNotes || "").trim();
          next[key] = legacyNotes ? [legacyNotes, ""] : [""];
          return;
        }

        next[key] = [""];
      });
      return next;
    });
  }, [selectedVehicleKeys, formData.appointmentDate, formData.customerNotes]);

  useEffect(() => {
    if (!formData.customerId || formData.isNewVehicle || formData.isQuickOrder) {
      setIsSavedVehiclePopupOpen(false);
    }
  }, [formData.customerId, formData.isNewVehicle, formData.isQuickOrder]);

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

  useEffect(() => {
    if (slotMode !== SLOT_MODE.INDIVIDUAL || !activeSlotVehicleKey) return;
    setSelectedDatesByVehicle((prev) => {
      if (String(prev[activeSlotVehicleKey] || "").trim()) return prev;
      if (!formData.appointmentDate) return prev;
      return { ...prev, [activeSlotVehicleKey]: formData.appointmentDate };
    });
  }, [activeSlotVehicleKey, formData.appointmentDate, slotMode]);

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
      const nextServiceIds = toSelectableItemIdList([...sharedServiceIds, ...(raw.serviceIds || [])]).filter((serviceId) => {
        const categoryId = Number(serviceCategoryMap.get(serviceId) || 0);
        return nextCategoryIds.includes(categoryId);
      });

      return normalizePlan({
        categoryIds: nextCategoryIds,
        serviceIds: nextServiceIds,
        excludedSharedCategoryIds: (raw.excludedSharedCategoryIds || []).filter((id) => sharedNormalized.categoryIds.includes(id)),
        excludedSharedServiceIds: (raw.excludedSharedServiceIds || []).filter((id) => sharedNormalized.serviceIds.includes(id))
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
        excludedSharedServiceIds
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
    vehicleModelIdByKey
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
    },
    [selectedVehicleKeys, serviceMode]
  );

  const applyPlanUpdate = (updater) => {
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
        [activePlanKey]: deriveRawVehiclePlanFromEffective(nextEffective, sharedPlan)
      };
    });
  };

  const toggleCategory = (categoryId) => {
    const id = Number(categoryId);
    if (!Number.isFinite(id) || id <= 0) return;
    setErrorText("");

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
          serviceIds: nextServiceIds
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
              excludedSharedServiceIds: (vehiclePlan.excludedSharedServiceIds || []).filter(isDifferentCategory)
            });
          });
          return next;
        });
      }
      return;
    }

    if ((sharedPlan.categoryIds || []).includes(id)) {
      setErrorText('Loại dịch vụ dùng chung chỉ có thể hủy ở tab "Dùng chung tất cả xe".');
      return;
    }

    applyPlanUpdate((plan) => {
      const categorySet = new Set(plan.categoryIds || []);
      if (categorySet.has(id)) {
        categorySet.delete(id);
      } else {
        categorySet.add(id);
      }

      const nextCategoryIds = Array.from(categorySet);
      const nextServiceIds = (plan.serviceIds || []).filter((serviceId) =>
        nextCategoryIds.includes(Number(serviceCategoryMap.get(serviceId)))
      );

      return {
        categoryIds: nextCategoryIds,
        serviceIds: nextServiceIds
      };
    });
  };

  const toggleService = (serviceId) => {
    const numericId = Number(serviceId);
    const categoryId = Number(serviceCategoryMap.get(numericId) || 0);
    if (!numericId || !categoryId) return;
    setErrorText("");
    let shouldCollapseAfterSelect = false;

    if (effectiveServiceMode === SERVICE_MODE.SHARED) {
      const has = (sharedPlan.serviceIds || []).includes(numericId);
      if (has) {
        setSharedPlan((prev) => {
          const normalized = normalizePlan(prev);
          return normalizePlan({
            ...normalized,
            serviceIds: (normalized.serviceIds || []).filter((item) => Number(item) !== numericId)
          });
        });
      } else {
        const oppositeSharedIds = getConflictingInspectionServiceIds(sharedPlan.serviceIds, numericId);
        setSharedPlan((prev) => {
          const normalized = normalizePlan(prev);
          const nextServiceIds = toSelectableItemIdList([
            ...(normalized.serviceIds || []).filter((item) => !oppositeSharedIds.includes(Number(item))),
            numericId
          ]);
          const nextCategoryIds = toNumberList([...(normalized.categoryIds || []), categoryId]);
          return normalizePlan({
            ...normalized,
            categoryIds: nextCategoryIds,
            serviceIds: nextServiceIds
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
              excludedSharedServiceIds: nextExcludedSharedServiceIds
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
        const sharedServiceIds = toSelectableItemIdList(sharedPlan.serviceIds);
        const sharedServiceSet = new Set(sharedServiceIds);
        const sharedCategorySet = new Set(toNumberList(sharedPlan.categoryIds));
        const nextRawCategorySet = new Set(rawVehiclePlan.categoryIds || []);

        if (!sharedCategorySet.has(categoryId)) {
          nextRawCategorySet.add(categoryId);
        }

        if (sharedServiceSet.has(numericId)) {
          const hasEx = (rawVehiclePlan.excludedSharedServiceIds || []).includes(numericId);
          if (!hasEx) {
            return {
              ...prev,
              [activePlanKey]: normalizePlan({
                ...rawVehiclePlan,
                categoryIds: Array.from(nextRawCategorySet),
                serviceIds: (rawVehiclePlan.serviceIds || []).filter((item) => Number(item) !== numericId),
                excludedSharedServiceIds: [...(rawVehiclePlan.excludedSharedServiceIds || []), numericId]
              })
            };
          }

          const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveVehiclePlan.serviceIds, numericId);
          const nextExcludedSet = new Set(
            (rawVehiclePlan.excludedSharedServiceIds || []).filter((item) => Number(item) !== numericId).map(Number)
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
              excludedSharedServiceIds: Array.from(nextExcludedSet)
            })
          };
        }

        const has = (rawVehiclePlan.serviceIds || []).includes(numericId);
        if (has) {
          return {
            ...prev,
            [activePlanKey]: normalizePlan({
              ...rawVehiclePlan,
              categoryIds: Array.from(nextRawCategorySet),
              serviceIds: (rawVehiclePlan.serviceIds || []).filter((item) => Number(item) !== numericId)
            })
          };
        }

        const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveVehiclePlan.serviceIds, numericId);
        const nextVehicleServiceIds = toSelectableItemIdList([
          ...(rawVehiclePlan.serviceIds || []).filter(
            (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
          ),
          numericId
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
            excludedSharedServiceIds: Array.from(nextExcludedSet)
          })
        };
      });
    }

    if (shouldCollapseAfterSelect) {
      setIsServicePickerCollapsed(true);
    }
  };

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
      .filter((service) => selectedCategories.has(service.categoryId))
      .filter((service) => (!activeCategoryId ? true : String(service.categoryId) === String(activeCategoryId)))
      .filter((service) => (term ? normalizeSearchText(service.name).includes(term) : true))
      .sort((a, b) => a.name.localeCompare(b.name, "vi"));
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

  const planSummaries = useMemo(() => {
    return selectedVehicleKeys.map((vehicleKey) => {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      const categoryIds = toNumberList(plan.categoryIds);
      const serviceIds = toSelectableItemIdList(plan.serviceIds);
      const selectedServices = serviceIds
        .map((id) => serviceById.get(id))
        .filter(Boolean);
      const serviceGroupsByCategory = Array.from(
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
      }));
      const minutes = sumServiceMinutes(selectedServices);
      const price = selectedServices.reduce((sum, item) => sum + Number(item.price || 0), 0);
      const bookingFlow = resolveBookingFlowByItemsAndDuration({
        itemIds: serviceIds,
        flexibleTriggerServiceIdSet,
        insuranceFlexiblePackageIdSet,
        executionMinutes: 0,
      }).bookingFlow;
      const slot = slotMode === SLOT_MODE.PARALLEL ? parallelSlot : selectedSlotsByVehicle[vehicleKey];
      const startRaw = readField(slot, ["startTime", "StartTime"], "");
      const endRaw = readField(slot, ["endTime", "EndTime"], "");
      const fallbackDate = slotMode === SLOT_MODE.INDIVIDUAL
        ? (selectedDatesByVehicle[vehicleKey] || formData.appointmentDate || "")
        : (formData.appointmentDate || "");
      const schedule = buildFixedFlowSchedule(startRaw, minutes, fallbackDate);
      const parsedStart = schedule.startDate || (startRaw ? new Date(startRaw) : null);
      const parsedSlotEnd = endRaw ? new Date(endRaw) : null;
      const hasValidStart = Boolean(parsedStart) && !Number.isNaN(parsedStart.getTime());
      const hasValidSlotEnd = Boolean(parsedSlotEnd) && !Number.isNaN(parsedSlotEnd.getTime());
      const resolvedEnd = schedule.endDate || (hasValidSlotEnd ? parsedSlotEnd : null);
      const startTimeLabel = hasValidStart ? formatDateTimeWithFullDate(parsedStart) : "Chưa chọn";
      const endTimeLabel = resolvedEnd ? formatDateTimeWithFullDate(resolvedEnd) : "Chưa chọn";
      const completionMs = Number.isFinite(schedule.completionMs)
        ? schedule.completionMs
        : (hasValidSlotEnd ? parsedSlotEnd.getTime() : null);
      const flowWarningLabel = bookingFlow === BOOKING_FLOW.FLEXIBLE && Number.isFinite(completionMs)
        ? `Thời gian hoàn thành dự kiến cho xe ${vehicleLabelByKey.get(vehicleKey) || vehicleKey} là ${formatCompletionWarningTime(new Date(completionMs))}. Thời gian hoàn tất thực tế sẽ được tư vấn viên cập nhật sau khi xe vào khoang.`
        : "";

      return {
        vehicleKey,
        vehicleId: getVehicleIdFromKey(vehicleKey),
        vehicleLabel: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
        categoryIds,
        categoryNames: categoryIds.map((categoryId) => categoryNameById.get(categoryId) || `Nhóm ${categoryId}`),
        serviceGroupsByCategory,
        serviceIds,
        serviceNames: selectedServices.map((item) => item.name),
        bookingFlow,
        startTimeLabel,
        endTimeLabel,
        completionMs,
        flowWarningLabel,
        totalMinutes: minutes,
        totalPrice: price
      };
    });
  }, [
    formData.appointmentDate,
    parallelSlot,
    plansByVehicleKey,
    selectedDatesByVehicle,
    selectedSlotsByVehicle,
    selectedVehicleKeys,
    categoryNameById,
    flexibleTriggerServiceIdSet,
    insuranceFlexiblePackageIdSet,
    serviceById,
    slotMode,
    vehicleLabelByKey
  ]);

  const missingSpecificServiceByVehicle = useMemo(
    () => getMissingSpecificServiceByVehicle(planSummaries, serviceCategoryMap, categoryNameById),
    [categoryNameById, planSummaries, serviceCategoryMap]
  );

  const missingSpecificServiceMessage = useMemo(
    () => buildMissingSpecificServiceMessage(missingSpecificServiceByVehicle),
    [missingSpecificServiceByVehicle]
  );
  const dateSelectionGuardMessage = useMemo(() => {
    if (selectedVehicleKeys.length === 0) {
      return "Vui lòng chọn xe trước khi chọn ngày đặt lịch.";
    }

    for (const vehicleKey of selectedVehicleKeys) {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      const vehicleLabel = vehicleLabelByKey.get(vehicleKey) || vehicleKey;
      const categoryCount = toNumberList(plan.categoryIds).length;
      const serviceCount = toSelectableItemIdList(plan.serviceIds).length;
      const unresolvedPackageIds = findUnresolvedPackageIds(
        toSelectableItemIdList(plan.serviceIds),
        serviceById
      );
      if (unresolvedPackageIds.length > 0) {
        return `Vui lòng chọn lại gói dịch vụ cho ${vehicleLabel} (mã không hợp lệ: #${unresolvedPackageIds.join(", #")}) trước khi chọn ngày đặt lịch.`;
      }

      if (categoryCount === 0) {
        return `Vui lòng chọn loại dịch vụ cho ${vehicleLabel} trước khi chọn ngày đặt lịch.`;
      }

      if (serviceCount === 0) {
        return `Vui lòng chọn dịch vụ cụ thể cho ${vehicleLabel} trước khi chọn ngày đặt lịch.`;
      }
    }

    if (missingSpecificServiceByVehicle.length > 0) {
      return missingSpecificServiceMessage;
    }

    return "";
  }, [
    missingSpecificServiceByVehicle.length,
    missingSpecificServiceMessage,
    plansByVehicleKey,
    selectedVehicleKeys,
    serviceById,
    vehicleLabelByKey
  ]);

  const totalMinutes = useMemo(
    () => planSummaries.reduce((sum, item) => sum + Number(item.totalMinutes || 0), 0),
    [planSummaries]
  );
  const totalPrice = useMemo(
    () => planSummaries.reduce((sum, item) => sum + Number(item.totalPrice || 0), 0),
    [planSummaries]
  );
  const completionMinutes = useMemo(
    () => Math.max(...planSummaries.map((item) => Number(item.totalMinutes || 0)), 0),
    [planSummaries]
  );
  const fleetCompletionDateTimeLabel = useMemo(() => {
    if (!Array.isArray(planSummaries) || planSummaries.length === 0) return "Chưa chọn";
    const endTimes = planSummaries
      .map((item) => Number(item.completionMs))
      .filter((value) => Number.isFinite(value) && value > 0);
    if (endTimes.length !== planSummaries.length) return "Chưa chọn";
    return formatDateTimeWithFullDate(new Date(Math.max(...endTimes)));
  }, [planSummaries]);

  const bookingItemIdsByVehicleKey = useMemo(() => {
    const map = new Map();
    selectedVehicleKeys.forEach((vehicleKey) => {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      map.set(vehicleKey, toSelectableItemIdList(plan.serviceIds));
    });
    return map;
  }, [selectedVehicleKeys, plansByVehicleKey]);

  const serviceIdsByVehicleKey = useMemo(() => {
    const map = new Map();
    bookingItemIdsByVehicleKey.forEach((itemIds, vehicleKey) => {
      map.set(vehicleKey, splitBookingItemIds(itemIds).serviceIds);
    });
    return map;
  }, [bookingItemIdsByVehicleKey]);

  const packageIdsByVehicleKey = useMemo(() => {
    const map = new Map();
    bookingItemIdsByVehicleKey.forEach((itemIds, vehicleKey) => {
      map.set(vehicleKey, splitBookingItemIds(itemIds).packageIds);
    });
    return map;
  }, [bookingItemIdsByVehicleKey]);
  const insuranceItemNamesByVehicleKey = useMemo(() => {
    const map = new Map();
    selectedVehicleKeys.forEach((vehicleKey) => {
      const itemIds = bookingItemIdsByVehicleKey.get(vehicleKey) || [];
      map.set(vehicleKey, getInsuranceBookingItemNames(itemIds, serviceById));
    });
    return map;
  }, [bookingItemIdsByVehicleKey, selectedVehicleKeys, serviceById]);
  const insuranceVehicleOptions = useMemo(
    () =>
      resolveInsuranceVehicleKeys(selectedVehicleKeys, bookingItemIdsByVehicleKey, serviceById).map((vehicleKey) => ({
        key: vehicleKey,
        label: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
        selectedInsuranceItemNames: insuranceItemNamesByVehicleKey.get(vehicleKey) || [],
      })),
    [bookingItemIdsByVehicleKey, insuranceItemNamesByVehicleKey, selectedVehicleKeys, serviceById, vehicleLabelByKey]
  );
  const insuranceVehicleKeySet = useMemo(
    () => new Set(insuranceVehicleOptions.map((vehicle) => vehicle.key)),
    [insuranceVehicleOptions]
  );

  useEffect(() => {
    setInsuranceByVehicleKey((prev) => {
      const next = {};
      insuranceVehicleOptions.forEach((vehicle) => {
        next[vehicle.key] = normalizeInsuranceSnapshot(prev[vehicle.key]);
      });
      return next;
    });
  }, [insuranceVehicleOptions]);

  useEffect(() => {
    setInsuranceUploadStateByVehicleKey((prev) => {
      const next = {};
      insuranceVehicleOptions.forEach((vehicle) => {
        if (prev[vehicle.key]) {
          next[vehicle.key] = prev[vehicle.key];
        }
      });
      return next;
    });
  }, [insuranceVehicleOptions]);

  useEffect(() => {
    if (insuranceVehicleOptions.length === 0) {
      setActiveInsuranceVehicleKey("");
      return;
    }

    if (!insuranceVehicleOptions.some((vehicle) => vehicle.key === activeInsuranceVehicleKey)) {
      setActiveInsuranceVehicleKey(insuranceVehicleOptions[0].key);
    }
  }, [activeInsuranceVehicleKey, insuranceVehicleOptions]);

  const activeInsuranceSnapshot = useMemo(
    () => normalizeInsuranceSnapshot(insuranceByVehicleKey[activeInsuranceVehicleKey]),
    [activeInsuranceVehicleKey, insuranceByVehicleKey]
  );
  const insuranceSummaryByVehicleKey = useMemo(() => {
    const map = new Map();
    insuranceVehicleOptions.forEach((vehicle) => {
      map.set(vehicle.key, {
        selectedInsuranceItemNames: Array.isArray(vehicle.selectedInsuranceItemNames)
          ? vehicle.selectedInsuranceItemNames.filter(Boolean)
          : [],
        summary: buildInsuranceSummary(insuranceByVehicleKey[vehicle.key], insuranceCompanyNameById, { mode: "booking" }),
      });
    });
    return map;
  }, [insuranceByVehicleKey, insuranceCompanyNameById, insuranceVehicleOptions]);

  const handleInsuranceSnapshotChange = useCallback(
    (field, value) => {
      if (!activeInsuranceVehicleKey) return;
      setInsuranceByVehicleKey((prev) => ({
        ...prev,
        [activeInsuranceVehicleKey]: normalizeInsuranceSnapshot({
          ...(prev[activeInsuranceVehicleKey] || createEmptyInsuranceSnapshot()),
          [field]: value,
        }),
      }));
    },
    [activeInsuranceVehicleKey]
  );

  const setInsuranceUploadState = useCallback((vehicleKey, assetType, patch) => {
    if (!vehicleKey || !assetType) return;
    setInsuranceUploadStateByVehicleKey((prev) => ({
      ...prev,
      [vehicleKey]: {
        ...(prev[vehicleKey] || {}),
        [assetType]: {
          ...(((prev[vehicleKey] || {})[assetType] || {})),
          ...(patch || {}),
        },
      },
    }));
  }, []);

  const handleInsuranceAssetUpload = useCallback(
    async (assetType, files) => {
      if (!activeInsuranceVehicleKey || !assetType) return;
      const uploadFiles = Array.isArray(files) ? files.filter(Boolean) : [];
      if (uploadFiles.length === 0) return;

      setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
        uploading: true,
        successMessage: "",
      });
      try {
        const uploadedUrls = [];
        for (const file of uploadFiles) {
          // Upload sequentially to keep response order stable.
          // eslint-disable-next-line no-await-in-loop
          const uploaded = await uploadAppointmentInsuranceAsset(file, assetType);
          const uploadedUrl = readField(uploaded, ["url", "Url"], "");
          if (uploadedUrl) uploadedUrls.push(uploadedUrl);
        }

        if (uploadedUrls.length > 0) {
          const targetField =
            assetType === INSURANCE_ASSET_TYPE.CARD
              ? "insuranceCardImageUrls"
              : "insuranceDamageImageUrls";

          setInsuranceByVehicleKey((prev) => {
            const currentSnapshot = normalizeInsuranceSnapshot(prev[activeInsuranceVehicleKey]);
            return {
              ...prev,
              [activeInsuranceVehicleKey]: normalizeInsuranceSnapshot({
                ...currentSnapshot,
                [targetField]: [...(currentSnapshot[targetField] || []), ...uploadedUrls],
              }),
            };
          });

          setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
            successMessage:
              assetType === INSURANCE_ASSET_TYPE.CARD
                ? "Đã tải ảnh thẻ bảo hiểm."
                : "Đã tải ảnh hiện trạng xe.",
          });
        }
      } catch (uploadError) {
        setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
          successMessage: "",
        });
        setErrorText(uploadError?.message || "Không thể tải ảnh bảo hiểm. Vui lòng thử lại.");
      } finally {
        setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
          uploading: false,
        });
      }
    },
    [activeInsuranceVehicleKey, setInsuranceUploadState]
  );

  const handleInsuranceAssetRemove = useCallback(
    (assetType, index) => {
      if (!activeInsuranceVehicleKey || !assetType || !Number.isInteger(index) || index < 0) return;
      const targetField =
        assetType === INSURANCE_ASSET_TYPE.CARD
          ? "insuranceCardImageUrls"
          : "insuranceDamageImageUrls";

      setInsuranceByVehicleKey((prev) => {
        const currentSnapshot = normalizeInsuranceSnapshot(prev[activeInsuranceVehicleKey]);
        const nextUrls = (currentSnapshot[targetField] || []).filter((_, itemIndex) => itemIndex !== index);
        return {
          ...prev,
          [activeInsuranceVehicleKey]: normalizeInsuranceSnapshot({
            ...currentSnapshot,
            [targetField]: nextUrls,
          }),
        };
      });
      setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
        successMessage: "",
      });
    },
    [activeInsuranceVehicleKey, setInsuranceUploadState]
  );

  const getVehicleFlow = useCallback((vehicleKey) => {
    const bookingItemIds = bookingItemIdsByVehicleKey.get(vehicleKey) || [];
    return resolveBookingFlowByItemsAndDuration({
      itemIds: bookingItemIds,
      flexibleTriggerServiceIdSet,
      insuranceFlexiblePackageIdSet,
      executionMinutes: 0,
    }).bookingFlow;
  }, [bookingItemIdsByVehicleKey, flexibleTriggerServiceIdSet, insuranceFlexiblePackageIdSet]);

  const resolveSlotFlow = useCallback((slot, fallbackFlow = BOOKING_FLOW.FIXED) => {
    const rawFlow = String(readField(slot, ["bookingFlow", "BookingFlow"], fallbackFlow) || "")
      .trim()
      .toLowerCase();
    if (rawFlow.includes("flex")) return BOOKING_FLOW.FLEXIBLE;
    if (rawFlow.includes("fixed")) return BOOKING_FLOW.FIXED;
    return fallbackFlow;
  }, []);

  const hasAnyFlexibleVehicle = useMemo(
    () => selectedVehicleKeys.some((vehicleKey) => getVehicleFlow(vehicleKey) === BOOKING_FLOW.FLEXIBLE),
    [getVehicleFlow, selectedVehicleKeys]
  );

  const effectiveSelectedDate = useMemo(() => {
    if (slotMode === SLOT_MODE.INDIVIDUAL && activeSlotVehicleKey) {
      return selectedDatesByVehicle[activeSlotVehicleKey] || "";
    }
    return formData.appointmentDate || "";
  }, [activeSlotVehicleKey, formData.appointmentDate, selectedDatesByVehicle, slotMode]);

  const activeIndividualSlot = useMemo(
    () => (activeSlotVehicleKey ? selectedSlotsByVehicle[activeSlotVehicleKey] || null : null),
    [activeSlotVehicleKey, selectedSlotsByVehicle]
  );

  const chosenSlot = slotMode === SLOT_MODE.PARALLEL ? parallelSlot : activeIndividualSlot;
  const selectedSlotCount = useMemo(
    () =>
      slotMode === SLOT_MODE.PARALLEL
        ? (parallelSlot ? selectedVehicleKeys.length : 0)
        : selectedVehicleKeys.filter((vehicleKey) => Boolean(selectedSlotsByVehicle[vehicleKey])).length,
    [parallelSlot, selectedSlotsByVehicle, selectedVehicleKeys, slotMode]
  );

  const effectiveSlotFlow = useMemo(() => {
    if (slotMode === SLOT_MODE.INDIVIDUAL) {
      const targetVehicleKey = activeSlotVehicleKey || selectedVehicleKeys[0] || "";
      if (targetVehicleKey) return getVehicleFlow(targetVehicleKey);
    }
    return hasAnyFlexibleVehicle ? BOOKING_FLOW.FLEXIBLE : BOOKING_FLOW.FIXED;
  }, [activeSlotVehicleKey, getVehicleFlow, hasAnyFlexibleVehicle, selectedVehicleKeys, slotMode]);

  const slotSelectionGuide = useMemo(() => {
    if (effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE) {
      return "Chọn khung giờ check-in (tiếp nhận).";
    }
    return "Chọn khung giờ sửa chữa.";
  }, [effectiveSlotFlow]);

  const availableSlots = useMemo(() => {
    const resolveSlotTimeMs = (rawValue) => {
      if (!rawValue) return null;
      const parsed = new Date(rawValue);
      if (!Number.isNaN(parsed.getTime())) {
        if (!effectiveSelectedDate) return parsed.getTime();
        const hours = String(parsed.getHours()).padStart(2, "0");
        const minutes = String(parsed.getMinutes()).padStart(2, "0");
        const composed = new Date(`${effectiveSelectedDate}T${hours}:${minutes}:00`);
        return Number.isNaN(composed.getTime()) ? parsed.getTime() : composed.getTime();
      }

      const match = String(rawValue).match(/^(\d{1,2}):(\d{2})/);
      if (!match || !effectiveSelectedDate) return null;

      const hours = String(match[1]).padStart(2, "0");
      const minutes = String(match[2]).padStart(2, "0");
      const composed = new Date(`${effectiveSelectedDate}T${hours}:${minutes}:00`);
      return Number.isNaN(composed.getTime()) ? null : composed.getTime();
    };

    const nowMs = Date.now();
    const baseCutoffMs = 15 * 60 * 1000;

    return (slots || []).filter((slot) => {
      const isAvailable = readField(slot, ["isAvailable", "IsAvailable"], false);
      const startTimeRaw = readField(slot, ["startTime", "StartTime"], null);
      const startMs = resolveSlotTimeMs(startTimeRaw);
      const endTimeRaw = readField(slot, ["endTime", "EndTime"], null);
      const endMs = resolveSlotTimeMs(endTimeRaw);

      const slotDurationMs =
        Number.isFinite(startMs) && Number.isFinite(endMs) && endMs > startMs ? endMs - startMs : 0;
      const effectiveCutoffMs = Math.max(baseCutoffMs, slotDurationMs);
      if (Number.isFinite(endMs) && endMs - nowMs < effectiveCutoffMs) return false;

      const rawRemaining = readField(slot, ["remainingCapacity", "RemainingCapacity"], null);
      const remaining = rawRemaining === null || rawRemaining === undefined ? null : Number(rawRemaining);

      if (!Boolean(isAvailable)) return false;
      if (remaining === null || Number.isNaN(remaining)) return true;
      return remaining > 0;
    });
  }, [effectiveSelectedDate, slots]);

  const slotsBySession = useMemo(() => buildSlotsBySession(availableSlots), [availableSlots]);
  const displayedSlots = useMemo(
    () => buildDisplayedSlots({ slotsBySession, activeSession, bookingFlow: effectiveSlotFlow, showOddSlots }),
    [activeSession, effectiveSlotFlow, showOddSlots, slotsBySession]
  );

  const appointmentSummary = useMemo(() => {
    if (slotMode === SLOT_MODE.PARALLEL) {
      const selection = normalizeSlotSelection(parallelSlot);
      if (!selection.startTime || !selection.endTime) return "";

      const start = new Date(selection.startTime);
      const end = new Date(selection.endTime);
      if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return "";
      return `${start.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })} - ${end.toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
        timeZone: "Asia/Ho_Chi_Minh",
      })}`;
    }

    if (selectedVehicleKeys.length === 0) return "";
    return `Đã chọn khung giờ cho ${selectedSlotCount}/${selectedVehicleKeys.length} xe.`;
  }, [parallelSlot, selectedSlotCount, selectedVehicleKeys.length, slotMode]);

  const selectedServiceIdsUnion = useMemo(
    () => toSelectableItemIdList(planSummaries.flatMap((item) => item.serviceIds)),
    [planSummaries]
  );

  const selectedCategoryIdsUnion = useMemo(
    () => toNumberList(planSummaries.flatMap((item) => item.categoryIds)),
    [planSummaries]
  );

  const activeNoteKey = useMemo(
    () => activeNoteVehicleKey || selectedVehicleKeys[0] || "",
    [activeNoteVehicleKey, selectedVehicleKeys]
  );

  const activeNoteVehicleLines = useMemo(() => {
    if (!activeNoteKey) return [""];
    const current = vehicleNotesByKey?.[activeNoteKey];
    if (!Array.isArray(current) || current.length === 0) return [""];
    return current;
  }, [activeNoteKey, vehicleNotesByKey]);

  const normalizeNoteLines = useCallback((lines) => {
    if (!Array.isArray(lines)) return [];
    return lines
      .map((line) => String(line || "").trim())
      .filter(Boolean);
  }, []);

  const buildVehicleCustomerNote = useCallback(
    (lines) => {
      const normalized = normalizeNoteLines(lines);
      return normalized.length > 0 ? JSON.stringify({ symptoms: normalized }) : null;
    },
    [normalizeNoteLines]
  );

  const onVehicleNoteChange = useCallback((vehicleKey, index, value) => {
    setVehicleNotesByKey((prev) => {
      const current = Array.isArray(prev?.[vehicleKey]) && prev[vehicleKey].length > 0
        ? [...prev[vehicleKey]]
        : [""];
      current[index] = value;

      if (
        index === current.length - 1 &&
        String(value || "").trim() &&
        current.length < 12
      ) {
        current.push("");
      }

      return {
        ...prev,
        [vehicleKey]: current
      };
    });
  }, []);

  useEffect(() => {
    setActiveSession(resolveFirstActiveSession(SESSION_TABS, slotsBySession));
  }, [slotsBySession]);

  useEffect(() => {
    if (!errorText) return;

    const formEl = formRef.current;
    if (formEl && typeof formEl.scrollTo === "function") {
      formEl.scrollTo({ top: 0, behavior: "smooth" });
    }
    if (formEl && typeof formEl.scrollIntoView === "function") {
      formEl.scrollIntoView({ behavior: "smooth", block: "start" });
    }
    if (typeof window !== "undefined" && typeof window.scrollTo === "function") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  }, [errorText]);

  useEffect(() => {
    if (typeof onErrorMessage === "function") {
      onErrorMessage(errorText || "");
    }
  }, [errorText, onErrorMessage]);

  const releaseHoldTokenIfAny = useCallback(async (vehicleKey) => {
    const token = slotHoldTokens?.[vehicleKey];
    if (!token) return;
    try {
      await releaseBookingSlotHold(token);
    } catch {
      // Ignore release failure; token will expire automatically.
    }
    setSlotHoldTokens((prev) => {
      const next = { ...prev };
      delete next[vehicleKey];
      return next;
    });
  }, [slotHoldTokens]);

  const releaseAllHoldTokens = useCallback(async () => {
    const vehicleKeys = Object.keys(slotHoldTokens || {});
    for (const vehicleKey of vehicleKeys) {
      // Release sequentially to keep flow deterministic.
      // eslint-disable-next-line no-await-in-loop
      await releaseHoldTokenIfAny(vehicleKey);
    }
  }, [releaseHoldTokenIfAny, slotHoldTokens]);

  const loadSlots = useCallback(async () => {
    const effectiveBranchId = Number(branchId || 0);
    if (!Number.isFinite(effectiveBranchId) || effectiveBranchId <= 0 || selectedVehicleKeys.length === 0 || !effectiveSelectedDate) {
      setSlots([]);
      return;
    }
    if (isBranchClosedBySchedule(branchOperatingHours, effectiveBranchId, `${effectiveSelectedDate}T08:00:00`)) {
      setSlots([]);
      setErrorText(getBranchClosedMessage(`${effectiveSelectedDate}T08:00:00`));
      return;
    }

    const serviceGroups = selectedVehicleKeys.map((vehicleKey) => serviceIdsByVehicleKey.get(vehicleKey) || []);
    const packageGroups = selectedVehicleKeys.map((vehicleKey) => packageIdsByVehicleKey.get(vehicleKey) || []);
    const effectiveActiveSlotVehicleKey = activeSlotVehicleKey || selectedVehicleKeys[0] || "";

    const requestedServiceIds =
      slotMode === SLOT_MODE.PARALLEL
        ? serviceGroups.map((group) => group.join(",")).join("|")
        : (serviceIdsByVehicleKey.get(effectiveActiveSlotVehicleKey) || []).join(",");
    const requestedPackageIds =
      slotMode === SLOT_MODE.PARALLEL
        ? packageGroups.map((group) => group.join(",")).join("|")
        : (packageIdsByVehicleKey.get(effectiveActiveSlotVehicleKey) || []).join(",");

    setSlotsLoading(true);
    setSlots([]);
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });

    try {
      let data = await getAvailableSlots(effectiveBranchId, effectiveSelectedDate, {
        bookingFlow: effectiveSlotFlow,
        vehicleCount: slotMode === SLOT_MODE.PARALLEL ? selectedVehicleKeys.length : 1,
        isParallel: slotMode === SLOT_MODE.PARALLEL,
        requestedServiceIds,
        requestedPackageIds
      });

      if (
        Array.isArray(data) &&
        data.length === 0 &&
        (
          (typeof requestedServiceIds === "string" && requestedServiceIds.includes("|")) ||
          (typeof requestedPackageIds === "string" && requestedPackageIds.includes("|"))
        )
      ) {
        const fallbackRequestedServiceIds = requestedServiceIds
          .split("|")
          .flatMap((group) => String(group || "").split(","))
          .map((item) => String(item || "").trim())
          .filter(Boolean)
          .join(",");
        const fallbackRequestedPackageIds = requestedPackageIds
          .split("|")
          .flatMap((group) => String(group || "").split(","))
          .map((item) => String(item || "").trim())
          .filter(Boolean)
          .join(",");
        data = await getAvailableSlots(effectiveBranchId, effectiveSelectedDate, {
          bookingFlow: effectiveSlotFlow,
          vehicleCount: slotMode === SLOT_MODE.PARALLEL ? selectedVehicleKeys.length : 1,
          isParallel: slotMode === SLOT_MODE.PARALLEL,
          requestedServiceIds: fallbackRequestedServiceIds,
          requestedPackageIds: fallbackRequestedPackageIds
        });
      }

      setSlots(Array.isArray(data) ? data : []);
    } catch (slotError) {
      setSlots([]);
      setErrorText(slotError?.message || "Không thể tải khung giờ. Vui lòng thử lại.");
    } finally {
      setSlotsLoading(false);
    }
  }, [
    activeSlotVehicleKey,
    branchOperatingHours,
    branchId,
    effectiveSelectedDate,
    effectiveSlotFlow,
    packageIdsByVehicleKey,
    selectedVehicleKeys,
    serviceIdsByVehicleKey,
    slotMode
  ]);

  useEffect(() => {
    void loadSlots();
  }, [loadSlots]);

  const toggleOddSlotsForSession = (sessionKey) => {
    setShowOddSlots((prev) => ({ ...prev, [sessionKey]: !prev[sessionKey] }));
  };

  const createHoldForVehicle = useCallback(
    async (slot, vehicleKey, vehicleFlow) => {
      const startTime = readField(slot, ["startTime", "StartTime"], "");
      const endTime = readField(slot, ["endTime", "EndTime"], "");
      const requestedServiceIds = (serviceIdsByVehicleKey.get(vehicleKey) || []).join(",");
      const requestedPackageIds = (packageIdsByVehicleKey.get(vehicleKey) || []).join(",");

      const response = await createSlotHold({
        branchId: Number(branchId),
        startTime,
        endTime,
        bookingFlow: vehicleFlow,
        requestedServiceIds,
        requestedPackageIds,
        vehicleCount: 1
      });
      const token = readField(response, ["holdToken", "HoldToken"], "");
      if (!token) throw new Error("Không thể giữ chỗ khung giờ đã chọn.");
      setSlotHoldTokens((prev) => ({ ...prev, [vehicleKey]: token }));
    },
    [branchId, packageIdsByVehicleKey, serviceIdsByVehicleKey]
  );

  const handleSlotModeChange = async (mode) => {
    if (mode === slotMode) return;
    await releaseAllHoldTokens();
    setSlotMode(mode);
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
    setSlots([]);
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });
    setFormData((prev) => ({ ...prev, startTime: "" }));
    if (mode === SLOT_MODE.PARALLEL) {
      setSelectedDatesByVehicle((prev) => {
        const next = { ...prev };
        selectedVehicleKeys.forEach((vehicleKey) => {
          next[vehicleKey] = formData.appointmentDate || next[vehicleKey] || "";
        });
        return next;
      });
    }
  };

  const handleDateChange = async (value) => {
    if (dateSelectionGuardMessage) {
      setErrorText(dateSelectionGuardMessage);
      return;
    }

    const nextDate = String(value || "").slice(0, 10);
    const effectiveBranchId = Number(branchId || 0);
    if (
      nextDate &&
      Number.isFinite(effectiveBranchId) &&
      effectiveBranchId > 0 &&
      isBranchClosedBySchedule(branchOperatingHours, effectiveBranchId, `${nextDate}T08:00:00`)
    ) {
      setErrorText(getBranchClosedMessage(`${nextDate}T08:00:00`));
      return;
    }

    if (slotMode === SLOT_MODE.INDIVIDUAL && activeSlotVehicleKey) {
      await releaseHoldTokenIfAny(activeSlotVehicleKey);
      setSelectedDatesByVehicle((prev) => ({ ...prev, [activeSlotVehicleKey]: nextDate }));
      setSelectedSlotsByVehicle((prev) => {
        const next = { ...prev };
        delete next[activeSlotVehicleKey];
        return next;
      });
      return;
    }

    await releaseAllHoldTokens();
    setFormData((prev) => ({ ...prev, appointmentDate: nextDate, startTime: "" }));
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
  };

  const handleSelectSlot = async (slot) => {
    if (!slot || selectingSlot) return;

    const effectiveBranchId = Number(branchId || 0);
    if (!Number.isFinite(effectiveBranchId) || effectiveBranchId <= 0) {
      setErrorText("Vui lòng chọn đúng chi nhánh làm việc trước khi chọn khung giờ.");
      return;
    }

    if (dateSelectionGuardMessage) {
      setErrorText(dateSelectionGuardMessage);
      return;
    }

    setErrorText("");
    setSelectingSlot(true);
    try {
      if (slotMode === SLOT_MODE.PARALLEL) {
        for (const vehicleKey of selectedVehicleKeys) {
          // Release previous hold then keep a new hold for each vehicle.
          // eslint-disable-next-line no-await-in-loop
          await releaseHoldTokenIfAny(vehicleKey);
          const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicleKey));
          // eslint-disable-next-line no-await-in-loop
          await createHoldForVehicle(slot, vehicleKey, vehicleFlow);
        }
        setParallelSlot(slot);
        const selection = normalizeSlotSelection(slot);
        setFormData((prev) => ({
          ...prev,
          appointmentDate: selection.slotDate || prev.appointmentDate,
          startTime: selection.slotTime || prev.startTime
        }));
        return;
      }

      if (!activeSlotVehicleKey) return;
      await releaseHoldTokenIfAny(activeSlotVehicleKey);
      const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(activeSlotVehicleKey));
      await createHoldForVehicle(slot, activeSlotVehicleKey, vehicleFlow);
      setSelectedSlotsByVehicle((prev) => ({ ...prev, [activeSlotVehicleKey]: slot }));

      const selection = normalizeSlotSelection(slot);
      setFormData((prev) => ({
        ...prev,
        appointmentDate: selection.slotDate || prev.appointmentDate,
        startTime: selection.slotTime || prev.startTime
      }));
    } catch (slotError) {
      setErrorText(slotError?.message || "Không thể giữ chỗ khung giờ. Vui lòng chọn lại.");
    } finally {
      setSelectingSlot(false);
    }
  };

  const isSlotHoldStaleMessage = useCallback((message) => {
    const normalized = normalizeText(message || "");
    return (
      normalized.includes("giu cho da het han") ||
      normalized.includes("thong tin giu cho khong khop voi lich dang dat") ||
      (normalized.includes("giu cho") && normalized.includes("khong khop")) ||
      (normalized.includes("giu cho") && normalized.includes("khong con phu hop"))
    );
  }, []);

  const refreshSelectedSlotHolds = useCallback(async () => {
    const failedVehicles = [];

    if (slotMode === SLOT_MODE.PARALLEL) {
      const slot = parallelSlot;
      if (!slot) return selectedVehicleKeys.map((vehicleKey) => vehicleLabelByKey.get(vehicleKey) || vehicleKey);

      for (const vehicleKey of selectedVehicleKeys) {
        const vehicleLabel = vehicleLabelByKey.get(vehicleKey) || vehicleKey;
        try {
          // eslint-disable-next-line no-await-in-loop
          await releaseHoldTokenIfAny(vehicleKey);
          const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicleKey));
          // eslint-disable-next-line no-await-in-loop
          await createHoldForVehicle(slot, vehicleKey, vehicleFlow);
        } catch {
          failedVehicles.push(vehicleLabel);
        }
      }
      return failedVehicles;
    }

    for (const vehicleKey of selectedVehicleKeys) {
      const vehicleLabel = vehicleLabelByKey.get(vehicleKey) || vehicleKey;
      const slot = selectedSlotsByVehicle?.[vehicleKey];
      if (!slot) {
        failedVehicles.push(vehicleLabel);
        continue;
      }
      try {
        // eslint-disable-next-line no-await-in-loop
        await releaseHoldTokenIfAny(vehicleKey);
        const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicleKey));
        // eslint-disable-next-line no-await-in-loop
        await createHoldForVehicle(slot, vehicleKey, vehicleFlow);
      } catch {
        failedVehicles.push(vehicleLabel);
      }
    }

    return failedVehicles;
  }, [
    createHoldForVehicle,
    getVehicleFlow,
    parallelSlot,
    releaseHoldTokenIfAny,
    resolveSlotFlow,
    selectedSlotsByVehicle,
    selectedVehicleKeys,
    slotMode,
    vehicleLabelByKey
  ]);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setErrorText("");
    const nextValue =
      name === "vehicleVin" || name === "guestVehicleVin"
        ? formatVinInput(value)
        : formatLicensePlateByFieldName(name, value);
    setFormData((prev) => ({ ...prev, [name]: nextValue }));
  };

  const handlePlateBlur = (event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({ ...prev, [name]: formatLicensePlateByFieldName(name, value) }));
  };

  const toggleQuickOrder = () => {
    void releaseAllHoldTokens();
    setErrorText("");
    setIsSavedVehiclePopupOpen(false);
    setSearch("");
    setServiceSearch("");
    setSelectedBrandId("");
    setGuestSelectedBrandId("");
    setSharedPlan({ ...EMPTY_PLAN });
    setVehiclePlans({});
    setActivePlanVehicleKey("");
    setActiveCategoryId("");
    setIsServicePickerCollapsed(false);
    setSlotMode(SLOT_MODE.PARALLEL);
    setActiveSlotVehicleKey("");
    setActiveNoteVehicleKey("");
    setActiveSession("MORNING");
    setSlots([]);
    setSlotsLoading(false);
    setSelectingSlot(false);
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
    setSlotHoldTokens({});
    setSelectedDatesByVehicle({});
    setVehicleNotesByKey({});
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });
    setFormData((prev) => ({
      ...prev,
      isQuickOrder: !prev.isQuickOrder,
      isNewVehicle: false,
      customerId: "",
      customerName: "",
      customerPhone: "",
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
      guestVehicleYear: ""
    }));
  };

  const selectCustomer = (customer) => {
    void releaseAllHoldTokens();
    setErrorText("");
    setIsSavedVehiclePopupOpen(false);
    setSearch("");
    setSelectedBrandId("");
    setSharedPlan({ ...EMPTY_PLAN });
    setVehiclePlans({});
    setActivePlanVehicleKey("");
    setActiveCategoryId("");
    setIsServicePickerCollapsed(false);
    setSlotMode(SLOT_MODE.PARALLEL);
    setActiveSlotVehicleKey("");
    setActiveNoteVehicleKey("");
    setActiveSession("MORNING");
    setSlots([]);
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
    setSlotHoldTokens({});
    setSelectedDatesByVehicle({});
    setVehicleNotesByKey({});
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });
    const customerId = Number(customer.id);
    const hasSavedVehicles = hasSavedVehiclesByCustomerId(customerId);
    setFormData((prev) => ({
      ...prev,
      customerId,
      customerName: customer.fullName || "",
      customerPhone: customer.phone || "",
      isNewVehicle: !hasSavedVehicles,
      selectedVehicleIds: [],
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: ""
    }));
  };

  const toggleVehicleSelection = (vehicleId) => {
    const numericId = Number(vehicleId);
    setFormData((prev) => {
      const existing = new Set(toNumberList(prev.selectedVehicleIds));
      if (existing.has(numericId)) existing.delete(numericId);
      else existing.add(numericId);
      return { ...prev, selectedVehicleIds: Array.from(existing) };
    });
  };

  const addExistingVehicle = (vehicleId) => {
    const numericId = Number(vehicleId);
    if (!Number.isFinite(numericId) || numericId <= 0) return;
    setFormData((prev) => {
      const existing = new Set(toNumberList(prev.selectedVehicleIds));
      existing.add(numericId);
      return { ...prev, selectedVehicleIds: Array.from(existing) };
    });
  };

  const switchToSavedVehicleMode = () => {
    if (customerHasNoVehicles) return;
    void releaseAllHoldTokens();
    setIsSavedVehiclePopupOpen(false);
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
    setSlotHoldTokens({});
    setVehicleNotesByKey({});
    setFormData((prev) => ({
      ...prev,
      isNewVehicle: false
    }));
  };

  const switchToNewVehicleMode = () => {
    void releaseAllHoldTokens();
    setIsSavedVehiclePopupOpen(false);
    setParallelSlot(null);
    setSelectedSlotsByVehicle({});
    setSlotHoldTokens({});
    setVehicleNotesByKey({});
    setFormData((prev) => ({
      ...prev,
      isNewVehicle: true
    }));
  };

  const confirmNewVehicleMode = () => {
    setErrorText("");
  };

  const cancelNewVehicleMode = () => {
    switchToSavedVehicleMode();
  };

  const validateBeforeSubmit = () => {
    if (formData.isQuickOrder) {
      if (!String(formData.guestName || "").trim()) return "Vui lòng nhập tên khách vãng lai.";
      if (!isValidPhone(formData.guestPhone)) return "Số điện thoại khách vãng lai không hợp lệ.";
      if (!String(formData.guestVehiclePlate || "").trim()) return "Vui lòng nhập biển số xe.";
      if (!isValidVietnameseLicensePlate(formData.guestVehiclePlate)) return "Biển số xe không đúng định dạng Việt Nam.";
      if (!formData.guestVehicleModelId) return "Vui lòng chọn dòng xe cho khách vãng lai.";
      if (!String(formData.guestVehicleVin || "").trim()) return "Vui lòng nhập số VIN cho xe vãng lai.";
      if (!isValidVin(formData.guestVehicleVin)) return "Số VIN không hợp lệ (17 ký tự, không chứa I, O, Q).";
    } else {
      if (!formData.customerId) return "Vui lòng chọn khách hàng.";
      if (!isValidPhone(formData.customerPhone)) return "Số điện thoại khách hàng không hợp lệ.";

      if (formData.isNewVehicle) {
        if (!String(formData.vehicleLicensePlate || "").trim()) return "Vui lòng nhập biển số xe mới.";
        if (!isValidVietnameseLicensePlate(formData.vehicleLicensePlate)) return "Biển số xe mới không đúng định dạng Việt Nam.";
        if (!formData.vehicleModelId) return "Vui lòng chọn dòng xe mới.";
        if (!String(formData.vehicleVin || "").trim()) return "Vui lòng nhập số VIN cho xe mới.";
        if (!isValidVin(formData.vehicleVin)) return "Số VIN không hợp lệ (17 ký tự, không chứa I, O, Q).";
      } else if (toNumberList(formData.selectedVehicleIds).length === 0) {
        return "Vui lòng chọn ít nhất 1 xe của khách.";
      }
    }

    if (selectedVehicleKeys.length === 0) {
      return "Vui lòng chọn ít nhất một xe để gán dịch vụ.";
    }

    if (slotMode === SLOT_MODE.PARALLEL) {
      if (!formData.appointmentDate) {
        return "Vui lòng chọn ngày đặt lịch.";
      }
      if (!parallelSlot) {
        return "Vui lòng chọn khung giờ hẹn.";
      }
      const missingHeldVehicles = selectedVehicleKeys
        .filter((vehicleKey) => !Boolean(slotHoldTokens?.[vehicleKey]))
        .map((vehicleKey) => vehicleLabelByKey.get(vehicleKey) || vehicleKey);
      if (missingHeldVehicles.length > 0) {
        return `Giữ chỗ chưa hoàn tất cho: ${missingHeldVehicles.join(", ")}. Vui lòng chọn lại khung giờ.`;
      }
    } else {
      const missingDateVehicles = selectedVehicleKeys
        .filter((vehicleKey) => !String(selectedDatesByVehicle?.[vehicleKey] || "").trim())
        .map((vehicleKey) => vehicleLabelByKey.get(vehicleKey) || vehicleKey);
      if (missingDateVehicles.length > 0) {
        return `Vui lòng chọn ngày đặt lịch cho: ${missingDateVehicles.join(", ")}.`;
      }

      const missingSlotVehicles = selectedVehicleKeys
        .filter((vehicleKey) => !Boolean(selectedSlotsByVehicle?.[vehicleKey]))
        .map((vehicleKey) => vehicleLabelByKey.get(vehicleKey) || vehicleKey);
      if (missingSlotVehicles.length > 0) {
        return `Vui lòng chọn khung giờ cho: ${missingSlotVehicles.join(", ")}.`;
      }

      const missingHeldVehicles = selectedVehicleKeys
        .filter((vehicleKey) => !Boolean(slotHoldTokens?.[vehicleKey]))
        .map((vehicleKey) => vehicleLabelByKey.get(vehicleKey) || vehicleKey);
      if (missingHeldVehicles.length > 0) {
        return `Giữ chỗ chưa hoàn tất cho: ${missingHeldVehicles.join(", ")}. Vui lòng chọn lại khung giờ.`;
      }
    }

    for (const plan of planSummaries) {
      if ((plan.categoryIds || []).length === 0) {
        return `Vui lòng chọn loại dịch vụ cho ${plan.vehicleLabel}.`;
      }
      if ((plan.serviceIds || []).length === 0) {
        return `Vui lòng chọn dịch vụ cụ thể cho ${plan.vehicleLabel}.`;
      }
    }

    if (missingSpecificServiceByVehicle.length > 0) {
      return missingSpecificServiceMessage;
    }

    for (const vehicle of insuranceVehicleOptions) {
      const issue = validateInsuranceSnapshotByMode(insuranceByVehicleKey[vehicle.key], { mode: "booking" });
      if (issue) return `${vehicle.label}: ${issue}`;
    }

    return "";
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    const validationError = validateBeforeSubmit();
    if (validationError) {
      setErrorText(validationError);
      return;
    }

    const vehicleSlotSelections = selectedVehicleKeys
      .map((vehicleKey) => {
        const slot = slotMode === SLOT_MODE.PARALLEL ? parallelSlot : selectedSlotsByVehicle[vehicleKey];
        const selection = normalizeSlotSelection(slot);
        if (!selection.startTime || !selection.endTime) return null;
        const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicleKey));
        return {
          vehicleKey,
          vehicleId: getVehicleIdFromKey(vehicleKey),
          startTime: selection.startTime,
          endTime: selection.endTime,
          bookingFlow: vehicleFlow,
          checkInWindowStart: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? selection.startTime : null,
          checkInWindowEnd: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? selection.endTime : null,
          slotHoldToken: slotHoldTokens?.[vehicleKey] || null
        };
      })
      .filter(Boolean);

    const primarySlot = vehicleSlotSelections[0] || null;
    const primarySlotParts = primarySlot?.startTime ? toDateTimeParts(primarySlot.startTime) : null;
    const vehicleNotePlans = selectedVehicleKeys.map((vehicleKey) => {
      const noteLines = normalizeNoteLines(vehicleNotesByKey?.[vehicleKey]);
      return {
        vehicleKey,
        vehicleId: getVehicleIdFromKey(vehicleKey),
        vehicleLabel: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
        noteLines,
        customerNotes: buildVehicleCustomerNote(noteLines)
      };
    });
    const groupCustomerNotesPayload = (() => {
      const items = vehicleNotePlans
        .filter((plan) => Array.isArray(plan.noteLines) && plan.noteLines.length > 0)
        .map((plan) => ({
          vehicle: plan.vehicleLabel,
          symptoms: plan.noteLines
        }));
      return items.length > 0 ? JSON.stringify({ symptomsByVehicle: items }) : null;
    })();
    const fallbackCustomerNotesPayload =
      selectedVehicleKeys.length === 1
        ? (vehicleNotePlans[0]?.customerNotes || null)
        : groupCustomerNotesPayload;

    try {
      await onSubmit({
        ...formData,
        appointmentDate: primarySlotParts?.appointmentDate || formData.appointmentDate,
        startTime: primarySlotParts?.startTime || formData.startTime,
        customerNotes: fallbackCustomerNotesPayload,
        groupCustomerNotes: groupCustomerNotesPayload,
        customerId: formData.customerId ? Number(formData.customerId) : null,
        selectedVehicleIds: toNumberList(formData.selectedVehicleIds),
        serviceIds: selectedServiceIdsUnion,
        selectedCategoryIds: selectedCategoryIdsUnion,
        slotMode,
        vehicleSlotSelections,
        vehicleNotePlans,
        vehicleServicePlans: planSummaries.map((plan) => ({
          ...(
            insuranceVehicleKeySet.has(plan.vehicleKey)
              ? buildInsuranceSnapshotPayload(insuranceByVehicleKey[plan.vehicleKey], { mode: "booking" })
              : buildInsuranceSnapshotPayload(null, { mode: "booking" })
          ),
          vehicleKey: plan.vehicleKey,
          vehicleId: plan.vehicleId,
          categoryIds: plan.categoryIds,
          serviceIds: plan.serviceIds,
          bookingFlow: plan.bookingFlow,
          totalMinutes: plan.totalMinutes,
          totalPrice: plan.totalPrice
        })),
        servicePlanMode: effectiveServiceMode,
        totalMinutes: Math.max(completionMinutes, 30),
        fleetTotalMinutes: totalMinutes,
        fleetCompletionMinutes: completionMinutes,
        fleetTotalPrice: totalPrice
      });
    } catch (submitError) {
      const message = submitError?.message || "Không thể đặt lịch. Vui lòng thử lại.";
      if (isSlotHoldStaleMessage(message)) {
        const failedVehicles = await refreshSelectedSlotHolds();
        await loadSlots();
        if (Array.isArray(failedVehicles) && failedVehicles.length > 0) {
          setErrorText(
            `Giữ chỗ đã hết hạn và không thể giữ lại cho: ${failedVehicles.join(", ")}. Vui lòng chọn lại khung giờ cho các xe này.`
          );
        } else {
          setErrorText("Thông tin giữ chỗ đã được đồng bộ lại. Vui lòng bấm xác nhận lại.");
        }
      } else {
        setErrorText(message);
      }
    }
  };

  const selectedVehicleCount = selectedVehicleKeys.length;

  return (
    <form id={formId || undefined} ref={formRef} className="admin-form reception-appt-form" onSubmit={handleSubmit}>
      {errorText && typeof onErrorMessage !== "function" ? <div className="reception-error-banner">{errorText}</div> : null}
      <div className="reception-appt-layout">
        <div className="reception-appt-left-column">
          <section className="reception-appt-section">
            <div className="reception-appt-step-head">
              <span className="reception-step-badge">BƯỚC 1</span>
              <label className="reception-appt-quick-toggle" htmlFor="appt-quick-toggle">
                <input
                  id="appt-quick-toggle"
                  type="checkbox"
                  checked={formData.isQuickOrder}
                  onChange={toggleQuickOrder}
                />
                <span>Đặt lịch cho khách vãng lai</span>
              </label>
            </div>
            <h4 className="reception-appt-section-title">
              {!formData.isQuickOrder ? "Khách hàng và xe" : "Khách vãng lai"}
            </h4>

            {!formData.isQuickOrder ? (
              <>
                <div className="form-group">
                  <label>Tìm khách hàng</label>
                  <div className="search-input-wrapper">
                    <input
                      type="text"
                      placeholder="Nhập tên hoặc số điện thoại..."
                      value={search}
                      onChange={(event) => setSearch(event.target.value)}
                      required={!formData.customerId}
                    />
                    {filteredCustomers.length > 0 ? (
                      <div
                        className="dropdown-panel"
                        style={{ position: "absolute", top: "100%", left: 0, right: 0, zIndex: 20 }}
                      >
                        {filteredCustomers.map((customer) => (
                          <div
                            key={customer.id}
                            className="dropdown-item"
                            onClick={() => selectCustomer(customer)}
                          >
                            <strong>{customer.fullName}</strong> - {customer.phone}
                          </div>
                        ))}
                      </div>
                    ) : null}
                  </div>
                </div>

                {formData.customerId ? (
                  <div className="reception-customer-selected-card">
                    <div className="reception-customer-selected-main">
                      <small>KHÁCH ĐÃ CHỌN</small>
                      <strong>{formData.customerName}</strong>
                      <span>{formData.customerPhone}</span>
                    </div>
                  </div>
                ) : null}

                {!formData.isNewVehicle ? (
                  <div className="form-group">
                    <label>Xe của khách (có thể chọn nhiều xe)</label>
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
                          <button
                            type="button"
                            className="reception-open-vehicle-popup-btn"
                            onClick={switchToNewVehicleMode}
                          >
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
                              <div
                                className="reception-vehicle-popup-backdrop"
                                onClick={() => setIsSavedVehiclePopupOpen(false)}
                              >
                                <div
                                  className="reception-vehicle-popup-card"
                                  role="dialog"
                                  aria-modal="true"
                                  aria-label="Chọn xe đã lưu"
                                  onClick={(event) => event.stopPropagation()}
                                >
                                  <div className="reception-vehicle-popup-head">
                                    <div className="reception-vehicle-popup-head-copy">
                                      <h5>Chọn xe đã lưu lại</h5>
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
                      <button
                        type="button"
                        className="reception-open-vehicle-popup-btn is-active"
                        onClick={switchToNewVehicleMode}
                      >
                        Thêm xe mới
                      </button>
                    </div>
                    <div className="form-group">
                      <label>Biển số xe mới *</label>
                      <input
                        type="text"
                        name="vehicleLicensePlate"
                        value={formData.vehicleLicensePlate}
                        onChange={handleChange}
                        onBlur={handlePlateBlur}
                        pattern={VIETNAM_LICENSE_PLATE_PATTERN}
                        title={VIETNAM_LICENSE_PLATE_TITLE}
                        style={{ textTransform: "uppercase" }}
                        placeholder="VD: 30A-12345"
                      />
                    </div>
                    <div className="reception-grid reception-grid-2">
                      <div className="form-group">
                        <label>Hãng xe *</label>
                        <select value={selectedBrandId} onChange={(event) => setSelectedBrandId(event.target.value)}>
                          <option value="">-- Chọn hãng --</option>
                          {brands.map((brand) => (
                            <option key={brand.id} value={brand.id}>
                              {brand.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Dòng xe *</label>
                        <select
                          name="vehicleModelId"
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
                      </div>
                    </div>
                    <div className="reception-grid reception-grid-3">
                      <div className="form-group">
                        <label>Màu sắc</label>
                        <input name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} />
                      </div>
                      <div className="form-group">
                        <label>Năm sản xuất</label>
                        <select name="vehicleYear" value={formData.vehicleYear} onChange={handleChange}>
                          <option value="">-- Chọn năm --</option>
                          {years.map((year) => (
                            <option key={year} value={year}>
                              {year}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="form-group">
                        <label>VIN *</label>
                        <input
                          name="vehicleVin"
                          value={formData.vehicleVin}
                          onChange={handleChange}
                          pattern={VIETNAM_VIN_PATTERN}
                          title={VIETNAM_VIN_TITLE}
                          maxLength={17}
                          style={{ textTransform: "uppercase" }}
                        />
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
              <div className="reception-new-vehicle-box">
                <div className="reception-grid reception-grid-2">
                  <div className="form-group">
                    <label>Tên khách *</label>
                    <input name="guestName" value={formData.guestName} onChange={handleChange} />
                  </div>
                  <div className="form-group">
                    <label>Số điện thoại *</label>
                  <input
                    type="tel"
                    inputMode="tel"
                    name="guestPhone"
                    value={formData.guestPhone}
                    onChange={handleChange}
                    pattern={VN_PHONE_PATTERN}
                    title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
                  />
                  </div>
                </div>
                <div className="form-group">
                  <label>Biển số xe *</label>
                  <input
                    name="guestVehiclePlate"
                    value={formData.guestVehiclePlate}
                    onChange={handleChange}
                    onBlur={handlePlateBlur}
                    pattern={VIETNAM_LICENSE_PLATE_PATTERN}
                    title={VIETNAM_LICENSE_PLATE_TITLE}
                    style={{ textTransform: "uppercase" }}
                  />
                </div>
                <div className="reception-grid reception-grid-2">
                  <div className="form-group">
                    <label>Hãng xe *</label>
                    <select value={guestSelectedBrandId} onChange={(event) => setGuestSelectedBrandId(event.target.value)}>
                      <option value="">-- Chọn hãng --</option>
                      {brands.map((brand) => (
                        <option key={brand.id} value={brand.id}>
                          {brand.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Dòng xe *</label>
                    <select
                      name="guestVehicleModelId"
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
                  </div>
                </div>
                <div className="reception-grid reception-grid-3">
                  <div className="form-group">
                    <label>Màu sắc</label>
                    <input name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} />
                  </div>
                  <div className="form-group">
                    <label>Năm sản xuất</label>
                    <select name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange}>
                      <option value="">-- Chọn năm --</option>
                      {years.map((year) => (
                        <option key={year} value={year}>
                          {year}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>VIN *</label>
                    <input
                      name="guestVehicleVin"
                      value={formData.guestVehicleVin}
                      onChange={handleChange}
                      pattern={VIETNAM_VIN_PATTERN}
                      title={VIETNAM_VIN_TITLE}
                      maxLength={17}
                      style={{ textTransform: "uppercase" }}
                    />
                  </div>
                </div>
              </div>
            )}
          </section>

          <section className="reception-appt-section">
            <div className="reception-appt-step-head reception-appt-step-head-service">
              <span className="reception-step-badge">BƯỚC 2</span>
            </div>
            <h4 className="reception-appt-section-title reception-appt-section-title-service">
              Loại dịch vụ và dịch vụ cụ thể
            </h4>

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
                      effectiveServiceMode === SERVICE_MODE.CUSTOM && activePlanVehicleKey === vehicleKey
                        ? "active"
                        : ""
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
                      ×
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
                          ×
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
                      <div className="reception-muted-note">
                        Không tìm thấy dịch vụ phù hợp trong nhóm đã chọn.
                      </div>
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

            {insuranceVehicleOptions.length > 0 ? (
              <div className="reception-service-subsection">
                <BookingInsurancePanel
                  vehicleOptions={insuranceVehicleOptions}
                  activeVehicleKey={activeInsuranceVehicleKey}
                  onActiveVehicleChange={setActiveInsuranceVehicleKey}
                  value={activeInsuranceSnapshot}
                  onChange={handleInsuranceSnapshotChange}
                  onUploadAsset={handleInsuranceAssetUpload}
                  onRemoveAsset={handleInsuranceAssetRemove}
                  uploadingState={insuranceUploadStateByVehicleKey[activeInsuranceVehicleKey] || {}}
                  companies={insuranceCompanies}
                  companyNameById={insuranceCompanyNameById}
                  contextLabel="tiếp nhận xe"
                  mode="booking"
                />
              </div>
            ) : null}
          </section>
        </div>

        <div className="reception-appt-right-column">
          <section className="reception-appt-section reception-appt-timing-section">
            <div className="reception-appt-step-head">
              <span className="reception-step-badge">BƯỚC 3</span>
            </div>
            <h4 className="reception-appt-section-title">Thông tin lịch hẹn</h4>
            <p className="reception-step-subtitle">Chọn thời gian và mô tả để xác nhận lịch hẹn.</p>

            {hasMultipleSelectedVehicles ? (
              <div className="reception-scope-switch reception-slot-mode-switch">
                <button
                  type="button"
                  className={slotMode === SLOT_MODE.PARALLEL ? "active" : ""}
                  onClick={() => {
                    void handleSlotModeChange(SLOT_MODE.PARALLEL);
                  }}
                >
                  Song song
                </button>
                <button
                  type="button"
                  className={slotMode === SLOT_MODE.INDIVIDUAL ? "active" : ""}
                  onClick={() => {
                    void handleSlotModeChange(SLOT_MODE.INDIVIDUAL);
                  }}
                >
                  Riêng lẻ
                </button>
              </div>
            ) : null}

            {slotMode === SLOT_MODE.INDIVIDUAL && selectedVehicleKeys.length > 1 ? (
              <div className="reception-selected-tabs reception-vehicle-tabs-layout">
                {selectedVehicleKeys.map((vehicleKey) => (
                  <div
                    key={`slot-mode-${vehicleKey}`}
                    className={`reception-selected-tab ${activeSlotVehicleKey === vehicleKey ? "active" : ""}`}
                  >
                    <button
                      type="button"
                      className="reception-selected-tab-main"
                      onClick={() => setActiveSlotVehicleKey(vehicleKey)}
                    >
                      {toVehicleTabLabel(vehicleLabelByKey.get(vehicleKey), vehicleKey)}
                    </button>
                  </div>
                ))}
              </div>
            ) : null}

            <div className="form-group">
              <label>Ngày đặt lịch *</label>
              <DateTimePicker
                value={effectiveSelectedDate ? `${effectiveSelectedDate}T08:00:00` : ""}
                onChange={(value) => {
                  void handleDateChange(value);
                }}
                minDate={new Date()}
                showTime={false}
                iconColor="#2f6bd8"
                onOpenRequest={() => {
                  if (dateSelectionGuardMessage) {
                    setErrorText(dateSelectionGuardMessage);
                    return false;
                  }
                  return true;
                }}
                placeholder="Chọn ngày..."
              />
              {appointmentSummary ? <div className="reception-muted-note">{appointmentSummary}</div> : null}
            </div>

            <div className="slot-section reception-slot-section">
              {!Number(branchId) ? (
                <div className="booking-empty">Vui lòng chọn đúng chi nhánh làm việc trước khi chọn giờ.</div>
              ) : !effectiveSelectedDate ? (
                <div className="booking-empty">
                  {slotMode === SLOT_MODE.INDIVIDUAL
                    ? "Vui lòng chọn ngày cho xe đang thao tác trước khi chọn giờ."
                    : "Vui lòng chọn ngày trước khi chọn giờ."}
                </div>
              ) : slotsLoading ? (
                <div className="booking-empty">Đang tải khung giờ...</div>
              ) : (
                <>
                  <p className={`slot-flow-note ${effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE ? "checkin" : "repair"}`}>
                    {slotSelectionGuide}
                  </p>
                  <div className="session-tabs">
                    {SESSION_TABS.map((tab) => (
                      <button
                        key={tab.key}
                        type="button"
                        className={`session-tab ${activeSession === tab.key ? "active" : ""}`}
                        onClick={() => setActiveSession(tab.key)}
                      >
                        <strong>{tab.label}</strong>
                        <small>{tab.hint}</small>
                      </button>
                    ))}
                  </div>

                  {(slotsBySession[activeSession] || []).length > 0 ? (
                    <div className="slot-actions">
                      <button
                        type="button"
                        className="slot-expand-toggle"
                        onClick={() => toggleOddSlotsForSession(activeSession)}
                      >
                        {showOddSlots[activeSession] ? "Ẩn" : "Xem thêm"}
                      </button>
                    </div>
                  ) : null}

                  <div className="slot-grid">
                    {displayedSlots.map((slot) => {
                      const start = readField(slot, ["startTime", "StartTime"], "");
                      const end = readField(slot, ["endTime", "EndTime"], "");
                      const selectedStart = readField(chosenSlot, ["startTime", "StartTime"], "");
                      const selectedEnd = readField(chosenSlot, ["endTime", "EndTime"], "");
                      const isSelected = selectedStart === start && selectedEnd === end;
                      const remaining = Number(readField(slot, ["remainingCapacity", "RemainingCapacity"], 0) || 0);
                      const hasCapacity = remaining > 0;

                      return (
                        <button
                          key={`${start}-${end}`}
                          type="button"
                          className={`slot-card ${isSelected ? "selected" : ""}`}
                          onClick={() => {
                            void handleSelectSlot(slot);
                          }}
                        >
                          <strong className="slot-time">
                            {timeLabel(start)} - {timeLabel(end)}
                          </strong>
                          <div className="slot-meta">
                            <span className={`slot-state-dot ${hasCapacity ? "ok" : "full"}`} />
                            <span className={`slot-state-text ${hasCapacity ? "ok" : "full"}`}>
                              {hasCapacity ? `Còn ${remaining} chỗ` : "Hết chỗ"}
                            </span>
                          </div>
                        </button>
                      );
                    })}
                  </div>

                  {displayedSlots.length === 0 ? (
                    <div className="booking-empty">Không có khung giờ phù hợp cho ngày đã chọn.</div>
                  ) : null}
                </>
              )}
            </div>

            <div className="form-group">
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
                            ×
                          </button>
                        </div>
                      ))}
                    </div>
                  ) : null}

                  <div className="reception-note-list">
                    {activeNoteVehicleLines.map((noteValue, index) => (
                      <textarea
                        key={`note-${activeNoteKey || "none"}-${index}`}
                        className="reception-note-input"
                        rows={1}
                        value={noteValue}
                        placeholder={`Nhập mô tả ${index + 1}...`}
                        onChange={(event) => onVehicleNoteChange(activeNoteKey, index, event.target.value)}
                      />
                    ))}
                  </div>
                  {selectedVehicleKeys.length > 1 ? (
                    <p className="reception-muted-note">Chọn xe ở tab bên trên để nhập ghi chú theo từng xe.</p>
                  ) : null}
                </div>
              ) : (
                <div className="reception-muted-note">Hãy chọn xe trước khi nhập mô tả.</div>
              )}
            </div>

            <div className="reception-summary-inline" role="status" aria-live="polite">
              <div className="reception-summary-inline-row">
                <span>Thời gian dự kiến hoàn tất toàn bộ xe</span>
                <strong>{fleetCompletionDateTimeLabel}</strong>
              </div>
              <div className="reception-summary-inline-row">
                <span>Tổng giá dịch vụ tất cả xe</span>
                <strong>{formatCurrency(totalPrice)}</strong>
              </div>
              <div className="reception-summary-inline-row">
                <span>Số xe</span>
                <strong>
                  {selectedVehicleCount} {selectedVehicleCount > 1 ? "(lịch nhóm)" : "(lịch thường)"}
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
                        {`${plan.serviceIds.length} dịch vụ`}
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
                        <strong>{`${plan.totalMinutes} phút`}</strong>
                      </div>
                    </div>

                    {plan.bookingFlow === BOOKING_FLOW.FLEXIBLE && plan.flowWarningLabel ? (
                      <p className="reception-plan-summary-warning">
                        {`* ${plan.flowWarningLabel}`}
                      </p>
                    ) : null}

                    <div className="reception-plan-summary-footer">
                      <span>Tổng giá dịch vụ</span>
                      <strong>{formatCurrency(plan.totalPrice)}</strong>
                    </div>
                  </article>
                )})}
              </div>
            ) : null}
          </section>
        </div>
      </div>

      {showFooterActions ? (
        <div className="form-actions reception-form-actions">
          <button type="button" className="btn-ghost" onClick={onCancel}>
            Hủy
          </button>
          <button type="submit" className="btn-primary" disabled={loading}>
            {loading ? "Đang xử lý..." : submitLabel}
          </button>
        </div>
      ) : null}
    </form>
  );
}
