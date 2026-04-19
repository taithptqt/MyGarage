import { useState, useEffect, useCallback, useMemo } from "react";
import { inventoryApi } from "@features/inventory/api/inventoryApi";
import { Modal, ConfirmDialog } from "@features/admin/components/AdminModals";
import { useSearch } from "@shared/core/context/SearchContext";
import { generateSlug } from "@shared/core/utils/stringUtils";

const EMPTY_PART = {
  name: "",
  sku: "",
  unit: "Cái",
  retailPrice: 0,
  costPrice: 0,
  servicePrice: 0,
  taxRate: 0,
  warrantyMonths: 0,
  isActive: true,
  categoryId: "",
};

function PartForm({ initial, categories, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);
  const [imageFile, setImageFile] = useState(null);
  const [previewUrl, setPreviewUrl] = useState(initial?.imageUrl || "");

  useEffect(() => {
    setForm(initial);
    setImageFile(null);
    setPreviewUrl(initial?.imageUrl || "");
  }, [initial]);

  function handleChange(e) {
    const { name, value, type, checked } = e.target;
    
    setForm((prev) => {
      const isNumeric = ["retailPrice", "costPrice", "servicePrice", "taxRate", "warrantyMonths"].includes(name);
      const isCheckbox = type === "checkbox";
      const newForm = { 
        ...prev, 
        [name]: isCheckbox ? checked : (isNumeric ? Number(value) : value) 
      };
      
      // T? d?ng t?o m� n?u dang nh?p t�n v� m� chua du?c nh?p ho?c kh?p v?i g?i � cu
      if (name === "name" && (!prev.id)) { // Ch? t? d?ng khi t?o m?i
        const oldSlug = generateSlug(prev.name);
        if (!prev.sku || prev.sku === oldSlug) {
          newForm.sku = generateSlug(value);
        }
      }
      
      return newForm;
    });
  }

  function handleImageChange(e) {
    const file = e.target.files?.[0] || null;
    setImageFile(file);

    if (!file) {
      setPreviewUrl(form?.imageUrl || "");
      return;
    }

    if (previewUrl?.startsWith("blob:")) {
      URL.revokeObjectURL(previewUrl);
    }
    setPreviewUrl(URL.createObjectURL(file));
  }

  function handleSubmit(e) {
    e.preventDefault();
    onSubmit({ ...form, imageFile });
  }

  useEffect(() => () => {
    if (previewUrl?.startsWith("blob:")) {
      URL.revokeObjectURL(previewUrl);
    }
  }, [previewUrl]);

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>T�n vật tư/phụ tùng *</label>
        <input name="name" value={form.name} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>M� vật tư (SKU) *</label>
        <input name="sku" value={form.sku} onChange={handleChange} required />
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Đơn v? t�nh *</label>
          <input name="unit" value={form.unit} onChange={handleChange} required />
        </div>
        <div className="form-group">
          <label>Gi? v?n (VN?)</label>
          <input name="costPrice" type="number" value={form.costPrice} onChange={handleChange} />
        </div>
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Gi? b?n l? (VN?) *</label>
          <input name="retailPrice" type="number" value={form.retailPrice} onChange={handleChange} required />
        </div>
        <div className="form-group">
          <label>Gi? dịch vụ (VN?)</label>
          <input name="servicePrice" type="number" value={form.servicePrice} onChange={handleChange} />
        </div>
      </div>
      <div className="form-row">
        <div className="form-group">
          <label>Thu? (%)</label>
          <input name="taxRate" type="number" value={form.taxRate} onChange={handleChange} />
        </div>
        <div className="form-group">
          <label>B?o h?nh (th?ng)</label>
          <input name="warrantyMonths" type="number" value={form.warrantyMonths} onChange={handleChange} />
        </div>
      </div>
      <div className="form-group">
        <label>Danh m?c</label>
        <select name="categoryId" value={form.categoryId || ""} onChange={handleChange} required>
          <option value="">-- Chọn danh m?c --</option>
          {categories.map((cat) => (
            <option key={cat.id} value={cat.id}>{cat.categoryName}</option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>?nh s?n ph?m</label>
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp"
          onChange={handleImageChange}
        />
        {previewUrl ? (
          <img
            src={previewUrl}
            alt="Xem tru?c ?nh s?n ph?m"
            style={{ marginTop: 8, width: "100%", maxWidth: 220, borderRadius: 8, objectFit: "cover" }}
          />
        ) : null}
      </div>
      <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
        <input name="isActive" type="checkbox" checked={form.isActive} onChange={handleChange} id="chk-active-part" />
        <label htmlFor="chk-active-part" style={{ marginBottom: 0, cursor: 'pointer' }}>?ang kinh doanh</label>
      </div>
      <div className="form-actions">
        <button type="button" className="btn-ghost" onClick={onCancel}>H?y</button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "?ang luu?" : "Luu"}
        </button>
      </div>
    </form>
  );
}

export default function PartsPage() {
  const { searchQuery } = useSearch();
  const [products, setProducts] = useState([]);
  const [productImages, setProductImages] = useState([]);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [modal, setModal] = useState(null);
  const [selected, setSelected] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [partsRes, catRes, imagesRes] = await Promise.all([
        inventoryApi.getProducts(),
        inventoryApi.getPartCategories(),
        inventoryApi.getProductImages(),
      ]);
      setProducts(partsRes);
      setCategories(catRes);
      setProductImages(Array.isArray(imagesRes) ? imagesRes : []);
    } catch (err) {
      setError("Không th? t?i danh m?c phụ tùng.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const imageByProductId = useMemo(() => {
    const grouped = new Map();
    (productImages || []).forEach((item) => {
      const productId = item?.productId;
      if (productId == null) return;

      const current = grouped.get(productId);
      if (!current) {
        grouped.set(productId, item);
        return;
      }

      const currentMain = current?.isMain === true ? 1 : 0;
      const incomingMain = item?.isMain === true ? 1 : 0;
      if (incomingMain > currentMain) {
        grouped.set(productId, item);
        return;
      }

      const currentSort = Number.isFinite(Number(current?.sortOrder)) ? Number(current.sortOrder) : Number.MAX_SAFE_INTEGER;
      const incomingSort = Number.isFinite(Number(item?.sortOrder)) ? Number(item.sortOrder) : Number.MAX_SAFE_INTEGER;
      if (incomingSort < currentSort) {
        grouped.set(productId, item);
      }
    });
    return grouped;
  }, [productImages]);

  const query = String(searchQuery || "").trim().toLowerCase();
  const filtered = (products || []).filter((p) =>
    (p.name || "").toLowerCase().includes(query) ||
    (p.sku || "").toLowerCase().includes(query)
  );

  const closeModal = () => { setModal(null); setSelected(null); setFormError(""); };

  async function handleCreate(form) {
    setSaving(true);
    try {
      const { imageFile, ...payload } = form;
      const created = await inventoryApi.createProduct(payload);
      if (imageFile && created?.id) {
        await inventoryApi.uploadProductImage(created.id, imageFile);
      }
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleEdit(form) {
    setSaving(true);
    try {
      const { imageFile, ...payload } = form;
      await inventoryApi.updateProduct(selected.id, payload);
      if (imageFile) {
        await inventoryApi.uploadProductImage(selected.id, imageFile);
      }
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await inventoryApi.deleteProduct(selected.id);
      await fetchData();
      closeModal();
    } catch (err) { setFormError(err.message); } finally { setSaving(false); }
  }

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Quản lý phụ tùng & vật tư</h1>
          <p className="header-sub">Danh m?c c?c lo?i linh ki?n, ph? ki?n trong kho</p>
        </div>
        <button className="btn-primary" onClick={() => setModal("create")}>+ Th�m vật tư</button>
      </header>

      <div className="table-toolbar">
        <span className="result-count">{filtered.length} k?t qu?</span>
      </div>

      <div className="table-wrapper">
        {loading ? (
            <div className="loading-state"><div className="spinner" /><p>Đang t?i...</p></div>
        ) : (
          <table className="admin-table">
            <thead>
                <tr>
                  <th>M?</th>
                  <th>T�n vật tư</th>
                  <th>?on v?</th>
                  <th>Gi? v?n</th>
                  <th>Gi? l?</th>
                  <th>Gi? dịch vụ</th>
                  <th>B?o h�nh</th>
                  <th className="th-center">Danh m?c</th>
                  <th className="th-center">Tr?ng th?i</th>
                  <th>Thao tác</th>
                </tr>
            </thead>
            <tbody>
              {filtered.map((p) => (
                <tr key={p.id}>
                  <td className="td-id">{p.sku}</td>
                  <td className="td-name">{p.name}</td>
                  <td className="td-center"><span className="badge badge-info">{p.unit}</span></td>
                  <td style={{ color: '#94a3b8' }}>{p.costPrice ? p.costPrice.toLocaleString() : 0}d</td>
                  <td style={{ fontWeight: 600, color: '#60a5fa' }}>{p.retailPrice ? p.retailPrice.toLocaleString() : 0}d</td>
                  <td>{p.servicePrice ? p.servicePrice.toLocaleString() : 0}d</td>
                  <td>{p.warrantyMonths > 0 ? `${p.warrantyMonths} th` : "?"}</td>
                  <td className="td-center">{categories.find(c => c.id === p.categoryId)?.categoryName || "?"}</td>
                  <td className="td-center">
                    <span className={`badge ${p.isActive ? 'badge-active' : 'badge-inactive'}`}>
                      {p.isActive ? "Kinh doanh" : "Ng?ng"}
                    </span>
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => { setSelected(p); setModal("edit"); }}>??</button>
                      <button className="btn-icon" onClick={() => { setSelected(p); setModal("delete"); }}>???</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal === "create" && (
        <Modal title="Th�m vật tư m?i" onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <PartForm initial={EMPTY_PART} categories={categories} onSubmit={handleCreate} onCancel={closeModal} loading={saving} />
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`S?a � ${selected.name}`} onClose={closeModal}>
          {formError && <div className="error">{formError}</div>}
          <PartForm
            initial={{
              ...selected,
              imageUrl: imageByProductId.get(selected.id)?.imageUrl || "",
            }}
            categories={categories}
            onSubmit={handleEdit}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      )}

      {modal === "delete" && selected && (
        <Modal title="X?c nh?n x?a" onClose={closeModal}>
          <ConfirmDialog message={`X�a vật tư "${selected.name}"?`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </div>
  );
}






