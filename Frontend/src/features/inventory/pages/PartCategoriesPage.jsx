import { useState, useEffect, useCallback } from "react";
import { inventoryApi } from "@features/inventory/api/inventoryApi";
import { Modal, ConfirmDialog } from "@features/admin/components/AdminModals";
import { useSearch } from "@shared/core/context/SearchContext";

const EMPTY_CATEGORY = {
  categoryName: "",
  description: "",
  isDepositRequired: false,
};

function CategoryForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);

  function handleChange(e) {
    const { name, value, type, checked } = e.target;
    setForm((prev) => ({ 
      ...prev, 
      [name]: type === "checkbox" ? checked : value
    }));
  }

  function handleSubmit(e) {
    e.preventDefault();
    onSubmit(form);
  }

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Tên danh mục *</label>
        <input name="categoryName" value={form.categoryName} onChange={handleChange} required />
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
          {loading ? "Đang lưuệô : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export default function PartCategoriesPage() {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const { searchQuery } = useSearch();

  const [modal, setModal] = useState(null);
  const [selected, setSelected] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

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

  const closeModal = () => { setModal(null); setSelected(null); setFormError(""); };

  async function handleCreate(form) {
    setSaving(true);
    try {
      await inventoryApi.createPartCategory(form);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleEdit(form) {
    setSaving(true);
    try {
      await inventoryApi.updatePartCategory(selected.id, form);
      await fetchData();
      closeModal();
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
          <p className="header-sub">Quản lý các nhóm phụ tùng (Lđp, Phanh, Dầu nhơt, v.v.)</p>
        </div>
        <button className="btn-primary" onClick={() => setModal("create")}>+ Thêm danh mục</button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} danh mục</span>
      </div>

      <div className="table-wrapper">
        {loading ? (
            <div className="loading-state"><div className="spinner" /><p>Đang tải</p></div>
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
              {filtered.map((c) => (
                <tr key={c.id}>
                  <td className="td-id">#{c.id}</td>
                  <td style={{ fontWeight: 500 }}>{c.categoryName}</td>
                  <td>
                    {c.isDepositRequired ? <span className="badge badge-warning">Cần cọc</span> : "Không"}
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => { setSelected(c); setModal("edit"); }}>đođ️</button>
                      <button className="btn-icon" onClick={() => { setSelected(c); setModal("delete"); }}>{"\u{1F5D1}\uFE0F"}</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal === "create" && (
        <Modal title="Thêm danh mục mơi" onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <CategoryForm initial={EMPTY_CATEGORY} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`Sửa ệ" ${selected.categoryName}`} onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <CategoryForm initial={selected} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />
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









