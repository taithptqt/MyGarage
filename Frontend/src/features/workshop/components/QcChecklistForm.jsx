import { useCallback, useEffect, useMemo, useState } from "react";
import { executionApi } from "@features/workshop/api/executionApi";

const RESULT_OPTIONS = ["PASS", "FAIL", "N/A"];

function createDraftItem(name = "", category = "") {
  return {
    id: 0,
    itemName: name,
    category,
    isRequired: true,
    result: "",
    failNote: "",
    photoUrl: "",
  };
}

export default function QcChecklistForm({ staffId = null }) {
  const [inspectionId, setInspectionId] = useState("");
  const [serviceType, setServiceType] = useState("");
  const [templates, setTemplates] = useState([]);
  const [items, setItems] = useState([createDraftItem()]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await executionApi.getQcChecklistTemplates({
        serviceType: serviceType || undefined,
      });
      const rows = Array.isArray(data?.items) ? data.items : [];
      setTemplates(rows);
    } catch (err) {
      setError(err?.message || "Không thể tải template checklist QC.");
    } finally {
      setLoading(false);
    }
  }, [serviceType]);

  useEffect(() => {
    loadTemplates();
  }, [loadTemplates]);

  const importTemplate = () => {
    if (templates.length === 0) return;
    const mapped = templates.map((tpl) => createDraftItem(tpl.name, tpl.serviceCategory || ""));
    setItems(mapped.length > 0 ? mapped : [createDraftItem()]);
    setMessage("Đã nạp checklist từ template.");
  };

  const updateItem = (index, field, value) => {
    setItems((prev) => prev.map((item, idx) => (idx === index ? { ...item, [field]: value } : item)));
  };

  const addItem = () => setItems((prev) => [...prev, createDraftItem()]);
  const removeItem = (index) => setItems((prev) => prev.filter((_, idx) => idx !== index));

  const summary = useMemo(() => {
    const required = items.filter((row) => row.isRequired);
    const passOrNa = required.filter((row) => ["PASS", "N/A"].includes(String(row.result || "").toUpperCase())).length;
    const percent = required.length === 0 ? 0 : (passOrNa * 100) / required.length;
    return { required: required.length, passOrNa, percent };
  }, [items]);

  const submit = async (event) => {
    event.preventDefault();
    const id = Number(inspectionId);
    if (!id) {
      setError("Vui lòng nhập QC Inspection ID.");
      return;
    }

    setSaving(true);
    setError("");
    setMessage("");
    try {
      const payload = {
        overwriteExisting: true,
        items: items
          .filter((row) => String(row.itemName || "").trim().length > 0)
          .map((row) => ({
            id: row.id || null,
            itemName: row.itemName,
            category: row.category || null,
            isRequired: Boolean(row.isRequired),
            result: row.result || null,
            failNote: row.failNote || null,
            photoUrl: row.photoUrl || null,
            checkedBy: staffId ? Number(staffId) : null,
          })),
      };
      const result = await executionApi.upsertQcInspectionItems(id, payload);
      setMessage(`Đã lưu checklist QC. Điểm tổng: ${Number(result?.overallScore || 0).toFixed(2)}%.`);
    } catch (err) {
      setError(err?.message || "Không thể lưu checklist QC.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="inventory-panel">
      <div className="inventory-panel-head">
        <h3>QC Checklist Form</h3>
        <p>Nhập kết quả từng hạng mục theo template để chuẩn hóa nghiệm thu.</p>
      </div>
      {error ? <div className="alert alert-error">{error}</div> : null}
      {message ? <div className="alert">{message}</div> : null}
      <form className="inventory-panel-body inventory-form-grid" onSubmit={submit}>
        <input className="search-input" placeholder="QC Inspection ID" value={inspectionId} onChange={(e) => setInspectionId(e.target.value)} />
        <input className="search-input" placeholder="Service type (tuỳ chọn)" value={serviceType} onChange={(e) => setServiceType(e.target.value)} />
        <button className="btn-ghost" type="button" onClick={loadTemplates}>{loading ? "Đang tải..." : "Tải template"}</button>
        <button className="btn-ghost" type="button" onClick={importTemplate} disabled={templates.length === 0}>Nạp template</button>

        <div className="table-wrapper" style={{ gridColumn: "1 / -1" }}>
          <table className="admin-table">
            <thead>
              <tr>
                <th>Hạng mục</th>
                <th>Nhóm</th>
                <th>Kết quả</th>
                <th>Bắt buộc</th>
                <th>Ghi chú lỗi</th>
                <th>Ảnh lỗi URL</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((row, index) => (
                <tr key={`qc-item-${index}`}>
                  <td><input className="search-input" value={row.itemName} onChange={(e) => updateItem(index, "itemName", e.target.value)} /></td>
                  <td><input className="search-input" value={row.category} onChange={(e) => updateItem(index, "category", e.target.value)} /></td>
                  <td>
                    <select className="search-input" value={row.result} onChange={(e) => updateItem(index, "result", e.target.value)}>
                      <option value="">Chưa chấm</option>
                      {RESULT_OPTIONS.map((option) => <option key={option} value={option}>{option}</option>)}
                    </select>
                  </td>
                  <td className="td-center">
                    <input type="checkbox" checked={Boolean(row.isRequired)} onChange={(e) => updateItem(index, "isRequired", e.target.checked)} />
                  </td>
                  <td><input className="search-input" value={row.failNote} onChange={(e) => updateItem(index, "failNote", e.target.value)} /></td>
                  <td><input className="search-input" value={row.photoUrl} onChange={(e) => updateItem(index, "photoUrl", e.target.value)} /></td>
                  <td className="td-center"><button className="btn-icon" type="button" onClick={() => removeItem(index)} disabled={items.length <= 1}>Xóa</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <button className="btn-ghost" type="button" onClick={addItem}>+ Thêm hạng mục</button>
          <div className="result-count">Pass/N-A: {summary.passOrNa}/{summary.required} ({summary.percent.toFixed(2)}%)</div>
          <button className="btn-primary" type="submit" disabled={saving}>{saving ? "Đang lưu..." : "Lưu checklist"}</button>
        </div>
      </form>
    </section>
  );
}

