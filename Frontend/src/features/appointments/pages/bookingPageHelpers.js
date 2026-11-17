import {
  extractField,
  formatStoredVehicleLabel,
  normalizeText,
} from "@features/appointments/utils/bookingCommon";
import {
  addMinutesWithinWorkingHours,
  calculateRoundedOccupancyMinutes,
} from "@shared/utils/workingHoursSchedule";

export const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM",
};

export const SLOT_MODE = {
  PARALLEL: "PARALLEL",
  INDIVIDUAL: "INDIVIDUAL",
};

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
      roundedTotalMinutes: 0,
    };
  }

  const roundedTotalMinutes = calculateRoundedOccupancyMinutes(serviceMinutes);
  const completionDate = addMinutesWithinWorkingHours(startDate, roundedTotalMinutes, {
    openHour: 8,
    closeHour: 20,
  });
  const completionMs = completionDate ? completionDate.getTime() : null;

  return {
    startDate,
    endDate: completionDate,
    completionMs,
    roundedTotalMinutes,
  };
}

export function normalizeSearchText(value) {
  return normalizeText(value)
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

export function buildBookingSuccessMessage() {
  return "Đặt lịch thành công. Chúng tôi sẽ xác nhận sớm nhất.";
}

export function buildServiceHierarchyByCategory(vehicleServices, categoryNameById) {
  const grouped = new Map();
  (vehicleServices || []).forEach((service) => {
    const categoryId = Number(extractField(service, ["categoryId", "CategoryId"]));
    const categoryName = categoryNameById.get(categoryId) || "Dịch vụ khác";
    const serviceName = String(extractField(service, ["name", "Name"], "") || "").trim();
    if (!serviceName) return;

    if (!grouped.has(categoryName)) {
      grouped.set(categoryName, []);
    }
    grouped.get(categoryName).push(serviceName);
  });

  return Array.from(grouped.entries()).map(([categoryName, serviceNames]) => ({
    categoryName,
    serviceNames,
  }));
}

export const INITIAL_MULTI = {
  vehicles: [],
  sharedCategoryIds: [],
  sharedServiceIds: [],
  serviceMode: SERVICE_MODE.SHARED,
  slotMode: SLOT_MODE.PARALLEL,
  parallelSlot: null,
  selectedSlots: {},
  slotHoldTokens: {},
};

export function uniqIds(values) {
  return Array.from(new Set((values || []).map(Number).filter(Number.isFinite)));
}

export function multiReducer(state, action) {
  switch (action.type) {
    case "TOGGLE_EXISTING": {
      const index = state.vehicles.findIndex((item) => item.key === action.payload.key);
      const nextVehicles = [...state.vehicles];
      if (index >= 0) {
        nextVehicles.splice(index, 1);
      } else {
        nextVehicles.push(action.payload);
      }
      const nextSlots = { ...state.selectedSlots };
      Object.keys(nextSlots).forEach((key) => {
        if (!nextVehicles.some((item) => item.key === key)) delete nextSlots[key];
      });
      return { ...state, vehicles: nextVehicles, selectedSlots: nextSlots };
    }
    case "ADD_NEW":
      if (state.vehicles.some((item) => item.key === action.payload.key)) return state;
      return { ...state, vehicles: [...state.vehicles, action.payload] };
    case "ADD_EXISTING":
      if (state.vehicles.some((item) => item.key === action.payload.key)) return state;
      return { ...state, vehicles: [...state.vehicles, action.payload] };
    case "REMOVE_VEHICLE": {
      const nextSlots = { ...state.selectedSlots };
      delete nextSlots[action.payload];
      const nextTokens = { ...state.slotHoldTokens };
      delete nextTokens[action.payload];
      return {
        ...state,
        vehicles: state.vehicles.filter((item) => item.key !== action.payload),
        selectedSlots: nextSlots,
        slotHoldTokens: nextTokens,
      };
    }
    case "SET_SERVICE_MODE":
      return { ...state, serviceMode: action.payload };
    case "SET_SHARED_CATEGORIES":
      return { ...state, sharedCategoryIds: uniqIds(action.payload) };
    case "SET_SHARED_SERVICES":
      return { ...state, sharedServiceIds: uniqIds(action.payload) };
    case "PATCH_VEHICLE":
      return {
        ...state,
        vehicles: state.vehicles.map((item) => (item.key === action.payload.key ? { ...item, ...action.payload.patch } : item)),
      };
    case "SET_SLOT_MODE":
      return {
        ...state,
        slotMode: action.payload,
        parallelSlot: action.payload === SLOT_MODE.PARALLEL ? state.parallelSlot : null,
      };
    case "SET_PARALLEL_SLOT":
      return { ...state, parallelSlot: action.payload };
    case "SET_INDIVIDUAL_SLOT":
      return {
        ...state,
        selectedSlots: { ...state.selectedSlots, [action.payload.key]: action.payload.slot },
      };
    case "CLEAR_INDIVIDUAL_SLOT": {
      const nextSlots = { ...state.selectedSlots };
      delete nextSlots[action.payload];
      const nextTokens = { ...state.slotHoldTokens };
      delete nextTokens[action.payload];
      return { ...state, selectedSlots: nextSlots, slotHoldTokens: nextTokens };
    }
    case "SET_SLOT_HOLD_TOKEN":
      return {
        ...state,
        slotHoldTokens: { ...state.slotHoldTokens, [action.payload.key]: action.payload.token },
      };
    case "CLEAR_SLOT_HOLD_TOKEN": {
      const nextTokens = { ...state.slotHoldTokens };
      delete nextTokens[action.payload];
      return { ...state, slotHoldTokens: nextTokens };
    }
    case "RESET_SLOTS":
      return { ...state, parallelSlot: null, selectedSlots: {}, slotHoldTokens: {} };
    default:
      return state;
  }
}

export function asVehicleEntryFromExisting(vehicle) {
  const id = Number(extractField(vehicle, ["id", "Id"]));
  return {
    key: `existing-${id}`,
    source: "existing",
    displayLabel: formatStoredVehicleLabel(vehicle),
    payload: {
      vehicleId: id,
      vehicleModelId: Number(extractField(vehicle, ["modelId", "ModelId"])) || null,
      licensePlate: extractField(vehicle, ["licensePlate", "LicensePlate"], null),
      vin: extractField(vehicle, ["vin", "Vin"], null),
      color: extractField(vehicle, ["color", "Color"], null),
      manufactureYear: Number(extractField(vehicle, ["manufactureYear", "ManufactureYear"])) || null,
    },
    categoryIds: [],
    serviceIds: [],
    excludedSharedCategoryIds: [],
    excludedSharedServiceIds: [],
  };
}
