import React, { useState, useEffect, useMemo, useCallback } from "react";
import { useAuth } from "@shared/core/context/AuthContext";
import TablePagination from "@shared/components/TablePagination";
import {
  getPayrollRuns,
  getPayrollPreview,
  createPayrollRun,
  approvePayrollRun,
} from "../api/payrollApi";
import "./PayrollRun.css";

function fmtCurrency(v) {
  return Number(v ?? 0).toLocaleString("vi-VN") + " ₫";
}

function getMonthStr() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
}

export default function PayrollRunPage() {
  const { user } = useAuth();
  const [period, setPeriod] = useState(getMonthStr());
  const [runs, setRuns] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalRuns, setTotalRuns] = useState(0);
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
      const res = await getPayrollRuns({
        branchId: user?.branchId,
        page,
        pageSize,
      });
      const items = Array.isArray(res) ? res : res?.items ?? [];
      const total = typeof res?.total === "number" ? res.total : items.length;
      setRuns(items);
      setTotalRuns(total);
    } catch (e) {
      setError("Không thể tải danh sách bảng lương.");
    } finally {
      setLoading(false);
    }
  }, [user, page, pageSize]);

  useEffect(() => {
    loadRuns();
  }, [loadRuns]);

  const handlePreview = async () => {
    setPreviewing(true);
    setError("");
    try {
      const res = await getPayrollPreview({
        branchId: user?.branchId,
        period,
      });
      // assume res is a PayrollRun object full of details
      const raw = res?.data ?? res;
      if (!raw || !raw.details) {
        throw new Error("Không có dữ liệu cho kỳ lương này.");
      }
      setActiveRun({ ...raw, isPreview: true });
    } catch (err) {
      setError(err?.response?.data?.message || err.message || "Lỗi tạo bản nháp lương.");
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
    item.netSalary =
      item.baseSalary +
      item.commissionAmount +
      item.overtimeAmount +
      item.bonus -
      item.deductions -
      item.penalty;
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
      await createPayrollRun({
        period: activeRun.period,
        branchId: activeRun.branchId || user?.branchId,
        createdBy: user?.id,
        details: activeRun.details,
      });
      setActiveRun(null);
      loadRuns();
    } catch (err) {
      setError(err?.response?.data?.message || "Lỗi khi lưu bảng lương.");
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async (id) => {
    try {
      await approvePayrollRun(id, { approvedBy: user?.id });
      loadRuns();
      if (activeRun?.id === id) setActiveRun(null);
    } catch (err) {
      alert("Lỗi duyệt: " + (err?.response?.data?.message || err.message));
    }
  };

  // ----- RENDERING -----
  if (activeRun) {
    const isEditing = activeRun.isPreview;
    return (
      <div className="payroll-page">
        <div className="payroll-header">
          <div>
            <h1 className="payroll-title">📋 Bảng lương {activeRun.period}</h1>
            <p className="payroll-subtitle">
              {isEditing ? "Đang xem bản nháp (Chưa lưu)" : "Chi tiết bảng lương"}
            </p>
          </div>
          <button className="payroll-btn payroll-btn-ghost" onClick={() => setActiveRun(null)}>
            ← Quay lại
          </button>
        </div>

        <div className="payroll-board">
          <table className="payroll-table">
            <thead>
              <tr>
                <th>Nhân viên</th>
                <th className="t-right">Lương cơ bản</th>
                <th className="t-right">Hoa hồng</th>
                <th className="t-right">Lương OT</th>
                <th className="t-right">Khấu trừ (-)</th>
                <th className="t-right">Thưởng (+)</th>
                <th className="t-right">Phạt (-)</th>
                <th className="t-right">Thực lãnh</th>
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
                      <input
                        type="number"
                        min="0"
                        step="1000"
                        className="payroll-input-inline text-emerald-600"
                        value={d.bonus || 0}
                        onChange={(e) => updateBonusPenalty(idx, "bonus", e.target.value)}
                      />
                    ) : (
                      fmtCurrency(d.bonus)
                    )}
                  </td>
                  <td className="t-right">
                    {isEditing ? (
                      <input
                        type="number"
                        min="0"
                        step="1000"
                        className="payroll-input-inline text-red-500"
                        value={d.penalty || 0}
                        onChange={(e) => updateBonusPenalty(idx, "penalty", e.target.value)}
                      />
                    ) : (
                      fmtCurrency(d.penalty)
                    )}
                  </td>
                  <td className="t-right font-bold text-slate-800">{fmtCurrency(d.netSalary)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan="7" className="t-right font-bold">
                  Tổng Quỹ Lương:
                </td>
                <td className="t-right font-bold text-indigo-700 text-lg">
                  {fmtCurrency(totalPayroll)}
                </td>
              </tr>
            </tfoot>
          </table>

          <div className="payroll-footer">
            {isEditing && (
              <button className="payroll-btn payroll-btn-primary" onClick={handleCreateRun} disabled={saving}>
                {saving ? "⏳ Đang lưu..." : "💾 Lưu bảng lương chính thức"}
              </button>
            )}
            {!isEditing && activeRun.status === "DRAFT" && (
              <button className="payroll-btn payroll-btn-approve" onClick={() => handleApprove(activeRun.id)}>
                ✓ Duyệt Bảng Lương
              </button>
            )}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="payroll-page">
      <div className="payroll-header">
        <div>
          <h1 className="payroll-title">💰 Quản lý lương thưởng (Payroll)</h1>
          <p className="payroll-subtitle">Tính toán bảng lương hàng tháng, duyệt lương và trả lương.</p>
        </div>
        <div className="payroll-actions">
          <input
            type="month"
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
            className="payroll-month-input"
          />
          <button className="payroll-btn payroll-btn-primary" onClick={handlePreview} disabled={previewing}>
            {previewing ? "Đang tính..." : "➕ Tạo bảng lương kỳ này"}
          </button>
        </div>
      </div>

      <div className="payroll-board">
        {loading ? (
          <div className="payroll-empty">Đang tải lịch sử...</div>
        ) : runs.length === 0 ? (
          <div className="payroll-empty">Chưa có bảng lương nào được tạo.</div>
        ) : (
          <>
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Mã Kỳ Lương</th>
                  <th>Thời gian tạo</th>
                  <th className="t-right">Tổng thực chi</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((r) => (
                  <tr key={r.id}>
                    <td className="font-semibold">{r.period}</td>
                    <td>{new Date(r.createdAt).toLocaleDateString("vi-VN")}</td>
                    <td className="t-right font-bold text-slate-700">{fmtCurrency(r.totalAmount)}</td>
                    <td>
                      <span className={`pr-status st-${(r.status || "").toLowerCase()}`}>{r.status}</span>
                    </td>
                    <td>
                      <button className="payroll-btn-sm" onClick={() => setActiveRun(r)}>
                        Xem chi tiết
                      </button>
                      {r.status === "DRAFT" && (user?.role === "ADMIN" || user?.role === "MANAGER") && (
                        <button
                          className="payroll-btn-sm bg-green-100 text-green-700 ml-2"
                          onClick={() => handleApprove(r.id)}
                        >
                          ✓ Duyệt
                        </button>
                      )}
                      {r.status === "APPROVED" && (
                        <button className="payroll-btn-sm bg-indigo-100 text-indigo-700 ml-2">Phiếu chi</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <TablePagination
              totalItems={totalRuns}
              currentPage={page}
              pageSize={pageSize}
              onPageChange={setPage}
              onPageSizeChange={(newSize) => {
                setPageSize(newSize);
                setPage(1);
              }}
              pageSizeOptions={[10, 20, 50]}
            />
          </>
        )}
      </div>
    </div>
  );
}
