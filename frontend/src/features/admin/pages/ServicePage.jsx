import { useState, useEffect, useCallback, useMemo } from "react";
import { serviceApi } from "../api/serviceApi";
import { Modal, ConfirmDialog } from "../components/AdminModals";
import { generateAcronym } from "../../../utils/stringUtils";
import { useSearch } from "@context/SearchContext";
import TablePagination from "../../../shared/components/TablePagination";

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
  const [fieldErrors, setFieldErrors] = useState({});

  function handleChange(e) {
    const { name, value } = e.target;
    
    setForm((prev) => {
      const newForm = { ...prev, [name]: name === "basePrice" || name === "standardMinutes" ? Number(value) : value };
      
      if (name === "name" && (!prev.id)) {
        const oldAcronym = generateAcronym(prev.name);
        if (!prev.code || prev.code === oldAcronym) {
          newForm.code = generateAcronym(value);
        }
      }
      
      return newForm;
    });

    if (fieldErrors[name]) {
      setFieldErrors(prev => ({ ...prev, [name]: "" }));
    }
  }

  function handleSubmit(e) {
    e.preventDefault();
    const errors = {};
    if (!form.name.trim()) errors.name = "Tên dịch vụ không được để trống";
    if (!form.code.trim()) errors.code = "Mã dịch vụ không được để trống";
    if (form.basePrice <= 0) errors.basePrice = "Đơn giá phải lớn hơn 0";
    if (form.standardMinutes <= 0) errors.standardMinutes = "Thời gian phải lớn hơn 0";
    if (!form.categoryId) errors.categoryId = "Vui lòng chọn danh mục";

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    onSubmit(form);
  }

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Tên dịch vụ *</label>
        <input name="name" value={form.name} onChange={handleChange} />
        {fieldErrors.name && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.name}</p>}
      </div>
      <div className="form-group">
        <label>Mã dịch vụ *</label>
        <input name="code" value={form.code} onChange={handleChange} />
        {fieldErrors.code && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.code}</p>}
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Đơn giá (VNĐ) *</label>
          <input name="basePrice" type="number" value={form.basePrice} onChange={handleChange} />
          {fieldErrors.basePrice && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.basePrice}</p>}
        </div>
        <div className="form-group">
          <label>Thời gian (phút) *</label>
          <input name="standardMinutes" type="number" value={form.standardMinutes} onChange={handleChange} />
          {fieldErrors.standardMinutes && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.standardMinutes}</p>}
        </div>
      </div>
      <div className="form-group">
        <label>Danh mục</label>
        <select name="categoryId" value={form.categoryId || ""} onChange={handleChange}>
          <option value="">-- Chọn danh mục --</option>
          {categories.map((cat) => (
            <option key={cat.id} value={cat.id}>{cat.categoryName || cat.name}</option>
          ))}
        </select>
        {fieldErrors.categoryId && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px' }}>{fieldErrors.categoryId}</p>}
      </div>
      <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '12px', justifyContent: 'flex-start' }}>
        <label className="premium-switch">
          <input name="isActive" type="checkbox" checked={form.isActive !== false} onChange={(e) => setForm(prev => ({ ...prev, isActive: e.target.checked }))} />
          <span className="switch-slider"></span>
        </label>
        <label style={{ marginBottom: 0, cursor: 'pointer', whiteSpace: 'nowrap', color: '#cbd5e0' }}>Đang kinh doanh</label>
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

export default function ServicePage() {
  const [services, setServices] = useState([]);
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
      const [srvRes, catRes] = await Promise.all([
        serviceApi.getStandardServices(),
        serviceApi.getServiceCategories(),
      ]);
      setServices(srvRes || []);
      setCategories(catRes || []);
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
  const pagedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, services.length]);

  const closeModal = () => { setModal(null); setSelected(null); setFormError(""); };

  async function handleCreate(form) {
    setSaving(true);
    setSuccess("");
    try {
      await serviceApi.createStandardService(form);
      setSuccess("Tạo dịch vụ thành công!");
      await fetchData();
      setTimeout(closeModal, 1500);
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleEdit(form) {
    setSaving(true);
    setSuccess("");
    try {
      await serviceApi.updateStandardService(selected.id, form);
      setSuccess("Cập nhật dịch vụ thành công!");
      await fetchData();
      setTimeout(closeModal, 1500);
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await serviceApi.deleteStandardService(selected.id);
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
              {pagedData.map((s) => (
                <tr key={s.id}>
                  <td className="td-id">{s.code}</td>
                  <td className="td-name">{s.name}</td>
                  <td>{s.basePrice ? s.basePrice.toLocaleString() : 0}đ</td>
                  <td>{s.standardMinutes} phút</td>
                  <td className="td-center">{categories.find(c => c.id === s.categoryId)?.name || categories.find(c => c.id === s.categoryId)?.categoryName || "—"}</td>
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
        <Modal title="Thêm dịch vụ mới" onClose={closeModal}>
          {success && <div className="success-msg" style={{ background: '#eefcf5', color: '#1e7e4a', padding: '10px 15px', borderRadius: '8px', marginBottom: '15px' }}>✅ {success}</div>}
          {formError && <div className="error-msg" style={{ color: '#e74c3c', background: '#fff5f5', padding: '10px 15px', borderRadius: '8px', marginBottom: '15px' }}>❌ {formError}</div>}
          {!success && <ServiceForm initial={EMPTY_SERVICE} categories={categories} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />}
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`Sửa — ${selected.name}`} onClose={closeModal}>
          {success && <div className="success-msg" style={{ background: '#eefcf5', color: '#1e7e4a', padding: '10px 15px', borderRadius: '8px', marginBottom: '15px' }}>✅ {success}</div>}
          {formError && <div className="error-msg" style={{ color: '#e74c3c', background: '#fff5f5', padding: '10px 15px', borderRadius: '8px', marginBottom: '15px' }}>❌ {formError}</div>}
          {!success && <ServiceForm initial={selected} categories={categories} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />}
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
