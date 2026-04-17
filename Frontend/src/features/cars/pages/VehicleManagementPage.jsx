import { useCallback, useEffect, useMemo, useState } from "react";
import { Modal } from "../../admin/components/AdminModals";
import { useSearch } from "@context/SearchContext";
import { useBranch } from "../../../context/BranchContext";
import { vehicleAdminApi } from "../api/vehicleAdminApi";
import { bayApi } from "../../storage/api/bayApi";
import { VIETNAM_LICENSE_PLATE_PATTERN, VIETNAM_LICENSE_PLATE_TITLE } from "@shared/utils/licensePlateValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import { VIETNAM_VIN_PATTERN, VIETNAM_VIN_TITLE, formatVinInput } from "@shared/utils/vinValidation";
import "../../admin/pages/AdminPage.css";
import "./VehicleManagementPage.css";

const STATUS_OPTIONS = [
  { value: "WAITING_RECEIVE", label: "Chờ tiếp nhận" },
  { value: "RECEIVED", label: "Đã tiếp nhận" },
  { value: "DIAGNOSING", label: "Đang kiểm tra" },
  { value: "REPAIRING", label: "Đang sửa" },
  { value: "WAITING_PARTS", label: "Chờ phụ tùng" },
  { value: "WAITING_DELIVERY", label: "Chờ giao" },
  { value: "COMPLETED", label: "Hoàn tất" },
  { value: "DELIVERED", label: "Đã giao" },
  { value: "CANCELED", label: "Hủy xử lý" },
];

const TABS = [
  { id: "list", label: "Danh sách & thông tin" },
  { id: "status", label: "Trạng thái xe" },
  { id: "location", label: "Vị trí xe" },
  { id: "assignment", label: "Phân công" },
  { id: "history", label: "Lịch sử xe" },
  { id: "alerts", label: "Cảnh báo" },
  { id: "stats", label: "Thống kê" },
];

const EMPTY_FORM = {
  licensePlate: "",
  vin: "",
  modelId: "",
  color: "",
  manufactureYear: "",
  odo: "",
  customerId: "",
  branchId: "",
  status: "WAITING_RECEIVE",
  version: "",
  engineNumber: "",
  chassisNumber: "",
  initialCondition: "",
  technicalTeam: "",
  expectedArea: "",
  note: "",
};

function removeDiacritics(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\u0111/g, "d")
    .replace(/\u0110/g, "D");
}

function normalizeBranchName(value) {
  const normalized = removeDiacritics(value)
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();

  if (!normalized) return "";

  if (["hcm", "tp hcm", "tphcm", "ho chi minh", "sai gon", "saigon"].includes(normalized)) {
    return "hcm";
  }
  if (["ha noi", "hanoi"].includes(normalized)) {
    return "ha noi";
  }
  if (["da nang", "danang"].includes(normalized)) {
    return "da nang";
  }

  return normalized;
}

function scoreBranch(branch) {
  let score = 0;
  const name = String(branch?.name || "");
  if (removeDiacritics(name) !== name) score += 2;
  if (String(branch?.status || "").toLowerCase() === "active") score += 1;
  return score;
}

function pickPreferredBranch(current, candidate) {
  if (!current) return candidate;
  const currentScore = scoreBranch(current);
  const candidateScore = scoreBranch(candidate);
  if (candidateScore > currentScore) return candidate;
  if (candidateScore < currentScore) return current;
  return Number(candidate.id) < Number(current.id) ? candidate : current;
}

function dedupeBranches(list) {
  const deduped = new Map();

  for (const branch of list || []) {
    if (!branch || branch.isDeleted === true) continue;
    const key = normalizeBranchName(branch.name) || `id:${branch.id}`;
    const existing = deduped.get(key);
    deduped.set(key, pickPreferredBranch(existing, branch));
  }

  return Array.from(deduped.values()).sort((a, b) =>
    String(a?.name || "").localeCompare(String(b?.name || ""), "vi")
  );
}

function normalizeTabId(value) {
  const id = String(value || "").trim().toLowerCase();
  return TABS.some((item) => item.id === id) ? id : "list";
}

function toNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function formatDateTime(value) {
  if (!value) return "-";
  const dt = new Date(value);
  return Number.isNaN(dt.getTime())
    ? "-"
    : dt.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
}

function formatHours(value) {
  return `${toNumber(value, 0).toFixed(1)}h`;
}

function statusBadgeClass(status) {
  if (status === "WAITING_PARTS") return "badge-warning";
  if (status === "WAITING_DELIVERY") return "badge-suspended";
  if (status === "COMPLETED" || status === "DELIVERED") return "badge-active";
  if (status === "CANCELED") return "badge-inactive";
  return "badge-active";
}

function VehicleForm({ initial, lookups, onSubmit, onCancel, saving }) {
  const [form, setForm] = useState({
    ...initial,
    modelId: initial.modelId ? String(initial.modelId) : "",
    customerId: initial.customerId ? String(initial.customerId) : "",
    branchId: initial.branchId ? String(initial.branchId) : "",
    manufactureYear: initial.manufactureYear ?? "",
    odo: initial.odo ?? "",
  });
  const isCreateMode = !initial?.id;

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({
      ...prev,
      [name]:
        name === "vin"
            ? formatVinInput(value)
            : formatLicensePlateByFieldName(name, value),
    }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      licensePlate: formatLicensePlateByFieldName("licensePlate", form.licensePlate || ""),
      vin: formatVinInput(form.vin || "") || null,
      modelId: Number(form.modelId),
      color: String(form.color || "").trim() || null,
      manufactureYear: form.manufactureYear === "" ? null : Number(form.manufactureYear),
      odo: form.odo === "" ? null : Number(form.odo),
      customerId: form.customerId ? Number(form.customerId) : null,
      branchId: form.branchId ? Number(form.branchId) : null,
      status: form.status || "WAITING_RECEIVE",
      version: String(form.version || "").trim() || null,
      engineNumber: String(form.engineNumber || "").trim() || null,
      chassisNumber: String(form.chassisNumber || "").trim() || null,
      initialCondition: String(form.initialCondition || "").trim() || null,
      technicalTeam: String(form.technicalTeam || "").trim() || null,
      expectedArea: String(form.expectedArea || "").trim() || null,
      note: String(form.note || "").trim() || null,
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="bay-form-row">
        <div className="form-group">
          <label>Biá»ƒn sá»‘ *</label>
          <input name="licensePlate" value={form.licensePlate || ""} onChange={handleChange} pattern={VIETNAM_LICENSE_PLATE_PATTERN} title={VIETNAM_LICENSE_PLATE_TITLE} style={{ textTransform: "uppercase" }} required />
        </div>
        <div className="form-group">
          <label>VIN (tùy chọn)</label>
          <input
            name="vin"
            value={form.vin || ""}
            onChange={handleChange}
            pattern={VIETNAM_VIN_PATTERN}
            title={VIETNAM_VIN_TITLE}
            maxLength={17}
            style={{ textTransform: "uppercase" }}
          />
        </div>
      </div>

      <div className="bay-form-row">
        <div className="form-group">
          <label>Dòng xe *</label>
          <select name="modelId" value={form.modelId} onChange={handleChange} required>
            <option value="">Chọn dòng xe</option>
            {(lookups.models || []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.brandName} - {item.modelName}
              </option>
            ))}
          </select>
        </div>
        <div className="form-group">
          <label>Trạng thái</label>
          <select name="status" value={form.status || "WAITING_RECEIVE"} onChange={handleChange}>
            {STATUS_OPTIONS.map((status) => (
              <option key={status.value} value={status.value}>
                {status.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="bay-form-row">
        <div className="form-group">
          <label>Màu xe (tùy chọn)</label>
          <input name="color" value={form.color || ""} onChange={handleChange} />
        </div>
        <div className="form-group">
          <label>Năm SX{isCreateMode ? " *" : ""}</label>
          <input
            name="manufactureYear"
            type="number"
            min="1900"
            max="2100"
            value={form.manufactureYear}
            onChange={handleChange}
            required={isCreateMode}
          />
        </div>
      </div>

      <div className="bay-form-row">
        <div className="form-group">
          <label>Odo</label>
          <input name="odo" type="number" min="0" value={form.odo} onChange={handleChange} />
        </div>
        <div className="form-group">
          <label>Chi nhánh</label>
          <select name="branchId" value={form.branchId} onChange={handleChange}>
            <option value="">Không gắn chi nhánh</option>
            {(lookups.branches || []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="bay-form-row">
        <div className="form-group">
          <label>Số máy</label>
          <input name="engineNumber" value={form.engineNumber || ""} onChange={handleChange} />
        </div>
        <div className="form-group">
          <label>Số khung</label>
          <input name="chassisNumber" value={form.chassisNumber || ""} onChange={handleChange} />
        </div>
      </div>

      <div className="form-group">
        <label>Mô tả tình trạng ban đầu</label>
        <input name="initialCondition" value={form.initialCondition || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Ghi chú</label>
        <input name="note" value={form.note || ""} onChange={handleChange} />
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={saving}>
          {saving ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export default function VehicleManagementPage({ section }) {
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const [activeTab, setActiveTab] = useState("list");
  const forcedTab = normalizeTabId(section);
  const currentTab = section ? forcedTab : activeTab;
  const showTabs = !section;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [feedback, setFeedback] = useState("");
  const [formError, setFormError] = useState("");
  const [modal, setModal] = useState(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);

  const [lookups, setLookups] = useState({
    branches: [],
    brands: [],
    models: [],
    bays: [],
    staffs: [],
    colorOptions: [],
    yearOptions: [],
    statusOptions: [],
  });
  const [rows, setRows] = useState([]);
  const [summary, setSummary] = useState(null);
  const [alerts, setAlerts] = useState({ items: [], total: 0 });
  const [history, setHistory] = useState({ items: [], total: 0 });

  const [selectedVehicleId, setSelectedVehicleId] = useState(null);
  const [selectedDetail, setSelectedDetail] = useState(null);
  const [suggestions, setSuggestions] = useState([]);

  const [filters, setFilters] = useState({
    branchId: "",
    brandId: "",
    modelId: "",
    color: "",
    manufactureYear: "",
    status: "",
    bayId: "",
    staffId: "",
    sortBy: "checkin",
    sortDirection: "desc",
    includeHidden: false,
  });

  const refreshLookups = useCallback(async () => {
    const data = await vehicleAdminApi.getLookups();
    setLookups({
      branches: dedupeBranches(data?.branches || []),
      brands: data?.brands || [],
      models: data?.models || [],
      bays: data?.bays || [],
      staffs: data?.staffs || [],
      colorOptions: data?.colorOptions || [],
      yearOptions: data?.yearOptions || [],
      statusOptions: data?.statusOptions || [],
    });
  }, []);

  const refreshDashboard = useCallback(async () => {
    const branchId = filters.branchId ? Number(filters.branchId) : null;
    const commonParams = branchId ? { branchId } : {};
    const listParams = {
      ...commonParams,
      search: searchQuery || undefined,
      brandId: filters.brandId || undefined,
      modelId: filters.modelId || undefined,
      color: filters.color || undefined,
      manufactureYear: filters.manufactureYear || undefined,
      status: filters.status || undefined,
      bayId: filters.bayId || undefined,
      staffId: filters.staffId || undefined,
      sortBy: filters.sortBy,
      sortDirection: filters.sortDirection,
      includeHidden: filters.includeHidden,
    };

    setLoading(true);
    setError("");
    try {
      const [listRes, summaryRes, alertsRes, historyRes] = await Promise.all([
        vehicleAdminApi.getList(listParams),
        vehicleAdminApi.getSummary(commonParams),
        vehicleAdminApi.getAlerts(commonParams),
        vehicleAdminApi.getHistory(commonParams),
      ]);

      const listRows = listRes?.items || [];
      setRows(listRows);
      setSummary(summaryRes || null);
      setAlerts(alertsRes || { items: [], total: 0 });
      setHistory(historyRes || { items: [], total: 0 });

      if (listRows.length === 0) {
        setSelectedVehicleId(null);
      } else if (!selectedVehicleId || !listRows.some((item) => Number(item.id) === Number(selectedVehicleId))) {
        setSelectedVehicleId(listRows[0].id);
      }
    } catch (err) {
      setError(err.message || "Không thể tải module Xe.");
    } finally {
      setLoading(false);
    }
  }, [filters, searchQuery, selectedVehicleId]);

  const loadVehicleDetail = useCallback(async (vehicleId) => {
    if (!vehicleId) return;
    try {
      const detail = await vehicleAdminApi.getVehicleDetail(vehicleId, { takeHistory: 200 });
      setSelectedDetail(detail || null);
    } catch (err) {
      setError(err.message || "Không thể tải chi tiết xe.");
    }
  }, []);

  useEffect(() => {
    refreshLookups().catch((err) => setError(err.message || "Không thể tải dữ liệu tham chiếu."));
  }, [refreshLookups]);

  useEffect(() => {
    if (!filters.branchId && selectedBranchId) {
      setFilters((prev) => ({ ...prev, branchId: String(selectedBranchId) }));
    }
  }, [selectedBranchId, filters.branchId]);

  useEffect(() => {
    refreshDashboard();
  }, [refreshDashboard]);

  useEffect(() => {
    if (selectedVehicleId) {
      loadVehicleDetail(selectedVehicleId);
    } else {
      setSelectedDetail(null);
    }
  }, [selectedVehicleId, loadVehicleDetail]);

  const runMutation = useCallback(async (task, successMessage, options = {}) => {
    setSaving(true);
    setError("");
    setFormError("");
    setFeedback("");
    try {
      await task();
      setFeedback(successMessage || "Thành công.");
      await refreshDashboard();
      if (options.reloadDetail !== false && (options.reloadVehicleId || selectedVehicleId)) {
        await loadVehicleDetail(options.reloadVehicleId || selectedVehicleId);
      }
    } catch (err) {
      const message = err.message || "Không thể thực hiện thao tác.";
      if (options.asFormError) setFormError(message);
      else setError(message);
      throw err;
    } finally {
      setSaving(false);
    }
  }, [loadVehicleDetail, refreshDashboard, selectedVehicleId]);

  const openCreateModal = () => {
    setFormError("");
    setModal({ mode: "create", item: { ...EMPTY_FORM, branchId: filters.branchId || selectedBranchId || "" } });
  };

  const openEditModal = (item) => {
    setFormError("");
    setModal({
      mode: "edit",
      item: {
        id: item.id,
        licensePlate: item.licensePlate || "",
        vin: item.vin || "",
        modelId: item.modelId || "",
        color: item.color || "",
        manufactureYear: item.manufactureYear || "",
        odo: item.odo || "",
        customerId: item.customerId || "",
        branchId: item.branchId || "",
        status: item.currentStatus || "WAITING_RECEIVE",
        version: item.version || "",
        engineNumber: item.engineNumber || "",
        chassisNumber: item.chassisNumber || "",
        initialCondition: item.initialCondition || "",
        technicalTeam: item.technicalTeam || "",
        expectedArea: item.expectedArea || "",
        note: "",
      },
    });
  };

  const closeModal = () => {
    setModal(null);
    setFormError("");
  };

  const handleSaveVehicle = async (payload) => {
    try {
      if (modal?.mode === "edit" && modal?.item?.id) {
        await runMutation(() => vehicleAdminApi.updateVehicle(modal.item.id, payload), "Cập nhật xe thành công.", { asFormError: true });
      } else {
        await runMutation(() => vehicleAdminApi.createVehicle(payload), "Tạo xe mới thành công.", { asFormError: true });
      }
      closeModal();
    } catch {
      // keep modal open
    }
  };

  const statusLabelMap = useMemo(() => Object.fromEntries(STATUS_OPTIONS.map((item) => [item.value, item.label])), []);
  const availableStatusOptions = useMemo(() => {
    const options = lookups.statusOptions || [];
    if (options.length === 0) return STATUS_OPTIONS;
    return options.map((value) => ({ value, label: statusLabelMap[value] || value }));
  }, [lookups.statusOptions, statusLabelMap]);
  const selectedRow = useMemo(() => rows.find((item) => Number(item.id) === Number(selectedVehicleId)) || null, [rows, selectedVehicleId]);
  const selectedVehicle = selectedDetail?.vehicle || selectedRow || null;
  const statusHistoryItems = selectedDetail?.statusHistory || [];
  const isListTab = currentTab === "list";
  const isStatusTab = currentTab === "status";
  const isLocationTab = currentTab === "location";
  const isAssignmentTab = currentTab === "assignment";
  const showCompactFilter = isStatusTab || isLocationTab || isAssignmentTab;
  const workloadByStaff = useMemo(() => {
    const map = new Map();
    (rows || []).forEach((item) => {
      if (!item.currentStaffId) return;
      const key = Number(item.currentStaffId);
      const current = map.get(key) || { staffId: key, staffName: item.currentStaffName || `#${key}`, total: 0 };
      current.total += 1;
      map.set(key, current);
    });
    return Array.from(map.values()).sort((a, b) => b.total - a.total);
  }, [rows]);

  return (
    <div className="management-content bay-admin-page vehicle-admin-page">
      <header className="admin-header">
        <div>
          <h1>Quản lý xe</h1>
          <p className="header-sub">Theo dõi danh sách, trạng thái, vị trí khoang, phân công và cảnh báo xe trong xưởng.</p>
        </div>
        <div className="bay-header-actions">
          <button className="btn-ghost" type="button" onClick={refreshDashboard}>Tải lại</button>
          {!isStatusTab && (
            <button className="btn-primary" type="button" onClick={openCreateModal}>+ Tạo xe</button>
          )}
        </div>
      </header>

      {summary && (
        <div className="stats-bar bay-stat-grid">
          <div className="stat-card"><div className="stat-value">{summary.totalVehicles || 0}</div><div className="stat-label">Tổng xe</div></div>
          <div className="stat-card"><div className="stat-value">{summary.inShopVehicles || 0}</div><div className="stat-label">Đang trong xưởng</div></div>
          <div className="stat-card"><div className="stat-value">{summary.noBayCount || 0}</div><div className="stat-label">Chưa gắn khoang</div></div>
          <div className="stat-card"><div className="stat-value">{summary.noStaffCount || 0}</div><div className="stat-label">Chưa phân công</div></div>
        </div>
      )}

      {isListTab && (
        <>
          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.branchId} onChange={(e) => setFilters((prev) => ({ ...prev, branchId: e.target.value }))}>
              <option value="">Tất cả chi nhánh</option>
              {(lookups.branches || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="search-input" value={filters.brandId} onChange={(e) => setFilters((prev) => ({ ...prev, brandId: e.target.value, modelId: "" }))}>
              <option value="">Tất cả hãng xe</option>
              {(lookups.brands || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="search-input" value={filters.modelId} onChange={(e) => setFilters((prev) => ({ ...prev, modelId: e.target.value }))}>
              <option value="">Tất cả dòng xe</option>
              {(lookups.models || []).filter((item) => !filters.brandId || Number(item.brandId) === Number(filters.brandId)).map((item) => (
                <option key={item.id} value={item.id}>{item.brandName} - {item.modelName}</option>
              ))}
            </select>
            <select className="search-input" value={filters.manufactureYear} onChange={(e) => setFilters((prev) => ({ ...prev, manufactureYear: e.target.value }))}>
              <option value="">Tất cả năm SX</option>
              {(lookups.yearOptions || []).map((year) => <option key={year} value={year}>{year}</option>)}
            </select>
          </div>

          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.color} onChange={(e) => setFilters((prev) => ({ ...prev, color: e.target.value }))}>
              <option value="">Tất cả màu xe</option>
              {(lookups.colorOptions || []).map((color) => <option key={color} value={color}>{color}</option>)}
            </select>
            <select className="search-input" value={filters.status} onChange={(e) => setFilters((prev) => ({ ...prev, status: e.target.value }))}>
              <option value="">Tất cả trạng thái</option>
              {availableStatusOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
            <select className="search-input" value={filters.bayId} onChange={(e) => setFilters((prev) => ({ ...prev, bayId: e.target.value }))}>
              <option value="">Tất cả khoang</option>
              {(lookups.bays || []).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
            </select>
            <select className="search-input" value={filters.staffId} onChange={(e) => setFilters((prev) => ({ ...prev, staffId: e.target.value }))}>
              <option value="">Tất cả nhân sự</option>
              {(lookups.staffs || []).map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
            <select className="search-input" value={filters.sortBy} onChange={(e) => setFilters((prev) => ({ ...prev, sortBy: e.target.value }))}>
              <option value="checkin">Sắp xếp: vào xưởng</option>
              <option value="brand_model">Sắp xếp: hãng/dòng</option>
              <option value="code">Sắp xếp: mã xe</option>
            </select>
            <select className="search-input" value={filters.sortDirection} onChange={(e) => setFilters((prev) => ({ ...prev, sortDirection: e.target.value }))}>
              <option value="desc">Giảm dần</option>
              <option value="asc">Tăng dần</option>
            </select>
            <label className="bay-checkbox bay-inline-checkbox">
              <input type="checkbox" checked={filters.includeHidden} onChange={(e) => setFilters((prev) => ({ ...prev, includeHidden: e.target.checked }))} />
              <span>Hiện xe đã ẩn</span>
            </label>
          </div>
        </>
      )}

      {showCompactFilter && (
        <>
          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.branchId} onChange={(e) => setFilters((prev) => ({ ...prev, branchId: e.target.value }))}>
              <option value="">Tất cả chi nhánh</option>
              {(lookups.branches || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="search-input" value={filters.status} onChange={(e) => setFilters((prev) => ({ ...prev, status: e.target.value }))}>
              <option value="">Tất cả trạng thái</option>
              {availableStatusOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
            <select className="search-input" value={filters.staffId} onChange={(e) => setFilters((prev) => ({ ...prev, staffId: e.target.value }))}>
              <option value="">Tất cả nhân sự</option>
              {(lookups.staffs || []).map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
            <select className="search-input" value={filters.bayId} onChange={(e) => setFilters((prev) => ({ ...prev, bayId: e.target.value }))}>
              <option value="">Tất cả khoang</option>
              {(lookups.bays || []).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
            </select>
          </div>

          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.sortBy} onChange={(e) => setFilters((prev) => ({ ...prev, sortBy: e.target.value }))}>
              <option value="status">Sắp xếp: trạng thái</option>
              <option value="checkin">Sắp xếp: vào xưởng</option>
              <option value="code">Sắp xếp: mã xe</option>
            </select>
            <select className="search-input" value={filters.sortDirection} onChange={(e) => setFilters((prev) => ({ ...prev, sortDirection: e.target.value }))}>
              <option value="desc">Giảm dần</option>
              <option value="asc">Tăng dần</option>
            </select>
            <label className="bay-checkbox bay-inline-checkbox">
              <input type="checkbox" checked={filters.includeHidden} onChange={(e) => setFilters((prev) => ({ ...prev, includeHidden: e.target.checked }))} />
              <span>Hiện xe đã ẩn</span>
            </label>
          </div>
        </>
      )}

      <div className="table-toolbar"><span className="result-count">{rows.length} xe | Module: {isListTab ? "Danh sách & thông tin" : isStatusTab ? "Trạng thái xe" : "Quản lý xe"} | Tìm kiếm: {searchQuery || "(trống)"}</span></div>
      {feedback && <div className="alert bay-alert-success">{feedback}</div>}
      {(error || formError) && <div className="alert alert-error">{error || formError}</div>}

      {showTabs && (
        <div className="bay-tabs">
          {TABS.map((tab) => (
            <button key={tab.id} type="button" className={`bay-tab-btn ${currentTab === tab.id ? "active" : ""}`} onClick={() => setActiveTab(tab.id)}>
              {tab.label}
            </button>
          ))}
        </div>
      )}

      {loading ? (
        <div className="loading-state"><div className="spinner" /><p>Đang tải dữ liệu xe...</p></div>
      ) : (
        <>
          {isListTab && (
            <div className="list-tab-container" style={{ padding: "0 28px 28px" }}>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Mã xe</th>
                      <th>Biển số</th>
                      <th>Hãng / dòng</th>
                      <th>VIN</th>
                      <th>Màu / Năm SX</th>
                      <th>Chi nhánh</th>
                      <th>Khách hàng</th>
                      <th>Cập nhật</th>
                      <th className="th-center">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={9}><div className="empty-state"><p>Chưa có xe nào.</p></div></td></tr>
                    ) : (
                      rows.map((item) => (
                        <tr key={item.id} className={Number(selectedVehicleId) === Number(item.id) ? "bay-row-selected" : ""} onClick={() => { setSelectedVehicleId(item.id); setIsDetailModalOpen(true); }}>
                          <td className="td-id">{item.vehicleCode}</td>
                          <td>{item.licensePlate}</td>
                          <td>{item.brandName} {item.modelName}</td>
                          <td>{item.vin || "-"}</td>
                          <td>{item.color || "-"} {item.manufactureYear ? `| ${item.manufactureYear}` : ""}</td>
                          <td>{item.branchName || "-"}</td>
                          <td>
                            <div>{item.customerName || "-"}</div>
                            {item.customerPhone && <div className="result-count">{item.customerPhone}</div>}
                          </td>
                          <td>{formatDateTime(item.updatedAt)}</td>
                          <td className="td-center">
                            <div className="row-actions bay-row-actions">
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); openEditModal(item); }}>Sửa</button>
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); runMutation(() => item.isEditLocked ? vehicleAdminApi.unlockEdit(item.id) : vehicleAdminApi.lockEdit(item.id), item.isEditLocked ? "Đã mở khóa sửa xe." : "Đã khóa sửa xe.").catch(() => {}); }}>{item.isEditLocked ? "Mở khóa" : "Khóa"}</button>
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); runMutation(() => vehicleAdminApi.hideVehicle(item.id), "Đã ẩn xe.").catch(() => {}); }}>Ẩn</button>
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {isStatusTab && (
            <div className="vehicle-grid-two">
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Mã xe</th>
                      <th>Biển số</th>
                      <th>Hãng / dòng</th>
                      <th>Trạng thái</th>
                      <th>Giờ trạng thái</th>
                      <th>Giờ trong xưởng</th>
                      <th>Khoang</th>
                      <th>Nhân sự</th>
                      <th className="th-center">Cập nhật</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={9}><div className="empty-state"><p>Chưa có xe nào.</p></div></td></tr>
                    ) : (
                      rows.map((item) => (
                        <tr key={item.id} className={Number(selectedVehicleId) === Number(item.id) ? "bay-row-selected" : ""} onClick={() => setSelectedVehicleId(item.id)}>
                          <td className="td-id">{item.vehicleCode}</td>
                          <td>{item.licensePlate}</td>
                          <td>{item.brandName} {item.modelName}</td>
                          <td><span className={`badge ${statusBadgeClass(item.currentStatus)}`}>{statusLabelMap[item.currentStatus] || item.currentStatus}</span></td>
                          <td>{formatHours(item.statusDurationHours)}</td>
                          <td>{formatHours(item.inShopHours)}</td>
                          <td>{item.currentBayCode ? `${item.currentBayCode} - ${item.currentBayName}` : "(chưa gắn)"}</td>
                          <td>{item.currentStaffName || "-"}</td>
                          <td className="td-center">
                            <select className="bay-inline-select" value={item.currentStatus || "WAITING_RECEIVE"} onClick={(e) => e.stopPropagation()} onChange={(e) => runMutation(() => vehicleAdminApi.updateStatus(item.id, e.target.value), "Đã cập nhật trạng thái xe.").catch(() => {})}>
                              {availableStatusOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                            </select>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              <div className="bay-panel">
                <div className="bay-panel-head"><h3>Lịch sử trạng thái</h3></div>
                <p className="result-count">
                  {selectedVehicle ? `Xe: ${selectedVehicle.vehicleCode} - ${selectedVehicle.licensePlate}` : "Chưa chọn xe"}
                </p>
                {statusHistoryItems.length === 0 ? (
                  <div className="empty-state"><p>Chưa có lịch sử trạng thái.</p></div>
                ) : (
                  <div className="vehicle-status-timeline">
                    {statusHistoryItems.slice(0, 20).map((event, idx) => (
                      <div key={`${event.changedAt}-${idx}`} className="vehicle-status-item">
                        <p className="vehicle-status-title">
                          {statusLabelMap[event.oldStatus] || event.oldStatus} ? {statusLabelMap[event.newStatus] || event.newStatus}
                        </p>
                        <p className="vehicle-status-meta">{formatDateTime(event.changedAt)} | {event.changedByName || "Hệ thống"}</p>
                        <p className="vehicle-status-note">{event.note || "Không có ghi chú."}</p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {isLocationTab && (
            <div className="vehicle-grid-two">
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Biển số</th>
                      <th>Dòng xe</th>
                      <th>Khoang hiện tại</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={3}><div className="empty-state"><p>Chưa có xe nào.</p></div></td></tr>
                    ) : (
                      rows.map((item) => (
                        <tr key={item.id} className={Number(selectedVehicleId) === Number(item.id) ? "bay-row-selected" : ""} onClick={() => { setSelectedVehicleId(item.id); setSuggestions([]); }}>
                          <td style={{ fontWeight: '600' }}>{item.licensePlate}</td>
                          <td>{item.modelName}</td>
                          <td>{item.currentBayCode ? <span className="badge badge-active">{item.currentBayCode}</span> : "-"}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div className="vehicle-action-panel">
                <div className="bay-panel">
                  <div className="bay-panel-head"><h3>Thông tin vị trí hiện tại</h3></div>
                  <div className="bay-meta-info">
                    <div className="bay-meta-row">
                      <strong>Xe:</strong> 
                      <span style={{ fontWeight: '600' }}>{selectedRow ? `${selectedRow.vehicleCode} - ${selectedRow.licensePlate}` : "(chưa chọn)"}</span>
                    </div>
                    <div className="bay-meta-row">
                      <strong>Trạng thái:</strong> 
                      <span className={`badge ${statusBadgeClass(selectedRow?.currentStatus)}`}>{selectedRow ? (statusLabelMap[selectedRow.currentStatus] || selectedRow.currentStatus) : "-"}</span>
                    </div>
                    <div className="bay-meta-row">
                      <strong>Khoang:</strong> 
                      <span className={selectedRow?.currentBayCode ? "badge badge-active" : "badge badge-inactive"}>
                        {selectedRow?.currentBayCode ? `${selectedRow.currentBayCode} - ${selectedRow.currentBayName}` : "Chưa có khoang"}
                      </span>
                    </div>
                    {selectedRow?.currentBayCode && (
                      <div className="bay-meta-row">
                        <strong>Lưu kho:</strong> 
                        <span style={{ fontWeight: '500' }}>{formatHours(selectedRow?.bayOccupiedHours)}</span>
                      </div>
                    )}
                  </div>
                </div>

                {selectedRow && !selectedRow.currentBayCode && (
                  <div className="bay-panel">
                    <div className="bay-panel-head"><h3>Gắn xe vào khoang mới</h3></div>
                    <form className="admin-form bay-mini-form" style={{ marginTop: '16px' }} onSubmit={(e) => {
                      e.preventDefault();
                      const bayId = Number(e.currentTarget.elements.assignBayId.value);
                      if (!selectedVehicleId || !bayId) return;
                      runMutation(() => vehicleAdminApi.assignBay(selectedVehicleId, { bayId }), "Đã gắn xe vào khoang.").catch(() => {});
                    }}>
                      <div className="form-group">
                        <select className="search-input" name="assignBayId" defaultValue="">
                          <option value="">-- ChẨn khoang trẨng --</option>
                          {(lookups.bays || []).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
                        </select>
                      </div>
                      <div className="form-actions" style={{ justifyContent: 'space-between', marginTop: '16px' }}>
                        <button className="btn-ghost" type="button" disabled={!selectedVehicleId || saving} onClick={async () => {
                           try {
                             setSaving(true);
                             const res = await bayApi.getSuggestions({ vehicleId: selectedVehicleId });
                             setSuggestions(res || []);
                             setFeedback("Đã tải gợi ý khoang.");
                           } catch(err) {
                             setError(err.message || "Lỗi khi lấy gợi ý khoang.");
                           } finally {
                             setSaving(false);
                           }
                        }}>Gợi ý (AI)</button>
                        <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving}>Gắn khoang</button>
                      </div>
                    </form>
                    
                    {suggestions && suggestions.length > 0 && (
                      <div className="suggestion-box">
                        <h4>Đề xuất phù hợp nhất</h4>
                        <div className="bay-suggestion-list">
                          {suggestions.map((s, idx) => (
                             <div key={idx} className="suggestion-item">
                                <div>
                                  <div className="suggestion-item-title">{s.bay?.code || s.bayCode} <span className="suggestion-item-score">Điểm: {s.score}</span></div>
                                  <div className="suggestion-item-reason">{s.reasons?.join(', ')}</div>
                                </div>
                                <button className="btn-ghost" type="button" disabled={saving} onClick={() => {
                                  runMutation(() => vehicleAdminApi.assignBay(selectedVehicleId, { bayId: s.bayId || s.bay?.id }), "Đã gắn tự động.").catch(() => {});
                                }}>Áp dụng</button>
                             </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {selectedRow?.currentBayCode && (
                  <div className="bay-panel">
                    <div className="bay-panel-head"><h3>Chuyển ra ngoài / điều phối</h3></div>
                    <form className="admin-form bay-mini-form" style={{ marginTop: '16px' }} onSubmit={(e) => {
                      e.preventDefault();
                      const toBayId = Number(e.currentTarget.elements.moveBayId.value);
                      if (!selectedVehicleId || !toBayId) return;
                      runMutation(() => vehicleAdminApi.moveBay(selectedVehicleId, { toBayId }), "Đã chuyển xe sang khoang mới.").catch(() => {});
                    }}>
                      <div className="form-group">
                        <select className="search-input" name="moveBayId" defaultValue="">
                          <option value="">-- Chọn khoang đích --</option>
                          {(lookups.bays || []).filter(b => b.code !== selectedRow.currentBayCode).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
                        </select>
                      </div>
                      <div className="form-actions" style={{ justifyContent: 'space-between', marginTop: '16px' }}>
                        <button className="btn-ghost" type="button" disabled={!selectedVehicleId || saving} onClick={() => runMutation(() => vehicleAdminApi.removeBay(selectedVehicleId, { note: "Chuyển ra ngoài xưởng" }), "Đã đưa ra khỏi khoang gốc.").catch(() => {})}>Đưa khỏi khoang</button>
                        <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving}>Điều phối ngay</button>
                      </div>
                    </form>
                  </div>
                )}
              </div>
            </div>
          )}

          {isAssignmentTab && (
            <div className="vehicle-grid-two">
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Biển số</th>
                      <th>Trạng thái</th>
                      <th>Người phụ trách</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={3}><div className="empty-state"><p>Chưa có xe nào.</p></div></td></tr>
                    ) : (
                      rows.map((item) => (
                        <tr key={item.id} className={Number(selectedVehicleId) === Number(item.id) ? "bay-row-selected" : ""} onClick={() => setSelectedVehicleId(item.id)}>
                          <td style={{ fontWeight: '600' }}>{item.licensePlate}</td>
                          <td><span className={`badge ${statusBadgeClass(item.currentStatus)}`}>{statusLabelMap[item.currentStatus] || item.currentStatus}</span></td>
                          <td>{item.currentStaffName ? <span style={{ fontWeight: '600' }}>{item.currentStaffName}</span> : <span style={{ color: 'var(--text-muted)' }}>(Chưa gắn)</span>}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div className="vehicle-action-panel">
                <div className="bay-panel">
                  <div className="bay-panel-head"><h3>Phân công nhân sự</h3></div>
                  <div className="bay-meta-info" style={{ marginBottom: '16px' }}>
                    <div className="bay-meta-row" style={{ borderBottom: 'none', paddingBottom: 0 }}>
                      <strong>Xe:</strong> 
                      <span style={{ fontWeight: '600' }}>{selectedRow ? `${selectedRow.vehicleCode} - ${selectedRow.licensePlate}` : "(chưa chọn)"}</span>
                    </div>
                  </div>
                  <form className="admin-form bay-mini-form" onSubmit={(e) => {
                    e.preventDefault();
                    const staffId = Number(e.currentTarget.elements.staffId.value) || null;
                    const technicalTeam = String(e.currentTarget.elements.technicalTeam.value || "").trim();
                    if (!selectedVehicleId) return;
                    runMutation(() => vehicleAdminApi.assignStaff(selectedVehicleId, { staffId, technicalTeam: technicalTeam || null }), "Đã cập nhật phân công xe.").catch(() => {});
                  }}>
                    <div className="form-group">
                      <label>Cố vấn dịch vụ / KTV chính</label>
                      <select className="search-input" name="staffId" defaultValue={selectedRow?.currentStaffId || ""}>
                        <option value="">-- Bộ phận công --</option>
                        {(lookups.staffs || []).map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
                      </select>
                    </div>
                    <div className="form-group">
                      <label>Tổ kỹ thuật hỗ trợ (tùy chọn)</label>
                      <input className="search-input" name="technicalTeam" defaultValue={selectedRow?.technicalTeam || ""} placeholder="VD: Tổ Đồng Sơn 1" />
                    </div>
                    <div className="form-actions" style={{ marginTop: '16px' }}>
                      <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving} style={{ width: '100%' }}>Lưu phân công</button>
                    </div>
                  </form>
                </div>
                
                <div className="bay-panel">
                  <div className="bay-panel-head" style={{ marginBottom: '16px' }}><h3>Workload Nhân Sự</h3></div>
                  <table className="admin-table">
                    <thead><tr><th>Nhân sự</th><th>Đang xử lý</th><th>Tình trạng</th></tr></thead>
                    <tbody>
                      {workloadByStaff.length === 0 ? <tr><td colSpan={3}><div className="empty-state" style={{ minHeight: '100px' }}><p>Chưa có dữ liệu.</p></div></td></tr> : workloadByStaff.map((item) => (
                        <tr key={item.staffId}>
                          <td style={{ fontWeight: '600' }}>{item.staffName}</td>
                          <td>{item.total} xe</td>
                          <td>{item.total > 5 ? <span className="badge badge-suspended">Quá tải</span> : <span className="badge badge-active">Ổn định</span>}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div className="alert alert-error" style={{ marginTop: '16px', margin: '16px 0 0 0' }}>
                    Cảnh báo nhân sự bị quá tải nếu đang xử lý > 5 xe.
                  </div>
                </div>
              </div>
            </div>
          )}

          {currentTab === "history" && (
            <div className="table-wrapper">
              <table className="admin-table">
                <thead><tr><th>Thời gian</th><th>Loại</th><th>Tiêu đề</th><th>Nội dung</th><th>Người thao tác</th></tr></thead>
                <tbody>
                  {(history.items || []).length === 0 ? <tr><td colSpan={5}><div className="empty-state"><p>Chưa có lịch sử.</p></div></td></tr> : history.items.map((item, idx) => (
                    <tr key={`${item.type}-${item.createdAt}-${idx}`}>
                      <td>{formatDateTime(item.createdAt)}</td><td>{item.type}</td><td>{item.title}</td><td>{item.description || "-"}</td><td>{item.actorName || "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {currentTab === "alerts" && (
            <div className="table-wrapper">
              <table className="admin-table">
                <thead><tr><th>Mức độ</th><th>Loại cảnh báo</th><th>Xe</th><th>Nội dung</th><th>Thời gian</th></tr></thead>
                <tbody>
                  {(alerts.items || []).length === 0 ? <tr><td colSpan={5}><div className="empty-state"><p>Không có cảnh báo.</p></div></td></tr> : alerts.items.map((item, idx) => (
                    <tr key={`${item.type}-${item.vehicleId}-${idx}`}>
                      <td><span className={`badge ${item.severity === "HIGH" ? "badge-warning" : item.severity === "MEDIUM" ? "badge-suspended" : "badge-inactive"}`}>{item.severity}</span></td>
                      <td>{item.type}</td>
                      <td>{item.vehicleCode} {item.licensePlate !== "-" ? `| ${item.licensePlate}` : ""}</td>
                      <td>{item.message}</td>
                      <td>{formatDateTime(item.createdAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {currentTab === "stats" && summary && (
            <div className="bay-grid-two">
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead><tr><th>Trạng thái</th><th>Số xe</th><th>Tỷ lệ</th></tr></thead>
                  <tbody>{(summary.statusBreakdown || []).map((item) => <tr key={item.status}><td>{statusLabelMap[item.status] || item.status}</td><td>{item.count}</td><td>{toNumber(item.rate, 0).toFixed(2)}%</td></tr>)}</tbody>
                </table>
              </div>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead><tr><th>Chi nhánh</th><th>Tổng xe</th><th>Đang xử lý</th></tr></thead>
                  <tbody>{(summary.byBranch || []).map((item) => <tr key={item.branchId}><td>{item.branchName}</td><td>{item.total}</td><td>{item.inShop}</td></tr>)}</tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {modal && (
        <Modal title={modal.mode === "edit" ? `Cập nhật xe #${modal.item?.id}` : "Tạo xe mới"} onClose={closeModal}>
          <VehicleForm initial={modal.item || EMPTY_FORM} lookups={lookups} onSubmit={handleSaveVehicle} onCancel={closeModal} saving={saving} />
        </Modal>
      )}

      {isDetailModalOpen && selectedVehicle && (
        <Modal title={`Thông tin xe: ${selectedVehicle.licensePlate || selectedVehicle.vehicleCode}`} onClose={() => setIsDetailModalOpen(false)}>
          <div className="vehicle-detail-grid" style={{ minWidth: '400px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', lineHeight: '1.6' }}>
            <p><strong>Mã xe:</strong> {selectedVehicle.vehicleCode || "-"}</p>
            <p><strong>Biển số:</strong> {selectedVehicle.licensePlate || "-"}</p>
            <p><strong>Hãng / dòng:</strong> {selectedVehicle.brandName} {selectedVehicle.modelName}</p>
            <p><strong>VIN:</strong> {selectedVehicle.vin || "-"}</p>
            <p><strong>Màu xe:</strong> {selectedVehicle.color || "-"}</p>
            <p><strong>Nam SX:</strong> {selectedVehicle.manufactureYear || "-"}</p>
            <p><strong>Trạng thái:</strong> <span className={`badge ${statusBadgeClass(selectedVehicle.currentStatus)}`}>{statusLabelMap[selectedVehicle.currentStatus] || selectedVehicle.currentStatus}</span></p>
            <p><strong>Giờ trong xưởng:</strong> {formatHours(selectedVehicle.inShopHours)}</p>
            <p><strong>Khoang:</strong> {selectedVehicle.currentBayCode ? <span className="badge badge-active">{selectedVehicle.currentBayCode} - {selectedVehicle.currentBayName}</span> : "(chưa gắn)"}</p>
            <p><strong>Nhân sự:</strong> {selectedVehicle.currentStaffName || "-"}</p>
            <p><strong>Tổ kỹ thuật:</strong> {selectedVehicle.technicalTeam || "-"}</p>
            <p><strong>Ngày cập nhật:</strong> {formatDateTime(selectedVehicle.updatedAt)}</p>
            <p><strong>Số máy:</strong> {selectedVehicle.engineNumber || "-"}</p>
            <p><strong>Số khung:</strong> {selectedVehicle.chassisNumber || "-"}</p>
            <p style={{ gridColumn: '1 / -1' }}><strong>Tình trạng ban đầu:</strong> {selectedVehicle.initialCondition || "-"}</p>
          </div>
          <div className="form-actions" style={{ marginTop: '24px', justifyContent: 'flex-end' }}>
            <button className="btn-primary" type="button" onClick={() => setIsDetailModalOpen(false)}>Đóng</button>
          </div>
        </Modal>
      )}
    </div>
  );
}




