import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
import Header from "../../../shared/components/Header";
import DateTimePicker from "../../workshop/components/DateTimePicker";
import {
  bookAppointment,
  createCustomerVehicle,
  createVehicle,
  getAllBranches,
  getAllCustomerVehicles,
  getAllCustomers,
  getAllServiceCategories,
  getAllStandardServices,
  getAllVehicles,
  getAvailableSlots,
  getVehicleBrands,
  getVehicleModels,
  updateCustomer,
} from "../api/appointmentApi";
import "./BookingPage.css";

const STEPS = [
  "Loại dịch vụ",
  "Chọn dịch vụ",
  "Chi nhánh",
  "Thông tin đặt hẹn",
  "Hoàn thành",
];

function formatVnd(value) {
  if (!Number.isFinite(Number(value))) return "0đ";
  return `${new Intl.NumberFormat("vi-VN").format(Math.round(Number(value)))}đ`;
}

function normalizeText(value) {
  return String(value || "").toLowerCase().trim();
}

function formatDateVi(dateString) {
  if (!dateString) return "Chưa chọn";
  const parsed = new Date(dateString);
  if (Number.isNaN(parsed.getTime())) return "Chưa chọn";
  return parsed.toLocaleDateString("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function extractField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

function toIsoDateInput(dateValue) {
  const d = dateValue ? new Date(dateValue) : new Date();
  if (Number.isNaN(d.getTime())) return "";
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function toSafeUpper(value) {
  return String(value || "").trim().toUpperCase();
}

function timeLabel(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (!Number.isNaN(parsed.getTime())) {
    return parsed.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  }
  return String(value).slice(0, 5);
}

function formatLicensePlateInput(value) {
  const raw = String(value || "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");

  if (!raw) return "";

  let prefix = "";
  let rest = "";

  const startsWithProvince = /^\d{2}[A-Z]/.test(raw);
  if (startsWithProvince) {
    prefix = raw.slice(0, 3);
    rest = raw.slice(3, 8);
  } else {
    prefix = raw.slice(0, Math.min(3, raw.length));
    rest = raw.slice(prefix.length, prefix.length + 5);
  }

  if (!rest) return prefix;
  if (rest.length <= 3) return `${prefix}-${rest}`;
  return `${prefix}-${rest.slice(0, 3)}.${rest.slice(3)}`;
}

function iconByIndex(index) {
  const icons = ["🔧", "🛞", "⚙️", "🛡️", "🧰", "🔩"];
  return icons[index % icons.length];
}

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

  const [selectedCategoryId, setSelectedCategoryId] = useState("");
  const [selectedServiceIds, setSelectedServiceIds] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState("");

  const [customerForm, setCustomerForm] = useState({ fullName: "", phone: "" });
  const [notes, setNotes] = useState("");

  const [vehicleMode, setVehicleMode] = useState("new");
  const [selectedVehicleId, setSelectedVehicleId] = useState("");
  const [newVehicle, setNewVehicle] = useState({
    brandId: "",
    modelId: "",
    licensePlate: "",
    vin: "",
    color: "",
  });

  const [selectedDate, setSelectedDate] = useState(toIsoDateInput(new Date()));
  const [slots, setSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [selectedSlot, setSelectedSlot] = useState(null);

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
        return {
          id: Number(extractField(vehicle, ["id", "Id"])),
          modelId: Number(extractField(vehicle, ["modelId", "ModelId"])),
          licensePlate: extractField(vehicle, ["licensePlate", "LicensePlate"], ""),
          vin: extractField(vehicle, ["vin", "Vin"], ""),
          color: extractField(vehicle, ["color", "Color"], ""),
          brandName: extractField(vehicle, ["brandName", "BrandName"], ""),
          modelName: extractField(vehicle, ["modelName", "ModelName"], ""),
        };
      })
      .filter(Boolean);
  }, [customerRecord, customerVehicles, user?.id, vehicles]);

  const selectedCategory = useMemo(
    () => categories.find((item) => String(item.id) === String(selectedCategoryId) || String(item.Id) === String(selectedCategoryId)),
    [categories, selectedCategoryId]
  );

  const servicesByCategory = useMemo(() => {
    if (!selectedCategoryId) return [];
    return (services || []).filter(
      (item) =>
        String(extractField(item, ["categoryId", "CategoryId"])) === String(selectedCategoryId) &&
        extractField(item, ["isActive", "IsActive"], true) !== false
    );
  }, [selectedCategoryId, services]);

  const selectedServices = useMemo(
    () => services.filter((item) => selectedServiceIds.includes(Number(extractField(item, ["id", "Id"])))),
    [selectedServiceIds, services]
  );

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

  const totalPrice = useMemo(
    () => selectedServices.reduce((sum, item) => sum + Number(extractField(item, ["basePrice", "BasePrice"], 0) || 0), 0),
    [selectedServices]
  );

  const totalMinutes = useMemo(
    () => selectedServices.reduce((sum, item) => sum + Number(extractField(item, ["standardMinutes", "StandardMinutes"], 0) || 0), 0),
    [selectedServices]
  );

  const summaryItems = useMemo(() => {
    const items = [];
    const customerName = customerForm.fullName.trim();
    const customerPhone = customerForm.phone.trim();
    const selectedServiceNames = selectedServices
      .map((item) => extractField(item, ["name", "Name"], "").trim())
      .filter(Boolean);
    const dateLabel = selectedDate ? formatDateVi(selectedDate) : "";
    const timeRange = selectedSlot
      ? `${timeLabel(extractField(selectedSlot, ["startTime", "StartTime"]))} - ${timeLabel(extractField(selectedSlot, ["endTime", "EndTime"]))}`
      : "";

    const existingVehicleLabel = selectedExistingVehicle
      ? `${selectedExistingVehicle.licensePlate || "Không biển số"} (${selectedExistingVehicle.brandName} ${selectedExistingVehicle.modelName})`
      : "";
    const newVehiclePlate = toSafeUpper(newVehicle.licensePlate);
    const newVehicleModel = selectedModel?.modelName || "";
    const newVehicleLabel = newVehiclePlate
      ? `${newVehiclePlate}${newVehicleModel ? ` (${newVehicleModel})` : ""}`
      : "";

    const step1Done = Boolean(selectedCategoryId);
    const step2Done = step1Done && selectedServiceIds.length > 0;
    const step3Done = step2Done && Boolean(selectedBranchId);

    const hasVehicle = linkedVehicles.length > 0;
    const usingNewVehicle = !hasVehicle || vehicleMode === "new";
    const vehicleReady = usingNewVehicle
      ? Boolean(newVehicle.brandId && newVehicle.modelId && newVehicle.licensePlate.trim() && newVehicle.vin.trim())
      : Boolean(selectedVehicleId);
    const step4Done = step3Done && Boolean(customerName && customerPhone && selectedDate && selectedSlot && vehicleReady);

    if (step1Done && selectedCategory) {
      items.push({ key: "category", label: "Loại dịch vụ", value: extractField(selectedCategory, ["name", "Name"], "") });
    }
    if (step2Done && selectedServiceNames.length > 0) {
      items.push({
        key: "services",
        label: "Dịch vụ cụ thể",
        value: selectedServiceNames.length <= 2 ? selectedServiceNames.join(", ") : `${selectedServiceNames.length} mục đã chọn`,
      });
    }
    if (step3Done && selectedBranch) {
      items.push({ key: "branch", label: "Chi nhánh", value: extractField(selectedBranch, ["name", "Name"], "") });
    }
    if (step4Done && dateLabel) {
      items.push({ key: "datetime", label: "Ngày giờ", value: timeRange ? `${dateLabel} - ${timeRange}` : dateLabel });
    }
    if (step4Done && customerName) {
      items.push({ key: "customer", label: "Khách hàng", value: customerName });
    }
    if (step4Done && customerPhone) {
      items.push({ key: "phone", label: "SĐT", value: customerPhone });
    }
    if (step4Done && (existingVehicleLabel || newVehicleLabel)) {
      items.push({ key: "vehicle", label: "Xe", value: existingVehicleLabel || newVehicleLabel });
    }

    return items;
  }, [
    customerForm.fullName,
    customerForm.phone,
    linkedVehicles.length,
    newVehicle.brandId,
    newVehicle.licensePlate,
    newVehicle.modelId,
    newVehicle.vin,
    selectedBranch,
    selectedBranchId,
    selectedCategory,
    selectedCategoryId,
    selectedDate,
    selectedExistingVehicle,
    selectedModel?.modelName,
    selectedServiceIds.length,
    selectedVehicleId,
    selectedServices,
    selectedSlot,
    vehicleMode,
  ]);

  const progressPercent = useMemo(() => (currentStep / STEPS.length) * 100, [currentStep]);

  const loadSlots = useCallback(async (branchId, date) => {
    if (!branchId || !date) {
      setSlots([]);
      setSelectedSlot(null);
      return;
    }

    setSlotsLoading(true);
    setSelectedSlot(null);
    setSlots([]);

    try {
      const data = await getAvailableSlots(Number(branchId), date);
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
        const [
          categoryData,
          serviceData,
          branchData,
          brandData,
          modelData,
          customerData,
          customerVehicleData,
          vehicleData,
        ] = await Promise.all([
          getAllServiceCategories(),
          getAllStandardServices(),
          getAllBranches(),
          getVehicleBrands(),
          getVehicleModels(),
          getAllCustomers(),
          getAllCustomerVehicles(),
          getAllVehicles(),
        ]);

        if (ignore) return;

        setCategories(Array.isArray(categoryData) ? categoryData.filter((item) => item?.isDeleted !== true) : []);
        setServices(Array.isArray(serviceData) ? serviceData.filter((item) => item?.isDeleted !== true) : []);
        setBranches(Array.isArray(branchData) ? branchData.filter((item) => normalizeText(item?.status) === "active") : []);
        setBrands(Array.isArray(brandData) ? brandData.filter((item) => item?.isDeleted !== true) : []);
        setModels(Array.isArray(modelData) ? modelData.filter((item) => item?.isDeleted !== true) : []);
        setCustomers(Array.isArray(customerData) ? customerData : []);
        setCustomerVehicles(Array.isArray(customerVehicleData) ? customerVehicleData : []);
        setVehicles(Array.isArray(vehicleData) ? vehicleData : []);
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
      loadSlots(selectedBranchId, selectedDate);
    }
  }, [selectedBranchId, selectedDate, loadSlots]);

  function updateNewVehicle(key, value) {
    setNewVehicle((prev) => ({ ...prev, [key]: value }));
  }

  function toggleService(serviceId) {
    setSelectedServiceIds((prev) =>
      prev.includes(serviceId) ? prev.filter((id) => id !== serviceId) : [...prev, serviceId]
    );
  }

  function validateStep(stepNumber) {
    if (stepNumber === 1 && !selectedCategoryId) {
      return "Vui lòng chọn loại dịch vụ.";
    }

    if (stepNumber === 2 && selectedServiceIds.length === 0) {
      return "Vui lòng chọn ít nhất một dịch vụ cụ thể.";
    }

    if (stepNumber === 3 && !selectedBranchId) {
      return "Vui lòng chọn chi nhánh tiếp nhận.";
    }

    if (stepNumber === 4) {
      if (!customerForm.fullName.trim()) return "Vui lòng nhập họ tên khách hàng.";
      if (!customerForm.phone.trim()) return "Vui lòng nhập số điện thoại khách hàng.";
      if (!selectedDate) return "Vui lòng chọn ngày đặt lịch.";
      if (!selectedSlot) return "Vui lòng chọn khung giờ khả dụng.";

      const hasVehicle = linkedVehicles.length > 0;
      const usingNewVehicle = !hasVehicle || vehicleMode === "new";

      if (!usingNewVehicle && !selectedVehicleId) {
        return "Vui lòng chọn xe hiện có hoặc chuyển sang thêm xe mới.";
      }

      if (usingNewVehicle) {
        if (!newVehicle.brandId) return "Vui lòng chọn hãng xe.";
        if (!newVehicle.modelId) return "Vui lòng chọn dòng xe.";
        if (!newVehicle.licensePlate.trim()) return "Vui lòng nhập biển số xe.";
        if (!newVehicle.vin.trim()) return "Vui lòng nhập số VIN.";
      }
    }

    return "";
  }

  function goNext() {
    const issue = validateStep(currentStep);
    if (issue) {
      setError(issue);
      return;
    }

    setError("");
    setCurrentStep((prev) => Math.min(prev + 1, STEPS.length));
  }

  function goPrevious() {
    setError("");
    setCurrentStep((prev) => Math.max(prev - 1, 1));
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
      const today = new Date();
      const selectedStart = new Date(extractField(selectedSlot, ["startTime", "StartTime"]));
      if (!Number.isNaN(selectedStart.getTime())) {
        const isToday = selectedStart.toDateString() === today.toDateString();
        if (isToday && selectedStart.getTime() < today.getTime() + 30 * 60 * 1000) {
          throw new Error("Khung giờ này không còn khả dụng (cần cách hiện tại ít nhất 30 phút).");
        }
      }

      const customerId = Number(extractField(customerRecord, ["id", "Id"], user?.id));

      if (Number.isFinite(customerId)) {
        await updateCustomer(customerId, {
          fullName: customerForm.fullName.trim(),
          phone: customerForm.phone.trim(),
          email: extractField(customerRecord, ["email", "Email"], user?.email || null),
          tierId: extractField(customerRecord, ["tierId", "TierId"], null),
          note: extractField(customerRecord, ["note", "Note"], null),
          isActive: extractField(customerRecord, ["isActive", "IsActive"], true),
        });
      }

      let vehicleId = null;
      let vehicleModelId = null;
      let licensePlate = null;
      let vin = null;
      let color = null;

      const hasVehicle = linkedVehicles.length > 0;
      const usingNewVehicle = !hasVehicle || vehicleMode === "new";

      if (!usingNewVehicle) {
        vehicleId = Number(selectedExistingVehicle?.id);
        vehicleModelId = Number(selectedExistingVehicle?.modelId) || null;
        licensePlate = selectedExistingVehicle?.licensePlate || null;
        vin = selectedExistingVehicle?.vin || null;
        color = selectedExistingVehicle?.color || null;
      } else {
        const createdVehicle = await createVehicle({
          modelId: Number(newVehicle.modelId),
          licensePlate: toSafeUpper(newVehicle.licensePlate),
          vin: toSafeUpper(newVehicle.vin),
          color: newVehicle.color.trim() || null,
        });

        vehicleId = Number(extractField(createdVehicle, ["id", "Id"]));
        if (!Number.isFinite(vehicleId)) {
          throw new Error("Không thể tạo xe mới. Vui lòng thử lại.");
        }

        if (Number.isFinite(customerId)) {
          await createCustomerVehicle({
            customerId,
            vehicleId,
            isCurrentOwner: true,
          });
        }

        vehicleModelId = Number(newVehicle.modelId);
        licensePlate = toSafeUpper(newVehicle.licensePlate);
        vin = toSafeUpper(newVehicle.vin);
        color = newVehicle.color.trim() || null;
      }

      await bookAppointment({
        branchId: Number(selectedBranchId),
        vehicleId,
        vehicleModelId,
        licensePlate,
        vin,
        color,
        startTime: extractField(selectedSlot, ["startTime", "StartTime"]),
        endTime: extractField(selectedSlot, ["endTime", "EndTime"]),
        requestedServiceIds: selectedServiceIds.join(","),
        customerNotes: notes.trim() || null,
      });

      setSuccess("Đặt lịch thành công. Chúng tôi sẽ xác nhận lịch hẹn của bạn trong thời gian sớm nhất.");
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
          <Header />
        </div>
        <div className="booking-loading">Đang tải dữ liệu đặt lịch...</div>
      </div>
    );
  }

  return (
    <div className="booking-page-shell">
      <div className="booking-fixed-header">
        <Header />
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
          {STEPS.map((label, idx) => {
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
      {success && <div className="booking-alert booking-alert-success">{success}</div>}

        <section className="booking-layout">
        <div className="booking-main-panel">
          {currentStep === 1 && (
            <div className="booking-card">
              <header className="booking-card-head">
                <h2>Chọn loại dịch vụ</h2>
                <p>Bắt đầu bằng cách chọn nhóm dịch vụ phù hợp với nhu cầu xe của bạn.</p>
              </header>
              <div className="category-grid">
                {categories.map((category) => {
                  const categoryId = extractField(category, ["id", "Id"]);
                  const name = extractField(category, ["name", "Name"], "");
                  const desc = extractField(category, ["description", "Description"], "");
                  if (!name) return null;
                  const selected = String(categoryId) === String(selectedCategoryId);
                  return (
                    <button
                      key={categoryId}
                      type="button"
                      className={`category-card ${selected ? "selected" : ""}`}
                      onClick={() => {
                        setSelectedCategoryId(String(categoryId));
                        setSelectedServiceIds([]);
                        setError("");
                      }}
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
                  Nhóm đang chọn: <strong>{extractField(selectedCategory, ["name", "Name"], "")}</strong>
                </p>
              </header>

              <div className="service-list">
                {servicesByCategory.length === 0 && (
                  <div className="booking-empty">Chưa có dịch vụ cụ thể cho loại này.</div>
                )}

                {servicesByCategory.map((service) => {
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
                          {vehicle.licensePlate || "Không biển số"} - {vehicle.brandName} {vehicle.modelName}
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
              </div>

              <div className="schedule-block">
                <h3>Thời gian đặt lịch</h3>
                <div className="form-grid">
                  <label>
                    Ngày
                    <DateTimePicker
                      value={selectedDate ? `${selectedDate}T08:00:00` : ""}
                      minDate={new Date()}
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
                  <h4>Khung giờ khả dụng</h4>
                  {!selectedBranchId ? (
                    <div className="booking-empty">Vui lòng chọn chi nhánh trước khi chọn giờ.</div>
                  ) : slotsLoading ? (
                    <div className="booking-empty">Đang tải khung giờ...</div>
                  ) : slots.length === 0 ? (
                    <div className="booking-empty">Không có khung giờ khả dụng cho ngày đã chọn.</div>
                  ) : (
                    <div className="slot-grid">
                      {slots.map((slot) => {
                        const startTime = extractField(slot, ["startTime", "StartTime"]);
                        const endTime = extractField(slot, ["endTime", "EndTime"]);
                        const isAvailable = extractField(slot, ["isAvailable", "IsAvailable"], false);
                        const isSelected =
                          selectedSlot &&
                          extractField(selectedSlot, ["startTime", "StartTime"]) === startTime &&
                          extractField(selectedSlot, ["endTime", "EndTime"]) === endTime;

                        return (
                          <button
                            key={`${startTime}-${endTime}`}
                            type="button"
                            className={`slot-card ${isSelected ? "selected" : ""}`}
                            disabled={!isAvailable}
                            onClick={() => setSelectedSlot(slot)}
                          >
                            <strong>{timeLabel(startTime)} - {timeLabel(endTime)}</strong>
                            <small>
                              {isAvailable
                                ? `Còn ${extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)} chỗ`
                                : "Hết chỗ"}
                            </small>
                          </button>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>

              <label className="notes-field">
                Ghi chú thêm
                <textarea
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="Mô tả tình trạng xe hoặc yêu cầu đặc biệt..."
                  rows={4}
                />
              </label>
            </div>
          )}

          {currentStep === 5 && (
            <div className="booking-card">
              <header className="booking-card-head">
                <h2>Bước 5: Xác nhận đặt lịch</h2>
                <p>Kiểm tra lại toàn bộ thông tin trước khi gửi yêu cầu đến gara.</p>
              </header>

              <div className="confirm-grid">
                <div><span>Loại dịch vụ</span><strong>{extractField(selectedCategory, ["name", "Name"], "Chưa chọn")}</strong></div>
                <div><span>Số dịch vụ</span><strong>{selectedServiceIds.length} dịch vụ</strong></div>
                <div><span>Chi nhánh</span><strong>{extractField(selectedBranch, ["name", "Name"], "Chưa chọn")}</strong></div>
                <div><span>Khách hàng</span><strong>{customerForm.fullName || "Chưa nhập"} - {customerForm.phone || "Chưa nhập"}</strong></div>
                <div>
                  <span>Xe</span>
                  <strong>
                    {selectedExistingVehicle
                      ? `${selectedExistingVehicle.licensePlate || "Không biển số"} - ${selectedExistingVehicle.brandName} ${selectedExistingVehicle.modelName}`
                      : `${toSafeUpper(newVehicle.licensePlate) || "Chưa nhập"} - ${selectedModel?.modelName || "Xe mới"}`}
                  </strong>
                </div>
                <div>
                  <span>Thời gian</span>
                  <strong>
                    {formatDateVi(selectedDate)}
                    {selectedSlot
                      ? `, ${timeLabel(extractField(selectedSlot, ["startTime", "StartTime"]))} - ${timeLabel(extractField(selectedSlot, ["endTime", "EndTime"]))}`
                      : ""}
                  </strong>
                </div>
              </div>

              <button type="button" className="primary-submit" onClick={handleConfirmBooking} disabled={submitting}>
                {submitting ? "Đang gửi lịch hẹn..." : "Xác nhận đặt lịch"}
              </button>

              {!!success && (
                <div className="success-actions">
                  <button type="button" onClick={() => navigate("/my-appointments")}>Xem lịch hẹn của tôi</button>
                  <button type="button" onClick={() => navigate("/home")}>Về trang chủ</button>
                </div>
              )}
            </div>
          )}

          <div className="wizard-actions">
            <button type="button" className="ghost-btn" onClick={goPrevious} disabled={currentStep === 1 || submitting}>
              Quay lại
            </button>
            {currentStep < STEPS.length && (
              <button type="button" className="next-btn" onClick={goNext} disabled={submitting}>
                Tiếp tục
              </button>
            )}
          </div>
        </div>

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
                    <dd>{item.value}</dd>
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
        </section>
      </div>
    </div>
  );
}


