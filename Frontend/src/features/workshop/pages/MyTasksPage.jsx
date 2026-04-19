import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllVehicleBrands, getAllVehicleModels } from "@features/admin/api/vehicleCatalogApi";
import { getAllVehicles } from "@features/admin/api/customerApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MyTasks.css";
import TechnicianTaskCard from "../components/TechnicianTaskCard";
import "../components/TechnicianTaskCard.css";

const TASK_STATUS_META = {
  PENDING: { label: "Chờ thực hiện", color: "#2563eb", bg: "#eff6ff", icon: "⏳" },
  IN_PROGRESS: { label: "Đang làm", color: "#ea580c", bg: "#fff7ed", icon: "🔧" },
  INPROGRESS: { label: "Đang làm", color: "#ea580c", bg: "#fff7ed", icon: "🔧" },
  PAUSED: { label: "Tạm dừng", color: "#ea580c", bg: "#fff7ed", icon: "⏸️" },
  QC_PENDING: { label: "Hoàn thành", color: "#16a34a", bg: "#f0fdf4", icon: "✅" },
  COMPLETED: { label: "Hoàn thành", color: "#16a34a", bg: "#f0fdf4", icon: "✅" },
  CANCELLED: { label: "Đã hủy", color: "#64748b", bg: "#f8fafc", icon: "❌" },
};

const defaultTaskMeta = { label: "Không rõ", color: "#64748b", bg: "#f8fafc", icon: "❓" };
const TASK_GROUP_ORDER = ["NEW", "IN_PROGRESS", "COMPLETED"];
const TASK_GROUP_META = {
  NEW: { label: "CHỜ NHẬN", hint: "Việc mới", icon: "🆕" },
  IN_PROGRESS: { label: "ĐANG XỬ LÝ", hint: "Đang làm", icon: "🔧" },
  COMPLETED: { label: "ĐÃ HOÀN THÀNH", hint: "Đã xong", icon: "✅" },
  CANCELLED: { label: "Đã hủy", hint: "Ngừng", icon: "❌" },
};

function getTaskMeta(status = "") {
  return TASK_STATUS_META[String(status).toUpperCase().replace(/-/g, "_")] || defaultTaskMeta;
}

function fmt(value, onlyTime = false) {
  if (!value) return "--";
  const opts = onlyTime
    ? { hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh" }
    : { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh" };
  return new Date(value).toLocaleString("vi-VN", opts);
}

function fmtMinsWithZero(mins) {
  const value = Number(mins || 0);
  if (!Number.isFinite(value) || value < 0) return "--";
  if (value < 60) return `${Math.floor(value)} phút`;
  return `${Math.floor(value / 60)}g${value % 60 > 0 ? ` ${value % 60}p` : ""}`;
}

function safeMinutesBetween(start, end) {
  if (!start || !end) return 0;
  const startTime = new Date(start).getTime();
  const endTime = new Date(end).getTime();
  if (!Number.isFinite(startTime) || !Number.isFinite(endTime) || endTime <= startTime) return 0;
  return Math.round((endTime - startTime) / 60_000);
}

function toText(value) {
  if (value === null || value === undefined) return "";
  return String(value).trim();
}

function readValue(source, ...keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== null && value !== undefined) return value;
  }
  return null;
}

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function normalizeCode(value) {
  return toText(value).toUpperCase();
}

function normalizeStatus(value) {
  return normalizeCode(value).replace(/-/g, "_");
}

function getVietnamDateKey(value) {
  if (!value) return "";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
}

function isSameVietnamDate(value, todayKey) {
  if (!todayKey) return false;
  return getVietnamDateKey(value) === todayKey;
}

function getDiagnosisCompletedAt(task) {
  return task.technicianCompletedAt || task.technicalApprovedAt || task.finalApprovedAt || null;
}

function getCancelledAt(task) {
  return task.cancelledAt || task.canceledAt || task.cancelledOn || task.canceledOn || task.updatedAt || null;
}

function getTaskAcceptedAt(task) {
  return task.taskType === "DIAGNOSIS" ? task.technicianAcceptedAt : task.startedAt;
}

function getTaskCompletedAt(task) {
  return task.taskType === "DIAGNOSIS" ? getDiagnosisCompletedAt(task) : task.completedAt;
}

function getTaskStatusColor(task) {
  const statusCode = normalizeStatus(task.status);
  const acceptedAt = getTaskAcceptedAt(task);
  const completedAt = getTaskCompletedAt(task);

  if (statusCode === "CANCELLED") return "#64748b";
  if (completedAt || ["COMPLETED", "QC_PENDING"].includes(statusCode)) return "#16a34a";
  if (acceptedAt || ["IN_PROGRESS", "INPROGRESS", "PAUSED"].includes(statusCode)) return "#ea580c";
  return "#2563eb";
}

function getTaskGroup(task) {
  const statusCode = normalizeStatus(task.status);
  if (statusCode === "CANCELLED") return "CANCELLED";
  if (getTaskCompletedAt(task) || ["COMPLETED", "QC_PENDING"].includes(statusCode)) return "COMPLETED";
  if (getTaskAcceptedAt(task) || ["IN_PROGRESS", "INPROGRESS", "PAUSED"].includes(statusCode)) return "IN_PROGRESS";
  return "NEW";
}

function QuickStatCard({ icon, label, value, color }) {
  return (
    <div className="mt-stat" style={{ borderTopColor: color }}>
      <span className="mt-stat-icon">{icon}</span>
      <span className="mt-stat-val" style={{ color }}>{value}</span>
      <span className="mt-stat-lbl">{label}</span>
    </div>
  );
}

function EmptyState() {
  return (
    <div className="mt-empty">
      <span className="mt-empty-icon">📋</span>
      <strong>Chưa có tác vụ nào</strong>
      <p>Hiện tại không có tác vụ phân công nào dành cho bạn.</p>
    </div>
  );
}

export default function MyTasksPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const techId = user?.id ?? user?.staffId ?? null;

  const [repairTasks, setRepairTasks] = useState([]);
  const [diagnosisTasks, setDiagnosisTasks] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [actionTaskKey, setActionTaskKey] = useState("");
  const [nowTick, setNowTick] = useState(() => new Date());
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [lastRefresh, setLastRefresh] = useState(new Date());

  const fetchData = useCallback(async () => {
    if (!techId) return;
    setLoading(true);
    setError("");

    try {
      const [assignRes, allOrdersRes, boardRes, vehiclesRes, vehicleModelsRes, vehicleBrandsRes] = await Promise.all([
        request({ url: "/api/service-staff-assignments", method: "GET", params: { staffId: techId } }),
        request({ url: "/api/service-orders", method: "GET" }),
        serviceOrderApi.getDiagnosisDispatchBoard({ includeCompleted: true }),
        getAllVehicles(),
        getAllVehicleModels(),
        getAllVehicleBrands(),
      ]);

      const assignments = Array.isArray(assignRes) ? assignRes : (assignRes?.items || []);
      const allOrders = Array.isArray(allOrdersRes) ? allOrdersRes : (allOrdersRes?.items || []);
      const board = Array.isArray(boardRes) ? boardRes : (boardRes?.items || []);
      const vehicles = Array.isArray(vehiclesRes) ? vehiclesRes : (vehiclesRes?.items || []);
      const vehicleModels = Array.isArray(vehicleModelsRes) ? vehicleModelsRes : (vehicleModelsRes?.items || []);
      const vehicleBrands = Array.isArray(vehicleBrandsRes) ? vehicleBrandsRes : (vehicleBrandsRes?.items || []);

      const orderMap = new Map(allOrders.map((order) => [order.id ?? order.Id, order]));
      const vehicleMap = new Map(vehicles.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const vehicleModelMap = new Map(vehicleModels.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const vehicleBrandMap = new Map(vehicleBrands.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));

      const repairRows = assignments.map((assignment) => {
        const serviceOrderId = assignment.serviceOrderId ?? assignment.ServiceOrderId;
        const order = orderMap.get(serviceOrderId) || {};

        return {
          taskType: "REPAIR",
          assignmentId: assignment.id ?? assignment.Id,
          serviceOrderId,
          serviceOrderCode: assignment.serviceOrderCode ?? assignment.ServiceOrderCode ?? order.code ?? order.Code ?? `SO-${serviceOrderId}`,
          vehicleLicensePlate: assignment.vehicleLicensePlate ?? assignment.VehicleLicensePlate ?? order.vehicleLicensePlate ?? order.VehicleLicensePlate ?? "--",
          vehicleDescriptor: assignment.vehicleDescriptor ?? assignment.VehicleDescriptor ?? order.vehicleDescriptor ?? order.VehicleDescriptor ?? "",
          bayCode: assignment.bayCode ?? assignment.BayCode ?? order.bayCode ?? order.BayCode ?? "",
          name: assignment.taskName ?? assignment.TaskName ?? assignment.serviceItemName ?? assignment.ServiceItemName ?? "Hạng mục",
          status: assignment.status ?? assignment.Status ?? "PENDING",
          roleInTask: assignment.roleInTask ?? assignment.RoleInTask ?? "PRIMARY",
          isSupport: (assignment.roleInTask ?? assignment.RoleInTask ?? "PRIMARY") !== "PRIMARY",
          estimatedDuration: assignment.estimatedDuration ?? assignment.EstimatedDuration ?? null,
          startedAt: assignment.startedAt ?? assignment.StartedAt ?? null,
          completedAt: assignment.completedAt ?? assignment.CompletedAt ?? null,
          cancelledAt: assignment.cancelledAt ?? assignment.CancelledAt ?? assignment.canceledAt ?? assignment.CanceledAt ?? null,
          updatedAt: assignment.updatedAt ?? assignment.UpdatedAt ?? null,
          assignedAt: assignment.assignedAt ?? assignment.AssignedAt ?? null,
          teamName: toText(readValue(order, "primaryTeamName", "PrimaryTeamName")) || "",
        };
      });

      const diagnosisRows = board
        .map((item) => {
          const leadId = toPositiveNumber(readValue(item, "technicianId", "TechnicianId"));
          const assistantId = toPositiveNumber(readValue(item, "assistantTechnicianId", "AssistantTechnicianId"));
          const isLead = leadId === toPositiveNumber(techId);
          const isAssistant = assistantId === toPositiveNumber(techId);
          if (!isLead && !isAssistant) return null;

          const serviceOrderId = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId"));
          const order = orderMap.get(serviceOrderId) || {};
          const vehicleId =
            toPositiveNumber(readValue(item, "vehicleId", "VehicleId")) ||
            toPositiveNumber(readValue(order, "vehicleId", "VehicleId"));
          const vehicle = vehicleId ? (vehicleMap.get(vehicleId) || null) : null;
          const modelId =
            toPositiveNumber(readValue(item, "vehicleModelId", "VehicleModelId")) ||
            toPositiveNumber(readValue(order, "vehicleModelId", "VehicleModelId")) ||
            toPositiveNumber(readValue(vehicle, "modelId", "ModelId"));
          const model = modelId ? (vehicleModelMap.get(modelId) || null) : null;
          const brandId =
            toPositiveNumber(readValue(item, "vehicleBrandId", "VehicleBrandId")) ||
            toPositiveNumber(readValue(order, "vehicleBrandId", "VehicleBrandId")) ||
            toPositiveNumber(readValue(vehicle, "brandId", "BrandId")) ||
            toPositiveNumber(readValue(model, "brandId", "BrandId"));
          const brand = brandId ? (vehicleBrandMap.get(brandId) || null) : null;
          const brandName =
            toText(readValue(item, "vehicleBrandName", "VehicleBrandName")) ||
            toText(readValue(order, "vehicleBrandName", "VehicleBrandName")) ||
            toText(readValue(vehicle, "brandName", "BrandName")) ||
            toText(readValue(brand, "name", "Name"));
          const modelName =
            toText(readValue(item, "vehicleModelName", "VehicleModelName")) ||
            toText(readValue(order, "vehicleModelName", "VehicleModelName")) ||
            toText(readValue(vehicle, "modelName", "ModelName")) ||
            toText(readValue(model, "modelName", "ModelName", "name", "Name"));
          const vehicleDescriptorFallback = brandName && modelName ? `${brandName} ${modelName}` : (modelName || brandName);

          return {
            taskType: "DIAGNOSIS",
            serviceOrderId,
            serviceOrderCode: toText(readValue(item, "serviceOrderCode", "ServiceOrderCode")) || toText(readValue(order, "code", "Code")) || `SO-${serviceOrderId}`,
            vehicleLicensePlate:
              toText(readValue(item, "vehicleLicensePlate", "VehicleLicensePlate")) ||
              toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) ||
              toText(readValue(vehicle, "licensePlate", "LicensePlate")) ||
              "--",
            vehicleDescriptor:
              toText(readValue(item, "vehicleDescriptor", "VehicleDescriptor")) ||
              toText(readValue(order, "vehicleDescriptor", "VehicleDescriptor")) ||
              vehicleDescriptorFallback ||
              "Chưa rõ dòng xe",
            assignedBayId: toPositiveNumber(readValue(item, "assignedBayId", "AssignedBayId")),
            assignedBayCode: toText(readValue(item, "assignedBayCode", "AssignedBayCode")),
            status: normalizeStatus(readValue(item, "status", "Status") || "PENDING"),
            assignedAt: readValue(item, "assignedAt", "AssignedAt"),
            technicianAcceptedAt: readValue(item, "technicianAcceptedAt", "TechnicianAcceptedAt"),
            technicianCompletedAt: readValue(item, "technicianCompletedAt", "TechnicianCompletedAt"),
            technicalApprovedAt: readValue(item, "technicalApprovedAt", "TechnicalApprovedAt"),
            finalApprovedAt: readValue(item, "finalApprovedAt", "FinalApprovedAt"),
            cancelledAt: readValue(item, "cancelledAt", "CancelledAt", "canceledAt", "CanceledAt"),
            updatedAt: readValue(item, "updatedAt", "UpdatedAt"),
            teamName: toText(readValue(item, "primaryTeamName", "PrimaryTeamName", "teamName", "TeamName")),
            roleInPair: isLead ? "Thợ chính" : "Thợ phụ",
            isSupport: isAssistant,
          };
        })
        .filter(Boolean);

      setRepairTasks(repairRows);
      setDiagnosisTasks(diagnosisRows);
      setLastRefresh(new Date());
    } catch (err) {
      setError("Không thể tải danh sách tác vụ. Kiểm tra kết nối.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [techId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    const timerId = setInterval(() => {
      fetchData();
    }, 30000);
    return () => clearInterval(timerId);
  }, [fetchData]);

  useEffect(() => {
    const timerId = setInterval(() => setNowTick(new Date()), 1000);
    return () => clearInterval(timerId);
  }, []);

  const showMsg = (message) => {
    setSuccess(message);
    setTimeout(() => setSuccess(""), 3000);
  };

  const handleStartRepair = async (task) => {
    setSaving(true);
    setActionTaskKey(`${task.taskType}-${task.assignmentId || task.serviceOrderId}`);
    setError("");
    try {
      await request({
        url: `/api/service-staff-assignments/${task.assignmentId}/start`,
        method: "PUT",
        data: { startedAt: new Date().toISOString() },
      });
      await fetchData();
      showMsg("▶️ Đã bắt đầu tác vụ.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể cập nhật tác vụ.");
    } finally {
      setSaving(false);
      setActionTaskKey("");
    }
  };

  const handlePauseRepair = async (task) => {
    setSaving(true);
    setActionTaskKey(`${task.taskType}-${task.assignmentId || task.serviceOrderId}`);
    setError("");
    try {
      await request({
        url: `/api/service-staff-assignments/${task.assignmentId}/pause`,
        method: "PUT",
      });
      await fetchData();
      showMsg("⏸️ Đã tạm dừng tác vụ.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể tạm dừng tác vụ.");
    } finally {
      setSaving(false);
      setActionTaskKey("");
    }
  };

  const handleCompleteRepair = async (task) => {
    if (!window.confirm(`Xác nhận hoàn thành: "${task.name}"?`)) return;
    setSaving(true);
    setActionTaskKey(`${task.taskType}-${task.assignmentId || task.serviceOrderId}`);
    setError("");
    try {
      await request({
        url: `/api/service-staff-assignments/${task.assignmentId}/complete`,
        method: "PUT",
        data: { completedAt: new Date().toISOString() },
      });
      await fetchData();
      showMsg("✅ Tác vụ hoàn thành! Hệ thống sẽ cập nhật trạng thái lệnh.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể hoàn thành tác vụ.");
    } finally {
      setSaving(false);
      setActionTaskKey("");
    }
  };

  const handleStartDiagnosis = async (task) => {
    if (!task || !task.serviceOrderId) return;
    if (!task.assignedBayId) {
      setError("Chưa có khoang được phân công. Vui lòng liên hệ tổ trưởng để chỉ định khoang.");
      return;
    }

    setSaving(true);
    setActionTaskKey(`${task.taskType}-${task.assignmentId || task.serviceOrderId}`);
    setError("");
    try {
      await serviceOrderApi.startDiagnosis(task.serviceOrderId, {
        technicianId: toPositiveNumber(techId),
        bayId: task.assignedBayId,
      });
      await fetchData();
      showMsg("▶️ Đã nhận yêu cầu chẩn đoán.");
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể nhận yêu cầu chẩn đoán.");
    } finally {
      setSaving(false);
      setActionTaskKey("");
    }
  };

  const handleOpenTask = (task) => {
    navigate(`/workshop/technician/orders/${task.serviceOrderId}/diagnosis`);
  };

  const allTasks = useMemo(() => {
    const merged = [...diagnosisTasks, ...repairTasks];
    return merged.sort((a, b) => {
      const aCompleted = getTaskCompletedAt(a);
      const bCompleted = getTaskCompletedAt(b);
      const aAccepted = getTaskAcceptedAt(a);
      const bAccepted = getTaskAcceptedAt(b);
      const aCancelled = normalizeStatus(a.status) === "CANCELLED";
      const bCancelled = normalizeStatus(b.status) === "CANCELLED";

      const rank = (task, completed, accepted, cancelled) => {
        if (cancelled) return 4;
        if (completed) return 3;
        if (accepted) return 2;
        return 1;
      };

      const rankA = rank(a, aCompleted, aAccepted, aCancelled);
      const rankB = rank(b, bCompleted, bAccepted, bCancelled);
      if (rankA !== rankB) return rankA - rankB;

      const aTime = new Date(aCompleted || a.assignedAt || 0).getTime();
      const bTime = new Date(bCompleted || b.assignedAt || 0).getTime();
      return bTime - aTime;
    });
  }, [diagnosisTasks, repairTasks]);

  const groupedTasks = useMemo(() => {
    const groups = {
      NEW: [],
      IN_PROGRESS: [],
      COMPLETED: [],
      CANCELLED: [],
    };
    allTasks.forEach((task) => {
      groups[getTaskGroup(task)].push(task);
    });
    return groups;
  }, [allTasks]);

  const todayKey = useMemo(() => getVietnamDateKey(new Date()), [lastRefresh]);

  const stats = useMemo(() => {
    const counts = { total: 0, active: 0, doing: 0, completedToday: 0, cancelledToday: 0 };
    allTasks.forEach((task) => {
      counts.total += 1;
      const statusCode = normalizeStatus(task.status);
      const acceptedAt = getTaskAcceptedAt(task);
      const completedAt = getTaskCompletedAt(task);

      if (completedAt || ["COMPLETED", "QC_PENDING"].includes(statusCode)) {
        if (isSameVietnamDate(completedAt, todayKey)) counts.completedToday += 1;
      } else if (statusCode === "CANCELLED") {
        if (isSameVietnamDate(getCancelledAt(task), todayKey)) counts.cancelledToday += 1;
      } else if (acceptedAt || ["IN_PROGRESS", "INPROGRESS", "PAUSED"].includes(statusCode)) {
        counts.doing += 1;
        counts.active += 1;
      } else {
        counts.active += 1;
      }
    });
    return counts;
  }, [allTasks, todayKey]);

  return (
    <div className="mt-page">
      <div className="mt-header">
        <div className="mt-header-left">
          <h1 className="mt-title">🔧 Tác vụ của tôi</h1>
          <p className="mt-subtitle">
            {user?.fullName || "Thợ kỹ thuật"} · Hôm nay {new Date().toLocaleDateString("vi-VN")} ·
            <span> Cập nhật {lastRefresh.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}</span>
          </p>
        </div>
        <button className="mt-btn-refresh" onClick={() => fetchData()} disabled={loading || saving}>
          {loading ? "⏳ Đang tải..." : "🔄 Làm mới"}
        </button>
      </div>

      {error ? <div className="mt-alert mt-alert-error">⚠️ {error}</div> : null}
      {success ? <div className="mt-alert mt-alert-success">{success}</div> : null}

      <div className="mt-stats">
        <QuickStatCard icon="📋" label="Tổng tác vụ" value={stats.total} color="#6366f1" />
        <QuickStatCard icon="⏳" label="Cần xử lý" value={stats.active} color="#0ea5e9" />
        <QuickStatCard icon="🔧" label="Đang thực hiện" value={stats.doing} color="#f59e0b" />
        <QuickStatCard icon="✅" label="Hoàn thành hôm nay" value={stats.completedToday} color="#10b981" />
        <QuickStatCard icon="❌" label="Đã hủy hôm nay" value={stats.cancelledToday} color="#64748b" />
      </div>

      <div className="mt-rows-container">
        {loading && allTasks.length === 0 ? (
          <div className="mt-loading">
            <div className="mt-spinner" />
            <span>Đang tải danh sách tác vụ...</span>
          </div>
        ) : null}

        {!loading && allTasks.length === 0 ? <EmptyState /> : null}

        {TASK_GROUP_ORDER.map((groupKey) => {
          const list = groupedTasks[groupKey] || [];
          if (!list.length) return null;
          const meta = TASK_GROUP_META[groupKey];
          return (
            <section key={groupKey} className="mt-task-group">
              <div className="mt-group-header">
                <div className="mt-group-title">{meta.icon} {meta.label}</div>
                <div className="mt-group-hint">{meta.hint} {"\u00B7"} {list.length}</div>
              </div>
              <div className="mt-group-list">
                {list.map((task) => (                  <TechnicianTaskCard
                    key={`${task.taskType}-${task.assignmentId || task.serviceOrderId}-${task.status}-${task.assignedAt || ""}`}
                    taskTitle={task.taskType === "DIAGNOSIS" ? "Yêu cầu chẩn đoán" : (task.name || "Hạng mục sửa chữa")}
                    licensePlate={task.vehicleLicensePlate}
                    carModel={task.vehicleDescriptor}
                    bayCode={task.bayCode || task.assignedBayCode}
                    serviceOrderId={task.serviceOrderCode}
                    status={getTaskGroup(task)}
                    taskTypeLabel={task.isSupport ? `Hỗ trợ ${task.taskType === "DIAGNOSIS" ? "chẩn đoán" : "sửa chữa"}` : (task.taskType === "DIAGNOSIS" ? "Chẩn đoán" : "Sửa chữa")}
                    waitingMinutes={safeMinutesBetween(task.assignedAt, getTaskAcceptedAt(task) || getTaskCompletedAt(task) || getCancelledAt(task) || nowTick)}
                    processingMinutes={safeMinutesBetween(getTaskAcceptedAt(task), getTaskCompletedAt(task) || nowTick)}
                    startedAt={getTaskAcceptedAt(task)}
                    completedAt={getTaskCompletedAt(task)}
                    onStart={() => (task.taskType === "DIAGNOSIS" ? handleStartDiagnosis(task) : handleStartRepair(task))}
                    onComplete={() => (task.taskType === "REPAIR" && handleCompleteRepair(task))}
                    onOpen={() => handleOpenTask(task)}
                    isActionLoading={actionTaskKey === `${task.taskType}-${task.assignmentId || task.serviceOrderId}`}
                  />
                ))}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
