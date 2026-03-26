import { useState, useEffect, useCallback, useMemo } from "react";
import { inventoryApi } from "../api/inventoryApi";
import { Modal, ConfirmDialog } from "../components/AdminModals";
import { useSearch } from "../../../context/SearchContext";
import TablePagination from "../../../shared/components/TablePagination";

const EMPTY_CATEGORY = {
  categoryName: "",
  description: "",
  isDepositRequired: false,
};

function CategoryForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);
  const [fieldErrors, setFieldErrors] = useState({});

  function handleChange(e) {
    const { name, value, type, checked } = e.target;
    setForm((prev) => ({ 
      ...prev, 
      [name]: type === "checkbox" ? checked : value
    }));
    if (fieldErrors[name]) {
      setFieldErrors(prev => ({ ...prev, [name]: "" }));
    }
  }

  function handleSubmit(e) {
    e.preventDefault();
    const errors = {};
    if (!form.categoryName.trim()) errors.categoryName = "Tên danh mục không được để trống";
    
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
        <input name="categoryName" value={form.categoryName} onChange={handleChange} />
        {fieldErrors.categoryName && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.categoryName}</p>}
      </div>
      <div className="form-group">
        <label>Mô tả</label>
        <textarea name="description" value={form.description} onChange={handleChange} rows={3} />
      </div>
      <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
        <input name="isDepositRequired" type="checkbox" checked={form.isDepositRequired} onChange={handleChange} id="chk-deposit" />
        <label htmlFor="chk-deposit" style={{ marginBottom: 0, cursor: 'pointer' }}>Yêu cầu đặt cọc</label>
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

export default function InventoryCategoryPage() {
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
      const res = await inventoryApi.getPartCategories();
      setCategories(res);
    } catch (err) {
      setError("Không thể tải danh mục.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = (categories || []).filter((c) =>
    (c.categoryName || "").toLowerCase().includes(query)
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
      await inventoryApi.createPartCategory(form);
      setSuccess("Tạo danh mục thành công!");
      await fetchData();
      setTimeout(closeModal, 1500);
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleEdit(form) {
    setSaving(true);
    setSuccess("");
    try {
      await inventoryApi.updatePartCategory(selected.id, form);
      setSuccess("Cập nhật danh mục thành công!");
      await fetchData();
      setTimeout(closeModal, 1500);
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await inventoryApi.deletePartCategory(selected.id);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Danh mục phụ tùng</h1>
          <p className="header-sub">Quản lý các nhóm phụ tùng (Lốp, Phanh, Dầu nhớt, v.v.)</p>
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
                <th>ID</th>
                <th>Tên danh mục</th>
                <th>Đặt cọc</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pagedData.map((c) => (
                <tr key={c.id}>
                  <td className="td-id">#{c.id}</td>
                  <td style={{ fontWeight: 500 }}>{c.categoryName}</td>
                  <td>
                    {c.isDepositRequired ? <span className="badge badge-warning">Cần cọc</span> : "Không"}
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
        <Modal title={`Sửa — ${selected.categoryName}`} onClose={closeModal}>
          {success && <div className="admin-alert success"><span className="icon">✓</span> {success}</div>}
          {formError && <div className="admin-alert error"><span className="icon">⚠</span> {formError}</div>}
          {!success && <CategoryForm initial={selected} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />}
        </Modal>
      )}

      {modal === "delete" && selected && (
        <Modal title="Xác nhận xóa" onClose={closeModal}>
          <ConfirmDialog message={`Xóa danh mục "${selected.categoryName}"? Tất cả phụ tùng thuộc danh mục này sẽ bị ảnh hưởng.`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </div>
  );
}
