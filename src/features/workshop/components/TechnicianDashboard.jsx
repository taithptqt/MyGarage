import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { getAllStaff } from "@features/admin/api/staffApi";
import { getAllVehicles } from "@features/admin/api/customerApi";
import { getAllVehicleBrands, getAllVehicleModels } from "@features/admin/api/vehicleCatalogApi";
import { Modal } from "@features/admin/components/AdminModals";
import { useSearch } from "@shared/core/context/SearchContext";
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
  Icon,
  SimpleGrid,
  Spinner,
  Stack,
  Text,
  Textarea,
  useToast,
  VStack,
} from "@chakra-ui/react";
import {
  MdAutorenew,
  MdBuild,
  MdCancel,
  MdCheckCircle,
  MdDirectionsCar,
  MdInfo,
  MdPerson,
  MdPlayArrow,
  MdPriorityHigh,
  MdSupportAgent,
  MdTimer,
} from "react-icons/md";
import { request } from "@app/apiClient";

const TAB_DIAGNOSIS = "DIAGNOSIS";
const TAB_REPAIR = "REPAIR";
const INTERNAL_ASSIGNMENT_STATUS_REJECTED = "REJECTED";

const KPI_FILTER_DIAGNOSIS_NEW = "DIAGNOSIS_NEW";
const KPI_FILTER_DIAGNOSIS_RED = "DIAGNOSIS_RED";
const KPI_FILTER_DIAGNOSIS_SUPPORT = "DIAGNOSIS_SUPPORT";
const KPI_FILTER_REPAIR_ACTIVE = "REPAIR_ACTIVE";

const QUEUE_CARD_META = {
  WHITE: { accent: "#94a3b8", bg: "#ffffff", badge: "gray" },
  YELLOW: { accent: "#d69e2e", bg: "#fffdf3", badge: "yellow" },
  RED: { accent: "#ef4444", bg: "#fff7f7", badge: "red" },
  GREEN: { accent: "#22c55e", bg: "#f3fff8", badge: "green" },
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

function formatMinutes(value) {
  const minutes = Number(value || 0);
  if (!Number.isFinite(minutes) || minutes <= 0) return "0 phút";
  if (minutes < 60) return `${Math.floor(minutes)} phút`;
  const hours = Math.floor(minutes / 60);
  const remain = Math.floor(minutes % 60);
  return remain > 0 ? `${hours} giờ ${remain} phút` : `${hours} giờ`;
}

function resolveApiErrorMessage(error, fallback = "Đã có lỗi xảy ra") {
  const message = toText(readValue(error, "response")?.data?.message);
  if (message) return message;
  const raw = toText(error?.message);
  if (raw) return raw;
  return fallback;
}

function mapAssignmentStatusToPersisted(status) {
  const normalized = String(status || "").trim().toUpperCase();
  if (normalized === INTERNAL_ASSIGNMENT_STATUS_REJECTED) {
    return "PAUSED";
  }
  return normalized;
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

function getQueueCardMeta(queueColor) {
  const normalized = normalizeCode(queueColor);
  return QUEUE_CARD_META[normalized] || QUEUE_CARD_META.WHITE;
}

function resolveVehicleDetails(item, order, vehicleMap, vehicleModelMap = new Map(), vehicleBrandMap = new Map()) {
  const vehicleId =
    toPositiveNumber(readValue(item, "vehicleId", "VehicleId")) ||
    toPositiveNumber(readValue(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? (vehicleMap.get(vehicleId) || null) : null;
  const vehicleModelId =
    toPositiveNumber(readValue(item, "vehicleModelId", "VehicleModelId")) ||
    toPositiveNumber(readValue(order, "vehicleModelId", "VehicleModelId")) ||
    toPositiveNumber(readValue(vehicle, "modelId", "ModelId"));
  const vehicleModel = vehicleModelId ? (vehicleModelMap.get(vehicleModelId) || null) : null;
  const vehicleBrandId =
    toPositiveNumber(readValue(item, "vehicleBrandId", "VehicleBrandId")) ||
    toPositiveNumber(readValue(order, "vehicleBrandId", "VehicleBrandId")) ||
    toPositiveNumber(readValue(vehicle, "brandId", "BrandId")) ||
    toPositiveNumber(readValue(vehicleModel, "brandId", "BrandId"));
  const vehicleBrand = vehicleBrandId ? (vehicleBrandMap.get(vehicleBrandId) || null) : null;

  const vehicleLicensePlate =
    toText(readValue(item, "vehicleLicensePlate", "VehicleLicensePlate")) ||
    toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) ||
    toText(readValue(vehicle, "licensePlate", "LicensePlate")) ||
    "--";

  const brandName =
    toText(readValue(item, "vehicleBrandName", "VehicleBrandName")) ||
    toText(readValue(order, "vehicleBrandName", "VehicleBrandName")) ||
    toText(readValue(vehicle, "brandName", "BrandName")) ||
    toText(readValue(vehicleBrand, "name", "Name", "brandName", "BrandName", "code", "Code"));

  const modelName =
    toText(readValue(item, "vehicleModelName", "VehicleModelName")) ||
    toText(readValue(order, "vehicleModelName", "VehicleModelName")) ||
    toText(readValue(vehicle, "modelName", "ModelName")) ||
    toText(readValue(vehicleModel, "modelName", "ModelName", "name", "Name"));

  const vehicleDescriptor =
    toText(readValue(item, "vehicleDescriptor", "VehicleDescriptor")) ||
    toText(readValue(order, "vehicleDescriptor", "VehicleDescriptor")) ||
    (brandName && modelName ? `${brandName} ${modelName}` : (modelName || brandName)) ||
    "Chưa rõ dòng xe";

  const vehicleVin =
    toText(readValue(item, "vehicleVin", "VehicleVin", "vin", "Vin")) ||
    toText(readValue(order, "vehicleVin", "VehicleVin")) ||
    toText(readValue(vehicle, "vin", "Vin"));

  const vehicleManufactureYear =
    toText(readValue(item, "vehicleManufactureYear", "VehicleManufactureYear", "manufactureYear", "ManufactureYear")) ||
    toText(readValue(order, "vehicleManufactureYear", "VehicleManufactureYear", "manufactureYear", "ManufactureYear")) ||
    toText(readValue(vehicle, "manufactureYear", "ManufactureYear", "year", "Year")) ||
    "--";

  const vehicleColor =
    toText(readValue(item, "vehicleColor", "VehicleColor", "color", "Color")) ||
    toText(readValue(order, "vehicleColor", "VehicleColor", "color", "Color")) ||
    toText(readValue(vehicle, "color", "Color")) ||
    "--";

  return {
    vehicleLicensePlate,
    vehicleBrandName: brandName,
    vehicleModelName: modelName,
    vehicleDescriptor,
    vehicleManufactureYear,
    vehicleColor,
    vehicleVin,
  };
}

export default function TechnicianDashboard() {
  const { searchQuery } = useSearch();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState(TAB_DIAGNOSIS);
  const [activeKpiFilter, setActiveKpiFilter] = useState(KPI_FILTER_DIAGNOSIS_NEW);
  const [staffId, setStaffId] = useState(null);
  const [currentStaffTeamName, setCurrentStaffTeamName] = useState("");
  const [assignments, setAssignments] = useState([]);
  const [diagnosisTasks, setDiagnosisTasks] = useState([]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [rejectingAssignment, setRejectingAssignment] = useState(null);
  const [rejectReason, setRejectReason] = useState("");

  const [supportTarget, setSupportTarget] = useState(null);
  const [supportNote, setSupportNote] = useState("");

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const [ordersRes, staffRes, assignmentsRes, detailsRes, boardRes, vehiclesRes, vehicleModelsRes, vehicleBrandsRes, teamsRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllStaff(),
        request({ url: "/api/service-staff-assignments", method: "GET" }),
        request({ url: "/api/service-order-details", method: "GET" }),
        serviceOrderApi.getDiagnosisDispatchBoard({ includeCompleted: true }),
        getAllVehicles(),
        getAllVehicleModels(),
        getAllVehicleBrands(),
        serviceOrderApi.getTeams(),
      ]);

      const orders = Array.isArray(ordersRes) ? ordersRes : [];
      const staffs = Array.isArray(staffRes) ? staffRes : [];
      const allAssignments = Array.isArray(assignmentsRes) ? assignmentsRes : [];
      const allDetails = Array.isArray(detailsRes) ? detailsRes : [];
      const board = Array.isArray(boardRes) ? boardRes : [];
      const vehicles = Array.isArray(vehiclesRes) ? vehiclesRes : [];
      const vehicleModels = Array.isArray(vehicleModelsRes) ? vehicleModelsRes : [];
      const vehicleBrands = Array.isArray(vehicleBrandsRes) ? vehicleBrandsRes : [];
      const teams = Array.isArray(teamsRes) ? teamsRes : [];
      const vehicleMap = new Map(
        vehicles.map((vehicle) => [toPositiveNumber(readValue(vehicle, "id", "Id")) || 0, vehicle])
      );
      const vehicleModelMap = new Map(
        vehicleModels.map((model) => [toPositiveNumber(readValue(model, "id", "Id")) || 0, model])
      );
      const vehicleBrandMap = new Map(
        vehicleBrands.map((brand) => [toPositiveNumber(readValue(brand, "id", "Id")) || 0, brand])
      );
      const teamMap = new Map(
        teams.map((team) => [toPositiveNumber(readValue(team, "id", "Id")) || 0, team])
      );

      const currentStaff = resolveCurrentStaff(staffs, user);
      const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id"));
      setStaffId(currentStaffId);
      const currentTeamName =
        toText(readValue(currentStaff, "teamName", "TeamName")) ||
        toText(readValue(
          teamMap.get(toPositiveNumber(readValue(currentStaff, "teamId", "TeamId")) || 0),
          "name",
          "Name"
        ));
      setCurrentStaffTeamName(currentTeamName);

      if (!currentStaffId) {
        setAssignments([]);
        setDiagnosisTasks([]);
        setCurrentStaffTeamName("");
        return;
      }

      const myAssignments = allAssignments
        .filter((assignment) => toPositiveNumber(readValue(assignment, "technicianId", "TechnicianId")) === currentStaffId)
        .filter((assignment) => {
          const status = normalizeCode(readValue(assignment, "status", "Status"));
          return status !== "PAUSED" && status !== "COMPLETED";
        })
        .map((assignment) => {
          const detailId = toPositiveNumber(readValue(assignment, "serviceOrderDetailId", "ServiceOrderDetailId"));
          const detail = allDetails.find((item) => toPositiveNumber(readValue(item, "id", "Id")) === detailId) || null;
          const orderId = toPositiveNumber(readValue(detail, "serviceOrderId", "ServiceOrderId"));
          const order = orders.find((item) => toPositiveNumber(readValue(item, "id", "Id")) === orderId) || null;
          return { ...assignment, detail, order };
        })
        .filter((assignment) => {
          const orderStatus = normalizeCode(readValue(assignment.order, "status", "Status"));
          return assignment.order && orderStatus !== "CANCELED" && orderStatus !== "COMPLETED";
        });

      setAssignments(myAssignments);

      const myDiagnosisTasks = board
        .map((item) => {
          const serviceOrderId = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId"));
          const order = orders.find((row) => toPositiveNumber(readValue(row, "id", "Id")) === serviceOrderId) || null;
          const vehicle = resolveVehicleDetails(item, order, vehicleMap, vehicleModelMap, vehicleBrandMap);

          const leadId = toPositiveNumber(readValue(item, "technicianId", "TechnicianId"));
          const assistantId = toPositiveNumber(readValue(item, "assistantTechnicianId", "AssistantTechnicianId"));
          const isLead = leadId === currentStaffId;
          const isAssistant = assistantId === currentStaffId;
          if (!isLead && !isAssistant) return null;

          return {
            serviceOrderId,
            serviceOrderCode: toText(readValue(item, "serviceOrderCode", "ServiceOrderCode")) || toText(readValue(order, "code", "Code")) || `SO-${serviceOrderId}`,
            customerName: toText(readValue(item, "customerName", "CustomerName")) || toText(readValue(order, "customerName", "CustomerName")) || "Khách hàng chưa rõ",
            vehicleLicensePlate: vehicle.vehicleLicensePlate,
            vehicleBrandName: vehicle.vehicleBrandName,
            vehicleModelName: vehicle.vehicleModelName,
            vehicleDescriptor: vehicle.vehicleDescriptor,
            vehicleManufactureYear: vehicle.vehicleManufactureYear,
            vehicleColor: vehicle.vehicleColor,
            vehicleVin: vehicle.vehicleVin,
            queueColor: normalizeCode(readValue(item, "queueColor", "QueueColor")) || "WHITE",
            queueState: toText(readValue(item, "queueState", "QueueState")) || "--",
            dispatchTag: normalizeCode(readValue(item, "dispatchTag", "DispatchTag")) || "",
            assignedBayId: toPositiveNumber(readValue(item, "assignedBayId", "AssignedBayId")),
            assignedBayCode: toText(readValue(item, "assignedBayCode", "AssignedBayCode")),
            pendingMinutes: Number(readValue(item, "pendingMinutes", "PendingMinutes") || 0),
            assignedMinutes: Number(readValue(item, "assignedMinutes", "AssignedMinutes") || 0),
            assignedAt: readValue(item, "assignedAt", "AssignedAt"),
            startedAt: readValue(item, "technicianAcceptedAt", "TechnicianAcceptedAt", "startedAt", "StartedAt"),
            technicianAcceptedAt: readValue(item, "technicianAcceptedAt", "TechnicianAcceptedAt", "startedAt", "StartedAt"),
            technicianCompletedAt: readValue(item, "technicianCompletedAt", "TechnicianCompletedAt"),
            technicalApprovedAt: readValue(item, "technicalApprovedAt", "TechnicalApprovedAt"),
            finalApprovedAt: readValue(item, "finalApprovedAt", "FinalApprovedAt"),
            isSupportRequested: Boolean(readValue(item, "isSupportRequested", "IsSupportRequested")),
            isCancelPendingApproval: Boolean(readValue(item, "isCancelPendingApproval", "IsCancelPendingApproval")),
            leadName: toText(readValue(item, "technicianName", "TechnicianName")),
            assistantName: toText(readValue(item, "assistantTechnicianName", "AssistantTechnicianName")),
            teamName:
              toText(readValue(item, "primaryTeamName", "PrimaryTeamName", "teamName", "TeamName")) ||
              currentTeamName ||
              "--",
            roleInPair: isLead ? "Thợ chính" : "Thợ phụ",
          };
        })
        .filter(Boolean)
        .sort((a, b) => {
          if (!a.startedAt && b.startedAt) return -1;
          if (a.startedAt && !b.startedAt) return 1;

          if (a.queueColor === "RED" && b.queueColor !== "RED") return -1;
          if (a.queueColor !== "RED" && b.queueColor === "RED") return 1;

          const assignedA = new Date(a.assignedAt || 0).getTime();
          const assignedB = new Date(b.assignedAt || 0).getTime();
          return assignedA - assignedB;
        });

      setDiagnosisTasks(myDiagnosisTasks);
    } catch (err) {
      setError(`Không thể tải dữ liệu kỹ thuật viên: ${resolveApiErrorMessage(err)}`);
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleUpdateAssignmentStatus = async (assignmentId, newStatus, reason = "") => {
    try {
      const assignment = assignments.find((a) => toPositiveNumber(readValue(a, "id", "Id")) === toPositiveNumber(assignmentId));
      if (!assignment) return;

      const normalizedStatus = String(newStatus || "").trim().toUpperCase();
      const persistedStatus = mapAssignmentStatusToPersisted(normalizedStatus);
      const isRejectedInternal = normalizedStatus === INTERNAL_ASSIGNMENT_STATUS_REJECTED;
      const isPausedPersisted = persistedStatus === "PAUSED";

      const payload = {
        ...assignment,
        status: persistedStatus,
        pauseLog: isPausedPersisted ? (reason || assignment.pauseLog) : assignment.pauseLog,
        updatedBy: staffId,
        updatedAt: new Date().toISOString(),
      };

      delete payload.detail;
      delete payload.order;

      await serviceOrderApi.updateAssignment(assignmentId, payload);

      toast({
        title: isRejectedInternal || isPausedPersisted ? "Đã tạm dừng công việc" : "Đã xác nhận công việc",
        status: isRejectedInternal || isPausedPersisted ? "warning" : "success",
        duration: 2500,
        isClosable: true,
      });

      fetchData();
    } catch (err) {
      toast({ title: "Lỗi cập nhật", description: resolveApiErrorMessage(err), status: "error" });
    }
  };

  const handleFinishRepair = async (assignment) => {
    const assignmentId = toPositiveNumber(readValue(assignment, "id", "Id"));
    if (!assignmentId) return;
    await handleUpdateAssignmentStatus(assignmentId, "COMPLETED");
  };

  const handleStartDiagnosis = async (task) => {
    if (!task || !staffId) return;
    if (!task.assignedBayId) {
      toast({
        title: "Chưa có khoang được phân công",
        description: "Tổ trưởng cần phân công khoang trước khi kỹ thuật viên nhận yêu cầu.",
        status: "warning",
        duration: 3000,
        isClosable: true,
      });
      return;
    }

    try {
      await serviceOrderApi.startDiagnosis(task.serviceOrderId, {
        technicianId: staffId,
        bayId: task.assignedBayId || null,
      });

      toast({ title: "Đã bắt đầu chẩn đoán", status: "success", duration: 2500, isClosable: true });
      await fetchData();
    } catch (err) {
      toast({ title: "Không thể bắt đầu chẩn đoán", description: resolveApiErrorMessage(err), status: "error" });
    }
  };

  const handleSubmitSupport = async () => {
    if (!supportTarget || !staffId) return;

    try {
      await serviceOrderApi.requestDiagnosisSupport(supportTarget.serviceOrderId, {
        requestedByStaffId: staffId,
        note: toText(supportNote) || null,
      });

      toast({ title: "Đã gửi yêu cầu hỗ trợ", status: "success", duration: 2500, isClosable: true });
      setSupportTarget(null);
      setSupportNote("");
      await fetchData();
    } catch (err) {
      toast({ title: "Không thể gửi hỗ trợ", description: resolveApiErrorMessage(err), status: "error" });
    }
  };

  const filteredAssignments = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return assignments;

    return assignments.filter((item) => {
      const orderCode = toText(readValue(item.order, "code", "Code")).toLowerCase();
      const itemName = toText(readValue(item.detail, "itemName", "ItemName")).toLowerCase();
      return orderCode.includes(query) || itemName.includes(query);
    });
  }, [assignments, searchQuery]);

  const filteredDiagnosisTasks = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return diagnosisTasks;

    return diagnosisTasks.filter((task) => {
      const haystack = [task.serviceOrderCode, task.vehicleLicensePlate, task.vehicleDescriptor, task.vehicleVin, task.teamName]
        .map((value) => toText(value).toLowerCase())
        .join(" ");
      return haystack.includes(query);
    });
  }, [diagnosisTasks, searchQuery]);

  const newAssignments = filteredAssignments.filter((item) => {
    const status = normalizeCode(readValue(item, "status", "Status"));
    return !status || status === "PENDING";
  });

  const activeAssignments = filteredAssignments.filter((item) => normalizeCode(readValue(item, "status", "Status")) === "IN_PROGRESS");
  const diagnosisWaitingToStart = filteredDiagnosisTasks.filter(
    (task) => !task.startedAt && !task.technicalApprovedAt && !task.finalApprovedAt
  ).length;
  const diagnosisNewCount = filteredDiagnosisTasks.filter(
    (task) => !task.startedAt && !task.technicalApprovedAt && !task.finalApprovedAt && task.queueColor !== "RED"
  ).length;
  const diagnosisUrgentCount = filteredDiagnosisTasks.filter((task) => task.queueColor === "RED").length;
  const diagnosisSupportCount = filteredDiagnosisTasks.filter((task) => task.isSupportRequested).length;
  const visibleDiagnosisTasks = useMemo(() => {
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_NEW) {
      // Keep accepted-but-not-approved tasks visible so technicians do not lose the card after clicking "Nhận yêu cầu".
      return filteredDiagnosisTasks.filter(
        (task) => !task.technicalApprovedAt && !task.finalApprovedAt && task.queueColor !== "RED"
      );
    }
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_RED) {
      return filteredDiagnosisTasks.filter((task) => task.queueColor === "RED");
    }
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_SUPPORT) {
      return filteredDiagnosisTasks.filter((task) => task.isSupportRequested);
    }
    return filteredDiagnosisTasks;
  }, [activeKpiFilter, filteredDiagnosisTasks]);

  const activeViewLabel = useMemo(() => {
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_NEW) return "Ca mới";
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_RED) return "Ca ưu tiên";
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_SUPPORT) return "Đang chờ hỗ trợ";
    if (activeKpiFilter === KPI_FILTER_REPAIR_ACTIVE) return "Sửa chữa đang làm";
    return "Ca mới";
  }, [activeKpiFilter]);

  const diagnosisEmptyMessage = useMemo(() => {
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_NEW) {
      return "Không có ca mới trong bộ lọc hiện tại.";
    }
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_RED) {
      return "Không có ca ưu tiên RED trong bộ lọc hiện tại.";
    }
    if (activeKpiFilter === KPI_FILTER_DIAGNOSIS_SUPPORT) {
      return "Không có ca nào đang chờ hỗ trợ hội chẩn.";
    }
    return "Hiện chưa có ca mới được giao cho bạn.";
  }, [activeKpiFilter]);

  const handleSelectKpiFilter = useCallback((nextFilter) => {
    if (nextFilter === KPI_FILTER_REPAIR_ACTIVE) {
      setActiveTab(TAB_REPAIR);
      setActiveKpiFilter(KPI_FILTER_REPAIR_ACTIVE);
      return;
    }

    setActiveTab(TAB_DIAGNOSIS);
    setActiveKpiFilter(nextFilter);
  }, []);

  return (
    <Box p={{ base: 4, md: 6 }} minH="100vh" bg="linear-gradient(180deg, #f4f7ff 0%, #eef4ff 100%)">
      <VStack align="stretch" spacing={5}>
        <HStack justify="space-between" align={{ base: "start", md: "center" }} spacing={3} flexWrap="wrap">
          <Box>
            <Heading size="lg" color="#1b2559">Bảng điều khiển Kỹ thuật viên</Heading>
            <Text color="gray.600" mt={1}>
              Theo dõi ca chẩn đoán, nhận việc đúng khoang và phối hợp hội chẩn khi cần.
            </Text>
            <HStack mt={2} spacing={2} flexWrap="wrap">
              <Badge colorScheme="teal" variant="subtle" borderRadius="full" px={2.5} py={1}>
                Tổ của bạn: {currentStaffTeamName || "Chưa có nhóm"}
              </Badge>
            </HStack>
          </Box>
        </HStack>

        <HStack justify="flex-end" align="center" spacing={3} flexWrap="wrap">
          <Button
            leftIcon={<MdAutorenew />}
            colorScheme="blue"
            variant="outline"
            onClick={fetchData}
            isLoading={loading}
          >
            Làm mới
          </Button>
        </HStack>

        {error && (
          <Alert status="error" borderRadius="xl">
            <AlertIcon />
            {error}
          </Alert>
        )}

        <Box
          bg="white"
          borderRadius="2xl"
          border="1px solid"
          borderColor="#d9e2f1"
          p={{ base: 3, md: 4 }}
          boxShadow="0 12px 30px rgba(15, 23, 42, 0.08)"
        >
          <HStack justify="space-between" align={{ base: "start", md: "center" }} spacing={2} mb={3} flexWrap="wrap">
            <Box>
              <Text fontSize="sm" fontWeight="700" color="#1e3a8a">Tổng quan công việc</Text>
              <Text fontSize="xs" color="gray.500">Bấm vào bất kỳ ô nào để lọc nhanh theo đúng trạng thái.</Text>
            </Box>
            <Badge borderRadius="full" px={3} py={1} bg="#eef4ff" color="#1d4ed8" fontSize="xs">
              Đang xem: {activeViewLabel}
            </Badge>
          </HStack>

          <SimpleGrid columns={{ base: 1, sm: 2, xl: 4 }} spacing={3}>
            <MetricCard
              title="Ca mới"
              value={diagnosisNewCount}
              description={`${diagnosisWaitingToStart} ca chờ nhận`}
              color="#5b4af7"
              isInteractive
              isActive={activeKpiFilter === KPI_FILTER_DIAGNOSIS_NEW}
              onClick={() => handleSelectKpiFilter(KPI_FILTER_DIAGNOSIS_NEW)}
            />
            <MetricCard
              title="Ca ưu tiên"
              value={diagnosisUrgentCount}
              description="Màu RED cần xử lý sớm"
              color="#dc2626"
              isInteractive
              isActive={activeKpiFilter === KPI_FILTER_DIAGNOSIS_RED}
              onClick={() => handleSelectKpiFilter(KPI_FILTER_DIAGNOSIS_RED)}
            />
            <MetricCard
              title="Đang chờ hỗ trợ"
              value={diagnosisSupportCount}
              description="Có yêu cầu hội chẩn"
              color="#d97706"
              isInteractive
              isActive={activeKpiFilter === KPI_FILTER_DIAGNOSIS_SUPPORT}
              onClick={() => handleSelectKpiFilter(KPI_FILTER_DIAGNOSIS_SUPPORT)}
            />
            <MetricCard
              title="Sửa chữa đang làm"
              value={activeAssignments.length}
              description={`${newAssignments.length} yêu cầu mới`}
              color="#0f766e"
              isInteractive
              isActive={activeKpiFilter === KPI_FILTER_REPAIR_ACTIVE}
              onClick={() => handleSelectKpiFilter(KPI_FILTER_REPAIR_ACTIVE)}
            />
          </SimpleGrid>
        </Box>

        {loading ? (
          <Box bg="white" borderRadius="2xl" border="1px solid" borderColor="gray.100" p={10} boxShadow="0 14px 32px rgba(15,23,42,0.08)">
            <VStack spacing={3}>
              <Spinner color="blue.500" />
              <Text color="gray.500">Đang tải dữ liệu kỹ thuật viên...</Text>
            </VStack>
          </Box>
        ) : (
          <>
            {activeTab === TAB_DIAGNOSIS && (
              <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" overflow="hidden" boxShadow="0 14px 32px rgba(15,23,42,0.08)">
                <HStack justify="space-between" align={{ base: "start", md: "center" }} spacing={3} p={4} borderBottom="1px solid" borderColor="gray.100" bg="gray.50" flexWrap="wrap">
                  <HStack spacing={2}>
                    <Icon as={MdInfo} color="purple.500" />
                    <Heading size="md" color="#1b2559">Ca chẩn đoán đã phân công</Heading>
                  </HStack>
                  <Badge borderRadius="full" px={3} py={1} colorScheme="blue">
                    {visibleDiagnosisTasks.length} ca
                  </Badge>
                </HStack>

                {visibleDiagnosisTasks.length === 0 ? (
                  <Box p={10} textAlign="center">
                    <Text color="gray.400">{diagnosisEmptyMessage}</Text>
                  </Box>
                ) : (
                  <VStack align="stretch" spacing={3} p={3} bg="#f8fafc">
                    {visibleDiagnosisTasks.map((task) => (
                      <DiagnosisTaskCard
                        key={`diag-task-${task.serviceOrderId}`}
                        task={task}
                        onStart={() => handleStartDiagnosis(task)}
                        onOpenDiagnosis={() =>
                          navigate(`/workshop/technician/orders/${task.serviceOrderId}/diagnosis`, {
                            state: {
                              vehicleSnapshot: {
                                vehicleLicensePlate: task.vehicleLicensePlate,
                                vehicleBrandName: task.vehicleBrandName,
                                vehicleModelName: task.vehicleModelName,
                                vehicleDescriptor: task.vehicleDescriptor,
                                vehicleManufactureYear: task.vehicleManufactureYear,
                                vehicleColor: task.vehicleColor,
                                vehicleVin: task.vehicleVin,
                              },
                            },
                          })
                        }
                        onRequestSupport={() => {
                          setSupportTarget(task);
                          setSupportNote("");
                        }}
                      />
                    ))}
                  </VStack>
                )}
              </Box>
            )}

            {activeTab === TAB_REPAIR && (
              <VStack align="stretch" spacing={4}>
                <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" p={4} boxShadow="0 14px 32px rgba(15,23,42,0.08)">
                  <HStack mb={4} spacing={2}>
                    <Icon as={MdInfo} color="orange.500" />
                    <Heading size="md" color="#1b2559">Yêu cầu sửa chữa mới</Heading>
                    <Badge borderRadius="full" colorScheme="orange">{newAssignments.length}</Badge>
                  </HStack>
                  {newAssignments.length === 0 ? (
                    <Box bg="gray.50" p={8} textAlign="center" borderRadius="xl" border="1px dashed" borderColor="gray.200">
                      <Text color="gray.500">Không có yêu cầu sửa chữa mới.</Text>
                    </Box>
                  ) : (
                    <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} spacing={4}>
                      {newAssignments.map((assignment) => (
                        <AssignmentCard
                          key={readValue(assignment, "id", "Id")}
                          assignment={assignment}
                          onConfirm={() => handleUpdateAssignmentStatus(readValue(assignment, "id", "Id"), "IN_PROGRESS")}
                          onReject={() => setRejectingAssignment(assignment)}
                        />
                      ))}
                    </SimpleGrid>
                  )}
                </Box>

                <Divider />

                <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" p={4} boxShadow="0 14px 32px rgba(15,23,42,0.08)">
                  <HStack mb={4} spacing={2}>
                    <Icon as={MdBuild} color="teal.500" />
                    <Heading size="md" color="#1b2559">Đang sửa chữa</Heading>
                    <Badge borderRadius="full" colorScheme="teal">{activeAssignments.length}</Badge>
                  </HStack>
                  {activeAssignments.length === 0 ? (
                    <Box bg="gray.50" p={8} textAlign="center" borderRadius="xl" border="1px dashed" borderColor="gray.200">
                      <Text color="gray.500">Bạn chưa bắt đầu công việc sửa chữa nào.</Text>
                    </Box>
                  ) : (
                    <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} spacing={4}>
                      {activeAssignments.map((assignment) => (
                        <AssignmentCard
                          key={readValue(assignment, "id", "Id")}
                          assignment={assignment}
                          isActive
                          onOpenDiagnosis={(orderId) =>
                            navigate(`/workshop/technician/orders/${orderId}/diagnosis`, {
                              state: {
                                orderSummary: assignment?.order || null,
                              },
                            })
                          }
                          onFinish={() => handleFinishRepair(assignment)}
                        />
                      ))}
                    </SimpleGrid>
                  )}
                </Box>
              </VStack>
            )}
          </>
        )}
      </VStack>

      {rejectingAssignment && (
        <Modal title="Từ chối công việc" onClose={() => setRejectingAssignment(null)}>
          <VStack align="stretch" spacing={4} p={2}>
            <Box bg="orange.50" p={3} borderRadius="xl" borderLeft="4px solid" borderColor="orange.400">
              <Text fontSize="sm" fontWeight="700">Công việc: {toText(readValue(rejectingAssignment.detail, "itemName", "ItemName")) || "N/A"}</Text>
              <Text fontSize="xs" color="gray.600">Lệnh: {toText(readValue(rejectingAssignment.order, "code", "Code")) || "N/A"}</Text>
            </Box>
            <Box>
              <Text fontWeight="700" mb={2}>Lý do từ chối *</Text>
              <Textarea value={rejectReason} onChange={(event) => setRejectReason(event.target.value)} minH="120px" />
            </Box>
            <HStack justify="flex-end" spacing={3}>
              <Button variant="ghost" borderRadius="full" onClick={() => setRejectingAssignment(null)}>Hủy</Button>
              <Button
                colorScheme="red"
                borderRadius="full"
                isDisabled={!toText(rejectReason)}
                onClick={() => {
                  handleUpdateAssignmentStatus(readValue(rejectingAssignment, "id", "Id"), INTERNAL_ASSIGNMENT_STATUS_REJECTED, rejectReason);
                  setRejectingAssignment(null);
                  setRejectReason("");
                }}
              >
                Gửi lý do và từ chối
              </Button>
            </HStack>
          </VStack>
        </Modal>
      )}

      {supportTarget && (
        <Modal title="Yêu cầu hỗ trợ hội chẩn" onClose={() => setSupportTarget(null)}>
          <VStack align="stretch" spacing={4} p={2}>
            <Box bg="orange.50" p={3} borderRadius="xl" borderLeft="4px solid" borderColor="orange.400">
              <Text fontSize="sm" fontWeight="700">Lệnh: {supportTarget.serviceOrderCode}</Text>
              <HStack mt={1} spacing={1.5} flexWrap="wrap">
                <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                  Biển số: {supportTarget.vehicleLicensePlate || "--"}
                </Badge>
                <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                  Dòng xe: {supportTarget.vehicleDescriptor || "Chưa rõ dòng xe"}
                </Badge>
                <Badge colorScheme="teal" variant="subtle" borderRadius="full" px={2.5}>
                  Nhóm: {supportTarget.teamName || currentStaffTeamName || "--"}
                </Badge>
              </HStack>
              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600">
                Năm SX: {supportTarget.vehicleManufactureYear || "--"} | Màu: {supportTarget.vehicleColor || "--"}
              </Text>
              {toText(supportTarget.vehicleVin) ? (
                <Text fontSize="sm" color="gray.700" fontWeight="600">VIN: {supportTarget.vehicleVin}</Text>
              ) : null}
            </Box>
            <Box>
              <Text fontWeight="700" mb={2}>Nội dung cần hỗ trợ *</Text>
              <Textarea
                value={supportNote}
                onChange={(event) => setSupportNote(event.target.value)}
                placeholder="Mô tả vấn đề kỹ thuật cần tổ trưởng/quản đốc hỗ trợ..."
                minH="120px"
              />
            </Box>
            <HStack justify="flex-end" spacing={3}>
              <Button variant="ghost" borderRadius="full" onClick={() => setSupportTarget(null)}>Hủy</Button>
              <Button colorScheme="orange" borderRadius="full" isDisabled={!toText(supportNote)} onClick={handleSubmitSupport}>Gửi hỗ trợ</Button>
            </HStack>
          </VStack>
        </Modal>
      )}
    </Box>
  );
}

function MetricCard({ title, value, description, color, isInteractive = false, isActive = false, onClick = null }) {
  const handleKeyDown = (event) => {
    if (!isInteractive || typeof onClick !== "function") return;
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onClick();
    }
  };

  return (
    <Box
      role={isInteractive ? "button" : undefined}
      tabIndex={isInteractive ? 0 : undefined}
      aria-pressed={isInteractive ? isActive : undefined}
      onClick={isInteractive ? onClick : undefined}
      onKeyDown={handleKeyDown}
      border="2px solid"
      borderColor={isActive ? color : "#dbe4f2"}
      borderRadius="xl"
      bg={isActive ? "linear-gradient(150deg, #ffffff 0%, #f8fafc 70%, #eef4ff 100%)" : "white"}
      p={4}
      minH="112px"
      boxShadow={isActive ? "0 12px 24px rgba(59, 130, 246, 0.18)" : "0 4px 14px rgba(30, 64, 175, 0.08)"}
      textAlign="left"
      transition="all 0.16s ease"
      cursor={isInteractive ? "pointer" : "default"}
      _hover={isInteractive ? { transform: "translateY(-2px)", borderColor: color, boxShadow: "0 10px 20px rgba(30, 64, 175, 0.14)" } : undefined}
      _focusVisible={isInteractive ? { outline: "2px solid #93c5fd", outlineOffset: "2px" } : undefined}
    >
      <HStack justify="space-between" align="start">
        <Text fontSize="xs" fontWeight="700" textTransform="uppercase" color="gray.500">
          {title}
        </Text>
        <Badge colorScheme="blue" borderRadius="full" px={2} visibility={isActive ? "visible" : "hidden"}>
          Lọc
        </Badge>
      </HStack>
      <Text mt={2} fontSize="3xl" lineHeight="1" fontWeight="800" color={color}>
        {value}
      </Text>
      <HStack mt={2} spacing={1.5}>
        <Icon as={MdTimer} color="gray.400" boxSize={4} />
        <Text fontSize="xs" color="gray.500">{description}</Text>
      </HStack>
    </Box>
  );
}

function DiagnosisTaskCard({ task, onStart, onOpenDiagnosis, onRequestSupport }) {
  const canStart = !task.startedAt && !task.technicalApprovedAt && !task.finalApprovedAt;
  const canOpenDiagnosis = Boolean(task.startedAt);
  const teammateName = task.roleInPair === "Thợ chính" ? task.assistantName : task.leadName;
  const waitingFinal = task.technicalApprovedAt && !task.finalApprovedAt;
  const finalized = Boolean(task.finalApprovedAt);
  const queueMeta = getQueueCardMeta(task.queueColor);
  const roleColorScheme = task.roleInPair === "Thợ chính" ? "blue" : "purple";
  const isMissingBayAssignment = !task.assignedBayId;

  return (
    <Box
      border="1px solid"
      borderColor="gray.200"
      borderLeftWidth="6px"
      borderLeftColor={queueMeta.accent}
      borderRadius="xl"
      bg={queueMeta.bg}
      p={4}
      boxShadow="0 8px 20px rgba(15, 23, 42, 0.08)"
      transition="transform 0.18s ease, box-shadow 0.18s ease, border-color 0.18s ease"
      cursor={canOpenDiagnosis ? "pointer" : "default"}
      onClick={canOpenDiagnosis ? onOpenDiagnosis : undefined}
      title={canOpenDiagnosis ? "Mở phiếu chẩn đoán" : undefined}
      _hover={
        canOpenDiagnosis
          ? {
              transform: "translateY(-3px)",
              borderColor: "blue.200",
              boxShadow: "0 16px 30px rgba(30, 64, 175, 0.16)",
            }
          : {
              boxShadow: "0 10px 24px rgba(15, 23, 42, 0.10)",
            }
      }
    >
      <HStack justify="space-between" align="start" spacing={3} flexWrap="wrap">
        <Box minW={0}>
          <Text
            as="button"
            type="button"
            fontSize="lg"
            fontWeight="800"
            color={canOpenDiagnosis ? "#1d4ed8" : "#1b2559"}
            textDecoration={canOpenDiagnosis ? "underline" : "none"}
            textDecorationThickness="1.5px"
            textUnderlineOffset="3px"
            cursor={canOpenDiagnosis ? "pointer" : "not-allowed"}
            opacity={canOpenDiagnosis ? 1 : 0.9}
            onClick={canOpenDiagnosis ? onOpenDiagnosis : undefined}
            title={canOpenDiagnosis ? "Mở phiếu chẩn đoán" : "Bấm Nhận yêu cầu trước khi mở phiếu"}
            _hover={canOpenDiagnosis ? { color: "#1e40af" } : undefined}
          >
            {task.serviceOrderCode}
          </Text>
          <HStack mt={1} spacing={1.5} flexWrap="wrap">
            <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
              Biển số: {task.vehicleLicensePlate || "--"}
            </Badge>
            <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
              Dòng xe: {task.vehicleDescriptor || "Chưa rõ dòng xe"}
            </Badge>
          </HStack>
          <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
            Năm SX: {task.vehicleManufactureYear || "--"} | Màu: {task.vehicleColor || "--"}
          </Text>
          {toText(task.vehicleVin) ? (
            <Text fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
              VIN: {task.vehicleVin}
            </Text>
          ) : null}
          {!canOpenDiagnosis && (
            <Text mt={0.5} fontSize="xs" color="orange.600">
              Nhấn "Nhận yêu cầu" để mở phiếu chẩn đoán.
            </Text>
          )}
        </Box>
        <HStack spacing={1.5} flexWrap="wrap" justify="flex-end">
          <Badge colorScheme={queueMeta.badge} borderRadius="full">{task.queueColor}</Badge>
          <Badge variant="subtle" colorScheme={roleColorScheme} borderRadius="full">{task.roleInPair}</Badge>
          <Badge variant="subtle" colorScheme="teal" borderRadius="full">{task.teamName || "--"}</Badge>
          {task.isSupportRequested && <Badge colorScheme="orange" borderRadius="full">Đang chờ hỗ trợ</Badge>}
        </HStack>
      </HStack>

      <SimpleGrid columns={{ base: 1, md: 2, xl: 4 }} spacing={2.5} mt={3}>
        <InfoTile
          title="Xe"
          icon={MdDirectionsCar}
          lines={[
            `Biển số: ${task.vehicleLicensePlate || "--"}`,
            `Dòng xe: ${task.vehicleDescriptor || "Chưa rõ dòng xe"}`,
            `Năm SX: ${task.vehicleManufactureYear || "--"}`,
            `Màu: ${task.vehicleColor || "--"}`,
            ...(toText(task.vehicleVin) ? [`VIN: ${task.vehicleVin}`] : []),
          ]}
        />
        <InfoTile
          title="Phân công"
          icon={MdPerson}
          lines={[
            `Nhóm: ${task.teamName || "--"}`,
            `Đồng đội: ${teammateName || "--"}`,
            `Khoang: ${task.assignedBayCode || "Chưa chỉ định"}`,
          ]}
          footer={isMissingBayAssignment ? (
            <Text fontSize="xs" color="red.600">Cần tổ trưởng chỉ định khoang trước khi nhận yêu cầu.</Text>
          ) : null}
        />
        <InfoTile
          title="Mốc thời gian"
          icon={MdTimer}
          lines={[
            `Nhận: ${formatDateTime(task.assignedAt)}`,
            `Bắt đầu: ${formatDateTime(task.startedAt)}`,
          ]}
        />
        <InfoTile
          title="Trạng thái"
          icon={MdInfo}
          lines={[
            `Chờ: ${formatMinutes(task.pendingMinutes)}`,
            `Xử lý: ${formatMinutes(task.assignedMinutes)}`,
          ]}
          footer={(
            <>
              {task.queueColor === "RED" && (
                <HStack spacing={1} color="red.500">
                  <Icon as={MdPriorityHigh} boxSize={4} />
                  <Text fontSize="xs">Ưu tiên xử lý ngay</Text>
                </HStack>
              )}
              {waitingFinal && (
                <Text fontSize="xs" color="blue.600">Đã duyệt kỹ thuật, chờ quản đốc duyệt cuối</Text>
              )}
              {finalized && (
                <Text fontSize="xs" color="green.600">Đã duyệt cuối lúc {formatDateTime(task.finalApprovedAt)}</Text>
              )}
            </>
          )}
        />
      </SimpleGrid>

      <HStack mt={3} pt={3} borderTop="1px solid" borderColor="gray.200" justify="flex-end" align="center" spacing={2} flexWrap="wrap">
        <Button
          size="sm"
          borderRadius="full"
          h="36px"
          minH="36px"
          leftIcon={<MdPlayArrow />}
          colorScheme="green"
          onClick={onStart}
          isDisabled={!canStart}
        >
          {canStart ? "Nhận yêu cầu" : "Đã nhận yêu cầu"}
        </Button>
        <Button
          size="sm"
          borderRadius="full"
          h="36px"
          minH="36px"
          leftIcon={<MdSupportAgent />}
          colorScheme="orange"
          variant="ghost"
          onClick={onRequestSupport}
        >
          Yêu cầu hỗ trợ
        </Button>
      </HStack>
    </Box>
  );
}

function InfoTile({ title, icon, lines, footer = null }) {
  return (
    <Box p={3} border="1px solid" borderColor="gray.100" borderRadius="lg" bg="white">
      <HStack spacing={1.5} mb={1}>
        <Icon as={icon} color="gray.500" boxSize={4} />
        <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
          {title}
        </Text>
      </HStack>
      <Stack spacing={1}>
        {(Array.isArray(lines) ? lines : []).map((line) => (
          <Text key={`${title}-${line}`} fontSize="sm" color="gray.700">
            {line}
          </Text>
        ))}
        {footer}
      </Stack>
    </Box>
  );
}

const AssignmentCard = ({ assignment, isActive, onConfirm, onReject, onOpenDiagnosis, onFinish }) => {
  const order = assignment.order;
  const detail = assignment.detail;
  const isDiagnosing = normalizeCode(readValue(order, "status", "Status")) === "DIAGNOSING";
  const accentColor = isActive ? "#0ea5e9" : "#94a3b8";

  return (
    <Box
      bg="white"
      p={4}
      borderRadius="xl"
      border="1px solid"
      borderColor="gray.200"
      borderLeftWidth="5px"
      borderLeftColor={accentColor}
      boxShadow="0 8px 20px rgba(15, 23, 42, 0.08)"
    >
      <Flex justify="space-between" align="start" mb={4} gap={2}>
        <Badge colorScheme={isActive ? "blue" : "gray"} variant="subtle" borderRadius="full" px={3}>
          {toText(readValue(assignment, "roleInTask", "RoleInTask")) || "Phụ trách"}
        </Badge>
        <Text fontSize="xs" fontWeight="700" color="blue.600">
          {toText(readValue(order, "code", "Code")) || "N/A"}
        </Text>
      </Flex>

      <VStack align="stretch" spacing={3} mb={5}>
        <HStack spacing={2} align="start">
          <Icon as={MdBuild} color="blue.500" mt={0.5} />
          <Text fontWeight="800" fontSize="md" color="#1b2559" noOfLines={2}>
            {toText(readValue(detail, "itemName", "ItemName")) || "Dịch vụ không xác định"}
          </Text>
        </HStack>

        <Box bg="gray.50" p={3} borderRadius="lg" border="1px solid" borderColor="gray.100">
          <HStack justify="space-between" spacing={2} align="start">
            <HStack spacing={2} minW={0}>
              <Icon as={MdDirectionsCar} color="gray.500" />
              <Text fontSize="sm" fontWeight="700" noOfLines={1}>
                {toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) || "N/A"}
              </Text>
            </HStack>
            <Badge variant="outline" fontSize="2xs">
              {toText(readValue(order, "status", "Status")) || "--"}
            </Badge>
          </HStack>
        </Box>

        <Box>
          <Text fontSize="xs" color="gray.500" fontWeight="700" mb={1}>GHI CHÚ DỊCH VỤ</Text>
          <Text fontSize="sm" color="gray.600" noOfLines={2}>
            {toText(readValue(detail, "note", "Note")) || toText(readValue(order, "customerNotes", "CustomerNotes")) || "Không có ghi chú"}
          </Text>
        </Box>
      </VStack>

      <Stack spacing={2}>
        {!isActive ? (
          <HStack spacing={2}>
            <Button flex={1} colorScheme="green" size="sm" h="36px" minH="36px" borderRadius="full" leftIcon={<MdCheckCircle />} onClick={onConfirm}>
              Xác nhận
            </Button>
            <Button flex={1} colorScheme="red" variant="ghost" size="sm" h="36px" minH="36px" borderRadius="full" leftIcon={<MdCancel />} onClick={onReject}>
              Từ chối
            </Button>
          </HStack>
        ) : (
          <>
            {isDiagnosing ? (
              <Button
                colorScheme="blue"
                color="white"
                size="sm"
                h="36px"
                minH="36px"
                borderRadius="full"
                onClick={() => onOpenDiagnosis?.(toPositiveNumber(readValue(order, "id", "Id")))}
              >
                Mở màn hình chẩn đoán
              </Button>
            ) : (
              <Button colorScheme="green" size="sm" h="36px" minH="36px" borderRadius="full" onClick={onFinish}>
                Hoàn thành hạng mục
              </Button>
            )}
          </>
        )}
      </Stack>
    </Box>
  );
};




