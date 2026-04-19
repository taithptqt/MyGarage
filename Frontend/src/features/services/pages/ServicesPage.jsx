import { useState, useEffect, useCallback } from "react";
import { servicesApi } from "@features/services/api/servicesApi";
import { Modal, ConfirmDialog } from "@features/admin/components/AdminModals";
import { generateAcronym } from "@shared/core/utils/stringUtils";
import { useSearch } from "@shared/core/context/SearchContext";
import TablePagination from "@shared/components/TablePagination";
import {
  filterPackageManagedStandardServices,
  getPackageManagedStandardServiceMessage,
  isPackageManagedStandardServiceCode,
  normalizeStandardServiceCode,
} from "@shared/utils/standardServiceCodeRules";
import "./ServicesPage.css";

const EMPTY_SERVICE = {
  name: "",
  code: "",
  basePrice: 0,
  standardMinutes: 0,
  categoryId: "",
  isActive: true,
};

const toNumber = (value, fallback = 0) => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
};

const categoryLabel = (category) =>
  category?.categoryName || category?.name || category?.code || (category?.id ? `Danh mục #${category.id}` : "-");

function ServiceForm({ initial, categories, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(() => ({
    ...initial,
    categoryId: initial?.categoryId != null ? String(initial.categoryId) : "",
    basePrice: initial?.basePrice ?? 0,
    standardMinutes: initial?.standardMinutes ?? 0,
    isActive: initial?.isActive !== false,
  }));
  const [codeError, setCodeError] = useState("");

  const isCreateMode = !form?.id;
  const formPrefix = form?.id ? `service-${form.id}` : "service-new";

  function handleChange(event) {
    const { name, value, type, checked } = event.target;

    setForm((prev) => {
      const nextValue = name === "code" ? normalizeStandardServiceCode(value) : value;
      const nextForm = { ...prev, [name]: type === "checkbox" ? checked : nextValue };

      if (name === "name" && !prev.id) {
        const oldAcronym = generateAcronym(prev.name || "");
        if (!prev.code || prev.code === oldAcronym) {
          nextForm.code = generateAcronym(value);
        }
      }

      return nextForm;
    });

    if (name === "code") {
      setCodeError(isPackageManagedStandardServiceCode(value) ? getPackageManagedStandardServiceMessage() : "");
    }
  }

  function handleSubmit(event) {
    event.preventDefault();
    const normalizedCode = normalizeStandardServiceCode(form.code);
    if (isPackageManagedStandardServiceCode(normalizedCode)) {
      setCodeError(getPackageManagedStandardServiceMessage());
      return;
    }

    onSubmit({
      ...form,
      code: normalizedCode,
      categoryId: form.categoryId ? Number(form.categoryId) : null,
      basePrice: toNumber(form.basePrice, 0),
      standardMinutes: toNumber(form.standardMinutes, 0),
      isActive: form.isActive !== false,
    });
  }

  return (
    <form className="admin-form service-form" onSubmit={handleSubmit}>
      <p className="service-form-note">Điền thông tin dịch vụ để đồng bộ cho đặt lịch và lệnh sửa chữa.</p>

      <div className="service-form-grid">
        <div className="form-group">
          <label htmlFor={`${formPrefix}-name`}>Tên dịch vụ *</label>
          <input
            id={`${formPrefix}-name`}
            name="name"
            value={form.name || ""}
            onChange={handleChange}
            placeholder="Ví dụ: Thay dầu động cơ"
            required
          />
        </div>

        <div className="form-group">
          <label htmlFor={`${formPrefix}-code`}>Code dịch vụ *</label>
          <input
            id={`${formPrefix}-code`}
            name="code"
            value={form.code || ""}
            onChange={handleChange}
            placeholder="Ví dụ: TDDC"
            required
          />
          <p className="service-form-hint">
            Gói dịch vụ dùng code bắt đầu bằng <strong>PKG-</strong> phải tạo ở mục Packages.
          </p>
          {codeError ? <p className="service-form-error">{codeError}</p> : null}
          {isCreateMode ? <p className="service-form-hint">Code được gợi ý tự động theo tên, bạn có thể sửa lại.</p> : null}
        </div>

        <div className="form-group">
          <label htmlFor={`${formPrefix}-price`}>Đơn gi� (VN�) *</label>
          <input
            id={`${formPrefix}-price`}
            name="basePrice"
            type="number"
            min="0"
            step="1000"
            value={form.basePrice ?? 0}
            onChange={handleChange}
            required
          />
        </div>

        <div className="form-group">
          <label htmlFor={`${formPrefix}-minutes`}>Thời gian (phút) *</label>
          <input
            id={`${formPrefix}-minutes`}
            name="standardMinutes"
            type="number"
            min="0"
            value={form.standardMinutes ?? 0}
            onChange={handleChange}
            required
          />
        </div>

        <div className="form-group service-form-group--full">
          <label htmlFor={`${formPrefix}-category`}>Danh mục *</label>
          <select
            id={`${formPrefix}-category`}
            name="categoryId"
            value={form.categoryId || ""}
            onChange={handleChange}
            required
          >
            <option value="">-- Chọn danh mục --</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {categoryLabel(category)}
              </option>
            ))}
          </select>
        </div>

        <div className="service-form-group--full service-form-status">
          <input
            id={`${formPrefix}-active`}
            name="isActive"
            type="checkbox"
            checked={form.isActive !== false}
            onChange={handleChange}
          />
          <label htmlFor={`${formPrefix}-active`} className="service-form-status-copy">
            <span className="service-form-status-title">Đang kinh doanh</span>
            <small className="service-form-status-note">
              Dịch vụ sẽ hiển thị ở giao diện đặt lịch và có thể được chọn khi lập phiếu.
            </small>
          </label>
        </div>
      </div>

      <div className="form-actions service-form-actions">
        <button type="button" className="btn-ghost" onClick={onCancel}>
          Hủy
        </button>
        <button type="submit" className="btn-primary" disabled={loading || Boolean(codeError)}>
          {loading ? "Đang lưu..." : "Lưu"}
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
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [serviceResponse, categoryResponse] = await Promise.all([
        servicesApi.getStandardServices(),
        servicesApi.getServiceCategories(),
      ]);
      setServices(filterPackageManagedStandardServices(serviceResponse || []));
      setCategories(categoryResponse || []);
    } catch {
      setError("Không thể tải danh mục dịch vụ.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = (services || []).filter(
    (service) => (service.name || "").toLowerCase().includes(query) || (service.code || "").toLowerCase().includes(query)
  );
  const pagedServices = filtered.slice((page - 1) * pageSize, page * pageSize);

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
      await servicesApi.createStandardService(form);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Không thể thêm dịch vụ.");
    } finally {
      setSaving(false);
    }
  }

  async function handleEdit(form) {
    setSaving(true);
    try {
      await servicesApi.updateStandardService(selected.id, form);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Không thể cập nhật dịch vụ.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await servicesApi.deleteStandardService(selected.id);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err?.response?.data?.message || err?.message || "Không thể xóa dịch vụ.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="management-content services-page">
      <header className="admin-header header-actions-only">
        <button className="btn-primary" onClick={() => setModal("create")}>
          + Thêm dịch vụ
        </button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} dịch vụ</span>
      </div>

      {error ? <div className="error service-page-error">{error}</div> : null}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Đang tải...</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Tên dịch vụ</th>
                <th>Đơn gi�</th>
                <th>Thời gian</th>
                <th className="th-center">Danh mục</th>
                <th className="th-center">Trạng thái</th>
                <th className="th-center">Thao t�c</th>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={7} className="td-center">
                    Không có dịch vụ phù hợp.
                  </td>
                </tr>
              ) : (
                pagedServices.map((service) => {
                  const category = categories.find((item) => Number(item.id) === Number(service.categoryId));
                  return (
                    <tr key={service.id}>
                      <td className="td-id">{service.code}</td>
                      <td className="td-name">{service.name}</td>
                      <td>{Number(service.basePrice || 0).toLocaleString("vi-VN")}đ</td>
                      <td>{Number(service.standardMinutes || 0)} phút</td>
                      <td className="td-center">{categoryLabel(category)}</td>
                      <td className="td-center">
                        <span className={`badge ${service.isActive !== false ? "badge-active" : "badge-inactive"}`}>
                          {service.isActive !== false ? "Đang kinh doanh" : "Ngừng kinh doanh"}
                        </span>
                      </td>
                      <td className="td-center">
                        <div className="row-actions">
                          <button
                            className="btn-icon btn-edit"
                            onClick={() => {
                              setSelected(service);
                              setModal("edit");
                            }}
                          >
                            Sửa
                          </button>
                          <button
                            className="btn-icon btn-del"
                            onClick={() => {
                              setSelected(service);
                              setModal("delete");
                            }}
                          >
                            Xóa
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })
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
        <Modal title="Thêm dịch vụ mới" onClose={closeModal} className="service-modal">
          {formError ? <div className="error">{formError}</div> : null}
          <ServiceForm initial={EMPTY_SERVICE} categories={categories} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "edit" && selected ? (
        <Modal title={`Sửa dịch vụ • ${selected.name}`} onClose={closeModal} className="service-modal">
          {formError ? <div className="error">{formError}</div> : null}
          <ServiceForm initial={selected} categories={categories} onSubmit={handleEdit} onCancel={closeModal} loading={saving} />
        </Modal>
      ) : null}

      {modal === "delete" && selected ? (
        <Modal title="Xác nhận x�a" onClose={closeModal}>
          <ConfirmDialog
            message={`Xóa dịch vụ "${selected.name}"?`}
            onConfirm={handleDelete}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      ) : null}
    </div>
  );
}

