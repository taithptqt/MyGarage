import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  MdDirectionsCar,
  MdCheckCircle,
  MdFingerprint,
  MdColorLens,
  MdCalendarToday,
  MdSpeed,
  MdAdd,
  MdArrowForward,
  MdEdit,
  MdCarRepair,
} from "react-icons/md";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import {
  createCustomerVehicle,
  getCustomerVehicles,
  getVehicleLookups,
  updateCustomerVehicle,
} from "@features/customerPortal/api/customerPortalApi";
import "./CustomerPortalShell.css";
import "./MyVehiclesPage.css";

const emptyForm = {
  licensePlate: "",
  vin: "",
  modelId: "",
  color: "",
  manufactureYear: "",
  odo: "",
};

export default function MyVehiclesPage() {
  const [list, setList] = useState([]);
  const [lookups, setLookups] = useState({ brands: [], models: [] });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [brandFilter, setBrandFilter] = useState("");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const [vehicles, lu] = await Promise.all([getCustomerVehicles(), getVehicleLookups()]);
      setList(Array.isArray(vehicles) ? vehicles : []);
      setLookups(lu || { brands: [], models: [] });
    } catch (e) {
      setError(e?.message || "Không tải được danh sách xe.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const modelsForBrand = useMemo(() => {
    const models = lookups.models || [];
    if (!brandFilter) return models;
    const brandId = Number(brandFilter);
    if (!Number.isFinite(brandId)) return models;
    return models.filter((model) => model.brandId === brandId);
  }, [brandFilter, lookups.models]);

  const openAdd = () => {
    setBrandFilter("");
    setForm(emptyForm);
    setModal("add");
  };

  const openEdit = (vehicle) => {
    const selectedModel = (lookups.models || []).find((model) => model.id === vehicle.modelId);
    setBrandFilter(selectedModel ? String(selectedModel.brandId) : "");
    setForm({
      licensePlate: vehicle.licensePlate || "",
      vin: vehicle.vin || "",
      modelId: vehicle.modelId ? String(vehicle.modelId) : "",
      color: vehicle.color || "",
      manufactureYear: vehicle.manufactureYear != null ? String(vehicle.manufactureYear) : "",
      odo: vehicle.odo != null ? String(vehicle.odo) : "",
    });
    setModal({ mode: "edit", vehicleId: vehicle.vehicleId });
  };

  const closeModal = () => {
    if (saving) return;
    setModal(null);
  };

  const submit = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      const payload = {
        licensePlate: form.licensePlate.trim(),
        vin: form.vin?.trim() || null,
        modelId: Number(form.modelId),
        color: form.color?.trim() || null,
        manufactureYear: form.manufactureYear ? Number(form.manufactureYear) : null,
        odo: form.odo ? Number(form.odo) : null,
      };

      if (!payload.modelId || !Number.isFinite(payload.modelId)) {
        throw new Error("Vui lòng chọn dòng xe.");
      }

      if (modal?.mode === "edit" && modal.vehicleId) {
        await updateCustomerVehicle(modal.vehicleId, payload);
      } else {
        await createCustomerVehicle(payload);
      }

      setModal(null);
      await load();
    } catch (err) {
      setError(err?.message || "Không lưu được.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-veh-hero">
            <div className="cp-veh-hero-copy">
              <h1 className="cp-title">Đội xe của tôi</h1>
              <p className="cp-desc">
                Quản lý thông tin xe, số khung VIN và chỉ số ODO để đặt hẹn và theo dõi tiến độ sửa chữa chuẩn xác.
              </p>
            </div>

            <div className="cp-veh-actions">
              <button type="button" className="cp-btn cp-btn-primary" onClick={openAdd}>
                <MdAdd className="cp-btn-icon" /> Thêm xe
              </button>
              <Link className="cp-btn cp-btn-ghost" to="/my-garage-dashboard">
                <MdDirectionsCar className="cp-btn-icon" /> Theo dõi xe đang sửa
              </Link>
            </div>
          </section>

          {error ? <div className="cp-error">{error}</div> : null}
          {loading ? <p className="cp-muted">Đang tải…</p> : null}

          {!loading && list.length === 0 ? (
            <section className="cp-card cp-empty-state">
              <h2 className="cp-empty-title">Bạn chưa có xe nào trong portal</h2>
              <p className="cp-muted">
                Thêm xe để xem lịch sử sửa chữa, hóa đơn và nhận nhắc bảo dưỡng theo đúng dữ liệu
                đang có trong hệ thống.
              </p>
              <div className="cp-veh-actions">
                <button type="button" className="cp-btn cp-btn-primary" onClick={openAdd}>
                  <MdAdd className="cp-btn-icon" /> Khai báo xe đầu tiên
                </button>
              </div>
            </section>
          ) : null}

          {!loading && list.length > 0 ? (
            <section className="cp-grid-2 cp-veh-grid">
              {list.map((vehicle) => (
                <article key={vehicle.vehicleId} className="cp-card cp-card--primary cp-veh-card">
                  <div className="cp-veh-card-head">
                    <div className="cp-veh-identity">
                      <div className="cp-license-plate-badge">
                        <span className="cp-plate-screw" />
                        <span className="cp-plate-number">{vehicle.licensePlate}</span>
                        <span className="cp-plate-screw" />
                      </div>
                      <h2 className="cp-veh-model">
                        {[vehicle.brandName, vehicle.modelName].filter(Boolean).join(" ") || "Đang cập nhật model"}
                      </h2>
                    </div>
                    <div className="cp-veh-head-actions">
                      <Link
                        className="cp-btn cp-btn-accent cp-btn-sm"
                        to="/booking/form"
                      >
                        <MdCarRepair className="cp-btn-icon" /> Đặt lịch bảo dưỡng
                      </Link>
                    </div>
                  </div>

                  <div className="cp-veh-specs-grid">
                    <div className="cp-veh-spec-item">
                      <span className="cp-veh-spec-label">
                        <MdFingerprint className="cp-meta-icon" /> SỐ KHUNG (VIN)
                      </span>
                      <span className={`cp-veh-spec-val ${vehicle.vin ? "cp-mono-vin" : "cp-veh-spec-empty"}`}>
                        {vehicle.vin || "Chưa khai báo"}
                      </span>
                    </div>
                    <div className="cp-veh-spec-item">
                      <span className="cp-veh-spec-label">
                        <MdColorLens className="cp-meta-icon" /> MÀU XE
                      </span>
                      <span className="cp-veh-spec-val">{vehicle.color || "—"}</span>
                    </div>
                    <div className="cp-veh-spec-item">
                      <span className="cp-veh-spec-label">
                        <MdCalendarToday className="cp-meta-icon" /> NĂM SẢN XUẤT
                      </span>
                      <span className="cp-veh-spec-val">{vehicle.manufactureYear || "—"}</span>
                    </div>
                    <div className="cp-veh-spec-item">
                      <span className="cp-veh-spec-label">
                        <MdSpeed className="cp-meta-icon" /> ODO HIỆN TẠI
                      </span>
                      <span className="cp-veh-spec-val cp-odo-val">
                        {vehicle.odo != null ? `${vehicle.odo.toLocaleString("vi-VN")} km` : "—"}
                      </span>
                    </div>
                  </div>

                  <div className="cp-veh-card-footer">
                    <Link className="cp-btn cp-btn-primary cp-btn-sm" to={`/my-vehicles/${vehicle.vehicleId}`}>
                      Xem chi tiết <MdArrowForward className="cp-btn-icon-right" />
                    </Link>
                    <button
                      type="button"
                      className="cp-btn cp-btn-ghost cp-btn-sm"
                      onClick={() => openEdit(vehicle)}
                    >
                      <MdEdit className="cp-btn-icon" /> Sửa thông tin
                    </button>
                  </div>
                </article>
              ))}
            </section>
          ) : null}
        </div>
      </div>

      {modal ? (
        <div className="cp-modal-backdrop" role="presentation" onClick={closeModal}>
          <div
            className="cp-modal cp-veh-modal"
            role="dialog"
            aria-modal="true"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="cp-modal-head">
              <div>
                <p className="cp-eyebrow">Đội xe</p>
                <h2 className="cp-modal-title">
                  {modal.mode === "edit" ? "Cập nhật thông tin xe" : "Thêm xe mới"}
                </h2>
              </div>
              <p className="cp-muted cp-modal-note">
                Biển số và VIN sẽ được chuẩn hóa theo cùng rule với màn admin.
              </p>
            </div>

            <form onSubmit={submit} className="cp-modal-form">
              <label className="cp-field cp-field--uppercase">
                <span>Biển số *</span>
                <input
                  required
                  maxLength={32}
                  autoCapitalize="characters"
                  autoCorrect="off"
                  spellCheck={false}
                  placeholder="Ví dụ: 30A-12345"
                  value={form.licensePlate}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      licensePlate: event.target.value.toUpperCase(),
                    }))
                  }
                />
              </label>

              <label className="cp-field">
                <span>Hãng (lọc model)</span>
                <select
                  value={brandFilter}
                  onChange={(event) => {
                    setBrandFilter(event.target.value);
                    setForm((current) => ({ ...current, modelId: "" }));
                  }}
                >
                  <option value="">— Tất cả —</option>
                  {(lookups.brands || []).map((brand) => (
                    <option key={brand.id} value={String(brand.id)}>
                      {brand.name}
                    </option>
                  ))}
                </select>
              </label>

              <label className="cp-field">
                <span>Dòng xe *</span>
                <select
                  required
                  value={form.modelId}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, modelId: event.target.value }))
                  }
                >
                  <option value="">Chọn model</option>
                  {modelsForBrand.map((model) => (
                    <option key={model.id} value={String(model.id)}>
                      {model.brandName} {model.modelName}
                    </option>
                  ))}
                </select>
              </label>

              <label className="cp-field cp-field--uppercase">
                <span>VIN</span>
                <input
                  maxLength={17}
                  autoCapitalize="characters"
                  autoCorrect="off"
                  spellCheck={false}
                  placeholder="17 ký tự"
                  value={form.vin}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, vin: event.target.value.toUpperCase() }))
                  }
                />
              </label>

              <label className="cp-field">
                <span>Màu xe</span>
                <input
                  maxLength={64}
                  placeholder="Ví dụ: Trắng ngọc trai"
                  value={form.color}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, color: event.target.value }))
                  }
                />
              </label>

              <div className="cp-field-row">
                <label className="cp-field">
                  <span>Năm sản xuất</span>
                  <input
                    type="number"
                    min={1900}
                    max={2100}
                    value={form.manufactureYear}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        manufactureYear: event.target.value,
                      }))
                    }
                  />
                </label>

                <label className="cp-field">
                  <span>ODO (km)</span>
                  <input
                    type="number"
                    min={0}
                    value={form.odo}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, odo: event.target.value }))
                    }
                  />
                </label>
              </div>

              <div className="cp-modal-footer">
                <button
                  type="button"
                  className="cp-btn cp-btn-ghost"
                  onClick={closeModal}
                  disabled={saving}
                >
                  Hủy
                </button>
                <button type="submit" className="cp-btn" disabled={saving}>
                  {saving ? "Đang lưu…" : "Lưu thông tin xe"}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      <Footer />
    </div>
  );
}


