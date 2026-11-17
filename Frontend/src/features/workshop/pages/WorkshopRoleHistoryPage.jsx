import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import {
  Alert,
  AlertIcon,
  Badge,
  Box,
  Button,
  Divider,
  Flex,
  Heading,
  HStack,
  SimpleGrid,
  Spinner,
  Text,
  VStack,
} from "@chakra-ui/react";
import {
  ArrowForwardIcon,
  CalendarIcon,
  CheckCircleIcon,
  RepeatClockIcon,
  RepeatIcon,
  TimeIcon,
} from "@chakra-ui/icons";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStaff } from "@features/admin/api/staffApi";
import { useAuth } from "@shared/core/context/AuthContext";
import FilterDropdown from "@shared/components/FilterDropdown";
import DateRangePickerModal, { getDateRangeButtonLabel } from "@features/admin/components/DateRangePickerModal";
import { useSearch } from "@shared/core/context/SearchContext";

const ROLE_CONFIG = {
  TECH: {
    title: "Lịch sử xử lý",
    dashboardPath: "/workshop/technician",
    openOrderPath: (orderId) => `/workshop/technician/orders/${orderId}/diagnosis`,
    openDiagnosisPath: (orderId) => `/workshop/technician/orders/${orderId}/diagnosis`,
    processedLabel: "Hoàn tất chẩn đoán",
  },
  TEAM_LEADER: {
    title: "Lịch sử xử lý",
    dashboardPath: "/workshop/team-leader",
    openOrderPath: (orderId) => `/workshop/team-leader/dispatch/${orderId}`,
    openDiagnosisPath: (orderId) => `/workshop/team-leader/orders/${orderId}/quote`,
    processedLabel: "Duyệt kỹ thuật",
  },
  FOREMAN: {
    title: "Lịch sử xử lý",
    dashboardPath: "/workshop/foreman",
    openOrderPath: (orderId) => `/workshop/foreman/dispatch/${orderId}`,
    openDiagnosisPath: (orderId) => `/workshop/foreman/orders/${orderId}/quote`,
    processedLabel: "Duyệt cuối",
  },
};

const DIAGNOSIS_WORKFLOW_STATUS_LABELS = {
  NEW: "Mới tạo",
  IN_PROGRESS: "Đang chẩn đoán",
  SUBMITTED: "Đã gửi duyệt",
  TL_APPROVED: "Tổ trưởng đã duyệt",
  TL_REJECTED_REDIAGNOSE: "Từ chối khám lại",
  ESCALATED_TO_FOREMAN: "Chuyển cấp quản đốc",
  FOREMAN_REJECTED_REDIAGNOSE: "Quản đốc từ chối",
  FOREMAN_APPROVED_LOCKED: "Quản đốc duyệt cuối",
  CANCELED: "Đã hủy",
};

const HISTORY_STATUS_BY_ROLE = {
  FOREMAN: ["FOREMAN_APPROVED_LOCKED", "FOREMAN_REJECTED_REDIAGNOSE", "CANCELED"],
  TEAM_LEADER: ["TL_APPROVED", "TL_REJECTED_REDIAGNOSE", "FOREMAN_REJECTED_REDIAGNOSE", "CANCELED"],
  TECH: ["TL_APPROVED", "FOREMAN_APPROVED_LOCKED", "TL_REJECTED_REDIAGNOSE", "FOREMAN_REJECTED_REDIAGNOSE", "CANCELED"],
};

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

function formatDateTime(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";
  return parsed.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function resolveCurrentStaff(staffs, user) {
  const userId = toPositiveNumber(readValue(user, "id", "Id"));
  if (userId) {
    const byId = staffs.find((item) => toPositiveNumber(readValue(item, "id", "Id")) === userId);
    if (byId) return byId;
  }

  const userEmail = toText(readValue(user, "email", "Email")).toLowerCase();
  if (userEmail) {
    const byEmail = staffs.find((item) => toText(readValue(item, "email", "Email")).toLowerCase() === userEmail);
    if (byEmail) return byEmail;
  }

  const userName = toText(readValue(user, "fullName", "FullName")).toLowerCase();
  if (userName) {
    const byName = staffs.find((item) => toText(readValue(item, "fullName", "FullName")).toLowerCase() === userName);
    if (byName) return byName;
  }

  return null;
}

function resolveWorkflowStatus(item) {
  const workflowStatus = normalizeCode(readValue(item, "workflowStatus", "WorkflowStatus"));
  if (workflowStatus && DIAGNOSIS_WORKFLOW_STATUS_LABELS[workflowStatus]) return workflowStatus;

  const diagnosisStatus = normalizeCode(readValue(item, "status", "Status", "diagnosisStatus", "DiagnosisStatus"));
  const teamLeaderApprovalStatus = normalizeCode(
    readValue(item, "teamLeaderApprovalStatus", "TeamLeaderApprovalStatus", "technicalApprovalStatus", "TechnicalApprovalStatus")
  );
  const foremanApprovalStatus = normalizeCode(
    readValue(item, "foremanApprovalStatus", "ForemanApprovalStatus", "finalApprovalStatus", "FinalApprovalStatus")
  );
  const queueState = normalizeCode(readValue(item, "queueState", "QueueState"));
  const queueColor = normalizeCode(readValue(item, "queueColor", "QueueColor"));
  const submittedAt = readValue(item, "submittedAt", "SubmittedAt");

  if (diagnosisStatus === "CANCELED") return "CANCELED";
  if (foremanApprovalStatus === "APPROVED") return "FOREMAN_APPROVED_LOCKED";
  if (foremanApprovalStatus === "REJECTED") return "FOREMAN_REJECTED_REDIAGNOSE";
  if (teamLeaderApprovalStatus === "REJECTED") return "TL_REJECTED_REDIAGNOSE";
  if (["BYPASSED", "ESCALATED"].includes(teamLeaderApprovalStatus) || queueColor === "RED") return "ESCALATED_TO_FOREMAN";
  if (teamLeaderApprovalStatus === "APPROVED") return "TL_APPROVED";
  if (submittedAt) return "SUBMITTED";
  if (queueState === "IN_PROGRESS") return "IN_PROGRESS";
  return "NEW";
}

function toHistoryFilterStatus(statusCode) {
  const normalized = normalizeCode(statusCode);
  if (normalized === "CANCELED") return "CANCELED";
  if (["TL_REJECTED_REDIAGNOSE", "FOREMAN_REJECTED_REDIAGNOSE"].includes(normalized)) return "REDIAGNOSE";
  if (["TL_APPROVED", "FOREMAN_APPROVED_LOCKED"].includes(normalized)) return "COMPLETED";
  return "ALL";
}

function resolveAssignedAt(item) {
  return readValue(
    item,
    "assignedAt",
    "AssignedAt",
    "technicianAcceptedAt",
    "TechnicianAcceptedAt",
    "leadTechnicianAcceptedAt",
    "LeadTechnicianAcceptedAt",
    "startedAt",
    "StartedAt"
  );
}

function resolveCompletedAt(item) {
  return (
    readValue(item, "finalApprovedAt", "FinalApprovedAt") ||
    readValue(item, "technicalApprovedAt", "TechnicalApprovedAt") ||
    readValue(item, "technicianCompletedAt", "TechnicianCompletedAt") ||
    readValue(item, "leadTechnicianCompletedAt", "LeadTechnicianCompletedAt") ||
    readValue(item, "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt") ||
    readValue(item, "assignedAt", "AssignedAt")
  );
}

function resolveLatestRecordAt(item) {
  return (
    resolveCompletedAt(item) ||
    readValue(item, "updatedAt", "UpdatedAt") ||
    readValue(item, "diagnosedAt", "DiagnosedAt") ||
    readValue(item, "createdAt", "CreatedAt") ||
    readValue(item, "assignedAt", "AssignedAt")
  );
}

function resolveHistoryStatusFromDiagnosis(item, roleCode) {
  const diagnosisStatus = normalizeCode(readValue(item, "status", "Status", "diagnosisStatus", "DiagnosisStatus"));
  const cancellationStatus = normalizeCode(readValue(item, "cancellationStatus", "CancellationStatus"));
  const teamLeaderApprovalStatus = normalizeCode(readValue(item, "teamLeaderApprovalStatus", "TeamLeaderApprovalStatus"));
  const finalApprovalStatus = normalizeCode(readValue(item, "finalApprovalStatus", "FinalApprovalStatus"));

  if (
    diagnosisStatus === "CANCELED" ||
    ["APPROVED", "ACCEPTED", "CANCELED"].includes(cancellationStatus)
  ) {
    return "CANCELED";
  }
  if (finalApprovalStatus === "REJECTED") return "FOREMAN_REJECTED_REDIAGNOSE";
  if (teamLeaderApprovalStatus === "REJECTED") return "TL_REJECTED_REDIAGNOSE";

  if (roleCode === "FOREMAN") {
    return finalApprovalStatus === "APPROVED" ? "FOREMAN_APPROVED_LOCKED" : null;
  }

  if (roleCode === "TEAM_LEADER") {
    return teamLeaderApprovalStatus === "APPROVED" ? "TL_APPROVED" : null;
  }

  if (roleCode === "TECH") {
    if (finalApprovalStatus === "APPROVED") return "FOREMAN_APPROVED_LOCKED";
    if (teamLeaderApprovalStatus === "APPROVED") return "TL_APPROVED";
  }

  return null;
}

function resolveCompletedAtFromDiagnosis(item, workflowStatus) {
  if (workflowStatus === "CANCELED") {
    return (
      readValue(item, "cancellationDecisionAt", "CancellationDecisionAt") ||
      readValue(item, "updatedAt", "UpdatedAt") ||
      readValue(item, "createdAt", "CreatedAt")
    );
  }

  if (workflowStatus === "FOREMAN_APPROVED_LOCKED") {
    return (
      readValue(item, "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt") ||
      readValue(item, "updatedAt", "UpdatedAt") ||
      readValue(item, "diagnosedAt", "DiagnosedAt") ||
      readValue(item, "createdAt", "CreatedAt")
    );
  }

  if (workflowStatus === "TL_APPROVED") {
    return (
      readValue(item, "approvedByForemanAt", "ApprovedByForemanAt") ||
      readValue(item, "updatedAt", "UpdatedAt") ||
      readValue(item, "diagnosedAt", "DiagnosedAt") ||
      readValue(item, "createdAt", "CreatedAt")
    );
  }

  return (
    readValue(item, "updatedAt", "UpdatedAt") ||
    readValue(item, "diagnosedAt", "DiagnosedAt") ||
    readValue(item, "createdAt", "CreatedAt")
  );
}

function toTimestamp(value) {
  const ts = new Date(value || 0).getTime();
  return Number.isFinite(ts) ? ts : 0;
}

function resolveAssignmentSequence(item) {
  return (
    toPositiveNumber(readValue(item, "diagnosisAssignmentId", "DiagnosisAssignmentId")) ||
    toPositiveNumber(readValue(item, "latestDiagnosisAssignmentId", "LatestDiagnosisAssignmentId")) ||
    toPositiveNumber(readValue(item, "assignmentId", "AssignmentId")) ||
    0
  );
}

function getProcessedTimelineLabel(roleCode) {
  if (roleCode === "FOREMAN") return "Duyệt cuối";
  if (roleCode === "TEAM_LEADER") return "Duyệt kỹ thuật";
  return "Hoàn tất xử lý";
}

function toDateText(dateObj) {
  if (!(dateObj instanceof Date) || Number.isNaN(dateObj.getTime())) return "";
  const y = dateObj.getFullYear();
  const m = String(dateObj.getMonth() + 1).padStart(2, "0");
  const d = String(dateObj.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function toStartOfDay(dateText) {
  if (!dateText) return null;
  const date = new Date(`${dateText}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function toEndOfDay(dateText) {
  if (!dateText) return null;
  const date = new Date(`${dateText}T23:59:59.999`);
  return Number.isNaN(date.getTime()) ? null : date;
}

export default function WorkshopRoleHistoryPage() {
  const { user } = useAuth();
  const { searchQuery } = useSearch();
  const location = useLocation();
  const navigate = useNavigate();

  const roleCode = normalizeCode(readValue(user, "roleCode", "RoleCode"));
  const roleConfig = ROLE_CONFIG[roleCode] || null;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [rows, setRows] = useState([]);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [sortBy, setSortBy] = useState("COMPLETED_DESC");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [isDateRangePickerOpen, setIsDateRangePickerOpen] = useState(false);

  useEffect(() => {
    document.title = "Lịch sử xử lý | MyGarage";
  }, []);

  const fetchHistory = useCallback(async () => {
    if (!roleConfig) return;
    setLoading(true);
    setError("");

    try {
      const [boardRes, diagnosesRes, staffsRes, ordersRes] = await Promise.all([
        serviceOrderApi.getDiagnosisDispatchBoard({ includeCompleted: true }).catch(() => []),
        serviceOrderApi.getDiagnoses().catch(() => []),
        getAllStaff().catch(() => []),
        serviceOrderApi.getAll().catch(() => []),
      ]);

      const board = Array.isArray(boardRes) ? boardRes : [];
      const diagnoses = Array.isArray(diagnosesRes) ? diagnosesRes : [];
      const staffs = Array.isArray(staffsRes) ? staffsRes : [];
      const rawOrders = Array.isArray(ordersRes)
        ? ordersRes
        : Array.isArray(ordersRes?.items)
        ? ordersRes.items
        : Array.isArray(ordersRes?.data)
        ? ordersRes.data
        : [];

      const currentStaff = resolveCurrentStaff(staffs, user);
      const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id"));
      const currentTeamId = toPositiveNumber(readValue(currentStaff, "teamId", "TeamId"));

      if (!currentStaffId && (roleCode === "TECH" || roleCode === "TEAM_LEADER")) {
        setRows([]);
        setError("Không xác định được hồ sơ nhân sự của tài khoản hiện tại.");
        return;
      }

      const ordersById = new Map();
      for (const order of rawOrders) {
        const id = toPositiveNumber(readValue(order, "id", "Id"));
        if (id) {
          ordersById.set(id, order);
        }
      }

      const boardByServiceOrderId = new Map();
      for (const item of board) {
        const sid = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId")) || 0;
        if (sid > 0 && !boardByServiceOrderId.has(sid)) {
          boardByServiceOrderId.set(sid, item);
        }
      }

      const latestByDiagnosisId = new Map();
      for (const item of diagnoses) {
        const diagnosisId = toPositiveNumber(readValue(item, "id", "Id")) || 0;
        if (!diagnosisId) continue;

        const assignmentSequence = resolveAssignmentSequence(item);
        const latestRecordAt = resolveLatestRecordAt(item);
        const existing = latestByDiagnosisId.get(diagnosisId);
        if (!existing) {
          latestByDiagnosisId.set(diagnosisId, { item, latestRecordAt, diagnosisId, assignmentSequence });
          continue;
        }

        if (assignmentSequence > (existing.assignmentSequence || 0)) {
          latestByDiagnosisId.set(diagnosisId, { item, latestRecordAt, diagnosisId, assignmentSequence });
          continue;
        }

        if (assignmentSequence === (existing.assignmentSequence || 0)) {
          const currentTs = toTimestamp(latestRecordAt);
          const existingTs = toTimestamp(existing.latestRecordAt);
          if (currentTs > existingTs) {
            latestByDiagnosisId.set(diagnosisId, { item, latestRecordAt, diagnosisId, assignmentSequence });
          }
        }
      }

      const latestRows = Array.from(latestByDiagnosisId.values())
        .map(({ item, latestRecordAt, diagnosisId }) => {
          const serviceOrderId = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId")) || 0;
          const workflowStatus = resolveHistoryStatusFromDiagnosis(item, roleCode);
          if (!workflowStatus) return null;

          const boardMeta = boardByServiceOrderId.get(serviceOrderId);
          const orderMeta = ordersById.get(serviceOrderId);

          const completedAt = resolveCompletedAtFromDiagnosis(item, workflowStatus);
          const assignedAt =
            readValue(item, "assignedAt", "AssignedAt") ||
            readValue(boardMeta, "assignedAt", "AssignedAt") ||
            readValue(orderMeta, "assignedAt", "AssignedAt") ||
            null;
          const startedAt =
            readValue(item, "technicianAcceptedAt", "TechnicianAcceptedAt", "startedAt", "StartedAt") ||
            readValue(boardMeta, "startedAt", "StartedAt") ||
            readValue(orderMeta, "startedAt", "StartedAt") ||
            null;
          const technicalApprovedAt =
            readValue(item, "approvedByForemanAt", "ApprovedByForemanAt", "technicalApprovedAt", "TechnicalApprovedAt") ||
            readValue(boardMeta, "technicalApprovedAt", "TechnicalApprovedAt") ||
            null;
          const finalApprovedAt =
            readValue(item, "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt", "finalApprovedAt", "FinalApprovedAt") ||
            readValue(boardMeta, "finalApprovedAt", "FinalApprovedAt") ||
            null;

          const code =
            toText(readValue(boardMeta, "serviceOrderCode", "ServiceOrderCode")) ||
            toText(readValue(orderMeta, "serviceOrderCode", "ServiceOrderCode", "code", "Code")) ||
            `SO-${serviceOrderId}`;

          const customer =
            toText(readValue(boardMeta, "customerName", "CustomerName")) ||
            toText(readValue(orderMeta, "customerName", "CustomerName")) ||
            toText(readValue(item, "customerName", "CustomerName")) ||
            "";

          const plate =
            toText(readValue(boardMeta, "vehicleLicensePlate", "VehicleLicensePlate")) ||
            toText(readValue(orderMeta, "vehicleLicensePlate", "VehicleLicensePlate", "licensePlate", "LicensePlate")) ||
            toText(readValue(item, "vehicleLicensePlate", "VehicleLicensePlate")) ||
            "--";

          const brand =
            toText(readValue(orderMeta, "vehicleBrandName", "VehicleBrandName")) ||
            toText(readValue(boardMeta, "vehicleBrandName", "VehicleBrandName"));
          const model =
            toText(readValue(orderMeta, "vehicleModelName", "VehicleModelName")) ||
            toText(readValue(boardMeta, "vehicleModelName", "VehicleModelName"));
          const descriptor =
            toText(readValue(boardMeta, "vehicleDescriptor", "VehicleDescriptor")) ||
            toText(readValue(orderMeta, "vehicleDescriptor", "VehicleDescriptor")) ||
            [brand, model].filter(Boolean).join(" ") ||
            "";

          const teamName =
            toText(readValue(item, "assignedTeamName", "AssignedTeamName")) ||
            toText(readValue(orderMeta, "teamName", "TeamName", "primaryTeamName", "PrimaryTeamName")) ||
            toText(readValue(boardMeta, "primaryTeamName", "PrimaryTeamName", "teamName", "TeamName")) ||
            "--";

          return {
            diagnosisId,
            serviceOrderId,
            serviceOrderCode: code,
            customerName: customer,
            vehicleLicensePlate: plate,
            vehicleDescriptor: descriptor,
            workflowStatus,
            teamName,
            assignedAt,
            startedAt,
            technicalApprovedAt,
            finalApprovedAt,
            completedAt,
            latestRecordAt: completedAt || latestRecordAt,
            raw: item,
          };
        })
        .filter(Boolean);

      const roleStatuses = HISTORY_STATUS_BY_ROLE[roleCode] || [];
      const filteredByRole = latestRows.filter((row) => {
        const item = row.raw;
        const leadId = toPositiveNumber(readValue(item, "technicianId", "TechnicianId"));
        const assistantId = toPositiveNumber(readValue(item, "assistantTechnicianId", "AssistantTechnicianId"));
        const teamLeaderApprovedById = toPositiveNumber(
          readValue(item, "approvedByForemanId", "ApprovedByForemanId", "teamLeaderApprovedById", "TeamLeaderApprovedById")
        );
        const primaryTeamId = toPositiveNumber(readValue(item, "assignedTeamId", "AssignedTeamId", "primaryTeamId", "PrimaryTeamId"));
        const workflowStatus = normalizeCode(row.workflowStatus);
        if (!roleStatuses.includes(workflowStatus)) return false;

        if (roleCode === "TECH") {
          return leadId === currentStaffId || assistantId === currentStaffId;
        }

        if (roleCode === "TEAM_LEADER") {
          if (currentTeamId) {
            if (primaryTeamId) return primaryTeamId === currentTeamId;
            if (currentStaffId && teamLeaderApprovedById) return teamLeaderApprovedById === currentStaffId;
            return true;
          }

          if (currentStaffId && teamLeaderApprovedById) {
            return teamLeaderApprovedById === currentStaffId;
          }

          return true;
        }

        return true;
      });

      const mapped = filteredByRole.sort((a, b) => toTimestamp(b.latestRecordAt) - toTimestamp(a.latestRecordAt));

      setRows(mapped);
    } catch (err) {
      const message = toText(readValue(err, "response")?.data?.message) || toText(err?.message) || "Không thể tải lịch sử đã xử lý.";
      setError(message);
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [roleCode, roleConfig, user]);

  useEffect(() => {
    fetchHistory();
  }, [fetchHistory]);

  const dateFilterButtonLabel = useMemo(
    () =>
      getDateRangeButtonLabel(dateFrom, dateTo, {
        todayDateText: toDateText(new Date()),
        emptyLabel: "Chọn khoảng ngày",
        todayLabel: "Hôm nay",
      }),
    [dateFrom, dateTo]
  );

  const stats = useMemo(() => {
    const total = rows.length;
    const now = Date.now();
    const dayStart = new Date();
    dayStart.setHours(0, 0, 0, 0);
    const today = rows.filter((row) => new Date(row.completedAt || 0).getTime() >= dayStart.getTime()).length;
    const sevenDays = rows.filter((row) => {
      const ts = new Date(row.completedAt || 0).getTime();
      return Number.isFinite(ts) && ts >= now - 7 * 24 * 60 * 60 * 1000;
    }).length;

    const completedCount = rows.filter((row) => toHistoryFilterStatus(row.workflowStatus) === "COMPLETED").length;
    const canceledCount = rows.filter((row) => toHistoryFilterStatus(row.workflowStatus) === "CANCELED").length;
    const rediagnoseCount = rows.filter((row) => toHistoryFilterStatus(row.workflowStatus) === "REDIAGNOSE").length;

    return { total, today, sevenDays, completedCount, canceledCount, rediagnoseCount };
  }, [rows]);

  const filteredRows = useMemo(() => {
    const normalizedKeyword = toText(searchQuery).toLowerCase();
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);

    const list = rows.filter((row) => {
      const historyFilterStatus = toHistoryFilterStatus(row.workflowStatus);
      const byStatus = statusFilter === "ALL" || historyFilterStatus === statusFilter;
      if (!byStatus) return false;

      const completedAtValue = new Date(row.completedAt || 0).getTime();
      const byDate =
        (!startDate || (Number.isFinite(completedAtValue) && completedAtValue >= startDate.getTime())) &&
        (!endDate || (Number.isFinite(completedAtValue) && completedAtValue <= endDate.getTime()));
      if (!byDate) return false;

      if (!normalizedKeyword) return true;

      const haystack = [
        row.serviceOrderCode,
        row.customerName,
        row.vehicleLicensePlate,
        row.vehicleDescriptor,
        row.teamName,
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(normalizedKeyword);
    });

    return list.sort((a, b) => {
      const aTime = new Date(a.completedAt || 0).getTime();
      const bTime = new Date(b.completedAt || 0).getTime();
      if (sortBy === "COMPLETED_ASC") return aTime - bTime;
      return bTime - aTime;
    });
  }, [dateFrom, dateTo, rows, searchQuery, sortBy, statusFilter]);

  const openHistoryDetail = useCallback((serviceOrderId) => {
    const targetOrderId = toPositiveNumber(serviceOrderId);
    if (!targetOrderId || !roleConfig) return;

    navigate(`${roleConfig.openOrderPath(targetOrderId)}?from=history`, {
      state: { returnToPath: location.pathname },
    });
  }, [location.pathname, navigate, roleConfig]);

  const openHistoryDiagnosis = useCallback((serviceOrderId) => {
    const targetOrderId = toPositiveNumber(serviceOrderId);
    if (!targetOrderId || !roleConfig) return;

    const openPathBuilder = roleConfig.openDiagnosisPath || roleConfig.openOrderPath;
    navigate(`${openPathBuilder(targetOrderId)}?from=history`, {
      state: { returnToPath: location.pathname },
    });
  }, [location.pathname, navigate, roleConfig]);

  if (!roleConfig) {
    return (
      <Alert status="warning" borderRadius="lg">
        <AlertIcon />
        Vai trò hiện tại không có trang lịch sử xử lý.
      </Alert>
    );
  }

  return (
    <Box p={{ base: 3.5, md: 6 }} maxW="1600px" mx="auto">
      <VStack align="stretch" spacing={{ base: 3.5, md: 4.5 }}>
        {/* Header Row: Title + Refresh CTA */}
        <Flex
          justify="space-between"
          align="center"
          gap={3}
        >
          <Heading size="md" color="#0f172a" fontWeight="900" letterSpacing="-0.02em">
            Lịch sử xử lý
          </Heading>
          <Button
            size="sm"
            height="38px"
            px={4}
            variant="outline"
            borderColor="#cbd5e1"
            bg="white"
            color="#334155"
            borderRadius="xl"
            fontWeight="700"
            _hover={{ bg: "#f8fafc", borderColor: "#94a3b8" }}
            _active={{ bg: "#f1f5f9" }}
            onClick={fetchHistory}
            isLoading={loading}
            leftIcon={<RepeatIcon />}
          >
            Làm mới
          </Button>
        </Flex>

        {/* Compact KPI Metric Strip: Sleek single-line metrics banner */}
        <Box
          bg="white"
          border="1px solid #e2e8f0"
          borderRadius="xl"
          p={{ base: 3, sm: 3.5 }}
          boxShadow="0 1px 3px rgba(15,23,42,0.03)"
        >
          <SimpleGrid columns={{ base: 1, sm: 3 }} spacing={{ base: 3, sm: 4 }}>
            <HStack spacing={3} align="center">
              <Box
                w="38px"
                h="38px"
                borderRadius="lg"
                bg="#eff6ff"
                display="flex"
                alignItems="center"
                justifyContent="center"
                color="#2563eb"
                flexShrink={0}
              >
                <CheckCircleIcon boxSize={4} />
              </Box>
              <Box>
                <Text fontSize="11px" fontWeight="700" color="#64748b" textTransform="uppercase" letterSpacing="0.05em">
                  Tổng yêu cầu đã xử lý
                </Text>
                <Text fontSize={{ base: "xl", sm: "22px" }} fontWeight="900" color="#0f172a" lineHeight="1.1">
                  {stats.total}
                </Text>
              </Box>
            </HStack>

            <HStack spacing={3} align="center">
              <Box
                w="38px"
                h="38px"
                borderRadius="lg"
                bg="#f0fdf4"
                display="flex"
                alignItems="center"
                justifyContent="center"
                color="#16a34a"
                flexShrink={0}
              >
                <TimeIcon boxSize={4} />
              </Box>
              <Box>
                <Text fontSize="11px" fontWeight="700" color="#64748b" textTransform="uppercase" letterSpacing="0.05em">
                  Đã xử lý hôm nay
                </Text>
                <Text fontSize={{ base: "xl", sm: "22px" }} fontWeight="900" color="#0f172a" lineHeight="1.1">
                  {stats.today}
                </Text>
              </Box>
            </HStack>

            <HStack spacing={3} align="center">
              <Box
                w="38px"
                h="38px"
                borderRadius="lg"
                bg="#f5f3ff"
                display="flex"
                alignItems="center"
                justifyContent="center"
                color="#7c3aed"
                flexShrink={0}
              >
                <RepeatClockIcon boxSize={4} />
              </Box>
              <Box>
                <Text fontSize="11px" fontWeight="700" color="#64748b" textTransform="uppercase" letterSpacing="0.05em">
                  7 ngày gần nhất
                </Text>
                <Text fontSize={{ base: "xl", sm: "22px" }} fontWeight="900" color="#0f172a" lineHeight="1.1">
                  {stats.sevenDays}
                </Text>
              </Box>
            </HStack>
          </SimpleGrid>
        </Box>

        {/* Filter & Search Toolbar */}
        <Box
          bg="white"
          border="1px solid #e2e8f0"
          borderRadius="xl"
          p={{ base: 3, md: 3.5 }}
          boxShadow="0 1px 3px rgba(15,23,42,0.03)"
          position="relative"
          zIndex={10}
        >
          <Flex
            justify="space-between"
            align={{ base: "stretch", lg: "center" }}
            direction={{ base: "column", lg: "row" }}
            gap={3}
            wrap="wrap"
          >
            {/* Filter Pills with Counter Badges */}
            <HStack spacing={1.5} wrap="wrap">
              {[
                { value: "ALL", label: "Tất cả", count: stats.total },
                { value: "COMPLETED", label: "Hoàn thành", count: stats.completedCount },
                { value: "CANCELED", label: "Đã hủy", count: stats.canceledCount },
                { value: "REDIAGNOSE", label: "Làm lại", count: stats.rediagnoseCount },
              ].map((tab) => {
                const isActive = statusFilter === tab.value;
                return (
                  <Button
                    key={tab.value}
                    size="sm"
                    height="34px"
                    px={3}
                    borderRadius="lg"
                    fontSize="xs"
                    fontWeight={isActive ? "800" : "600"}
                    bg={isActive ? "#0f172a" : "#f8fafc"}
                    color={isActive ? "white" : "#475569"}
                    border="1px solid"
                    borderColor={isActive ? "#0f172a" : "#e2e8f0"}
                    _hover={{ bg: isActive ? "#1e293b" : "#f1f5f9" }}
                    onClick={() => setStatusFilter(tab.value)}
                  >
                    {tab.label}
                    <Badge
                      ml={1.5}
                      bg={isActive ? "rgba(255,255,255,0.22)" : "#e2e8f0"}
                      color={isActive ? "white" : "#475569"}
                      borderRadius="full"
                      px={1.5}
                      fontSize="10px"
                    >
                      {tab.count}
                    </Badge>
                  </Button>
                );
              })}
            </HStack>

            {/* Controls: Sort Dropdown, Date Range, Reset, Counter */}
            <Flex align="center" gap={2.5} wrap="wrap">
              <FilterDropdown
                id="history-sort-filter"
                label="Sắp xếp"
                value={sortBy}
                options={[
                  { value: "COMPLETED_DESC", label: "Mới nhất" },
                  { value: "COMPLETED_ASC", label: "Cũ nhất" },
                ]}
                onChange={(value) => setSortBy(String(value || "COMPLETED_DESC"))}
                minWidth={140}
              />

              <Button
                size="sm"
                height="36px"
                variant="outline"
                borderColor={dateFrom || dateTo ? "#2563eb" : "#d8e1ef"}
                bg={dateFrom || dateTo ? "#eff6ff" : "white"}
                color={dateFrom || dateTo ? "#1d4ed8" : "#334155"}
                fontWeight="700"
                borderRadius="xl"
                leftIcon={<CalendarIcon />}
                onClick={() => setIsDateRangePickerOpen(true)}
              >
                {dateFilterButtonLabel || "Chọn khoảng ngày"}
              </Button>

              {(statusFilter !== "ALL" || sortBy !== "COMPLETED_DESC" || dateFrom || dateTo) && (
                <Button
                  size="sm"
                  height="36px"
                  variant="ghost"
                  color="#dc2626"
                  fontWeight="700"
                  borderRadius="xl"
                  _hover={{ bg: "#fef2f2" }}
                  onClick={() => {
                    setStatusFilter("ALL");
                    setSortBy("COMPLETED_DESC");
                    setDateFrom("");
                    setDateTo("");
                  }}
                >
                  Xóa lọc
                </Button>
              )}

              <Text fontSize="xs" fontWeight="700" color="#64748b" ml={{ base: 0, sm: 2 }} whiteSpace="nowrap">
                Hiển thị <strong>{filteredRows.length}</strong> yêu cầu
              </Text>
            </Flex>
          </Flex>
        </Box>

        <DateRangePickerModal
          isOpen={isDateRangePickerOpen}
          onClose={() => setIsDateRangePickerOpen(false)}
          initialDateFrom={dateFrom}
          initialDateTo={dateTo}
          onApply={({ dateFrom: nextFrom, dateTo: nextTo }) => {
            setDateFrom(nextFrom || "");
            setDateTo(nextTo || "");
          }}
          title="Lọc lịch sử theo khoảng ngày"
          closeLabel="Đóng"
          applyLabel="Áp dụng"
          requireFullRange
        />

        {error ? (
          <Alert status="error" borderRadius="xl">
            <AlertIcon />
            {error}
          </Alert>
        ) : null}

        {/* Job History List */}
        {loading ? (
          <Box p={8} bg="white" borderRadius="xl" border="1px solid #e2e8f0" textAlign="center">
            <Spinner size="lg" color="blue.500" />
            <Text mt={3} color="#64748b" fontSize="sm" fontWeight="600">
              Đang tải lịch sử xử lý...
            </Text>
          </Box>
        ) : filteredRows.length === 0 ? (
          <Box p={8} bg="white" borderRadius="xl" border="1px solid #e2e8f0" textAlign="center">
            <Text color="#64748b" fontSize="sm" fontWeight="600">
              Chưa có dữ liệu lịch sử phù hợp với bộ lọc hiện tại.
            </Text>
          </Box>
        ) : (
          <VStack align="stretch" spacing={2.5}>
            {filteredRows.map((row) => {
              const statusMeta = (() => {
                const normalized = normalizeCode(row.workflowStatus);
                if (normalized === "CANCELED") {
                  return {
                    label: "Đã hủy",
                    bg: "#fef2f2",
                    color: "#991b1b",
                    border: "1px solid #fecaca",
                  };
                }
                if (["TL_REJECTED_REDIAGNOSE", "FOREMAN_REJECTED_REDIAGNOSE"].includes(normalized)) {
                  return {
                    label: "Làm lại",
                    bg: "#fffbeb",
                    color: "#92400e",
                    border: "1px solid #fde68a",
                  };
                }
                return {
                  label: "Hoàn thành",
                  bg: "#f0fdf4",
                  color: "#166534",
                  border: "1px solid #bbf7d0",
                };
              })();

              const hasPlate = row.vehicleLicensePlate && row.vehicleLicensePlate !== "--" && row.vehicleLicensePlate.trim() !== "";
              const processedLabel = getProcessedTimelineLabel(roleCode);
              const milestoneTitle = row.workflowStatus === "CANCELED" ? "Đã hủy vào lúc" : processedLabel;

              return (
                <Box
                  key={`${row.diagnosisId || 0}-${row.serviceOrderId}-${row.completedAt || ""}`}
                  bg="white"
                  border="1px solid #e2e8f0"
                  borderRadius="xl"
                  p={{ base: 3.5, sm: 4 }}
                  boxShadow="0 1px 3px rgba(15,23,42,0.03)"
                  transition="all 0.16s ease"
                  _hover={{
                    borderColor: "#cbd5e1",
                    boxShadow: "0 6px 16px rgba(15,23,42,0.06)",
                    transform: "translateY(-1px)",
                  }}
                  cursor="pointer"
                  onClick={() => openHistoryDetail(row.serviceOrderId)}
                >
                  <VStack align="stretch" spacing={2.5}>
                    {/* Top Row: Plate, Vehicle Name, Order Code, Status */}
                    <Flex justify="space-between" align={{ base: "start", sm: "center" }} gap={3} wrap="wrap">
                      <HStack spacing={2.5} align="center" wrap="wrap">
                        {/* Official Workshop License Plate Badge */}
                        <Box
                          display="inline-flex"
                          alignItems="center"
                          justifyContent="center"
                          bg="#ffffff"
                          border="2px solid #0f172a"
                          borderRadius="md"
                          px={2.5}
                          py={0.5}
                          boxShadow="0 1px 2px rgba(0,0,0,0.06)"
                        >
                          <Text fontSize="14px" fontWeight="900" letterSpacing="0.05em" color="#0f172a" lineHeight="1.2">
                            {hasPlate ? row.vehicleLicensePlate : "CHƯA BIỂN"}
                          </Text>
                        </Box>

                        {/* Vehicle Descriptor */}
                        <Text fontSize={{ base: "md", sm: "16px" }} fontWeight="800" color="#0f172a" lineHeight="1.2">
                          {row.vehicleDescriptor && row.vehicleDescriptor !== "Chưa rõ dòng xe"
                            ? row.vehicleDescriptor
                            : "Xe tiếp nhận"}
                        </Text>
                      </HStack>

                      {/* Status Badge */}
                      <Badge
                        bg={statusMeta.bg}
                        color={statusMeta.color}
                        border={statusMeta.border}
                        borderRadius="md"
                        px={2.5}
                        py={1}
                        fontSize="xs"
                        fontWeight="800"
                        letterSpacing="0.02em"
                      >
                        {statusMeta.label}
                      </Badge>
                    </Flex>

                    {/* Middle Row: Team info (only rendered when meaningful) */}
                    {Boolean(row.teamName && row.teamName !== "--") && (
                      <HStack spacing={3} wrap="wrap" fontSize="xs" color="#475569">
                        <Text>
                          <strong style={{ color: "#334155" }}>Tổ thực hiện:</strong> {row.teamName}
                        </Text>
                      </HStack>
                    )}

                    {/* Bottom Row: Relevant Timestamps and Action Buttons */}
                    <Flex
                      justify="space-between"
                      align={{ base: "start", sm: "center" }}
                      direction={{ base: "column", sm: "row" }}
                      gap={2.5}
                      pt={2}
                      borderTop="1px solid #f1f5f9"
                    >
                      <HStack spacing={2} fontSize="xs" color="#64748b" wrap="wrap">
                        <TimeIcon boxSize={3.5} color="#64748b" />
                        <Text>
                          <strong>{milestoneTitle}:</strong> {formatDateTime(row.completedAt)}
                        </Text>
                        {row.assignedAt && (
                          <>
                            <Text color="#cbd5e1">·</Text>
                            <Text>
                              Tiếp nhận: {formatDateTime(row.assignedAt)}
                            </Text>
                          </>
                        )}
                      </HStack>

                      <HStack spacing={2} alignSelf={{ base: "flex-end", sm: "center" }}>
                        <Button
                          size="sm"
                          height="34px"
                          px={3}
                          variant="ghost"
                          color="#475569"
                          fontSize="xs"
                          fontWeight="700"
                          borderRadius="lg"
                          _hover={{ bg: "#f1f5f9", color: "#0f172a" }}
                          onClick={(event) => {
                            event.stopPropagation();
                            openHistoryDetail(row.serviceOrderId);
                          }}
                        >
                          Lệnh điều phối
                        </Button>
                        <Button
                          size="sm"
                          height="34px"
                          px={3.5}
                          bg="#2563eb"
                          color="white"
                          fontSize="xs"
                          fontWeight="800"
                          borderRadius="lg"
                          _hover={{ bg: "#1d4ed8" }}
                          rightIcon={<ArrowForwardIcon />}
                          onClick={(event) => {
                            event.stopPropagation();
                            openHistoryDiagnosis(row.serviceOrderId);
                          }}
                        >
                          Xem chẩn đoán
                        </Button>
                      </HStack>
                    </Flex>
                  </VStack>
                </Box>
              );
            })}
          </VStack>
        )}
      </VStack>
    </Box>
  );
}
