import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
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
      setError(e?.message || "Không t?i ??c danh sách xe.");
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

  const stats = useMemo(
    () => [
      { label: "Tổng số xe", value: list.length },
      { label: "Đang là chủ sở hữu", value: list.filter((item) => item.isCurrentOwner).length },
      { label: "Đã khai b�o VIN", value: list.filter((item) => item.vin).length },
    ],
    [list]
  );

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
              <p className="cp-eyebrow">Garage của tôi</p>
              <h1 className="cp-title">Đội xe của tôi</h1>
              <p className="cp-desc">
                Quản lý biển số, model, VIN và thông tin cơ bản của từng xe để theo dõi dịch vụ
                nhanh hơn trong customer portal.
              </p>

              <div className="cp-veh-actions">
                <button type="button" className="cp-btn" onClick={openAdd}>
                  Thêm xe
                </button>
                <Link className="cp-btn cp-btn-ghost" to="/my-garage-dashboard">
                  Theo dõi xe
                </Link>
              </div>
            </div>

            <div className="cp-veh-summary">
              {stats.map((stat) => (
                <div key={stat.label} className="cp-stat-card">
                  <span className="cp-stat-label">{stat.label}</span>
                  <strong className="cp-stat-value">{stat.value}</strong>
                </div>
              ))}
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
                <button type="button" className="cp-btn" onClick={openAdd}>
                  Khai b�o xe d?u ti�n
                </button>
              </div>
            </section>
          ) : null}

          {!loading && list.length > 0 ? (
            <section className="cp-grid-2 cp-veh-grid">
              {list.map((vehicle) => (
                <article key={vehicle.vehicleId} className="cp-card cp-card--primary cp-veh-card">
                  <div className="cp-veh-card-head">
                    <div>
                      <h2 className="cp-veh-plate">{vehicle.licensePlate}</h2>
                      <p className="cp-veh-model">
                        {[vehicle.brandName, vehicle.modelName].filter(Boolean).join(" ") || "Đang cập nhật model"}
                      </p>
                    </div>
                    <span className={`cp-chip ${vehicle.isCurrentOwner ? "cp-chip--success" : "cp-chip--soft"}`}>
                      {vehicle.isCurrentOwner ? "Chủ sở hữu hiện tại" : "Lịch sử sở hữu"}
                    </span>
                  </div>

                  <dl className="cp-veh-dl">
                    <div className="cp-veh-meta-item cp-veh-meta-item--wide">
                      <dt>VIN</dt>
                      <dd>{vehicle.vin || "—"}</dd>
                    </div>
                    <div className="cp-veh-meta-item">
                      <dt>Màu xe</dt>
                      <dd>{vehicle.color || "—"}</dd>
                    </div>
                    <div className="cp-veh-meta-item">
                      <dt>Năm sản xuất</dt>
                      <dd>{vehicle.manufactureYear || "—"}</dd>
                    </div>
                    <div className="cp-veh-meta-item">
                      <dt>ODO hiện tại</dt>
                      <dd>{vehicle.odo != null ? `${vehicle.odo.toLocaleString("vi-VN")} km` : "—"}</dd>
                    </div>
                  </dl>

                  <div className="cp-veh-card-actions">
                    <Link className="cp-btn" to={`/my-vehicles/${vehicle.vehicleId}`}>
                      Xem chi tiết
                    </Link>
                    <button
                      type="button"
                      className="cp-btn cp-btn-ghost"
                      onClick={() => openEdit(vehicle)}
                    >
                      Sửa thông tin
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


