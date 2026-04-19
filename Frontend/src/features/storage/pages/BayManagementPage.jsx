import { useCallback, useEffect, useMemo, useState } from "react";
import { Modal } from "../../admin/components/AdminModals";
import { bayApi } from "../api/bayApi";
import { executionApi } from "@features/workshop/api/executionApi";
import { createChatHubConnection } from "@features/chat/realtime/chatHubConnection";
import { useSearch } from "@context/SearchContext";
import { useBranch } from "../../../context/BranchContext";
import TablePagination from "@shared/components/TablePagination";
import "../../admin/pages/AdminPage.css";
import "./BayManagementPage.css";

const STATUS_LABELS = {
  EMPTY: "Trống",
  IN_USE: "Đang sử dụng",
  MAINTENANCE: "Bảo trì",
  SUSPENDED: "Tạm ngưng",
};

const STATUS_OPTIONS = [
  { value: "EMPTY", label: "Trống" },
  { value: "IN_USE", label: "Đang sử dụng" },
  { value: "MAINTENANCE", label: "Bảo trì" },
  { value: "SUSPENDED", label: "Tạm ngưng" },
];

const TABS = [
  { id: "list", label: "Danh sách & thông tin" },
  { id: "vehicles", label: "Xe trong khoang" },
  { id: "usage", label: "Theo dõi sử dụng" },
  { id: "layout", label: "Sơ đồ & điều phối" },
  { id: "alerts", label: "Cầnh b�o" },
  { id: "stats", label: "Thống kê" },
];

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
    return "ho chi minh";
  }
  if (["ha noi", "hanoi"].includes(normalized)) {
    return "ha noi";
  }
  if (["da nang", "danang"].includes(normalized)) {
    return "da nang";
  }

  return normalized;
}

function pickPreferredBranch(current, candidate) {
  if (!current) return candidate;

  let currentScore = 0;
  let candidateScore = 0;

  if (removeDiacritics(current.name) !== String(current.name || "")) currentScore += 2;
  if (removeDiacritics(candidate.name) !== String(candidate.name || "")) candidateScore += 2;
  if (String(current.status || "").toUpperCase() === "ACTIVE") currentScore += 1;
  if (String(candidate.status || "").toUpperCase() === "ACTIVE") candidateScore += 1;

  if (candidateScore > currentScore) return candidate;
  if (candidateScore < currentScore) return current;

  return Number(candidate.id) < Number(current.id) ? candidate : current;
}

function dedupeBranches(list) {
  const map = new Map();

  for (const branch of list || []) {
    if (!branch) continue;
    const key = normalizeBranchName(branch.name) || `id:${branch.id}`;
    const existing = map.get(key);
    map.set(key, pickPreferredBranch(existing, branch));
  }

  return Array.from(map.values()).sort((a, b) =>
    String(a?.name || "").localeCompare(String(b?.name || ""), "vi")
  );
}

function normalizeTabId(value) {
  const id = String(value || "").trim().toLowerCase();
  const exists = TABS.some((item) => item.id === id);
  return exists ? id : "list";
}

const EMPTY_BAY_FORM = {
  sequenceOrder: "",
  code: "",
  name: "",
  branchId: "",
  bayType: "GENERAL",
  maxTonnage: "",
  operationalStatus: "EMPTY",
  isActive: true,
  area: "Khu A",
  capacity: 1,
  supportedSegmentsText: "",
};

const EMPTY_ASSIGN_FORM = {
  bayId: "",
  vehicleId: "",
  serviceStatus: "",
  note: "",
};

const EMPTY_MOVE_FORM = {
  vehicleId: "",
  fromBayId: "",
  toBayId: "",
  serviceStatus: "",
  note: "",
};

const EMPTY_SUGGEST_FORM = {
  vehicleId: "",
  preferredArea: "",
  serviceStatus: "",
};

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
  return `${toNumber(value, 0).toFixed(2)}h`;
}

function getFriendlyErrorMessage(err, fallbackMessage) {
  const statusCode = err?.response?.status;
  const apiMessage = err?.response?.data?.message;

  if (statusCode === 401) {
    return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  }
  if (statusCode === 403) {
    return "Bạn không có quyền truy cập chức năng này.";
  }
  if (statusCode === 404) {
    return apiMessage || "Không tìm thấy dữ liệu.";
  }

  return apiMessage || fallbackMessage;
}

function statusBadge(status) {
  if (status === "IN_USE") return "badge-suspended";
  if (status === "MAINTENANCE") return "badge-warning";
  if (status === "SUSPENDED") return "badge-inactive";
  return "badge-active";
}

function FormRow({ children }) {
  return <div className="bay-form-row">{children}</div>;
}

function BayForm({ initial, branches, onSubmit, onCancel, saving }) {
  const [form, setForm] = useState({
    ...initial,
    sequenceOrder: initial.sequenceOrder ?? "",
    branchId: initial.branchId ? String(initial.branchId) : "",
    capacity: initial.capacity ?? 1,
    maxTonnage: initial.maxTonnage ?? "",
  });

  const handleChange = (event) => {
    const { name, value, type, checked } = event.target;
    setForm((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const sequenceOrder = Number(form.sequenceOrder);
    onSubmit({
      sequenceOrder: Number.isFinite(sequenceOrder) && sequenceOrder > 0 ? Math.floor(sequenceOrder) : null,
      code: String(form.code || "").trim(),
      name: String(form.name || "").trim(),
      branchId: Number(form.branchId),
      bayType: String(form.bayType || "GENERAL").trim(),
      maxTonnage: form.maxTonnage === "" ? null : Number(form.maxTonnage),
      operationalStatus: form.operationalStatus || "EMPTY",
      isActive: Boolean(form.isActive),
      area: String(form.area || "").trim(),
      capacity: Math.max(1, Number(form.capacity) || 1),
      supportedSegments: String(form.supportedSegmentsText || "")
        .split(",")
        .map((item) => item.trim())
        .filter(Boolean),
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <FormRow>
        <div className="form-group">
          <label>Mã khoang *</label>
          <input name="code" value={form.code || ""} onChange={handleChange} required />
        </div>
        <div className="form-group">
          <label>Tên khoang *</label>
          <input name="name" value={form.name || ""} onChange={handleChange} required />
        </div>
      </FormRow>

      <FormRow>
        <div className="form-group">
          <label>Chi nh�nh *</label>
          <select name="branchId" value={form.branchId} onChange={handleChange} required>
            <option value="">Chọn chi nh�nh</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
        </div>
        <div className="form-group">
          <label>Thứ tự khoang</label>
          <input
            name="sequenceOrder"
            type="number"
            min="1"
            value={form.sequenceOrder ?? ""}
            onChange={handleChange}
            placeholder="Tự tăng nếu bỏ trống"
          />
        </div>
      </FormRow>

      <FormRow>
        <div className="form-group">
          <label>Loại khoang</label>
          <input name="bayType" value={form.bayType || ""} onChange={handleChange} />
        </div>
        <div className="form-group">
          <label>Sức chứa (xe)</label>
          <input
            name="capacity"
            type="number"
            min="1"
            max="20"
            value={form.capacity}
            onChange={handleChange}
          />
        </div>
      </FormRow>

      <FormRow>
        <div className="form-group">
          <label>Tải trọng tối đa (tấn)</label>
          <input
            name="maxTonnage"
            type="number"
            step="0.1"
            min="0"
            value={form.maxTonnage}
            onChange={handleChange}
          />
        </div>
        <div className="form-group">
          <label>Trạng thái khoang</label>
          <select name="operationalStatus" value={form.operationalStatus || "EMPTY"} onChange={handleChange}>
            {STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </FormRow>

      <div className="form-group">
        <label>Khu vực</label>
        <input name="area" value={form.area || ""} onChange={handleChange} />
      </div>

      <div className="form-group">
        <label>Ph�n kh�c xe ph� h?p (c�ch nhau bảng d?u ph?y)</label>
        <input
          name="supportedSegmentsText"
          value={form.supportedSegmentsText || ""}
          onChange={handleChange}
          placeholder="Sedan, SUV, Pickup hoặc TẤT CẢ"
        />
      </div>

      <label className="bay-checkbox">
        <input
          type="checkbox"
          name="isActive"
          checked={Boolean(form.isActive)}
          onChange={handleChange}
        />
        <span>Khoang đang mở khóa</span>
      </label>

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

export default function BayManagementPage({ section }) {
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

  const [lookups, setLookups] = useState({ branches: [], vehicles: [], areaOptions: [] });
  const [rows, setRows] = useState([]);
  const [summary, setSummary] = useState(null);
  const [usage, setUsage] = useState({ byBay: [], mostUsed: [], leastUsed: [], frequentOverload: [], longestOccupied: [] });
  const [layout, setLayout] = useState({ branches: [] });
  const [timelineRows, setTimelineRows] = useState([]);
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [alerts, setAlerts] = useState({ items: [], total: 0 });

  const [selectedBayId, setSelectedBayId] = useState(null);
  const [selectedBayVehicles, setSelectedBayVehicles] = useState({ vehicles: [], occupiedCount: 0, capacity: 0 });
  const [selectedBayHistory, setSelectedBayHistory] = useState({ items: [], totalSessions: 0, uniqueVehicleCount: 0, totalUsageHours: 0 });

  const [filters, setFilters] = useState({
    branchId: "",
    area: "",
    status: "",
    sortBy: "sequence",
    sortDirection: "asc",
    includeHidden: false,
  });
  const [listPage, setListPage] = useState(1);
  const [listPageSize, setListPageSize] = useState(10);
  const [usagePage, setUsagePage] = useState(1);
  const [usagePageSize, setUsagePageSize] = useState(10);
  const [historyPage, setHistoryPage] = useState(1);
  const [historyPageSize, setHistoryPageSize] = useState(10);
  const [alertPage, setAlertPage] = useState(1);
  const [alertPageSize, setAlertPageSize] = useState(10);

  const [modal, setModal] = useState(null);
  const [formError, setFormError] = useState("");

  const [assignForm, setAssignForm] = useState(EMPTY_ASSIGN_FORM);
  const [moveForm, setMoveForm] = useState(EMPTY_MOVE_FORM);
  const [suggestForm, setSuggestForm] = useState(EMPTY_SUGGEST_FORM);
  const [suggestions, setSuggestions] = useState([]);

  const refreshLookups = useCallback(async () => {
    const response = await bayApi.getLookups({ takeVehicles: 1500 });
    setLookups({
      branches: dedupeBranches(response?.branches || []),
      vehicles: response?.vehicles || [],
      areaOptions: response?.areaOptions || [],
    });
  }, []);

  const refreshDashboard = useCallback(async () => {
    const branchId = filters.branchId ? Number(filters.branchId) : null;
    const commonParams = branchId ? { branchId } : {};
    const listParams = {
      ...commonParams,
      search: searchQuery || undefined,
      area: filters.area || undefined,
      status: filters.status || undefined,
      sortBy: filters.sortBy,
      sortDirection: filters.sortDirection,
      includeHidden: filters.includeHidden,
    };

    setLoading(true);
    setError("");

    try {
      const [listRes, summaryRes, usageRes, layoutRes, alertRes] = await Promise.all([
        bayApi.getList(listParams),
        bayApi.getSummary(commonParams),
        bayApi.getUsage(commonParams),
        bayApi.getLayout(commonParams),
        bayApi.getAlerts(commonParams),
      ]);

      const listRows = listRes?.items || [];
      setRows(listRows);
      setSummary(summaryRes || null);
      setUsage(usageRes || { byBay: [] });
      setLayout(layoutRes || { branches: [] });
      setAlerts(alertRes || { items: [], total: 0 });

      if (listRows.length === 0) {
        setSelectedBayId(null);
        return;
      }

      const stillExists = selectedBayId && listRows.some((item) => Number(item.id) === Number(selectedBayId));
      if (!stillExists) {
        setSelectedBayId(listRows[0].id);
      }
    } catch (err) {
      setError(getFriendlyErrorMessage(err, "Không thể tải dữ liệu module Khoang."));
    } finally {
      setLoading(false);
    }
  }, [filters, searchQuery, selectedBayId]);

  const loadTimeline = useCallback(async () => {
    const branchId = Number(filters.branchId || selectedBranchId || 0);
    if (!branchId) {
      setTimelineRows([]);
      return;
    }

    setTimelineLoading(true);
    try {
      const result = await executionApi.getBayTimeline({
        branchId,
        date: new Date().toISOString(),
      });
      setTimelineRows(Array.isArray(result) ? result : []);
    } catch (err) {
      setError(getFriendlyErrorMessage(err, "Không thể tải timeline khoang thời gian thực."));
    } finally {
      setTimelineLoading(false);
    }
  }, [filters.branchId, selectedBranchId]);

  useEffect(() => {
    refreshLookups().catch((err) => setError(getFriendlyErrorMessage(err, "Không thể tải dữ liệu tham chiếu.")));
  }, [refreshLookups]);

  useEffect(() => {
    refreshDashboard();
  }, [refreshDashboard]);

  useEffect(() => {
    if (currentTab !== "layout") return;
    loadTimeline();
  }, [currentTab, loadTimeline]);

  useEffect(() => {
    const branchId = Number(filters.branchId || selectedBranchId || 0);
    if (!branchId) return undefined;

    const connection = createChatHubConnection();
    let active = true;

    connection.on("BayTimelineUpdated", (payload) => {
      const payloadBranchId = Number(payload?.branchId || 0);
      if (!active) return;
      if (payloadBranchId > 0 && payloadBranchId !== branchId) return;
      if (currentTab === "layout") {
        loadTimeline();
      }
    });

    (async () => {
      try {
        await connection.start();
        await connection.invoke("JoinWorkshopBranchGroup", branchId);
      } catch {
        // Keep silent: page still works without realtime.
      }
    })();

    return () => {
      active = false;
      connection.off("BayTimelineUpdated");
      connection.stop().catch(() => { });
    };
  }, [currentTab, filters.branchId, loadTimeline, selectedBranchId]);

  const loadSelectedBayDetails = useCallback(async (bayId) => {
    if (!bayId) return;
    try {
      const [vehicleRes, historyRes] = await Promise.all([
        bayApi.getBayVehicles(bayId),
        bayApi.getBayHistory(bayId, { take: 200 }),
      ]);
      setSelectedBayVehicles(vehicleRes || { vehicles: [] });
      setSelectedBayHistory(historyRes || { items: [] });
    } catch (err) {
      setError(getFriendlyErrorMessage(err, "Không thể tải chi tiết khoang."));
    }
  }, []);

  useEffect(() => {
    if (!selectedBayId) {
      setSelectedBayVehicles({ vehicles: [], occupiedCount: 0, capacity: 0 });
      setSelectedBayHistory({ items: [], totalSessions: 0, uniqueVehicleCount: 0, totalUsageHours: 0 });
      return;
    }

    loadSelectedBayDetails(selectedBayId);
    setAssignForm((prev) => ({ ...prev, bayId: String(selectedBayId) }));
    setMoveForm((prev) => ({ ...prev, fromBayId: String(selectedBayId) }));
  }, [selectedBayId, loadSelectedBayDetails]);

  const selectedBay = useMemo(
    () => rows.find((item) => Number(item.id) === Number(selectedBayId)) || null,
    [rows, selectedBayId]
  );

  const activeVehicles = selectedBayVehicles?.vehicles || [];
  const visibleRows = rows || [];
  const pagedVisibleRows = useMemo(
    () => visibleRows.slice((listPage - 1) * listPageSize, listPage * listPageSize),
    [visibleRows, listPage, listPageSize]
  );
  const pagedUsageRows = useMemo(
    () => (usage.byBay || []).slice((usagePage - 1) * usagePageSize, usagePage * usagePageSize),
    [usage.byBay, usagePage, usagePageSize]
  );
  const pagedHistoryRows = useMemo(
    () => (selectedBayHistory.items || []).slice((historyPage - 1) * historyPageSize, historyPage * historyPageSize),
    [selectedBayHistory.items, historyPage, historyPageSize]
  );
  const pagedAlertRows = useMemo(
    () => (alerts.items || []).slice((alertPage - 1) * alertPageSize, alertPage * alertPageSize),
    [alerts.items, alertPage, alertPageSize]
  );

  useEffect(() => {
    setListPage(1);
    setUsagePage(1);
    setHistoryPage(1);
    setAlertPage(1);
  }, [currentTab, filters.branchId, filters.area, filters.status, searchQuery, selectedBayId]);

  const runMutation = useCallback(async (task, successMessage, options = {}) => {
    setSaving(true);
    setError("");
    setFormError("");
    setFeedback("");

    try {
      await task();
      setFeedback(successMessage || "Thao t�c th�nh c�ng.");
      await refreshDashboard();
      if (options.reloadBayId || selectedBayId) {
        await loadSelectedBayDetails(options.reloadBayId || selectedBayId);
      }
    } catch (err) {
      const message = getFriendlyErrorMessage(err, "Không th? th?c hi?n thao t�c.");
      if (options.asFormError) {
        setFormError(message);
      } else {
        setError(message);
      }
      throw err;
    } finally {
      setSaving(false);
    }
  }, [loadSelectedBayDetails, refreshDashboard, selectedBayId]);

  const openCreateModal = () => {
    setFormError("");
    setModal({
      mode: "create",
      item: {
        ...EMPTY_BAY_FORM,
        branchId: filters.branchId || selectedBranchId || "",
      },
    });
  };

  const openEditModal = (item) => {
    setFormError("");
    setModal({
      mode: "edit",
      item: {
        id: item.id,
        sequenceOrder: item.sequenceOrder ?? "",
        code: item.code || "",
        name: item.name || "",
        branchId: item.branchId || "",
        bayType: item.bayType || "GENERAL",
        maxTonnage: item.maxTonnage ?? "",
        operationalStatus: item.operationalStatus || "EMPTY",
        isActive: !item.isLocked,
        area: item.area || "Chua ph�n khu",
        capacity: item.capacity || 1,
        supportedSegmentsText: (item.supportedSegments || []).join(", "),
      },
    });
  };

  const closeModal = () => {
    setModal(null);
    setFormError("");
  };

  const handleSaveBay = async (payload) => {
    try {
      if (modal?.mode === "edit" && modal?.item?.id) {
        await runMutation(
          () => bayApi.updateBay(modal.item.id, payload),
          "Cập nhật khoang thành công.",
          { asFormError: true }
        );
        closeModal();
        return;
      }

      await runMutation(
        () => bayApi.createBay(payload),
        "Tạo khoang thành công.",
        { asFormError: true }
      );
      closeModal();
    } catch {
      // Keep modal open and show form error from runMutation.
    }
  };

  const handleAssignVehicle = (event) => {
    event.preventDefault();
    const bayId = Number(assignForm.bayId || selectedBayId);
    const vehicleId = Number(assignForm.vehicleId);
    if (!bayId || !vehicleId) {
      setError("Cần chọn khoang v� xe d? g�n.");
      return;
    }

    runMutation(
      () => bayApi.assignVehicle(bayId, {
        vehicleId,
        serviceStatus: assignForm.serviceStatus || null,
        note: assignForm.note || null,
      }),
      "Đã g�n xe v�o khoang.",
      { reloadBayId: bayId }
    ).then(() => {
      setAssignForm((prev) => ({ ...prev, vehicleId: "", note: "" }));
    }).catch(() => { });
  };

  const handleMoveVehicle = (event) => {
    event.preventDefault();
    const payload = {
      vehicleId: Number(moveForm.vehicleId),
      fromBayId: Number(moveForm.fromBayId || selectedBayId),
      toBayId: Number(moveForm.toBayId),
      serviceStatus: moveForm.serviceStatus || null,
      note: moveForm.note || null,
    };

    if (!payload.vehicleId || !payload.fromBayId || !payload.toBayId) {
      setError("Cần chọn đủ xe, khoang nguồn và khoang đích.");
      return;
    }

    runMutation(
      () => bayApi.moveVehicle(payload),
      "Điều phối xe thành công.",
      { reloadBayId: payload.toBayId }
    ).then(() => {
      setMoveForm((prev) => ({ ...prev, toBayId: "", note: "" }));
    }).catch(() => { });
  };

  const handleSuggest = async (event) => {
    event.preventDefault();
    const vehicleId = Number(suggestForm.vehicleId);
    if (!vehicleId) {
      setError("Chọn xe để hệ thống gợi ý khoang.");
      return;
    }

    try {
      const response = await bayApi.getSuggestions({
        vehicleId,
        branchId: filters.branchId ? Number(filters.branchId) : undefined,
        preferredArea: suggestForm.preferredArea || undefined,
        sourceBayId: selectedBayId || undefined,
        serviceStatus: suggestForm.serviceStatus || undefined,
      });
      setSuggestions(response?.items || []);
    } catch (err) {
      setError(getFriendlyErrorMessage(err, "Không thể lấy gợi ý khoang."));
    }
  };

  return (
    <div className="management-content bay-admin-page">
      <header className="admin-header header-actions-only">
        <div className="bay-header-actions">
          <button className="btn-ghost" type="button" onClick={refreshDashboard}>
            Tải lại
          </button>
          <button className="btn-primary" type="button" onClick={openCreateModal}>
            + Tạo khoang
          </button>
        </div>
      </header>

      {summary && (
        <div className="stats-bar bay-stat-grid">
          <div className="stat-card"><div className="stat-value">{summary.totalBays || 0}</div><div className="stat-label">Tổng khoang</div></div>
          <div className="stat-card"><div className="stat-value">{summary.emptyBays || 0}</div><div className="stat-label">Khoang trống</div></div>
          <div className="stat-card"><div className="stat-value">{summary.inUseBays || 0}</div><div className="stat-label">Đang sử dụng</div></div>
          <div className="stat-card"><div className="stat-value">{summary.maintenanceBays || 0}</div><div className="stat-label">Bảo trì</div></div>
          <div className="stat-card"><div className="stat-value">{summary.lockedBays || 0}</div><div className="stat-label">Tạm khóa</div></div>
          <div className="stat-card"><div className="stat-value">{toNumber(summary.fillRate, 0).toFixed(2)}%</div><div className="stat-label">Tỷ lệ lấp đầy</div></div>
        </div>
      )}

      <div className="table-toolbar bay-filter-bar">
        <select
          className="search-input"
          value={filters.branchId}
          onChange={(event) => setFilters((prev) => ({ ...prev, branchId: event.target.value }))}
        >
          <option value="">T?t c? chi nh�nh</option>
          {(lookups.branches || []).map((branch) => (
            <option key={branch.id} value={branch.id}>
              {branch.name}
            </option>
          ))}
        </select>
        <select
          className="search-input"
          value={filters.area}
          onChange={(event) => setFilters((prev) => ({ ...prev, area: event.target.value }))}
        >
          <option value="">Tất cả khu vực</option>
          {(lookups.areaOptions || []).map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
        <select
          className="search-input"
          value={filters.status}
          onChange={(event) => setFilters((prev) => ({ ...prev, status: event.target.value }))}
        >
          <option value="">T?t c? trạng thái</option>
          {STATUS_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
        <select
          className="search-input"
          value={filters.sortBy}
          onChange={(event) => setFilters((prev) => ({ ...prev, sortBy: event.target.value }))}
        >
          <option value="sequence">Sắp xếp: Thứ tự khoang</option>
          <option value="code">Sắp xếp: Mã khoang</option>
          <option value="usage">Sắp xếp: Mức sử dụng</option>
        </select>
        <select
          className="search-input"
          value={filters.sortDirection}
          onChange={(event) => setFilters((prev) => ({ ...prev, sortDirection: event.target.value }))}
        >
          <option value="asc">Tăng dần</option>
          <option value="desc">Giảm dần</option>
        </select>
        <label className="bay-checkbox bay-inline-checkbox">
          <input
            type="checkbox"
            checked={filters.includeHidden}
            onChange={(event) => setFilters((prev) => ({ ...prev, includeHidden: event.target.checked }))}
          />
          <span>Hiện khoang đã ẩn</span>
        </label>
      </div>

      <div className="table-toolbar">
        <span className="result-count">{visibleRows.length} khoang | Tìm kiếm: {searchQuery || "(trống)"}</span>
      </div>

      {feedback && <div className="alert bay-alert-success">{feedback}</div>}
      {(error || formError) && <div className="alert alert-error">{error || formError}</div>}

      {showTabs && (
        <div className="bay-tabs">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              type="button"
              className={`bay-tab-btn ${currentTab === tab.id ? "active" : ""}`}
              onClick={() => setActiveTab(tab.id)}
            >
              {tab.label}
            </button>
          ))}
        </div>
      )}

      {loading ? (
        <div className="loading-state"><div className="spinner" /><p>Đang tải dữ liệu khoang...</p></div>
      ) : (
        <>
          {currentTab === "list" && (
            <>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>STT</th>
                      <th>Mã</th>
                      <th>Tên khoang</th>
                      <th>Chi nh�nh</th>
                      <th>Khu vực</th>
                      <th>Trạng thái</th>
                      <th>Sức chứa</th>
                      <th>Đang chứa</th>
                      <th>Mức sử dụng</th>
                      <th className="th-center">Thao t�c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleRows.length === 0 ? (
                      <tr><td colSpan={10}><div className="empty-state"><p>Chưa có khoang nào.</p></div></td></tr>
                    ) : (
                      pagedVisibleRows.map((item) => (
                        <tr
                          key={item.id}
                          className={Number(selectedBayId) === Number(item.id) ? "bay-row-selected" : ""}
                          onClick={() => setSelectedBayId(item.id)}
                        >
                          <td className="td-center">{item.sequenceOrder || "-"}</td>
                          <td className="td-id">{item.code}</td>
                          <td>{item.name}</td>
                          <td>{item.branchName}</td>
                          <td>{item.area}</td>
                          <td><span className={`badge ${statusBadge(item.operationalStatus)}`}>{STATUS_LABELS[item.operationalStatus] || item.operationalStatus}</span></td>
                          <td>{item.capacity}</td>
                          <td>{item.occupiedCount}</td>
                          <td>{toNumber(item.utilizationRate, 0).toFixed(2)}%</td>
                          <td className="td-center">
                            <div className="row-actions bay-row-actions">
                              <button className="btn-icon" type="button" onClick={(event) => { event.stopPropagation(); openEditModal(item); }}>Sửa</button>
                              <button className="btn-icon" type="button" onClick={(event) => { event.stopPropagation(); runMutation(() => bayApi.hideBay(item.id), "Đã ẩn khoang.").catch(() => { }); }}>Ẩn</button>
                              <button
                                className="btn-icon"
                                type="button"
                                onClick={(event) => {
                                  event.stopPropagation();
                                  if (item.isLocked) {
                                    runMutation(() => bayApi.unlockBay(item.id), "Đã mở khóa khoang.").catch(() => { });
                                  } else {
                                    runMutation(() => bayApi.lockBay(item.id), "Đã khóa khoang.").catch(() => { });
                                  }
                                }}
                              >
                                {item.isLocked ? "Mở khóa" : "Khóa"}
                              </button>
                              <select
                                className="bay-inline-select"
                                value={item.operationalStatus}
                                onClick={(event) => event.stopPropagation()}
                                onChange={(event) => runMutation(() => bayApi.updateBayStatus(item.id, event.target.value), "Đã c?p nh?t trạng thái khoang.").catch(() => { })}
                              >
                                {STATUS_OPTIONS.map((option) => (
                                  <option key={option.value} value={option.value}>{option.label}</option>
                                ))}
                              </select>
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div style={{ display: "flex", justifyContent: "center" }}>
                <TablePagination
                  totalItems={visibleRows.length}
                  currentPage={listPage}
                  pageSize={listPageSize}
                  onPageChange={setListPage}
                  onPageSizeChange={(size) => {
                    setListPageSize(size);
                    setListPage(1);
                  }}
                />
              </div>
            </>
          )}

          {currentTab === "vehicles" && (
            <div className="bay-grid-two">
              <div className="bay-panel">
                <div className="bay-panel-head">
                  <h3>Xe trong khoang</h3>
                  <select
                    className="search-input"
                    value={selectedBayId || ""}
                    onChange={(event) => setSelectedBayId(Number(event.target.value) || null)}
                  >
                    <option value="">Chọn khoang</option>
                    {visibleRows.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} - {item.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="table-wrapper">
                  <table className="admin-table">
                    <thead>
                      <tr>
                        <th>Biển số</th>
                        <th>Dòng xe</th>
                        <th>Ph�n kh�c</th>
                        <th>Thời gian trong khoang</th>
                        <th className="th-center">Thao t�c</th>
                      </tr>
                    </thead>
                    <tbody>
                      {activeVehicles.length === 0 ? (
                        <tr><td colSpan={5}><div className="empty-state"><p>Khoang chưa có xe.</p></div></td></tr>
                      ) : (
                        activeVehicles.map((item) => (
                          <tr key={item.vehicleId}>
                            <td>{item.licensePlate}</td>
                            <td>{item.brandName} {item.modelName}</td>
                            <td>{item.segment || "-"}</td>
                            <td>{toNumber(item.occupiedMinutes, 0)} phút</td>
                            <td className="td-center">
                              <button
                                className="btn-icon"
                                type="button"
                                onClick={() =>
                                  runMutation(
                                    () => bayApi.removeVehicle(selectedBayId, { vehicleId: item.vehicleId }),
                                    "Đã chuyển xe ra khỏi khoang."
                                  ).catch(() => { })
                                }
                              >
                                Chuyển ra
                              </button>
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              <div className="bay-panel">
                <div className="bay-panel-head"><h3>G�n / di?u ph?i xe</h3></div>

                <form className="admin-form bay-mini-form" onSubmit={handleAssignVehicle}>
                  <label>G�n xe v�o khoang</label>
                  <select className="search-input" value={assignForm.vehicleId} onChange={(event) => setAssignForm((prev) => ({ ...prev, vehicleId: event.target.value }))}>
                    <option value="">Chọn xe</option>
                    {(lookups.vehicles || []).map((vehicle) => (
                      <option key={vehicle.id} value={vehicle.id}>
                        {vehicle.licensePlate} - {vehicle.brandName} {vehicle.modelName}
                      </option>
                    ))}
                  </select>
                  <input className="search-input" placeholder="Trạng thái s?a ch?a (t�y chọn)" value={assignForm.serviceStatus} onChange={(event) => setAssignForm((prev) => ({ ...prev, serviceStatus: event.target.value }))} />
                  <input className="search-input" placeholder="Ghi chú (tùy chọn)" value={assignForm.note} onChange={(event) => setAssignForm((prev) => ({ ...prev, note: event.target.value }))} />
                  <button className="btn-primary" type="submit" disabled={saving || !selectedBayId}>G�n xe</button>
                </form>

                <form className="admin-form bay-mini-form" onSubmit={handleMoveVehicle}>
                  <label>Điều ph?i xe gi?a c�c khoang</label>
                  <select className="search-input" value={moveForm.vehicleId} onChange={(event) => setMoveForm((prev) => ({ ...prev, vehicleId: event.target.value }))}>
                    <option value="">Chọn xe đang trong khoang</option>
                    {activeVehicles.map((vehicle) => (
                      <option key={vehicle.vehicleId} value={vehicle.vehicleId}>
                        {vehicle.licensePlate} - {vehicle.brandName} {vehicle.modelName}
                      </option>
                    ))}
                  </select>
                  <select className="search-input" value={moveForm.toBayId} onChange={(event) => setMoveForm((prev) => ({ ...prev, toBayId: event.target.value }))}>
                    <option value="">Chọn khoang đích</option>
                    {visibleRows.filter((item) => Number(item.id) !== Number(selectedBayId)).map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} - {item.name} ({item.area})
                      </option>
                    ))}
                  </select>
                  <input className="search-input" placeholder="Trạng thái s?a ch?a (t�y chọn)" value={moveForm.serviceStatus} onChange={(event) => setMoveForm((prev) => ({ ...prev, serviceStatus: event.target.value }))} />
                  <button className="btn-primary" type="submit" disabled={saving || !selectedBayId}>Điều phối</button>
                </form>

                <form className="admin-form bay-mini-form" onSubmit={handleSuggest}>
                  <label>Tìm khoang trống phù hợp</label>
                  <select className="search-input" value={suggestForm.vehicleId} onChange={(event) => setSuggestForm((prev) => ({ ...prev, vehicleId: event.target.value }))}>
                    <option value="">Chọn xe để gợi ý</option>
                    {(lookups.vehicles || []).map((vehicle) => (
                      <option key={vehicle.id} value={vehicle.id}>
                        {vehicle.licensePlate} - {vehicle.brandName} {vehicle.modelName}
                      </option>
                    ))}
                  </select>
                  <input className="search-input" placeholder="Khu vực ưu tiên (tùy chọn)" value={suggestForm.preferredArea} onChange={(event) => setSuggestForm((prev) => ({ ...prev, preferredArea: event.target.value }))} />
                  <input className="search-input" placeholder="Trạng thái s?a ch?a (t�y chọn)" value={suggestForm.serviceStatus} onChange={(event) => setSuggestForm((prev) => ({ ...prev, serviceStatus: event.target.value }))} />
                  <button className="btn-ghost" type="submit">Lấy gợi ý</button>
                </form>

                <div className="bay-suggestion-list">
                  {suggestions.length === 0 ? (
                    <p className="result-count">Chưa có gợi ý.</p>
                  ) : (
                    suggestions.map((item) => (
                      <div key={item.id} className="bay-suggestion-item">
                        <div>
                          <strong>#{item.sequenceOrder || "-"} | {item.code} - {item.name}</strong>
                          <p>{item.branchName} | {item.area} | {item.occupiedCount}/{item.capacity}</p>
                          <p>Điểm gợi ý: {toNumber(item.score, 0).toFixed(2)}</p>
                        </div>
                        <button
                          className="btn-icon"
                          type="button"
                          onClick={() => setMoveForm((prev) => ({ ...prev, toBayId: String(item.id) }))}
                        >
                          Chọn
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}

          {currentTab === "usage" && (
            <div className="bay-grid-two">
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Mã khoang</th>
                      <th>Khu vực</th>
                      <th>Tần suất sử dụng</th>
                      <th>Tổng giờ sử dụng</th>
                      <th>Chi?m l�u nh?t</th>
                      <th>Lần dùng cuối</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(usage.byBay || []).length === 0 ? (
                      <tr><td colSpan={6}><div className="empty-state"><p>Chưa có dữ liệu sử dụng.</p></div></td></tr>
                    ) : (
                      pagedUsageRows.map((item) => (
                        <tr key={item.id}>
                          <td>{item.code}</td>
                          <td>{item.area}</td>
                          <td>{item.sessionCount}</td>
                          <td>{formatHours(item.totalUsageHours)}</td>
                          <td>{formatHours(item.longestActiveHours)}</td>
                          <td>{formatDateTime(item.lastUsedAt)}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th colSpan={4}>Lịch sử của khoang đang chọn</th>
                    </tr>
                    <tr>
                      <th>Biển số</th>
                      <th>Bắt đầu</th>
                      <th>Kết thúc</th>
                      <th>Thời lượng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(selectedBayHistory.items || []).length === 0 ? (
                      <tr><td colSpan={4}><div className="empty-state"><p>Chưa có lịch sử.</p></div></td></tr>
                    ) : (
                      pagedHistoryRows.map((item) => (
                        <tr key={item.sessionId}>
                          <td>{item.licensePlate}</td>
                          <td>{formatDateTime(item.assignedAt)}</td>
                          <td>{item.isActive ? "Đang chiếm" : formatDateTime(item.releasedAt)}</td>
                          <td>{formatHours(item.durationHours)}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div style={{ display: "flex", justifyContent: "center" }}>
                <TablePagination
                  totalItems={(usage.byBay || []).length}
                  currentPage={usagePage}
                  pageSize={usagePageSize}
                  onPageChange={setUsagePage}
                  onPageSizeChange={(size) => {
                    setUsagePageSize(size);
                    setUsagePage(1);
                  }}
                />
              </div>
              <div style={{ display: "flex", justifyContent: "center" }}>
                <TablePagination
                  totalItems={(selectedBayHistory.items || []).length}
                  currentPage={historyPage}
                  pageSize={historyPageSize}
                  onPageChange={setHistoryPage}
                  onPageSizeChange={(size) => {
                    setHistoryPageSize(size);
                    setHistoryPage(1);
                  }}
                />
              </div>
            </div>
          )}

          {currentTab === "layout" && (
            <div className="bay-layout-wrap">
              <div className="table-toolbar bay-filter-bar">
                <span className="result-count">Realtime timeline {timelineLoading ? "(đang cập nhật...)" : ""}</span>
                <button className="btn-ghost" type="button" onClick={loadTimeline}>
                  Làm mới timeline
                </button>
              </div>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Khoang</th>
                      <th>Khu vực</th>
                      <th>Trạng thái</th>
                      <th>Xe hiện tại</th>
                      <th>Lệnh sửa chữa</th>
                      <th>Bắt đầu</th>
                      <th>Thời lượng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {timelineRows.length === 0 ? (
                      <tr><td colSpan={7}><div className="empty-state"><p>Chưa có dữ liệu timeline.</p></div></td></tr>
                    ) : (
                      timelineRows.map((row) => {
                        const activeAllocation = (row.allocations || []).find((item) => !item.releasedAt) || null;
                        return (
                          <tr key={row.bayId}>
                            <td>{row.bayName || `Bay #${row.bayId}`}</td>
                            <td>{row.serviceArea || "-"}</td>
                            <td>
                              <span className={`badge ${statusBadge(String(row.operationalStatus || "").toUpperCase())}`}>
                                {STATUS_LABELS[String(row.operationalStatus || "").toUpperCase()] || row.operationalStatus}
                              </span>
                            </td>
                            <td>{activeAllocation?.licensePlate || "-"}</td>
                            <td>{activeAllocation?.serviceOrderCode || "-"}</td>
                            <td>{formatDateTime(activeAllocation?.allocatedAt)}</td>
                            <td>{activeAllocation?.actualDuration ? `${activeAllocation.actualDuration} phút` : "-"}</td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
              {(layout.branches || []).map((branch) => (
                <div key={branch.branchId} className="bay-layout-branch">
                  <h3>{branch.branchName}</h3>
                  <p className="result-count">Tổng khoang: {branch.totalBays}</p>
                  {(branch.areas || []).map((area) => (
                    <div key={`${branch.branchId}-${area.area}`} className="bay-layout-area">
                      <div className="bay-layout-area-head">
                        <strong>{area.area}</strong>
                        <span>Mật độ: {toNumber(area.densityPercent, 0).toFixed(2)}%</span>
                      </div>
                      <div className="bay-layout-grid">
                        {(area.bays || []).map((bay) => (
                          <button
                            key={bay.id}
                            type="button"
                            className={`bay-layout-card ${statusBadge(bay.status)}`}
                            onClick={() => setSelectedBayId(bay.id)}
                          >
                            <div className="bay-layout-code">{bay.code}</div>
                            <div>{bay.name}</div>
                            <div className="bay-layout-meta">Thứ tự: {bay.sequenceOrder || "-"}</div>
                            <div className="bay-layout-meta">{bay.occupiedCount}/{bay.capacity} xe</div>
                            <div className="bay-layout-meta">{STATUS_LABELS[bay.status] || bay.status}</div>
                          </button>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          )}

          {currentTab === "alerts" && (
            <>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Mức độ</th>
                      <th>Lo?i cầnh b�o</th>
                      <th>Khoang</th>
                      <th>Nội dung</th>
                      <th>Thời gian</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(alerts.items || []).length === 0 ? (
                      <tr><td colSpan={5}><div className="empty-state"><p>Không c� cầnh b�o.</p></div></td></tr>
                    ) : (
                      pagedAlertRows.map((item, idx) => (
                        <tr key={`${item.type}-${item.bayId}-${idx}`}>
                          <td><span className={`badge ${item.severity === "CRITICAL" ? "badge-danger" : item.severity === "HIGH" ? "badge-warning" : "badge-inactive"}`}>{item.severity}</span></td>
                          <td>{item.type}</td>
                          <td>{item.code} - {item.name}</td>
                          <td>{item.message}</td>
                          <td>{formatDateTime(item.createdAt)}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <div style={{ display: "flex", justifyContent: "center" }}>
                <TablePagination
                  totalItems={(alerts.items || []).length}
                  currentPage={alertPage}
                  pageSize={alertPageSize}
                  onPageChange={setAlertPage}
                  onPageSizeChange={(size) => {
                    setAlertPageSize(size);
                    setAlertPage(1);
                  }}
                />
              </div>
            </>
          )}

          {currentTab === "stats" && summary && (
            <div className="bay-grid-two">
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Khu vực</th>
                      <th>Số khoang</th>
                      <th>Sức chứa</th>
                      <th>Đang chứa</th>
                      <th>Tỷ lệ lấp đầy</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(summary.utilizationByArea || []).map((item) => (
                      <tr key={item.area}>
                        <td>{item.area}</td>
                        <td>{item.totalBays}</td>
                        <td>{item.totalCapacity}</td>
                        <td>{item.occupiedVehicles}</td>
                        <td>{toNumber(item.fillRate, 0).toFixed(2)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Chi nh�nh</th>
                      <th>Số khoang</th>
                      <th>Sức chứa</th>
                      <th>Đang chứa</th>
                      <th>Tỷ lệ lấp đầy</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(summary.utilizationByBranch || []).map((item) => (
                      <tr key={item.branchId}>
                        <td>{item.branchName}</td>
                        <td>{item.totalBays}</td>
                        <td>{item.totalCapacity}</td>
                        <td>{item.occupiedVehicles}</td>
                        <td>{toNumber(item.fillRate, 0).toFixed(2)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {modal && (
        <Modal
          title={modal.mode === "edit" ? `Cập nhật khoang #${modal.item?.id}` : "Tạo khoang mới"}
          onClose={closeModal}
        >
          <BayForm
            initial={modal.item || EMPTY_BAY_FORM}
            branches={lookups.branches || []}
            onSubmit={handleSaveBay}
            onCancel={closeModal}
            saving={saving}
          />
        </Modal>
      )}
    </div>
  );
}



