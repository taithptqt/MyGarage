import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { executionApi } from "@features/workshop/api/executionApi";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MyTasks.css";

const ROW_STATE_META = {
  PENDING: { label: "Chờ nhận", className: "mt-row-pending" },
  IN_PROGRESS: { label: "Đang xử lí", className: "mt-row-in-progress" },
  PAUSED: { label: "Đang xử lí", className: "mt-row-in-progress" },
  COMPLETED: { label: "Hoàn thành", className: "mt-row-completed" },
  QC_PENDING: { label: "Hoàn thành", className: "mt-row-completed" },
  CANCELLED: { label: "Đã hủy", className: "mt-row-cancelled" },
};

function normalizeStatus(value) {
  const key = String(value || "").toUpperCase().replace(/-/g, "_");
  if (key === "INPROGRESS") return "IN_PROGRESS";
  if (key === "CANCELED") return "CANCELLED";
  return key || "PENDING";
}

function getRowMeta(status) {
  const key = normalizeStatus(status);
  return ROW_STATE_META[key] || { label: "Không rõ", className: "mt-row-cancelled" };
}

function formatDateTime(value) {
  if (!value) return "--";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--";
  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function formatMinutes(mins) {
  if (!Number.isFinite(mins) || mins <= 0) return "0 phút";
  if (mins < 60) return `${mins} phút`;
  const hours = Math.floor(mins / 60);
  const remain = mins % 60;
  return remain > 0 ? `${hours} giờ ${remain} phút` : `${hours} giờ`;
}

function calcDurationMinutes(start, end = new Date()) {
  if (!start) return 0;
  const startDate = new Date(start);
  const endDate = new Date(end);
  if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) return 0;
  return Math.max(0, Math.round((endDate.getTime() - startDate.getTime()) / 60000));
}

function resolveWorkTypeLabel(task) {
  const isDiagnosis = task.sourceType === "DIAGNOSIS_ASSIGNMENT";
  const isSupport = String(task.roleInTask || "").toUpperCase() !== "PRIMARY";
  if (isDiagnosis) return isSupport ? "Hỗ trợ chẩn đoán" : "Chẩn đoán";
  return isSupport ? "Hỗ trợ sửa chữa" : "Sửa chữa";
}

function resolveVehicleLine(task) {
  const brand = String(task.vehicleBrandName || "").trim();
  const model = String(task.vehicleModelName || "").trim();
  if (brand || model) return `${brand} ${model}`.trim();
  return String(task.vehicleDescriptor || "").trim() || "--";
}

function TaskRow({ task, nowTick, onAccept, onComplete, onOpenDetail, saving }) {
  const status = normalizeStatus(task.status);
  const meta = getRowMeta(status);
  const workType = resolveWorkTypeLabel(task);
  const vehicleLine = resolveVehicleLine(task);
  const waitingMinutes = calcDurationMinutes(task.assignedAt, nowTick);
  const processingMinutes = calcDurationMinutes(task.startedAt, nowTick);
  const handledMinutes = calcDurationMinutes(task.startedAt, task.completedAt);

  const canAccept = status === "PENDING";
  const canComplete = task.sourceType !== "DIAGNOSIS_ASSIGNMENT" && (status === "IN_PROGRESS" || status === "PAUSED");

  return (
    <article className={`mt-task-row ${meta.className}`} onClick={() => onOpenDetail(task)}>
      <div className="mt-row-main">
        <div className="mt-row-topline">
          <span className="mt-so-code">{task.serviceOrderCode || `SO-${task.serviceOrderId || "--"}`}</span>
          <span className="mt-sep">|</span>
          <span className="mt-plate">{task.vehicleLicensePlate || "--"}</span>
          <span className="mt-sep">|</span>
          <span className="mt-vehicle">{vehicleLine}</span>
        </div>

        <div className="mt-row-secondline">
          <span className="mt-task-name">{task.name || "Hạng mục sửa chữa"}</span>
          <span className="mt-task-chip">{workType}</span>
          {task.bayCode ? <span className="mt-bay-chip">Khoang: {task.bayCode}</span> : null}
        </div>

        <div className="mt-row-statusline">
          <span className="mt-status-badge">{meta.label}</span>
          {status === "PENDING" && <span className="mt-status-detail">Đã chờ: {formatMinutes(waitingMinutes)}</span>}
          {(status === "IN_PROGRESS" || status === "PAUSED") && (
            <>
              <span className="mt-status-detail">Đã nhận lúc: {formatDateTime(task.startedAt)}</span>
              <span className="mt-status-detail">Đang xử lí: {formatMinutes(processingMinutes)}</span>
            </>
          )}
          {(status === "COMPLETED" || status === "QC_PENDING") && (
            <>
              {task.startedAt ? <span className="mt-status-detail">Đã nhận lúc: {formatDateTime(task.startedAt)}</span> : null}
              <span className="mt-status-detail">Hoàn thành lúc: {formatDateTime(task.completedAt)}</span>
              {task.startedAt && task.completedAt ? <span className="mt-status-detail">Tổng xử lí: {formatMinutes(handledMinutes)}</span> : null}
            </>
          )}
          {status === "CANCELLED" && <span className="mt-status-detail">Yêu cầu đã bị hủy.</span>}
        </div>
      </div>

      <div className="mt-row-actions" onClick={(event) => event.stopPropagation()}>
        {canAccept ? (
          <button type="button" className="mt-btn-accept" onClick={() => onAccept(task)} disabled={saving}>
            {saving ? "Đang xử lí..." : "Nhận yêu cầu"}
          </button>
        ) : null}

        {canComplete ? (
          <button type="button" className="mt-btn-complete" onClick={() => onComplete(task)} disabled={saving}>
            {saving ? "Đang xử lí..." : "Hoàn thành"}
          </button>
        ) : null}
      </div>
    </article>
  );
}

export default function TechnicianTaskRowsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const techId =
    user?.staffId ??
    user?.StaffId ??
    user?.profile?.staffId ??
    user?.profile?.StaffId ??
    user?.id ??
    null;

  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [lastRefresh, setLastRefresh] = useState(new Date());
  const [nowTick, setNowTick] = useState(new Date());

  const fetchData = useCallback(async () => {
    if (!techId) {
      setTasks([]);
      setError("Thiếu thông tin nhân sự để tải phân công.");
      return;
    }

    setLoading(true);
    setError("");
    try {
      const toArray = (res) => {
        if (Array.isArray(res)) return res;
        if (Array.isArray(res?.items)) return res.items;
        if (Array.isArray(res?.data)) return res.data;
        if (Array.isArray(res?.result)) return res.result;
        return [];
      };

      const [assignRes, allOrdersRes, allDetailsRes] = await Promise.all([
        request({
          url: "/api/service-staff-assignments",
          method: "GET",
          params: { staffId: techId, technicianId: techId },
        }),
        request({ url: "/api/service-orders", method: "GET" }),
        request({ url: "/api/service-order-details", method: "GET" }),
      ]);

      let assigns = toArray(assignRes);
      if (assigns.length === 0) {
        const allAssignRes = await request({ url: "/api/service-staff-assignments", method: "GET" });
        const allAssignments = toArray(allAssignRes);
        const targetId = Number(techId);
        assigns = allAssignments.filter((item) => {
          const candidateIds = [
            item?.staffId,
            item?.StaffId,
            item?.technicianId,
            item?.TechnicianId,
            item?.assignedStaffId,
            item?.AssignedStaffId,
            item?.assigneeId,
            item?.AssigneeId,
          ].map((v) => Number(v));
          return candidateIds.some((id) => id > 0 && id === targetId);
        });
      }

      if (assigns.length === 0) {
        const diagBoardRes = await request({
          url: "/api/diagnoses/dispatch-board",
          method: "GET",
          params: { includeCompleted: true },
        });
        const boardItems = toArray(diagBoardRes);
        const targetId = Number(techId);
        const diagnosisTasks = boardItems
          .filter((item) => {
            const ids = [
              item?.technicianId,
              item?.TechnicianId,
              item?.assistantTechnicianId,
              item?.AssistantTechnicianId,
            ].map((v) => Number(v));
            const supporting = item?.supportingAssignments ?? item?.SupportingAssignments ?? [];
            const supportIds = Array.isArray(supporting)
              ? supporting.flatMap((row) => [
                Number(row?.technicianId ?? row?.TechnicianId),
                Number(row?.assistantTechnicianId ?? row?.AssistantTechnicianId),
              ])
              : [];
            return [...ids, ...supportIds].some((id) => id > 0 && id === targetId);
          })
          .map((item) => {
            const workflow = normalizeStatus(item?.workflowStatus ?? item?.WorkflowStatus ?? "PENDING");
            const normalizedStatus =
              workflow === "COMPLETED"
                ? "COMPLETED"
                : workflow === "CANCELLED"
                  ? "CANCELLED"
                  : workflow === "ASSIGNED" || workflow === "IN_PROGRESS"
                    ? "IN_PROGRESS"
                    : "PENDING";
            const isPrimary = Number(item?.technicianId ?? item?.TechnicianId) === targetId;
            const diagnosisIds = item?.diagnosisIds ?? item?.DiagnosisIds ?? [];
            const diagnosisKey = Array.isArray(diagnosisIds) && diagnosisIds.length > 0 ? diagnosisIds[0] : "0";
            return {
              assignmentId: `diag-${item?.serviceOrderId ?? item?.ServiceOrderId ?? "0"}-${diagnosisKey}`,
              serviceOrderId: item?.serviceOrderId ?? item?.ServiceOrderId,
              serviceOrderCode: item?.serviceOrderCode ?? item?.ServiceOrderCode ?? "--",
              vehicleLicensePlate: item?.licensePlate ?? item?.LicensePlate ?? "--",
              vehicleDescriptor: item?.vehicleDescriptor ?? item?.VehicleDescriptor ?? "",
              vehicleBrandName: item?.vehicleBrandName ?? item?.VehicleBrandName ?? "",
              vehicleModelName: item?.vehicleModelName ?? item?.VehicleModelName ?? "",
              bayCode: item?.assignedBayCode ?? item?.AssignedBayCode ?? "",
              name: "Yêu cầu chẩn đoán",
              description: item?.teamName ?? item?.TeamName ?? "",
              status: normalizedStatus,
              roleInTask: isPrimary ? "PRIMARY" : "ASSISTANT",
              estimatedDuration: null,
              startedAt: item?.technicianAcceptedAt ?? item?.TechnicianAcceptedAt ?? item?.startedAt ?? item?.StartedAt ?? null,
              completedAt: item?.technicianCompletedAt ?? item?.TechnicianCompletedAt ?? null,
              assignedAt: item?.assignedAt ?? item?.AssignedAt ?? null,
              sourceType: "DIAGNOSIS_ASSIGNMENT",
            };
          });
        setTasks(diagnosisTasks);
        setLastRefresh(new Date());
        return;
      }

      const allOrders = toArray(allOrdersRes);
      const allDetails = toArray(allDetailsRes);
      const orderMap = new Map(allOrders.map((order) => [order.id ?? order.Id, order]));
      const detailMap = new Map(allDetails.map((detail) => [detail.id ?? detail.Id, detail]));

      const enriched = assigns.map((item) => {
        const detailId = item.serviceOrderDetailId ?? item.ServiceOrderDetailId;
        const detail = detailMap.get(detailId) || {};
        const soId = detail.serviceOrderId ?? detail.ServiceOrderId ?? item.serviceOrderId ?? item.ServiceOrderId;
        const order = orderMap.get(soId) || {};
        return {
          assignmentId: item.id ?? item.Id,
          serviceOrderId: soId,
          serviceOrderCode: item.serviceOrderCode ?? item.ServiceOrderCode ?? order.code ?? order.Code ?? `SO-${soId}`,
          vehicleLicensePlate: item.vehicleLicensePlate ?? item.VehicleLicensePlate ?? order.vehicleLicensePlate ?? order.VehicleLicensePlate ?? "--",
          vehicleDescriptor: item.vehicleDescriptor ?? item.VehicleDescriptor ?? order.vehicleDescriptor ?? order.VehicleDescriptor ?? "",
          vehicleBrandName: item.vehicleBrandName ?? item.VehicleBrandName ?? order.vehicleBrandName ?? order.VehicleBrandName ?? "",
          vehicleModelName: item.vehicleModelName ?? item.VehicleModelName ?? order.vehicleModelName ?? order.VehicleModelName ?? "",
          bayCode: item.bayCode ?? item.BayCode ?? order.bayCode ?? order.BayCode ?? "",
          name: detail.itemName ?? detail.ItemName ?? "Hạng mục sửa chữa",
          description: detail.description ?? detail.Description ?? "",
          status: item.status ?? item.Status ?? "PENDING",
          roleInTask: item.roleInTask ?? item.RoleInTask ?? "PRIMARY",
          startedAt: item.startedAt ?? item.StartedAt ?? null,
          completedAt: item.completedAt ?? item.CompletedAt ?? null,
          assignedAt: item.assignedAt ?? item.AssignedAt ?? null,
          sourceType: "SERVICE_STAFF_ASSIGNMENT",
        };
      });

      setTasks(enriched);
      setLastRefresh(new Date());
    } catch (err) {
      setError("Không thể tải danh sách phân công.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [techId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    const id = setInterval(() => {
      setNowTick(new Date());
      fetchData();
    }, 30000);
    return () => clearInterval(id);
  }, [fetchData]);

  const showMsg = (msg) => {
    setSuccess(msg);
    setTimeout(() => setSuccess(""), 3000);
  };

  const handleAccept = async (task) => {
    setSaving(true);
    setError("");
    try {
      if (task.sourceType === "DIAGNOSIS_ASSIGNMENT") {
        await serviceOrderApi.acceptDiagnosisRequest(task.serviceOrderId, techId, {
          acceptedAt: new Date().toISOString(),
        });
      } else {
        await executionApi.startAssignment(task.assignmentId, {
          startedAt: new Date().toISOString(),
        });
      }
      await fetchData();
      showMsg("Đã nhận yêu cầu.");
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Không thể nhận yêu cầu.");
    } finally {
      setSaving(false);
    }
  };

  const handleComplete = async (task) => {
    if (!window.confirm(`Xác nhận hoàn thành: "${task.name}"?`)) return;
    setSaving(true);
    setError("");
    try {
      await executionApi.completeAssignment(task.assignmentId, {
        completedAt: new Date().toISOString(),
      });
      await fetchData();
      showMsg("Đã hoàn thành yêu cầu.");
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Không thể hoàn thành yêu cầu.");
    } finally {
      setSaving(false);
    }
  };

  const handleOpenDetail = (task) => {
    if (!task?.serviceOrderId) return;
    navigate(`/workshop/technician/orders/${task.serviceOrderId}/diagnosis`);
  };

  const sortedTasks = useMemo(() => {
    return [...tasks].sort((a, b) => {
      const aTime = new Date(a.assignedAt || 0).getTime();
      const bTime = new Date(b.assignedAt || 0).getTime();
      return bTime - aTime;
    });
  }, [tasks]);

  return (
    <div className="mt-page">
      <header className="mt-header">
        <div>
          <h1 className="mt-title">Phân công theo dòng dữ liệu</h1>
          <p className="mt-subtitle">
            {user?.fullName || "Kỹ thuật viên"} · Hôm nay {new Date().toLocaleDateString("vi-VN")} · Cập nhật{" "}
            {lastRefresh.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}
          </p>
        </div>
        <button
          type="button"
          className="mt-btn-refresh"
          onClick={() => {
            setNowTick(new Date());
            fetchData();
          }}
          disabled={loading}
        >
          {loading ? "Đang tải..." : "Làm mới"}
        </button>
      </header>

      {error ? <div className="mt-alert mt-alert-error">{error}</div> : null}
      {success ? <div className="mt-alert mt-alert-success">{success}</div> : null}

      {loading && tasks.length === 0 ? (
        <div className="mt-loading">Đang tải danh sách phân công...</div>
      ) : sortedTasks.length === 0 ? (
        <div className="mt-empty">
          <strong>Chưa có dữ liệu phân công</strong>
          <p>Hiện chưa có yêu cầu nào được phân công cho bạn.</p>
        </div>
      ) : (
        <section className="mt-task-list">
          {sortedTasks.map((task) => (
            <TaskRow
              key={`${task.assignmentId}-${task.status}`}
              task={task}
              nowTick={nowTick}
              onAccept={handleAccept}
              onComplete={handleComplete}
              onOpenDetail={handleOpenDetail}
              saving={saving}
            />
          ))}
        </section>
      )}
    </div>
  );
}
