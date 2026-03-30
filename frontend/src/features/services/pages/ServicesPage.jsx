import { useState, useEffect, useCallback } from "react";
import { servicesApi } from "../api/servicesApi";
import { Modal, ConfirmDialog } from "../../admin/components/AdminModals";
import { generateAcronym } from "../../../utils/stringUtils";
import { useSearch } from "@context/SearchContext";

const EMPTY_SERVICE = {
  name: "",
  code: "",
  basePrice: 0,
  standardMinutes: 0,
  categoryId: "",
  isActive: true,
};

function ServiceForm({ initial, categories, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);

  function handleChange(e) {
    const { name, value } = e.target;
    
    setForm((prev) => {
      const newForm = { ...prev, [name]: name === "basePrice" || name === "standardMinutes" ? Number(value) : value };
      
      // Tự động tạo mã nếu đang nhập tên và mã chưa được nhập hoặc khớp với gợi ý cũ
      if (name === "name" && (!prev.id)) { // Chỉ tự động khi tạo mới
        const oldAcronym = generateAcronym(prev.name);
        if (!prev.code || prev.code === oldAcronym) {
          newForm.code = generateAcronym(value);
        }
      }
      
      return newForm;
    });
  }

  function handleSubmit(e) {
    e.preventDefault();
    onSubmit(form);
  }

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Tên dịch vụ *</label>
        <input name="name" value={form.name} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Mã dịch vụ *</label>
        <input name="code" value={form.code} onChange={handleChange} required />
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Đơn giá (VNĐ) *</label>
          <input name="basePrice" type="number" value={form.basePrice} onChange={handleChange} required />
        </div>
        <div className="form-group">
          <label>Thời gian (phút) *</label>
          <input name="standardMinutes" type="number" value={form.standardMinutes} onChange={handleChange} required />
        </div>
      </div>
      <div className="form-group">
        <label>Danh mục</label>
        <select name="categoryId" value={form.categoryId || ""} onChange={handleChange} required>
          <option value="">-- Chọn danh mục --</option>
          {categories.map((cat) => (
            <option key={cat.id} value={cat.id}>{cat.categoryName}</option>
          ))}
        </select>
      </div>
      <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
        <input name="isActive" type="checkbox" checked={form.isActive !== false} onChange={(e) => setForm(prev => ({ ...prev, isActive: e.target.checked }))} id="chk-active-service" />
        <label htmlFor="chk-active-service" style={{ marginBottom: 0, cursor: 'pointer' }}>Đang kinh doanh</label>
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

export default function ServicesPage() {
  const [services, setServices] = useState([]);
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
      const [srvRes, catRes] = await Promise.all([
        servicesApi.getStandardServices(),
        servicesApi.getServiceCategories(),
      ]);
      setServices(srvRes);
      setCategories(catRes);
    } catch (err) {
      setError("Không thể tải danh mục dịch vụ.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = (services || []).filter((s) =>
    (s.name || "").toLowerCase().includes(query) ||
    (s.code || "").toLowerCase().includes(query)
  );

  const closeModal = () => { setModal(null); setSelected(null); setFormError(""); };

  async function handleCreate(form) {
    setSaving(true);
    try {
      await servicesApi.createStandardService(form);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleEdit(form) {
    setSaving(true);
    try {
      await servicesApi.updateStandardService(selected.id, form);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await servicesApi.deleteStandardService(selected.id);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Quản lý dịch vụ</h1>
          <p className="header-sub">Danh mục các dịch vụ sửa chữa và bảo dưỡng</p>
        </div>
        <button className="btn-primary" onClick={() => setModal("create")}>+ Thêm dịch vụ</button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} dịch vụ</span>
      </div>

      <div className="table-wrapper">
        {loading ? (
            <div className="loading-state"><div className="spinner" /><p>Đang tải…</p></div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên dịch vụ</th>
                <th>Đơn giá</th>
                <th>Thời gian</th>
                <th className="th-center">Danh mục</th>
                <th className="th-center">Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((s) => (
                <tr key={s.id}>
                  <td className="td-id">{s.code}</td>
                  <td className="td-name">{s.name}</td>
                  <td>{s.basePrice ? s.basePrice.toLocaleString() : 0}đ</td>
                  <td>{s.standardMinutes} phút</td>
                  <td className="td-center">{categories.find(c => c.id === s.categoryId)?.name || "—"}</td>
                  <td className="td-center">
                    <span className={`badge ${s.isActive !== false ? 'badge-active' : 'badge-inactive'}`}>
                      {s.isActive !== false ? "Kinh doanh" : "Ngừng"}
                    </span>
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => { setSelected(s); setModal("edit"); }}>✏️</button>
                      <button className="btn-icon" onClick={() => { setSelected(s); setModal("delete"); }}>🗑️</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal === "create" && (
        <Modal title="Thêm dịch vụ mới" onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <ServiceForm initial={EMPTY_SERVICE} categories={categories} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`Sửa — ${selected.name}`} onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <ServiceForm initial={selected} categories={categories} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "delete" && selected && (
        <Modal title="Xác nhận xóa" onClose={closeModal}>
          <ConfirmDialog message={`Xóa dịch vụ "${selected.name}"?`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </div>
  );
}
