import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import Header from "@shared/components/Header";
import { getMyAppointments } from "@features/appointments/api/appointmentApi";
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
  uploadBookingInsuranceAsset,
} from "@features/appointments/api/bookingFormApi";
import {
  extractField,
  formatStoredVehicleLabel,
  formatVnd,
  normalizeText,
  timeLabel,
  toIsoDateInput,
} from "@features/appointments/utils/bookingCommon";
import { isValidVietnamesePhone } from "@shared/utils/contactValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import { formatVinInput } from "@shared/utils/vinValidation";
import {
  buildDisplayedSlots,
  buildSlotsBySession,
  canToggleOddSlotsForSession,
  resolveFirstActiveSession,
} from "@features/appointments/utils/bookingSlots";
import {
  buildRequestedPackageIdsByMode,
  buildRequestedServiceIdsByMode,
  loadAvailabilityWithFallback,
} from "@features/appointments/utils/bookingAvailability";
import {
  buildFlexibleCompletionWarningMessage,
  buildFlexibleTriggerServiceIdSet,
  buildInsuranceFlexiblePackageIdSet,
  findUnresolvedPackageIds,
  resolveBookingFlowByItemsAndDuration,
  WORKING_DAY_MINUTES,
} from "@features/appointments/utils/bookingFlowRules";
import { buildCurrentVehiclePayload } from "@features/appointments/utils/bookingVehicle";
import { normalizeAppointmentsResponse } from "@features/appointments/utils/myAppointmentsData";
import {
  buildInspectionTriadTypeByServiceId,
  getInspectionTriadDisplayName,
  getConflictingInspectionTriadServiceIds,
  isMiniCheckService,
  sumServiceMinutes,
} from "@shared/utils/serviceDurationUtils";
import {
  calculateRoundedOccupancyMinutes,
} from "@shared/utils/workingHoursSchedule";
import {
  arePlanSelectionsEqual,
  collectCompatibleCategoryIds,
  filterBookableItemsForVehicleModels,
  prunePlanByVehicleCompatibility,
} from "@shared/utils/serviceEngineCompatibility";
import { getBranchClosedMessage, isBranchClosedBySchedule } from "@shared/utils/branchOperatingSchedule";
import {
  INSURANCE_ASSET_TYPE,
  buildInsuranceSnapshotPayload,
  buildInsuranceSummary,
  createEmptyInsuranceSnapshot,
  getInsuranceBookingItemNames,
  hasInsuranceServices,
  normalizeInsuranceSnapshot,
  resolveInsuranceVehicleKeys,
  validateInsuranceSnapshotByMode,
} from "@features/appointments/utils/bookingInsurance";
import { splitBookingItemIds } from "@features/appointments/utils/bookingPackages";
import {
  INITIAL_MULTI,
  SERVICE_MODE,
  SLOT_MODE,
  asVehicleEntryFromExisting,
  buildBookingSuccessMessage,
  buildFixedFlowSchedule,
  buildServiceHierarchyByCategory,
  multiReducer,
  normalizeSearchText,
  resolveSlotDateTime,
  uniqIds,
} from "@features/appointments/pages/bookingPageHelpers";
import {
  BookingStep1Customer,
  BookingStep2Categories,
  BookingStep3Services,
  BookingStep4Branch,
  BookingStep5Slots,
  BookingStep6Confirmation,
  BookingSummarySidebar,
} from "@features/appointments/pages/BookingStepPanels";
import "./BookingPage.css";

export default function BookingPage() {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [success, setSuccess] = useState("");
  const [successTargetTab, setSuccessTargetTab] = useState("single");
  const [currentStep, setCurrentStep] = useState(1);
  const [slideDirection, setSlideDirection] = useState("next");
  const alertAnchorRef = useRef(null);
  const [alertTick, setAlertTick] = useState(0);

  const [categories, setCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [branches, setBranches] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [insuranceCompanies, setInsuranceCompanies] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [branchOperatingHours, setBranchOperatingHours] = useState([]);
  const [hasPreviousCustomerBookings, setHasPreviousCustomerBookings] = useState(false);

  const [customerForm, setCustomerForm] = useState({ fullName: "", phone: "" });
  const [draftVehicle, setDraftVehicle] = useState({ ...INITIAL_NEW_VEHICLE_FORM });
  const [vehicleSymptoms, setVehicleSymptoms] = useState({});
  const [activeSymptomVehicleKey, setActiveSymptomVehicleKey] = useState("");
  const [insuranceByVehicleKey, setInsuranceByVehicleKey] = useState({});
  const [insuranceUploadStateByVehicleKey, setInsuranceUploadStateByVehicleKey] = useState({});
  const [activeInsuranceVehicleKey, setActiveInsuranceVehicleKey] = useState("");

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

  useEffect(() => {
    document.title = "Đặt lịch | MyGarage";
  }, []);

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

  const scrollToTopForAlerts = useCallback(() => {
    if (typeof window === "undefined" || typeof document === "undefined") return;

    const scrollNodeToTop = (node) => {
      if (!node) return;
      if (typeof node.scrollTo === "function") {
        node.scrollTo({ top: 0, behavior: "smooth" });
      } else if (typeof node.scrollTop === "number") {
        node.scrollTop = 0;
      }
    };

    window.scrollTo({ top: 0, behavior: "smooth" });
    scrollNodeToTop(document.scrollingElement);
    scrollNodeToTop(document.documentElement);
    scrollNodeToTop(document.body);

    let parent = alertAnchorRef.current?.parentElement || null;
    while (parent) {
      const style = window.getComputedStyle(parent);
      const overflowY = style?.overflowY || "";
      const canScroll =
        (overflowY === "auto" || overflowY === "scroll" || overflowY === "overlay") &&
        parent.scrollHeight > parent.clientHeight;
      if (canScroll) {
        scrollNodeToTop(parent);
      }
      parent = parent.parentElement;
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", { replace: true, state: { from: { pathname: "/booking/form" } } });
    }
  }, [isAuthenticated, navigate]);

  const customerRecord = user;

  const linkedVehicles = useMemo(() => {
    const brandNameById = new Map();
    (brands || []).forEach((brand) => {
      const brandId = Number(extractField(brand, ["id", "Id"], 0));
      if (!Number.isFinite(brandId) || brandId <= 0) return;
      const brandName = String(extractField(brand, ["name", "Name", "brandName", "BrandName"], "") || "").trim();
      if (!brandName) return;
      brandNameById.set(brandId, brandName);
    });

    const modelInfoById = new Map();
    (models || []).forEach((model) => {
      const modelId = Number(extractField(model, ["id", "Id"], 0));
      if (!Number.isFinite(modelId) || modelId <= 0) return;
      const modelName = String(extractField(model, ["modelName", "ModelName", "name", "Name"], "") || "").trim();
      const brandId = Number(extractField(model, ["brandId", "BrandId"], 0));
      const modelBrandName = String(extractField(model, ["brandName", "BrandName"], "") || "").trim();
      modelInfoById.set(modelId, {
        modelName,
        brandId: Number.isFinite(brandId) && brandId > 0 ? brandId : 0,
        brandName: modelBrandName,
      });
    });

    return (vehicles || [])
      .map((vehicle) => {
        const modelObject = extractField(vehicle, ["model", "Model", "vehicleModel", "VehicleModel"], null);
        const brandObject = extractField(vehicle, ["brand", "Brand", "vehicleBrand", "VehicleBrand"], null);

        const modelId = Number(
          extractField(vehicle, ["modelId", "ModelId", "vehicleModelId", "VehicleModelId"], extractField(modelObject, ["id", "Id"], 0))
        );
        const modelInfo = modelInfoById.get(modelId) || null;

        const brandIdFromVehicle = Number(
          extractField(vehicle, ["brandId", "BrandId", "vehicleBrandId", "VehicleBrandId"], extractField(brandObject, ["id", "Id"], 0))
        );
        const brandId = Number.isFinite(brandIdFromVehicle) && brandIdFromVehicle > 0
          ? brandIdFromVehicle
          : Number(modelInfo?.brandId || 0);

        const modelName = String(
          extractField(vehicle, ["modelName", "ModelName", "vehicleModelName", "VehicleModelName"], "") ||
          extractField(modelObject, ["modelName", "ModelName", "name", "Name"], "") ||
          modelInfo?.modelName ||
          ""
        ).trim();

        const brandName = String(
          extractField(vehicle, ["brandName", "BrandName", "vehicleBrandName", "VehicleBrandName"], "") ||
          extractField(brandObject, ["name", "Name", "brandName", "BrandName"], "") ||
          modelInfo?.brandName ||
          brandNameById.get(brandId) ||
          ""
        ).trim();

        if (!modelName && !brandName) return vehicle;
        return {
          ...vehicle,
          modelName,
          brandName,
        };
      });
  }, [brands, models, vehicles]);
  const customerHasNoSavedVehicles = linkedVehicles.length === 0;

  const vehicleModelInfoById = useMemo(() => {
    const map = new Map();
    (models || []).forEach((model) => {
      const modelId = Number(extractField(model, ["id", "Id"], 0));
      if (!Number.isFinite(modelId) || modelId <= 0) return;
      map.set(modelId, {
        fuelType: String(extractField(model, ["fuelType", "FuelType"], "") || "").trim(),
      });
    });
    return map;
  }, [models]);

  const serviceCategoryById = useMemo(() => {
    const map = new Map();
    (services || []).forEach((service) => {
      map.set(Number(extractField(service, ["id", "Id"])), Number(extractField(service, ["categoryId", "CategoryId"])));
    });
    return map;
  }, [services]);
  const serviceById = useMemo(() => {
    const map = new Map();
    (services || []).forEach((service) => {
      const serviceId = Number(extractField(service, ["id", "Id"]));
      if (!Number.isFinite(serviceId) || serviceId === 0) return;
      map.set(serviceId, service);
    });
    return map;
  }, [services]);
  const insuranceCompanyNameById = useMemo(() => {
    const map = new Map();
    (insuranceCompanies || []).forEach((company) => {
      const companyId = Number(extractField(company, ["id", "Id"]));
      const companyName = String(extractField(company, ["name", "Name"], "") || "").trim();
      if (!Number.isFinite(companyId) || companyId <= 0 || !companyName) return;
      map.set(companyId, companyName);
    });
    return map;
  }, [insuranceCompanies]);

  const diagnosticService = useMemo(() => {
    const byCode = services.find((item) => normalizeText(extractField(item, ["code", "Code"], "")) === normalizeText(DEFAULT_DIAGNOSTIC_SERVICE_CODE));
    if (byCode) return byCode;
    return services.find((item) => normalizeSearchText(extractField(item, ["name", "Name"], "")).includes("kiem tra tong quat"));
  }, [services]);
  const diagnosticServiceId = useMemo(() => Number(extractField(diagnosticService, ["id", "Id"])), [diagnosticService]);
  const flexibleTriggerServiceIdSet = useMemo(
    () => buildFlexibleTriggerServiceIdSet(services),
    [services]
  );
  const insuranceFlexiblePackageIdSet = useMemo(
    () => buildInsuranceFlexiblePackageIdSet(services),
    [services]
  );
  const isFirstCustomerBooking = !hasPreviousCustomerBookings;
  const miniCheckServiceIdSet = useMemo(
    () =>
      new Set(
        (services || [])
          .filter((service) => isMiniCheckService(service))
          .map((service) => Number(extractField(service, ["id", "Id"])))
          .filter(Number.isFinite)
      ),
    [services]
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
    const itemIds = getEffectiveServiceIds(vehicle);
    return resolveBookingFlowByItemsAndDuration({
      itemIds,
      flexibleTriggerServiceIdSet,
      insuranceFlexiblePackageIdSet,
      executionMinutes: 0,
    }).bookingFlow;
  }, [flexibleTriggerServiceIdSet, getEffectiveServiceIds, insuranceFlexiblePackageIdSet]);
  const resolveSlotFlow = useCallback((slot, fallbackFlow = BOOKING_FLOW.FIXED) => {
    const rawFlow = normalizeText(extractField(slot, ["bookingFlow", "BookingFlow"], fallbackFlow));
    if (rawFlow.includes("flex")) return BOOKING_FLOW.FLEXIBLE;
    if (rawFlow.includes("fixed")) return BOOKING_FLOW.FIXED;
    return fallbackFlow;
  }, []);

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
  const getVehicleModelId = useCallback((vehicle) => {
    const modelId = Number(
      vehicle?.payload?.vehicleModelId
        ?? extractField(vehicle, ["vehicleModelId", "VehicleModelId", "modelId", "ModelId"], 0)
    );
    return Number.isFinite(modelId) && modelId > 0 ? modelId : 0;
  }, []);
  const getVehicleModelIds = useCallback(
    (vehicleList) =>
      (Array.isArray(vehicleList) ? vehicleList : [])
        .map((vehicle) => getVehicleModelId(vehicle))
        .filter((modelId) => Number.isFinite(modelId) && modelId > 0),
    [getVehicleModelId]
  );
  const getCompatibleCategoryIdsForVehicles = useCallback(
    (vehicleList) =>
      Array.from(
        collectCompatibleCategoryIds(
          services,
          getVehicleModelIds(vehicleList),
          vehicleModelInfoById,
          serviceById
        )
      ),
    [getVehicleModelIds, serviceById, services, vehicleModelInfoById]
  );
  const getCompatibleItemsForVehicles = useCallback(
    (vehicleList) =>
      filterBookableItemsForVehicleModels(
        services,
        getVehicleModelIds(vehicleList),
        vehicleModelInfoById,
        serviceById
      ),
    [getVehicleModelIds, serviceById, services, vehicleModelInfoById]
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
        next[key] = typeof prev[key] === "string" ? prev[key] : "";
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

  useEffect(() => {
    const selectedModelIds = getVehicleModelIds(multi.vehicles);
    const nextSharedPlan = prunePlanByVehicleCompatibility(
      {
        categoryIds: multi.sharedCategoryIds,
        serviceIds: multi.sharedServiceIds,
        excludedSharedCategoryIds: [],
        excludedSharedServiceIds: [],
      },
      services,
      selectedModelIds,
      vehicleModelInfoById,
      serviceById
    );

    if (
      !arePlanSelectionsEqual(
        {
          categoryIds: multi.sharedCategoryIds,
          serviceIds: multi.sharedServiceIds,
          excludedSharedCategoryIds: [],
          excludedSharedServiceIds: [],
        },
        nextSharedPlan
      )
    ) {
      dispatch({ type: "SET_SHARED_CATEGORIES", payload: nextSharedPlan.categoryIds });
      dispatch({ type: "SET_SHARED_SERVICES", payload: nextSharedPlan.serviceIds });
    }

    (multi.vehicles || []).forEach((vehicle) => {
      const nextVehiclePlan = prunePlanByVehicleCompatibility(
        vehicle,
        services,
        [getVehicleModelId(vehicle)].filter((id) => id > 0),
        vehicleModelInfoById,
        serviceById
      );

      if (!arePlanSelectionsEqual(vehicle, nextVehiclePlan)) {
        dispatch({
          type: "PATCH_VEHICLE",
          payload: {
            key: vehicle.key,
            patch: nextVehiclePlan,
          },
        });
      }
    });
  }, [
    getVehicleModelId,
    getVehicleModelIds,
    multi.sharedCategoryIds,
    multi.sharedServiceIds,
    multi.vehicles,
    serviceById,
    services,
    vehicleModelInfoById,
  ]);

  const getBookedServiceIds = useCallback((vehicle) => {
    if (!vehicle) return [];
    const selectedServiceIds = getEffectiveServiceIds(vehicle);
    if (selectedServiceIds.length > 0) {
      return selectedServiceIds;
    }
    if (getVehicleFlow(vehicle) === BOOKING_FLOW.FLEXIBLE) {
      const categoryIds = getEffectiveCategoryIds(vehicle);
      if (!Array.isArray(categoryIds) || categoryIds.length === 0) return [];
      if (!Number.isFinite(diagnosticServiceId)) return [];
      if (!isFirstCustomerBooking && miniCheckServiceIdSet.has(Number(diagnosticServiceId))) return [];
      return [diagnosticServiceId];
    }
    return [];
  }, [diagnosticServiceId, getEffectiveCategoryIds, getEffectiveServiceIds, getVehicleFlow, isFirstCustomerBooking, miniCheckServiceIdSet]);
  const bookedServiceIdsByVehicleKey = useMemo(() => {
    const map = new Map();
    (multi.vehicles || []).forEach((vehicle) => {
      map.set(vehicle.key, getBookedServiceIds(vehicle));
    });
    return map;
  }, [getBookedServiceIds, multi.vehicles]);
  const insuranceItemNamesByVehicleKey = useMemo(() => {
    const map = new Map();
    (multi.vehicles || []).forEach((vehicle) => {
      const serviceIds = bookedServiceIdsByVehicleKey.get(vehicle.key) || [];
      map.set(vehicle.key, getInsuranceBookingItemNames(serviceIds, serviceById));
    });
    return map;
  }, [bookedServiceIdsByVehicleKey, multi.vehicles, serviceById]);
  const insuranceVehicleOptions = useMemo(
    () =>
      resolveInsuranceVehicleKeys(
        (multi.vehicles || []).map((vehicle) => vehicle.key),
        bookedServiceIdsByVehicleKey,
        serviceById
      ).map((vehicleKey) => {
        const vehicle = (multi.vehicles || []).find((item) => item.key === vehicleKey);
        return {
          key: vehicleKey,
          label: vehicle?.displayLabel || vehicleKey,
          selectedInsuranceItemNames: insuranceItemNamesByVehicleKey.get(vehicleKey) || [],
        };
      }),
    [bookedServiceIdsByVehicleKey, insuranceItemNamesByVehicleKey, multi.vehicles, serviceById]
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
      map.set(
        vehicle.key,
        buildInsuranceSummary(insuranceByVehicleKey[vehicle.key], insuranceCompanyNameById, { mode: "booking" })
      );
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

      clearError();
      setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
        uploading: true,
        successMessage: "",
      });
      try {
        const uploadedUrls = [];
        for (const file of uploadFiles) {
          const uploaded = await uploadBookingInsuranceAsset(file, assetType);
          const uploadedUrl = extractField(uploaded, ["url", "Url"], "");
          if (uploadedUrl) {
            uploadedUrls.push(uploadedUrl);
          }
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
        showError(uploadError?.message || "Không thể tải ảnh bảo hiểm. Vui lòng thử lại.");
      } finally {
        setInsuranceUploadState(activeInsuranceVehicleKey, assetType, {
          uploading: false,
        });
      }
    },
    [activeInsuranceVehicleKey, clearError, setInsuranceUploadState, showError]
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

  const getVehicleRoundedOccupancyMinutes = useCallback(
    (vehicle) => {
      if (!vehicle) return 0;
      const serviceIds = getBookedServiceIds(vehicle);
      if (!Array.isArray(serviceIds) || serviceIds.length === 0) return 0;

      const selectedServices = (services || []).filter((item) =>
        serviceIds.includes(Number(extractField(item, ["id", "Id"])))
      );
      const serviceMinutes = sumServiceMinutes(selectedServices);
      return calculateRoundedOccupancyMinutes(serviceMinutes);
    },
    [getBookedServiceIds, services]
  );

  const shouldVehicleUseLongDurationFlexibleFlow = useCallback(
    (vehicle) => getVehicleRoundedOccupancyMinutes(vehicle) > WORKING_DAY_MINUTES,
    [getVehicleRoundedOccupancyMinutes]
  );
  const hasAnyLongDurationVehicle = useMemo(
    () => (multi.vehicles || []).some((vehicle) => shouldVehicleUseLongDurationFlexibleFlow(vehicle)),
    [multi.vehicles, shouldVehicleUseLongDurationFlexibleFlow]
  );

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

  const step2CompatibleCategoryIds = useMemo(
    () =>
      step2Vehicle
        ? getCompatibleCategoryIdsForVehicles([step2Vehicle])
        : getCompatibleCategoryIdsForVehicles(multi.vehicles),
    [getCompatibleCategoryIdsForVehicles, multi.vehicles, step2Vehicle]
  );

  const step3CategoryIds = useMemo(
    () => {
      const selectedIds = step3Vehicle
        ? uniqIds(getEffectiveCategoryIds(step3Vehicle))
        : sharedCategoryIntersectionIds;
      const compatibleIdSet = new Set(
        step3Vehicle
          ? getCompatibleCategoryIdsForVehicles([step3Vehicle])
          : getCompatibleCategoryIdsForVehicles(multi.vehicles)
      );
      return selectedIds.filter((id) => compatibleIdSet.has(Number(id)));
    },
    [getCompatibleCategoryIdsForVehicles, getEffectiveCategoryIds, multi.vehicles, sharedCategoryIntersectionIds, step3Vehicle]
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
    return getCompatibleItemsForVehicles(step3Vehicle ? [step3Vehicle] : multi.vehicles)
      .filter((item) => String(extractField(item, ["categoryId", "CategoryId"])) === String(activeCategoryId))
      .filter((item) => isFirstCustomerBooking || !isMiniCheckService(item))
      .sort((a, b) => {
        const nameA = getInspectionTriadDisplayName(a, extractField(a, ["name", "Name"], ""));
        const nameB = getInspectionTriadDisplayName(b, extractField(b, ["name", "Name"], ""));
        const byName = String(nameA || "").localeCompare(String(nameB || ""), "vi");
        if (byName !== 0) return byName;
        const idA = Number(extractField(a, ["id", "Id"], 0));
        const idB = Number(extractField(b, ["id", "Id"], 0));
        return idA - idB;
      });
  }, [activeCategoryId, getCompatibleItemsForVehicles, isFirstCustomerBooking, isMiniCheckService, multi.vehicles, step3Vehicle]);

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
      const serviceId = Number(extractField(item, ["id", "Id"]));
      const rawName = extractField(item, ["name", "Name"], "");
      map.set(serviceId, getInspectionTriadDisplayName(item, rawName));
    });
    return map;
  }, [services]);
  const inspectionTriadTypeByServiceId = useMemo(
    () => buildInspectionTriadTypeByServiceId(services || []),
    [services]
  );
  const getConflictingInspectionServiceIds = useCallback(
    (serviceIds, targetServiceId) =>
      getConflictingInspectionTriadServiceIds(
        serviceIds,
        targetServiceId,
        inspectionTriadTypeByServiceId
      ),
    [inspectionTriadTypeByServiceId]
  );
  const selectedBranch = useMemo(() => branches.find((item) => String(extractField(item, ["id", "Id"])) === String(selectedBranchId)), [branches, selectedBranchId]);
  const formatDateTimeWithDate = useCallback((rawValue) => {
    if (!rawValue) return "Chưa chọn";
    const parsed = new Date(rawValue);
    if (Number.isNaN(parsed.getTime())) return "Chưa chọn";
    const timeText = parsed.toLocaleTimeString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit",
      timeZone: "Asia/Ho_Chi_Minh",
    });
    const dateText = parsed.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
    return `${timeText} (${dateText})`;
  }, []);

  const totalPrice = useMemo(() => multi.vehicles.reduce((sum, vehicle) => {
    const ids = getBookedServiceIds(vehicle);
    const serviceTotal = services.filter((item) => ids.includes(Number(extractField(item, ["id", "Id"])))).reduce((acc, item) => acc + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0);
    return sum + serviceTotal;
  }, 0), [getBookedServiceIds, multi.vehicles, services]);

  const totalMinutes = useMemo(
    () =>
      multi.vehicles.reduce((sum, vehicle) => {
        const ids = getBookedServiceIds(vehicle);
        const selectedServices = services.filter((item) =>
          ids.includes(Number(extractField(item, ["id", "Id"])))
        );
        return sum + sumServiceMinutes(selectedServices);
      }, 0),
    [getBookedServiceIds, multi.vehicles, services]
  );
  const totalDurationLabel = useMemo(() => `${totalMinutes} phút`, [totalMinutes]);
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
        const serviceIds = getBookedServiceIds(vehicle);
        const vehicleServices = (services || []).filter((item) => serviceIds.includes(Number(extractField(item, ["id", "Id"]))));
        const totalVehiclePrice = vehicleServices.reduce((sum, item) => sum + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0);
        const totalVehicleMinutes = sumServiceMinutes(vehicleServices);
        const selectedSlot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
        const vehicleFlow = getVehicleFlow(vehicle);
        const startTime = extractField(selectedSlot, ["startTime", "StartTime"]);
        const fallbackDate = multi.slotMode === SLOT_MODE.INDIVIDUAL
          ? (selectedDatesByVehicle[vehicle.key] || selectedDate || "")
          : (selectedDate || "");
        const schedule = buildFixedFlowSchedule(startTime, totalVehicleMinutes, fallbackDate);
        const displayStart = schedule.startDate || startTime;
        const displayEnd = schedule.endDate || extractField(selectedSlot, ["endTime", "EndTime"], null);
        const noteValue = String(vehicleSymptoms[vehicle.key] || "").trim();
        return {
          key: vehicle.key,
          label: vehicle.displayLabel,
          bookingFlow: vehicleFlow,
          categoryNames: getEffectiveCategoryIds(vehicle).map((id) => categoryNameById.get(Number(id))).filter(Boolean),
          serviceNames: serviceIds.map((id) => serviceNameById.get(Number(id))).filter(Boolean),
          serviceHierarchy: buildServiceHierarchyByCategory(vehicleServices, categoryNameById),
          insuranceSummary: insuranceSummaryByVehicleKey.get(vehicle.key) || null,
          slotLabel: displayStart && displayEnd ? `${timeLabel(displayStart)} - ${timeLabel(displayEnd)}` : "Chưa chọn",
          slotStartLabel: formatDateTimeWithDate(displayStart),
          slotEndLabel: formatDateTimeWithDate(displayEnd),
          noteValue,
          totalVehiclePrice,
          totalVehicleMinutes,
          durationLabel: `${totalVehicleMinutes} phút`,
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
      resolveSlotFlow,
      insuranceSummaryByVehicleKey,
      serviceNameById,
      services,
      formatDateTimeWithDate,
      selectedDate,
      selectedDatesByVehicle,
      vehicleSymptoms,
    ]
  );
  const summaryVehicleDetails = useMemo(
    () =>
      (multi.vehicles || []).map((vehicle) => {
        const serviceIds = getBookedServiceIds(vehicle);
        const vehicleServices = (services || []).filter((item) => serviceIds.includes(Number(extractField(item, ["id", "Id"]))));
        const vehiclePrice = vehicleServices.reduce((sum, item) => sum + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0);
        const vehicleMinutes = sumServiceMinutes(vehicleServices);
        const selectedSlot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
        const vehicleFlow = getVehicleFlow(vehicle);
        const startTime = extractField(selectedSlot, ["startTime", "StartTime"]);
        const fallbackDate = multi.slotMode === SLOT_MODE.INDIVIDUAL
          ? (selectedDatesByVehicle[vehicle.key] || selectedDate || "")
          : (selectedDate || "");
        const schedule = buildFixedFlowSchedule(startTime, vehicleMinutes, fallbackDate);
        const displayStart = schedule.startDate || startTime;
        const displayEnd = schedule.endDate || extractField(selectedSlot, ["endTime", "EndTime"], null);
        const fallbackCompletionMs = displayEnd ? new Date(displayEnd).getTime() : null;
        const categoryNames = getEffectiveCategoryIds(vehicle).map((id) => categoryNameById.get(Number(id))).filter(Boolean);
        const serviceNames = serviceIds.map((id) => serviceNameById.get(Number(id))).filter(Boolean);
        return {
          key: vehicle.key,
          label: vehicle.displayLabel,
          bookingFlow: vehicleFlow,
          categoryNames,
          serviceNames,
          serviceHierarchy: buildServiceHierarchyByCategory(vehicleServices, categoryNameById),
          insuranceSummary: insuranceSummaryByVehicleKey.get(vehicle.key) || null,
          vehiclePrice,
          vehicleMinutes,
          slotLabel: displayStart && displayEnd ? `${timeLabel(displayStart)} - ${timeLabel(displayEnd)}` : "Chưa chọn",
          slotStartLabel: formatDateTimeWithDate(displayStart),
          slotEndLabel: formatDateTimeWithDate(displayEnd),
          durationLabel: `${vehicleMinutes} phút`,
          completionMs: Number.isFinite(schedule.completionMs) ? schedule.completionMs : fallbackCompletionMs,
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
      insuranceSummaryByVehicleKey,
      serviceNameById,
      services,
      formatDateTimeWithDate,
      selectedDate,
      selectedDatesByVehicle,
    ]
  );
  const effectiveSlotFlow = useMemo(() => {
    if (multi.slotMode === SLOT_MODE.INDIVIDUAL && activeVehicle) {
      if (getVehicleFlow(activeVehicle) === BOOKING_FLOW.FLEXIBLE) return BOOKING_FLOW.FLEXIBLE;
      if (shouldVehicleUseLongDurationFlexibleFlow(activeVehicle)) return BOOKING_FLOW.FLEXIBLE;
      return BOOKING_FLOW.FIXED;
    }
    return hasAnyFlexibleVehicle || hasAnyLongDurationVehicle ? BOOKING_FLOW.FLEXIBLE : BOOKING_FLOW.FIXED;
  }, [
    activeVehicle,
    getVehicleFlow,
    hasAnyFlexibleVehicle,
    hasAnyLongDurationVehicle,
    multi.slotMode,
    shouldVehicleUseLongDurationFlexibleFlow
  ]);
  const effectiveSelectedDate = useMemo(() => {
    if (multi.slotMode === SLOT_MODE.INDIVIDUAL) {
      return activeVehicle?.key ? (selectedDatesByVehicle[activeVehicle.key] || "") : "";
    }
    return selectedDate;
  }, [activeVehicle?.key, multi.slotMode, selectedDate, selectedDatesByVehicle]);
  const slotSelectionGuide = useMemo(() => {
    if (effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE) {
      if (
        multi.slotMode === SLOT_MODE.INDIVIDUAL &&
        activeVehicle &&
        getVehicleFlow(activeVehicle) !== BOOKING_FLOW.FLEXIBLE &&
        shouldVehicleUseLongDurationFlexibleFlow(activeVehicle)
      ) {
        return "Tổng thời lượng vượt 12 giờ/ngày, hệ thống chuyển sang chọn khung giờ check-in (tiếp nhận).";
      }
      if (multi.slotMode === SLOT_MODE.PARALLEL && hasAnyLongDurationVehicle && !hasAnyFlexibleVehicle) {
        return "Có xe vượt 12 giờ/ngày, hệ thống chuyển sang chọn khung giờ check-in (tiếp nhận).";
      }
      return "Chọn khung giờ check-in (tiếp nhận).";
    }
    return "Chọn khung giờ sửa chữa.";
  }, [
    activeVehicle,
    effectiveSlotFlow,
    getVehicleFlow,
    hasAnyFlexibleVehicle,
    hasAnyLongDurationVehicle,
    multi.slotMode,
    shouldVehicleUseLongDurationFlexibleFlow
  ]);
  const flexibleCompletionNoteByVehicleKey = useMemo(() => {
    const noteByVehicleKey = new Map();
    (summaryVehicleDetails || [])
      .filter((vehicle) => vehicle.bookingFlow === BOOKING_FLOW.FLEXIBLE && Number.isFinite(vehicle.completionMs))
      .forEach((vehicle) => {
        const vehicleName = String(vehicle.label || "").trim() || "chưa xác định";
        const note = buildFlexibleCompletionWarningMessage({
          vehicleLabel: vehicleName,
          completionValue: vehicle.completionMs,
        });
        if (note) {
          noteByVehicleKey.set(vehicle.key, note);
        }
      });

    return noteByVehicleKey;
  }, [summaryVehicleDetails]);
  const fleetCompletionLabel = useMemo(() => {
    if ((summaryVehicleDetails || []).some((item) => item.completionMs === null)) {
      return "Chưa xác định";
    }
    const completionTimes = summaryVehicleDetails.map((item) => item.completionMs).filter((item) => Number.isFinite(item));
    if (completionTimes.length === 0) {
      return "Chưa xác định";
    }
    const maxCompletion = new Date(Math.max(...completionTimes));
    return `${timeLabel(maxCompletion.toISOString())} (${maxCompletion.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })})`;
  }, [summaryVehicleDetails]);

  const availableSlots = useMemo(
    () => {
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
        const rawRemaining = extractField(slot, ["remainingCapacity", "RemainingCapacity"], null);
        const remaining = rawRemaining === null || rawRemaining === undefined ? null : Number(rawRemaining);
        if (!Boolean(isAvailable)) return false;
        if (remaining === null || Number.isNaN(remaining)) return true;
        return remaining > 0;
      });
    },
    [effectiveSelectedDate, slots]
  );
  const slotsBySession = useMemo(() => buildSlotsBySession(availableSlots), [availableSlots]);
  const displayedSlots = useMemo(
    () => buildDisplayedSlots({ slotsBySession, activeSession, bookingFlow: effectiveSlotFlow, showOddSlots }),
    [activeSession, effectiveSlotFlow, showOddSlots, slotsBySession]
  );
  const canToggleOddSlots = useMemo(
    () => canToggleOddSlotsForSession({ slotsBySession, activeSession, bookingFlow: effectiveSlotFlow }),
    [activeSession, effectiveSlotFlow, slotsBySession]
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
    if (isBranchClosedBySchedule(branchOperatingHours, selectedBranchId, `${effectiveSelectedDate}T08:00:00`)) {
      setSlots([]);
      showError(getBranchClosedMessage(`${effectiveSelectedDate}T08:00:00`));
      return;
    }
    const groups = buildServiceGroups();
    const requestedServiceIds = buildRequestedServiceIdsByMode({
      serviceGroups: groups,
      isParallel: multi.slotMode === SLOT_MODE.PARALLEL,
      activeIndex: multi.vehicles.findIndex((item) => item.key === activeVehicle?.key),
    });
    const requestedPackageIds = buildRequestedPackageIdsByMode({
      serviceGroups: groups,
      isParallel: multi.slotMode === SLOT_MODE.PARALLEL,
      activeIndex: multi.vehicles.findIndex((item) => item.key === activeVehicle?.key),
    });

    setSlotsLoading(true);
    setSlots([]);
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });
    try {
      const data = await loadAvailabilityWithFallback({
        loadBookingAvailableSlots,
        branchId: selectedBranchId,
        date: effectiveSelectedDate,
        serviceIds: [],
        requestedServiceIds,
        requestedPackageIds,
        bookingFlow: effectiveSlotFlow,
        vehicleCount: multi.slotMode === SLOT_MODE.PARALLEL ? multi.vehicles.length : 1,
        isParallel: multi.slotMode === SLOT_MODE.PARALLEL,
      });
      setSlots(data);
    } catch (slotError) {
      setSlots([]);
      showError(slotError?.message || "Không thể tải khung giờ. Vui lòng thử lại.");
    } finally {
      setSlotsLoading(false);
    }
  }, [
    activeVehicle?.key,
    branchOperatingHours,
    buildServiceGroups,
    effectiveSelectedDate,
    effectiveSlotFlow,
    multi.slotMode,
    multi.vehicles,
    selectedBranchId,
    showError,
  ]);

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

  async function createHoldForVehicle(slot, vehicleKey, requestedServiceIds, requestedPackageIds, vehicleFlow) {
    const startTime = extractField(slot, ["startTime", "StartTime"]);
    const endTime = extractField(slot, ["endTime", "EndTime"]);
    const response = await createBookingSlotHold({
      branchId: Number(selectedBranchId),
      startTime,
      endTime,
      bookingFlow: vehicleFlow,
      requestedServiceIds,
      requestedPackageIds,
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
          const split = splitBookingItemIds(groups[i] || []);
          const requestedServiceIds = split.serviceIds.length > 0 ? split.serviceIds.join(",") : null;
          const requestedPackageIds = split.packageIds.length > 0 ? split.packageIds.join(",") : null;
          const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicle));
          await createHoldForVehicle(slot, vehicle.key, requestedServiceIds, requestedPackageIds, vehicleFlow);
        }
        dispatch({ type: "SET_PARALLEL_SLOT", payload: slot });
        return;
      }

      if (!activeVehicle) return;
      const activeIndex = multi.vehicles.findIndex((item) => item.key === activeVehicle.key);
      await releaseHoldTokenIfAny(activeVehicle.key);
      const split = splitBookingItemIds(groups[activeIndex] || []);
      const requestedServiceIds = split.serviceIds.length > 0 ? split.serviceIds.join(",") : null;
      const requestedPackageIds = split.packageIds.length > 0 ? split.packageIds.join(",") : null;
      const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(activeVehicle));
      await createHoldForVehicle(slot, activeVehicle.key, requestedServiceIds, requestedPackageIds, vehicleFlow);
      dispatch({ type: "SET_INDIVIDUAL_SLOT", payload: { key: activeVehicle.key, slot } });
    } catch (slotError) {
      showError(slotError?.message || "Không thể giữ chỗ khung giờ. Vui lòng chọn lại.");
    } finally {
      setSelectingSlot(false);
    }
  }

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
    const groups = buildServiceGroups();
    const failedVehicles = [];

    if (multi.slotMode === SLOT_MODE.PARALLEL) {
      const slot = multi.parallelSlot;
      if (!slot) return false;
      for (let i = 0; i < multi.vehicles.length; i += 1) {
        const vehicle = multi.vehicles[i];
        const split = splitBookingItemIds(groups[i] || []);
        const requestedServiceIds = split.serviceIds.length > 0 ? split.serviceIds.join(",") : null;
        const requestedPackageIds = split.packageIds.length > 0 ? split.packageIds.join(",") : null;
        const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicle));
        try {
          await createHoldForVehicle(slot, vehicle.key, requestedServiceIds, requestedPackageIds, vehicleFlow);
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
      const split = splitBookingItemIds(groups[i] || []);
      const requestedServiceIds = split.serviceIds.length > 0 ? split.serviceIds.join(",") : null;
      const requestedPackageIds = split.packageIds.length > 0 ? split.packageIds.join(",") : null;
      const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicle));
      try {
        await createHoldForVehicle(slot, vehicle.key, requestedServiceIds, requestedPackageIds, vehicleFlow);
      } catch {
        failedVehicles.push(vehicle.displayLabel);
      }
    }
    return failedVehicles;
  }, [buildServiceGroups, getVehicleFlow, multi.parallelSlot, multi.selectedSlots, multi.slotMode, multi.vehicles, resolveSlotFlow]);

  useEffect(() => {
    if (isFirstCustomerBooking || miniCheckServiceIdSet.size === 0) return;

    const nextSharedServiceIds = (multi.sharedServiceIds || []).filter(
      (serviceId) => !miniCheckServiceIdSet.has(Number(serviceId))
    );
    if (nextSharedServiceIds.length !== (multi.sharedServiceIds || []).length) {
      dispatch({ type: "SET_SHARED_SERVICES", payload: nextSharedServiceIds });
    }

    (multi.vehicles || []).forEach((vehicle) => {
      const nextServiceIds = (vehicle.serviceIds || []).filter(
        (serviceId) => !miniCheckServiceIdSet.has(Number(serviceId))
      );
      const nextExcludedSharedServiceIds = (vehicle.excludedSharedServiceIds || []).filter(
        (serviceId) => !miniCheckServiceIdSet.has(Number(serviceId))
      );

      if (
        nextServiceIds.length !== (vehicle.serviceIds || []).length ||
        nextExcludedSharedServiceIds.length !== (vehicle.excludedSharedServiceIds || []).length
      ) {
        dispatch({
          type: "PATCH_VEHICLE",
          payload: {
            key: vehicle.key,
            patch: {
              serviceIds: nextServiceIds,
              excludedSharedServiceIds: nextExcludedSharedServiceIds,
            },
          },
        });
      }
    });
  }, [isFirstCustomerBooking, miniCheckServiceIdSet, multi.sharedServiceIds, multi.vehicles]);

  useEffect(() => {
    let ignore = false;
    async function loadData() {
      setLoading(true);
      try {
        const [data, myAppointmentsPayload] = await Promise.all([
          loadBookingFormMasterData(),
          getMyAppointments().catch(() => []),
        ]);
        if (ignore) return;
        const myAppointmentCount = normalizeAppointmentsResponse(myAppointmentsPayload).length;
        setHasPreviousCustomerBookings(myAppointmentCount > 0);
        setCategories(data.categories);
        setServices(data.services);
        setBranches(data.branches);
        setBrands(data.brands);
        setModels(data.models);
        setInsuranceCompanies(data.insuranceCompanies);
        setVehicles(data.vehicles);
        setBranchOperatingHours(Array.isArray(data.branchOperatingHours) ? data.branchOperatingHours : []);
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
    scrollToTopForAlerts();
  }, [alertTick, error, notice, scrollToTopForAlerts]);

  useEffect(() => {
    if (currentStep !== 5) return;
    loadSlots();
  }, [currentStep, loadSlots]);

  useEffect(() => {
    if (currentStep !== 5) return undefined;

    const refreshSlotsWhenVisible = () => {
      if (typeof document !== "undefined" && document.hidden) return;
      void loadSlots();
    };

    const handleVisibilityChange = () => {
      if (typeof document !== "undefined" && !document.hidden) {
        refreshSlotsWhenVisible();
      }
    };

    if (typeof window !== "undefined") {
      window.addEventListener("focus", refreshSlotsWhenVisible);
    }
    if (typeof document !== "undefined") {
      document.addEventListener("visibilitychange", handleVisibilityChange);
    }

    return () => {
      if (typeof window !== "undefined") {
        window.removeEventListener("focus", refreshSlotsWhenVisible);
      }
      if (typeof document !== "undefined") {
        document.removeEventListener("visibilitychange", handleVisibilityChange);
      }
    };
  }, [currentStep, loadSlots]);

  function updateDraftVehicle(key, value) {
    if (key === "licensePlate") {
      setDraftVehicle((prev) => ({ ...prev, licensePlate: formatLicensePlateByFieldName(key, value) }));
      return;
    }
    if (key === "vin") {
      setDraftVehicle((prev) => ({ ...prev, vin: formatVinInput(value) }));
      return;
    }
    setDraftVehicle((prev) => ({ ...prev, [key]: value }));
  }

  function toggleExistingVehicle(vehicle) {
    dispatch({ type: "ADD_EXISTING", payload: asVehicleEntryFromExisting(vehicle) });
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
      showError("Loại dịch vụ dùng chung chỉ có thể hủy ở tab \"Áp dụng tất cả xe\".");
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

  function toggleService(serviceId) {
    const id = Number(serviceId);
    if (!Number.isFinite(id)) return;
    if (!step3Vehicle) {
      const has = multi.sharedServiceIds.includes(id);
      if (has) {
        dispatch({ type: "SET_SHARED_SERVICES", payload: multi.sharedServiceIds.filter((item) => item !== id) });
        return;
      }

      const oppositeSharedIds = getConflictingInspectionServiceIds(multi.sharedServiceIds, id);
      const nextSharedServiceIds = uniqIds([
        ...multi.sharedServiceIds.filter((item) => !oppositeSharedIds.includes(Number(item))),
        id,
      ]);
      dispatch({ type: "SET_SHARED_SERVICES", payload: nextSharedServiceIds });

      (multi.vehicles || []).forEach((vehicle) => {
        const effectiveServiceIds = getEffectiveServiceIds(vehicle);
        const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveServiceIds, id);
        if (oppositeServiceIds.length === 0) return;

        const nextVehicleServiceIds = (vehicle.serviceIds || []).filter(
          (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
        );
        const nextExcludedSharedServiceIds = (vehicle.excludedSharedServiceIds || []).filter(
          (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
        );

        const shouldPatchServices = nextVehicleServiceIds.length !== (vehicle.serviceIds || []).length;
        const shouldPatchExcluded = nextExcludedSharedServiceIds.length !== (vehicle.excludedSharedServiceIds || []).length;
        if (!shouldPatchServices && !shouldPatchExcluded) return;

        const patch = {};
        if (shouldPatchServices) patch.serviceIds = nextVehicleServiceIds;
        if (shouldPatchExcluded) patch.excludedSharedServiceIds = nextExcludedSharedServiceIds;
        dispatch({ type: "PATCH_VEHICLE", payload: { key: vehicle.key, patch } });
      });
      return;
    }

    if (multi.sharedServiceIds.includes(id)) {
      const hasEx = (step3Vehicle.excludedSharedServiceIds || []).includes(id);
      if (!hasEx) {
        dispatch({
          type: "PATCH_VEHICLE",
          payload: {
            key: step3Vehicle.key,
            patch: {
              excludedSharedServiceIds: [...(step3Vehicle.excludedSharedServiceIds || []), id],
            },
          },
        });
        return;
      }

      const effectiveServiceIds = getEffectiveServiceIds(step3Vehicle);
      const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveServiceIds, id);
      const nextExcludedSet = new Set(
        (step3Vehicle.excludedSharedServiceIds || []).filter((item) => Number(item) !== id).map(Number)
      );
      const nextVehicleServiceIds = (step3Vehicle.serviceIds || []).filter(
        (serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))
      );

      oppositeServiceIds
        .filter((serviceItemId) => multi.sharedServiceIds.includes(Number(serviceItemId)))
        .forEach((serviceItemId) => nextExcludedSet.add(Number(serviceItemId)));

      const nextExcludedSharedServiceIds = Array.from(nextExcludedSet);
      dispatch({
        type: "PATCH_VEHICLE",
        payload: {
          key: step3Vehicle.key,
          patch: {
            serviceIds: nextVehicleServiceIds,
            excludedSharedServiceIds: nextExcludedSharedServiceIds,
          },
        },
      });
      return;
    }

    const has = (step3Vehicle.serviceIds || []).includes(id);
    if (has) {
      dispatch({
        type: "PATCH_VEHICLE",
        payload: {
          key: step3Vehicle.key,
          patch: { serviceIds: step3Vehicle.serviceIds.filter((item) => item !== id) },
        },
      });
      return;
    }

    const effectiveServiceIds = getEffectiveServiceIds(step3Vehicle);
    const oppositeServiceIds = getConflictingInspectionServiceIds(effectiveServiceIds, id);
    const nextVehicleServiceIds = uniqIds([
      ...(step3Vehicle.serviceIds || []).filter((serviceItemId) => !oppositeServiceIds.includes(Number(serviceItemId))),
      id,
    ]);
    const nextExcludedSet = new Set((step3Vehicle.excludedSharedServiceIds || []).map(Number));
    oppositeServiceIds
      .filter((serviceItemId) => multi.sharedServiceIds.includes(Number(serviceItemId)))
      .forEach((serviceItemId) => nextExcludedSet.add(Number(serviceItemId)));

    dispatch({
      type: "PATCH_VEHICLE",
      payload: {
        key: step3Vehicle.key,
        patch: {
          serviceIds: nextVehicleServiceIds,
          excludedSharedServiceIds: Array.from(nextExcludedSet),
        },
      },
    });
  }

  function validateStep(stepNo) {
    if (stepNo === 1) {
      if (!String(customerForm.fullName || "").trim()) return "Vui lòng nhập họ tên khách hàng.";
      if (!String(customerForm.phone || "").trim()) return "Vui lòng nhập số điện thoại khách hàng.";
      if (!isValidVietnamesePhone(customerForm.phone)) return "Số điện thoại khách hàng không đúng định dạng Việt Nam.";
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

      for (const vehicle of insuranceVehicleOptions) {
        const issue = validateInsuranceSnapshotByMode(insuranceByVehicleKey[vehicle.key], { mode: "booking" });
        if (issue) {
          return `Xe ${vehicle.label}: ${issue}`;
        }
      }
    }
    if (stepNo === 4 && !selectedBranchId) return "Vui lòng chọn chi nhánh.";
    if (stepNo === 4) {
      for (const vehicle of multi.vehicles || []) {
        const unresolvedPackageIds = findUnresolvedPackageIds(getEffectiveServiceIds(vehicle), serviceById);
        if (unresolvedPackageIds.length > 0) {
          return `Xe ${vehicle.displayLabel}: có gói dịch vụ chưa hợp lệ (#${unresolvedPackageIds.join(", #")}). Vui lòng chọn lại trước khi qua bước chọn giờ.`;
        }
      }
    }
    if (stepNo === 5) {
      if (multi.slotMode === SLOT_MODE.PARALLEL && !selectedDate) return "Vui lòng chọn ngày đặt lịch.";
      if (multi.slotMode === SLOT_MODE.INDIVIDUAL) {
        const missingDateVehicle = multi.vehicles.find((item) => !String(selectedDatesByVehicle[item.key] || "").trim());
        if (missingDateVehicle) return `Xe ${missingDateVehicle.displayLabel}: vui lòng chọn ngày đặt lịch.`;
      }
      if (multi.slotMode === SLOT_MODE.PARALLEL && !multi.parallelSlot) return "Vui lòng chọn khung giờ song song.";
      if (multi.slotMode === SLOT_MODE.PARALLEL) {
        const missingHeldVehicles = (multi.vehicles || [])
          .filter((item) => !Boolean(multi.slotHoldTokens?.[item.key]))
          .map((item) => item.displayLabel);
        if (missingHeldVehicles.length > 0) {
          return `Các xe chưa giữ chỗ thành công: ${missingHeldVehicles.join(", ")}. Vui lòng chọn lại khung giờ.`;
        }
      }
      if (multi.slotMode === SLOT_MODE.INDIVIDUAL) {
        const missingSlotVehicles = (multi.vehicles || [])
          .filter((item) => !Boolean(multi.selectedSlots[item.key]))
          .map((item) => item.displayLabel);
        if (missingSlotVehicles.length > 0) {
          return `Các xe chưa chọn khung giờ: ${missingSlotVehicles.join(", ")}.`;
        }
        const missingHeldVehicles = (multi.vehicles || [])
          .filter((item) => !Boolean(multi.slotHoldTokens?.[item.key]))
          .map((item) => item.displayLabel);
        if (missingHeldVehicles.length > 0) {
          return `Các xe chưa giữ chỗ thành công: ${missingHeldVehicles.join(", ")}. Vui lòng giữ chỗ lại cho các xe này.`;
        }
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
    setSlideDirection("next");
    setCurrentStep((prev) => Math.min(prev + 1, BOOKING_STEPS.length));
  }

  function goPrevious() {
    clearError();
    setSlideDirection("prev");
    setCurrentStep((prev) => Math.max(prev - 1, 1));
  }

  function toggleOddSlotsForSession(sessionKey) {
    setShowOddSlots((prev) => ({ ...prev, [sessionKey]: !prev[sessionKey] }));
  }

  function onSymptomChange(vehicleKey, value) {
    setVehicleSymptoms((prev) => ({ ...prev, [vehicleKey]: value }));
  }

  function buildVehicleNote(vehicle) {
    const note = String(vehicleSymptoms[vehicle.key] || "").trim();
    return note || null;
  }

  function buildBatchCustomerNote() {
    const items = (multi.vehicles || [])
      .map((vehicle) => {
        const note = String(vehicleSymptoms[vehicle.key] || "").trim();
        if (!note) return null;
        return `${vehicle.displayLabel}: ${note}`;
      })
      .filter(Boolean);
    return items.length > 0 ? items.join("\n") : null;
  }

  async function handleConfirm() {
    const issue = validateStep(5);
    if (issue) {
      showError(issue);
      setSlideDirection("prev");
      setCurrentStep(5);
      return;
    }
    setSubmitting(true);
    clearError();
    setSuccess("");
    setSuccessTargetTab("single");
    try {
      const groups = buildServiceGroups();
      if (multi.vehicles.length === 1) {
        setSuccessTargetTab("single");
        const vehicle = multi.vehicles[0];
        const slot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
        const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicle));
        const note = buildVehicleNote(vehicle);
        const itemIds = groups[0] || [];
        const split = splitBookingItemIds(itemIds);
      const insurancePayload = hasInsuranceServices(itemIds, serviceById)
        ? buildInsuranceSnapshotPayload(insuranceByVehicleKey[vehicle.key], { mode: "booking" })
        : buildInsuranceSnapshotPayload(null, { mode: "booking" });
        const created = await submitSingleBooking({
          branchId: Number(selectedBranchId),
          customerFullName: customerForm.fullName.trim(),
          customerPhone: customerForm.phone.trim(),
          bookingFlow: vehicleFlow,
          startTime: extractField(slot, ["startTime", "StartTime"]),
          endTime: extractField(slot, ["endTime", "EndTime"]),
          checkInWindowStart: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["startTime", "StartTime"]) : null,
          checkInWindowEnd: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["endTime", "EndTime"]) : null,
          requestedServiceIds: split.serviceIds.length > 0 ? split.serviceIds.join(",") : null,
          requestedPackageIds: split.packageIds.length > 0 ? split.packageIds.join(",") : null,
          slotHoldToken: multi.slotHoldTokens?.[vehicle.key] || null,
          customerNotes: note,
          vehicleId: vehicle.payload.vehicleId,
          vehicleModelId: vehicle.payload.vehicleModelId,
          licensePlate: vehicle.payload.licensePlate,
          vin: vehicle.payload.vin,
          color: vehicle.payload.color,
          manufactureYear: vehicle.payload.manufactureYear,
          ...insurancePayload,
        });
      } else {
        setSuccessTargetTab("group");
        const batchNote = buildBatchCustomerNote();
        const items = multi.vehicles.map((vehicle, index) => {
          const slot = multi.slotMode === SLOT_MODE.PARALLEL ? multi.parallelSlot : multi.selectedSlots[vehicle.key];
          const vehicleFlow = resolveSlotFlow(slot, getVehicleFlow(vehicle));
          const itemIds = groups[index] || [];
          const split = splitBookingItemIds(itemIds);
          const note = buildVehicleNote(vehicle);
      const insurancePayload = hasInsuranceServices(itemIds, serviceById)
        ? buildInsuranceSnapshotPayload(insuranceByVehicleKey[vehicle.key], { mode: "booking" })
        : buildInsuranceSnapshotPayload(null, { mode: "booking" });
          return {
            branchId: Number(selectedBranchId),
            customerFullName: customerForm.fullName.trim(),
            customerPhone: customerForm.phone.trim(),
            bookingFlow: vehicleFlow,
            startTime: extractField(slot, ["startTime", "StartTime"]),
            endTime: extractField(slot, ["endTime", "EndTime"]),
            checkInWindowStart: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["startTime", "StartTime"]) : null,
            checkInWindowEnd: vehicleFlow === BOOKING_FLOW.FLEXIBLE ? extractField(slot, ["endTime", "EndTime"]) : null,
            requestedServiceIds: split.serviceIds.length > 0 ? split.serviceIds.join(",") : null,
            requestedPackageIds: split.packageIds.length > 0 ? split.packageIds.join(",") : null,
            slotHoldToken: multi.slotHoldTokens?.[vehicle.key] || null,
            customerNotes: note,
            vehicleId: vehicle.payload.vehicleId,
            vehicleModelId: vehicle.payload.vehicleModelId,
            licensePlate: vehicle.payload.licensePlate,
            vin: vehicle.payload.vin,
            color: vehicle.payload.color,
            manufactureYear: vehicle.payload.manufactureYear,
            ...insurancePayload,
          };
        });
        await submitBatchBooking({ branchId: Number(selectedBranchId), customerNote: batchNote, items });
      }
      setSuccess(buildBookingSuccessMessage());
      setSlideDirection("next");
      setCurrentStep(6);
    } catch (submitError) {
      const message = submitError?.message || "Đặt lịch thất bại. Vui lòng thử lại.";
      if (isSlotHoldStaleMessage(message)) {
        setSlideDirection("prev");
        setCurrentStep(5);
        const failedVehicles = await refreshSelectedSlotHolds();
        await loadSlots();
        if (Array.isArray(failedVehicles) && failedVehicles.length > 0) {
          showError(`Giữ chỗ đã hết hạn và không thể giữ lại cho: ${failedVehicles.join(", ")}. Vui lòng chọn lại khung giờ cho các xe này.`);
        } else {
          showNotice("Thông tin giữ chỗ đã được đồng bộ lại. Vui lòng bấm xác nhận lại.");
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
            <div key={currentStep} className={`booking-step-slide-container slide-${slideDirection}`}>
            {currentStep === 1 ? (
              <BookingStep1Customer
                customerForm={customerForm}
                setCustomerForm={setCustomerForm}
                linkedVehicles={linkedVehicles}
                multiVehicles={multi.vehicles}
                brands={brands}
                models={models}
                activeSymptomVehicleKey={activeSymptomVehicleKey}
                setActiveSymptomVehicleKey={setActiveSymptomVehicleKey}
                customerHasNoSavedVehicles={customerHasNoSavedVehicles}
                draftVehicle={draftVehicle}
                updateDraftVehicle={updateDraftVehicle}
                toggleExistingVehicle={toggleExistingVehicle}
                addNewVehicle={addNewVehicle}
                handleRemoveVehicle={handleRemoveVehicle}
                activeSymptomVehicle={activeSymptomVehicle}
                vehicleSymptoms={vehicleSymptoms}
                onSymptomChange={onSymptomChange}
              />
            ) : null}

            {currentStep === 2 ? (
              <BookingStep2Categories
                multiVehicles={multi.vehicles}
                step2VehicleKey={step2VehicleKey}
                setStep2VehicleKey={setStep2VehicleKey}
                dispatch={dispatch}
                step2Vehicle={step2Vehicle}
                handleRemoveVehicle={handleRemoveVehicle}
                categories={categories}
                step2CompatibleCategoryIds={step2CompatibleCategoryIds}
                getEffectiveCategoryIds={getEffectiveCategoryIds}
                sharedCategoryIds={multi.sharedCategoryIds}
                toggleVehicleCategory={toggleVehicleCategory}
                toggleSharedCategory={toggleSharedCategory}
              />
            ) : null}

            {currentStep === 3 ? (
              <BookingStep3Services
                multiVehicles={multi.vehicles}
                step3VehicleKey={step3VehicleKey}
                setStep3VehicleKey={setStep3VehicleKey}
                step3Vehicle={step3Vehicle}
                handleRemoveVehicle={handleRemoveVehicle}
                step3CategoryIds={step3CategoryIds}
                activeCategoryId={activeCategoryId}
                setActiveCategoryId={setActiveCategoryId}
                categories={categories}
                toggleVehicleCategory={toggleVehicleCategory}
                toggleSharedCategory={toggleSharedCategory}
                servicesByCategory={servicesByCategory}
                getEffectiveServiceIds={getEffectiveServiceIds}
                sharedServiceIds={multi.sharedServiceIds}
                toggleService={toggleService}
                insuranceVehicleOptions={insuranceVehicleOptions}
                activeInsuranceVehicleKey={activeInsuranceVehicleKey}
                setActiveInsuranceVehicleKey={setActiveInsuranceVehicleKey}
                activeInsuranceSnapshot={activeInsuranceSnapshot}
                handleInsuranceSnapshotChange={handleInsuranceSnapshotChange}
                handleInsuranceAssetUpload={handleInsuranceAssetUpload}
                handleInsuranceAssetRemove={handleInsuranceAssetRemove}
                insuranceUploadStateByVehicleKey={insuranceUploadStateByVehicleKey}
                insuranceCompanies={insuranceCompanies}
                insuranceCompanyNameById={insuranceCompanyNameById}
              />
            ) : null}

            {currentStep === 4 ? (
              <BookingStep4Branch
                branches={branches}
                selectedBranchId={selectedBranchId}
                setSelectedBranchId={setSelectedBranchId}
                dispatch={dispatch}
              />
            ) : null}

            {currentStep === 5 ? (
              <BookingStep5Slots
                multi={multi}
                dispatch={dispatch}
                activeVehicle={activeVehicle}
                setActiveVehicleKey={setActiveVehicleKey}
                effectiveSelectedDate={effectiveSelectedDate}
                branchOperatingHours={branchOperatingHours}
                selectedBranchId={selectedBranchId}
                showError={showError}
                releaseHoldTokenIfAny={releaseHoldTokenIfAny}
                setSelectedDatesByVehicle={setSelectedDatesByVehicle}
                setSelectedDate={setSelectedDate}
                slotsLoading={slotsLoading}
                effectiveSlotFlow={effectiveSlotFlow}
                slotSelectionGuide={slotSelectionGuide}
                activeSession={activeSession}
                setActiveSession={setActiveSession}
                canToggleOddSlots={canToggleOddSlots}
                toggleOddSlotsForSession={toggleOddSlotsForSession}
                showOddSlots={showOddSlots}
                displayedSlots={displayedSlots}
                handleSelectSlot={handleSelectSlot}
              />
            ) : null}

            {currentStep === 6 ? (
              <BookingStep6Confirmation
                success={success}
                navigate={navigate}
                successTargetTab={successTargetTab}
                customerForm={customerForm}
                selectedBranch={selectedBranch}
                confirmationVehicleSummaries={confirmationVehicleSummaries}
                flexibleCompletionNoteByVehicleKey={flexibleCompletionNoteByVehicleKey}
                totalPrice={totalPrice}
                totalDurationLabel={totalDurationLabel}
                handleConfirm={handleConfirm}
                submitting={submitting}
              />
            ) : null}
            </div>

            {!(currentStep === 6 && success) ? <div className="wizard-actions"><button type="button" className="ghost-btn" onClick={goPrevious} disabled={currentStep === 1 || submitting}>Quay lại</button>{currentStep < BOOKING_STEPS.length ? <button type="button" className="next-btn" onClick={goNext} disabled={submitting}>Tiếp tục</button> : null}</div> : null}
          </div>
          {currentStep !== 6 ? (
            <BookingSummarySidebar
              summaryItems={summaryItems}
              summaryVehicleDetails={summaryVehicleDetails}
              flexibleCompletionNoteByVehicleKey={flexibleCompletionNoteByVehicleKey}
              totalPrice={totalPrice}
              fleetCompletionLabel={fleetCompletionLabel}
            />
          ) : null}
        </section>
      </div>
    </div>
  );
}


















