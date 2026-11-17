import { getInspectionTriadDisplayName } from "@shared/utils/serviceDurationUtils";

export const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM"
};

export const SLOT_MODE = {
  PARALLEL: "PARALLEL",
  INDIVIDUAL: "INDIVIDUAL"
};

export const EMPTY_PLAN = {
  categoryIds: [],
  serviceIds: [],
  excludedSharedCategoryIds: [],
  excludedSharedServiceIds: []
};

export const EMPTY_FORM_DATA = {
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

export const VN_PHONE_PATTERN = "^(0[0-9]{9}|\\+84[0-9]{9})$";
export const SLOT_DURATION_MINUTES = 15;
export const CHECK_IN_SETUP_MINUTES = 15;
export const EXECUTION_BUFFER_MINUTES = 15;

export function resolveSlotDateTime(rawValue, fallbackDate = "") {
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

export function buildFixedFlowSchedule(startTimeRaw, serviceMinutes, fallbackDate = "") {
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

export function normalizePhone(phone) {
  return String(phone || "").replace(/\s+/g, "");
}

export function normalizeSearchText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

export function isValidPhone(phone) {
  return /^(0\d{9}|\+84\d{9})$/.test(normalizePhone(phone));
}

export function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .trim()
    .toLowerCase();
}

export function toDateTimeParts(value) {
  if (!value) return { appointmentDate: "", startTime: "" };
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return { appointmentDate: "", startTime: "" };
  const appointmentDate = `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(
    parsed.getDate()
  ).padStart(2, "0")}`;
  const startTime = `${String(parsed.getHours()).padStart(2, "0")}:${String(parsed.getMinutes()).padStart(2, "0")}`;
  return { appointmentDate, startTime };
}

export function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id > 0)
    )
  );
}

export function toSelectableItemIdList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id !== 0)
    )
  );
}

export function parseServiceIdsFromInput(value) {
  if (Array.isArray(value)) return toSelectableItemIdList(value);
  if (Number.isFinite(Number(value)) && Number(value) !== 0) return [Number(value)];

  return toSelectableItemIdList(
    String(value || "")
      .split(/[,|]/g)
      .map((item) => Number(String(item || "").trim()))
      .filter((id) => Number.isFinite(id) && id !== 0)
  );
}

export function parseInitialNoteText(value) {
  const raw = String(value || "").trim();
  if (!raw) return "";

  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed?.symptoms)) {
      return parsed.symptoms.map((item) => String(item || "").trim()).filter(Boolean).join("; ");
    }
    if (Array.isArray(parsed?.symptomsByVehicle)) {
      const firstWithSymptoms = parsed.symptomsByVehicle.find(
        (item) => Array.isArray(item?.symptoms) && item.symptoms.length > 0
      );
      if (firstWithSymptoms) {
        return firstWithSymptoms.symptoms.map((item) => String(item || "").trim()).filter(Boolean).join("; ");
      }
    }
  } catch {
    // Fallback to plain text parsing.
  }

  return raw;
}

export function buildPlanFromServiceIds(serviceIds, serviceCategoryMap) {
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

export function buildInitialFormData(initialValues) {
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

export function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

export function normalizePlan(plan) {
  const categoryIds = toNumberList(plan?.categoryIds || []);
  const serviceIds = toSelectableItemIdList(plan?.serviceIds || []);
  const excludedSharedCategoryIds = toNumberList(plan?.excludedSharedCategoryIds || []);
  const excludedSharedServiceIds = toSelectableItemIdList(plan?.excludedSharedServiceIds || []);
  return { categoryIds, serviceIds, excludedSharedCategoryIds, excludedSharedServiceIds };
}

export function normalizeService(service) {
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

export function getVehicleKeyFromId(vehicleId) {
  return `existing-${Number(vehicleId)}`;
}

export function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : null;
}

export function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

export function formatSavedVehicleDescriptor(vehicle, brandNameById = new Map(), modelInfoById = new Map()) {
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

export function toVehicleTabLabel(vehicleLabel, fallback = "") {
  const normalizedLabel = String(vehicleLabel || "").trim();
  if (!normalizedLabel) {
    const normalizedFallback = String(fallback || "").trim();
    return normalizedFallback || "Kh\u00f4ng bi\u1ec3n s\u1ed1";
  }

  const [platePart] = normalizedLabel.split(" - ");
  const plate = String(platePart || "").trim();
  return plate || normalizedLabel;
}

export function toIsoDate(value) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  return `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(
    parsed.getDate()
  ).padStart(2, "0")}`;
}

export function toTimeValue(value) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  return `${String(parsed.getHours()).padStart(2, "0")}:${String(parsed.getMinutes()).padStart(2, "0")}`;
}

export function formatDateTimeWithFullDate(value) {
  const parsed = value instanceof Date ? value : new Date(value);
  if (!(parsed instanceof Date) || Number.isNaN(parsed.getTime())) return "Chưa chọn";

  const hour = String(parsed.getHours()).padStart(2, "0");
  const minute = String(parsed.getMinutes()).padStart(2, "0");
  const day = String(parsed.getDate());
  const month = String(parsed.getMonth() + 1);
  const year = String(parsed.getFullYear());
  return `${hour}:${minute} (${day}/${month}/${year})`;
}

export function formatCompletionWarningTime(value) {
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

export function normalizeSlotSelection(slot) {
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

export function buildCategoryOptions(serviceCategories, normalizedServices) {
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

export function clonePlan(plan) {
  return {
    categoryIds: [...(plan?.categoryIds || [])],
    serviceIds: [...(plan?.serviceIds || [])],
    excludedSharedCategoryIds: [...(plan?.excludedSharedCategoryIds || [])],
    excludedSharedServiceIds: [...(plan?.excludedSharedServiceIds || [])]
  };
}

export function getMissingSpecificServiceByVehicle(planSummaries, serviceCategoryMap, categoryNameById) {
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

export function buildMissingSpecificServiceMessage(missingByVehicle) {
  if (!Array.isArray(missingByVehicle) || missingByVehicle.length === 0) return "";

  const details = missingByVehicle.map((item) => {
    const categories = (item.missingCategoryNames || []).map((name) => `'${name}'`).join(", ");
    return `${item.vehicleLabel}: ${categories}`;
  });

  return `Vui lòng chọn dịch vụ cụ thể cho từng loại dịch vụ trước khi chọn khung giờ. Thiếu trên các xe: ${details.join("; ")}.`;
}
