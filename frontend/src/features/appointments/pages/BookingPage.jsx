<<<<<<< ours
<<<<<<< ours
import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import { useAuth } from "../../../context/AuthContext";
import SlotPicker from "../components/SlotPicker";
import {
  getAllBranches,
  getAllStandardServices,
  getVehicleBrands,
  getVehicleModels,
  getAvailableSlots,
  bookAppointment,
} from "../api/appointmentApi";

function formatVND(n) {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(n);
}

function dateToString(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export default function BookingPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();

  const [branches, setBranches] = useState([]);
  const [services, setServices] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);

  const [branchId, setBranchId] = useState("");
  const [selectedDate, setSelectedDate] = useState(null);
  const [slots, setSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [selectedServiceIds, setSelectedServiceIds] = useState([]);
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [selectedModelId, setSelectedModelId] = useState("");
  const [licensePlate, setLicensePlate] = useState("");
  const [vin, setVin] = useState("");
  const [vehicleColor, setVehicleColor] = useState("");
  const [notes, setNotes] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", { replace: true });
      return;
    }
    loadReferenceData();
  }, [isAuthenticated, navigate]);

  async function loadReferenceData() {
    try {
      const [br, svc, vb, vm] = await Promise.all([
        getAllBranches(),
        getAllStandardServices(),
        getVehicleBrands(),
        getVehicleModels(),
      ]);
      setBranches(Array.isArray(br) ? br.filter((b) => b.status === "active") : []);
      setServices(Array.isArray(svc) ? svc.filter((s) => s.isActive !== false) : []);
      setBrands(Array.isArray(vb) ? vb.filter((b) => b.isActive !== false) : []);
      setModels(Array.isArray(vm) ? vm : []);
    } catch {
      /* ignore */
    }
  }

  const loadSlots = useCallback(async (bid, date) => {
    if (!bid || !date) return;
    setSlotsLoading(true);
    setSelectedSlot(null);
    try {
      const data = await getAvailableSlots(bid, dateToString(date));
      setSlots(Array.isArray(data) ? data : []);
    } catch {
      setSlots([]);
    } finally {
      setSlotsLoading(false);
    }
  }, []);

  function handleBranchChange(e) {
    const val = e.target.value;
    setBranchId(val);
    setSelectedSlot(null);
    setSlots([]);
    if (val && selectedDate) loadSlots(val, selectedDate);
  }

  function handleDateChange(date) {
    setSelectedDate(date);
    setSelectedSlot(null);
    if (branchId) loadSlots(branchId, date);
  }

  function toggleService(id) {
    setSelectedServiceIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  }

  async function handleSubmit() {
    setError("");
    setSuccess("");

    if (!branchId) return setError("Vui lòng chọn chi nhánh.");
    if (!selectedDate) return setError("Vui lòng chọn ngày.");
    if (!selectedSlot) return setError("Vui lòng chọn khung giờ.");

    // Add 30-minute buffer check for today
    const now = new Date();
    const todayStr = dateToString(now);
    if (dateToString(selectedDate) === todayStr) {
      const [h, m] = selectedSlot.startTime.split(':').map(Number);
      const slotMins = h * 60 + m;
      const currentMins = now.getHours() * 60 + now.getMinutes();
      if (slotMins < currentMins + 30) {
        return setError("Khung giờ này hiện không còn khả dụng (phải cách hiện tại ít nhất 30 phút). Vui lòng chọn khung giờ khác.");
      }
    }

    setSubmitting(true);
    try {
      await bookAppointment({
        branchId: Number(branchId),
        vehicleId: null,
        vehicleModelId: selectedModelId ? Number(selectedModelId) : null,
        licensePlate: licensePlate.trim() || null,
        vin: vin.trim() || null,
        color: vehicleColor.trim() || null,
        scheduledDate: dateToString(selectedDate),
        startTime: selectedSlot.startTime,
        endTime: selectedSlot.endTime,
        requestedServiceIds: selectedServiceIds.length
          ? selectedServiceIds.join(",")
          : null,
        customerNotes: notes || null,
      });
      setSuccess("Đặt lịch thành công! Vui lòng chờ xác nhận từ Garage.");
      setTimeout(() => navigate("/my-appointments"), 2000);
    } catch (err) {
      setError(err.message || "Đặt lịch thất bại.");
    } finally {
      setSubmitting(false);
    }
  }

  const selectedBranch = branches.find((b) => String(b.id) === String(branchId));
  const selectedBrand = brands.find((b) => String(b.id) === String(selectedBrandId));
  const selectedModel = models.find((m) => String(m.id) === String(selectedModelId));
  const vehicleSummary = [
    selectedBrand?.name,
    selectedModel?.modelName,
    licensePlate ? `(${licensePlate})` : null,
  ].filter(Boolean).join(" ") || "Chưa nhập";
  const totalPrice = services
    .filter((s) => selectedServiceIds.includes(s.id))
    .reduce((sum, s) => sum + (s.basePrice || 0), 0);
  const totalMinutes = services
    .filter((s) => selectedServiceIds.includes(s.id))
    .reduce((sum, s) => sum + (s.standardMinutes || 0), 0);

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 24 }}>
        Đặt lịch hẹn sửa chữa
      </h1>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      <div className="booking-container">
        <div className="booking-sidebar">
          {/* Branch selector */}
          <div className="booking-card">
            <h3>1. Chọn chi nhánh</h3>
            <select value={branchId} onChange={handleBranchChange}>
              <option value="">-- Chọn chi nhánh --</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name} {b.address ? `- ${b.address}` : ""}
                </option>
              ))}
            </select>
          </div>

          {/* Calendar */}
          <div className="booking-card booking-calendar">
            <h3>2. Chọn ngày</h3>
            <Calendar
              onChange={handleDateChange}
              value={selectedDate}
              minDate={new Date()}
              locale="vi-VN"
            />
          </div>

          {/* Vehicle Info */}
          <div className="booking-card">
            <h3>5. Thông tin xe (tùy chọn)</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 4, color: "#4a5568" }}>
                  Hãng xe
                </label>
                <select
                  value={selectedBrandId}
                  onChange={(e) => {
                    setSelectedBrandId(e.target.value);
                    setSelectedModelId("");
                  }}
                >
                  <option value="">-- Chọn hãng xe --</option>
                  {brands.map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 4, color: "#4a5568" }}>
                  Dòng xe
                </label>
                <select
                  value={selectedModelId}
                  onChange={(e) => setSelectedModelId(e.target.value)}
                  disabled={!selectedBrandId}
                >
                  <option value="">-- Chọn dòng xe --</option>
                  {models
                    .filter((m) => String(m.brandId) === String(selectedBrandId))
                    .map((m) => (
                      <option key={m.id} value={m.id}>{m.modelName}</option>
                    ))}
                </select>
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 4, color: "#4a5568" }}>
                  Biển số xe
                </label>
                <input
                  type="text"
                  placeholder="VD: 43A-12345"
                  value={licensePlate}
                  onChange={(e) => setLicensePlate(e.target.value.toUpperCase())}
                  style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 4, color: "#4a5568" }}>
                  Số VIN (tùy chọn)
                </label>
                <input
                  type="text"
                  placeholder="Nhập số khung (VIN)"
                  value={vin}
                  onChange={(e) => setVin(e.target.value.toUpperCase())}
                  style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 4, color: "#4a5568" }}>
                  Màu xe (tùy chọn)
                </label>
                <input
                  type="text"
                  placeholder="VD: Trắng, Đen, Đỏ..."
                  value={vehicleColor}
                  onChange={(e) => setVehicleColor(e.target.value)}
                  style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
                />
              </div>
            </div>
          </div>

          {/* Notes */}
          <div className="booking-card">
            <h3>6. Ghi chú</h3>
            <textarea
              placeholder="Mô tả triệu chứng hoặc yêu cầu đặc biệt..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
            />
          </div>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
          {/* Slot picker */}
          <div className="booking-card">
            <h3>3. Chọn khung giờ</h3>
            {!branchId || !selectedDate ? (
              <div className="empty-state">
                <p>Vui lòng chọn chi nhánh và ngày trước.</p>
              </div>
            ) : (
              <SlotPicker
                slots={slots}
                selectedSlot={selectedSlot}
                onSelect={setSelectedSlot}
                loading={slotsLoading}
                selectedDate={selectedDate}
              />
            )}
          </div>

          {/* Service picker */}
          <div className="booking-card">
            <h3>4. Chọn dịch vụ (tùy chọn)</h3>
            <div className="service-list">
              {services.map((s) => (
                <div
                  key={s.id}
                  className={`service-item ${selectedServiceIds.includes(s.id) ? "selected" : ""}`}
                  onClick={() => toggleService(s.id)}
                >
                  <input
                    type="checkbox"
                    checked={selectedServiceIds.includes(s.id)}
                    onChange={() => toggleService(s.id)}
                  />
                  <div className="service-info">
                    <div className="service-name">{s.name}</div>
                    <div className="service-meta">
                      {s.standardMinutes} phút
                    </div>
                  </div>
                  <div className="service-price">{formatVND(s.basePrice)}</div>
                </div>
              ))}
            </div>
          </div>

          {/* Summary & submit */}
          <div className="booking-card">
            <h3>Tóm tắt</h3>
            <div className="booking-summary">
              <dl>
                <dt>Chi nhánh</dt>
                <dd>{selectedBranch?.name || "Chưa chọn"}</dd>
                <dt>Ngày</dt>
                <dd>
                  {selectedDate
                    ? selectedDate.toLocaleDateString("vi-VN")
                    : "Chưa chọn"}
                </dd>
                <dt>Giờ</dt>
                <dd>
                  {selectedSlot
                    ? `${selectedSlot.startTime?.substring(0, 5)} - ${selectedSlot.endTime?.substring(0, 5)}`
                    : "Chưa chọn"}
                </dd>
                <dt>Xe</dt>
                <dd>{vehicleSummary}</dd>
                <dt>Dịch vụ</dt>
                <dd>
                  {selectedServiceIds.length > 0
                    ? `${selectedServiceIds.length} dịch vụ`
                    : "Không"}
                </dd>
                {totalPrice > 0 && (
                  <>
                    <dt>Tạm tính</dt>
                    <dd style={{ fontWeight: 600, color: "#422afb" }}>
                      {formatVND(totalPrice)}
                    </dd>
                    <dt>Thời gian dự kiến</dt>
                    <dd>{totalMinutes} phút</dd>
                  </>
                )}
              </dl>
            </div>
            <button
              className="btn-book"
              onClick={handleSubmit}
              disabled={submitting || !branchId || !selectedDate || !selectedSlot}
            >
              {submitting ? "Đang đặt lịch..." : "Xác nhận đặt lịch"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
=======
﻿import { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { isAuthenticated } from "../../../app/authState";
import {
  getAllBranches,
  getAllStandardServices,
  getVehicleBrands,
  getVehicleModels,
  bookAppointment,
  bookGuestAppointment,
  getAllProducts,
  getAllCategories,
} from "../api/appointmentApi";
import "./BookingPage.css";

function formatVND(n) {
  return new Intl.NumberFormat("vi-VN").format(n) + " vnd";
}

const OTHER_MODELS = [
  { id: "manual", modelName: "Xe so san" },
  { id: "auto", modelName: "Xe tu dong" },
];

const STEPS = [
  { num: 1, label: "Dich vu" },
  { num: 2, label: "Chon phu kien" },
  { num: 3, label: "Chi nhanh" },
  { num: 4, label: "Thong tin dat hen" },
  { num: 5, label: "Hoan thanh" },
];

const SERVICE_ICONS = ["🔍", "🛡️", "🔧", "🛞", "🔋", "💨", "⛓️", "🛑", "🔩", "⚙️"];

export default function BookingPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);

  const [branches, setBranches] = useState([]);
  const [services, setServices] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);

  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [selectedModelId, setSelectedModelId] = useState("");

  const [branchId, setBranchId] = useState("");
  const [selectedDate, setSelectedDate] = useState("");
  const [selectedTime, setSelectedTime] = useState("09:00");
  const [selectedServiceIds, setSelectedServiceIds] = useState([]);
  const [cart, setCart] = useState({});
  const [activeCatFilter, setActiveCatFilter] = useState("all");
  const [cityFilter, setCityFilter] = useState("");
  const [licensePlate, setLicensePlate] = useState("");
  const [phone, setPhone] = useState("");
  const [notes, setNotes] = useState("");

  const [guestName, setGuestName] = useState("");
  const [guestPhone, setGuestPhone] = useState("");
  const [guestEmail, setGuestEmail] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const loggedIn = isAuthenticated();

  useEffect(() => {
    loadReferenceData();
  }, []);

  async function loadReferenceData() {
    try {
      const [br, svc, vb, vm, prods, cats] = await Promise.all([
        getAllBranches(),
        getAllStandardServices(),
        getVehicleBrands(),
        getVehicleModels(),
        getAllProducts(),
        getAllCategories(),
      ]);
      setBranches(Array.isArray(br) ? br.filter((b) => b.status === "active") : []);
      setServices(Array.isArray(svc) ? svc.filter((s) => s.isActive !== false) : []);
      setBrands(Array.isArray(vb) ? vb.filter((b) => b.isActive !== false) : []);
      setModels(Array.isArray(vm) ? vm : []);
      setProducts(Array.isArray(prods) ? prods.filter((p) => p.isActive !== false && !p.isDeleted) : []);
      setCategories(Array.isArray(cats) ? cats.filter((c) => !c.isDeleted) : []);
    } catch {
      /* ignore */
    }
  }

  function toggleService(id) {
    setSelectedServiceIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  }

  function updateCart(productId, delta) {
    setCart((prev) => {
      const current = prev[productId] || 0;
      const next = Math.max(0, current + delta);
      if (next === 0) {
        const copy = { ...prev };
        delete copy[productId];
        return copy;
      }
      return { ...prev, [productId]: next };
    });
  }

  function removeFromCart(productId) {
    setCart((prev) => {
      const copy = { ...prev };
      delete copy[productId];
      return copy;
    });
  }

  function goToStep(s) {
    setStep(s);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  const isOtherBrand = selectedBrandId === "other";
  const currentModels = isOtherBrand
    ? OTHER_MODELS
    : models.filter((m) => String(m.brandId) === String(selectedBrandId));

  const selectedBranch = branches.find((b) => String(b.id) === String(branchId));
  const selectedBrand = isOtherBrand
    ? { name: "Khac" }
    : brands.find((b) => String(b.id) === String(selectedBrandId));
  const selectedModel = isOtherBrand
    ? OTHER_MODELS.find((m) => m.id === selectedModelId)
    : models.find((m) => String(m.id) === String(selectedModelId));

  const vehicleName = [selectedBrand?.name, selectedModel?.modelName].filter(Boolean).join(" - ") || "";
  const selectedServices = services.filter((s) => selectedServiceIds.includes(s.id));
  const serviceTotalPrice = selectedServices.reduce((sum, s) => sum + (s.basePrice || 0), 0);
  const totalMinutes = selectedServices.reduce((sum, s) => sum + (s.standardMinutes || 0), 0);

  const cartItems = useMemo(() => {
    return Object.entries(cart).map(([pid, qty]) => {
      const p = products.find((x) => String(x.id) === String(pid));
      return p ? { ...p, qty } : null;
    }).filter(Boolean);
  }, [cart, products]);

  const productTotalPrice = cartItems.reduce((sum, item) => sum + (item.retailPrice || 0) * item.qty, 0);
  const totalPrice = serviceTotalPrice + productTotalPrice;

  const filteredProducts = useMemo(() => {
    let list = products;
    if (activeCatFilter !== "all") {
      list = list.filter((p) => String(p.categoryId) === String(activeCatFilter));
    }
    return list;
  }, [products, activeCatFilter]);

  const usedCategoryIds = useMemo(() => {
    const ids = new Set();
    products.forEach((p) => { if (p.categoryId) ids.add(p.categoryId); });
    return ids;
  }, [products]);

  const visibleCategories = categories.filter((c) => usedCategoryIds.has(c.id));

  function extractCity(address) {
    if (!address) return "";
    const parts = address.split(",").map((s) => s.trim());
    return parts[parts.length - 1] || "";
  }

  const branchCities = useMemo(() => {
    const cities = new Set();
    branches.forEach((b) => {
      const city = extractCity(b.address);
      if (city) cities.add(city);
    });
    return [...cities].sort();
  }, [branches]);

  const filteredBranches = useMemo(() => {
    if (!cityFilter) return branches;
    return branches.filter((b) => extractCity(b.address) === cityFilter);
  }, [branches, cityFilter]);

  function selectBranch(id) {
    setBranchId(String(id));
  }

  function getTodayStr() {
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  }

  async function handleSubmit() {
    setError("");
    setSuccess("");
    if (!branchId) return setError("Vui long chon chi nhanh.");
    if (!selectedDate) return setError("Vui long chon ngay hen.");
    if (!selectedTime) return setError("Vui long chon gio hen.");
    if (!loggedIn && !guestPhone.trim()) return setError("Vui long nhap so dien thoai.");

    setSubmitting(true);
    try {
      const realModelId = isOtherBrand ? null : (selectedModelId ? Number(selectedModelId) : null);
=======
﻿import { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { isAuthenticated } from "../../../app/authState";
import {
  getAllBranches,
  getAllStandardServices,
  getVehicleBrands,
  getVehicleModels,
  bookAppointment,
  bookGuestAppointment,
  getAllProducts,
  getAllCategories,
} from "../api/appointmentApi";
import "./BookingPage.css";

function formatVND(n) {
  return new Intl.NumberFormat("vi-VN").format(n) + " vnd";
}

const OTHER_MODELS = [
  { id: "manual", modelName: "Xe so san" },
  { id: "auto", modelName: "Xe tu dong" },
];

const STEPS = [
  { num: 1, label: "Dich vu" },
  { num: 2, label: "Chon phu kien" },
  { num: 3, label: "Chi nhanh" },
  { num: 4, label: "Thong tin dat hen" },
  { num: 5, label: "Hoan thanh" },
];

const SERVICE_ICONS = ["🔍", "🛡️", "🔧", "🛞", "🔋", "💨", "⛓️", "🛑", "🔩", "⚙️"];

export default function BookingPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);

  const [branches, setBranches] = useState([]);
  const [services, setServices] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);

  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [selectedModelId, setSelectedModelId] = useState("");

  const [branchId, setBranchId] = useState("");
  const [selectedDate, setSelectedDate] = useState("");
  const [selectedTime, setSelectedTime] = useState("09:00");
  const [selectedServiceIds, setSelectedServiceIds] = useState([]);
  const [cart, setCart] = useState({});
  const [activeCatFilter, setActiveCatFilter] = useState("all");
  const [cityFilter, setCityFilter] = useState("");
  const [licensePlate, setLicensePlate] = useState("");
  const [phone, setPhone] = useState("");
  const [notes, setNotes] = useState("");

  const [guestName, setGuestName] = useState("");
  const [guestPhone, setGuestPhone] = useState("");
  const [guestEmail, setGuestEmail] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const loggedIn = isAuthenticated();

  useEffect(() => {
    loadReferenceData();
  }, []);

  async function loadReferenceData() {
    try {
      const [br, svc, vb, vm, prods, cats] = await Promise.all([
        getAllBranches(),
        getAllStandardServices(),
        getVehicleBrands(),
        getVehicleModels(),
        getAllProducts(),
        getAllCategories(),
      ]);
      setBranches(Array.isArray(br) ? br.filter((b) => b.status === "active") : []);
      setServices(Array.isArray(svc) ? svc.filter((s) => s.isActive !== false) : []);
      setBrands(Array.isArray(vb) ? vb.filter((b) => b.isActive !== false) : []);
      setModels(Array.isArray(vm) ? vm : []);
      setProducts(Array.isArray(prods) ? prods.filter((p) => p.isActive !== false && !p.isDeleted) : []);
      setCategories(Array.isArray(cats) ? cats.filter((c) => !c.isDeleted) : []);
    } catch {
      /* ignore */
    }
  }

  function toggleService(id) {
    setSelectedServiceIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  }

  function updateCart(productId, delta) {
    setCart((prev) => {
      const current = prev[productId] || 0;
      const next = Math.max(0, current + delta);
      if (next === 0) {
        const copy = { ...prev };
        delete copy[productId];
        return copy;
      }
      return { ...prev, [productId]: next };
    });
  }

  function removeFromCart(productId) {
    setCart((prev) => {
      const copy = { ...prev };
      delete copy[productId];
      return copy;
    });
  }

  function goToStep(s) {
    setStep(s);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  const isOtherBrand = selectedBrandId === "other";
  const currentModels = isOtherBrand
    ? OTHER_MODELS
    : models.filter((m) => String(m.brandId) === String(selectedBrandId));

  const selectedBranch = branches.find((b) => String(b.id) === String(branchId));
  const selectedBrand = isOtherBrand
    ? { name: "Khac" }
    : brands.find((b) => String(b.id) === String(selectedBrandId));
  const selectedModel = isOtherBrand
    ? OTHER_MODELS.find((m) => m.id === selectedModelId)
    : models.find((m) => String(m.id) === String(selectedModelId));

  const vehicleName = [selectedBrand?.name, selectedModel?.modelName].filter(Boolean).join(" - ") || "";
  const selectedServices = services.filter((s) => selectedServiceIds.includes(s.id));
  const serviceTotalPrice = selectedServices.reduce((sum, s) => sum + (s.basePrice || 0), 0);
  const totalMinutes = selectedServices.reduce((sum, s) => sum + (s.standardMinutes || 0), 0);

  const cartItems = useMemo(() => {
    return Object.entries(cart).map(([pid, qty]) => {
      const p = products.find((x) => String(x.id) === String(pid));
      return p ? { ...p, qty } : null;
    }).filter(Boolean);
  }, [cart, products]);

  const productTotalPrice = cartItems.reduce((sum, item) => sum + (item.retailPrice || 0) * item.qty, 0);
  const totalPrice = serviceTotalPrice + productTotalPrice;

  const filteredProducts = useMemo(() => {
    let list = products;
    if (activeCatFilter !== "all") {
      list = list.filter((p) => String(p.categoryId) === String(activeCatFilter));
    }
    return list;
  }, [products, activeCatFilter]);

  const usedCategoryIds = useMemo(() => {
    const ids = new Set();
    products.forEach((p) => { if (p.categoryId) ids.add(p.categoryId); });
    return ids;
  }, [products]);

  const visibleCategories = categories.filter((c) => usedCategoryIds.has(c.id));

  function extractCity(address) {
    if (!address) return "";
    const parts = address.split(",").map((s) => s.trim());
    return parts[parts.length - 1] || "";
  }

  const branchCities = useMemo(() => {
    const cities = new Set();
    branches.forEach((b) => {
      const city = extractCity(b.address);
      if (city) cities.add(city);
    });
    return [...cities].sort();
  }, [branches]);

  const filteredBranches = useMemo(() => {
    if (!cityFilter) return branches;
    return branches.filter((b) => extractCity(b.address) === cityFilter);
  }, [branches, cityFilter]);

  function selectBranch(id) {
    setBranchId(String(id));
  }

  function getTodayStr() {
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  }

  async function handleSubmit() {
    setError("");
    setSuccess("");
    if (!branchId) return setError("Vui long chon chi nhanh.");
    if (!selectedDate) return setError("Vui long chon ngay hen.");
    if (!selectedTime) return setError("Vui long chon gio hen.");
    if (!loggedIn && !guestPhone.trim()) return setError("Vui long nhap so dien thoai.");

    setSubmitting(true);
    try {
      const realModelId = isOtherBrand ? null : (selectedModelId ? Number(selectedModelId) : null);
>>>>>>> theirs
      const [startHour, startMinute] = selectedTime.split(":").map(Number);
      const startAt = new Date(selectedDate);
      startAt.setHours(startHour || 0, startMinute || 0, 0, 0);
      const durationMinutes = Math.max(60, totalMinutes || 0);
      const endAt = new Date(startAt.getTime() + durationMinutes * 60 * 1000);
<<<<<<< ours

      const payload = {
        branchId: Number(branchId),
        vehicleId: null,
        vehicleModelId: realModelId,
        licensePlate: licensePlate.trim() || null,
        color: null,
        startTime: startAt.toISOString(),
        endTime: endAt.toISOString(),
        requestedServiceIds: selectedServiceIds.length
          ? selectedServiceIds.join(",")
          : null,
        customerNotes: notes || null,
      };

      if (loggedIn) {
        await bookAppointment(payload);
      } else {
        payload.guestName = guestName.trim() || null;
        payload.guestPhone = guestPhone.trim();
        payload.guestEmail = guestEmail.trim() || null;
        await bookGuestAppointment(payload);
      }

      setSuccess("Dat lich thanh cong!");
      goToStep(5);
    } catch (err) {
      setError(err.message || "Dat lich that bai.");
    } finally {
      setSubmitting(false);
    }
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 0: Landing
     ═══════════════════════════════════════════════════════════════════════ */
=======

      const payload = {
        branchId: Number(branchId),
        vehicleId: null,
        vehicleModelId: realModelId,
        licensePlate: licensePlate.trim() || null,
        color: null,
        startTime: startAt.toISOString(),
        endTime: endAt.toISOString(),
        requestedServiceIds: selectedServiceIds.length
          ? selectedServiceIds.join(",")
          : null,
        customerNotes: notes || null,
      };

      if (loggedIn) {
        await bookAppointment(payload);
      } else {
        payload.guestName = guestName.trim() || null;
        payload.guestPhone = guestPhone.trim();
        payload.guestEmail = guestEmail.trim() || null;
        await bookGuestAppointment(payload);
      }

      setSuccess("Dat lich thanh cong!");
      goToStep(5);
    } catch (err) {
      setError(err.message || "Dat lich that bai.");
    } finally {
      setSubmitting(false);
    }
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 0: Landing
     ═══════════════════════════════════════════════════════════════════════ */
>>>>>>> theirs
  if (step === 0) {
    return (
      <div className="bp-landing">
        <div className="bp-hero">
          <div className="bp-hero-banner-shell">
            <div className="bp-hero-banner">
              <div className="bp-hero-hotline">HOTLINE: 1900 6868</div>
              <div className="bp-hero-nav-sections">
                <div className="bp-hero-menu left">
                  <button type="button" onClick={() => navigate("/home")}>TRANG CHỦ</button>
                  <button type="button" className="active" onClick={() => navigate("/booking")}>ĐẶT LỊCH</button>
                  <button type="button" onClick={() => navigate("/my-appointments")}>LỊCH HẸN CỦA TÔI</button>
                </div>
                <button type="button" className="bp-hero-logo" onClick={() => navigate("/home")} aria-label="MY GARAGE">
                  <span>MY</span>
                  <small>GARAGE</small>
                </button>
                <div className="bp-hero-menu right">
                  <button type="button" onClick={() => navigate("/workshop")}>XƯỞNG DỊCH VỤ</button>
                  <a href="#doi-tac">ĐỐI TÁC</a>
                  <a href="#dich-vu-noi-bat">DỊCH VỤ &amp; GIÁ</a>
                  <a href="#lien-he">LIÊN HỆ</a>
                </div>
              </div>
            </div>
          </div>
          <div className="bp-hero-content">
            <h1>Dat Lich Sua Chua & Bao Duong</h1>
            <p>He thong dat lich truc tuyen - Nhanh chong, tien loi, chuyen nghiep</p>
          </div>
<<<<<<< ours
          <div className="bp-finder">
            <div className="bp-finder-header">
              <span className="bp-finder-icon">🔧</span>
              Tim dich vu
            </div>
            <div className="bp-finder-body">
              <div className="bp-finder-fields">
                <div className="bp-finder-field">
                  <label>Hang xe</label>
                  <select
                    value={selectedBrandId}
                    onChange={(e) => { setSelectedBrandId(e.target.value); setSelectedModelId(""); }}
                  >
                    <option value="">Chon Hang Xe</option>
                    {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                    <option value="other">Khac</option>
                  </select>
                </div>
                <div className="bp-finder-field">
                  <label>Loai xe</label>
                  <select
                    value={selectedModelId}
                    onChange={(e) => setSelectedModelId(e.target.value)}
                    disabled={!selectedBrandId}
                  >
                    <option value="">Chon Loai Xe</option>
                    {currentModels.map((m) => <option key={m.id} value={m.id}>{m.modelName}</option>)}
                  </select>
                </div>
                <button className="bp-finder-btn" onClick={() => { if (selectedBrandId) goToStep(1); }} disabled={!selectedBrandId}>
                  Tim dich vu
                </button>
              </div>
            </div>
          </div>
        </div>
        <div className="bp-features">
          <div className="bp-feature-card"><div className="bp-feature-icon">📅</div><h3>Dat lich de dang</h3><p>Chon ngay, gio va dich vu chi trong vai buoc don gian</p></div>
          <div className="bp-feature-card"><div className="bp-feature-icon">🔧</div><h3>Dich vu chuyen nghiep</h3><p>Doi ngu ky thuat vien gioi, thiet bi hien dai</p></div>
          <div className="bp-feature-card"><div className="bp-feature-icon">💰</div><h3>Gia ca minh bach</h3><p>Bao gia ro rang truoc khi thuc hien dich vu</p></div>
          <div className="bp-feature-card"><div className="bp-feature-icon">⏱️</div><h3>Tiet kiem thoi gian</h3><p>Khong phai cho doi, xe duoc xu ly dung gio hen</p></div>
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     Progress Bar
     ═══════════════════════════════════════════════════════════════════════ */
  const progressBar = (
    <div className="bp-progress">
      {STEPS.map((s) => (
        <div key={s.num} className={`bp-progress-step ${step >= s.num ? "active" : ""} ${step === s.num ? "current" : ""}`}>
          <div className="bp-progress-num">{s.num}</div>
          <span>{s.label}</span>
        </div>
      ))}
    </div>
  );

  /* ═══════════════════════════════════════════════════════════════════════
     Sidebar — Order Summary
     ═══════════════════════════════════════════════════════════════════════ */
  const sidebar = (
    <div className="bp-sidebar">
      <div className="bp-sidebar-card">
        <h3>Tom tat don dat lich</h3>

        <div className="bp-sidebar-section">
          <div className="bp-sidebar-label">THONG TIN XE</div>
          <div className="bp-sidebar-vehicle">
            <span className="bp-sidebar-vehicle-icon">🚗</span>
            <span>{vehicleName || "Chua chon xe"}</span>
          </div>
        </div>

        {selectedServices.length > 0 && (
          <div className="bp-sidebar-section">
            <div className="bp-sidebar-label">DICH VU</div>
            {selectedServices.map((s) => (
              <div key={s.id} className="bp-sidebar-svc-row">
                <span>{s.name}</span>
                <span className="bp-sidebar-svc-price">{formatVND(s.basePrice)}</span>
              </div>
            ))}
          </div>
        )}

        {cartItems.length > 0 && (
          <div className="bp-sidebar-section">
            <div className="bp-sidebar-label">SAN PHAM</div>
            {cartItems.map((item) => (
              <div key={item.id} className="bp-sidebar-product-row">
                <button className="bp-sidebar-remove" onClick={() => removeFromCart(item.id)}>×</button>
                <span className="bp-sidebar-product-name">{item.name} x {item.qty}</span>
                <span className="bp-sidebar-svc-price">{formatVND((item.retailPrice || 0) * item.qty)}</span>
              </div>
            ))}
          </div>
        )}

        {selectedBranch && (
          <div className="bp-sidebar-section">
            <div className="bp-sidebar-label">CHI NHANH</div>
            <div className="bp-sidebar-branch">
              <strong>{selectedBranch.name}</strong>
              {selectedBranch.address && <div className="bp-sidebar-addr">{selectedBranch.address}</div>}
            </div>
          </div>
        )}

        <div className="bp-sidebar-total">
          <span>Tong cong</span>
          <span className="bp-sidebar-total-price">{formatVND(totalPrice)}</span>
        </div>

        {step === 1 && (
          <button className="bp-sidebar-btn" onClick={() => goToStep(2)}>Tiep tuc</button>
        )}
        {step === 2 && (
          <button className="bp-sidebar-btn" onClick={() => goToStep(3)}>Tiep tuc</button>
        )}
        {step === 3 && (
          <button className="bp-sidebar-btn" onClick={() => goToStep(4)} disabled={!branchId}>
            Tiep tuc
          </button>
        )}
        {step === 4 && (
          <button className="bp-sidebar-btn" onClick={handleSubmit} disabled={submitting || !branchId || !selectedDate || !selectedTime}>
            {submitting ? "Dang dat lich..." : "Xac nhan dat lich"}
          </button>
        )}
      </div>
    </div>
  );

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 1: Chon dich vu
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 1) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Chon dich vu</h2>
            <p className="bp-wizard-subtitle">Quy khach nhan chon vao dich vu quy khach can su dung</p>
            <div className="bp-svc-list">
              {services.map((s, idx) => {
                const isSelected = selectedServiceIds.includes(s.id);
                return (
                  <div key={s.id} className={`bp-svc-card ${isSelected ? "selected" : ""}`} onClick={() => toggleService(s.id)}>
                    <div className="bp-svc-icon">{SERVICE_ICONS[idx % SERVICE_ICONS.length]}</div>
                    <div className="bp-svc-info">
                      <div className="bp-svc-name">{s.name}</div>
                      <div className="bp-svc-desc">{s.description || `Thoi gian: ~${s.standardMinutes} phut`}</div>
                    </div>
                    <div className="bp-svc-right">
                      <div className="bp-svc-price">{formatVND(s.basePrice)}</div>
                      <button className={`bp-svc-btn ${isSelected ? "selected" : ""}`} onClick={(e) => { e.stopPropagation(); toggleService(s.id); }}>
                        {isSelected ? "✓ Da chon" : "+ Chon dich vu"}
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 2: Chon phu kien / san pham
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 2) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Chon san pham</h2>
            <p className="bp-wizard-subtitle">Chon phu kien, phu tung can thiet cho xe cua ban</p>

            {/* Category filter pills */}
            <div className="bp-filter-row">
              <span className="bp-filter-label">Danh muc:</span>
              <div className="bp-filter-pills">
                <button className={`bp-pill ${activeCatFilter === "all" ? "active" : ""}`} onClick={() => setActiveCatFilter("all")}>
                  {activeCatFilter === "all" && <span className="bp-pill-check">✓</span>} Tat ca
                </button>
                {visibleCategories.map((c) => (
                  <button key={c.id} className={`bp-pill ${activeCatFilter === c.id ? "active" : ""}`} onClick={() => setActiveCatFilter(c.id)}>
                    {activeCatFilter === c.id && <span className="bp-pill-check">✓</span>} {c.categoryName}
                  </button>
                ))}
              </div>
            </div>

            {/* Product list */}
            <div className="bp-prod-list">
              {filteredProducts.length === 0 ? (
                <div className="empty-state">
                  <div className="empty-icon">📦</div>
                  <p>Chua co san pham nao.</p>
                </div>
              ) : (
                filteredProducts.map((p) => {
                  const qty = cart[p.id] || 0;
                  return (
                    <div key={p.id} className="bp-prod-card">
                      <div className="bp-prod-img">📦</div>
                      <div className="bp-prod-info">
                        <div className="bp-prod-name">{p.name}</div>
                        <div className="bp-prod-price">{formatVND(p.retailPrice || 0)}</div>
                      </div>
                      <div className="bp-prod-actions">
                        <div className="bp-qty-control">
                          <button className="bp-qty-btn" onClick={() => updateCart(p.id, -1)} disabled={qty === 0}>−</button>
                          <span className="bp-qty-value">{qty}</span>
                          <button className="bp-qty-btn" onClick={() => updateCart(p.id, 1)}>+</button>
                        </div>
                        {qty === 0 ? (
                          <button className="bp-add-cart-btn" onClick={() => updateCart(p.id, 1)}>
                            🛒 Them vao gio hang
                          </button>
                        ) : (
                          <button className="bp-add-cart-btn added" onClick={() => removeFromCart(p.id)}>
                            ✓ Da them ({qty})
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })
              )}
            </div>

            <div style={{ marginTop: 20 }}>
              <button className="bp-back-link" onClick={() => goToStep(1)}>← Quay lai chon dich vu</button>
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 3: Cua hang (Branch cards) + Ngay + Ca
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 3) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Cua hang</h2>
            <p className="bp-wizard-subtitle">Chon chi nhanh phu hop voi ban</p>

            {error && <div className="alert alert-error">{error}</div>}

            {/* City filter */}
            {branchCities.length > 1 && (
              <div className="bp-branch-filters">
                <select value={cityFilter} onChange={(e) => setCityFilter(e.target.value)} className="bp-city-select">
                  <option value="">Tat ca thanh pho</option>
                  {branchCities.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
            )}

            {/* Branch cards */}
            <div className="bp-branch-list">
              {filteredBranches.map((b) => {
                const isSelected = String(b.id) === String(branchId);
                return (
                  <div key={b.id} className={`bp-branch-card ${isSelected ? "selected" : ""}`} onClick={() => selectBranch(b.id)}>
                    <div className="bp-branch-avatar">
                      {b.avatarUrl ? <img src={b.avatarUrl} alt={b.name} /> : <span>🏪</span>}
                    </div>
                    <div className="bp-branch-info">
                      <div className="bp-branch-name">{b.name}</div>
                      {b.address && <div className="bp-branch-detail">📍 {b.address}</div>}
                      {b.hotline && <div className="bp-branch-detail">📞 {b.hotline}</div>}
                    </div>
                    <div className="bp-branch-radio">
                      <div className={`bp-radio ${isSelected ? "checked" : ""}`}>
                        {isSelected && <div className="bp-radio-dot" />}
                      </div>
                      <span className="bp-branch-radio-label">{isSelected ? "Da chon" : "Chon cua hang"}</span>
                    </div>
                  </div>
                );
              })}
            </div>

            <div style={{ marginTop: 20 }}>
              <button className="bp-back-link" onClick={() => goToStep(2)}>← Quay lai chon phu kien</button>
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 4: Thong tin dat hen
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 4) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Thong tin dat hen</h2>
            <p className="bp-wizard-subtitle">Dien thong tin va chon thoi gian hen</p>

            {error && <div className="alert alert-error">{error}</div>}

            {/* Guest info — only when NOT logged in */}
            {!loggedIn && (
              <div className="booking-card">
                <h3>Thong tin khach hang</h3>
                <div className="bp-guest-fields">
                  <div className="bp-guest-field">
                    <label>Ho va ten</label>
                    <input type="text" placeholder="Nguyen Van A" value={guestName} onChange={(e) => setGuestName(e.target.value)} className="bp-plate-input" />
                  </div>
                  <div className="bp-guest-field">
                    <label>So dien thoai <span className="bp-required">*</span></label>
                    <input type="tel" placeholder="0901234567" value={guestPhone} onChange={(e) => setGuestPhone(e.target.value)} className="bp-plate-input" />
                  </div>
                  <div className="bp-guest-field">
                    <label>Email</label>
                    <input type="email" placeholder="email@example.com" value={guestEmail} onChange={(e) => setGuestEmail(e.target.value)} className="bp-plate-input" />
                  </div>
                </div>
              </div>
            )}

            {/* License plate + Phone */}
            <div className="booking-card" style={{ marginTop: loggedIn ? 0 : 20 }}>
              <h3>Thong tin xe</h3>
              <div className="bp-guest-fields">
                <div className="bp-guest-field">
                  <label>Bien so xe (tuy chon)</label>
                  <input type="text" placeholder="VD: 43A-12345" value={licensePlate} onChange={(e) => setLicensePlate(e.target.value.toUpperCase())} className="bp-plate-input" />
                </div>
                {loggedIn && (
                  <div className="bp-guest-field">
                    <label>So dien thoai (tuy chon)</label>
                    <input type="tel" placeholder="0901234567" value={phone} onChange={(e) => setPhone(e.target.value)} className="bp-plate-input" />
                  </div>
                )}
              </div>
            </div>

            {/* Date + Time picker */}
            <div className="booking-card" style={{ marginTop: 20 }}>
              <h3>Thoi gian dat lich hen</h3>
              <div className="bp-datetime-row">
                <div className="bp-datetime-field">
                  <label>Ngay</label>
                  <input
                    type="date"
                    value={selectedDate}
                    onChange={(e) => setSelectedDate(e.target.value)}
                    min={getTodayStr()}
                    className="bp-date-input"
                  />
                </div>
                <div className="bp-datetime-field">
                  <label>Gio</label>
                  <input
                    type="time"
                    value={selectedTime}
                    onChange={(e) => setSelectedTime(e.target.value)}
                    min="07:00"
                    max="21:00"
                    className="bp-time-input"
                  />
                </div>
              </div>
            </div>

            {/* Notes */}
            <div className="booking-card" style={{ marginTop: 20 }}>
              <h3>Ghi chu</h3>
              <textarea placeholder="Mo ta trieu chung hoac yeu cau dac biet..." value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>

            <div style={{ marginTop: 20 }}>
              <button className="bp-back-link" onClick={() => goToStep(3)}>← Quay lai chon cua hang</button>
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 5: Hoan thanh
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 5) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-done">
          <div className="bp-done-icon">✅</div>
          <h2>Dat lich thanh cong!</h2>
          <p>Cam on ban da su dung dich vu cua MyGarage.</p>
          <p>Chung toi se xac nhan lich hen cua ban trong thoi gian som nhat.</p>
          <div className="bp-done-actions">
            <button className="bp-done-btn primary" onClick={() => navigate("/my-appointments")}>Xem lich hen cua toi</button>
            <button className="bp-done-btn secondary" onClick={() => { setStep(0); setSelectedServiceIds([]); setCart({}); setBranchId(""); setSelectedDate(""); setSelectedTime("09:00"); setLicensePlate(""); setNotes(""); }}>
              Dat lich moi
            </button>
          </div>
        </div>
      </div>
    );
  }

  return null;
}
>>>>>>> theirs
=======
          <div className="bp-finder">
            <div className="bp-finder-header">
              <span className="bp-finder-icon">🔧</span>
              Tim dich vu
            </div>
            <div className="bp-finder-body">
              <div className="bp-finder-fields">
                <div className="bp-finder-field">
                  <label>Hang xe</label>
                  <select
                    value={selectedBrandId}
                    onChange={(e) => { setSelectedBrandId(e.target.value); setSelectedModelId(""); }}
                  >
                    <option value="">Chon Hang Xe</option>
                    {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                    <option value="other">Khac</option>
                  </select>
                </div>
                <div className="bp-finder-field">
                  <label>Loai xe</label>
                  <select
                    value={selectedModelId}
                    onChange={(e) => setSelectedModelId(e.target.value)}
                    disabled={!selectedBrandId}
                  >
                    <option value="">Chon Loai Xe</option>
                    {currentModels.map((m) => <option key={m.id} value={m.id}>{m.modelName}</option>)}
                  </select>
                </div>
                <button className="bp-finder-btn" onClick={() => { if (selectedBrandId) goToStep(1); }} disabled={!selectedBrandId}>
                  Tim dich vu
                </button>
              </div>
            </div>
          </div>
        </div>
        <div className="bp-features">
          <div className="bp-feature-card"><div className="bp-feature-icon">📅</div><h3>Dat lich de dang</h3><p>Chon ngay, gio va dich vu chi trong vai buoc don gian</p></div>
          <div className="bp-feature-card"><div className="bp-feature-icon">🔧</div><h3>Dich vu chuyen nghiep</h3><p>Doi ngu ky thuat vien gioi, thiet bi hien dai</p></div>
          <div className="bp-feature-card"><div className="bp-feature-icon">💰</div><h3>Gia ca minh bach</h3><p>Bao gia ro rang truoc khi thuc hien dich vu</p></div>
          <div className="bp-feature-card"><div className="bp-feature-icon">⏱️</div><h3>Tiet kiem thoi gian</h3><p>Khong phai cho doi, xe duoc xu ly dung gio hen</p></div>
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     Progress Bar
     ═══════════════════════════════════════════════════════════════════════ */
  const progressBar = (
    <div className="bp-progress">
      {STEPS.map((s) => (
        <div key={s.num} className={`bp-progress-step ${step >= s.num ? "active" : ""} ${step === s.num ? "current" : ""}`}>
          <div className="bp-progress-num">{s.num}</div>
          <span>{s.label}</span>
        </div>
      ))}
    </div>
  );

  /* ═══════════════════════════════════════════════════════════════════════
     Sidebar — Order Summary
     ═══════════════════════════════════════════════════════════════════════ */
  const sidebar = (
    <div className="bp-sidebar">
      <div className="bp-sidebar-card">
        <h3>Tom tat don dat lich</h3>

        <div className="bp-sidebar-section">
          <div className="bp-sidebar-label">THONG TIN XE</div>
          <div className="bp-sidebar-vehicle">
            <span className="bp-sidebar-vehicle-icon">🚗</span>
            <span>{vehicleName || "Chua chon xe"}</span>
          </div>
        </div>

        {selectedServices.length > 0 && (
          <div className="bp-sidebar-section">
            <div className="bp-sidebar-label">DICH VU</div>
            {selectedServices.map((s) => (
              <div key={s.id} className="bp-sidebar-svc-row">
                <span>{s.name}</span>
                <span className="bp-sidebar-svc-price">{formatVND(s.basePrice)}</span>
              </div>
            ))}
          </div>
        )}

        {cartItems.length > 0 && (
          <div className="bp-sidebar-section">
            <div className="bp-sidebar-label">SAN PHAM</div>
            {cartItems.map((item) => (
              <div key={item.id} className="bp-sidebar-product-row">
                <button className="bp-sidebar-remove" onClick={() => removeFromCart(item.id)}>×</button>
                <span className="bp-sidebar-product-name">{item.name} x {item.qty}</span>
                <span className="bp-sidebar-svc-price">{formatVND((item.retailPrice || 0) * item.qty)}</span>
              </div>
            ))}
          </div>
        )}

        {selectedBranch && (
          <div className="bp-sidebar-section">
            <div className="bp-sidebar-label">CHI NHANH</div>
            <div className="bp-sidebar-branch">
              <strong>{selectedBranch.name}</strong>
              {selectedBranch.address && <div className="bp-sidebar-addr">{selectedBranch.address}</div>}
            </div>
          </div>
        )}

        <div className="bp-sidebar-total">
          <span>Tong cong</span>
          <span className="bp-sidebar-total-price">{formatVND(totalPrice)}</span>
        </div>

        {step === 1 && (
          <button className="bp-sidebar-btn" onClick={() => goToStep(2)}>Tiep tuc</button>
        )}
        {step === 2 && (
          <button className="bp-sidebar-btn" onClick={() => goToStep(3)}>Tiep tuc</button>
        )}
        {step === 3 && (
          <button className="bp-sidebar-btn" onClick={() => goToStep(4)} disabled={!branchId}>
            Tiep tuc
          </button>
        )}
        {step === 4 && (
          <button className="bp-sidebar-btn" onClick={handleSubmit} disabled={submitting || !branchId || !selectedDate || !selectedTime}>
            {submitting ? "Dang dat lich..." : "Xac nhan dat lich"}
          </button>
        )}
      </div>
    </div>
  );

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 1: Chon dich vu
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 1) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Chon dich vu</h2>
            <p className="bp-wizard-subtitle">Quy khach nhan chon vao dich vu quy khach can su dung</p>
            <div className="bp-svc-list">
              {services.map((s, idx) => {
                const isSelected = selectedServiceIds.includes(s.id);
                return (
                  <div key={s.id} className={`bp-svc-card ${isSelected ? "selected" : ""}`} onClick={() => toggleService(s.id)}>
                    <div className="bp-svc-icon">{SERVICE_ICONS[idx % SERVICE_ICONS.length]}</div>
                    <div className="bp-svc-info">
                      <div className="bp-svc-name">{s.name}</div>
                      <div className="bp-svc-desc">{s.description || `Thoi gian: ~${s.standardMinutes} phut`}</div>
                    </div>
                    <div className="bp-svc-right">
                      <div className="bp-svc-price">{formatVND(s.basePrice)}</div>
                      <button className={`bp-svc-btn ${isSelected ? "selected" : ""}`} onClick={(e) => { e.stopPropagation(); toggleService(s.id); }}>
                        {isSelected ? "✓ Da chon" : "+ Chon dich vu"}
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 2: Chon phu kien / san pham
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 2) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Chon san pham</h2>
            <p className="bp-wizard-subtitle">Chon phu kien, phu tung can thiet cho xe cua ban</p>

            {/* Category filter pills */}
            <div className="bp-filter-row">
              <span className="bp-filter-label">Danh muc:</span>
              <div className="bp-filter-pills">
                <button className={`bp-pill ${activeCatFilter === "all" ? "active" : ""}`} onClick={() => setActiveCatFilter("all")}>
                  {activeCatFilter === "all" && <span className="bp-pill-check">✓</span>} Tat ca
                </button>
                {visibleCategories.map((c) => (
                  <button key={c.id} className={`bp-pill ${activeCatFilter === c.id ? "active" : ""}`} onClick={() => setActiveCatFilter(c.id)}>
                    {activeCatFilter === c.id && <span className="bp-pill-check">✓</span>} {c.categoryName}
                  </button>
                ))}
              </div>
            </div>

            {/* Product list */}
            <div className="bp-prod-list">
              {filteredProducts.length === 0 ? (
                <div className="empty-state">
                  <div className="empty-icon">📦</div>
                  <p>Chua co san pham nao.</p>
                </div>
              ) : (
                filteredProducts.map((p) => {
                  const qty = cart[p.id] || 0;
                  return (
                    <div key={p.id} className="bp-prod-card">
                      <div className="bp-prod-img">📦</div>
                      <div className="bp-prod-info">
                        <div className="bp-prod-name">{p.name}</div>
                        <div className="bp-prod-price">{formatVND(p.retailPrice || 0)}</div>
                      </div>
                      <div className="bp-prod-actions">
                        <div className="bp-qty-control">
                          <button className="bp-qty-btn" onClick={() => updateCart(p.id, -1)} disabled={qty === 0}>−</button>
                          <span className="bp-qty-value">{qty}</span>
                          <button className="bp-qty-btn" onClick={() => updateCart(p.id, 1)}>+</button>
                        </div>
                        {qty === 0 ? (
                          <button className="bp-add-cart-btn" onClick={() => updateCart(p.id, 1)}>
                            🛒 Them vao gio hang
                          </button>
                        ) : (
                          <button className="bp-add-cart-btn added" onClick={() => removeFromCart(p.id)}>
                            ✓ Da them ({qty})
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })
              )}
            </div>

            <div style={{ marginTop: 20 }}>
              <button className="bp-back-link" onClick={() => goToStep(1)}>← Quay lai chon dich vu</button>
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 3: Cua hang (Branch cards) + Ngay + Ca
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 3) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Cua hang</h2>
            <p className="bp-wizard-subtitle">Chon chi nhanh phu hop voi ban</p>

            {error && <div className="alert alert-error">{error}</div>}

            {/* City filter */}
            {branchCities.length > 1 && (
              <div className="bp-branch-filters">
                <select value={cityFilter} onChange={(e) => setCityFilter(e.target.value)} className="bp-city-select">
                  <option value="">Tat ca thanh pho</option>
                  {branchCities.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
            )}

            {/* Branch cards */}
            <div className="bp-branch-list">
              {filteredBranches.map((b) => {
                const isSelected = String(b.id) === String(branchId);
                return (
                  <div key={b.id} className={`bp-branch-card ${isSelected ? "selected" : ""}`} onClick={() => selectBranch(b.id)}>
                    <div className="bp-branch-avatar">
                      {b.avatarUrl ? <img src={b.avatarUrl} alt={b.name} /> : <span>🏪</span>}
                    </div>
                    <div className="bp-branch-info">
                      <div className="bp-branch-name">{b.name}</div>
                      {b.address && <div className="bp-branch-detail">📍 {b.address}</div>}
                      {b.hotline && <div className="bp-branch-detail">📞 {b.hotline}</div>}
                    </div>
                    <div className="bp-branch-radio">
                      <div className={`bp-radio ${isSelected ? "checked" : ""}`}>
                        {isSelected && <div className="bp-radio-dot" />}
                      </div>
                      <span className="bp-branch-radio-label">{isSelected ? "Da chon" : "Chon cua hang"}</span>
                    </div>
                  </div>
                );
              })}
            </div>

            <div style={{ marginTop: 20 }}>
              <button className="bp-back-link" onClick={() => goToStep(2)}>← Quay lai chon phu kien</button>
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 4: Thong tin dat hen
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 4) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-wizard-layout">
          <div className="bp-wizard-main">
            <h2 className="bp-wizard-title">Thong tin dat hen</h2>
            <p className="bp-wizard-subtitle">Dien thong tin va chon thoi gian hen</p>

            {error && <div className="alert alert-error">{error}</div>}

            {/* Guest info — only when NOT logged in */}
            {!loggedIn && (
              <div className="booking-card">
                <h3>Thong tin khach hang</h3>
                <div className="bp-guest-fields">
                  <div className="bp-guest-field">
                    <label>Ho va ten</label>
                    <input type="text" placeholder="Nguyen Van A" value={guestName} onChange={(e) => setGuestName(e.target.value)} className="bp-plate-input" />
                  </div>
                  <div className="bp-guest-field">
                    <label>So dien thoai <span className="bp-required">*</span></label>
                    <input type="tel" placeholder="0901234567" value={guestPhone} onChange={(e) => setGuestPhone(e.target.value)} className="bp-plate-input" />
                  </div>
                  <div className="bp-guest-field">
                    <label>Email</label>
                    <input type="email" placeholder="email@example.com" value={guestEmail} onChange={(e) => setGuestEmail(e.target.value)} className="bp-plate-input" />
                  </div>
                </div>
              </div>
            )}

            {/* License plate + Phone */}
            <div className="booking-card" style={{ marginTop: loggedIn ? 0 : 20 }}>
              <h3>Thong tin xe</h3>
              <div className="bp-guest-fields">
                <div className="bp-guest-field">
                  <label>Bien so xe (tuy chon)</label>
                  <input type="text" placeholder="VD: 43A-12345" value={licensePlate} onChange={(e) => setLicensePlate(e.target.value.toUpperCase())} className="bp-plate-input" />
                </div>
                {loggedIn && (
                  <div className="bp-guest-field">
                    <label>So dien thoai (tuy chon)</label>
                    <input type="tel" placeholder="0901234567" value={phone} onChange={(e) => setPhone(e.target.value)} className="bp-plate-input" />
                  </div>
                )}
              </div>
            </div>

            {/* Date + Time picker */}
            <div className="booking-card" style={{ marginTop: 20 }}>
              <h3>Thoi gian dat lich hen</h3>
              <div className="bp-datetime-row">
                <div className="bp-datetime-field">
                  <label>Ngay</label>
                  <input
                    type="date"
                    value={selectedDate}
                    onChange={(e) => setSelectedDate(e.target.value)}
                    min={getTodayStr()}
                    className="bp-date-input"
                  />
                </div>
                <div className="bp-datetime-field">
                  <label>Gio</label>
                  <input
                    type="time"
                    value={selectedTime}
                    onChange={(e) => setSelectedTime(e.target.value)}
                    min="07:00"
                    max="21:00"
                    className="bp-time-input"
                  />
                </div>
              </div>
            </div>

            {/* Notes */}
            <div className="booking-card" style={{ marginTop: 20 }}>
              <h3>Ghi chu</h3>
              <textarea placeholder="Mo ta trieu chung hoac yeu cau dac biet..." value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>

            <div style={{ marginTop: 20 }}>
              <button className="bp-back-link" onClick={() => goToStep(3)}>← Quay lai chon cua hang</button>
            </div>
          </div>
          {sidebar}
        </div>
      </div>
    );
  }

  /* ═══════════════════════════════════════════════════════════════════════
     STEP 5: Hoan thanh
     ═══════════════════════════════════════════════════════════════════════ */
  if (step === 5) {
    return (
      <div className="bp-wizard">
        {progressBar}
        <div className="bp-done">
          <div className="bp-done-icon">✅</div>
          <h2>Dat lich thanh cong!</h2>
          <p>Cam on ban da su dung dich vu cua MyGarage.</p>
          <p>Chung toi se xac nhan lich hen cua ban trong thoi gian som nhat.</p>
          <div className="bp-done-actions">
            <button className="bp-done-btn primary" onClick={() => navigate("/my-appointments")}>Xem lich hen cua toi</button>
            <button className="bp-done-btn secondary" onClick={() => { setStep(0); setSelectedServiceIds([]); setCart({}); setBranchId(""); setSelectedDate(""); setSelectedTime("09:00"); setLicensePlate(""); setNotes(""); }}>
              Dat lich moi
            </button>
          </div>
        </div>
      </div>
    );
  }

  return null;
}
>>>>>>> theirs
