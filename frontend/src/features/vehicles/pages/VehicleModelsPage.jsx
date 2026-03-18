import { useCallback, useEffect, useRef, useState } from "react";
import AdminLayout from "../../../shared/components/AdminLayout";
import ConfirmDialog from "../../../shared/components/ConfirmDialog";
import { brandApi, FUEL_LABELS, FUEL_TYPES, modelApi, SEGMENTS } from "../api/vehicleApi";

// ─── Model Form Modal ─────────────────────────────────────────────────────────
function ModelModal({ model, brands, onClose, onSaved }) {
  const isEdit = !!model?.id;
  const [form, setForm] = useState({
    brandId:          model?.brandId          ?? "",
    modelName:        model?.modelName        ?? "",
    segment:          model?.segment          ?? "",
    fuelType:         model?.fuelType         ?? "",
    cylinderCapacity: model?.cylinderCapacity ?? "",
  });
  const [errors, setErrors]   = useState({});
  const [loading, setLoading] = useState(false);
  const [apiError, setApiError] = useState("");
  const firstRef = useRef(null);

  useEffect(() => { firstRef.current?.focus(); }, []);

  function set(key, value) {
    setForm((prev) => ({ ...prev, [key]: value }));
    setErrors((prev) => ({ ...prev, [key]: "" }));
  }

  function validate() {
    const e = {};
    if (!form.brandId)        e.brandId   = "Vui lòng chọn hãng xe.";
    if (!form.modelName.trim()) e.modelName = "Tên dòng xe không được để trống.";
    return e;
  }

  async function handleSubmit(e) {
    e.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }

    setLoading(true);
    setApiError("");
    try {
      const payload = {
        brandId:          Number(form.brandId),
        modelName:        form.modelName.trim(),
        segment:          form.segment || null,
        fuelType:         form.fuelType || null,
        cylinderCapacity: form.cylinderCapacity.trim() || null,
      };

      const { ok, data } = isEdit
        ? await modelApi.update(model.id, payload)
        : await modelApi.create(payload);

      if (!ok) { setApiError(data?.message || data?.title || "Lưu thất bại."); return; }
      onSaved();
    } catch {
      setApiError("Không kết nối được server.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <h3>{isEdit ? "✏️ Sửa dòng xe" : "➕ Thêm dòng xe"}</h3>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            {apiError && <div className="alert alert-error">⚠️ {apiError}</div>}

            <div className="form-grid">
              {/* Brand */}
              <div className={`field ${errors.brandId ? "error" : ""}`}>
                <label>Hãng xe <span style={{ color: "#e53e3e" }}>*</span></label>
                <select
                  ref={firstRef}
                  value={form.brandId}
                  onChange={(e) => set("brandId", e.target.value)}
                >
                  <option value="">— Chọn hãng xe —</option>
                  {brands.filter((b) => b.isActive).map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
                {errors.brandId && <span className="field-error">{errors.brandId}</span>}
              </div>

              {/* Model Name */}
              <div className={`field ${errors.modelName ? "error" : ""}`}>
                <label>Tên dòng xe <span style={{ color: "#e53e3e" }}>*</span></label>
                <input
                  value={form.modelName}
                  onChange={(e) => set("modelName", e.target.value)}
                  placeholder="VD: Camry, Civic, Fortuner..."
                  maxLength={150}
                />
                {errors.modelName && <span className="field-error">{errors.modelName}</span>}
              </div>

              <div className="form-grid form-grid-2">
                {/* Segment */}
                <div className="field">
                  <label>Phân khúc</label>
                  <select value={form.segment} onChange={(e) => set("segment", e.target.value)}>
                    <option value="">— Chọn phân khúc —</option>
                    {SEGMENTS.map((s) => (
                      <option key={s} value={s}>{s}</option>
                    ))}
                  </select>
                </div>

                {/* Fuel Type */}
                <div className="field">
                  <label>Loại nhiên liệu</label>
                  <select value={form.fuelType} onChange={(e) => set("fuelType", e.target.value)}>
                    <option value="">— Chọn nhiên liệu —</option>
                    {FUEL_TYPES.map((f) => (
                      <option key={f} value={f}>{FUEL_LABELS[f]}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Cylinder Capacity */}
              <div className="field">
                <label>Dung tích xi-lanh</label>
                <input
                  value={form.cylinderCapacity}
                  onChange={(e) => set("cylinderCapacity", e.target.value)}
                  placeholder="VD: 2.0L, 1600cc, Electric..."
                  maxLength={50}
                />
                <span className="field-hint">Nhập tự do — VD: 2.5L, 1800cc.</span>
              </div>
            </div>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={loading}>
              Hủy
            </button>
            <button type="submit" className="btn btn-primary" disabled={loading}>
              {loading ? "Đang lưu..." : isEdit ? "Cập nhật" : "Tạo mới"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────────────────
export default function VehicleModelsPage() {
  const [models, setModels]           = useState([]);
  const [brands, setBrands]           = useState([]);
  const [loading, setLoading]         = useState(true);
  const [error, setError]             = useState("");
  const [search, setSearch]           = useState("");
  const [filterBrand, setFilterBrand] = useState("all");
  const [filterFuel, setFilterFuel]   = useState("all");
  const [modal, setModal]             = useState(null);
  const [confirm, setConfirm]         = useState(null);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [mRes, bRes] = await Promise.all([modelApi.getAll(), brandApi.getAll()]);
      if (!mRes.ok) { setError("Không tải được danh sách dòng xe."); return; }
      if (!bRes.ok) { setError("Không tải được danh sách hãng xe."); return; }
      setModels(mRes.data);
      setBrands(bRes.data);
    } catch {
      setError("Không kết nối được server.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const brandMap = Object.fromEntries(brands.map((b) => [b.id, b]));

  const filtered = models.filter((m) => {
    const bName = brandMap[m.brandId]?.name ?? "";
    const matchSearch =
      m.modelName?.toLowerCase().includes(search.toLowerCase()) ||
      bName.toLowerCase().includes(search.toLowerCase()) ||
      m.segment?.toLowerCase().includes(search.toLowerCase());
    const matchBrand = filterBrand === "all" || String(m.brandId) === filterBrand;
    const matchFuel  = filterFuel  === "all" || m.fuelType === filterFuel;
    return matchSearch && matchBrand && matchFuel;
  });

  async function handleDelete() {
    if (!confirm) return;
    setDeleteLoading(true);
    try {
      const { ok } = await modelApi.remove(confirm.id);
      if (!ok) { setError("Xóa thất bại."); return; }
      setConfirm(null);
      load();
    } catch {
      setError("Không kết nối được server.");
    } finally {
      setDeleteLoading(false);
    }
  }

  return (
    <AdminLayout title="Dòng xe">
      {/* Header */}
      <div className="page-header">
        <div>
          <h2>🚗 Quản lý Dòng xe</h2>
          <p>Danh sách các dòng xe theo từng hãng</p>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => setModal({ mode: "create", model: null })}
        >
          ＋ Thêm dòng xe
        </button>
      </div>

      {error && <div className="alert alert-error">⚠️ {error}</div>}

      {/* Toolbar */}
      <div className="toolbar">
        <div className="toolbar-search">
          <span className="search-icon">🔍</span>
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên, hãng, phân khúc..."
          />
        </div>

        <select value={filterBrand} onChange={(e) => setFilterBrand(e.target.value)}>
          <option value="all">Tất cả hãng</option>
          {brands.map((b) => (
            <option key={b.id} value={String(b.id)}>{b.name}</option>
          ))}
        </select>

        <select value={filterFuel} onChange={(e) => setFilterFuel(e.target.value)}>
          <option value="all">Tất cả nhiên liệu</option>
          {FUEL_TYPES.map((f) => (
            <option key={f} value={f}>{FUEL_LABELS[f]}</option>
          ))}
        </select>
      </div>

      {/* Table */}
      <div className="table-card">
        {loading ? (
          <div className="spinner-wrap"><div className="spinner" /></div>
        ) : filtered.length === 0 ? (
          <div className="empty-state">
            <p>🚗 {search ? "Không tìm thấy kết quả phù hợp." : "Chưa có dòng xe nào."}</p>
          </div>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Hãng xe</th>
                    <th>Tên dòng xe</th>
                    <th>Phân khúc</th>
                    <th>Nhiên liệu</th>
                    <th>Xi-lanh</th>
                    <th style={{ textAlign: "right" }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((m) => {
                    const brand = brandMap[m.brandId];
                    return (
                      <tr key={m.id}>
                        <td>
                          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                            {brand?.logoUrl ? (
                              <img src={brand.logoUrl} alt={brand.name} className="brand-logo"
                                style={{ width: 24, height: 24 }}
                                onError={(e) => (e.target.style.display = "none")} />
                            ) : null}
                            <span style={{ fontWeight: 600, color: "#2f80ed" }}>
                              {brand?.name ?? <span style={{ color: "#9fb3c8" }}>—</span>}
                            </span>
                          </div>
                        </td>
                        <td style={{ fontWeight: 600, color: "#102a43" }}>{m.modelName}</td>
                        <td>
                          {m.segment
                            ? <span className="badge badge-blue">{m.segment}</span>
                            : <span style={{ color: "#9fb3c8" }}>—</span>}
                        </td>
                        <td>
                          {m.fuelType
                            ? <span className={`badge ${fuelBadge(m.fuelType)}`}>
                                {FUEL_LABELS[m.fuelType] ?? m.fuelType}
                              </span>
                            : <span style={{ color: "#9fb3c8" }}>—</span>}
                        </td>
                        <td>{m.cylinderCapacity || <span style={{ color: "#9fb3c8" }}>—</span>}</td>
                        <td>
                          <div className="cell-actions">
                            <button
                              className="btn btn-secondary btn-icon btn-sm"
                              title="Chỉnh sửa"
                              onClick={() => setModal({ mode: "edit", model: m })}
                            >
                              ✏️
                            </button>
                            <button
                              className="btn btn-danger btn-icon btn-sm"
                              title="Xóa"
                              onClick={() => setConfirm({ id: m.id, name: m.modelName })}
                            >
                              🗑️
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="table-meta">
              Hiển thị {filtered.length} / {models.length} dòng xe
            </div>
          </>
        )}
      </div>

      {/* Model Modal */}
      {modal && (
        <ModelModal
          model={modal.model}
          brands={brands}
          onClose={() => setModal(null)}
          onSaved={() => { setModal(null); load(); }}
        />
      )}

      {/* Delete Confirm */}
      {confirm && (
        <ConfirmDialog
          message={`Bạn có chắc muốn xóa dòng xe "${confirm.name}" không?`}
          loading={deleteLoading}
          onConfirm={handleDelete}
          onCancel={() => setConfirm(null)}
        />
      )}
    </AdminLayout>
  );
}

function fuelBadge(fuelType) {
  return { ELECTRIC: "badge-green", HYBRID: "badge-blue", DIESEL: "badge-orange", GASOLINE: "badge-gray" }[fuelType] ?? "badge-gray";
}
