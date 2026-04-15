import { useCallback, useEffect, useMemo, useState } from "react";
import { executionApi } from "@features/workshop/api/executionApi";
import { useBranch } from "@shared/core/context/BranchContext";

function pct(value) {
  const number = Number(value || 0);
  return `${number.toFixed(2)}%`;
}

function money(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} đ`;
}

export default function TechnicianProductivityDashboard() {
  const { selectedBranchId } = useBranch();
  const [period, setPeriod] = useState("month");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [report, setReport] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await executionApi.getTechnicianProductivityReport({
        period,
        branchId: selectedBranchId || undefined,
        top: 30,
      });
      setReport(data || null);
    } catch (err) {
      setError(err?.message || "Không thể tải dashboard năng suất.");
    } finally {
      setLoading(false);
    }
  }, [period, selectedBranchId]);

  useEffect(() => {
    load();
  }, [load]);

  const rows = useMemo(() => {
    const candidates = report?.rows || report?.technicians || report?.items || [];
    return Array.isArray(candidates) ? candidates : [];
  }, [report]);

  return (
    <div className="management-content">
      <header className="admin-header header-actions-only">
        <h1>Năng suất kỹ thuật viên</h1>
        <div className="bay-header-actions">
          <select className="search-input" value={period} onChange={(e) => setPeriod(e.target.value)}>
            <option value="week">Tuần</option>
            <option value="month">Tháng</option>
            <option value="quarter">Quý</option>
          </select>
          <button className="btn-ghost" type="button" onClick={load}>Làm mới</button>
        </div>
      </header>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {loading ? (
        <div className="loading-state"><div className="spinner" /><p>Đang tải dữ liệu năng suất...</p></div>
      ) : (
        <div className="table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Kỹ thuật viên</th>
                <th>Chuyên môn</th>
                <th>Giờ chuẩn</th>
                <th>Giờ thực tế</th>
                <th>Efficiency</th>
                <th>Doanh thu quy đổi</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr><td colSpan={6}><div className="empty-state"><p>Chưa có dữ liệu trong kỳ.</p></div></td></tr>
              ) : (
                rows.map((row, index) => (
                  <tr key={`${row.technicianId || row.id || index}`}>
                    <td>{row.technicianName || row.fullName || "-"}</td>
                    <td>{row.specialty || "-"}</td>
                    <td>{Number(row.flatRateHours || row.totalFlatRateHours || 0).toFixed(2)}h</td>
                    <td>{Number(row.actualHours || row.totalActualHours || 0).toFixed(2)}h</td>
                    <td>{pct(row.efficiencyPercent || row.efficiency || 0)}</td>
                    <td>{money(row.attributedRevenue || row.totalRevenue || 0)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

