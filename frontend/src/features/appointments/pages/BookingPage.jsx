import { Fragment, useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import Header from "@shared/components/Header";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import VehicleActionPanel from "@features/appointments/components/VehicleActionPanel";
import {
  BOOKING_FLOW,
  BOOKING_STEPS,
  DEFAULT_DIAGNOSTIC_SERVICE_CODE,
  INITIAL_NEW_VEHICLE_FORM,
  INITIAL_ODD_SLOT_VISIBILITY,
  SESSION_TABS,
} from "@features/appointments/constants/bookingConstants";
import {
  createBookingSlotHold,
  loadBookingAvailableSlots,
  loadBookingFormMasterData,
  releaseBookingSlotHold,
  submitBatchBooking,
  submitSingleBooking,
} from "@features/appointments/api/bookingFormApi";
import {
  buildCustomerNotesPayload,
  extractField,
  formatLicensePlateInput,
  formatStoredVehicleLabel,
  formatVehicleDescriptor,
  formatVnd,
  iconByIndex,
  normalizeText,
  timeLabel,
  toIsoDateInput,
} from "@features/appointments/utils/bookingCommon";
import {
  buildDisplayedSlots,
  buildSlotsBySession,
  getAutoExpandOddStateForFixedFlow,
  resolveFirstActiveSession,
} from "@features/appointments/utils/bookingSlots";
import { buildCurrentVehiclePayload } from "@features/appointments/utils/bookingVehicle";
import "./BookingPage.css";

const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM",
};

const SLOT_MODE = {
  PARALLEL: "PARALLEL",
  INDIVIDUAL: "INDIVIDUAL",
};

const FLEXIBLE_CHECKIN_WINDOW_MINUTES = 15;

function normalizeSearchText(value) {
  return normalizeText(value)
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

const INITIAL_MULTI = {
  vehicles: [],
  sharedCategoryIds: [],
  sharedServiceIds: [],
  serviceMode: SERVICE_MODE.SHARED,
  slotMode: SLOT_MODE.PARALLEL,
  parallelSlot: null,
  selectedSlots: {},
  slotHoldTokens: {},
};

function uniqIds(values) {
  return Array.from(new Set((values || []).map(Number).filter(Number.isFinite)));
}

function multiReducer(state, action) {
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
    case "REMOVE_VEHICLE": {
      const nextSlots = { ...state.selectedSlots };
      delete nextSlots[action.payload];
      const nextTokens = { ...state.slotHoldTokens };
      delete nextTokens[action.payload];
      return { ...state, vehicles: state.vehicles.filter((item) => item.key !== action.payload), selectedSlots: nextSlots, slotHoldTokens: nextTokens };
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

function asVehicleEntryFromExisting(vehicle) {
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
    },
    categoryIds: [],
    serviceIds: [],
    excludedSharedCategoryIds: [],
    excludedSharedServiceIds: [],
  };
}

export default function BookingPage() {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [success, setSuccess] = useState("");
  const [currentStep, setCurrentStep] = useState(1);
  const alertAnchorRef = useRef(null);
  const [alertTick, setAlertTick] = useState(0);

  const [categories, setCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [branches, setBranches] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [vehicles, setVehicles] = useState([]);

  const [customerForm, setCustomerForm] = useState({ fullName: "", phone: "" });
  const [draftVehicle, setDraftVehicle] = useState({ ...INITIAL_NEW_VEHICLE_FORM });
  const [vehicleSymptoms, setVehicleSymptoms] = useState({});
  const [activeSymptomVehicleKey, setActiveSymptomVehicleKey] = useState("");

  const [bookingFlow, setBookingFlow] = useState(BOOKING_FLOW.FIXED);
  const [selectedBranchId, setSelectedBranchId] = useState("");
  const [selectedDate, setSelectedDate] = useState(toIsoDateInput(new Date()));
  const [selectedDatesByVehicle, setSelectedDatesByVehicle] = useState({});
  const [activeVehicleKey, setActiveVehicleKey] = useState("");
  const [activeCategoryId, setActiveCategoryId] = useState("");
  const [activeSession, setActiveSession] = useState("MORNING");
  const [step2VehicleKey, setStep2VehicleKey] = useState("");
  const [step3VehicleKey, setStep3VehicleKey] = useState("");
  const [showOddSlots, setShowOddSlots] = useState({ ...INITIAL_ODD_SLOT_VISIBILITY });
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [selectingSlot, setSelectingSlot] = useState(false);
  const [slots, setSlots] = useState([]);

  const [multi, dispatch] = useReducer(multiReducer, INITIAL_MULTI);

  const showError = useCallback((message) => {
    setError(message);
    if (message) setNotice("");
    setAlertTick((tick) => tick + 1);
  }, []);

  const clearError = useCallback(() => {
    setError("");
  }, []);

  const showNotice = useCallback((message) => {
    setNotice(message);
    setAlertTick((tick) => tick + 1);
  }, []);

  const clearNotice = useCallback(() => {
    setNotice("");
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", { replace: true, state: { from: { pathname: "/booking/form" } } });
    }
  }, [isAuthenticated, navigate]);

  const customerRecord = useMemo(() => {
    const userId = Number(user?.id);
    if (Number.isFinite(userId)) {
      const byId = customers.find((item) => Number(item?.id) === userId || Number(item?.Id) === userId);
      if (byId) return byId;
    }
    const userPhone = normalizeText(user?.phone);
    if (userPhone) {
      const byPhone = customers.find((item) => normalizeText(item?.phone || item?.Phone) === userPhone);
      if (byPhone) return byPhone;
    }
    const userEmail = normalizeText(user?.email);
    if (userEmail) {
      const byEmail = customers.find((item) => normalizeText(item?.email || item?.Email) === userEmail);
      if (byEmail) return byEmail;
    }
    return null;
  }, [customers, user?.email, user?.id, user?.phone]);

  const linkedVehicles = useMemo(() => {
    const customerId = Number(extractField(customerRecord, ["id", "Id"], user?.id));
    if (!Number.isFinite(customerId)) return [];
    const relationIds = new Set(
      (customerVehicles || [])
        .filter((item) => Number(extractField(item, ["customerId", "CustomerId"])) === customerId)
        .map((item) => Number(extractField(item, ["vehicleId", "VehicleId"])))
    );
    return (vehicles || []).filter((item) => relationIds.has(Number(extractField(item, ["id", "Id"]))));
  }, [customerRecord, customerVehicles, user?.id, vehicles]);

  const serviceCategoryById = useMemo(() => {
    const map = new Map();
    (services || []).forEach((service) => {
      map.set(Number(extractField(service, ["id", "Id"])), Number(extractField(service, ["categoryId", "CategoryId"])));
    });
    return map;
  }, [services]);

  const diagnosticService = useMemo(() => {
    const byCode = services.find((item) => normalizeText(extractField(item, ["code", "Code"], "")) === normalizeText(DEFAULT_DIAGNOSTIC_SERVICE_CODE));
    if (byCode) return byCode;
    return services.find((item) => normalizeSearchText(extractField(item, ["name", "Name"], "")).includes("kiem tra tong quat"));
  }, [services]);
  const diagnosticServiceId = useMemo(() => Number(extractField(diagnosticService, ["id", "Id"])), [diagnosticService]);
  const flexibleTriggerServiceIds = useMemo(
    () =>
      (services || [])
        .filter((item) => {
          const raw = normalizeSearchText(extractField(item, ["name", "Name"], ""));
          return raw.includes("kiem tra tong quat") || raw.includes("kiem tra nhanh");
        })
        .map((item) => Number(extractField(item, ["id", "Id"])))
        .filter(Number.isFinite),
    [services]
  );
  const flexibleTriggerServiceIdSet = useMemo(
    () => new Set((flexibleTriggerServiceIds || []).map(Number).filter(Number.isFinite)),
    [flexibleTriggerServiceIds]
  );
  const getEffectiveCategoryIds = useCallback((vehicle) => {
    if (!vehicle) return [];
    if (multi.serviceMode === SERVICE_MODE.CUSTOM) return uniqIds(vehicle.categoryIds || []);
    const shared = (multi.sharedCategoryIds || []).filter((id) => !(vehicle.excludedSharedCategoryIds || []).includes(id));
    return uniqIds([...shared, ...(vehicle.categoryIds || [])]);
  }, [multi.serviceMode, multi.sharedCategoryIds]);

  const getEffectiveServiceIds = useCallback((vehicle) => {
    if (!vehicle) return [];
    if (multi.serviceMode === SERVICE_MODE.CUSTOM) return uniqIds(vehicle.serviceIds || []);
    const shared = (multi.sharedServiceIds || []).filter((id) => !(vehicle.excludedSharedServiceIds || []).includes(id));
    return uniqIds([...shared, ...(vehicle.serviceIds || [])]);
  }, [multi.serviceMode, multi.sharedServiceIds]);

  const getVehicleFlow = useCallback((vehicle) => {
    if (!vehicle) return BOOKING_FLOW.FIXED;
    const serviceIds = getEffectiveServiceIds(vehicle);
    return serviceIds.some((id) => flexibleTriggerServiceIdSet.has(Number(id)))
      ? BOOKING_FLOW.FLEXIBLE
      : BOOKING_FLOW.FIXED;
  }, [flexibleTriggerServiceIdSet, getEffectiveServiceIds]);

  const hasAnyFlexibleVehicle = useMemo(
    () => (multi.vehicles || []).some((vehicle) => getVehicleFlow(vehicle) === BOOKING_FLOW.FLEXIBLE),
    [getVehicleFlow, multi.vehicles]
  );
  const activeVehicle = useMemo(() => multi.vehicles.find((item) => item.key === activeVehicleKey) || multi.vehicles[0] || null, [activeVehicleKey, multi.vehicles]);
  const step2Vehicle = useMemo(
    () => multi.vehicles.find((item) => item.key === step2VehicleKey) || null,
    [multi.vehicles, step2VehicleKey]
  );
  const step3Vehicle = useMemo(
    () => multi.vehicles.find((item) => item.key === step3VehicleKey) || null,
    [multi.vehicles, step3VehicleKey]
  );
  const activeSymptomVehicle = useMemo(
    () => multi.vehicles.find((item) => item.key === activeSymptomVehicleKey) || multi.vehicles[0] || null,
    [activeSymptomVehicleKey, multi.vehicles]
  );

  useEffect(() => {
    setVehicleSymptoms((prev) => {
      const next = {};
      (multi.vehicles || []).forEach((vehicle) => {
        const key = vehicle.key;
        next[key] = Array.isArray(prev[key]) && prev[key].length > 0 ? prev[key] : [""];
      });
      return next;
    });
  }, [multi.vehicles]);

  useEffect(() => {
    if (!activeSymptomVehicleKey && multi.vehicles.length > 0) {
      setActiveSymptomVehicleKey(multi.vehicles[0].key);
      return;
    }
    if (activeSymptomVehicleKey && !multi.vehicles.some((item) => item.key === activeSymptomVehicleKey)) {
      setActiveSymptomVehicleKey(multi.vehicles[0]?.key || "");
    }
  }, [activeSymptomVehicleKey, multi.vehicles]);

  useEffect(() => {
    if (!activeVehicleKey && multi.vehicles.length > 0) {
      setActiveVehicleKey(multi.vehicles[0].key);
      return;
    }
    if (activeVehicleKey && !multi.vehicles.some((item) => item.key === activeVehicleKey)) {
      setActiveVehicleKey(multi.vehicles[0]?.key || "");
    }
  }, [activeVehicleKey, multi.vehicles]);

  useEffect(() => {
    setSelectedDatesByVehicle((prev) => {
      const next = {};
      (multi.vehicles || []).forEach((vehicle) => {
        if (typeof prev[vehicle.key] === "string") {
          next[vehicle.key] = prev[vehicle.key];
        }
      });
      return next;
    });
  }, [multi.vehicles]);

  useEffect(() => {
    if (step2VehicleKey && !multi.vehicles.some((item) => item.key === step2VehicleKey)) {
      setStep2VehicleKey("");
    }
  }, [step2VehicleKey, multi.vehicles]);

  useEffect(() => {
    if (step3VehicleKey && !multi.vehicles.some((item) => item.key === step3VehicleKey)) {
      setStep3VehicleKey("");
    }
  }, [step3VehicleKey, multi.vehicles]);

  useEffect(() => {
    if (currentStep === 2 && !step2VehicleKey && multi.serviceMode !== SERVICE_MODE.SHARED) {
      dispatch({ type: "SET_SERVICE_MODE", payload: SERVICE_MODE.SHARED });
    }
  }, [currentStep, multi.serviceMode, step2VehicleKey]);

  useEffect(() => {
    if (currentStep === 3 && !step3VehicleKey && multi.serviceMode !== SERVICE_MODE.SHARED) {
      dispatch({ type: "SET_SERVICE_MODE", payload: SERVICE_MODE.SHARED });
    }
  }, [currentStep, multi.serviceMode, step3VehicleKey]);

  const getBookedServiceIds = useCallback((vehicle) => {
    if (!vehicle) return [];
    const selectedServiceIds = getEffectiveServiceIds(vehicle);
    if (selectedServiceIds.length > 0) {
      return selectedServiceIds;
    }
    if (getVehicleFlow(vehicle) === BOOKING_FLOW.FLEXIBLE) {
      const categoryIds = getEffectiveCategoryIds(vehicle);
      if (!Array.isArray(categoryIds) || categoryIds.length === 0) return [];
      return Number.isFinite(diagnosticServiceId) ? [diagnosticServiceId] : [];
    }
    return [];
  }, [diagnosticServiceId, getEffectiveCategoryIds, getEffectiveServiceIds, getVehicleFlow]);

  const sharedCategoryIntersectionIds = useMemo(() => {
    if (!multi.vehicles || multi.vehicles.length === 0) return [];
    let intersection = null;
    multi.vehicles.forEach((vehicle) => {
      const ids = uniqIds(getEffectiveCategoryIds(vehicle));
      if (!intersection) {
        intersection = ids;
        return;
      }
      const idSet = new Set(ids);
      intersection = intersection.filter((id) => idSet.has(id));
    });
    return intersection || [];
  }, [multi.vehicles, getEffectiveCategoryIds]);

  const step3CategoryIds = useMemo(
    () => (step3Vehicle ? uniqIds(getEffectiveCategoryIds(step3Vehicle)) : sharedCategoryIntersectionIds),
    [step3Vehicle, getEffectiveCategoryIds, sharedCategoryIntersectionIds]
  );

  useEffect(() => {
    if (!activeCategoryId && step3CategoryIds.length > 0) {
      setActiveCategoryId(String(step3CategoryIds[0]));
      return;
    }
    if (activeCategoryId && !step3CategoryIds.some((id) => String(id) === String(activeCategoryId))) {
      setActiveCategoryId(step3CategoryIds[0] ? String(step3CategoryIds[0]) : "");
    }
  }, [activeCategoryId, step3CategoryIds]);

  const servicesByCategory = useMemo(() => {
    if (!activeCategoryId) return [];
    return services.filter((item) => String(extractField(item, ["categoryId", "CategoryId"])) === String(activeCategoryId));
  }, [activeCategoryId, services]);

  const categoryNameById = useMemo(() => {
    const map = new Map();
    (categories || []).forEach((item) => {
      map.set(Number(extractField(item, ["id", "Id"])), extractField(item, ["name", "Name"], ""));
    });
    return map;
  }, [categories]);
  const serviceNameById = useMemo(() => {
    const map = new Map();
    (services || []).forEach((item) => {
      map.set(Number(extractField(item, ["id", "Id"])), extractField(item, ["name", "Name"], ""));
    });
    return map;
  }, [services]);
  const selectedBranch = useMemo(() => branches.find((item) => String(extractField(item, ["id", "Id"])) === String(selectedBranchId)), [branches, selectedBranchId]);

  const totalPrice = useMemo(() => multi.vehicles.reduce((sum, vehicle) => {
    const ids = getBookedServiceIds(vehicle);
    const serviceTotal = services.filter((item) => ids.includes(Number(extractField(item, ["id", "Id"])))).reduce((acc, item) => acc + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0);
    return sum + serviceTotal;
  }, 0), [getBookedServiceIds, multi.vehicles, services]);

  const totalMinutes = useMemo(() => multi.vehicles.reduce((sum, vehicle) => {
    const ids = getBookedServiceIds(vehicle);
    const minutes = services.filter((item) => ids.includes(Number(extractField(item, ["id", "Id"])))).reduce((acc, item) => acc + Number(extractField(item, ["standardMinutes", "StandardMinutes"], 0) || 0), 0);
    return sum + (minutes || 30);
  }, 0), [getBookedServiceIds, multi.vehicles, services]);
  const totalDurationLabel = useMemo(
    () => (hasAnyFlexibleVehicle ? "Xác định sau kiểm tra tại gara" : `${totalMinutes} phút`),
    [hasAnyFlexibleVehicle, totalMinutes]
  );
  const vehicleServiceSummaries = useMemo(
    () =>
      (multi.vehicles || []).map((vehicle) => {
        const categoryNames = getEffectiveCategoryIds(vehicle)
          .map((id) => categoryNameById.get(Number(id)))
          .filter(Boolean);
        const serviceNames = getBookedServiceIds(vehicle)
          .map((id) => serviceNameById.get(Number(id)))
          .filter(Boolean);
        return {
          key: vehicle.key,
          label: vehicle.displayLabel,
          categoryNames,
          serviceNames,
        };
      }),
    [multi.vehicles, getEffectiveCategoryIds, getBookedServiceIds, categoryNameById, serviceNameById]
  );
  const confirmationVehicleSummaries = useMemo(
    () =>
      (multi.vehicles || []).map((vehicle) => {
        const vehicleFlow = getVehicleFlow(vehicle);
        const serviceIds = getBookedServiceIds(vehicle);
        const vehicleServices = (services || []).filter((item) => serviceIds.includes(Number(extractField(item, ["id", "Id"]))));
        const totalVehiclePrice = vehicleServices.reduce((sum, item) => sum + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0);
        const totalVehicleMinutes = vehicleServices.reduce((sum, item) => sum + Number(extractField(item, ["standardMinutes", "StandardMinutes"], 0) || 0), 0);
        const selectedSlot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
        const startTime = extractField(selectedSlot, ["startTime", "StartTime"]);
        const endTime = extractField(selectedSlot, ["endTime", "EndTime"]);
        const symptomValues = (vehicleSymptoms[vehicle.key] || []).map((item) => String(item || "").trim()).filter(Boolean);
        return {
          key: vehicle.key,
          label: vehicle.displayLabel,
          categoryNames: getEffectiveCategoryIds(vehicle).map((id) => categoryNameById.get(Number(id))).filter(Boolean),
          serviceNames: serviceIds.map((id) => serviceNameById.get(Number(id))).filter(Boolean),
          slotLabel: startTime && endTime ? `${timeLabel(startTime)} - ${timeLabel(endTime)}` : "Chưa chọn",
          slotStartLabel: startTime ? timeLabel(startTime) : "Chưa chọn",
          slotEndLabel: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? "Chưa xác định" : (endTime ? timeLabel(endTime) : "Chưa chọn"),
          symptomValues,
          totalVehiclePrice,
          totalVehicleMinutes: totalVehicleMinutes || 30,
          durationLabel:
            vehicleFlow === BOOKING_FLOW.FLEXIBLE
              ? "Xác định sau kiểm tra tại gara"
              : `${(totalVehicleMinutes || 30)} phút`,
        };
      }),
    [
      categoryNameById,
      getEffectiveCategoryIds,
      getBookedServiceIds,
      multi.parallelSlot,
      multi.selectedSlots,
      multi.slotMode,
      multi.vehicles,
      serviceNameById,
      services,
      vehicleSymptoms,
    ]
  );
  const summaryVehicleDetails = useMemo(
    () =>
      (multi.vehicles || []).map((vehicle) => {
        const vehicleFlow = getVehicleFlow(vehicle);
        const serviceIds = getBookedServiceIds(vehicle);
        const vehicleServices = (services || []).filter((item) => serviceIds.includes(Number(extractField(item, ["id", "Id"]))));
        const vehiclePrice = vehicleServices.reduce((sum, item) => sum + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0);
        const vehicleMinutes = vehicleServices.reduce((sum, item) => sum + Number(extractField(item, ["standardMinutes", "StandardMinutes"], 0) || 0), 0) || 30;
        const selectedSlot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
        const startTime = extractField(selectedSlot, ["startTime", "StartTime"]);
        const endTime = extractField(selectedSlot, ["endTime", "EndTime"]);
        const categoryNames = getEffectiveCategoryIds(vehicle).map((id) => categoryNameById.get(Number(id))).filter(Boolean);
        const serviceNames = serviceIds.map((id) => serviceNameById.get(Number(id))).filter(Boolean);
        return {
          key: vehicle.key,
          label: vehicle.displayLabel,
          categoryNames,
          serviceNames,
          vehiclePrice,
          vehicleMinutes,
          slotLabel: startTime && endTime ? `${timeLabel(startTime)} - ${timeLabel(endTime)}` : "Chưa chọn",
          slotStartLabel: startTime ? timeLabel(startTime) : "Chưa chọn",
          slotEndLabel: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? "Chưa xác định" : (endTime ? timeLabel(endTime) : "Chưa chọn"),
          durationLabel:
            vehicleFlow === BOOKING_FLOW.FLEXIBLE
              ? "Xác định sau kiểm tra tại gara"
              : `${vehicleMinutes} phút`,
          completionLabel:
            vehicleFlow === BOOKING_FLOW.FLEXIBLE
              ? "Xác định sau kiểm tra tại gara"
              : (endTime ? `${timeLabel(endTime)} (${new Date(endTime).toLocaleDateString("vi-VN")})` : "Chưa xác định"),
          completionMs: vehicleFlow === BOOKING_FLOW.FLEXIBLE || !endTime ? null : new Date(endTime).getTime(),
        };
      }),
    [
      categoryNameById,
      getEffectiveCategoryIds,
      getBookedServiceIds,
      getVehicleFlow,
      multi.parallelSlot,
      multi.selectedSlots,
      multi.slotMode,
      multi.vehicles,
      serviceNameById,
      services,
    ]
  );
  const fleetCompletionLabel = useMemo(() => {
    if ((summaryVehicleDetails || []).some((item) => item.completionMs === null)) {
      return "Xác định sau kiểm tra tại gara";
    }
    const completionTimes = summaryVehicleDetails.map((item) => item.completionMs).filter((item) => Number.isFinite(item));
    if (completionTimes.length === 0) {
      return "Chưa xác định";
    }
    const maxCompletion = new Date(Math.max(...completionTimes));
    return `${timeLabel(maxCompletion.toISOString())} (${maxCompletion.toLocaleDateString("vi-VN")})`;
  }, [summaryVehicleDetails]);

  const effectiveSlotFlow = useMemo(() => {
    if (multi.slotMode === SLOT_MODE.INDIVIDUAL && activeVehicle) {
      return getVehicleFlow(activeVehicle);
    }
    return hasAnyFlexibleVehicle ? BOOKING_FLOW.FLEXIBLE : BOOKING_FLOW.FIXED;
  }, [activeVehicle, getVehicleFlow, hasAnyFlexibleVehicle, multi.slotMode]);
  const effectiveSelectedDate = useMemo(() => {
    if (multi.slotMode === SLOT_MODE.INDIVIDUAL) {
      return activeVehicle?.key ? (selectedDatesByVehicle[activeVehicle.key] || "") : "";
    }
    return selectedDate;
  }, [activeVehicle?.key, multi.slotMode, selectedDate, selectedDatesByVehicle]);

  const step5FlowGuide = useMemo(() => {
    if (currentStep !== 5) return "";
    if (effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE) {
      return "Bạn đang chọn giờ mang xe đến.";
    }
    return "Bạn đang chọn khung giờ cố định.";
  }, [currentStep, effectiveSlotFlow]);

  const availableSlots = useMemo(
    () => {
      const resolveSlotTimeMs = (rawValue) => {
        if (!rawValue) return null;
        const parsed = new Date(rawValue);
        if (!Number.isNaN(parsed.getTime())) return parsed.getTime();
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
        const isAvailable = extractField(slot, ["isAvailable", "IsAvailable"], false);
        const startTimeRaw = extractField(slot, ["startTime", "StartTime"], null);
        const startMs = resolveSlotTimeMs(startTimeRaw);
        const endTimeRaw = extractField(slot, ["endTime", "EndTime"], null);
        const endMs = resolveSlotTimeMs(endTimeRaw);
        const slotDurationMs = Number.isFinite(startMs) && Number.isFinite(endMs) && endMs > startMs
          ? (endMs - startMs)
          : 0;
        const effectiveCutoffMs = Math.max(baseCutoffMs, slotDurationMs);
        if (Number.isFinite(endMs) && (endMs - nowMs) < effectiveCutoffMs) return false;
        if (effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE) return Boolean(isAvailable);
        const rawRemaining = extractField(slot, ["remainingCapacity", "RemainingCapacity"], null);
        const remaining = rawRemaining === null || rawRemaining === undefined ? null : Number(rawRemaining);
        if (!Boolean(isAvailable)) return false;
        if (remaining === null || Number.isNaN(remaining)) return true;
        return remaining > 0;
      });
    },
    [effectiveSelectedDate, effectiveSlotFlow, slots]
  );
  const slotsBySession = useMemo(() => buildSlotsBySession(availableSlots), [availableSlots]);
  const displayedSlots = useMemo(
    () => buildDisplayedSlots({ slotsBySession, activeSession, bookingFlow: effectiveSlotFlow, showOddSlots }),
    [activeSession, effectiveSlotFlow, showOddSlots, slotsBySession]
  );
  const selectedFlexibleSlot = useMemo(
    () => (multi.slotMode === SLOT_MODE.PARALLEL
      ? multi.parallelSlot
      : multi.selectedSlots?.[activeVehicle?.key || ""] || null),
    [activeVehicle?.key, multi.parallelSlot, multi.selectedSlots, multi.slotMode]
  );

  const buildServiceGroups = useCallback(() => {
    return multi.vehicles.map((vehicle) => {
      const ids = getBookedServiceIds(vehicle);
      if (ids.length > 0) return ids;
      return [];
    });
  }, [getBookedServiceIds, multi.vehicles]);

  const loadSlots = useCallback(async () => {
    if (!selectedBranchId || !effectiveSelectedDate || multi.vehicles.length === 0) {
      setSlots([]);
      return;
    }
    const groups = buildServiceGroups();
    const requestedServiceIds = effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE
      ? null
      : multi.slotMode === SLOT_MODE.PARALLEL
        ? groups.map((group) => group.join(",")).join("|")
        : (groups[multi.vehicles.findIndex((item) => item.key === activeVehicle?.key)] || []).join(",");

    setSlotsLoading(true);
    setSlots([]);
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });
    try {
      let data = await loadBookingAvailableSlots({
        branchId: selectedBranchId,
        date: effectiveSelectedDate,
        serviceIds: [],
        bookingFlow: effectiveSlotFlow,
        vehicleCount: multi.slotMode === SLOT_MODE.PARALLEL ? multi.vehicles.length : 1,
        isParallel: multi.slotMode === SLOT_MODE.PARALLEL,
        requestedServiceIds,
      });
      if (
        effectiveSlotFlow === BOOKING_FLOW.FIXED &&
        Array.isArray(data) &&
        data.length === 0 &&
        typeof requestedServiceIds === "string" &&
        requestedServiceIds.includes("|")
      ) {
        const fallbackRequestedServiceIds = requestedServiceIds
          .split("|")
          .flatMap((group) => String(group || "").split(","))
          .map((item) => String(item || "").trim())
          .filter(Boolean)
          .join(",");
        data = await loadBookingAvailableSlots({
          branchId: selectedBranchId,
          date: effectiveSelectedDate,
          serviceIds: [],
          bookingFlow: effectiveSlotFlow,
          vehicleCount: multi.slotMode === SLOT_MODE.PARALLEL ? multi.vehicles.length : 1,
          isParallel: multi.slotMode === SLOT_MODE.PARALLEL,
          requestedServiceIds: fallbackRequestedServiceIds,
        });
      }
      setSlots(Array.isArray(data) ? data : []);
    } catch (slotError) {
      setSlots([]);
      showError(slotError?.message || "Không thể tải khung giờ. Vui lòng thử lại.");
    } finally {
      setSlotsLoading(false);
    }
  }, [activeVehicle?.key, buildServiceGroups, effectiveSelectedDate, effectiveSlotFlow, multi.slotMode, multi.vehicles, selectedBranchId]);

  async function releaseHoldTokenIfAny(vehicleKey) {
    const token = multi.slotHoldTokens?.[vehicleKey];
    if (!token) return;
    try {
      await releaseBookingSlotHold(token);
    } catch {
      // Ignore release failure; token will expire automatically in 5 minutes.
    }
    dispatch({ type: "CLEAR_SLOT_HOLD_TOKEN", payload: vehicleKey });
  }

  async function createHoldForVehicle(slot, vehicleKey, requestedServiceIds, vehicleFlow) {
    const startTime = extractField(slot, ["startTime", "StartTime"]);
    const endTime = extractField(slot, ["endTime", "EndTime"]);
    const response = await createBookingSlotHold({
      branchId: Number(selectedBranchId),
      startTime,
      endTime,
      bookingFlow: vehicleFlow,
      requestedServiceIds: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : requestedServiceIds,
      vehicleCount: 1,
    });
    const token = extractField(response, ["holdToken", "HoldToken"], "");
    if (!token) {
      throw new Error("Không thể tạo giữ chỗ cho khung giờ đã chọn.");
    }
    dispatch({ type: "SET_SLOT_HOLD_TOKEN", payload: { key: vehicleKey, token } });
  }

  async function handleSelectSlot(slot) {
    if (!slot || selectingSlot) return;
    if (!selectedBranchId) {
      showError("Vui lòng chọn chi nhánh trước khi chọn khung giờ.");
      return;
    }
    setSelectingSlot(true);
    const groups = buildServiceGroups();
    try {
      clearError();
      if (multi.slotMode === SLOT_MODE.PARALLEL) {
        for (let i = 0; i < multi.vehicles.length; i += 1) {
          const vehicle = multi.vehicles[i];
          await releaseHoldTokenIfAny(vehicle.key);
          const requestedServiceIds = (groups[i] || []).join(",");
          const vehicleFlow = getVehicleFlow(vehicle);
          await createHoldForVehicle(slot, vehicle.key, requestedServiceIds, vehicleFlow);
        }
        dispatch({ type: "SET_PARALLEL_SLOT", payload: slot });
        return;
      }

      if (!activeVehicle) return;
      const activeIndex = multi.vehicles.findIndex((item) => item.key === activeVehicle.key);
      await releaseHoldTokenIfAny(activeVehicle.key);
      const requestedServiceIds = (groups[activeIndex] || []).join(",");
      const vehicleFlow = getVehicleFlow(activeVehicle);
      await createHoldForVehicle(slot, activeVehicle.key, requestedServiceIds, vehicleFlow);
      dispatch({ type: "SET_INDIVIDUAL_SLOT", payload: { key: activeVehicle.key, slot } });
    } catch (slotError) {
      showError(slotError?.message || "Không thể giữ chỗ khung giờ. Vui lòng chọn lại.");
    } finally {
      setSelectingSlot(false);
    }
  }

  function buildFlexibleSlotFromIso(isoValue) {
    const startRaw = String(isoValue || "").slice(0, 16);
    const start = new Date(startRaw);
    if (Number.isNaN(start.getTime())) return null;
    const end = new Date(start.getTime() + (FLEXIBLE_CHECKIN_WINDOW_MINUTES * 60 * 1000));
    const toLocalIso = (date) => {
      const y = date.getFullYear();
      const m = String(date.getMonth() + 1).padStart(2, "0");
      const d = String(date.getDate()).padStart(2, "0");
      const hh = String(date.getHours()).padStart(2, "0");
      const mm = String(date.getMinutes()).padStart(2, "0");
      return `${y}-${m}-${d}T${hh}:${mm}:00`;
    };
    return {
      startTime: toLocalIso(start),
      endTime: toLocalIso(end),
      isAvailable: true,
      remainingCapacity: 1,
      bookingFlow: BOOKING_FLOW.FLEXIBLE,
    };
  }

  async function handleFlexibleTimeChange(value) {
    if (!value) return;
    if (!activeVehicle && multi.slotMode === SLOT_MODE.INDIVIDUAL) return;
    const slot = buildFlexibleSlotFromIso(value);
    if (!slot) {
      showError("Giờ chọn không hợp lệ. Vui lòng chọn lại.");
      return;
    }
    const pickedDate = String(value).slice(0, 10);
    if (multi.slotMode === SLOT_MODE.INDIVIDUAL && activeVehicle?.key) {
      setSelectedDatesByVehicle((prev) => ({ ...prev, [activeVehicle.key]: pickedDate }));
    } else {
      setSelectedDate(pickedDate);
    }
    await handleSelectSlot(slot);
  }

  const isHoldExpiredMessage = useCallback((message) => {
    const normalized = normalizeText(message || "");
    return normalized.includes("giu cho da het han");
  }, []);

  const refreshSelectedSlotHolds = useCallback(async () => {
    const groups = buildServiceGroups();
    const failedVehicles = [];

    if (multi.slotMode === SLOT_MODE.PARALLEL) {
      const slot = multi.parallelSlot;
      if (!slot) return false;
      for (let i = 0; i < multi.vehicles.length; i += 1) {
        const vehicle = multi.vehicles[i];
        const requestedServiceIds = (groups[i] || []).join(",");
        const vehicleFlow = getVehicleFlow(vehicle);
        try {
          await createHoldForVehicle(slot, vehicle.key, requestedServiceIds, vehicleFlow);
        } catch {
          failedVehicles.push(vehicle.displayLabel);
        }
      }
      return failedVehicles;
    }

    for (let i = 0; i < multi.vehicles.length; i += 1) {
      const vehicle = multi.vehicles[i];
      const slot = multi.selectedSlots?.[vehicle.key];
      if (!slot) {
        failedVehicles.push(vehicle.displayLabel);
        continue;
      }
      const requestedServiceIds = (groups[i] || []).join(",");
      const vehicleFlow = getVehicleFlow(vehicle);
      try {
        await createHoldForVehicle(slot, vehicle.key, requestedServiceIds, vehicleFlow);
      } catch {
        failedVehicles.push(vehicle.displayLabel);
      }
    }
    return failedVehicles;
  }, [buildServiceGroups, getVehicleFlow, multi.parallelSlot, multi.selectedSlots, multi.slotMode, multi.vehicles]);

  useEffect(() => {
    let ignore = false;
    async function loadData() {
      setLoading(true);
      try {
        const data = await loadBookingFormMasterData();
        if (ignore) return;
        setCategories(data.categories);
        setServices(data.services);
        setBranches(data.branches);
        setBrands(data.brands);
        setModels(data.models);
        setCustomers(data.customers);
        setCustomerVehicles(data.customerVehicles);
        setVehicles(data.vehicles);
      } catch {
        if (!ignore) showError("Không thể tải dữ liệu đặt lịch. Vui lòng thử lại.");
      } finally {
        if (!ignore) setLoading(false);
      }
    }
    loadData();
    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    setCustomerForm({
      fullName: String(extractField(customerRecord, ["fullName", "FullName"], user?.fullName || "")),
      phone: String(extractField(customerRecord, ["phone", "Phone"], user?.phone || "")),
    });
  }, [customerRecord, user?.fullName, user?.phone]);

  useEffect(() => {
    setActiveSession(resolveFirstActiveSession(SESSION_TABS, slotsBySession));
  }, [slotsBySession]);

  useEffect(() => {
    if (!error && !notice) return;
    alertAnchorRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [alertTick, error, notice]);

  useEffect(() => {
    if (bookingFlow !== BOOKING_FLOW.FIXED) return;
    setShowOddSlots((prev) => getAutoExpandOddStateForFixedFlow(SESSION_TABS, slotsBySession, prev));
  }, [bookingFlow, slotsBySession]);

  useEffect(() => {
    if (currentStep !== 5) return;
    if (!hasAnyFlexibleVehicle) return;
    if (multi.slotMode === SLOT_MODE.INDIVIDUAL) return;
    dispatch({ type: "SET_SLOT_MODE", payload: SLOT_MODE.INDIVIDUAL });
    dispatch({ type: "RESET_SLOTS" });
    showNotice("Có xe dùng dịch vụ kiểm tra. Hệ thống chuyển sang đặt giờ riêng lẻ để mỗi xe có khung nhận xe phù hợp.");
  }, [currentStep, hasAnyFlexibleVehicle, multi.slotMode, showNotice]);

  useEffect(() => {
    if (currentStep !== 5) return;
    loadSlots();
  }, [currentStep, loadSlots]);

  function updateDraftVehicle(key, value) {
    if (key === "licensePlate") {
      setDraftVehicle((prev) => ({ ...prev, licensePlate: formatLicensePlateInput(value) }));
      return;
    }
    if (key === "vin") {
      setDraftVehicle((prev) => ({ ...prev, vin: String(value || "").toUpperCase() }));
      return;
    }
    setDraftVehicle((prev) => ({ ...prev, [key]: value }));
  }

  function toggleExistingVehicle(vehicle) {
    dispatch({ type: "TOGGLE_EXISTING", payload: asVehicleEntryFromExisting(vehicle) });
    clearError();
  }

  async function handleRemoveVehicle(vehicleKey) {
    await releaseHoldTokenIfAny(vehicleKey);
    dispatch({ type: "REMOVE_VEHICLE", payload: vehicleKey });
  }

  function addNewVehicle() {
    const selectedBrand = brands.find((item) => String(extractField(item, ["id", "Id"])) === String(draftVehicle.brandId));
    const selectedModel = models.find((item) => String(extractField(item, ["id", "Id"])) === String(draftVehicle.modelId));
    const built = buildCurrentVehiclePayload({
      linkedVehicles: [],
      vehicleMode: "new",
      selectedExistingVehicle: null,
      newVehicle: draftVehicle,
      selectedBrand,
      selectedModel,
    });
    if (built.error) {
      showError(built.error);
      return;
    }
    dispatch({ type: "ADD_NEW", payload: { ...built, key: built.dedupeKey, source: "new", categoryIds: [], serviceIds: [], excludedSharedCategoryIds: [], excludedSharedServiceIds: [] } });
    setDraftVehicle({ ...INITIAL_NEW_VEHICLE_FORM });
    clearError();
  }

  function toggleSharedCategory(categoryId) {
    const id = Number(categoryId);
    if (!Number.isFinite(id)) return;
    const has = multi.sharedCategoryIds.includes(id);
    const next = has ? multi.sharedCategoryIds.filter((item) => item !== id) : [...multi.sharedCategoryIds, id];
    dispatch({ type: "SET_SHARED_CATEGORIES", payload: next });
    if (has) {
      const isDifferentCategory = (serviceId) => Number(serviceCategoryById.get(serviceId)) !== id;
      dispatch({ type: "SET_SHARED_SERVICES", payload: multi.sharedServiceIds.filter(isDifferentCategory) });
      (multi.vehicles || []).forEach((vehicle) => {
        dispatch({
          type: "PATCH_VEHICLE",
          payload: {
            key: vehicle.key,
            patch: {
              categoryIds: (vehicle.categoryIds || []).filter((item) => Number(item) !== id),
              serviceIds: (vehicle.serviceIds || []).filter(isDifferentCategory),
              excludedSharedCategoryIds: (vehicle.excludedSharedCategoryIds || []).filter((item) => Number(item) !== id),
              excludedSharedServiceIds: (vehicle.excludedSharedServiceIds || []).filter(isDifferentCategory),
            },
          },
        });
      });
    }
  }

  function toggleVehicleCategory(vehicle, categoryId) {
    const id = Number(categoryId);
    if (!Number.isFinite(id)) return;
    const isDifferentCategory = (serviceId) => Number(serviceCategoryById.get(serviceId)) !== id;
    if (multi.serviceMode === SERVICE_MODE.CUSTOM) {
      const has = (vehicle.categoryIds || []).includes(id);
      dispatch({
        type: "PATCH_VEHICLE",
        payload: {
          key: vehicle.key,
          patch: has
            ? {
              categoryIds: (vehicle.categoryIds || []).filter((item) => item !== id),
              serviceIds: (vehicle.serviceIds || []).filter(isDifferentCategory),
              excludedSharedServiceIds: (vehicle.excludedSharedServiceIds || []).filter(isDifferentCategory),
            }
            : { categoryIds: [...(vehicle.categoryIds || []), id] },
        },
      });
      return;
    }
    if (multi.sharedCategoryIds.includes(id)) {
      showError("Loại dịch vụ dùng chung chỉ có thể hủy ở tab \"Dùng chung tất cả xe\".");
      return;
    }
    const has = (vehicle.categoryIds || []).includes(id);
    dispatch({
      type: "PATCH_VEHICLE",
      payload: {
        key: vehicle.key,
        patch: has
          ? {
            categoryIds: (vehicle.categoryIds || []).filter((item) => item !== id),
            serviceIds: (vehicle.serviceIds || []).filter(isDifferentCategory),
            excludedSharedServiceIds: (vehicle.excludedSharedServiceIds || []).filter(isDifferentCategory),
          }
          : { categoryIds: [...(vehicle.categoryIds || []), id] },
      },
    });
  }

  function handleBookingFlowChange(nextFlow) {
    if (nextFlow === bookingFlow) return;
    setBookingFlow(nextFlow);
    clearError();
    if (nextFlow === BOOKING_FLOW.FLEXIBLE) {
      showNotice("Đã chuyển sang Check-in linh hoạt. Hệ thống sẽ áp dụng dịch vụ Kiểm tra tổng quát và thời gian hoàn tất sẽ xác định sau kiểm tra tại gara.");
      return;
    }
    clearNotice();
  }

  function toggleService(serviceId) {
    const id = Number(serviceId);
    if (!Number.isFinite(id)) return;
    const selectedNow = step3Vehicle ? getEffectiveServiceIds(step3Vehicle).includes(id) : multi.sharedServiceIds.includes(id);
    if (!selectedNow && bookingFlow === BOOKING_FLOW.FIXED && flexibleTriggerServiceIdSet.has(id)) {
      showNotice("Xe sẽ được tự động chuyển sang luồng Giờ linh hoạt. Thời gian hoàn tất sẽ được xác định sau khi xe được kiểm tra tại gara.");
    }
    if (!step3Vehicle) {
      const has = multi.sharedServiceIds.includes(id);
      dispatch({ type: "SET_SHARED_SERVICES", payload: has ? multi.sharedServiceIds.filter((item) => item !== id) : [...multi.sharedServiceIds, id] });
      return;
    }
    if (multi.sharedServiceIds.includes(id)) {
      const hasEx = (step3Vehicle.excludedSharedServiceIds || []).includes(id);
      dispatch({ type: "PATCH_VEHICLE", payload: { key: step3Vehicle.key, patch: { excludedSharedServiceIds: hasEx ? step3Vehicle.excludedSharedServiceIds.filter((item) => item !== id) : [...step3Vehicle.excludedSharedServiceIds, id] } } });
      return;
    }
    const has = (step3Vehicle.serviceIds || []).includes(id);
    dispatch({ type: "PATCH_VEHICLE", payload: { key: step3Vehicle.key, patch: { serviceIds: has ? step3Vehicle.serviceIds.filter((item) => item !== id) : [...step3Vehicle.serviceIds, id] } } });
  }

  function validateStep(stepNo) {
    if (stepNo === 1) {
      if (!String(customerForm.fullName || "").trim()) return "Vui lòng nhập họ tên khách hàng.";
      if (!String(customerForm.phone || "").trim()) return "Vui lòng nhập số điện thoại khách hàng.";
      if (multi.vehicles.length === 0) return "Vui lòng chọn ít nhất một xe.";
    }
    if (stepNo === 2) {
      if ((multi.vehicles || []).length === 0) {
        return "Vui lòng chọn ít nhất một xe.";
      }
      for (const vehicle of multi.vehicles) {
        const categoryIds = uniqIds(getEffectiveCategoryIds(vehicle));
        if (categoryIds.length === 0) {
          return `Xe ${vehicle.displayLabel}: vui lòng chọn ít nhất một loại dịch vụ.`;
        }
      }
    }
    if (stepNo === 3) {
      for (const vehicle of multi.vehicles) {
        const categoryIds = uniqIds(getEffectiveCategoryIds(vehicle));
        if (categoryIds.length === 0) {
          return `Xe ${vehicle.displayLabel}: vui lòng chọn ít nhất một loại dịch vụ trước khi chọn dịch vụ cụ thể.`;
        }

        const serviceIds = uniqIds(getEffectiveServiceIds(vehicle));
        if (serviceIds.length === 0) {
          return `Xe ${vehicle.displayLabel}: vui lòng chọn dịch vụ cụ thể cho các loại dịch vụ đã chọn.`;
        }

        const coveredCategoryIds = new Set(
          serviceIds
            .map((serviceId) => Number(serviceCategoryById.get(Number(serviceId))))
            .filter(Number.isFinite)
        );

        const missingCategoryIds = categoryIds.filter((categoryId) => !coveredCategoryIds.has(Number(categoryId)));
        if (missingCategoryIds.length > 0) {
          const missingCategoryNames = missingCategoryIds
            .map((categoryId) => categoryNameById.get(Number(categoryId)) || `Nhóm ${categoryId}`)
            .join(", ");
          return `Xe ${vehicle.displayLabel}: các loại dịch vụ sau chưa có dịch vụ cụ thể: ${missingCategoryNames}.`;
        }
      }
    }
    if (stepNo === 4 && !selectedBranchId) return "Vui lòng chọn chi nhánh.";
    if (stepNo === 5) {
      if (multi.slotMode === SLOT_MODE.PARALLEL && !selectedDate) return "Vui lòng chọn ngày đặt lịch.";
      if (multi.slotMode === SLOT_MODE.INDIVIDUAL) {
        const missingDateVehicle = multi.vehicles.find((item) => getVehicleFlow(item) === BOOKING_FLOW.FIXED && !String(selectedDatesByVehicle[item.key] || "").trim());
        if (missingDateVehicle) return `Xe ${missingDateVehicle.displayLabel}: vui lòng chọn ngày đặt lịch.`;
      }
      if (hasAnyFlexibleVehicle && multi.slotMode !== SLOT_MODE.INDIVIDUAL) {
        return "Có xe dùng dịch vụ kiểm tra nên cần đặt giờ riêng lẻ cho từng xe.";
      }
      if (multi.slotMode === SLOT_MODE.PARALLEL && !multi.parallelSlot) return "Vui lòng chọn khung giờ song song.";
      if (multi.slotMode === SLOT_MODE.PARALLEL) {
        const allHeld = multi.vehicles.every((item) => Boolean(multi.slotHoldTokens?.[item.key]));
        if (!allHeld) return "Khung giờ chưa được giữ chỗ đầy đủ. Vui lòng chọn lại.";
      }
      if (multi.slotMode === SLOT_MODE.INDIVIDUAL) {
        const allSlot = multi.vehicles.every((item) => Boolean(multi.selectedSlots[item.key]));
        if (!allSlot) return "Vui lòng chọn đủ khung giờ cho từng xe.";
        const allHeld = multi.vehicles.every((item) => Boolean(multi.slotHoldTokens?.[item.key]));
        if (!allHeld) return "Vui lòng giữ chỗ đủ khung giờ cho từng xe.";
      }
    }
    return "";
  }

  function goNext() {
    const issue = validateStep(currentStep);
    if (issue) {
      showError(issue);
      return;
    }
    clearError();
    setCurrentStep((prev) => Math.min(prev + 1, BOOKING_STEPS.length));
  }

  function goPrevious() {
    clearError();
    setCurrentStep((prev) => Math.max(prev - 1, 1));
  }

  function toggleOddSlotsForSession(sessionKey) {
    setShowOddSlots((prev) => ({ ...prev, [sessionKey]: !prev[sessionKey] }));
  }

  function onSymptomChange(vehicleKey, index, value) {
    setVehicleSymptoms((prev) => {
      const current = Array.isArray(prev[vehicleKey]) ? [...prev[vehicleKey]] : [""];
      current[index] = value;
      if (index === current.length - 1 && String(value || "").trim() && current.length < 12) {
        current.push("");
      }
      return { ...prev, [vehicleKey]: current };
    });
  }

  function buildVehicleNote(vehicle) {
    return buildCustomerNotesPayload(vehicleSymptoms[vehicle.key] || []);
  }

  function buildBatchCustomerNote() {
    const items = (multi.vehicles || [])
      .map((vehicle) => {
        const symptoms = (vehicleSymptoms[vehicle.key] || [])
          .map((item) => String(item || "").trim())
          .filter(Boolean);
        if (symptoms.length === 0) return null;
        return { vehicle: vehicle.displayLabel, symptoms };
      })
      .filter(Boolean);
    return items.length > 0 ? JSON.stringify({ symptomsByVehicle: items }) : null;
  }

  async function handleConfirm() {
    const issue = validateStep(5);
    if (issue) {
      showError(issue);
      setCurrentStep(5);
      return;
    }
    setSubmitting(true);
    clearError();
    setSuccess("");
    try {
      const groups = buildServiceGroups();
      if (multi.vehicles.length === 1) {
        const vehicle = multi.vehicles[0];
        const vehicleFlow = getVehicleFlow(vehicle);
        const note = buildVehicleNote(vehicle);
        const slot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
        const serviceIds = groups[0];
        await submitSingleBooking({
          branchId: Number(selectedBranchId),
          customerFullName: customerForm.fullName.trim(),
          customerPhone: customerForm.phone.trim(),
          bookingFlow: vehicleFlow,
          startTime: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : extractField(slot, ["startTime", "StartTime"]),
          endTime: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : extractField(slot, ["endTime", "EndTime"]),
          checkInWindowStart: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["startTime", "StartTime"]) : null,
          checkInWindowEnd: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["endTime", "EndTime"]) : null,
          requestedServiceIds: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : (serviceIds.length > 0 ? serviceIds.join(",") : null),
          slotHoldToken: multi.slotHoldTokens?.[vehicle.key] || null,
          customerNotes: note,
          vehicleId: vehicle.payload.vehicleId,
          vehicleModelId: vehicle.payload.vehicleModelId,
          licensePlate: vehicle.payload.licensePlate,
          vin: vehicle.payload.vin,
          color: vehicle.payload.color,
        });
      } else {
        const batchNote = buildBatchCustomerNote();
        const items = multi.vehicles.map((vehicle, index) => {
          const vehicleFlow = getVehicleFlow(vehicle);
          const slot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
          const serviceIds = groups[index];
          const note = buildVehicleNote(vehicle);
          return {
            branchId: Number(selectedBranchId),
            customerFullName: customerForm.fullName.trim(),
            customerPhone: customerForm.phone.trim(),
            bookingFlow: vehicleFlow,
            startTime: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : extractField(slot, ["startTime", "StartTime"]),
            endTime: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : extractField(slot, ["endTime", "EndTime"]),
            checkInWindowStart: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["startTime", "StartTime"]) : null,
            checkInWindowEnd: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["endTime", "EndTime"]) : null,
            requestedServiceIds: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? null : (serviceIds.length > 0 ? serviceIds.join(",") : null),
            slotHoldToken: multi.slotHoldTokens?.[vehicle.key] || null,
            customerNotes: note,
            vehicleId: vehicle.payload.vehicleId,
            vehicleModelId: vehicle.payload.vehicleModelId,
            licensePlate: vehicle.payload.licensePlate,
            vin: vehicle.payload.vin,
            color: vehicle.payload.color,
          };
        });
        await submitBatchBooking({ branchId: Number(selectedBranchId), customerNote: batchNote, items });
      }
      setSuccess("Đặt lịch thành công. Chúng tôi sẽ xác nhận sớm nhất.");
      setCurrentStep(6);
    } catch (submitError) {
      const message = submitError?.message || "Đặt lịch thất bại. Vui lòng thử lại.";
      if (isHoldExpiredMessage(message)) {
        setCurrentStep(5);
        const failedVehicles = await refreshSelectedSlotHolds();
        await loadSlots();
        if (Array.isArray(failedVehicles) && failedVehicles.length > 0) {
          showError(`Giữ chỗ đã hết hạn và không thể giữ lại cho: ${failedVehicles.join(", ")}. Vui lòng chọn lại khung giờ cho các xe này.`);
        } else {
          showNotice("Giữ chỗ đã được làm mới. Vui lòng bấm xác nhận lại.");
        }
      } else {
        showError(message);
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <div className="booking-loading">Đang tải dữ liệu đặt lịch...</div>;
  }

  const summaryItems = [
    { key: "customer", label: "Khách hàng", value: customerForm.fullName },
    { key: "phone", label: "SĐT", value: customerForm.phone },
    { key: "vehicles", label: "Danh sách xe", values: multi.vehicles.map((item) => item.displayLabel) },
  ];

  return (
    <div
      className="booking-page-shell"
      onSubmitCapture={(event) => {
        event.preventDefault();
      }}
    >
      <div className="booking-fixed-header"><Header /></div>
      <div className="booking-flow">
        <section className="booking-progress-card">
          <div className="booking-progress-track"><span style={{ width: `${(currentStep / BOOKING_STEPS.length) * 100}%` }} /></div>
          <div className="booking-progress-grid">
            {BOOKING_STEPS.map((label, index) => <article key={label} className={`booking-progress-step ${index + 1 < currentStep ? "done" : ""} ${index + 1 === currentStep ? "current" : ""}`}><strong>{index + 1}</strong><p>{label}</p></article>)}
          </div>
        </section>
        <div ref={alertAnchorRef} />
        {error ? <div className="booking-alert booking-alert-error">{error}</div> : null}
        {notice ? <div className="booking-alert booking-alert-info">{notice}</div> : null}
        <section className={`booking-layout ${currentStep === 6 ? "step5-layout" : ""}`}>
          <div className="booking-main-panel">
            {currentStep === 1 ? (
              <div className="booking-card">
                <header className="booking-card-head"><h2>Bước 1: Thông tin khách hàng & xe</h2></header>
                <div className="form-grid step4-form-grid">
                  <label>Họ và tên <span>*</span><input type="text" value={customerForm.fullName} onChange={(e) => setCustomerForm((prev) => ({ ...prev, fullName: e.target.value }))} /></label>
                  <label>Số điện thoại <span>*</span><input type="tel" value={customerForm.phone} onChange={(e) => setCustomerForm((prev) => ({ ...prev, phone: e.target.value }))} /></label>
                </div>
                <VehicleActionPanel linkedVehicles={linkedVehicles} selectedVehicles={multi.vehicles} brands={brands} models={models} activeVehicleKey={activeSymptomVehicleKey} onSelectVehicle={setActiveSymptomVehicleKey} draftVehicle={draftVehicle} onDraftChange={updateDraftVehicle} onToggleExistingVehicle={toggleExistingVehicle} onAddNewVehicle={addNewVehicle} onRemoveVehicle={handleRemoveVehicle} />
                <label className="notes-field">
                  Mô tả triệu chứng
                  {(multi.vehicles || []).length > 0 ? (
                    <div style={{ display: "grid", gap: "10px" }}>
                      <strong style={{ fontSize: "15px", fontWeight: 700, color: "rgba(29,42,38,0.86)" }}>
                        {activeSymptomVehicle?.displayLabel || ""}
                      </strong>
                      <div style={{ display: "grid", gap: "8px" }}>
                        {(vehicleSymptoms[activeSymptomVehicle?.key] || [""]).map((item, index) => (
                          <textarea
                            key={`symptom-${activeSymptomVehicle?.key || "none"}-${index}`}
                            rows={1}
                            value={item}
                            placeholder={`Nhập mô tả triệu chứng ${index + 1}...`}
                            onChange={(e) => onSymptomChange(activeSymptomVehicle?.key || "", index, e.target.value)}
                          />
                        ))}
                      </div>
                      <p className="vehicle-batch-note">
                        Chọn xe ở danh sách phía trên để nhập triệu chứng cho xe cụ thể. Mỗi xe có thể nhập nhiều triệu chứng (1, 2, 3...).
                      </p>
                    </div>
                  ) : null}
                </label>
              </div>
            ) : null}

            {currentStep === 2 ? (
              <div className="booking-card">
                <header className="booking-card-head"><h2>Bước 2: Phân loại dịch vụ</h2></header>
                {multi.vehicles.length > 1 ? <div className="booking-scope-switch"><button type="button" className={!step2VehicleKey ? "active" : ""} onClick={() => { setStep2VehicleKey(""); dispatch({ type: "SET_SERVICE_MODE", payload: SERVICE_MODE.SHARED }); }}>Dùng chung tất cả xe</button><button type="button" className={step2VehicleKey ? "active" : ""} onClick={() => { if (!step2VehicleKey && multi.vehicles[0]) setStep2VehicleKey(multi.vehicles[0].key); dispatch({ type: "SET_SERVICE_MODE", payload: SERVICE_MODE.SHARED }); }}>Tùy chỉnh theo từng xe</button></div> : null}
                {multi.vehicles.length > 1 ? <div className="selected-category-tabs vehicle-tabs step2-vehicle-tabs">{multi.vehicles.map((vehicle) => <div key={`step2-${vehicle.key}`} className={`selected-category-tab ${step2Vehicle?.key === vehicle.key ? "active" : ""}`}><button type="button" className="selected-category-tab-main" onClick={() => setStep2VehicleKey(vehicle.key)}>{vehicle.displayLabel}</button><button type="button" className="selected-category-tab-remove" onClick={(e) => { e.stopPropagation(); handleRemoveVehicle(vehicle.key); }}>x</button></div>)}</div> : null}
                <div className="category-grid">{categories.map((item) => { const id = Number(extractField(item, ["id", "Id"])); const selected = step2Vehicle ? getEffectiveCategoryIds(step2Vehicle).includes(id) : multi.sharedCategoryIds.includes(id); return <button key={id} type="button" className={`category-card ${selected ? "selected" : ""}`} onClick={() => { if (step2Vehicle) toggleVehicleCategory(step2Vehicle, id); else toggleSharedCategory(id); }}><span className="category-icon">{iconByIndex(id || 0)}</span><h3>{extractField(item, ["name", "Name"], "")}</h3></button>; })}</div>
              </div>
            ) : null}

            {currentStep === 3 ? (
              <div className="booking-card">
                <header className="booking-card-head"><h2>Bước 3: Chi tiết dịch vụ</h2></header>
                <div className="flow-mode-switch"><button type="button" className={bookingFlow === BOOKING_FLOW.FIXED ? "active" : ""} onClick={() => handleBookingFlowChange(BOOKING_FLOW.FIXED)}>Giờ cố định</button><button type="button" className={bookingFlow === BOOKING_FLOW.FLEXIBLE ? "active" : ""} onClick={() => handleBookingFlowChange(BOOKING_FLOW.FLEXIBLE)}>Giờ linh hoạt</button></div>
                {bookingFlow === BOOKING_FLOW.FLEXIBLE ? <div className="booking-empty flow-note">Chỉ những xe có dịch vụ kiểm tra mới tự động chuyển sang luồng Giờ linh hoạt. Các xe còn lại vẫn theo luồng Giờ cố định.</div> : (
                  <>
                    {multi.vehicles.length > 1 ? <div className="booking-scope-switch"><button type="button" className={!step3VehicleKey ? "active" : ""} onClick={() => setStep3VehicleKey("")}>Dùng chung tất cả xe</button><button type="button" className={step3VehicleKey ? "active" : ""} onClick={() => { if (!step3VehicleKey && multi.vehicles[0]) setStep3VehicleKey(multi.vehicles[0].key); }}>Tùy chỉnh theo từng xe</button></div> : null}
                    {multi.vehicles.length > 1 ? <div className="selected-category-tabs vehicle-tabs step3-vehicle-tabs">{multi.vehicles.map((vehicle) => <div key={`step3-${vehicle.key}`} className={`selected-category-tab ${step3Vehicle?.key === vehicle.key ? "active" : ""}`}><button type="button" className="selected-category-tab-main" onClick={() => setStep3VehicleKey(vehicle.key)}>{vehicle.displayLabel}</button><button type="button" className="selected-category-tab-remove" onClick={(e) => { e.stopPropagation(); handleRemoveVehicle(vehicle.key); }}>x</button></div>)}</div> : null}
                    <div className="selected-category-tabs">{step3CategoryIds.map((id) => <div key={`cat-${id}`} className={`selected-category-tab ${String(activeCategoryId) === String(id) ? "active" : ""}`}><button type="button" className="selected-category-tab-main" onClick={() => setActiveCategoryId(String(id))}>{extractField(categories.find((item) => Number(extractField(item, ["id", "Id"])) === Number(id)), ["name", "Name"], "") || `Nhóm ${id}`}</button><button type="button" className="selected-category-tab-remove" onClick={(e) => { e.stopPropagation(); if (step3Vehicle) toggleVehicleCategory(step3Vehicle, id); else toggleSharedCategory(id); }}>x</button></div>)}</div>
                    <div className="service-list">{servicesByCategory.map((service) => { const id = Number(extractField(service, ["id", "Id"])); const selected = step3Vehicle ? getEffectiveServiceIds(step3Vehicle).includes(id) : multi.sharedServiceIds.includes(id); const minutes = Number(extractField(service, ["standardMinutes", "StandardMinutes"], 0) || 0); return <button key={id} type="button" className={`service-card ${selected ? "selected" : ""}`} onClick={() => toggleService(id)}><span className="service-icon">{iconByIndex(id || 0)}</span><div className="service-card-body"><h3>{extractField(service, ["name", "Name"], "")}</h3><small>{minutes > 0 ? `${minutes} phút` : "Chưa có thời lượng"}</small></div><aside className="service-card-side"><strong>{formatVnd(extractField(service, ["basePrice", "BasePrice"], 0))}</strong></aside></button>; })}</div>
                  </>
                )}
              </div>
            ) : null}

            {currentStep === 4 ? <div className="booking-card"><header className="booking-card-head"><h2>Bước 4: Chọn chi nhánh</h2></header><div className="branch-list">{branches.map((branch) => { const id = extractField(branch, ["id", "Id"]); const selected = String(id) === String(selectedBranchId); return <button key={id} type="button" className={`branch-card ${selected ? "selected" : ""}`} onClick={() => { setSelectedBranchId(String(id)); dispatch({ type: "RESET_SLOTS" }); }}><h3>{extractField(branch, ["name", "Name"], "Chi nhánh")}</h3><p>{extractField(branch, ["address", "Address"], "")}</p></button>; })}</div></div> : null}

            {currentStep === 5 ? (
              <div className="booking-card">
                <header className="booking-card-head"><h2>Bước 5: Chọn khung giờ</h2></header>
                {multi.vehicles.length > 1 ? <div className="booking-scope-switch"><button type="button" className={multi.slotMode === SLOT_MODE.PARALLEL ? "active" : ""} onClick={() => { dispatch({ type: "SET_SLOT_MODE", payload: SLOT_MODE.PARALLEL }); dispatch({ type: "RESET_SLOTS" }); }}>Song song</button><button type="button" className={multi.slotMode === SLOT_MODE.INDIVIDUAL ? "active" : ""} onClick={() => { dispatch({ type: "SET_SLOT_MODE", payload: SLOT_MODE.INDIVIDUAL }); dispatch({ type: "RESET_SLOTS" }); }}>Riêng lẻ</button></div> : null}
                {multi.slotMode === SLOT_MODE.INDIVIDUAL ? <div className="selected-category-tabs vehicle-tabs">{multi.vehicles.map((vehicle) => <div key={vehicle.key} className={`selected-category-tab ${activeVehicle?.key === vehicle.key ? "active" : ""}`}><button type="button" className="selected-category-tab-main" onClick={() => setActiveVehicleKey(vehicle.key)}>{vehicle.displayLabel}</button></div>)}</div> : null}
                <div className="form-grid">
                  <div className="booking-field-group">
                    <span className="booking-field-label">
                      {effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE ? "Ngày giờ" : "Ngày"}
                    </span>
                    <DateTimePicker
                      value={effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE ? extractField(selectedFlexibleSlot, ["startTime", "StartTime"], "") : (effectiveSelectedDate ? `${effectiveSelectedDate}T08:00:00` : "")}
                      minDate={new Date()}
                      showTime={effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE}
                      onChange={(value) => {
                        if (effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE) {
                          handleFlexibleTimeChange(value);
                          return;
                        }
                        const nextDate = String(value || "").slice(0, 10);
                        if (multi.slotMode === SLOT_MODE.INDIVIDUAL && activeVehicle?.key) {
                          void releaseHoldTokenIfAny(activeVehicle.key);
                          setSelectedDatesByVehicle((prev) => ({ ...prev, [activeVehicle.key]: nextDate }));
                          dispatch({ type: "CLEAR_INDIVIDUAL_SLOT", payload: activeVehicle.key });
                        } else {
                          setSelectedDate(nextDate);
                          dispatch({ type: "RESET_SLOTS" });
                        }
                      }}
                    />
                  </div>
                </div>
                {step5FlowGuide ? <p className="slot-guide-note">{step5FlowGuide}</p> : null}
                <div className="slot-section">
                  {!selectedBranchId ? <div className="booking-empty">Vui lòng chọn chi nhánh trước khi chọn giờ.</div> : effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE ? null : !effectiveSelectedDate ? <div className="booking-empty">Vui lòng chọn ngày cho xe đang thao tác trước khi chọn giờ.</div> : (
                    slotsLoading ? <div className="booking-empty">Đang tải khung giờ...</div> : (
                      <>
                        <div className="session-tabs">{SESSION_TABS.map((tab) => <button key={tab.key} type="button" className={`session-tab ${activeSession === tab.key ? "active" : ""}`} onClick={() => setActiveSession(tab.key)}><strong>{tab.label}</strong><small>{tab.hint}</small></button>)}</div>
                        {(slotsBySession[activeSession] || []).length > 0 ? <div className="slot-actions"><button type="button" className="slot-expand-toggle" onClick={() => toggleOddSlotsForSession(activeSession)}>{showOddSlots[activeSession] ? "Ẩn giờ lẻ" : "Xem thêm giờ lẻ"}</button></div> : null}
                        <div className="slot-grid">{displayedSlots.map((slot) => {
                          const start = extractField(slot, ["startTime", "StartTime"]);
                          const end = extractField(slot, ["endTime", "EndTime"]);
                          const chosen = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[activeVehicle?.key || ""];
                          const isSelected = chosen && extractField(chosen, ["startTime", "StartTime"]) === start && extractField(chosen, ["endTime", "EndTime"]) === end;
                          const isSelectingThis = selectingSlot && isSelected;

                          return (
                            <button
                              key={`${start}-${end}`}
                              type="button"
                              className={`slot-card ${isSelected ? "selected" : ""} ${selectingSlot ? "selecting" : ""}`}
                              onClick={() => handleSelectSlot(slot)}
                              disabled={selectingSlot}
                            >
                              {selectingSlot && isSelected ? (
                                <div className="slot-loading-overlay">Đang giữ chỗ...</div>
                              ) : null}
                              <strong className="slot-time">{timeLabel(start)} - {timeLabel(end)}</strong>
                              <div className="slot-meta">
                                <span className={`slot-state-dot ${Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)) > 0 ? "ok" : "full"}`} />
                                <span className={`slot-state-text ${Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)) > 0 ? "ok" : "full"}`}>
                                  {Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)) > 0 ? `Còn ${Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0))} chỗ` : "Hết chỗ"}
                                </span>
                              </div>
                            </button>
                          );
                        })}</div>
                      </>
                    )
                  )}
                </div>
              </div>
            ) : null}
            {currentStep === 6 ? (
              <div className="summary-card summary-card-large">
                {success ? (
                  <div className="booking-success-screen">
                    <div className="booking-success-icon">✓</div>
                    <h3>Đặt lịch thành công</h3>
                    <p>{success}</p>
                    <div className="success-actions">
                      <button type="button" onClick={() => navigate("/")}>Về trang chủ</button>
                      <button type="button" onClick={() => navigate("/my-appointments")}>Xem lịch hẹn</button>
                    </div>
                  </div>
                ) : (
                  <>
                    <h3>Xác nhận đặt lịch</h3>
                    <dl>
                      <dt>Khách hàng</dt>
                      <dd>{customerForm.fullName}</dd>
                      <dt>SĐT</dt>
                      <dd>{customerForm.phone}</dd>
                      <dt>Chi nhánh</dt>
                      <dd>{extractField(selectedBranch, ["name", "Name"], "Chưa chọn")}</dd>
                    </dl>
                    <div className="summary-vehicle-services">
                      <h4>Tóm tắt theo từng xe</h4>
                      <div className="summary-vehicle-list">
                        {confirmationVehicleSummaries.map((vehicle) => (
                          <article key={`confirm-vehicle-${vehicle.key}`} className="summary-vehicle-card">
                            <strong>{vehicle.label}</strong>
                            <div className="summary-vehicle-content">
                              <p><span>Loại dịch vụ:</span> {vehicle.categoryNames.length > 0 ? vehicle.categoryNames.join(", ") : "Chưa chọn"}</p>
                              <p><span>Dịch vụ cụ thể:</span> {vehicle.serviceNames.length > 0 ? vehicle.serviceNames.join(", ") : "Chưa chọn"}</p>
                              <p><span>Giờ bắt đầu:</span> {vehicle.slotStartLabel}</p>
                              <p><span>Giờ kết thúc:</span> {vehicle.slotEndLabel}</p>
                              <p><span>Triệu chứng:</span> {vehicle.symptomValues.length > 0 ? vehicle.symptomValues.join(" | ") : "Không có"}</p>
                              <p><span>Tổng tiền xe:</span> {formatVnd(vehicle.totalVehiclePrice)}</p>
                              <p><span>Thời lượng xe:</span> {vehicle.durationLabel}</p>
                            </div>
                          </article>
                        ))}
                      </div>
                    </div>
                    <div className="summary-total">
                      <div>
                        <span>Tổng dịch vụ</span>
                        <strong>{formatVnd(totalPrice)}</strong>
                      </div>
                      <div>
                        <span>Thời lượng dự kiến xong tất cả xe</span>
                        <strong>{totalDurationLabel}</strong>
                      </div>
                    </div>
                    <button type="button" className="primary-submit" onClick={handleConfirm} disabled={submitting}>
                      {submitting ? "Đang gửi lịch hẹn..." : "Xác nhận đặt lịch"}
                    </button>
                  </>
                )}
              </div>
            ) : null}

            {!(currentStep === 6 && success) ? <div className="wizard-actions"><button type="button" className="ghost-btn" onClick={goPrevious} disabled={currentStep === 1 || submitting}>Quay lại</button>{currentStep < BOOKING_STEPS.length ? <button type="button" className="next-btn" onClick={goNext} disabled={submitting}>Tiếp tục</button> : null}</div> : null}
          </div>
          {currentStep !== 6 ? (
            <aside className="booking-summary-panel">
              <div className="summary-card">
                <h3>Tóm tắt lịch hẹn</h3>
                <dl>
                  {summaryItems.map((item) => (
                    <Fragment key={item.key}>
                      <dt>{item.label}</dt>
                      <dd>{Array.isArray(item.values) ? <ul className="summary-bullet-list">{item.values.map((entry, index) => <li key={`${item.key}-${index}`}>{entry}</li>)}</ul> : item.value}</dd>
                    </Fragment>
                  ))}
                </dl>
                <div className="summary-vehicle-services">
                  <h4>Chi tiết theo từng xe</h4>
                  <div className="summary-vehicle-list">
                    {summaryVehicleDetails.map((vehicle, index) => (
                      <article key={`sum-vehicle-${vehicle.key}`} className="summary-vehicle-card">
                        <div className="summary-vehicle-header">
                          <strong>{vehicle.label}</strong>
                          <span>Xe {index + 1}</span>
                        </div>
                        <div className="summary-vehicle-content">
                          <p><span>Loại dịch vụ:</span> {vehicle.categoryNames.length > 0 ? vehicle.categoryNames.join(", ") : "Chưa chọn"}</p>
                          <p><span>Dịch vụ cụ thể:</span> {vehicle.serviceNames.length > 0 ? vehicle.serviceNames.join(", ") : "Chưa chọn"}</p>
                          <p><span>Giờ bắt đầu:</span> {vehicle.slotStartLabel}</p>
                          <p><span>Giờ kết thúc:</span> {vehicle.slotEndLabel}</p>
                          <p><span>Hoàn tất dự kiến:</span> {vehicle.completionLabel}</p>
                          <p><span>Tổng tiền xe:</span> {formatVnd(vehicle.vehiclePrice)}</p>
                          <p><span>Thời lượng xe:</span> {vehicle.durationLabel}</p>
                        </div>
                      </article>
                    ))}
                  </div>
                </div>
                <div className="summary-total">
                  <div>
                    <span>Tổng dịch vụ toàn bộ xe</span>
                    <strong>{formatVnd(totalPrice)}</strong>
                  </div>
                  <div>
                    <span>Thời gian hoàn tất toàn bộ xe</span>
                    <strong>{fleetCompletionLabel}</strong>
                  </div>
                </div>
              </div>
            </aside>
          ) : null}
        </section>
      </div>
    </div>
  );
}

















