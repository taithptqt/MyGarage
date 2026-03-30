import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
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
import Header from "../../../shared/components/Header";
import "./BookingPage.css";

const STEPS = [
  "Loại dịch vụ",
  "Dịch vụ cụ thể",
  "Chi nhánh",
  "Thông tin & thời gian",
  "Xác nhận",
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
      <>
        <Header />
        <div className="booking-loading">Đang tải dữ liệu đặt lịch...</div>
      </>
    );
  }

  return (
    <>
      <Header />
      <div className="booking-flow">
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
                <h2>Bước 1: Chọn loại dịch vụ</h2>
                <p>Phân loại nhu cầu để hệ thống gợi ý hạng mục phù hợp.</p>
              </header>
              <div className="category-grid">
                {categories.map((category) => {
                  const categoryId = extractField(category, ["id", "Id"]);
                  const name = extractField(category, ["name", "Name"], "Loại dịch vụ");
                  const desc = extractField(category, ["description", "Description"], "");
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
                      <h3>{name}</h3>
                      <p>{desc || "Nhóm dịch vụ chuyên biệt cho tình trạng xe của bạn."}</p>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {currentStep === 2 && (
            <div className="booking-card">
              <header className="booking-card-head">
                <h2>Bước 2: Chọn dịch vụ cụ thể</h2>
                <p>
                  Danh sách theo loại <strong>{extractField(selectedCategory, ["name", "Name"], "đã chọn")}</strong>.
                </p>
              </header>

              <div className="service-list">
                {servicesByCategory.length === 0 && (
                  <div className="booking-empty">Chưa có dịch vụ cụ thể cho loại này.</div>
                )}

                {servicesByCategory.map((service) => {
                  const serviceId = Number(extractField(service, ["id", "Id"]));
                  const selected = selectedServiceIds.includes(serviceId);
                  return (
                    <button
                      key={serviceId}
                      type="button"
                      className={`service-card ${selected ? "selected" : ""}`}
                      onClick={() => toggleService(serviceId)}
                    >
                      <div>
                        <h3>{extractField(service, ["name", "Name"], "Dịch vụ")}</h3>
                        <p>{extractField(service, ["description", "Description"], "Dịch vụ tiêu chuẩn tại MyGarage")}</p>
                      </div>
                      <aside>
                        <strong>{formatVnd(extractField(service, ["basePrice", "BasePrice"], 0))}</strong>
                        <small>{extractField(service, ["standardMinutes", "StandardMinutes"], 0)} phút</small>
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
              <header className="booking-card-head">
                <h2>Bước 4: Thông tin khách hàng, xe và thời gian</h2>
                <p>Cập nhật dữ liệu chuẩn trước khi xác nhận lịch.</p>
              </header>

              <div className="form-grid">
                <label>
                  Họ và tên
                  <input
                    type="text"
                    value={customerForm.fullName}
                    onChange={(e) => setCustomerForm((prev) => ({ ...prev, fullName: e.target.value }))}
                    placeholder="Nhập họ tên khách hàng"
                  />
                </label>

                <label>
                  Số điện thoại
                  <input
                    type="tel"
                    value={customerForm.phone}
                    onChange={(e) => setCustomerForm((prev) => ({ ...prev, phone: e.target.value }))}
                    placeholder="Nhập số điện thoại"
                  />
                </label>
              </div>

              <div className="vehicle-block">
                <div className="vehicle-block-head">
                  <h3>Thông tin xe</h3>
                  <p>
                    {linkedVehicles.length > 0
                      ? "Bạn có thể chọn xe đã lưu hoặc thêm xe mới."
                      : "Bạn chưa có xe lưu trong hệ thống, vui lòng nhập xe mới đầy đủ."}
                  </p>
                </div>

                {linkedVehicles.length > 0 && (
                  <div className="vehicle-mode-switch">
                    <button
                      type="button"
                      className={vehicleMode === "existing" ? "active" : ""}
                      onClick={() => setVehicleMode("existing")}
                    >
                      Chọn xe có sẵn
                    </button>
                    <button
                      type="button"
                      className={vehicleMode === "new" ? "active" : ""}
                      onClick={() => setVehicleMode("new")}
                    >
                      Thêm xe mới
                    </button>
                  </div>
                )}

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
                  <div className="form-grid">
                    <label>
                      Hãng xe
                      <select
                        value={newVehicle.brandId}
                        onChange={(e) => {
                          updateNewVehicle("brandId", e.target.value);
                          updateNewVehicle("modelId", "");
                        }}
                      >
                        <option value="">-- Chọn hãng xe --</option>
                        {brands.map((brand) => (
                          <option key={extractField(brand, ["id", "Id"])} value={extractField(brand, ["id", "Id"])}>
                            {extractField(brand, ["name", "Name"], "")}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      Dòng xe
                      <select
                        value={newVehicle.modelId}
                        onChange={(e) => updateNewVehicle("modelId", e.target.value)}
                        disabled={!newVehicle.brandId}
                      >
                        <option value="">-- Chọn dòng xe --</option>
                        {modelOptions.map((model) => (
                          <option key={extractField(model, ["id", "Id"])} value={extractField(model, ["id", "Id"])}>
                            {extractField(model, ["modelName", "ModelName"], "")}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      Biển số
                      <input
                        type="text"
                        value={newVehicle.licensePlate}
                        onChange={(e) => updateNewVehicle("licensePlate", e.target.value.toUpperCase())}
                        placeholder="VD: 51H-123.45"
                      />
                    </label>

                    <label>
                      Số VIN
                      <input
                        type="text"
                        value={newVehicle.vin}
                        onChange={(e) => updateNewVehicle("vin", e.target.value.toUpperCase())}
                        placeholder="Nhập số VIN"
                      />
                    </label>

                    <label>
                      Màu xe (tuỳ chọn)
                      <input
                        type="text"
                        value={newVehicle.color}
                        onChange={(e) => updateNewVehicle("color", e.target.value)}
                        placeholder="VD: Trắng ngọc trai"
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
                    <input
                      type="date"
                      value={selectedDate}
                      min={toIsoDateInput(new Date())}
                      onChange={(e) => {
                        setSelectedDate(e.target.value);
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
            <dl>
              <dt>Loại dịch vụ</dt>
              <dd>{extractField(selectedCategory, ["name", "Name"], "Chưa chọn")}</dd>

              <dt>Dịch vụ cụ thể</dt>
              <dd>{selectedServices.length > 0 ? `${selectedServices.length} mục đã chọn` : "Chưa chọn"}</dd>

              <dt>Chi nhánh</dt>
              <dd>{extractField(selectedBranch, ["name", "Name"], "Chưa chọn")}</dd>

              <dt>Ngày giờ</dt>
              <dd>
                {formatDateVi(selectedDate)}
                {selectedSlot
                  ? ` - ${timeLabel(extractField(selectedSlot, ["startTime", "StartTime"]))} đến ${timeLabel(extractField(selectedSlot, ["endTime", "EndTime"]))}`
                  : ""}
              </dd>

              <dt>Khách hàng</dt>
              <dd>{customerForm.fullName || "Chưa nhập"}</dd>

              <dt>SĐT</dt>
              <dd>{customerForm.phone || "Chưa nhập"}</dd>

              <dt>Xe</dt>
              <dd>
                {selectedExistingVehicle
                  ? `${selectedExistingVehicle.licensePlate || "Không biển số"} (${selectedExistingVehicle.brandName} ${selectedExistingVehicle.modelName})`
                  : toSafeUpper(newVehicle.licensePlate) || "Chưa chọn"}
              </dd>
            </dl>

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
    </>
  );
}
