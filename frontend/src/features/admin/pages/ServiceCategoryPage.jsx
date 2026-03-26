import { useState, useEffect, useCallback, useMemo } from "react";
import { serviceApi } from "../api/serviceApi";
import { Modal, ConfirmDialog } from "../components/AdminModals";
import { useSearch } from "../../../context/SearchContext";
import TablePagination from "../../../shared/components/TablePagination";

const EMPTY_CATEGORY = {
  name: "",
  code: "",
  description: "",
  isActive: true,
  displayOrder: 0,
};

function CategoryForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);
  const [fieldErrors, setFieldErrors] = useState({});

  function handleChange(e) {
    const { name, value, type, checked } = e.target;
    setForm((prev) => ({ 
      ...prev, 
      [name]: type === "checkbox" ? checked : (name === "displayOrder" ? Number(value) : value)
    }));
    if (fieldErrors[name]) {
      setFieldErrors(prev => ({ ...prev, [name]: "" }));
    }
  }

  function handleSubmit(e) {
    e.preventDefault();
    const errors = {};
    if (!form.name.trim()) errors.name = "Tên danh mục không được để trống";
    if (!form.code.trim()) errors.code = "Mã danh mục không được để trống";
    
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    onSubmit(form);
  }

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Tên danh mục *</label>
        <input name="name" value={form.name} onChange={handleChange} />
        {fieldErrors.name && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.name}</p>}
      </div>
      <div className="form-row" style={{ gridTemplateColumns: '2fr 1fr' }}>
        <div className="form-group">
          <label>Mã danh mục *</label>
          <input name="code" value={form.code} onChange={handleChange} />
          {fieldErrors.code && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.code}</p>}
        </div>
        <div className="form-group">
          <label>Thứ tự hiển thị</label>
          <input name="displayOrder" type="number" value={form.displayOrder} onChange={handleChange} />
        </div>
      </div>
      <div className="form-group">
        <label>Mô tả</label>
        <textarea name="description" value={form.description} onChange={handleChange} rows={3} />
      </div>
      <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '12px', justifyContent: 'flex-start' }}>
        <label className="premium-switch">
          <input name="isActive" type="checkbox" checked={form.isActive} onChange={handleChange} />
          <span className="switch-slider"></span>
        </label>
        <label style={{ marginBottom: 0, cursor: 'pointer', whiteSpace: 'nowrap', color: '#cbd5e0' }}>Đang hoạt động</label>
      </div>
      <div className="form-actions">
        <button type="button" className="btn-ghost" onClick={onCancel}>Hủy</button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "Đang lưu…" : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export default function ServiceCategoryPage() {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const { searchQuery } = useSearch();

  const [modal, setModal] = useState(null);
  const [selected, setSelected] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await serviceApi.getServiceCategories();
      setCategories(res || []);
    } catch (err) {
      setError("Không thể tải danh mục.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = (categories || []).filter((c) =>
    (c.name || "").toLowerCase().includes(query)
  );
  const pagedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, categories.length]);

  const closeModal = () => { setModal(null); setSelected(null); setFormError(""); setSuccess(""); };

  async function handleCreate(form) {
    setSaving(true);
    setSuccess("");
    try {
      await serviceApi.createServiceCategory(form);
      setSuccess("Tạo danh mục thành công!");
      await fetchData();
      setTimeout(closeModal, 1500);
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleEdit(form) {
    setSaving(true);
    setSuccess("");
    try {
      await serviceApi.updateServiceCategory(selected.id, form);
      setSuccess("Cập nhật danh mục thành công!");
      await fetchData();
      setTimeout(closeModal, 1500);
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await serviceApi.deleteServiceCategory(selected.id);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Danh mục dịch vụ</h1>
          <p className="header-sub">Quản lý các nhóm dịch vụ (Bảo dưỡng, Sửa chữa, v.v.)</p>
        </div>
        <button className="btn-primary" onClick={() => setModal("create")}>+ Thêm danh mục</button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} danh mục</span>
      </div>

      <div className="table-wrapper">
        {loading ? (
            <div className="loading-state"><div className="spinner" /><p>Đang tải…</p></div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên danh mục</th>
                <th>Thứ tự</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pagedData.map((c) => (
                <tr key={c.id}>
                  <td className="td-id">{c.code}</td>
                  <td style={{ fontWeight: 500 }}>{c.name}</td>
                  <td>{c.displayOrder}</td>
                  <td>
                    <span className={`badge ${c.isActive ? 'badge-success' : 'badge-danger'}`}>
                      {c.isActive ? "Hoạt động" : "Tạm dừng"}
                    </span>
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => { setSelected(c); setModal("edit"); }}>✏️</button>
                      <button className="btn-icon" onClick={() => { setSelected(c); setModal("delete"); }}>🗑️</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <TablePagination
        totalItems={filtered.length}
        currentPage={currentPage}
        pageSize={pageSize}
        onPageChange={setCurrentPage}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />

      {modal === "create" && (
        <Modal title="Thêm danh mục mới" onClose={closeModal}>
          {success && <div className="admin-alert success"><span className="icon">✓</span> {success}</div>}
          {formError && <div className="admin-alert error"><span className="icon">⚠</span> {formError}</div>}
          {!success && <CategoryForm initial={EMPTY_CATEGORY} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />}
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`Sửa — ${selected.name}`} onClose={closeModal}>
          {success && <div className="admin-alert success"><span className="icon">✓</span> {success}</div>}
          {formError && <div className="admin-alert error"><span className="icon">⚠</span> {formError}</div>}
          {!success && <CategoryForm initial={selected} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />}
        </Modal>
      )}

      {modal === "delete" && selected && (
        <Modal title="Xác nhận xóa" onClose={closeModal}>
          <ConfirmDialog message={`Xóa danh mục "${selected.name}"? Tất cả dịch vụ thuộc danh mục này sẽ bị ảnh hưởng.`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </div>
  );
}
