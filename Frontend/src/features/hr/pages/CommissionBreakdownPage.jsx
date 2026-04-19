import React, { useState, useEffect, useCallback } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./CommissionBreakdown.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }
function fmtDate(v) { return v ? new Date(v).toLocaleDateString("vi-VN") : "--"; }

export default function CommissionBreakdownPage() {
  const { user } = useAuth();
  const [period, setPeriod] = useState(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  });
  
  const [commissions, setCommissions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const loadData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await request({ 
        url: "/api/commissions", 
        method: "GET", 
        params: { branchId: user?.branchId, period } 
      });
      setCommissions(res?.items ?? res ?? []);
    } catch (e) {
      setError("Không thể tải dữ liệu hoa hồng.");
    } finally {
      setLoading(false);
    }
  }, [user, period]);

  useEffect(() => { loadData(); }, [loadData]);

  // Group by Staff
  const grouped = commissions.reduce((acc, curr) => {
    const sId = curr.staffId;
    if (!acc[sId]) acc[sId] = { staffName: curr.staffName || "Unknown", total: 0, items: [] };
    acc[sId].items.push(curr);
    acc[sId].total += (curr.amount || 0);
    return acc;
  }, {});

  const handleExport = () => {
    alert("Chức năng xuất Excel đang được tích hợp!");
  };

  return (
    <div className="comm-page">
      <div className="comm-header">
        <div>
          <h1 className="comm-title">💰 Bảng Kê Hoa Hồng</h1>
          <p className="comm-subtitle">Theo d�i v� d?i so�t hoa h?ng theo tổng nhân viên trong th�ng.</p>
        </div>
        <div className="comm-filters">
          <input type="month" className="comm-month-input" value={period} onChange={e => setPeriod(e.target.value)} />
          <button className="comm-btn-export" onClick={handleExport}>⬇ Xuất Excel</button>
        </div>
      </div>

      {error ? (
        <div className="comm-alert err">{error}</div>
      ) : loading ? (
        <div className="comm-loading">Đang tải dữ liệu...</div>
      ) : Object.keys(grouped).length === 0 ? (
        <div className="comm-empty">Trong th�ng n�y chua c� ph�t sinh hoa h?ng n�o.</div>
      ) : (
        <div className="comm-list">
          {Object.values(grouped).map((group, gIdx) => (
            <div key={gIdx} className="comm-group">
              <div className="comm-group-header">
                <h2>ðŸ‘¤ {group.staffName}</h2>
                <div className="comm-group-total">Tổng: <span>{fmtCurrency(group.total)}</span></div>
              </div>
              <table className="comm-table">
                <thead>
                  <tr>
                    <th>Ngày ghi nhận</th>
                    <th>Nguồn (Lệnh / Hóa đơn)</th>
                    <th>Vai trò hưởng</th>
                    <th className="t-right">Doanh thu tính</th>
                    <th className="t-right">Tỷ lệ (%)</th>
                    <th className="t-right">Tiền hoa hồng</th>
                  </tr>
                </thead>
                <tbody>
                  {group.items.map((item, iIdx) => (
                    <tr key={iIdx}>
                      <td>{fmtDate(item.createdAt)}</td>
                      <td>
                        <span className="comm-badge-source">#{item.sourceCode || item.serviceOrderId}</span>
                      </td>
                      <td><span className={`comm-role-badge r-${(item.roleType||"").toLowerCase()}`}>{item.roleType || "TECH"}</span></td>
                      <td className="t-right text-slate-500">{fmtCurrency(item.sourceAmount)}</td>
                      <td className="t-right font-semibold">{item.commissionRate}%</td>
                      <td className="t-right font-bold text-emerald-600">{fmtCurrency(item.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}


