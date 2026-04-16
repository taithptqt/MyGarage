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
  Stack,
  Text,
  VStack,
} from "@chakra-ui/react";
import { TimeIcon } from "@chakra-ui/icons";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStaff } from "@features/admin/api/staffApi";
import { useAuth } from "@shared/core/context/AuthContext";
import FilterDropdown from "@shared/components/FilterDropdown";
import DateRangePickerModal, { getDateRangeButtonLabel } from "@features/admin/components/DateRangePickerModal";
import { useSearch } from "@shared/core/context/SearchContext";

const ROLE_CONFIG = {
  TECH: {
    title: "Lịch sử xử lý của thợ",
    subtitle: "Theo dõi các yêu cầu chẩn đoán mà bạn đã xử lý xong.",
    dashboardPath: "/workshop/technician",
    openOrderPath: (orderId) => `/workshop/technician/orders/${orderId}/diagnosis`,
    openDiagnosisPath: (orderId) => `/workshop/technician/orders/${orderId}/diagnosis`,
    processedLabel: "Đã hoàn tất chẩn đoán",
  },
  TEAM_LEADER: {
    title: "Lịch sử xử lý của tổ trưởng",
    subtitle: "Theo dõi các yêu cầu đã được tổ trưởng duyệt kỹ thuật.",
    dashboardPath: "/workshop/team-leader",
    openOrderPath: (orderId) => `/workshop/team-leader/dispatch/${orderId}`,
    openDiagnosisPath: (orderId) => `/workshop/team-leader/orders/${orderId}/quote`,
    processedLabel: "Đã duyệt kỹ thuật",
  },
  FOREMAN: {
    title: "Lịch sử xử lý của quản đốc",
    subtitle: "Theo dõi các yêu cầu đã được quản đốc duyệt cuối.",
    dashboardPath: "/workshop/foreman",
    openOrderPath: (orderId) => `/workshop/foreman/dispatch/${orderId}`,
    openDiagnosisPath: (orderId) => `/workshop/foreman/orders/${orderId}/quote`,
    processedLabel: "Đã duyệt cuối",
  },
};

const DIAGNOSIS_WORKFLOW_STATUS_LABELS = {
  NEW: "Mới tạo",
  IN_PROGRESS: "Đang chuẩn đoán",
  SUBMITTED: "Đã gửi duyệt",
  TL_APPROVED: "Tổ trưởng đã duyệt",
  TL_REJECTED_REDIAGNOSE: "Tổ trưởng từ chối khám lại",
  ESCALATED_TO_FOREMAN: "Đã chuyển cấp cho quản đốc",
  FOREMAN_REJECTED_REDIAGNOSE: "Quản đốc từ chối khám lại",
  FOREMAN_APPROVED_LOCKED: "Quản đốc duyệt cuối",
  CANCELED: "Hủy do khách",
};

const HISTORY_STATUS_BY_ROLE = {
  FOREMAN: ["FOREMAN_APPROVED_LOCKED", "CANCELED"],
  TEAM_LEADER: ["TL_APPROVED", "CANCELED"],
  TECH: ["TL_APPROVED", "FOREMAN_APPROVED_LOCKED", "CANCELED"],
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

function isCompletedRecord(item) {
  const workflowStatus = resolveWorkflowStatus(item);
  return [
    "TL_APPROVED",
    "TL_REJECTED_REDIAGNOSE",
    "ESCALATED_TO_FOREMAN",
    "FOREMAN_REJECTED_REDIAGNOSE",
    "FOREMAN_APPROVED_LOCKED",
    "CANCELED",
  ].includes(workflowStatus);
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

function getWorkflowStatusLabel(statusCode) {
  if (!statusCode) return "--";
  return DIAGNOSIS_WORKFLOW_STATUS_LABELS[statusCode] || statusCode;
}

function getHistoryStatusLabel(statusCode, roleCode) {
  const normalized = normalizeCode(statusCode);
  if (normalized === "CANCELED") return "Đã hủy";

  if (roleCode === "FOREMAN") {
    if (normalized === "FOREMAN_APPROVED_LOCKED") return "Đã duyệt";
  }

  if (roleCode === "TEAM_LEADER") {
    if (normalized === "TL_APPROVED") return "Đã duyệt";
  }

  if (normalized.endsWith("_APPROVED") || normalized.includes("APPROVED")) return "Đã duyệt";
  return getWorkflowStatusLabel(statusCode);
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

function toRow(item) {
  const diagnosisId = toPositiveNumber(readValue(item, "diagnosisId", "DiagnosisId")) || 0;
  const serviceOrderId = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId")) || 0;
  const code = toText(readValue(item, "serviceOrderCode", "ServiceOrderCode")) || `SO-${serviceOrderId}`;
  const customerName = toText(readValue(item, "customerName", "CustomerName")) || "Khách hàng chưa rõ";
  const plate = toText(readValue(item, "vehicleLicensePlate", "VehicleLicensePlate")) || "--";
  const brand = toText(readValue(item, "vehicleBrandName", "VehicleBrandName"));
  const model = toText(readValue(item, "vehicleModelName", "VehicleModelName"));
  const descriptor = toText(readValue(item, "vehicleDescriptor", "VehicleDescriptor")) || [brand, model].filter(Boolean).join(" ");
  const teamName = toText(readValue(item, "primaryTeamName", "PrimaryTeamName", "teamName", "TeamName")) || "--";
  const completedAt = resolveCompletedAt(item);
  const latestRecordAt = resolveLatestRecordAt(item);

  return {
    diagnosisId,
    serviceOrderId,
    serviceOrderCode: code,
    customerName,
    vehicleLicensePlate: plate,
    vehicleDescriptor: descriptor || "Chưa rõ dòng xe",
    workflowStatus: resolveWorkflowStatus(item),
    teamName,
    assignedAt: resolveAssignedAt(item),
    startedAt: readValue(
      item,
      "startedAt",
      "StartedAt",
      "technicianAcceptedAt",
      "TechnicianAcceptedAt",
      "leadTechnicianAcceptedAt",
      "LeadTechnicianAcceptedAt"
    ),
    technicalApprovedAt: readValue(item, "technicalApprovedAt", "TechnicalApprovedAt"),
    finalApprovedAt: readValue(item, "finalApprovedAt", "FinalApprovedAt"),
    completedAt,
    latestRecordAt,
    raw: item,
  };
}

function getProcessedAtLabel(roleCode) {
  if (roleCode === "FOREMAN") return "Duyệt cuối";
  if (roleCode === "TEAM_LEADER") return "Duyệt kỹ thuật";
  return "Hoàn tất xử lý";
}

function getProcessedTimelineLabel(roleCode) {
  if (roleCode === "FOREMAN") return "Ghi nhận lịch sử";
  return getProcessedAtLabel(roleCode);
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
  const [dateFrom, setDateFrom] = useState(() => toDateText(new Date()));
  const [dateTo, setDateTo] = useState(() => toDateText(new Date()));
  const [isDateRangePickerOpen, setIsDateRangePickerOpen] = useState(false);

  const fetchHistory = useCallback(async () => {
    if (!roleConfig) return;
    setLoading(true);
    setError("");

    try {
      const [boardRes, diagnosesRes, staffsRes] = await Promise.all([
        serviceOrderApi.getDiagnosisDispatchBoard({ includeCompleted: true }),
        serviceOrderApi.getDiagnoses(),
        getAllStaff(),
      ]);

      const board = Array.isArray(boardRes) ? boardRes : [];
      const diagnoses = Array.isArray(diagnosesRes) ? diagnosesRes : [];
      const staffs = Array.isArray(staffsRes) ? staffsRes : [];
      const currentStaff = resolveCurrentStaff(staffs, user);
      const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id"));
      const currentTeamId = toPositiveNumber(readValue(currentStaff, "teamId", "TeamId"));

      if (!currentStaffId && (roleCode === "TECH" || roleCode === "TEAM_LEADER")) {
        setRows([]);
        setError("Không xác định được hồ sơ nhân sự của tài khoản hiện tại.");
        return;
      }

      const boardByServiceOrderId = new Map();
      for (const item of board) {
        const row = toRow(item);
        if (row.serviceOrderId > 0 && !boardByServiceOrderId.has(row.serviceOrderId)) {
          boardByServiceOrderId.set(row.serviceOrderId, row);
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
          const completedAt = resolveCompletedAtFromDiagnosis(item, workflowStatus);
          const assignedAt = readValue(item, "assignedAt", "AssignedAt") || boardMeta?.assignedAt || null;
          const startedAt =
            readValue(item, "technicianAcceptedAt", "TechnicianAcceptedAt", "startedAt", "StartedAt") || boardMeta?.startedAt || null;
          const technicalApprovedAt =
            readValue(item, "approvedByForemanAt", "ApprovedByForemanAt", "technicalApprovedAt", "TechnicalApprovedAt") ||
            boardMeta?.technicalApprovedAt ||
            null;
          const finalApprovedAt =
            readValue(item, "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt", "finalApprovedAt", "FinalApprovedAt") ||
            boardMeta?.finalApprovedAt ||
            null;

          return {
            diagnosisId,
            serviceOrderId,
            serviceOrderCode: boardMeta?.serviceOrderCode || `SO-${serviceOrderId}`,
            customerName: boardMeta?.customerName || "Khách hàng chưa rõ",
            vehicleLicensePlate: boardMeta?.vehicleLicensePlate || "--",
            vehicleDescriptor: boardMeta?.vehicleDescriptor || "Chưa rõ dòng xe",
            workflowStatus,
            teamName:
              toText(readValue(item, "assignedTeamName", "AssignedTeamName")) ||
              boardMeta?.teamName ||
              "--",
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

  const statusOptions = useMemo(() => {
    return HISTORY_STATUS_BY_ROLE[roleCode] || [];
  }, [roleCode]);

  const dateFilterButtonLabel = useMemo(
    () =>
      getDateRangeButtonLabel(dateFrom, dateTo, {
        todayDateText: toDateText(new Date()),
        emptyLabel: "Chọn khoảng ngày",
        todayLabel: "Hôm nay",
      }),
    [dateFrom, dateTo]
  );

  const filteredRows = useMemo(() => {
    const normalizedKeyword = toText(searchQuery).toLowerCase();
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);

    const list = rows.filter((row) => {
      const byStatus = statusFilter === "ALL" || normalizeCode(row.workflowStatus) === statusFilter;
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

  const stats = useMemo(() => {
    const total = filteredRows.length;
    const now = Date.now();
    const dayStart = new Date();
    dayStart.setHours(0, 0, 0, 0);
    const today = filteredRows.filter((row) => new Date(row.completedAt || 0).getTime() >= dayStart.getTime()).length;
    const sevenDays = filteredRows.filter((row) => {
      const ts = new Date(row.completedAt || 0).getTime();
      return Number.isFinite(ts) && ts >= now - 7 * 24 * 60 * 60 * 1000;
    }).length;
    return { total, today, sevenDays };
  }, [filteredRows]);

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
    <Box p={{ base: 4, md: 6 }}>
      <Stack spacing={5}>
        <Flex justify="space-between" align={{ base: "start", md: "center" }} direction={{ base: "column", md: "row" }} gap={3}>
          <Box>
            <Heading size="md" color="#1b2559">{roleConfig.title}</Heading>
            <Text mt={1} color="#707eae">{roleConfig.subtitle}</Text>
          </Box>
        </Flex>

        <HStack justify="flex-end" align="center" spacing={3} flexWrap="wrap">
          <Button variant="outline" colorScheme="blue" onClick={fetchHistory} isLoading={loading}>
            ⟳ Làm mới
          </Button>
        </HStack>

        <SimpleGrid columns={{ base: 1, md: 3 }} spacing={4}>
          <Box p={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
            <Text fontSize="sm" color="#707eae">Tổng yêu cầu đã xử lý</Text>
            <Text mt={1} fontWeight="800" fontSize="2xl" color="#1b2559">{stats.total}</Text>
          </Box>
          <Box p={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
            <Text fontSize="sm" color="#707eae">Đã xử lý hôm nay</Text>
            <Text mt={1} fontWeight="800" fontSize="2xl" color="#1b2559">{stats.today}</Text>
          </Box>
          <Box p={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
            <Text fontSize="sm" color="#707eae">7 ngày gần nhất</Text>
            <Text mt={1} fontWeight="800" fontSize="2xl" color="#1b2559">{stats.sevenDays}</Text>
          </Box>
        </SimpleGrid>

        <Box p={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
          <div className="table-toolbar" style={{ marginBottom: 0 }}>
            <div className="toolbar-controls">
              <FilterDropdown
                id="history-status-filter"
                label="Trạng thái"
                value={statusFilter}
                options={[
                  { value: "ALL", label: "Tất cả trạng thái" },
                  ...statusOptions.map((status) => ({ value: status, label: getHistoryStatusLabel(status, roleCode) })),
                ]}
                onChange={(value) => setStatusFilter(String(value || "ALL"))}
                minWidth={210}
              />
              <FilterDropdown
                id="history-sort-filter"
                label="Sắp xếp"
                value={sortBy}
                options={[
                  { value: "COMPLETED_DESC", label: "Mới nhất" },
                  { value: "COMPLETED_ASC", label: "Cũ nhất" },
                ]}
                onChange={(value) => setSortBy(String(value || "COMPLETED_DESC"))}
                minWidth={180}
              />
              <button
                type="button"
                className="toolbar-action-btn toolbar-action-outline"
                onClick={() => setIsDateRangePickerOpen(true)}
              >
                {dateFilterButtonLabel || "Chọn khoảng ngày"}
              </button>
              <button
                type="button"
                className="toolbar-action-btn toolbar-action-ghost"
                onClick={() => {
                  setStatusFilter("ALL");
                  setSortBy("COMPLETED_DESC");
                  setDateFrom("");
                  setDateTo("");
                }}
              >
                Xóa lọc
              </button>
            </div>
            <span className="result-count">{filteredRows.length} yêu cầu</span>
          </div>
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
          <Alert status="error" borderRadius="lg">
            <AlertIcon />
            {error}
          </Alert>
        ) : null}

        <Box p={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
          {loading ? (
            <Flex py={14} justify="center">
              <Spinner size="lg" color="blue.500" />
            </Flex>
          ) : filteredRows.length === 0 ? (
            <Text color="#707eae">Chưa có dữ liệu lịch sử phù hợp với bộ lọc hiện tại.</Text>
          ) : (
            <VStack align="stretch" spacing={3}>
              {filteredRows.map((row) => (
                <Box
                  key={`${row.diagnosisId || 0}-${row.serviceOrderId}-${row.completedAt || ""}`}
                  role="button"
                  tabIndex={0}
                  p={4}
                  borderRadius="lg"
                  border="1px solid #e6ecf8"
                  bg="#fbfdff"
                  cursor="pointer"
                  onClick={() => openHistoryDetail(row.serviceOrderId)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      openHistoryDetail(row.serviceOrderId);
                    }
                  }}
                  _hover={{ borderColor: "#c9d9f3", boxShadow: "0 6px 18px rgba(27, 37, 89, 0.08)" }}
                  _focusVisible={{ outline: "2px solid #7aa2ff", outlineOffset: "2px" }}
                  transition="all 0.18s ease"
                >
                  <Stack spacing={3}>
                    <Flex justify="space-between" align={{ base: "start", md: "center" }} direction={{ base: "column", md: "row" }} gap={3}>
                      <Box>
                        <HStack spacing={2} flexWrap="wrap">
                          <Text fontWeight="800" color="#1b2559">{row.serviceOrderCode}</Text>
                          <Badge colorScheme="blue" borderRadius="full" px={2.5} py={0.5}>
                            {getHistoryStatusLabel(row.workflowStatus, roleCode)}
                          </Badge>
                        </HStack>
                        <Text mt={1} color="#4a5568">{row.customerName}</Text>
                        <HStack mt={1} spacing={2} flexWrap="wrap">
                          <Badge colorScheme="blue" borderRadius="full">Biển số: {row.vehicleLicensePlate}</Badge>
                          <Badge colorScheme="gray" borderRadius="full">Dòng xe: {row.vehicleDescriptor || "--"}</Badge>
                          <Badge colorScheme="purple" borderRadius="full">Tổ: {row.teamName || "--"}</Badge>
                        </HStack>
                      </Box>
                    </Flex>
                    <Divider />
                    <SimpleGrid columns={{ base: 1, md: 4 }} spacing={2}>
                      <Text fontSize="sm" color="#4a5568">
                        <strong>Nhận:</strong> {formatDateTime(row.assignedAt)}
                      </Text>
                      <Text fontSize="sm" color="#4a5568">
                        <strong>Bắt đầu:</strong> {formatDateTime(row.startedAt)}
                      </Text>
                      <Text fontSize="sm" color="#4a5568">
                        <strong>Duyệt kỹ thuật:</strong> {formatDateTime(row.technicalApprovedAt)}
                      </Text>
                      <Text fontSize="sm" color="#4a5568">
                        <strong>Duyệt cuối:</strong> {formatDateTime(row.finalApprovedAt)}
                      </Text>
                    </SimpleGrid>
                    <HStack spacing={2}>
                      <TimeIcon color="#5a6d9a" />
                      <Text fontSize="sm" color="#2b3674">
                        <strong>{getProcessedTimelineLabel(roleCode)}:</strong> {formatDateTime(row.completedAt)}
                      </Text>
                    </HStack>
                    <HStack justify="flex-end" spacing={2} pt={1} borderTop="1px solid" borderColor="gray.100">
                      <Button
                        size="sm"
                        colorScheme="blue"
                        variant="solid"
                        borderRadius="full"
                        fontWeight="700"
                        onClick={(event) => {
                          event.stopPropagation();
                          openHistoryDiagnosis(row.serviceOrderId);
                        }}
                        onKeyDown={(event) => event.stopPropagation()}
                      >
                        Xem chuẩn đoán
                      </Button>
                    </HStack>
                  </Stack>
                </Box>
              ))}
            </VStack>
          )}
        </Box>
      </Stack>
    </Box>
  );
}
