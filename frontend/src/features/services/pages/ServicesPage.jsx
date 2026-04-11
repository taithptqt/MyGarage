import { useState, useEffect, useCallback } from "react";
import { servicesApi } from "@features/services/api/servicesApi";
import { Modal, ConfirmDialog } from "@features/admin/components/AdminModals";
import { generateAcronym } from "@shared/core/utils/stringUtils";
import { useSearch } from "@shared/core/context/SearchContext";
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
  isActive: true
};

const toNumber = (value, fallback = 0) => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
};

const categoryLabel = (category) =>
  category?.categoryName || category?.name || category?.code || (category?.id ? `Danh má»¥c #${category.id}` : "-");

function ServiceForm({ initial, categories, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(() => ({
    ...initial,
    categoryId: initial?.categoryId != null ? String(initial.categoryId) : "",
    basePrice: initial?.basePrice ?? 0,
    standardMinutes: initial?.standardMinutes ?? 0,
    isActive: initial?.isActive !== false
  }));
  const [codeError, setCodeError] = useState("");

  const isCreateMode = !form?.id;
  const formPrefix = form?.id ? `service-${form.id}` : "service-new";

  function handleChange(event) {
    const { name, value, type, checked } = event.target;

    setForm((prev) => {
      const nextValue = name === "code" ? normalizeStandardServiceCode(value) : value;
      const nextForm = { ...prev, [name]: type === "checkbox" ? checked : nextValue };

      // Tá»± Ä‘á»™ng táº¡o mÃ£ theo tÃªn khi táº¡o má»›i vÃ  ngÆ°á»i dÃ¹ng chÆ°a nháº­p mÃ£ riÃªng.
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
      isActive: form.isActive !== false
    });
  }

  return (
    <form className="admin-form service-form" onSubmit={handleSubmit}>
      <p className="service-form-note">
        Äiá»n thÃ´ng tin dá»‹ch vá»¥ Ä‘á»ƒ Ä‘á»“ng bá»™ cho Ä‘áº·t lá»‹ch vÃ  lá»‡nh sá»­a chá»¯a.
      </p>

      <div className="service-form-grid">
        <div className="form-group">
          <label htmlFor={`${formPrefix}-name`}>TÃªn dá»‹ch vá»¥ *</label>
          <input
            id={`${formPrefix}-name`}
            name="name"
            value={form.name || ""}
            onChange={handleChange}
            placeholder="VÃ­ dá»¥: Thay dáº§u Ä‘á»™ng cÆ¡"
            required
          />
        </div>

        <div className="form-group">
          <label htmlFor={`${formPrefix}-code`}>MÃ£ dá»‹ch vá»¥ *</label>
          <input
            id={`${formPrefix}-code`}
            name="code"
            value={form.code || ""}
            onChange={handleChange}
            placeholder="VÃ­ dá»¥: TDDC"
            required
          />
          <p className="service-form-hint">
            GÃ³i dá»‹ch vá»¥ dÃ¹ng mÃ£ báº¯t Ä‘áº§u báº±ng <strong>PKG-</strong> pháº£i táº¡o á»Ÿ má»¥c Packages.
          </p>
          {codeError ? <p className="service-form-error">{codeError}</p> : null}
          {isCreateMode ? <p className="service-form-hint">MÃ£ Ä‘Æ°á»£c gá»£i Ã½ tá»± Ä‘á»™ng theo tÃªn, báº¡n cÃ³ thá»ƒ sá»­a láº¡i.</p> : null}
        </div>

        <div className="form-group">
          <label htmlFor={`${formPrefix}-price`}>ÄÆ¡n giÃ¡ (VNÄ) *</label>
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
          <label htmlFor={`${formPrefix}-minutes`}>Thá»i gian (phÃºt) *</label>
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
          <label htmlFor={`${formPrefix}-category`}>Danh má»¥c *</label>
          <select
            id={`${formPrefix}-category`}
            name="categoryId"
            value={form.categoryId || ""}
            onChange={handleChange}
            required
          >
            <option value="">-- Chá»n danh má»¥c --</option>
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
            <span className="service-form-status-title">Äang kinh doanh</span>
            <small className="service-form-status-note">
              Dá»‹ch vá»¥ sáº½ hiá»ƒn thá»‹ á»Ÿ giao diá»‡n Ä‘áº·t lá»‹ch vÃ  cÃ³ thá»ƒ Ä‘Æ°á»£c chá»n khi láº­p phiáº¿u.
            </small>
          </label>
        </div>
      </div>

      <div className="form-actions service-form-actions">
        <button type="button" className="btn-ghost" onClick={onCancel}>
          Há»§y
        </button>
        <button type="submit" className="btn-primary" disabled={loading || Boolean(codeError)}>
          {loading ? "Äang lÆ°u..." : "LÆ°u"}
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
      const [serviceResponse, categoryResponse] = await Promise.all([
        servicesApi.getStandardServices(),
        servicesApi.getServiceCategories()
      ]);
      setServices(filterPackageManagedStandardServices(serviceResponse || []));
      setCategories(categoryResponse || []);
    } catch {
      setError("KhÃ´ng thá»ƒ táº£i danh má»¥c dá»‹ch vá»¥.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const query = String(searchQuery || "")
    .trim()
    .toLowerCase();
  const filtered = (services || []).filter((service) =>
    (service.name || "").toLowerCase().includes(query) ||
    (service.code || "").toLowerCase().includes(query)
  );

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
      setFormError(err?.message || "KhÃ´ng thá»ƒ thÃªm dá»‹ch vá»¥.");
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
      setFormError(err?.message || "KhÃ´ng thá»ƒ cáº­p nháº­t dá»‹ch vá»¥.");
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
      setFormError(err?.message || "KhÃ´ng thá»ƒ xÃ³a dá»‹ch vá»¥.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="management-content services-page">
      <header className="admin-header">
        <div>
          <h1>Quáº£n lÃ½ dá»‹ch vá»¥</h1>
          <p className="header-sub">Danh má»¥c cÃ¡c dá»‹ch vá»¥ sá»­a chá»¯a vÃ  báº£o dÆ°á»¡ng</p>
        </div>
        <button className="btn-primary" onClick={() => setModal("create")}>
          + ThÃªm dá»‹ch vá»¥
        </button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} dá»‹ch vá»¥</span>
      </div>

      {error ? <div className="error service-page-error">{error}</div> : null}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Äang táº£i...</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>MÃ£</th>
                <th>TÃªn dá»‹ch vá»¥</th>
                <th>ÄÆ¡n giÃ¡</th>
                <th>Thá»i gian</th>
                <th className="th-center">Danh má»¥c</th>
                <th className="th-center">Tráº¡ng thÃ¡i</th>
                <th>Thao tÃ¡c</th>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={7} className="td-center">
                    KhÃ´ng cÃ³ dá»‹ch vá»¥ phÃ¹ há»£p.
                  </td>
                </tr>
              ) : (
                filtered.map((service) => {
                  const category = categories.find((item) => Number(item.id) === Number(service.categoryId));
                  return (
                    <tr key={service.id}>
                      <td className="td-id">{service.code}</td>
                      <td className="td-name">{service.name}</td>
                      <td>{Number(service.basePrice || 0).toLocaleString("vi-VN")}Ä‘</td>
                      <td>{Number(service.standardMinutes || 0)} phÃºt</td>
                      <td className="td-center">{categoryLabel(category)}</td>
                      <td className="td-center">
                        <span className={`badge ${service.isActive !== false ? "badge-active" : "badge-inactive"}`}>
                          {service.isActive !== false ? "Äang kinh doanh" : "Ngá»«ng kinh doanh"}
                        </span>
                      </td>
                      <td>
                        <div className="row-actions">
                          <button
                            className="btn-icon"
                            onClick={() => {
                              setSelected(service);
                              setModal("edit");
                            }}
                          >
                            Sá»­a
                          </button>
                          <button
                            className="btn-icon"
                            onClick={() => {
                              setSelected(service);
                              setModal("delete");
                            }}
                          >
                            XÃ³a
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

      {modal === "create" && (
        <Modal title="ThÃªm dá»‹ch vá»¥ má»›i" onClose={closeModal} className="service-modal">
          {formError ? <div className="error">{formError}</div> : null}
          <ServiceForm
            initial={EMPTY_SERVICE}
            categories={categories}
            onSubmit={handleCreate}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      )}

      {modal === "edit" && selected ? (
        <Modal title={`Sá»­a dá»‹ch vá»¥ â€¢ ${selected.name}`} onClose={closeModal} className="service-modal">
          {formError ? <div className="error">{formError}</div> : null}
          <ServiceForm
            initial={selected}
            categories={categories}
            onSubmit={handleEdit}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      ) : null}

      {modal === "delete" && selected ? (
        <Modal title="XÃ¡c nháº­n xÃ³a" onClose={closeModal}>
          <ConfirmDialog
            message={`XÃ³a dá»‹ch vá»¥ "${selected.name}"?`}
            onConfirm={handleDelete}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      ) : null}
    </div>
  );
}

