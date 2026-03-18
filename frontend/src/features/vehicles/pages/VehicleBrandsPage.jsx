import { useCallback, useEffect, useRef, useState } from "react";
import AdminLayout from "../../../shared/components/AdminLayout";
import ConfirmDialog from "../../../shared/components/ConfirmDialog";
import { brandApi } from "../api/vehicleApi";

// ─── Brand Form Modal ─────────────────────────────────────────────────────────
function BrandModal({ brand, onClose, onSaved }) {
  const isEdit = !!brand?.id;
  const [form, setForm] = useState({
    code:            brand?.code            ?? "",
    name:            brand?.name            ?? "",
    logoUrl:         brand?.logoUrl         ?? "",
    countryOfOrigin: brand?.countryOfOrigin ?? "",
    isActive:        brand?.isActive        ?? true,
  });
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);
  const [apiError, setApiError] = useState("");
  const firstInputRef = useRef(null);

  useEffect(() => { firstInputRef.current?.focus(); }, []);

  function set(key, value) {
    setForm((prev) => ({ ...prev, [key]: value }));
    setErrors((prev) => ({ ...prev, [key]: "" }));
  }

  function validate() {
    const e = {};
    if (!form.code.trim())  e.code  = "Mã hãng xe không được để trống.";
    if (!form.name.trim())  e.name  = "Tên hãng xe không được để trống.";
    return e;
  }

  async function handleSubmit(e) {
    e.preventDefault();
    const e2 = validate();
    if (Object.keys(e2).length) { setErrors(e2); return; }

    setLoading(true);
    setApiError("");
    try {
      const payload = {
        code: form.code.trim().toUpperCase(),
        name: form.name.trim(),
        logoUrl: form.logoUrl.trim() || null,
        countryOfOrigin: form.countryOfOrigin.trim() || null,
        isActive: form.isActive,
      };

      const { ok, data } = isEdit
        ? await brandApi.update(brand.id, payload)
        : await brandApi.create(payload);

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
          <h3>{isEdit ? "✏️ Sửa hãng xe" : "➕ Thêm hãng xe"}</h3>
          <button className="modal-close" onClick={onClose}>✕</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            {apiError && <div className="alert alert-error">⚠️ {apiError}</div>}

            <div className="form-grid">
              {/* Code */}
              <div className={`field ${errors.code ? "error" : ""}`}>
                <label>Mã hãng <span style={{ color: "#e53e3e" }}>*</span></label>
                <input
                  ref={firstInputRef}
                  value={form.code}
                  onChange={(e) => set("code", e.target.value)}
                  placeholder="VD: TOYOTA"
                  maxLength={50}
                />
                {errors.code && <span className="field-error">{errors.code}</span>}
                <span className="field-hint">Mã viết hoa, không dấu, không trùng.</span>
              </div>

              {/* Name */}
              <div className={`field ${errors.name ? "error" : ""}`}>
                <label>Tên hãng <span style={{ color: "#e53e3e" }}>*</span></label>
                <input
                  value={form.name}
                  onChange={(e) => set("name", e.target.value)}
                  placeholder="VD: Toyota"
                  maxLength={100}
                />
                {errors.name && <span className="field-error">{errors.name}</span>}
              </div>

              {/* Country */}
              <div className="field">
                <label>Quốc gia sản xuất</label>
                <input
                  value={form.countryOfOrigin}
                  onChange={(e) => set("countryOfOrigin", e.target.value)}
                  placeholder="VD: Nhật Bản"
                  maxLength={100}
                />
              </div>

              {/* Logo URL */}
              <div className="field">
                <label>URL Logo</label>
                <input
                  value={form.logoUrl}
                  onChange={(e) => set("logoUrl", e.target.value)}
                  placeholder="https://..."
                  type="url"
                />
                <span className="field-hint">Link ảnh logo của hãng.</span>
              </div>

              {/* Logo preview */}
              {form.logoUrl && (
                <div style={{ textAlign: "center" }}>
                  <img
                    src={form.logoUrl}
                    alt="Logo"
                    className="brand-logo"
                    style={{ width: 64, height: 64 }}
                    onError={(e) => (e.target.style.display = "none")}
                  />
                </div>
              )}

              {/* Active toggle */}
              <div className="toggle-row">
                <label>Đang hoạt động</label>
                <label className="toggle">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => set("isActive", e.target.checked)}
                  />
                  <span className="toggle-track" />
                </label>
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
export default function VehicleBrandsPage() {
  const [brands, setBrands]           = useState([]);
  const [loading, setLoading]         = useState(true);
  const [error, setError]             = useState("");
  const [search, setSearch]           = useState("");
  const [filterActive, setFilterActive] = useState("all");
  const [modal, setModal]             = useState(null); // null | { mode: "create" | "edit", brand }
  const [confirm, setConfirm]         = useState(null); // null | { id, name }
  const [deleteLoading, setDeleteLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const { ok, data } = await brandApi.getAll();
      if (!ok) { setError("Không tải được danh sách hãng xe."); return; }
      setBrands(data);
    } catch {
      setError("Không kết nối được server.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = brands.filter((b) => {
    const matchSearch =
      b.name?.toLowerCase().includes(search.toLowerCase()) ||
      b.code?.toLowerCase().includes(search.toLowerCase()) ||
      b.countryOfOrigin?.toLowerCase().includes(search.toLowerCase());
    const matchActive =
      filterActive === "all" ||
      (filterActive === "active" && b.isActive) ||
      (filterActive === "inactive" && !b.isActive);
    return matchSearch && matchActive;
  });

  async function handleDelete() {
    if (!confirm) return;
    setDeleteLoading(true);
    try {
      const { ok } = await brandApi.remove(confirm.id);
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
    <AdminLayout title="Hãng xe">
      {/* Header */}
      <div className="page-header">
        <div>
          <h2>🏭 Quản lý Hãng xe</h2>
          <p>Danh sách tất cả hãng xe trong hệ thống</p>
        </div>
        <button className="btn btn-primary" onClick={() => setModal({ mode: "create", brand: null })}>
          ＋ Thêm hãng xe
        </button>
      </div>

      {/* Error */}
      {error && <div className="alert alert-error">⚠️ {error}</div>}

      {/* Toolbar */}
      <div className="toolbar">
        <div className="toolbar-search">
          <span className="search-icon">🔍</span>
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên, mã, quốc gia..."
          />
        </div>
        <select value={filterActive} onChange={(e) => setFilterActive(e.target.value)}>
          <option value="all">Tất cả trạng thái</option>
          <option value="active">Đang hoạt động</option>
          <option value="inactive">Ngừng hoạt động</option>
        </select>
      </div>

      {/* Table */}
      <div className="table-card">
        {loading ? (
          <div className="spinner-wrap"><div className="spinner" /></div>
        ) : filtered.length === 0 ? (
          <div className="empty-state">
            <p>🏭 {search ? "Không tìm thấy kết quả phù hợp." : "Chưa có hãng xe nào."}</p>
          </div>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Logo</th>
                    <th>Mã hãng</th>
                    <th>Tên hãng</th>
                    <th>Quốc gia</th>
                    <th>Trạng thái</th>
                    <th style={{ textAlign: "right" }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((b) => (
                    <tr key={b.id}>
                      <td>
                        {b.logoUrl ? (
                          <img src={b.logoUrl} alt={b.name} className="brand-logo"
                            onError={(e) => { e.target.style.display = "none"; }} />
                        ) : (
                          <div className="brand-logo-placeholder">🏭</div>
                        )}
                      </td>
                      <td>
                        <code style={{ background: "#f0f4f8", padding: "2px 8px", borderRadius: 4, fontSize: 12 }}>
                          {b.code}
                        </code>
                      </td>
                      <td style={{ fontWeight: 600, color: "#102a43" }}>{b.name}</td>
                      <td>{b.countryOfOrigin || <span style={{ color: "#9fb3c8" }}>—</span>}</td>
                      <td>
                        <span className={`badge ${b.isActive ? "badge-green" : "badge-gray"}`}>
                          {b.isActive ? "✓ Hoạt động" : "✗ Ngừng"}
                        </span>
                      </td>
                      <td>
                        <div className="cell-actions">
                          <button
                            className="btn btn-secondary btn-icon btn-sm"
                            title="Chỉnh sửa"
                            onClick={() => setModal({ mode: "edit", brand: b })}
                          >
                            ✏️
                          </button>
                          <button
                            className="btn btn-danger btn-icon btn-sm"
                            title="Xóa"
                            onClick={() => setConfirm({ id: b.id, name: b.name })}
                          >
                            🗑️
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="table-meta">
              Hiển thị {filtered.length} / {brands.length} hãng xe
            </div>
          </>
        )}
      </div>

      {/* Brand Modal */}
      {modal && (
        <BrandModal
          brand={modal.brand}
          onClose={() => setModal(null)}
          onSaved={() => { setModal(null); load(); }}
        />
      )}

      {/* Delete Confirm */}
      {confirm && (
        <ConfirmDialog
          message={`Bạn có chắc muốn xóa hãng xe "${confirm.name}" không?`}
          loading={deleteLoading}
          onConfirm={handleDelete}
          onCancel={() => setConfirm(null)}
        />
      )}
    </AdminLayout>
  );
}
