import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
import Header from "../../../shared/components/Header";
import DateTimePicker from "../../workshop/components/DateTimePicker";
import {
  BOOKING_FLOW,
  BOOKING_STEPS,
  DEFAULT_DIAGNOSTIC_SERVICE_CODE,
  INITIAL_NEW_VEHICLE_FORM,
  INITIAL_ODD_SLOT_VISIBILITY,
  SESSION_TABS,
} from "../constants/bookingConstants";
import {
  loadBookingAvailableSlots,
  loadBookingFormMasterData,
  submitBatchBooking,
  submitSingleBooking,
} from "../api/bookingFormApi";
import {
  buildCustomerNotesPayload,
  extractField,
  formatLicensePlateInput,
  formatStoredVehicleLabel,
  formatVehicleDescriptor,
  formatVnd,
  iconByIndex,
  isQuickCategory,
  normalizeText,
  timeLabel,
  toIsoDateInput,
} from "../utils/bookingCommon";
import {
  buildDisplayedSlots,
  buildSlotsBySession,
  getAutoExpandOddStateForFixedFlow,
  resolveFirstActiveSession,
} from "../utils/bookingSlots";
import {
  buildSelectedCategorySummaryLabel,
  buildSummaryItems,
} from "../utils/bookingSummary";
import { validateBookingStep } from "../utils/bookingValidation";
import { buildCurrentVehiclePayload } from "../utils/bookingVehicle";
import "./BookingPage.css";

export default function BookingPage() {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [categories, setCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [branches, setBranches] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [vehicles, setVehicles] = useState([]);

  const [currentStep, setCurrentStep] = useState(1);

  const [selectedCategoryIds, setSelectedCategoryIds] = useState([]);
  const [activeServiceCategoryId, setActiveServiceCategoryId] = useState("");
  const [selectedServiceIds, setSelectedServiceIds] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState("");

  const [customerForm, setCustomerForm] = useState({ fullName: "", phone: "" });
  const [symptomInputs, setSymptomInputs] = useState([""]);
  const [focusedSymptomIndex, setFocusedSymptomIndex] = useState(-1);

  const [vehicleMode, setVehicleMode] = useState("new");
  const [selectedVehicleId, setSelectedVehicleId] = useState("");
  const [newVehicle, setNewVehicle] = useState(() => ({ ...INITIAL_NEW_VEHICLE_FORM }));
  const [isBatchBooking, setIsBatchBooking] = useState(false);
  const [batchVehicles, setBatchVehicles] = useState([]);

  const [selectedDate, setSelectedDate] = useState(toIsoDateInput(new Date()));
  const [slots, setSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [bookingFlow, setBookingFlow] = useState(BOOKING_FLOW.FIXED);
  const [activeSession, setActiveSession] = useState("MORNING");
  const [showOddSlots, setShowOddSlots] = useState(() => ({ ...INITIAL_ODD_SLOT_VISIBILITY }));

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", {
        replace: true,
        state: { from: { pathname: "/booking/form" } },
      });
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

    const links = (customerVehicles || []).filter(
      (link) =>
        Number(extractField(link, ["customerId", "CustomerId"])) === customerId &&
        extractField(link, ["isDeleted", "IsDeleted"], false) !== true
    );

    return links
      .map((link) => {
        const vehicleId = Number(extractField(link, ["vehicleId", "VehicleId"]));
        const vehicle = vehicles.find((item) => Number(extractField(item, ["id", "Id"])) === vehicleId);
        if (!vehicle) return null;
        const modelId = Number(extractField(vehicle, ["modelId", "ModelId"]));
        const matchedModel = models.find((item) => Number(extractField(item, ["id", "Id"])) === modelId);
        const matchedBrand = brands.find(
          (item) =>
            Number(extractField(item, ["id", "Id"])) === Number(extractField(matchedModel, ["brandId", "BrandId"]))
        );
        const resolvedModelName =
          extractField(vehicle, ["modelName", "ModelName"], "") ||
          extractField(matchedModel, ["modelName", "ModelName"], "");
        const resolvedBrandName =
          extractField(vehicle, ["brandName", "BrandName"], "") ||
          extractField(matchedBrand, ["name", "Name"], "");
        return {
          id: Number(extractField(vehicle, ["id", "Id"])),
          modelId,
          licensePlate: extractField(vehicle, ["licensePlate", "LicensePlate"], ""),
          vin: extractField(vehicle, ["vin", "Vin"], ""),
          color: extractField(vehicle, ["color", "Color"], ""),
          brandName: resolvedBrandName,
          modelName: resolvedModelName,
        };
      })
      .filter(Boolean);
  }, [brands, customerRecord, customerVehicles, models, user?.id, vehicles]);

  const selectedCategories = useMemo(
    () =>
      categories.filter((item) => {
        const categoryId = Number(extractField(item, ["id", "Id"]));
        return selectedCategoryIds.includes(categoryId);
      }),
    [categories, selectedCategoryIds]
  );
  const selectedCategory = useMemo(
    () =>
      categories.find(
        (item) => String(extractField(item, ["id", "Id"])) === String(activeServiceCategoryId)
      ) || selectedCategories[0] || null,
    [activeServiceCategoryId, categories, selectedCategories]
  );
  const selectedCategorySummaryLabel = useMemo(
    () => buildSelectedCategorySummaryLabel(selectedCategories),
    [selectedCategories]
  );
  const hasQuickCategorySelected = useMemo(
    () => selectedCategories.some((item) => isQuickCategory(item)),
    [selectedCategories]
  );

  const servicesByCategory = useMemo(() => {
    if (!activeServiceCategoryId) return [];
    return (services || []).filter(
      (item) =>
        String(extractField(item, ["categoryId", "CategoryId"])) === String(activeServiceCategoryId) &&
        extractField(item, ["isActive", "IsActive"], true) !== false
    );
  }, [activeServiceCategoryId, services]);

  const selectedServices = useMemo(
    () => services.filter((item) => selectedServiceIds.includes(Number(extractField(item, ["id", "Id"])))),
    [selectedServiceIds, services]
  );
  const serviceCategoryById = useMemo(() => {
    const map = new Map();
    for (const service of services) {
      const serviceId = Number(extractField(service, ["id", "Id"]));
      const categoryId = Number(extractField(service, ["categoryId", "CategoryId"]));
      if (Number.isFinite(serviceId) && Number.isFinite(categoryId)) {
        map.set(serviceId, categoryId);
      }
    }
    return map;
  }, [services]);
  const autoDiagnosticService = useMemo(() => {
    return (
      services.find((item) => normalizeText(extractField(item, ["code", "Code"], "")) === normalizeText(DEFAULT_DIAGNOSTIC_SERVICE_CODE)) ||
      services.find((item) => normalizeText(extractField(item, ["name", "Name"], "")).includes("chuan doan tieu chuan")) ||
      services.find((item) => normalizeText(extractField(item, ["name", "Name"], "")).includes("chuẩn đoán tiêu chuẩn")) ||
      services.find((item) => normalizeText(extractField(item, ["name", "Name"], "")).includes("chan doan tieu chuan"))
    );
  }, [services]);
  const effectiveServices = useMemo(() => {
    if (selectedServices.length > 0) return selectedServices;
    if (currentStep >= 2 && bookingFlow === BOOKING_FLOW.FLEXIBLE && autoDiagnosticService) return [autoDiagnosticService];
    return [];
  }, [autoDiagnosticService, bookingFlow, currentStep, selectedServices]);
  const effectiveServiceIds = useMemo(
    () => effectiveServices.map((item) => Number(extractField(item, ["id", "Id"]))).filter(Number.isFinite),
    [effectiveServices]
  );
  const isUsingAutoDiagnostic = bookingFlow === BOOKING_FLOW.FLEXIBLE && selectedServiceIds.length === 0 && effectiveServices.length > 0;

  const selectedBranch = useMemo(
    () => branches.find((item) => String(item.id) === String(selectedBranchId) || String(item.Id) === String(selectedBranchId)),
    [branches, selectedBranchId]
  );

  const selectedExistingVehicle = useMemo(
    () => linkedVehicles.find((item) => String(item.id) === String(selectedVehicleId)),
    [linkedVehicles, selectedVehicleId]
  );

  const modelOptions = useMemo(() => {
    if (!newVehicle.brandId) return [];
    return (models || []).filter(
      (item) =>
        String(extractField(item, ["brandId", "BrandId"])) === String(newVehicle.brandId) &&
        extractField(item, ["isDeleted", "IsDeleted"], false) !== true
    );
  }, [models, newVehicle.brandId]);

  const selectedModel = useMemo(
    () => models.find((item) => String(extractField(item, ["id", "Id"])) === String(newVehicle.modelId)),
    [models, newVehicle.modelId]
  );
  const selectedBrand = useMemo(
    () => brands.find((item) => String(extractField(item, ["id", "Id"])) === String(newVehicle.brandId)),
    [brands, newVehicle.brandId]
  );

  const totalPrice = useMemo(
    () => effectiveServices.reduce((sum, item) => sum + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0),
    [effectiveServices]
  );

  const totalMinutes = useMemo(
    () => effectiveServices.reduce((sum, item) => sum + Number(extractField(item, ["standardMinutes", "StandardMinutes"], 0) || 0), 0),
    [effectiveServices]
  );

  const availableSlots = useMemo(
    () => (slots || []).filter((slot) => extractField(slot, ["isAvailable", "IsAvailable"], false)),
    [slots]
  );

  const slotsBySession = useMemo(() => buildSlotsBySession(availableSlots), [availableSlots]);

  const displayedSlots = useMemo(() =>
    buildDisplayedSlots({ slotsBySession, activeSession, bookingFlow, showOddSlots }),
    [activeSession, bookingFlow, showOddSlots, slotsBySession]
  );

  const summaryItems = useMemo(() => buildSummaryItems({
    customerForm,
    effectiveServices,
    selectedDate,
    selectedSlot,
    selectedExistingVehicle,
    newVehicle,
    selectedBrand,
    selectedModel,
    selectedCategoryIds,
    selectedServiceIds,
    bookingFlow,
    selectedBranchId,
    selectedBranch,
    selectedCategories,
    linkedVehicles,
    vehicleMode,
    selectedVehicleId,
    isUsingAutoDiagnostic,
    isBatchBooking,
    batchVehicles,
  }), [
    customerForm.fullName,
    customerForm.phone,
    linkedVehicles.length,
    newVehicle.brandId,
    newVehicle.licensePlate,
    newVehicle.modelId,
    newVehicle.vin,
    selectedBrand,
    selectedBranch,
    selectedBranchId,
    selectedCategories,
    selectedCategory,
    selectedCategoryIds.length,
    bookingFlow,
    selectedDate,
    selectedExistingVehicle,
    selectedModel?.modelName,
    selectedServiceIds.length,
    selectedVehicleId,
    effectiveServices,
    isUsingAutoDiagnostic,
    selectedSlot,
    vehicleMode,
    isBatchBooking,
    batchVehicles,
  ]);

  const progressPercent = useMemo(() => (currentStep / BOOKING_STEPS.length) * 100, [currentStep]);

  const loadSlots = useCallback(async (branchId, date, flow, serviceIds) => {
    if (!branchId || !date) {
      setSlots([]);
      setSelectedSlot(null);
      return;
    }

    setSlotsLoading(true);
    setSelectedSlot(null);
    setSlots([]);
    setShowOddSlots({ ...INITIAL_ODD_SLOT_VISIBILITY });

    try {
      const data = await loadBookingAvailableSlots({
        branchId,
        date,
        serviceIds,
        bookingFlow: flow,
      });
      setSlots(Array.isArray(data) ? data : []);
    } catch {
      setSlots([]);
    } finally {
      setSlotsLoading(false);
    }
  }, []);

  useEffect(() => {
    let ignore = false;

    async function loadData() {
      setLoading(true);
      try {
        const {
          categories: categoryData,
          services: serviceData,
          branches: branchData,
          brands: brandData,
          models: modelData,
          customers: customerData,
          customerVehicles: customerVehicleData,
          vehicles: vehicleData,
        } = await loadBookingFormMasterData();

        if (ignore) return;

        setCategories(categoryData);
        setServices(serviceData);
        setBranches(branchData);
        setBrands(brandData);
        setModels(modelData);
        setCustomers(customerData);
        setCustomerVehicles(customerVehicleData);
        setVehicles(vehicleData);
      } catch {
        if (!ignore) {
          setError("Không thể tải dữ liệu đặt lịch. Vui lòng thử lại.");
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
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
    if (linkedVehicles.length > 0) {
      setVehicleMode("existing");
      setSelectedVehicleId(String(linkedVehicles[0].id));
      return;
    }

    setVehicleMode("new");
    setSelectedVehicleId("");
  }, [linkedVehicles]);

  useEffect(() => {
    if (selectedBranchId && selectedDate) {
      loadSlots(selectedBranchId, selectedDate, bookingFlow, selectedServiceIds);
    }
  }, [selectedBranchId, selectedDate, bookingFlow, selectedServiceIds, loadSlots]);

  useEffect(() => {
    if (selectedCategoryIds.length === 0) {
      setActiveServiceCategoryId("");
      return;
    }

    if (!selectedCategoryIds.some((id) => String(id) === String(activeServiceCategoryId))) {
      setActiveServiceCategoryId(String(selectedCategoryIds[0]));
    }
  }, [activeServiceCategoryId, selectedCategoryIds]);

  useEffect(() => {
    setBookingFlow((prev) => {
      if (hasQuickCategorySelected) return BOOKING_FLOW.FIXED;
      if (selectedServiceIds.length > 0 && prev === BOOKING_FLOW.FLEXIBLE) return BOOKING_FLOW.FIXED;
      return prev;
    });
  }, [hasQuickCategorySelected, selectedServiceIds.length]);

  useEffect(() => {
    setActiveSession(resolveFirstActiveSession(SESSION_TABS, slotsBySession));
  }, [slotsBySession]);

  useEffect(() => {
    if (bookingFlow !== BOOKING_FLOW.FIXED) return;
    setShowOddSlots((prev) => getAutoExpandOddStateForFixedFlow(SESSION_TABS, slotsBySession, prev));
  }, [bookingFlow, slotsBySession]);

  function updateNewVehicle(key, value) {
    setNewVehicle((prev) => ({ ...prev, [key]: value }));
  }

  function toggleCategorySelection(categoryId) {
    const normalizedId = Number(categoryId);
    if (!Number.isFinite(normalizedId)) return;

    const currentlySelected = selectedCategoryIds.includes(normalizedId);
    const nextCategoryIds = currentlySelected
      ? selectedCategoryIds.filter((id) => id !== normalizedId)
      : [...selectedCategoryIds, normalizedId];

    setSelectedCategoryIds(nextCategoryIds);

    if (nextCategoryIds.length === 0) {
      setActiveServiceCategoryId("");
    } else if (!nextCategoryIds.some((id) => String(id) === String(activeServiceCategoryId))) {
      setActiveServiceCategoryId(String(nextCategoryIds[0]));
    }

    setSelectedServiceIds((prev) =>
      prev.filter((serviceId) => {
        const categoryOfService = serviceCategoryById.get(serviceId);
        return categoryOfService && nextCategoryIds.includes(categoryOfService);
      })
    );

    setError("");
  }

  function toggleService(serviceId) {
    setSelectedServiceIds((prev) =>
      prev.includes(serviceId) ? prev.filter((id) => id !== serviceId) : [...prev, serviceId]
    );
  }

  function toggleOddSlotsForSession(sessionKey) {
    setShowOddSlots((prev) => ({ ...prev, [sessionKey]: !prev[sessionKey] }));
  }

  function validateStep(stepNumber) {
    return validateBookingStep(stepNumber, {
      selectedCategoryIds,
      bookingFlow,
      selectedServiceIds,
      selectedBranchId,
      customerForm,
      selectedDate,
      selectedSlot,
      isBatchBooking,
      batchVehicles,
      linkedVehicles,
      vehicleMode,
      selectedVehicleId,
      newVehicle,
      BOOKING_FLOW,
    });
  }

  function goNext() {
    const issue = validateStep(currentStep);
    if (issue) {
      setError(issue);
      return;
    }

    setError("");
    setCurrentStep((prev) => Math.min(prev + 1, BOOKING_STEPS.length));
  }

  function goPrevious() {
    setError("");
    setCurrentStep((prev) => Math.max(prev - 1, 1));
  }

  function handleSymptomChange(index, value) {
    setSymptomInputs((prev) => {
      const next = [...prev];
      next[index] = value;
      const isLast = index === next.length - 1;
      if (isLast && String(value || "").trim() && next.length < 12) {
        next.push("");
      }
      return next;
    });
  }

  function handleSymptomFocus(index) {
    setFocusedSymptomIndex(index);
  }

  function handleAddBatchVehicle() {
    const built = buildCurrentVehiclePayload({
      linkedVehicles,
      vehicleMode,
      selectedExistingVehicle,
      newVehicle,
      selectedBrand,
      selectedModel,
    });
    if (built.error) {
      setError(built.error);
      return;
    }

    setBatchVehicles((prev) => {
      if (prev.some((item) => item.dedupeKey === built.dedupeKey)) {
        setError("Xe này đã có trong danh sách đặt lịch.");
        return prev;
      }

      setError("");
      return [...prev, built];
    });
  }

  function handleRemoveBatchVehicle(indexToRemove) {
    setBatchVehicles((prev) => prev.filter((_, index) => index !== indexToRemove));
  }

  async function handleConfirmBooking() {
    setError("");
    setSuccess("");

    const issue = validateStep(4);
    if (issue) {
      setError(issue);
      setCurrentStep(4);
      return;
    }

    setSubmitting(true);

    try {
      const slotStart = extractField(selectedSlot, ["startTime", "StartTime"]);
      const slotEnd = extractField(selectedSlot, ["endTime", "EndTime"]);
      const isFlexibleFlow = bookingFlow === BOOKING_FLOW.FLEXIBLE;
      const customerNotesPayload = buildCustomerNotesPayload(symptomInputs);

      const commonPayload = {
        branchId: Number(selectedBranchId),
        customerFullName: customerForm.fullName.trim(),
        customerPhone: customerForm.phone.trim(),
        bookingFlow,
        startTime: isFlexibleFlow ? null : slotStart,
        endTime: isFlexibleFlow ? null : slotEnd,
        checkInWindowStart: isFlexibleFlow ? slotStart : null,
        checkInWindowEnd: isFlexibleFlow ? slotEnd : null,
        requestedServiceIds: effectiveServiceIds.length > 0 ? effectiveServiceIds.join(",") : null,
        customerNotes: customerNotesPayload,
      };

      if (isBatchBooking) {
        const result = await submitBatchBooking({
          branchId: Number(selectedBranchId),
          customerNote: customerNotesPayload,
          items: batchVehicles.map((vehicle) => ({
            ...commonPayload,
            vehicleId: vehicle.payload.vehicleId,
            vehicleModelId: vehicle.payload.vehicleModelId,
            licensePlate: vehicle.payload.licensePlate,
            vin: vehicle.payload.vin,
            color: vehicle.payload.color,
          })),
        });

        const groupCode = extractField(result, ["groupCode", "GroupCode"], "");
        setSuccess(
          groupCode
            ? `Đặt lịch nhóm thành công (${groupCode}) cho ${batchVehicles.length} xe. Chúng tôi sẽ xác nhận sớm nhất.`
            : `Đặt lịch nhóm thành công cho ${batchVehicles.length} xe. Chúng tôi sẽ xác nhận sớm nhất.`
        );
      } else {
        const built = buildCurrentVehiclePayload({
          linkedVehicles,
          vehicleMode,
          selectedExistingVehicle,
          newVehicle,
          selectedBrand,
          selectedModel,
        });
        if (built.error) {
          setError(built.error);
          setCurrentStep(4);
          return;
        }

        await submitSingleBooking({
          ...commonPayload,
          vehicleId: built.payload.vehicleId,
          vehicleModelId: built.payload.vehicleModelId,
          licensePlate: built.payload.licensePlate,
          vin: built.payload.vin,
          color: built.payload.color,
        });
        setSuccess("Đặt lịch thành công. Chúng tôi sẽ xác nhận lịch hẹn của bạn trong thời gian sớm nhất.");
      }

      setCurrentStep(5);
    } catch (submitError) {
      setError(submitError?.message || "Đặt lịch thất bại. Vui lòng kiểm tra lại thông tin.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return (
      <div className="booking-page-shell">
        <div className="booking-fixed-header">
          <Header overlay />
        </div>
        <div className="booking-loading">Đang tải dữ liệu đặt lịch...</div>
      </div>
    );
  }

  return (
    <div className="booking-page-shell">
      <div className="booking-fixed-header">
        <Header overlay />
      </div>
      <div className="booking-flow">
        <section className="booking-hero">
          <p>HỆ THỐNG BOOKING MY GARAGE</p>
          <h1>Đặt lịch dịch vụ chuyên nghiệp trong vài bước</h1>
          <span>Chọn đúng nhóm dịch vụ, kỹ thuật và thời gian bạn mong muốn.</span>
        </section>

        <section className="booking-progress-shell">
        <div className="booking-progress-energy">
          <span style={{ width: `${progressPercent}%` }} />
        </div>
        <div className="booking-progress-steps">
          {BOOKING_STEPS.map((label, idx) => {
            const stepNo = idx + 1;
            const isDone = stepNo < currentStep;
            const isCurrent = stepNo === currentStep;
            return (
              <article
                key={label}
                className={`booking-progress-step ${isDone ? "done" : ""} ${isCurrent ? "current" : ""}`}
              >
                <strong>{String(stepNo).padStart(2, "0")}</strong>
                <span>{label}</span>
              </article>
            );
          })}
        </div>
      </section>

      {error && <div className="booking-alert booking-alert-error">{error}</div>}

        <section className={`booking-layout ${currentStep === 5 ? "step5-layout" : ""}`}>
        <div className="booking-main-panel">
          {currentStep === 1 && (
            <div className="booking-card">
              <header className="booking-card-head">
                <h2>Chọn loại dịch vụ</h2>
                <p>Bắt đầu bằng cách chọn nhóm dịch vụ phù hợp với nhu cầu xe của bạn.</p>
              </header>
              <div className="category-grid">
                {categories.map((category) => {
                  const categoryId = Number(extractField(category, ["id", "Id"]));
                  const name = extractField(category, ["name", "Name"], "");
                  const desc = extractField(category, ["description", "Description"], "");
                  if (!name) return null;
                  const selected = selectedCategoryIds.includes(categoryId);
                  return (
                    <button
                      key={categoryId}
                      type="button"
                      className={`category-card ${selected ? "selected" : ""}`}
                      onClick={() => toggleCategorySelection(categoryId)}
                    >
                      <span className="category-icon" aria-hidden="true">{iconByIndex(Number(categoryId) || 0)}</span>
                      <h3>{name}</h3>
                      {desc ? <p>{desc}</p> : null}
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {currentStep === 2 && (
            <div className="booking-card">
              <header className="booking-card-head">
                <h2>Chọn dịch vụ cụ thể</h2>
                <p>
                  Nhóm đang xem: <strong>{extractField(selectedCategory, ["name", "Name"], "Chưa chọn")}</strong>
                </p>
                <div className="step2-category-actions">
                  <button
                    type="button"
                    className="ghost-btn"
                    onClick={() => {
                      setCurrentStep(1);
                      setError("");
                    }}
                  >
                    + Chọn thêm loại dịch vụ
                  </button>
                </div>
              </header>

              <div className="flow-mode-switch">
                <button
                  type="button"
                  className={bookingFlow === BOOKING_FLOW.FIXED ? "active" : ""}
                  onClick={() => {
                    setBookingFlow(BOOKING_FLOW.FIXED);
                    setError("");
                  }}
                >
                  Giờ cố định
                  <small>Khách chốt slot cụ thể</small>
                </button>
                <button
                  type="button"
                  className={bookingFlow === BOOKING_FLOW.FLEXIBLE ? "active" : ""}
                  disabled={hasQuickCategorySelected}
                  onClick={() => {
                    if (hasQuickCategorySelected) return;
                    setBookingFlow(BOOKING_FLOW.FLEXIBLE);
                    setSelectedServiceIds([]);
                    setSelectedSlot(null);
                    setError("");
                  }}
                >
                  Check-in linh hoạt
                  <small>{hasQuickCategorySelected ? "Không áp dụng khi có dịch vụ nhanh" : "Mang xe trong khung giờ nhận xe"}</small>
                </button>
              </div>

              {bookingFlow === BOOKING_FLOW.FLEXIBLE && (
                <div className="booking-empty flow-note">
                  Dành cho ca cần kiểm tra sâu hoặc chưa xác định hạng mục. Hệ thống sẽ tự gán dịch vụ <strong>Chuẩn đoán tiêu chuẩn</strong> để giữ chỗ và gara sẽ tư vấn gói phù hợp khi tiếp nhận xe.
                </div>
              )}

              <div className="selected-category-tabs">
                {selectedCategories.map((category) => {
                  const categoryId = String(extractField(category, ["id", "Id"]));
                  const isActiveTab = String(activeServiceCategoryId) === categoryId;
                  return (
                    <div
                      key={categoryId}
                      className={`selected-category-tab ${isActiveTab ? "active" : ""}`}
                    >
                      <button
                        type="button"
                        className="selected-category-tab-main"
                        onClick={() => {
                          setActiveServiceCategoryId(categoryId);
                          setError("");
                        }}
                      >
                        {extractField(category, ["name", "Name"], `Nhóm ${categoryId}`)}
                      </button>
                      <button
                        type="button"
                        className="selected-category-tab-remove"
                        aria-label={`Bo loai dich vu ${extractField(category, ["name", "Name"], "")}`}
                        onClick={() => toggleCategorySelection(Number(categoryId))}
                      >
                        ×
                      </button>
                    </div>
                  );
                })}
              </div>

              <div className="service-list">
                {bookingFlow !== BOOKING_FLOW.FLEXIBLE && servicesByCategory.length === 0 ? (
                  <div className="booking-empty">Chưa có dịch vụ cụ thể cho loại đang chọn.</div>
                ) : null}

                {bookingFlow === BOOKING_FLOW.FIXED && servicesByCategory.map((service) => {
                  const serviceId = Number(extractField(service, ["id", "Id"]));
                  const serviceName = extractField(service, ["name", "Name"], "");
                  if (!serviceName) return null;
                  const selected = selectedServiceIds.includes(serviceId);
                  const standardMinutes = extractField(service, ["standardMinutes", "StandardMinutes"], 0);
                  return (
                    <button
                      key={serviceId}
                      type="button"
                      className={`service-card ${selected ? "selected" : ""}`}
                      onClick={() => toggleService(serviceId)}
                    >
                      <span className="service-icon" aria-hidden="true">{iconByIndex(serviceId || 0)}</span>
                      <div className="service-card-body">
                        <h3>{serviceName}</h3>
                        {extractField(service, ["description", "Description"], "")
                          ? <p>{extractField(service, ["description", "Description"], "")}</p>
                          : null}
                        {Number(standardMinutes) > 0 ? <small>Thời gian dự kiến: {standardMinutes} phút</small> : null}
                      </div>
                      <aside className="service-card-side">
                        <strong>{formatVnd(extractField(service, ["basePrice", "BasePrice"], 0))}</strong>
                        <em>{selected ? "Đã chọn" : "+ Chọn dịch vụ"}</em>
                      </aside>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {currentStep === 3 && (
            <div className="booking-card">
              <header className="booking-card-head">
                <h2>Bước 3: Chọn chi nhánh</h2>
                <p>Chọn điểm tiếp nhận thuận tiện cho bạn.</p>
              </header>

              <div className="branch-list">
                {branches.map((branch) => {
                  const id = extractField(branch, ["id", "Id"]);
                  const selected = String(id) === String(selectedBranchId);
                  return (
                    <button
                      key={id}
                      type="button"
                      className={`branch-card ${selected ? "selected" : ""}`}
                      onClick={() => {
                        setSelectedBranchId(String(id));
                        setError("");
                      }}
                    >
                      <h3>{extractField(branch, ["name", "Name"], "Chi nhánh")}</h3>
                      <p>{extractField(branch, ["address", "Address"], "")}</p>
                      <small>
                        Hotline: {extractField(branch, ["hotline", "Hotline"], "Đang cập nhật")}
                      </small>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {currentStep === 4 && (
            <div className="booking-card">
              <div className="step4-section">
                <h3>Thông tin khách hàng</h3>
                <div className="form-grid step4-form-grid">
                  <label>
                    Họ và tên <span>*</span>
                    <input
                      type="text"
                      value={customerForm.fullName}
                      onChange={(e) => setCustomerForm((prev) => ({ ...prev, fullName: e.target.value }))}
                      placeholder="Nhập họ tên khách hàng"
                    />
                  </label>

                  <label>
                    Số điện thoại <span>*</span>
                    <input
                      type="tel"
                      value={customerForm.phone}
                      onChange={(e) => setCustomerForm((prev) => ({ ...prev, phone: e.target.value }))}
                      placeholder="Nhập số điện thoại"
                    />
                  </label>
                </div>
              </div>

              <div className="step4-section vehicle-block">
                <h3>Thông tin xe</h3>
                <div className="booking-scope-switch" role="group" aria-label="Phạm vi đặt lịch">
                  <button
                    type="button"
                    className={!isBatchBooking ? "active" : ""}
                    onClick={() => {
                      setIsBatchBooking(false);
                      setError("");
                    }}
                  >
                    Một xe
                  </button>
                  <button
                    type="button"
                    className={isBatchBooking ? "active" : ""}
                    onClick={() => {
                      setIsBatchBooking(true);
                      setError("");
                    }}
                  >
                    Nhiều xe
                  </button>
                </div>
                {isBatchBooking ? (
                  <p className="vehicle-batch-note">
                    Thêm từng xe vào danh sách bên dưới. Tất cả xe sẽ dùng chung dịch vụ, chi nhánh và khung giờ đã chọn.
                  </p>
                ) : null}

                <div className="step4-mode-picker">
                  <label className="step4-mode-item">
                    <input
                      type="radio"
                      name="vehicleMode"
                      value="existing"
                      checked={vehicleMode === "existing"}
                      disabled={linkedVehicles.length === 0}
                      onChange={() => setVehicleMode("existing")}
                    />
                    Chọn xe có sẵn
                  </label>
                  <label className="step4-mode-item">
                    <input
                      type="radio"
                      name="vehicleMode"
                      value="new"
                      checked={vehicleMode === "new"}
                      onChange={() => setVehicleMode("new")}
                    />
                    Thêm xe mới
                  </label>
                </div>

                {linkedVehicles.length > 0 && vehicleMode === "existing" && (
                  <label>
                    Xe đã lưu
                    <select
                      value={selectedVehicleId}
                      onChange={(e) => setSelectedVehicleId(e.target.value)}
                    >
                      <option value="">-- Chọn xe --</option>
                      {linkedVehicles.map((vehicle) => (
                        <option key={vehicle.id} value={vehicle.id}>
                          {formatStoredVehicleLabel(vehicle)}
                        </option>
                      ))}
                    </select>
                  </label>
                )}

                {(linkedVehicles.length === 0 || vehicleMode === "new") && (
                  <div className="form-grid step4-form-grid">
                    <label>
                      Biển số xe <span>*</span>
                      <input
                        type="text"
                        value={newVehicle.licensePlate}
                        onChange={(e) => updateNewVehicle("licensePlate", formatLicensePlateInput(e.target.value))}
                        placeholder="Nhập biển số xe"
                      />
                    </label>

                    <label>
                      Hãng xe <span>*</span>
                      <select
                        value={newVehicle.brandId}
                        onChange={(e) => {
                          updateNewVehicle("brandId", e.target.value);
                          updateNewVehicle("modelId", "");
                        }}
                      >
                        <option value="">Chọn hãng xe</option>
                        {brands.map((brand) => (
                          <option key={extractField(brand, ["id", "Id"])} value={extractField(brand, ["id", "Id"])}>
                            {extractField(brand, ["name", "Name"], "")}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      Dòng xe <span>*</span>
                      <select
                        value={newVehicle.modelId}
                        onChange={(e) => updateNewVehicle("modelId", e.target.value)}
                        disabled={!newVehicle.brandId}
                      >
                        <option value="">Chọn dòng xe</option>
                        {modelOptions.map((model) => (
                          <option key={extractField(model, ["id", "Id"])} value={extractField(model, ["id", "Id"])}>
                            {extractField(model, ["modelName", "ModelName"], "")}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      Số VIN <span>*</span>
                      <input
                        type="text"
                        value={newVehicle.vin}
                        onChange={(e) => updateNewVehicle("vin", e.target.value.toUpperCase())}
                        placeholder="Nhập số VIN"
                      />
                    </label>
                  </div>
                )}

                {isBatchBooking ? (
                  <div className="vehicle-batch-actions">
                    <button
                      type="button"
                      className="vehicle-add-btn"
                      onClick={handleAddBatchVehicle}
                    >
                      Thêm xe vào danh sách
                    </button>
                    {batchVehicles.length > 0 ? (
                      <span className="vehicle-batch-count">Đã thêm {batchVehicles.length} xe</span>
                    ) : null}
                  </div>
                ) : null}

                {isBatchBooking && batchVehicles.length > 0 ? (
                  <div className="vehicle-batch-list">
                    {batchVehicles.map((vehicle, index) => (
                      <article key={vehicle.dedupeKey} className="vehicle-batch-item">
                        <strong>{vehicle.displayLabel}</strong>
                        <button
                          type="button"
                          onClick={() => handleRemoveBatchVehicle(index)}
                          aria-label="Xóa xe"
                        >
                          Xóa
                        </button>
                      </article>
                    ))}
                  </div>
                ) : null}
              </div>

              <div className="schedule-block">
                <h3>Thời gian đặt lịch</h3>
                <div className="form-grid">
                  <label>
                    Ngày
                    <DateTimePicker
                      value={selectedDate ? `${selectedDate}T08:00:00` : ""}
                      minDate={new Date()}
                      showTime={false}
                      placeholder="Chọn ngày hẹn"
                      onChange={(val) => {
                        const pickedDate = String(val || "").slice(0, 10);
                        setSelectedDate(pickedDate);
                        setSelectedSlot(null);
                      }}
                    />
                  </label>
                </div>

                <div className="slot-section">
                  <h4>{bookingFlow === BOOKING_FLOW.FIXED ? "Khung giờ khả dụng" : "Khung giờ mang xe đến (Check-in Window)"}</h4>
                  {!selectedBranchId ? (
                    <div className="booking-empty">Vui lòng chọn chi nhánh trước khi chọn giờ.</div>
                  ) : slotsLoading ? (
                    <div className="booking-empty">Đang tải khung giờ...</div>
                  ) : availableSlots.length === 0 ? (
                    <div className="booking-empty">Không có khung giờ khả dụng cho ngày đã chọn.</div>
                  ) : (
                    <>
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

                      {(slotsBySession[activeSession] || []).length > 0 && (
                        <div className="slot-actions">
                          <button
                            type="button"
                            className="slot-expand-toggle"
                            onClick={() => toggleOddSlotsForSession(activeSession)}
                          >
                            {showOddSlots[activeSession] ? "Ẩn giờ lẻ" : "Xem thêm giờ lẻ"}
                          </button>
                        </div>
                      )}

                      {(slotsBySession[activeSession] || []).length === 0 ? (
                        <div className="booking-empty">Buổi này không còn slot trống.</div>
                      ) : (
                        <div className="slot-grid">
                          {displayedSlots.map((slot) => {
                            const startTime = extractField(slot, ["startTime", "StartTime"]);
                            const endTime = extractField(slot, ["endTime", "EndTime"]);
                            const isAvailable = extractField(slot, ["isAvailable", "IsAvailable"], false);
                            const remainingCapacity = Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0));
                            const isSelected =
                              selectedSlot &&
                              extractField(selectedSlot, ["startTime", "StartTime"]) === startTime &&
                              extractField(selectedSlot, ["endTime", "EndTime"]) === endTime;
                            const isTopPick = extractField(slot, ["isTopPick", "IsTopPick"], false);
                            const isRoundedHour = extractField(slot, ["isRoundedHour", "IsRoundedHour"], false);
                            const slotBadge = isTopPick ? "Đề xuất" : isRoundedHour ? "Giờ tròn" : "Giờ lẻ";

                            return (
                              <button
                                key={`${startTime}-${endTime}`}
                                type="button"
                                className={`slot-card ${isSelected ? "selected" : ""} ${isTopPick ? "top-pick" : ""}`}
                                disabled={!isAvailable}
                                onClick={() => setSelectedSlot(slot)}
                              >
                                <div className="slot-card-head">
                                  <strong className="slot-time">{timeLabel(startTime)} - {timeLabel(endTime)}</strong>
                                  <span className={`slot-pill ${isTopPick ? "top" : isRoundedHour ? "round" : "odd"}`}>
                                    {slotBadge}
                                  </span>
                                </div>
                                <div className="slot-meta">
                                  <span className={`slot-state-dot ${isAvailable ? "ok" : "full"}`} />
                                  <span className={`slot-state-text ${isAvailable ? "ok" : "full"}`}>
                                    {bookingFlow === BOOKING_FLOW.FIXED
                                      ? (isAvailable ? `Còn ${remainingCapacity} chỗ` : "Hết chỗ")
                                      : "Khung nhận xe"}
                                  </span>
                                </div>
                              </button>
                            );
                          })}
                        </div>
                      )}
                    </>
                  )}
                </div>
              </div>

              <label className="notes-field">
                Mô tả triệu chứng
                <div style={{ display: "grid", gap: "10px" }}>
                  {symptomInputs.map((symptom, index) => (
                    <textarea
                      key={`booking-symptom-${index}`}
                      rows={1}
                      value={symptom}
                      placeholder={`Nhập mô tả triệu chứng ${index + 1}...`}
                      onChange={(e) => handleSymptomChange(index, e.target.value)}
                      onFocus={() => handleSymptomFocus(index)}
                      onBlur={() => setFocusedSymptomIndex(-1)}
                      disabled={index > 0 && !String(symptomInputs[index - 1] || "").trim()}
                      style={{
                        minHeight: "42px",
                        maxHeight: "72px",
                        resize: "vertical",
                        opacity: String(symptom || "").trim() || focusedSymptomIndex === index ? 1 : 0.5,
                        transition: "opacity 0.2s ease",
                        cursor: index > 0 && !String(symptomInputs[index - 1] || "").trim() ? "not-allowed" : "text",
                      }}
                    />
                  ))}
                </div>
              </label>
            </div>
          )}

          {currentStep === 5 && (
            success ? (
              <div className="summary-card summary-card-large booking-success-screen">
                <div className="booking-success-icon" aria-hidden="true">✓</div>
                <h3>Đặt lịch thành công</h3>
                <p>{success}</p>
                <div className="summary-total">
                  <div>
                    <span>Tổng dịch vụ</span>
                    <strong>{formatVnd(totalPrice)}</strong>
                  </div>
                  <div>
                    <span>Thời lượng dự kiến</span>
                    <strong>{totalMinutes} phút</strong>
                  </div>
                </div>
                <div className="success-actions">
                  <button type="button" onClick={() => navigate("/my-appointments")}>Xem lịch hẹn của tôi</button>
                  <button type="button" onClick={() => navigate("/home")}>Về trang chủ</button>
                </div>
              </div>
            ) : (
              <div className="summary-card summary-card-large">
                <h3>Xác nhận đặt lịch</h3>
                <p className="summary-intro">
                  Vui lòng kiểm tra lại toàn bộ thông tin trước khi gửi yêu cầu đến gara.
                </p>
                {summaryItems.length === 0 ? (
                  <p className="summary-empty">Hoàn thành từng bước để thông tin xuất hiện tại đây.</p>
                ) : (
                  <div className="summary-review-grid">
                    {summaryItems.map((item) => (
                      <article key={`step5-${item.key}`} className="summary-review-item">
                        <h4>{item.label}</h4>
                        <div className="summary-review-value">
                          {Array.isArray(item.values) ? (
                            <>
                              <ul className="summary-bullet-list">
                                {item.values.map((entry, index) => (
                                  <li key={`step5-${item.key}-${index}`}>{entry}</li>
                                ))}
                              </ul>
                              {item.autoNote ? <span className="summary-auto-note">(tự động)</span> : null}
                            </>
                          ) : (
                            item.value
                          )}
                        </div>
                      </article>
                    ))}
                  </div>
                )}

                <div className="summary-total">
                  <div>
                    <span>Tổng dịch vụ</span>
                    <strong>{formatVnd(totalPrice)}</strong>
                  </div>
                  <div>
                    <span>Thời lượng dự kiến</span>
                    <strong>{totalMinutes} phút</strong>
                  </div>
                </div>

                <button type="button" className="primary-submit" onClick={handleConfirmBooking} disabled={submitting}>
                  {submitting ? "Đang gửi lịch hẹn..." : "Xác nhận đặt lịch"}
                </button>
              </div>
            )
          )}

          {!(currentStep === 5 && success) && (
            <div className="wizard-actions">
              <button type="button" className="ghost-btn" onClick={goPrevious} disabled={currentStep === 1 || submitting}>
                Quay lại
              </button>
              {currentStep < BOOKING_STEPS.length && (
                <button type="button" className="next-btn" onClick={goNext} disabled={submitting}>
                  Tiếp tục
                </button>
              )}
            </div>
          )}
        </div>

        {currentStep !== 5 ? (
        <aside className="booking-summary-panel">
          <div className="summary-card">
            <h3>Tóm tắt lịch hẹn</h3>
            {summaryItems.length === 0 ? (
              <p className="summary-empty">Hoàn thành từng bước để thông tin xuất hiện tại đây.</p>
            ) : (
              <dl>
                {summaryItems.map((item) => (
                  <Fragment key={item.key}>
                    <dt>{item.label}</dt>
                    <dd>
                      {Array.isArray(item.values) ? (
                        <>
                          <ul className="summary-bullet-list">
                            {item.values.map((entry, index) => (
                              <li key={`${item.key}-${index}`}>{entry}</li>
                            ))}
                          </ul>
                          {item.autoNote ? <span className="summary-auto-note">(tự động)</span> : null}
                        </>
                      ) : (
                        item.value
                      )}
                    </dd>
                  </Fragment>
                ))}
              </dl>
            )}

            <div className="summary-total">
              <div>
                <span>Tổng dịch vụ</span>
                <strong>{formatVnd(totalPrice)}</strong>
              </div>
              <div>
                <span>Thời lượng dự kiến</span>
                <strong>{totalMinutes} phút</strong>
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


