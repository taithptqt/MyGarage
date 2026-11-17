import React, { useState, useEffect } from "react";
import { request } from "@app/apiClient";
import "./QcTemplateAdmin.css";

export default function QcTemplateAdminPage() {
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [activeTpl, setActiveTpl] = useState(null);

  useEffect(() => {
    loadTemplates();
  }, []);

  const loadTemplates = async () => {
    setLoading(true);
    try {
      // Mocked endpoint - assuming GET /api/qc-templates
      const res = await request({ url: "/api/v1/qc-templates", method: "GET" }).catch(e => []);
      // If API doesn't exist yet, seed with sample data
      const mockData = (res && res.length) > 0 ? res : [
        { id: "T1", name: "Bảo dưỡng 5,000km", description: "Mẫu kiểm tra tiêu chuẩn cho mốc 5,000km", groups: [
          { name: "Nội thất", items: ["Kiểm tra hệ thống đèn", "Kiểm tra gạt mưa", "Xịt rửa kính"] },
  { name: "Dưới gầm", items: ["Kiểm tra áp suất lốp", "Kiểm tra rò rỉ dầu"] }
        ]}
      ];
      setTemplates(mockData);
    } catch (e) {
      setError("Không th? t?i danh sách template.");
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    alert("Lưu template thành công (Mock)!");
    setActiveTpl(null);
  };

  if (activeTpl) {
    return (
      <div className="qc-tpl-page">
        <div className="qc-tpl-header">
          <div>
            <h1 className="qc-tpl-title">{activeTpl.id ? "Sửa Template QC" : "Tạo Template QC mới"}</h1>
        <p className="qc-tpl-subtitle">Cấu hình các hạng mục kiểm tra chất lượng</p>
          </div>
          <div style={{display:'flex', gap: '10px'}}>
            <button className="qc-btn qc-btn-ghost" onClick={() => setActiveTpl(null)}>Hủy</button>
            <button className="qc-btn qc-btn-primary" onClick={handleSave}>Lưu Template</button>
          </div>
        </div>

        <div className="qc-tpl-form">
          <div className="qc-field">
            <label>Tên Template:</label>
          <input type="text" value={activeTpl.name || ""} onChange={e => setActiveTpl({...activeTpl, name: e.target.value})} placeholder="VD: Bảo dưỡng tổng quát" />
          </div>
          <div className="qc-field">
            <label>Mô tả ngắn gọn:</label>
            <input type="text" value={activeTpl.description || ""} onChange={e => setActiveTpl({...activeTpl, description: e.target.value})} placeholder="Dùng cho gói bảo dưỡng nào..." />
          </div>

          <hr className="qc-divider" />
          <h3>Danh sách nhóm kiểm tra</h3>
          <p className="qc-tpl-subtitle">Mỗi nhóm chứa nhiều hạng mục Pass / Fail</p>

          <div className="qc-group-list">
            {(activeTpl.groups || []).map((grp, gId) => (
              <div key={gId} className="qc-group-card">
                <input type="text" className="qc-group-name" value={grp.name} onChange={()=>{}} placeholder="Tên nhóm (VD: Khoang động cơ)" />
                <ul className="qc-item-list">
                  {grp.items.map((it, iIdx) => (
                    <li key={iIdx}>
                      <input type="text" className="qc-item-name" value={it} onChange={()=>{}} />
                      <button className="qc-btn-sm text-red-500">Xóa</button>
                    </li>
                  ))}
                </ul>
                <button className="qc-btn-sm qc-btn-ghost mt-2">+ Thêm hạng mục</button>
              </div>
            ))}
            <button className="qc-btn qc-btn-dashed w-full">+ Thêm Nhóm Mới</button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="qc-tpl-page">
      <div className="qc-tpl-header">
        <div>
          <h1 className="qc-tpl-title">📋 Quản lý QC Template</h1>
        <p className="qc-tpl-subtitle">Quản lý các mẫu kiểm tra chất lượng chuẩn hóa cho thợ QC.</p>
        </div>
        <button className="qc-btn qc-btn-primary" onClick={() => setActiveTpl({ name: "", description: "", groups: [] })}>
          + Tạo Template
        </button>
      </div>

      <div className="qc-tpl-grid">
        {templates.map(t => (
          <div key={t.id} className="qc-tpl-card">
             <div className="flex justify-between items-start">
               <div>
                  <h3 className="text-lg font-bold text-slate-800">{t.name}</h3>
                  <p className="text-sm text-slate-500 mb-4">{t.description}</p>
               </div>
               <span className="bg-blue-100 text-blue-800 text-xs font-semibold px-2 py-1 rounded">
                 {(t.groups || []).reduce((acc, g) => acc + g.items.length, 0)} Items
               </span>
             </div>
             <button className="qc-btn-sm qc-btn-ghost w-full" onClick={() => setActiveTpl(t)}>Chỉnh sửa cấu hình</button>
          </div>
        ))}
      </div>
    </div>
  );
}


