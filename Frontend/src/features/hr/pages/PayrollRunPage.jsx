import React, { useState, useEffect, useMemo, useCallback } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./PayrollRun.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ?"; }
function getMonthStr() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

export default function PayrollRunPage() {
  const { user } = useAuth();
  const [period, setPeriod] = useState(getMonthStr());
  const [runs, setRuns] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  
  // -- view details or create new --
  const [activeRun, setActiveRun] = useState(null); // the payload we are previewing/editing
  const [previewing, setPreviewing] = useState(false);
  const [saving, setSaving] = useState(false);

  // load past runs
  const loadRuns = useCallback(async () => {
    setLoading(true);
    try {
      const res = await request({ url: "/api/payroll-runs", method: "GET", params: { branchId: user?.branchId } });
      setRuns(res?.items ?? res ?? []);
    } catch (e) {
      setError("Kh?ng th? t?i danh s?ch bảng luong.");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => { loadRuns(); }, [loadRuns]);

  const handlePreview = async () => {
    setPreviewing(true);
    setError("");
    try {
      const res = await request({ url: "/api/payroll-runs/preview", method: "GET", params: { branchId: user?.branchId, period } });
      // assume res is a PayrollRun object full of details
      const raw = res?.data ?? res;
      if (!raw || !raw.details) {
        throw new Error("Không c� dữ liệu cho k? luong n�y.");
      }
      setActiveRun({ ...raw, isPreview: true });
    } catch (err) {
      setError(err?.response?.data?.message || err.message || "L?i t?o b?n nh?p luong.");
    } finally {
      setPreviewing(false);
    }
  };

  const updateBonusPenalty = (idx, field, rawVal) => {
    if (!activeRun) return;
    const val = Number(rawVal) || 0;
    const items = [...activeRun.details];
    items[idx][field] = val;
    // recal net
    const item = items[idx];
    item.netSalary = item.baseSalary + item.commissionAmount + item.overtimeAmount + item.bonus - item.deductions - item.penalty;
    setActiveRun({ ...activeRun, details: items });
  };

  const totalPayroll = useMemo(() => {
    if (!activeRun?.details) return 0;
    return activeRun.details.reduce((sum, i) => sum + (i.netSalary || 0), 0);
  }, [activeRun]);

  const handleCreateRun = async () => {
    if (!activeRun) return;
    setSaving(true);
    setError("");
    try {
      await request({ url: "/api/payroll-runs", method: "POST", data: { 
        period: activeRun.period, 
        branchId: activeRun.branchId || user?.branchId, 
        createdBy: user?.id,
        details: activeRun.details 
      }});
      setActiveRun(null);
      loadRuns();
    } catch (err) {
      setError(err?.response?.data?.message || "L?i khi luu bảng luong.");
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id) => {
    try {
      await request({ url: `/api/payroll-runs/${id}/approve`, method: "PUT", data: { approvedBy: user?.id } });
      loadRuns();
      if (activeRun?.id === id) setActiveRun(null);
    } catch (err) {
      alert("L?i duy?t: " + (err?.response?.data?.message || err.message));
    }
  };

  // ----- RENDERING -----
  if (activeRun) {
    const isEditing = activeRun.isPreview;
    return (
      <div className="payroll-page">
        <div className="payroll-header">
          <div>
            <h1 className="payroll-title">?? Bảng luong {activeRun.period}</h1>
            <p className="payroll-subtitle">{isEditing ? "?ang xem b?n nh?p (Chua luu)" : "Chi ti?t bảng luong"}</p>
          </div>
          <button className="payroll-btn payroll-btn-ghost" onClick={() => setActiveRun(null)}>?? Quay l?i</button>
        </div>

        <div className="payroll-board">
          <table className="payroll-table">
            <thead>
              <tr>
                <th>Nh?n vi?n</th>
                <th className="t-right">Luong co b?n</th>
                <th className="t-right">Hoa h?ng</th>
                <th className="t-right">Luong OT</th>
                <th className="t-right">Kh?u tr? (-)</th>
                <th className="t-right">Thu?ng (+)</th>
                <th className="t-right">Ph?t (-)</th>
                <th className="t-right">Th?c l�nh</th>
              </tr>
            </thead>
            <tbody>
              {activeRun.details.map((d, idx) => (
                <tr key={idx}>
                  <td className="font-semibold text-blue-800">{d.staffName}</td>
                  <td className="t-right">{fmtCurrency(d.baseSalary)}</td>
                  <td className="t-right text-emerald-600">{fmtCurrency(d.commissionAmount)}</td>
                  <td className="t-right text-indigo-600">{fmtCurrency(d.overtimeAmount)}</td>
                  <td className="t-right text-red-500">{fmtCurrency(d.deductions)}</td>
                  <td className="t-right">
                    {isEditing ? (
                       <input type="number" min="0" step="1000" className="payroll-input-inline text-emerald-600" value={d.bonus || 0} onChange={e => updateBonusPenalty(idx, 'bonus', e.target.value)} />
                    ) : fmtCurrency(d.bonus)}
                  </td>
                  <td className="t-right">
                    {isEditing ? (
                       <input type="number" min="0" step="1000" className="payroll-input-inline text-red-500" value={d.penalty || 0} onChange={e => updateBonusPenalty(idx, 'penalty', e.target.value)} />
                    ) : fmtCurrency(d.penalty)}
                  </td>
                  <td className="t-right font-bold text-slate-800">{fmtCurrency(d.netSalary)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan="7" className="t-right font-bold">Tổng Qu? Luong:</td>
                <td className="t-right font-bold text-indigo-700 text-lg">{fmtCurrency(totalPayroll)}</td>
              </tr>
            </tfoot>
          </table>
          
          <div className="payroll-footer">
            {isEditing && (
              <button className="payroll-btn payroll-btn-primary" onClick={handleCreateRun} disabled={saving}>
                {saving ? "? Đang luu..." : "?? Luu bảng luong ch�nh th?c"}
              </button>
            )}
            {!isEditing && activeRun.status === "DRAFT" && (
              <button className="payroll-btn payroll-btn-approve" onClick={() => handleApprove(activeRun.id)}>
                ? Duy?t Bảng Luong
              </button>
            )}
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="payroll-page">
      <div className="payroll-header">
        <div>
          <h1 className="payroll-title">?? Quản lý luong thu?ng (Payroll)</h1>
          <p className="payroll-subtitle">T?nh to?n bảng luong h?ng th?ng, duy?t luong v? tr? luong.</p>
        </div>
        <div className="payroll-actions">
           <input type="month" value={period} onChange={e => setPeriod(e.target.value)} className="payroll-month-input" />
           <button className="payroll-btn payroll-btn-primary" onClick={handlePreview} disabled={previewing}>
             {previewing ? "Đang t�nh..." : "? T?o bảng luong k? n�y"}
           </button>
        </div>
      </div>

      <div className="payroll-board">
        {loading ? <div className="payroll-empty">Đang t?i l?ch s?...</div> : 
         runs.length === 0 ? <div className="payroll-empty">Chua c� bảng luong n�o du?c t?o.</div> : (
           <table className="payroll-table">
             <thead>
               <tr>
                 <th>M� K? Luong</th>
                 <th>Thời gian t?o</th>
                 <th className="t-right">Tổng th?c chi</th>
                 <th>Tr?ng th?i</th>
                 <th>Thao tác</th>
               </tr>
             </thead>
             <tbody>
               {runs.map(r => (
                 <tr key={r.id}>
                   <td className="font-semibold">{r.period}</td>
                   <td>{new Date(r.createdAt).toLocaleDateString("vi-VN")}</td>
                   <td className="t-right font-bold text-slate-700">{fmtCurrency(r.totalAmount)}</td>
                   <td><span className={`pr-status st-${(r.status || "").toLowerCase()}`}>{r.status}</span></td>
                   <td>
                     <button className="payroll-btn-sm" onClick={() => setActiveRun(r)}>Xem chi ti?t</button>
                     {r.status === "DRAFT" && (user?.role === "ADMIN" || user?.role === "MANAGER") && (
                       <button className="payroll-btn-sm bg-green-100 text-green-700 ml-2" onClick={() => handleApprove(r.id)}>? Duy?t</button>
                     )}
                     {r.status === "APPROVED" && (
                       <button className="payroll-btn-sm bg-indigo-100 text-indigo-700 ml-2">Phi?u chi</button>
                     )}
                   </td>
                 </tr>
               ))}
             </tbody>
           </table>
         )}
      </div>
    </div>
  );
}


