import { useState, useEffect, useMemo } from "react";
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
      const startTime = selectedTime + ":00";
      const endH = Math.min(parseInt(selectedTime.split(":")[0]) + Math.ceil(totalMinutes / 60) || 1, 22);
      const endTime = String(endH).padStart(2, "0") + ":00:00";

      const payload = {
        branchId: Number(branchId),
        vehicleId: null,
        vehicleModelId: realModelId,
        licensePlate: licensePlate.trim() || null,
        color: null,
        scheduledDate: selectedDate,
        startTime,
        endTime,
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
  if (step === 0) {
    return (
      <div className="bp-landing">
        <div className="bp-hero">
          <div className="bp-hero-content">
            <h1>Dat Lich Sua Chua & Bao Duong</h1>
            <p>He thong dat lich truc tuyen - Nhanh chong, tien loi, chuyen nghiep</p>
          </div>
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
