import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";
import { WORKING_DAY_MINUTES } from "@features/appointments/utils/bookingFlowRules";
import { addMinutesWithinWorkingHours } from "@shared/utils/workingHoursSchedule";
import { splitBookingItemIds } from "@features/appointments/utils/bookingPackages";
import { getInspectionTriadDisplayName } from "@shared/utils/serviceDurationUtils";

export const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM",
};

export const EMPTY_PLAN = {
  categoryIds: [],
  serviceIds: [],
  excludedSharedCategoryIds: [],
  excludedSharedServiceIds: [],
};

export const VEHICLE_KEY_GUEST = "guest";
export const VEHICLE_KEY_NEW = "new-customer";
export const VN_PHONE_PATTERN = "^(0[0-9]{9}|\\+84[0-9]{9})$";

export const EMPTY_FORM_DATA = {
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

export function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id !== 0)
    )
  );
}

export function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
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

export function validateVietnamesePhone(phone) {
  return /^(0\d{9}|\+84\d{9})$/.test(normalizePhone(phone));
}

export function getVehicleKeyFromId(vehicleId) {
  return `existing-${Number(vehicleId)}`;
}

export function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : null;
}

export function normalizePlan(plan) {
  const categoryIds = toNumberList(plan?.categoryIds || []);
  const serviceIds = toNumberList(plan?.serviceIds || []);
  const excludedSharedCategoryIds = toNumberList(plan?.excludedSharedCategoryIds || []);
  const excludedSharedServiceIds = toNumberList(plan?.excludedSharedServiceIds || []);
  return { categoryIds, serviceIds, excludedSharedCategoryIds, excludedSharedServiceIds };
}

export function clonePlan(plan) {
  return {
    categoryIds: [...(plan?.categoryIds || [])],
    serviceIds: [...(plan?.serviceIds || [])],
    excludedSharedCategoryIds: [...(plan?.excludedSharedCategoryIds || [])],
    excludedSharedServiceIds: [...(plan?.excludedSharedServiceIds || [])],
  };
}

export function getMissingSpecificServiceByVehicle(planSummaries, serviceCategoryMap, categoryNameById) {
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

export function buildMissingSpecificServiceMessage(missingByVehicle) {
  if (!Array.isArray(missingByVehicle) || missingByVehicle.length === 0) return "";

  const details = missingByVehicle.map((item) => {
    const categories = (item.missingCategoryNames || []).map((name) => `'${name}'`).join(", ");
    return `${item.vehicleLabel}: ${categories}`;
  });

  return `Vui lòng chọn dịch vụ cụ thể cho từng loại dịch vụ trước khi chọn khung giờ. Thiếu trên các xe: ${details.join("; ")}.`;
}

export function normalizeService(service) {
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

  (normalizedServices || []).forEach((service) => {
    if (!Number.isFinite(service.categoryId) || service.categoryId <= 0) return;
    if (!map.has(service.categoryId)) {
      map.set(service.categoryId, { id: service.categoryId, name: `Nhóm ${service.categoryId}` });
    }
  });

  return Array.from(map.values());
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

export function toVehicleTabLabel(vehicleLabel, fallback = "") {
  const normalizedLabel = String(vehicleLabel || "").trim();
  if (!normalizedLabel) {
    const normalizedFallback = String(fallback || "").trim();
    return normalizedFallback || "Không biển số";
  }

  const [platePart] = normalizedLabel.split(" - ");
  const plate = String(platePart || "").trim();
  return plate || normalizedLabel;
}

export function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

export function parsePositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

export function formatDateTimeValue(value) {
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

export function formatTimeWithDateInBrackets(value) {
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



export function toMinuteEpoch(value) {
  if (!value) return null;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;
  parsed.setSeconds(0, 0);
  return parsed.getTime();
}

export function toLocalDateTimeValue(value) {
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

export function formatSlotTime(value) {
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

export function toDateKey(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function buildCheckInTimeWindowMap(now, rangeMinutes = 30) {
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

export function hasPlanCompletionWarning(plan) {
  if (!plan) return false;
  return Boolean(plan.bookingFlow === BOOKING_FLOW.FLEXIBLE && Number(plan.executionMinutes || 0) > 0 && plan.completionWarningText);
}

