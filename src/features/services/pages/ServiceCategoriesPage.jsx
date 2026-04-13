import { useState, useEffect, useCallback } from "react";
import { servicesApi } from "@features/services/api/servicesApi";
import { Modal, ConfirmDialog } from "@features/admin/components/AdminModals";
import { useSearch } from "@shared/core/context/SearchContext";
import TablePagination from "@shared/components/TablePagination";

const EMPTY_CATEGORY = {
  name: "",
  code: "",
  description: "",
  isActive: true,
  displayOrder: 0,
};

function CategoryForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);

  function handleChange(e) {
    const { name, value, type, checked } = e.target;
    setForm((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : name === "displayOrder" ? Number(value) : value,
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
        <input name="name" value={form.name} onChange={handleChange} required />
      </div>
      <div className="form-row" style={{ gridTemplateColumns: "2fr 1fr" }}>
        <div className="form-group">
          <label>Code danh mục *</label>
          <input name="code" value={form.code} onChange={handleChange} required />
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
      <div className="form-group" style={{ flexDirection: "row", alignItems: "center", gap: "8px" }}>
        <input name="isActive" type="checkbox" checked={form.isActive} onChange={handleChange} id="chk-active" />
        <label htmlFor="chk-active" style={{ marginBottom: 0, cursor: "pointer" }}>
          Đang hoạt động
        </label>
      </div>
      <div className="form-actions">
        <button type="button" className="btn-ghost" onClick={onCancel}>
          Hủy
        </button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export default function ServiceCategoriesPage() {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const { searchQuery } = useSearch();

  const [modal, setModal] = useState(null);
  const [selected, setSelected] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await servicesApi.getServiceCategories();
      setCategories(res || []);
    } catch {
      setError("Không thể tải danh mục.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = (categories || []).filter((c) => (c.name || "").toLowerCase().includes(query));
  const pagedCategories = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [query, pageSize]);

  useEffect(() => {
    const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
    if (page > totalPages) setPage(totalPages);
  }, [filtered.length, page, pageSize]);

  const closeModal = () => {
    setModal(null);
    setSelected(null);
    setFormError("");
  };

  async function handleCreate(form) {
    setSaving(true);
    try {
      await servicesApi.createServiceCategory(form);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Không thể thêm danh mục.");
    } finally {
      setSaving(false);
    }
  }

  async function handleEdit(form) {
    setSaving(true);
    try {
      await servicesApi.updateServiceCategory(selected.id, form);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Không thể cập nhật danh mục.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await servicesApi.deleteServiceCategory(selected.id);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Không thể xóa danh mục.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="management-content">
      <header className="admin-header header-actions-only">
        <button className="btn-primary" onClick={() => setModal("create")}>+ Thêm danh mục</button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} danh mục</span>
      </div>

      {error ? <div className="error" style={{ margin: "0 28px 12px" }}>{error}</div> : null}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>Đang tải...</p></div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Tên danh mục</th>
                <th>Thứ tự</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={5} className="td-center">Không có danh mục phù hợp.</td>
                </tr>
              ) : (
                pagedCategories.map((c) => (
                  <tr key={c.id}>
                    <td className="td-id">{c.code}</td>
                    <td style={{ fontWeight: 500 }}>{c.name}</td>
                    <td>{c.displayOrder}</td>
                    <td>
                      <span className={`badge ${c.isActive ? "badge-success" : "badge-danger"}`}>
                        {c.isActive ? "Hoạt động" : "Tạm dừng"}
                      </span>
                    </td>
                    <td>
                      <div className="row-actions">
                        <button className="btn-icon" onClick={() => { setSelected(c); setModal("edit"); }}>Sửa</button>
                        <button className="btn-icon" onClick={() => { setSelected(c); setModal("delete"); }}>Xóa</button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        )}
      </div>

      <div style={{ display: "flex", justifyContent: "center" }}>
        <TablePagination
          totalItems={filtered.length}
          currentPage={page}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPage(1);
          }}
        />
      </div>

      {modal === "create" && (
        <Modal title="Thêm danh mục mới" onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <CategoryForm initial={EMPTY_CATEGORY} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`Sửa • ${selected.name}`} onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <CategoryForm initial={selected} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "delete" && selected && (
        <Modal title="Xác nhận xóa" onClose={closeModal}>
          <ConfirmDialog
            message={`Xóa danh mục "${selected.name}"? Tất cả dịch vụ thuộc danh mục này sẽ bị ảnh hưởng.`}
            onConfirm={handleDelete}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      )}
    </div>
  );
}
