import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import { isAuthenticated, getUser } from "../../../app/authState";
import SlotPicker from "../components/SlotPicker";
import {
  getAllBranches,
  getAllStandardServices,
  getCustomerVehicles,
  getAllVehicles,
  getAvailableSlots,
  bookAppointment,
} from "../api/appointmentApi";

function formatVND(n) {
  return new Intl.NumberFormat("vi-VN").format(n) + " d";
}

function dateToString(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export default function BookingPage() {
  const navigate = useNavigate();
  const user = getUser();

  const [branches, setBranches] = useState([]);
  const [services, setServices] = useState([]);
  const [vehicles, setVehicles] = useState([]);

  const [branchId, setBranchId] = useState("");
  const [selectedDate, setSelectedDate] = useState(null);
  const [slots, setSlots] = useState([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [selectedServiceIds, setSelectedServiceIds] = useState([]);
  const [vehicleId, setVehicleId] = useState("");
  const [notes, setNotes] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  useEffect(() => {
    if (!isAuthenticated()) {
      navigate("/", { replace: true });
      return;
    }
    loadReferenceData();
  }, []);

  async function loadReferenceData() {
    try {
      const [br, svc, cv, veh] = await Promise.all([
        getAllBranches(),
        getAllStandardServices(),
        getCustomerVehicles(),
        getAllVehicles(),
      ]);
      setBranches(Array.isArray(br) ? br.filter((b) => b.status === "active") : []);
      setServices(Array.isArray(svc) ? svc.filter((s) => s.isActive !== false) : []);

      const cvList = Array.isArray(cv) ? cv : [];
      const vehList = Array.isArray(veh) ? veh : [];
      const customerId = user?.id;
      const myVehicleIds = cvList
        .filter((x) => x.customerId == customerId && x.isCurrentOwner)
        .map((x) => x.vehicleId);
      const myVehicles = vehList.filter((v) => myVehicleIds.includes(v.id));
      setVehicles(myVehicles);
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

    if (!branchId) return setError("Vui long chon chi nhanh.");
    if (!selectedDate) return setError("Vui long chon ngay.");
    if (!selectedSlot) return setError("Vui long chon khung gio.");

    setSubmitting(true);
    try {
      await bookAppointment({
        branchId: Number(branchId),
        vehicleId: vehicleId ? Number(vehicleId) : null,
        scheduledDate: dateToString(selectedDate),
        startTime: selectedSlot.startTime,
        endTime: selectedSlot.endTime,
        requestedServiceIds: selectedServiceIds.length
          ? selectedServiceIds.join(",")
          : null,
        customerNotes: notes || null,
      });
      setSuccess("Dat lich thanh cong! Vui long cho xac nhan tu Garage.");
      setTimeout(() => navigate("/my-appointments"), 2000);
    } catch (err) {
      setError(err.message || "Dat lich that bai.");
    } finally {
      setSubmitting(false);
    }
  }

  const selectedBranch = branches.find((b) => String(b.id) === String(branchId));
  const totalPrice = services
    .filter((s) => selectedServiceIds.includes(s.id))
    .reduce((sum, s) => sum + (s.basePrice || 0), 0);
  const totalMinutes = services
    .filter((s) => selectedServiceIds.includes(s.id))
    .reduce((sum, s) => sum + (s.standardMinutes || 0), 0);

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 24 }}>
        Dat lich hen sua chua
      </h1>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      <div className="booking-container">
        <div className="booking-sidebar">
          {/* Branch selector */}
          <div className="booking-card">
            <h3>1. Chon chi nhanh</h3>
            <select value={branchId} onChange={handleBranchChange}>
              <option value="">-- Chon chi nhanh --</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name} {b.address ? `- ${b.address}` : ""}
                </option>
              ))}
            </select>
          </div>

          {/* Calendar */}
          <div className="booking-card booking-calendar">
            <h3>2. Chon ngay</h3>
            <Calendar
              onChange={handleDateChange}
              value={selectedDate}
              minDate={new Date()}
              locale="vi-VN"
            />
          </div>

          {/* Vehicle */}
          <div className="booking-card">
            <h3>5. Chon xe (tuy chon)</h3>
            <select value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
              <option value="">-- Khong chon xe --</option>
              {vehicles.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.licensePlate} {v.color ? `(${v.color})` : ""}
                </option>
              ))}
            </select>
          </div>

          {/* Notes */}
          <div className="booking-card">
            <h3>6. Ghi chu</h3>
            <textarea
              placeholder="Mo ta trieu chung hoac yeu cau dac biet..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
            />
          </div>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
          {/* Slot picker */}
          <div className="booking-card">
            <h3>3. Chon khung gio</h3>
            {!branchId || !selectedDate ? (
              <div className="empty-state">
                <p>Vui long chon chi nhanh va ngay truoc.</p>
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
            <h3>4. Chon dich vu (tuy chon)</h3>
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
                      {s.standardMinutes} phut
                    </div>
                  </div>
                  <div className="service-price">{formatVND(s.basePrice)}</div>
                </div>
              ))}
            </div>
          </div>

          {/* Summary & submit */}
          <div className="booking-card">
            <h3>Tom tat</h3>
            <div className="booking-summary">
              <dl>
                <dt>Chi nhanh</dt>
                <dd>{selectedBranch?.name || "Chua chon"}</dd>
                <dt>Ngay</dt>
                <dd>
                  {selectedDate
                    ? selectedDate.toLocaleDateString("vi-VN")
                    : "Chua chon"}
                </dd>
                <dt>Gio</dt>
                <dd>
                  {selectedSlot
                    ? `${selectedSlot.startTime?.substring(0, 5)} - ${selectedSlot.endTime?.substring(0, 5)}`
                    : "Chua chon"}
                </dd>
                <dt>Dich vu</dt>
                <dd>
                  {selectedServiceIds.length > 0
                    ? `${selectedServiceIds.length} dich vu`
                    : "Khong"}
                </dd>
                {totalPrice > 0 && (
                  <>
                    <dt>Tam tinh</dt>
                    <dd style={{ fontWeight: 600, color: "#422afb" }}>
                      {formatVND(totalPrice)}
                    </dd>
                    <dt>Thoi gian du kien</dt>
                    <dd>{totalMinutes} phut</dd>
                  </>
                )}
              </dl>
            </div>
            <button
              className="btn-book"
              onClick={handleSubmit}
              disabled={submitting || !branchId || !selectedDate || !selectedSlot}
            >
              {submitting ? "Dang dat lich..." : "Xac nhan dat lich"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
