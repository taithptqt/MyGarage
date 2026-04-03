import { useCallback, useEffect, useMemo, useState } from "react";
import { getAdminChangeLogs } from "../api/auditLogApi";
import "./AdminPage.css";
import "./AdminChangeLogPage.css";

const ACTION_OPTIONS = [
  { value: "", label: "Tat ca hanh dong" },
  { value: "CREATE", label: "Them moi" },
  { value: "UPDATE", label: "Cap nhat" },
  { value: "DELETE", label: "Xoa" },
];

const TABLE_LABELS = {
  Branches: "Chi nhanh",
  Staffs: "Nhan su",
  Customers: "Khach hang",
  Roles: "Vai tro",
  VehicleBrands: "Hang xe",
  VehicleModels: "Dong xe",
  Services: "Dich vu",
  SalaryCoefficients: "He so luong",
  SalaryRequests: "Yeu cau luong",
  SalaryProfiles: "Ho so luong",
  Warehouses: "Kho",
  InventoryItems: "Vat tu",
  Appointments: "Lich hen",
};

const FIELD_LABELS = {
  fullName: "Ho ten",
  name: "Ten",
  code: "Ma",
  status: "Trang thai",
  isActive: "Kich hoat",
  email: "Email",
  phoneNumber: "So dien thoai",
  address: "Dia chi",
  roleId: "Vai tro",
  roleName: "Ten vai tro",
  branchId: "Chi nhanh",
  branchName: "Ten chi nhanh",
  baseSalary: "Luong co ban",
  salaryCoefficient: "He so luong",
  allowance: "Phu cap",
  note: "Ghi chu",
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
  if (normalized === "CREATE") return "Them moi";
  if (normalized === "UPDATE") return "Cap nhat";
  if (normalized === "DELETE") return "Xoa";
  return normalized || "Khac";
}

function actionBadgeClass(action) {
  const normalized = String(action || "").toUpperCase();
  if (normalized === "DELETE") return "badge-deleted";
  if (normalized === "UPDATE") return "badge-suspended";
  return "badge-active";
}

function tableLabel(tableName) {
  const key = String(tableName || "");
  return TABLE_LABELS[key] || key || "Khac";
}

function userTypeLabel(type) {
  const normalized = String(type || "").toUpperCase();
  if (normalized === "STAFF") return "Nhan vien";
  if (normalized === "CUSTOMER") return "Khach hang";
  if (normalized === "ADMIN") return "Quan tri";
  if (normalized === "SYSTEM") return "He thong";
  return type || "Nguoi dung";
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
  if (!key) return "Truong";
  if (FIELD_LABELS[key]) return FIELD_LABELS[key];
  const withSpaces = String(key).replace(/([a-z])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ");
  return withSpaces.charAt(0).toUpperCase() + withSpaces.slice(1);
}

function compactString(value) {
  const text = String(value || "").trim();
  if (!text) return "trong";
  return text.length > 45 ? `${text.slice(0, 45)}...` : text;
}

function formatValue(value) {
  if (value === null || value === undefined || value === "") return "trong";
  if (typeof value === "boolean") return value ? "Bat" : "Tat";
  if (typeof value === "number") return Number.isFinite(value) ? value.toLocaleString("vi-VN") : String(value);
  if (Array.isArray(value)) return `${value.length} muc`;
  if (typeof value === "object") {
    const nameLike = value.name ?? value.fullName ?? value.code ?? value.email;
    return nameLike ? compactString(nameLike) : "doi tuong";
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
  if (!keys.length) return "Khong co thong tin chi tiet";
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

  if (!changedKeys.length) return "Cap nhat thong tin";

  const top = changedKeys.slice(0, 3).map((key) => {
    const oldVal = formatValue(oldRecord[key]);
    const newVal = formatValue(newRecord[key]);
    return `${toLabelFromKey(key)}: ${oldVal} -> ${newVal}`;
  });
  const remain = changedKeys.length - top.length;
  if (remain > 0) top.push(`+${remain} thay doi khac`);
  return top.join(" | ");
}

function buildSummary(log) {
  const action = String(log.action || "").toUpperCase();
  const oldRecord = normalizeRecord(safeParseJson(log.oldValue));
  const newRecord = normalizeRecord(safeParseJson(log.newValue));
  if (action === "CREATE") return `Da them moi: ${summarizeRecord(newRecord)}`;
  if (action === "DELETE") return `Da xoa: ${summarizeRecord(oldRecord)}`;
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
    () => [{ value: "", label: "Tat ca danh muc" }].concat(
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
      setError(err.message || "Khong tai duoc nhat ky thay doi.");
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
          <div className="stat-label">Tong ban ghi</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{filters.page}</div>
          <div className="stat-label">Trang hien tai</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{totalPages}</div>
          <div className="stat-label">Tong trang</div>
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
          placeholder="Tim theo ten, ma ban ghi, noi dung thay doi..."
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
          Loc
        </button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Dang tai nhat ky...</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Ma log</th>
                <th>Thoi gian</th>
                <th>Hanh dong</th>
                <th>Danh muc</th>
                <th>Ma ban ghi</th>
                <th>Nguoi thao tac</th>
                <th>Noi dung thay doi</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={7}>
                    <div className="empty-state">
                      <p>Chua co ban ghi phu hop.</p>
                    </div>
                  </td>
                </tr>
              ) : (
                rows.map((log) => (
                  <tr key={log.id}>
                    <td className="td-id">#{log.id}</td>
                    <td>{log.timestamp ? new Date(log.timestamp).toLocaleString() : "-"}</td>
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
          Trang truoc
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
