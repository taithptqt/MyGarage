import { useCallback, useEffect, useMemo, useState } from "react";
import { getAdminChangeLogs } from "@features/admin/api/auditLogApi";
import "./AdminPage.css";
import "./AdminChangeLogPage.css";

const ACTION_OPTIONS = ["", "CREATE", "UPDATE", "DELETE"];

function previewJson(value) {
  if (!value) return "-";
  const text = String(value);
  return text.length > 140 ? `${text.slice(0, 140)}...` : text;
}

export default function AdminChangeLogPage() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [logs, setLogs] = useState([]);
  const [total, setTotal] = useState(0);
  const [filters, setFilters] = useState({
    page: 1,
    pageSize: 30,
    action: "",
    tableName: "",
    query: "",
    from: "",
    to: ""
  });

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await getAdminChangeLogs(filters);
      setLogs(res?.items || []);
      setTotal(Number(res?.total || 0));
    } catch (err) {
      setError(err.message || "Không tải được nhật ký chệnh sửa.");
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(total / Math.max(1, filters.pageSize))),
    [total, filters.pageSize]
  );

  const updateFilter = (name, value) => {
    setFilters((prev) => ({ ...prev, [name]: value, page: 1 }));
  };

  const changePage = (delta) => {
    setFilters((prev) => ({ ...prev, page: Math.min(totalPages, Math.max(1, prev.page + delta)) }));
  };

  return (
    <div className="management-content admin-change-log-page">
      <header className="admin-header">
        <div>
          <h1>Nhật ký chệnh sửa</h1>
          <p className="header-sub">Theo dõi toàn bộ thao tác thêm/sửa/xóa trên màn hình quản trị.</p>
        </div>
      </header>

      <div className="stats-bar">
        <div className="stat-card"><div className="stat-value">{total.toLocaleString()}</div><div className="stat-label">Tđ.ng bản ghi</div></div>
        <div className="stat-card"><div className="stat-value">{filters.page}</div><div className="stat-label">Trang hiện tại</div></div>
        <div className="stat-card"><div className="stat-value">{totalPages}</div><div className="stat-label">Tđ.ng trang</div></div>
      </div>

      <div className="table-toolbar admin-log-filters">
        <select className="search-input" value={filters.action} onChange={(e) => updateFilter("action", e.target.value)}>
          {ACTION_OPTIONS.map((action) => (
            <option key={action || "ALL"} value={action}>{action || "Tất cả hành động"}</option>
          ))}
        </select>
        <input className="search-input" placeholder="Lọc theo bảng (Branches, Staffs, ...)" value={filters.tableName} onChange={(e) => updateFilter("tableName", e.target.value)} />
        <input className="search-input" placeholder="Tìm theo nội dung thay đđ.i..." value={filters.query} onChange={(e) => updateFilter("query", e.target.value)} />
        <input className="search-input" type="datetime-local" value={filters.from} onChange={(e) => updateFilter("from", e.target.value)} />
        <input className="search-input" type="datetime-local" value={filters.to} onChange={(e) => updateFilter("to", e.target.value)} />
        <button className="btn-primary" onClick={fetchLogs}>Lọc</button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>Đang tải nhật ký...</p></div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Thời gian</th>
                <th>Hành động</th>
                <th>Bảng</th>
                <th>Record</th>
                <th>User</th>
                <th>IP</th>
                <th>Dữ liệu cũ</th>
                <th>Dữ liệu mơi</th>
              </tr>
            </thead>
            <tbody>
              {logs.length === 0 ? (
                <tr>
                  <td colSpan={9}>
                    <div className="empty-state"><p>Chưa có bản ghi phù hợp.</p></div>
                  </td>
                </tr>
              ) : logs.map((log) => (
                <tr key={log.id}>
                  <td className="td-id">#{log.id}</td>
                  <td>{log.timestamp ? new Date(log.timestamp).toLocaleString() : "-"}</td>
                  <td><span className={`badge ${log.action === "DELETE" ? "badge-deleted" : log.action === "UPDATE" ? "badge-suspended" : "badge-active"}`}>{log.action}</span></td>
                  <td>{log.tableName}</td>
                  <td>{log.recordId}</td>
                  <td>{log.userType}#{log.userId}</td>
                  <td>{log.ipAddress || "-"}</td>
                  <td className="admin-log-json">{previewJson(log.oldValue)}</td>
                  <td className="admin-log-json">{previewJson(log.newValue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="table-toolbar">
        <button className="btn-ghost" onClick={() => changePage(-1)} disabled={filters.page <= 1}>Trang trươc</button>
        <span className="result-count">Trang {filters.page} / {totalPages}</span>
        <button className="btn-ghost" onClick={() => changePage(1)} disabled={filters.page >= totalPages}>Trang sau</button>
      </div>
    </div>
  );
}





