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
  { value: "WAITING_RECEIVE", label: "Ch? ti?p nh?n" },
  { value: "RECEIVED", label: "ï¿½ï¿½ ti?p nh?n" },
  { value: "DIAGNOSING", label: "ï¿½ang ki?m tra" },
  { value: "REPAIRING", label: "ï¿½ang s?a" },
  { value: "WAITING_PARTS", label: "Ch? ph? tï¿½ng" },
  { value: "WAITING_DELIVERY", label: "Ch? giao" },
  { value: "COMPLETED", label: "Hoï¿½n t?t" },
  { value: "DELIVERED", label: "ï¿½ï¿½ giao" },
  { value: "CANCELED", label: "H?y x? lï¿½" },
];

const TABS = [
  { id: "list", label: "Danh sï¿½ch & thï¿½ng tin" },
  { id: "status", label: "Tr?ng thï¿½i xe" },
  { id: "location", label: "V? trï¿½ xe" },
  { id: "assignment", label: "Phï¿½n cï¿½ng" },
  { id: "history", label: "L?ch s? xe" },
  { id: "alerts", label: "C?nh bï¿½o" },
  { id: "stats", label: "Th?ng kï¿½" },
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
          <label>Biển số *</label>
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
          <label>Chi nhï¿½nh</label>
          <select name="branchId" value={form.branchId} onChange={handleChange}>
            <option value="">Khï¿½ng g?n chi nhï¿½nh</option>
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
          <label>S? mï¿½y</label>
          <input name="engineNumber" value={form.engineNumber || ""} onChange={handleChange} />
        </div>
        <div className="form-group">
          <label>S? khung</label>
          <input name="chassisNumber" value={form.chassisNumber || ""} onChange={handleChange} />
        </div>
      </div>

      <div className="form-group">
        <label>Mï¿½ t? tï¿½nh tr?ng ban d?u</label>
        <input name="initialCondition" value={form.initialCondition || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Ghi chï¿½</label>
        <input name="note" value={form.note || ""} onChange={handleChange} />
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          H?y
        </button>
        <button className="btn-primary" type="submit" disabled={saving}>
          {saving ? "ï¿½ang luu..." : "Luu"}
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
      setError(err.message || "Khï¿½ng th? t?i module Xe.");
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
      setError(err.message || "Khï¿½ng th? t?i chi ti?t xe.");
    }
  }, []);

  useEffect(() => {
    refreshLookups().catch((err) => setError(err.message || "Khï¿½ng th? t?i d? li?u tham chi?u."));
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
      setFeedback(successMessage || "Thï¿½nh cï¿½ng.");
      await refreshDashboard();
      if (options.reloadDetail !== false && (options.reloadVehicleId || selectedVehicleId)) {
        await loadVehicleDetail(options.reloadVehicleId || selectedVehicleId);
      }
    } catch (err) {
      const message = err.message || "Khï¿½ng th? th?c hi?n thao tï¿½c.";
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
        await runMutation(() => vehicleAdminApi.updateVehicle(modal.item.id, payload), "C?p nh?t xe thï¿½nh cï¿½ng.", { asFormError: true });
      } else {
        await runMutation(() => vehicleAdminApi.createVehicle(payload), "T?o xe m?i thï¿½nh cï¿½ng.", { asFormError: true });
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
          <h1>Qu?n lï¿½ xe</h1>
          <p className="header-sub">Theo dï¿½i danh sï¿½ch, tr?ng thï¿½i, v? trï¿½ khoang, phï¿½n cï¿½ng vï¿½ c?nh bï¿½o xe trong xu?ng.</p>
        </div>
        <div className="bay-header-actions">
          <button className="btn-ghost" type="button" onClick={refreshDashboard}>T?i l?i</button>
          {!isStatusTab && (
            <button className="btn-primary" type="button" onClick={openCreateModal}>+ T?o xe</button>
          )}
        </div>
      </header>

      {summary && (
        <div className="stats-bar bay-stat-grid">
          <div className="stat-card"><div className="stat-value">{summary.totalVehicles || 0}</div><div className="stat-label">T?ng xe</div></div>
          <div className="stat-card"><div className="stat-value">{summary.inShopVehicles || 0}</div><div className="stat-label">ï¿½ang trong xu?ng</div></div>
          <div className="stat-card"><div className="stat-value">{summary.noBayCount || 0}</div><div className="stat-label">Chua gï¿½n khoang</div></div>
          <div className="stat-card"><div className="stat-value">{summary.noStaffCount || 0}</div><div className="stat-label">Chua phï¿½n cï¿½ng</div></div>
        </div>
      )}

      {isListTab && (
        <>
          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.branchId} onChange={(e) => setFilters((prev) => ({ ...prev, branchId: e.target.value }))}>
              <option value="">T?t c? chi nhï¿½nh</option>
              {(lookups.branches || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="search-input" value={filters.brandId} onChange={(e) => setFilters((prev) => ({ ...prev, brandId: e.target.value, modelId: "" }))}>
              <option value="">T?t c? hï¿½ng xe</option>
              {(lookups.brands || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="search-input" value={filters.modelId} onChange={(e) => setFilters((prev) => ({ ...prev, modelId: e.target.value }))}>
              <option value="">T?t c? dï¿½ng xe</option>
              {(lookups.models || []).filter((item) => !filters.brandId || Number(item.brandId) === Number(filters.brandId)).map((item) => (
                <option key={item.id} value={item.id}>{item.brandName} - {item.modelName}</option>
              ))}
            </select>
            <select className="search-input" value={filters.manufactureYear} onChange={(e) => setFilters((prev) => ({ ...prev, manufactureYear: e.target.value }))}>
              <option value="">T?t c? nam SX</option>
              {(lookups.yearOptions || []).map((year) => <option key={year} value={year}>{year}</option>)}
            </select>
          </div>

          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.color} onChange={(e) => setFilters((prev) => ({ ...prev, color: e.target.value }))}>
              <option value="">T?t c? mï¿½u xe</option>
              {(lookups.colorOptions || []).map((color) => <option key={color} value={color}>{color}</option>)}
            </select>
            <select className="search-input" value={filters.status} onChange={(e) => setFilters((prev) => ({ ...prev, status: e.target.value }))}>
              <option value="">T?t c? tr?ng thï¿½i</option>
              {availableStatusOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
            <select className="search-input" value={filters.bayId} onChange={(e) => setFilters((prev) => ({ ...prev, bayId: e.target.value }))}>
              <option value="">T?t c? khoang</option>
              {(lookups.bays || []).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
            </select>
            <select className="search-input" value={filters.staffId} onChange={(e) => setFilters((prev) => ({ ...prev, staffId: e.target.value }))}>
              <option value="">T?t c? nhï¿½n s?</option>
              {(lookups.staffs || []).map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
            <select className="search-input" value={filters.sortBy} onChange={(e) => setFilters((prev) => ({ ...prev, sortBy: e.target.value }))}>
              <option value="checkin">S?p x?p: vï¿½o xu?ng</option>
              <option value="brand_model">S?p x?p: hï¿½ng/dï¿½ng</option>
              <option value="code">S?p x?p: mï¿½ xe</option>
            </select>
            <select className="search-input" value={filters.sortDirection} onChange={(e) => setFilters((prev) => ({ ...prev, sortDirection: e.target.value }))}>
              <option value="desc">Gi?m d?n</option>
              <option value="asc">Tang d?n</option>
            </select>
            <label className="bay-checkbox bay-inline-checkbox">
              <input type="checkbox" checked={filters.includeHidden} onChange={(e) => setFilters((prev) => ({ ...prev, includeHidden: e.target.checked }))} />
              <span>Hi?n xe dï¿½ ?n</span>
            </label>
          </div>
        </>
      )}

      {showCompactFilter && (
        <>
          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.branchId} onChange={(e) => setFilters((prev) => ({ ...prev, branchId: e.target.value }))}>
              <option value="">T?t c? chi nhï¿½nh</option>
              {(lookups.branches || []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="search-input" value={filters.status} onChange={(e) => setFilters((prev) => ({ ...prev, status: e.target.value }))}>
              <option value="">T?t c? tr?ng thï¿½i</option>
              {availableStatusOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
            <select className="search-input" value={filters.staffId} onChange={(e) => setFilters((prev) => ({ ...prev, staffId: e.target.value }))}>
              <option value="">T?t c? nhï¿½n s?</option>
              {(lookups.staffs || []).map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
            </select>
            <select className="search-input" value={filters.bayId} onChange={(e) => setFilters((prev) => ({ ...prev, bayId: e.target.value }))}>
              <option value="">T?t c? khoang</option>
              {(lookups.bays || []).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
            </select>
          </div>

          <div className="table-toolbar bay-filter-bar">
            <select className="search-input" value={filters.sortBy} onChange={(e) => setFilters((prev) => ({ ...prev, sortBy: e.target.value }))}>
              <option value="status">S?p x?p: tr?ng thï¿½i</option>
              <option value="checkin">S?p x?p: vï¿½o xu?ng</option>
              <option value="code">S?p x?p: mï¿½ xe</option>
            </select>
            <select className="search-input" value={filters.sortDirection} onChange={(e) => setFilters((prev) => ({ ...prev, sortDirection: e.target.value }))}>
              <option value="desc">Gi?m d?n</option>
              <option value="asc">Tang d?n</option>
            </select>
            <label className="bay-checkbox bay-inline-checkbox">
              <input type="checkbox" checked={filters.includeHidden} onChange={(e) => setFilters((prev) => ({ ...prev, includeHidden: e.target.checked }))} />
              <span>Hi?n xe dï¿½ ?n</span>
            </label>
          </div>
        </>
      )}

      <div className="table-toolbar"><span className="result-count">{rows.length} xe | Module: {isListTab ? "Danh sï¿½ch & thï¿½ng tin" : isStatusTab ? "Tr?ng thï¿½i xe" : "Qu?n lï¿½ xe"} | Tï¿½m ki?m: {searchQuery || "(tr?ng)"}</span></div>
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
        <div className="loading-state"><div className="spinner" /><p>ï¿½ang t?i d? li?u xe...</p></div>
      ) : (
        <>
          {isListTab && (
            <div className="list-tab-container" style={{ padding: "0 28px 28px" }}>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Mï¿½ xe</th>
                      <th>Bi?n s?</th>
                      <th>Hï¿½ng / dï¿½ng</th>
                      <th>VIN</th>
                      <th>Mï¿½u / Nam SX</th>
                      <th>Chi nhï¿½nh</th>
                      <th>Khï¿½ch hï¿½ng</th>
                      <th>C?p nh?t</th>
                      <th className="th-center">Thao tï¿½c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={9}><div className="empty-state"><p>Chua cï¿½ xe nï¿½o.</p></div></td></tr>
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
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); openEditModal(item); }}>Sửa</button>
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); runMutation(() => item.isEditLocked ? vehicleAdminApi.unlockEdit(item.id) : vehicleAdminApi.lockEdit(item.id), item.isEditLocked ? "Đã mở khóa sửa xe." : "Đã khóa sửa xe.").catch(() => { }); }}>{item.isEditLocked ? "Mở khóa" : "Khóa"}</button>
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); runMutation(() => vehicleAdminApi.hideVehicle(item.id), "Đã ẩn xe.").catch(() => { }); }}>Ẩn</button>
=======
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); openEditModal(item); }}>S?a</button>
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); runMutation(() => item.isEditLocked ? vehicleAdminApi.unlockEdit(item.id) : vehicleAdminApi.lockEdit(item.id), item.isEditLocked ? "ï¿½ï¿½ m? khï¿½a s?a xe." : "ï¿½ï¿½ khï¿½a s?a xe.").catch(() => {}); }}>{item.isEditLocked ? "M? khï¿½a" : "Khï¿½a"}</button>
                              <button className="btn-icon" type="button" onClick={(e) => { e.stopPropagation(); runMutation(() => vehicleAdminApi.hideVehicle(item.id), "ï¿½ï¿½ ?n xe.").catch(() => {}); }}>?n</button>
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
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
                      <th>Mï¿½ xe</th>
                      <th>Bi?n s?</th>
                      <th>Hï¿½ng / dï¿½ng</th>
                      <th>Tr?ng thï¿½i</th>
                      <th>Gi? tr?ng thï¿½i</th>
                      <th>Gi? trong xu?ng</th>
                      <th>Khoang</th>
                      <th>Nhï¿½n s?</th>
                      <th className="th-center">C?p nh?t</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={9}><div className="empty-state"><p>Chua cï¿½ xe nï¿½o.</p></div></td></tr>
                    ) : (
                      rows.map((item) => (
                        <tr key={item.id} className={Number(selectedVehicleId) === Number(item.id) ? "bay-row-selected" : ""} onClick={() => setSelectedVehicleId(item.id)}>
                          <td className="td-id">{item.vehicleCode}</td>
                          <td>{item.licensePlate}</td>
                          <td>{item.brandName} {item.modelName}</td>
                          <td><span className={`badge ${statusBadgeClass(item.currentStatus)}`}>{statusLabelMap[item.currentStatus] || item.currentStatus}</span></td>
                          <td>{formatHours(item.statusDurationHours)}</td>
                          <td>{formatHours(item.inShopHours)}</td>
                          <td>{item.currentBayCode ? `${item.currentBayCode} - ${item.currentBayName}` : "(chua gï¿½n)"}</td>
                          <td>{item.currentStaffName || "-"}</td>
                          <td className="td-center">
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                            <select className="bay-inline-select" value={item.currentStatus || "WAITING_RECEIVE"} onClick={(e) => e.stopPropagation()} onChange={(e) => runMutation(() => vehicleAdminApi.updateStatus(item.id, e.target.value), "Đã cập nhật trạng thái xe.").catch(() => { })}>
=======
                            <select className="bay-inline-select" value={item.currentStatus || "WAITING_RECEIVE"} onClick={(e) => e.stopPropagation()} onChange={(e) => runMutation(() => vehicleAdminApi.updateStatus(item.id, e.target.value), "ï¿½ï¿½ c?p nh?t tr?ng thï¿½i xe.").catch(() => {})}>
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
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
                <div className="bay-panel-head"><h3>L?ch s? tr?ng thï¿½i</h3></div>
                <p className="result-count">
                  {selectedVehicle ? `Xe: ${selectedVehicle.vehicleCode} - ${selectedVehicle.licensePlate}` : "Chua ch?n xe"}
                </p>
                {statusHistoryItems.length === 0 ? (
                  <div className="empty-state"><p>Chua cï¿½ l?ch s? tr?ng thï¿½i.</p></div>
                ) : (
                  <div className="vehicle-status-timeline">
                    {statusHistoryItems.slice(0, 20).map((event, idx) => (
                      <div key={`${event.changedAt}-${idx}`} className="vehicle-status-item">
                        <p className="vehicle-status-title">
                          {statusLabelMap[event.oldStatus] || event.oldStatus} ? {statusLabelMap[event.newStatus] || event.newStatus}
                        </p>
                        <p className="vehicle-status-meta">{formatDateTime(event.changedAt)} | {event.changedByName || "H? th?ng"}</p>
                        <p className="vehicle-status-note">{event.note || "Khï¿½ng cï¿½ ghi chï¿½."}</p>
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
                      <th>Bi?n s?</th>
                      <th>Dï¿½ng xe</th>
                      <th>Khoang hi?n t?i</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={3}><div className="empty-state"><p>Chua cï¿½ xe nï¿½o.</p></div></td></tr>
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
                  <div className="bay-panel-head"><h3>Thï¿½ng tin v? trï¿½ hi?n t?i</h3></div>
                  <div className="bay-meta-info">
                    <div className="bay-meta-row">
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                      <strong>Xe:</strong>
                      <span style={{ fontWeight: '600' }}>{selectedRow ? `${selectedRow.vehicleCode} - ${selectedRow.licensePlate}` : "(chưa chọn)"}</span>
                    </div>
                    <div className="bay-meta-row">
                      <strong>Trạng thái:</strong>
=======
                      <strong>Xe:</strong> 
                      <span style={{ fontWeight: '600' }}>{selectedRow ? `${selectedRow.vehicleCode} - ${selectedRow.licensePlate}` : "(chua ch?n)"}</span>
                    </div>
                    <div className="bay-meta-row">
                      <strong>Tr?ng thï¿½i:</strong> 
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                      <span className={`badge ${statusBadgeClass(selectedRow?.currentStatus)}`}>{selectedRow ? (statusLabelMap[selectedRow.currentStatus] || selectedRow.currentStatus) : "-"}</span>
                    </div>
                    <div className="bay-meta-row">
                      <strong>Khoang:</strong>
                      <span className={selectedRow?.currentBayCode ? "badge badge-active" : "badge badge-inactive"}>
                        {selectedRow?.currentBayCode ? `${selectedRow.currentBayCode} - ${selectedRow.currentBayName}` : "Chua cï¿½ khoang"}
                      </span>
                    </div>
                    {selectedRow?.currentBayCode && (
                      <div className="bay-meta-row">
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                        <strong>Lưu kho:</strong>
=======
                        <strong>Luu kho:</strong> 
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                        <span style={{ fontWeight: '500' }}>{formatHours(selectedRow?.bayOccupiedHours)}</span>
                      </div>
                    )}
                  </div>
                </div>

                {selectedRow && !selectedRow.currentBayCode && (
                  <div className="bay-panel">
                    <div className="bay-panel-head"><h3>Gï¿½n xe vï¿½o khoang m?i</h3></div>
                    <form className="admin-form bay-mini-form" style={{ marginTop: '16px' }} onSubmit={(e) => {
                      e.preventDefault();
                      const bayId = Number(e.currentTarget.elements.assignBayId.value);
                      if (!selectedVehicleId || !bayId) return;
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                      runMutation(() => vehicleAdminApi.assignBay(selectedVehicleId, { bayId }), "Đã gán xe vào khoang.").catch(() => { });
=======
                      runMutation(() => vehicleAdminApi.assignBay(selectedVehicleId, { bayId }), "ï¿½ï¿½ gï¿½n xe vï¿½o khoang.").catch(() => {});
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                    }}>
                      <div className="form-group">
                        <select className="search-input" name="assignBayId" defaultValue="">
                          <option value="">-- Ch?n khoang tr?ng --</option>
                          {(lookups.bays || []).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
                        </select>
                      </div>
                      <div className="form-actions" style={{ justifyContent: 'space-between', marginTop: '16px' }}>
                        <button className="btn-ghost" type="button" disabled={!selectedVehicleId || saving} onClick={async () => {
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                          try {
                            setSaving(true);
                            const res = await bayApi.getSuggestions({ vehicleId: selectedVehicleId });
                            setSuggestions(res || []);
                            setFeedback("Đã tải gợi ý khoang.");
                          } catch (err) {
                            setError(err.message || "Lỗi khi lấy gợi ý khoang.");
                          } finally {
                            setSaving(false);
                          }
                        }}>💡 Gợi ý (AI)</button>
                        <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving}>Gán khoang</button>
=======
                           try {
                             setSaving(true);
                             const res = await bayApi.getSuggestions({ vehicleId: selectedVehicleId });
                             setSuggestions(res || []);
                             setFeedback("ï¿½ï¿½ t?i g?i ï¿½ khoang.");
                           } catch(err) {
                             setError(err.message || "L?i khi l?y g?i ï¿½ khoang.");
                           } finally {
                             setSaving(false);
                           }
                        }}>?? G?i ï¿½ (AI)</button>
                        <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving}>Gï¿½n khoang</button>
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                      </div>
                    </form>

                    {suggestions && suggestions.length > 0 && (
                      <div className="suggestion-box">
                        <h4>ï¿½? xu?t phï¿½ h?p nh?t</h4>
                        <div className="bay-suggestion-list">
                          {suggestions.map((s, idx) => (
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                            <div key={idx} className="suggestion-item">
                              <div>
                                <div className="suggestion-item-title">{s.bay?.code || s.bayCode} <span className="suggestion-item-score">Điểm: {s.score}</span></div>
                                <div className="suggestion-item-reason">{s.reasons?.join(', ')}</div>
                              </div>
                              <button className="btn-ghost" type="button" disabled={saving} onClick={() => {
                                runMutation(() => vehicleAdminApi.assignBay(selectedVehicleId, { bayId: s.bayId || s.bay?.id }), "Đã gán tự động.").catch(() => { });
                              }}>Áp dụng</button>
                            </div>
=======
                             <div key={idx} className="suggestion-item">
                                <div>
                                  <div className="suggestion-item-title">{s.bay?.code || s.bayCode} <span className="suggestion-item-score">ï¿½i?m: {s.score}</span></div>
                                  <div className="suggestion-item-reason">{s.reasons?.join(', ')}</div>
                                </div>
                                <button className="btn-ghost" type="button" disabled={saving} onClick={() => {
                                  runMutation(() => vehicleAdminApi.assignBay(selectedVehicleId, { bayId: s.bayId || s.bay?.id }), "ï¿½ï¿½ gï¿½n t? d?ng.").catch(() => {});
                                }}>ï¿½p d?ng</button>
                             </div>
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {selectedRow?.currentBayCode && (
                  <div className="bay-panel">
                    <div className="bay-panel-head"><h3>Chuy?n ra ngoï¿½i / ï¿½i?u ph?i</h3></div>
                    <form className="admin-form bay-mini-form" style={{ marginTop: '16px' }} onSubmit={(e) => {
                      e.preventDefault();
                      const toBayId = Number(e.currentTarget.elements.moveBayId.value);
                      if (!selectedVehicleId || !toBayId) return;
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                      runMutation(() => vehicleAdminApi.moveBay(selectedVehicleId, { toBayId }), "Đã chuyển xe sang khoang mới.").catch(() => { });
=======
                      runMutation(() => vehicleAdminApi.moveBay(selectedVehicleId, { toBayId }), "ï¿½ï¿½ chuy?n xe sang khoang m?i.").catch(() => {});
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                    }}>
                      <div className="form-group">
                        <select className="search-input" name="moveBayId" defaultValue="">
                          <option value="">-- Ch?n khoang dï¿½ch --</option>
                          {(lookups.bays || []).filter(b => b.code !== selectedRow.currentBayCode).map((item) => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
                        </select>
                      </div>
                      <div className="form-actions" style={{ justifyContent: 'space-between', marginTop: '16px' }}>
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                        <button className="btn-ghost" type="button" disabled={!selectedVehicleId || saving} onClick={() => runMutation(() => vehicleAdminApi.removeBay(selectedVehicleId, { note: "Chuyển ra ngoài xưởng" }), "Đã đưa ra khỏi khoang gốc.").catch(() => { })}>Đưa khỏi khoang</button>
                        <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving}>Điều phối ngay</button>
=======
                        <button className="btn-ghost" type="button" disabled={!selectedVehicleId || saving} onClick={() => runMutation(() => vehicleAdminApi.removeBay(selectedVehicleId, { note: "Chuy?n ra ngoï¿½i xu?ng" }), "ï¿½ï¿½ dua ra kh?i khoang g?c.").catch(() => {})}>ï¿½ua kh?i khoang</button>
                        <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving}>ï¿½i?u ph?i ngay</button>
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
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
                      <th>Bi?n s?</th>
                      <th>Tr?ng thï¿½i</th>
                      <th>Ngu?i ph? trï¿½ch</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr><td colSpan={3}><div className="empty-state"><p>Chua cï¿½ xe nï¿½o.</p></div></td></tr>
                    ) : (
                      rows.map((item) => (
                        <tr key={item.id} className={Number(selectedVehicleId) === Number(item.id) ? "bay-row-selected" : ""} onClick={() => setSelectedVehicleId(item.id)}>
                          <td style={{ fontWeight: '600' }}>{item.licensePlate}</td>
                          <td><span className={`badge ${statusBadgeClass(item.currentStatus)}`}>{statusLabelMap[item.currentStatus] || item.currentStatus}</span></td>
                          <td>{item.currentStaffName ? <span style={{ fontWeight: '600' }}>{item.currentStaffName}</span> : <span style={{ color: 'var(--text-muted)' }}>(Chua gï¿½n)</span>}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div className="vehicle-action-panel">
                <div className="bay-panel">
                  <div className="bay-panel-head"><h3>Phï¿½n cï¿½ng nhï¿½n s?</h3></div>
                  <div className="bay-meta-info" style={{ marginBottom: '16px' }}>
                    <div className="bay-meta-row" style={{ borderBottom: 'none', paddingBottom: 0 }}>
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                      <strong>Xe:</strong>
                      <span style={{ fontWeight: '600' }}>{selectedRow ? `${selectedRow.vehicleCode} - ${selectedRow.licensePlate}` : "(chưa chọn)"}</span>
=======
                      <strong>Xe:</strong> 
                      <span style={{ fontWeight: '600' }}>{selectedRow ? `${selectedRow.vehicleCode} - ${selectedRow.licensePlate}` : "(chua ch?n)"}</span>
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                    </div>
                  </div>
                  <form className="admin-form bay-mini-form" onSubmit={(e) => {
                    e.preventDefault();
                    const staffId = Number(e.currentTarget.elements.staffId.value) || null;
                    const technicalTeam = String(e.currentTarget.elements.technicalTeam.value || "").trim();
                    if (!selectedVehicleId) return;
<<<<<<< HEAD:frontend/src/features/cars/pages/VehicleManagementPage.jsx
                    runMutation(() => vehicleAdminApi.assignStaff(selectedVehicleId, { staffId, technicalTeam: technicalTeam || null }), "Đã cập nhật phân công xe.").catch(() => { });
=======
                    runMutation(() => vehicleAdminApi.assignStaff(selectedVehicleId, { staffId, technicalTeam: technicalTeam || null }), "ï¿½ï¿½ c?p nh?t phï¿½n cï¿½ng xe.").catch(() => {});
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/features/cars/pages/VehicleManagementPage.jsx
                  }}>
                    <div className="form-group">
                      <label>C? v?n d?ch v? / KTV chï¿½nh</label>
                      <select className="search-input" name="staffId" defaultValue={selectedRow?.currentStaffId || ""}>
                        <option value="">-- B? phï¿½n cï¿½ng --</option>
                        {(lookups.staffs || []).map((item) => <option key={item.id} value={item.id}>{item.fullName}</option>)}
                      </select>
                    </div>
                    <div className="form-group">
                      <label>T? k? thu?t h? tr? (tï¿½y ch?n)</label>
                      <input className="search-input" name="technicalTeam" defaultValue={selectedRow?.technicalTeam || ""} placeholder="VD: T? ï¿½?ng Son 1" />
                    </div>
                    <div className="form-actions" style={{ marginTop: '16px' }}>
                      <button className="btn-primary" type="submit" disabled={!selectedVehicleId || saving} style={{ width: '100%' }}>Luu phï¿½n cï¿½ng</button>
                    </div>
                  </form>
                </div>

                <div className="bay-panel">
                  <div className="bay-panel-head" style={{ marginBottom: '16px' }}><h3>Workload Nhï¿½n S?</h3></div>
                  <table className="admin-table">
                    <thead><tr><th>Nhï¿½n s?</th><th>ï¿½ang x? lï¿½</th><th>Tï¿½nh tr?ng</th></tr></thead>
                    <tbody>
                      {workloadByStaff.length === 0 ? <tr><td colSpan={3}><div className="empty-state" style={{ minHeight: '100px' }}><p>Chua cï¿½ d? li?u.</p></div></td></tr> : workloadByStaff.map((item) => (
                        <tr key={item.staffId}>
                          <td style={{ fontWeight: '600' }}>{item.staffName}</td>
                          <td>{item.total} xe</td>
                          <td>{item.total > 5 ? <span className="badge badge-suspended">Quï¿½ t?i</span> : <span className="badge badge-active">?n d?nh</span>}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div className="alert alert-error" style={{ marginTop: '16px', margin: '16px 0 0 0' }}>
                    C?nh bï¿½o nhï¿½n s? b? quï¿½ t?i n?u dang x? lï¿½ &gt; 5 xe.
                  </div>
                </div>
              </div>
            </div>
          )}

          {currentTab === "history" && (
            <div className="table-wrapper">
              <table className="admin-table">
                <thead><tr><th>Th?i gian</th><th>Lo?i</th><th>Tiï¿½u d?</th><th>N?i dung</th><th>Ngu?i thao tï¿½c</th></tr></thead>
                <tbody>
                  {(history.items || []).length === 0 ? <tr><td colSpan={5}><div className="empty-state"><p>Chua cï¿½ l?ch s?.</p></div></td></tr> : history.items.map((item, idx) => (
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
                <thead><tr><th>M?c d?</th><th>Lo?i c?nh bï¿½o</th><th>Xe</th><th>N?i dung</th><th>Th?i gian</th></tr></thead>
                <tbody>
                  {(alerts.items || []).length === 0 ? <tr><td colSpan={5}><div className="empty-state"><p>Khï¿½ng cï¿½ c?nh bï¿½o.</p></div></td></tr> : alerts.items.map((item, idx) => (
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
                  <thead><tr><th>Tr?ng thï¿½i</th><th>S? xe</th><th>T? l?</th></tr></thead>
                  <tbody>{(summary.statusBreakdown || []).map((item) => <tr key={item.status}><td>{statusLabelMap[item.status] || item.status}</td><td>{item.count}</td><td>{toNumber(item.rate, 0).toFixed(2)}%</td></tr>)}</tbody>
                </table>
              </div>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead><tr><th>Chi nhï¿½nh</th><th>T?ng xe</th><th>ï¿½ang x? lï¿½</th></tr></thead>
                  <tbody>{(summary.byBranch || []).map((item) => <tr key={item.branchId}><td>{item.branchName}</td><td>{item.total}</td><td>{item.inShop}</td></tr>)}</tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {modal && (
        <Modal title={modal.mode === "edit" ? `C?p nh?t xe #${modal.item?.id}` : "T?o xe m?i"} onClose={closeModal}>
          <VehicleForm initial={modal.item || EMPTY_FORM} lookups={lookups} onSubmit={handleSaveVehicle} onCancel={closeModal} saving={saving} />
        </Modal>
      )}

      {isDetailModalOpen && selectedVehicle && (
        <Modal title={`Thï¿½ng tin xe: ${selectedVehicle.licensePlate || selectedVehicle.vehicleCode}`} onClose={() => setIsDetailModalOpen(false)}>
          <div className="vehicle-detail-grid" style={{ minWidth: '400px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', lineHeight: '1.6' }}>
            <p><strong>Mï¿½ xe:</strong> {selectedVehicle.vehicleCode || "-"}</p>
            <p><strong>Bi?n s?:</strong> {selectedVehicle.licensePlate || "-"}</p>
            <p><strong>Hï¿½ng / dï¿½ng:</strong> {selectedVehicle.brandName} {selectedVehicle.modelName}</p>
            <p><strong>VIN:</strong> {selectedVehicle.vin || "-"}</p>
            <p><strong>Mï¿½u xe:</strong> {selectedVehicle.color || "-"}</p>
            <p><strong>Nam SX:</strong> {selectedVehicle.manufactureYear || "-"}</p>
            <p><strong>Tr?ng thï¿½i:</strong> <span className={`badge ${statusBadgeClass(selectedVehicle.currentStatus)}`}>{statusLabelMap[selectedVehicle.currentStatus] || selectedVehicle.currentStatus}</span></p>
            <p><strong>Gi? trong xu?ng:</strong> {formatHours(selectedVehicle.inShopHours)}</p>
            <p><strong>Khoang:</strong> {selectedVehicle.currentBayCode ? <span className="badge badge-active">{selectedVehicle.currentBayCode} - {selectedVehicle.currentBayName}</span> : "(chua gï¿½n)"}</p>
            <p><strong>Nhï¿½n s?:</strong> {selectedVehicle.currentStaffName || "-"}</p>
            <p><strong>T? k? thu?t:</strong> {selectedVehicle.technicalTeam || "-"}</p>
            <p><strong>Ngï¿½y c?p nh?t:</strong> {formatDateTime(selectedVehicle.updatedAt)}</p>
            <p><strong>S? mï¿½y:</strong> {selectedVehicle.engineNumber || "-"}</p>
            <p><strong>S? khung:</strong> {selectedVehicle.chassisNumber || "-"}</p>
            <p style={{ gridColumn: '1 / -1' }}><strong>Tï¿½nh tr?ng ban d?u:</strong> {selectedVehicle.initialCondition || "-"}</p>
          </div>
          <div className="form-actions" style={{ marginTop: '24px', justifyContent: 'flex-end' }}>
            <button className="btn-primary" type="button" onClick={() => setIsDetailModalOpen(false)}>ï¿½ï¿½ng</button>
          </div>
        </Modal>
      )}
    </div>
  );
}
