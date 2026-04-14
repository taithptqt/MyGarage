import { useCallback, useEffect, useMemo, useState } from "react";
import { getAdminChangeLogs } from "../api/auditLogApi";
import "./AdminPage.css";
import "./AdminChangeLogPage.css";

const ACTION_OPTIONS = [
  { value: "", label: "Tất cả hành động" },
  { value: "CREATE", label: "Thêm mới" },
  { value: "UPDATE", label: "Cập nhật" },
  { value: "DELETE", label: "Xóa" },
];

const TABLE_LABELS = {
  Branches: "Chi nhánh",
  Staffs: "Nhân sự",
  Customers: "Khách hàng",
  Roles: "Vai trò",
  VehicleBrands: "Hãng xe",
  VehicleModels: "Dòng xe",
  Services: "Dịch vụ",
  SalaryCoefficients: "Hệ số lương",
  SalaryRequests: "Yêu cầu lương",
  SalaryProfiles: "Hồ sơ lương",
  Warehouses: "Kho",
  InventoryItems: "Vật tư",
  Appointments: "Lịch hẹn",
};

const FIELD_LABELS = {
  fullName: "Họ tên",
  name: "Tên",
  code: "Mã",
  status: "Trạng thái",
  isActive: "Kích hoạt",
  email: "Email",
  phoneNumber: "Số điện thoại",
  address: "Địa chỉ",
  roleId: "Vai trò",
  roleName: "Tên vai trò",
  branchId: "Chi nhánh",
  branchName: "Tên chi nhánh",
  baseSalary: "Lương cơ bản",
  salaryCoefficient: "Hệ số lương",
  allowance: "Phụ cấp",
  note: "Ghi chú",
};

const IGNORED_FIELDS = new Set([
  "id",
  "createdAt",
  "updatedAt",
  "deletedAt",
  "createdBy",
  "updatedBy",
  "timestamp",
  "rowVersion",
  "passwordHash",
  "password",
  "salt",
]);

function actionLabel(action) {
  const normalized = String(action || "").toUpperCase();
  if (normalized === "CREATE") return "Thêm mới";
  if (normalized === "UPDATE") return "Cập nhật";
  if (normalized === "DELETE") return "Xóa";
  return normalized || "Khác";
}

function actionBadgeClass(action) {
  const normalized = String(action || "").toUpperCase();
  if (normalized === "DELETE") return "badge-deleted";
  if (normalized === "UPDATE") return "badge-suspended";
  return "badge-active";
}

function tableLabel(tableName) {
  const key = String(tableName || "");
  return TABLE_LABELS[key] || key || "Khác";
}

function userTypeLabel(type) {
  const normalized = String(type || "").toUpperCase();
  if (normalized === "STAFF") return "Nhân viên";
  if (normalized === "CUSTOMER") return "Khách hàng";
  if (normalized === "ADMIN") return "Quản trị";
  if (normalized === "SYSTEM") return "Hệ thống";
  return type || "Người dùng";
}

function safeParseJson(value) {
  if (value === null || value === undefined || value === "") return null;
  if (typeof value === "object") return value;
  const raw = String(value).trim();
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return { value: raw };
  }
}

function normalizeRecord(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return {};
  return value;
}

function toLabelFromKey(key) {
  if (!key) return "Trường";
  if (FIELD_LABELS[key]) return FIELD_LABELS[key];
  const withSpaces = String(key).replace(/([a-z])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ");
  return withSpaces.charAt(0).toUpperCase() + withSpaces.slice(1);
}

function compactString(value) {
  const text = String(value || "").trim();
  if (!text) return "trống";
  return text.length > 45 ? `${text.slice(0, 45)}...` : text;
}

function formatValue(value) {
  if (value === null || value === undefined || value === "") return "trống";
  if (typeof value === "boolean") return value ? "Bật" : "Tắt";
  if (typeof value === "number") return Number.isFinite(value) ? value.toLocaleString("vi-VN") : String(value);
  if (Array.isArray(value)) return `${value.length} mục`;
  if (typeof value === "object") {
    const nameLike = value.name ?? value.fullName ?? value.code ?? value.email;
    return nameLike ? compactString(nameLike) : "đối tượng";
  }
  return compactString(value);
}

function normalizeCompare(value) {
  if (value === null || value === undefined || value === "") return "";
  if (typeof value === "object") {
    try {
      return JSON.stringify(value);
    } catch {
      return String(value);
    }
  }
  return String(value).trim();
}

function summarizeRecord(record) {
  const keys = Object.keys(record).filter((key) => !IGNORED_FIELDS.has(key));
  if (!keys.length) return "Không có thông tin chi tiết";
  const highlighted = ["fullName", "name", "code", "status", "email", "phoneNumber", "baseSalary", "salaryCoefficient"];
  const selected = [];
  highlighted.forEach((key) => {
    if (selected.length >= 3) return;
    if (Object.prototype.hasOwnProperty.call(record, key)) selected.push(key);
  });
  keys.forEach((key) => {
    if (selected.length >= 3) return;
    if (!selected.includes(key)) selected.push(key);
  });
  return selected
    .map((key) => `${toLabelFromKey(key)}: ${formatValue(record[key])}`)
    .join(" | ");
}

function summarizeUpdate(oldRecord, newRecord) {
  const changedKeys = Array.from(new Set([...Object.keys(oldRecord), ...Object.keys(newRecord)]))
    .filter((key) => !IGNORED_FIELDS.has(key))
    .filter((key) => normalizeCompare(oldRecord[key]) !== normalizeCompare(newRecord[key]));

  if (!changedKeys.length) return "Cập nhật thông tin";

  const top = changedKeys.slice(0, 3).map((key) => {
    const oldVal = formatValue(oldRecord[key]);
    const newVal = formatValue(newRecord[key]);
    return `${toLabelFromKey(key)}: ${oldVal} -> ${newVal}`;
  });
  const remain = changedKeys.length - top.length;
  if (remain > 0) top.push(`+${remain} thay đổi khác`);
  return top.join(" | ");
}

function buildSummary(log) {
  const action = String(log.action || "").toUpperCase();
  const oldRecord = normalizeRecord(safeParseJson(log.oldValue));
  const newRecord = normalizeRecord(safeParseJson(log.newValue));
  if (action === "CREATE") return `Đã thêm mới: ${summarizeRecord(newRecord)}`;
  if (action === "DELETE") return `Đã xóa: ${summarizeRecord(oldRecord)}`;
  return summarizeUpdate(oldRecord, newRecord);
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
    to: "",
  });

  const tableOptions = useMemo(
    () => [{ value: "", label: "Tất cả danh mục" }].concat(
      Object.entries(TABLE_LABELS).map(([value, label]) => ({ value, label })),
    ),
    [],
  );

  const rows = useMemo(
    () =>
      (logs || []).map((log) => ({
        ...log,
        actionText: actionLabel(log.action),
        actionBadge: actionBadgeClass(log.action),
        tableText: tableLabel(log.tableName),
        actorText: `${userTypeLabel(log.userType)} #${log.userId || "-"}`,
        summaryText: buildSummary(log),
      })),
    [logs],
  );

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await getAdminChangeLogs(filters);
      setLogs(res?.items || []);
      setTotal(Number(res?.total || 0));
    } catch (err) {
      setError(err.message || "Không tải được nhật ký thay đổi.");
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(total / Math.max(1, filters.pageSize))),
    [total, filters.pageSize],
  );

  const updateFilter = (name, value) => {
    setFilters((prev) => ({ ...prev, [name]: value, page: 1 }));
  };

  const changePage = (delta) => {
    setFilters((prev) => ({ ...prev, page: Math.min(totalPages, Math.max(1, prev.page + delta)) }));
  };

  return (
    <div className="management-content admin-change-log-page">
      <div className="stats-bar">
        <div className="stat-card">
          <div className="stat-value">{total.toLocaleString()}</div>
          <div className="stat-label">Tổng bản ghi</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{filters.page}</div>
          <div className="stat-label">Trang hiện tại</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{totalPages}</div>
          <div className="stat-label">Tổng trang</div>
        </div>
      </div>

      <div className="table-toolbar admin-log-filters">
        <select className="search-input" value={filters.action} onChange={(e) => updateFilter("action", e.target.value)}>
          {ACTION_OPTIONS.map((action) => (
            <option key={action.value || "ALL"} value={action.value}>
              {action.label}
            </option>
          ))}
        </select>

        <select className="search-input" value={filters.tableName} onChange={(e) => updateFilter("tableName", e.target.value)}>
          {tableOptions.map((table) => (
            <option key={table.value || "ALL"} value={table.value}>
              {table.label}
            </option>
          ))}
        </select>

        <input
          className="search-input"
          placeholder="Tìm theo tên, mã bản ghi, nội dung thay đổi..."
          value={filters.query}
          onChange={(e) => updateFilter("query", e.target.value)}
        />
        <input
          className="search-input"
          type="datetime-local"
          value={filters.from}
          onChange={(e) => updateFilter("from", e.target.value)}
        />
        <input
          className="search-input"
          type="datetime-local"
          value={filters.to}
          onChange={(e) => updateFilter("to", e.target.value)}
        />
        <button className="btn-primary" onClick={fetchLogs}>
          Lọc
        </button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Đang tải nhật ký...</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Mã log</th>
                <th>Thời gian</th>
                <th>Hành động</th>
                <th>Danh mục</th>
                <th>Mã bản ghi</th>
                <th>Người thao tác</th>
                <th>Nội dung thay đổi</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={7}>
                    <div className="empty-state">
                      <p>Chưa có bản ghi phù hợp.</p>
                    </div>
                  </td>
                </tr>
              ) : (
                rows.map((log) => (
                  <tr key={log.id}>
                    <td className="td-id">#{log.id}</td>
                    <td>
                      {log.timestamp
                        ? new Date(log.timestamp).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })
                        : "-"}
                    </td>
                    <td>
                      <span className={`badge ${log.actionBadge}`}>{log.actionText}</span>
                    </td>
                    <td>{log.tableText}</td>
                    <td>{log.recordId || "-"}</td>
                    <td>{log.actorText}</td>
                    <td className="admin-log-summary">{log.summaryText}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        )}
      </div>

      <div className="table-toolbar">
        <button className="btn-ghost" onClick={() => changePage(-1)} disabled={filters.page <= 1}>
          Trang trước
        </button>
        <span className="result-count">
          Trang {filters.page} / {totalPages}
        </span>
        <button className="btn-ghost" onClick={() => changePage(1)} disabled={filters.page >= totalPages}>
          Trang sau
        </button>
      </div>
    </div>
  );
}
