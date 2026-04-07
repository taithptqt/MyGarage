import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  AlertIcon,
  Badge,
  Box,
  Button,
  Checkbox,
  CheckboxGroup,
  Divider,
  FormControl,
  FormLabel,
  Grid,
  GridItem,
  Heading,
  HStack,
  Input,
  SimpleGrid,
  Spinner,
  Stack,
  Text,
  Textarea,
  VStack,
  useToast,
} from "@chakra-ui/react";
import { ArrowBackIcon, RepeatIcon } from "@chakra-ui/icons";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import FilterDropdown from "@shared/components/FilterDropdown";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useSearch } from "@shared/core/context/SearchContext";

const COLUMN_ORDER = ["WHITE", "YELLOW", "RED", "GREEN"];

const COLOR_META = {
  WHITE: {
    label: "Mới tiếp nhận",
    description: "Chờ tổ trưởng nhận xe và phân công",
    tabBg: "#f8fafc",
    tabBorder: "#cbd5e1",
    tabText: "#334155",
    activeBg: "#0f172a",
    activeText: "white",
    cardAccent: "#94a3b8",
    cardBg: "white",
  },
  YELLOW: {
    label: "Đang chuẩn đoán",
    description: "Đã có tổ nhận và đang xử lý",
    tabBg: "#fff8db",
    tabBorder: "#f6d365",
    tabText: "#8a6a00",
    activeBg: "#d69e2e",
    activeText: "white",
    cardAccent: "#d69e2e",
    cardBg: "#fffdf3",
  },
  RED: {
    label: "Cần can thiệp",
    description: "Có cảnh báo hỗ trợ, hủy lệnh hoặc quá SLA",
    tabBg: "#fff1f2",
    tabBorder: "#fca5a5",
    tabText: "#b91c1c",
    activeBg: "#e53e3e",
    activeText: "white",
    cardAccent: "#ef4444",
    cardBg: "#fff7f7",
  },
  GREEN: {
    label: "Đã chuẩn đoán",
    description: "Đã có kết quả kỹ thuật để duyệt",
    tabBg: "#ecfdf3",
    tabBorder: "#86efac",
    tabText: "#166534",
    activeBg: "#16a34a",
    activeText: "white",
    cardAccent: "#22c55e",
    cardBg: "#f3fff8",
  },
};

const STATUS_CARD_THEME = {
  WHITE: {
    dot: "#64748b",
    activeBorder: "#334155",
    activeBg: "linear-gradient(150deg, #ffffff 0%, #f8fafc 60%, #eef2ff 100%)",
    activeShadow: "0 12px 26px rgba(51, 65, 85, 0.14)",
  },
  YELLOW: {
    dot: "#b45309",
    activeBorder: "#d69e2e",
    activeBg: "linear-gradient(150deg, #fffdf7 0%, #fff8db 58%, #fff1c7 100%)",
    activeShadow: "0 12px 26px rgba(214, 158, 46, 0.22)",
  },
  RED: {
    dot: "#dc2626",
    activeBorder: "#e53e3e",
    activeBg: "linear-gradient(150deg, #fff8f8 0%, #fff1f2 58%, #ffe0e3 100%)",
    activeShadow: "0 12px 26px rgba(229, 62, 62, 0.22)",
  },
  GREEN: {
    dot: "#15803d",
    activeBorder: "#16a34a",
    activeBg: "linear-gradient(150deg, #f8fffb 0%, #ecfdf3 58%, #dcfce7 100%)",
    activeShadow: "0 12px 26px rgba(22, 163, 74, 0.2)",
  },
};

const DISPATCH_TAG_OPTIONS = [
  { value: "ENGINE_CHASSIS", label: "Máy - Gầm" },
  { value: "ELECTRICAL_AC", label: "Điện - Máy lạnh" },
  { value: "BODY_PAINT", label: "Đồng sơn - Chăm sóc xe" },
  { value: "MAINTENANCE", label: "Bảo dưỡng nhanh" },
];

const DISPATCH_TAG_LABELS = DISPATCH_TAG_OPTIONS.reduce((acc, item) => {
  acc[item.value] = item.label;
  return acc;
}, {});

const CANCEL_REASONS = ["Sai chuyên môn", "Quá tải cầu", "Thiếu thiết bị", "Khác"];

const TEAM_LEADER_ROLE_CODES = new Set(["TEAM_LEADER"]);
const TECHNICIAN_ROLE_CODES = new Set(["TECH", "TECHNICIAN", "TEAM_LEADER", "FOREMAN"]);

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
  const pad2 = (num) => String(num).padStart(2, "0");
  const hours = pad2(parsed.getHours());
  const minutes = pad2(parsed.getMinutes());
  const day = pad2(parsed.getDate());
  const month = pad2(parsed.getMonth() + 1);
  const year = parsed.getFullYear();
  return `${hours}:${minutes} (${day}/${month}/${year})`;
}

function formatMinutes(value) {
  const minutes = Number(value || 0);
  if (!Number.isFinite(minutes) || minutes <= 0) return "0 phút";
  if (minutes < 60) return `${Math.floor(minutes)} phút`;
  const hours = Math.floor(minutes / 60);
  const remain = Math.floor(minutes % 60);
  return remain > 0 ? `${hours} giờ ${remain} phút` : `${hours} giờ`;
}

function getDispatchTagLabel(tag) {
  const normalized = normalizeCode(tag);
  if (!normalized) return "Chưa chọn chuyên môn";

  const splitTags = normalized
    .split(/[;,|/]/)
    .map((item) => normalizeCode(item))
    .filter(Boolean);

  const uniqueTags = splitTags.length > 0 ? Array.from(new Set(splitTags)) : [normalized];
  return uniqueTags.map((code) => DISPATCH_TAG_LABELS[code] || code).join(" + ");
}

function dispatchTagIncludes(tagSource, targetTag) {
  const normalizedTarget = normalizeCode(targetTag);
  if (!normalizedTarget) return true;

  const normalizedSource = normalizeCode(tagSource);
  if (!normalizedSource) return false;

  const splitTags = normalizedSource
    .split(/[;,|/]/)
    .map((item) => normalizeCode(item))
    .filter(Boolean);

  if (splitTags.length === 0) {
    return normalizedSource === normalizedTarget;
  }

  return splitTags.includes(normalizedTarget);
}

function normalizeTeamIds(ids, excludedId = null) {
  const excluded = toPositiveNumber(excludedId);
  const unique = new Set();
  (Array.isArray(ids) ? ids : []).forEach((value) => {
    const id = toPositiveNumber(value);
    if (!id) return;
    if (excluded && id === excluded) return;
    unique.add(id);
  });
  return Array.from(unique);
}

function extractRequestedServiceNames(details) {
  if (!Array.isArray(details) || details.length === 0) return "";

  const names = [];
  const seen = new Set();

  details.forEach((detail) => {
    if (readValue(detail, "isDeleted", "IsDeleted") === true) return;

    const recommendationId = toPositiveNumber(readValue(detail, "diagnoseRecommendationId", "DiagnoseRecommendationId"));
    if (recommendationId) return;

    const itemType = normalizeCode(readValue(detail, "itemType", "ItemType"));
    const hasStandardService = toPositiveNumber(readValue(detail, "standardServiceId", "StandardServiceId"));
    const isServiceLike = itemType === "SERVICE" || itemType === "LABOR" || Boolean(hasStandardService);
    if (!isServiceLike) return;

    const name =
      toText(readValue(detail, "itemName", "ItemName")) ||
      toText(readValue(detail, "serviceName", "ServiceName")) ||
      toText(readValue(detail, "standardServiceName", "StandardServiceName"));

    if (!name) return;

    const key = name.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    names.push(name);
  });

  return names.join(", ");
}

function normalizeServiceOrderDetailsResponse(response) {
  if (Array.isArray(response)) return response;

  const directRows = readValue(response, "items", "Items", "rows", "Rows", "data", "Data");
  if (Array.isArray(directRows)) return directRows;

  const nestedData = readValue(response, "data", "Data");
  if (nestedData && typeof nestedData === "object") {
    const nestedRows = readValue(nestedData, "items", "Items", "rows", "Rows", "data", "Data");
    if (Array.isArray(nestedRows)) return nestedRows;
  }

  return [];
}

const QUEUE_STATE_META = {
  NEW: { label: "Mới tiếp nhận", colorScheme: "gray" },
  IN_PROGRESS: { label: "Đang chuẩn đoán", colorScheme: "yellow" },
  ALERT: { label: "Cần can thiệp", colorScheme: "red" },
  DIAGNOSED: { label: "Đã chuẩn đoán", colorScheme: "green" },
};

function getQueueStateMeta(queueState, queueColor) {
  const normalizedState = normalizeCode(queueState);
  if (normalizedState && QUEUE_STATE_META[normalizedState]) {
    return QUEUE_STATE_META[normalizedState];
  }

  const normalizedColor = normalizeCode(queueColor);
  if (normalizedColor === "RED") return QUEUE_STATE_META.ALERT;
  if (normalizedColor === "YELLOW") return QUEUE_STATE_META.IN_PROGRESS;
  if (normalizedColor === "GREEN") return QUEUE_STATE_META.DIAGNOSED;
  return QUEUE_STATE_META.NEW;
}

function resolveApiErrorMessage(error, fallback = "Đã có lỗi xảy ra") {
  const message = toText(readValue(error, "response")?.data?.message);
  if (message) return message;
  const raw = toText(error?.message);
  if (raw) return raw;
  return fallback;
}

function isOperationalBayAvailable(operationalStatus) {
  const normalized = normalizeCode(operationalStatus);
  if (!normalized) return true;
  return !["IN_USE", "BROKEN", "UNDER_MAINTENANCE", "MAINTENANCE", "SUSPENDED"].includes(normalized);
}

function getStaffRoleCode(staff, roleById) {
  const directRoleCode = normalizeCode(readValue(staff, "roleCode", "RoleCode"));
  if (directRoleCode) return directRoleCode;

  const roleId = toPositiveNumber(readValue(staff, "roleId", "RoleId"));
  if (!roleId) return "";

  const role = roleById.get(roleId);
  return normalizeCode(readValue(role, "roleCode", "RoleCode"));
}
function isTechnicianCandidate(staff, roleById) {
  const roleCode = getStaffRoleCode(staff, roleById);
  const roleName = toText(readValue(staff, "roleName", "RoleName")).toLowerCase();

  if (TECHNICIAN_ROLE_CODES.has(roleCode)) return true;
  return roleName.includes("kỹ thuật") || roleName.includes("ky thuat") || roleName.includes("tổ trưởng") || roleName.includes("to truong");
}

function isTeamLeader(staff, roleById) {
  const roleCode = getStaffRoleCode(staff, roleById);
  return TEAM_LEADER_ROLE_CODES.has(roleCode);
}

function isStaffActive(staff) {
  if (readValue(staff, "isDeleted", "IsDeleted") === true) return false;
  const status = normalizeCode(readValue(staff, "status", "Status"));
  if (!status) return true;
  return status !== "INACTIVE";
}

function sortStaffByName(list) {
  return [...list].sort((a, b) => {
    const aName = toText(readValue(a, "fullName", "FullName"));
    const bName = toText(readValue(b, "fullName", "FullName"));
    return aName.localeCompare(bName, "vi");
  });
}

function resolveCurrentStaff(staffs, user) {
  if (!Array.isArray(staffs) || staffs.length === 0 || !user) return null;

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

function resolveCustomerName(raw, order, customerMap) {
  const boardName = toText(readValue(raw, "customerName", "CustomerName"));
  if (boardName) return boardName;

  const orderName = toText(readValue(order, "customerName", "CustomerName")) || toText(readValue(order, "guestName", "GuestName"));
  if (orderName) return orderName;

  const customerId = toPositiveNumber(readValue(order, "customerId", "CustomerId"));
  if (!customerId) return "";

  const customer = customerMap.get(customerId);
  return toText(readValue(customer, "fullName", "FullName", "name", "Name"));
}

function resolveVehiclePlate(raw, order, vehicleMap) {
  const boardPlate = toText(readValue(raw, "vehicleLicensePlate", "VehicleLicensePlate"));
  if (boardPlate) return boardPlate;

  const orderPlate = toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) || toText(readValue(order, "guestVehicleInfo", "GuestVehicleInfo"));
  if (orderPlate) return orderPlate;

  const vehicleId = toPositiveNumber(readValue(order, "vehicleId", "VehicleId"));
  if (!vehicleId) return "";

  const vehicle = vehicleMap.get(vehicleId);
  return toText(readValue(vehicle, "licensePlate", "LicensePlate", "plateNumber", "PlateNumber"));
}

function extractAlertReasons(item) {
  const reasons = [];
  if (!item) return reasons;

  if (item.isSupportRequested) {
    reasons.push("Có yêu cầu hỗ trợ hội chẩn từ cầu nâng");
  }

  if (item.isCancelPendingApproval) {
    reasons.push("Đang chờ quản đốc duyệt hủy/phân lại");
  }

  if (item.queueColor === "RED" && !item.technicianId && item.pendingMinutes >= 10) {
    reasons.push("Xe chờ nhận quá 10 phút");
  }

  if (item.queueColor === "RED" && item.technicianId && item.assignedMinutes >= 45) {
    reasons.push("Chuẩn đoán đang vượt SLA 45 phút");
  }

  return reasons;
}

function normalizeBoardItem(raw, orderMap, customerMap, vehicleMap, teamMap = new Map()) {
  const serviceOrderId = toPositiveNumber(readValue(raw, "serviceOrderId", "ServiceOrderId")) || 0;
  const order = orderMap.get(serviceOrderId) || null;
  const teamId = toPositiveNumber(readValue(raw, "teamId", "TeamId"));
  const teamName = toText(readValue(raw, "teamName", "TeamName"));
  const primaryTeamId = toPositiveNumber(readValue(raw, "primaryTeamId", "PrimaryTeamId")) || teamId;
  const primaryTeamName =
    toText(readValue(raw, "primaryTeamName", "PrimaryTeamName")) ||
    teamName ||
    (primaryTeamId ? toText(readValue(teamMap.get(primaryTeamId), "name", "Name")) : "");
  const rawAssignedTeamIds = Array.isArray(readValue(raw, "assignedTeamIds", "AssignedTeamIds"))
    ? readValue(raw, "assignedTeamIds", "AssignedTeamIds")
    : [];
  const assignedTeamIds = normalizeTeamIds(
    [primaryTeamId, teamId, ...rawAssignedTeamIds],
    null
  );
  const rawAssignedTeamNames = Array.isArray(readValue(raw, "assignedTeamNames", "AssignedTeamNames"))
    ? readValue(raw, "assignedTeamNames", "AssignedTeamNames")
    : [];
  const normalizedAssignedTeamNames = rawAssignedTeamNames
    .map((name) => toText(name))
    .filter(Boolean);
  const supportingTeamIds = normalizeTeamIds(assignedTeamIds, primaryTeamId);
  const supportingTeamNamesFromApi = normalizedAssignedTeamNames.filter(
    (name) => normalizeCode(name) !== normalizeCode(primaryTeamName)
  );
  const supportingTeamNamesFromMap = supportingTeamIds
    .map((id) => toText(readValue(teamMap.get(id), "name", "Name")))
    .filter(Boolean);
  const supportingTeamNames = supportingTeamNamesFromApi.length > 0
    ? supportingTeamNamesFromApi
    : supportingTeamNamesFromMap;
  const technicianId = toPositiveNumber(readValue(raw, "technicianId", "TechnicianId"));
  const technicianName = toText(readValue(raw, "technicianName", "TechnicianName"));
  const assistantTechnicianId = toPositiveNumber(readValue(raw, "assistantTechnicianId", "AssistantTechnicianId"));
  const assistantTechnicianName = toText(readValue(raw, "assistantTechnicianName", "AssistantTechnicianName"));
  const assignedBayId = toPositiveNumber(readValue(raw, "assignedBayId", "AssignedBayId"));
  const assignedBayCode = toText(readValue(raw, "assignedBayCode", "AssignedBayCode"));
  const assignedAt = readValue(raw, "assignedAt", "AssignedAt");
  const startedAt = readValue(raw, "startedAt", "StartedAt");
  const customerNotesRaw = toText(readValue(order, "customerNotes", "CustomerNotes"));
  const parsedCustomerNotes = parseCustomerNotes(customerNotesRaw);

  // Guard rail: if no real assignment signal exists, keep assignee fields empty.
  const hasRealAssignment = Boolean(primaryTeamId || assignedAt || startedAt || assignedBayId);
  const hasLeadTechnicianAssigned = Boolean(technicianId);
  const hasAssistantTechnicianAssigned = Boolean(assistantTechnicianId);

  return {
    serviceOrderId,
    serviceOrderCode:
      toText(readValue(raw, "serviceOrderCode", "ServiceOrderCode")) ||
      toText(readValue(order, "code", "Code")) ||
      `SO-${serviceOrderId}`,
    serviceOrderStatus: toText(readValue(raw, "serviceOrderStatus", "ServiceOrderStatus")) || toText(readValue(order, "status", "Status")),
    queueColor: normalizeCode(readValue(raw, "queueColor", "QueueColor")) || "WHITE",
    queueState: toText(readValue(raw, "queueState", "QueueState")),
    dispatchTag: normalizeCode(readValue(raw, "dispatchTag", "DispatchTag")),
    teamId: hasRealAssignment ? primaryTeamId : null,
    teamName: hasRealAssignment ? primaryTeamName : "",
    primaryTeamId: hasRealAssignment ? primaryTeamId : null,
    primaryTeamName: hasRealAssignment ? primaryTeamName : "",
    assignedTeamIds: hasRealAssignment ? assignedTeamIds : [],
    supportingTeamIds: hasRealAssignment ? supportingTeamIds : [],
    supportingTeamNames: hasRealAssignment ? supportingTeamNames : [],
    technicianId: hasRealAssignment && hasLeadTechnicianAssigned ? technicianId : null,
    technicianName: hasRealAssignment && hasLeadTechnicianAssigned ? technicianName : "",
    assistantTechnicianId: hasRealAssignment && hasAssistantTechnicianAssigned ? assistantTechnicianId : null,
    assistantTechnicianName: hasRealAssignment && hasAssistantTechnicianAssigned ? assistantTechnicianName : "",
    assignedBayId: hasRealAssignment ? assignedBayId : null,
    assignedBayCode: hasRealAssignment ? assignedBayCode : "",
    requestedAt: readValue(raw, "requestedAt", "RequestedAt"),
    assignedAt: hasRealAssignment ? assignedAt : null,
    startedAt: hasRealAssignment ? startedAt : null,
    pendingMinutes: Number(readValue(raw, "pendingMinutes", "PendingMinutes") || 0),
    assignedMinutes: Number(readValue(raw, "assignedMinutes", "AssignedMinutes") || 0),
    isSupportRequested: Boolean(readValue(raw, "isSupportRequested", "IsSupportRequested")),
    isCancelPendingApproval: Boolean(readValue(raw, "isCancelPendingApproval", "IsCancelPendingApproval")),
    cancelReason: toText(readValue(raw, "cancelReason", "CancelReason")),
    technicalApprovedByTeamLeaderId: toPositiveNumber(readValue(raw, "technicalApprovedByTeamLeaderId", "TechnicalApprovedByTeamLeaderId")),
    technicalApprovedAt: readValue(raw, "technicalApprovedAt", "TechnicalApprovedAt"),
    finalApprovedByManagerId: toPositiveNumber(readValue(raw, "finalApprovedByManagerId", "FinalApprovedByManagerId")),
    finalApprovedAt: readValue(raw, "finalApprovedAt", "FinalApprovedAt"),
    customerName: resolveCustomerName(raw, order, customerMap),
    vehicleLicensePlate: resolveVehiclePlate(raw, order, vehicleMap),
    customerNotes: customerNotesRaw,
    customerMainNotes: toText(parsedCustomerNotes?.mainNotes),
    customerRequestedServices: toText(parsedCustomerNotes?.requestedServices),
  };
}
function DispatchQueueCard({ item, isSelected, onSelect }) {
  const meta = COLOR_META[item.queueColor] || COLOR_META.WHITE;
  const alerts = extractAlertReasons(item);
  const queueStateMeta = getQueueStateMeta(item.queueState, item.queueColor);
  const statusBorderByColor = {
    WHITE: "#cbd5e1",
    YELLOW: "#f6d365",
    RED: "#fca5a5",
    GREEN: "#86efac",
  };
  const baseBorderColor = statusBorderByColor[item.queueColor] || "#dbe4f2";

  return (
    <Box
      border="2px solid"
      borderColor={isSelected ? meta.activeBg : baseBorderColor}
      borderLeftWidth="6px"
      borderLeftColor={meta.cardAccent}
      borderRadius="xl"
      bg={isSelected ? "#f8fbff" : "white"}
      p={3.5}
      cursor="pointer"
      transition="all 0.18s ease"
      boxShadow={isSelected ? "0 12px 26px rgba(37, 99, 235, 0.16)" : "0 4px 12px rgba(15,23,42,0.06)"}
      _hover={{ borderColor: meta.activeBg, transform: "translateY(-1px)" }}
      onClick={() => onSelect(item.serviceOrderId)}
    >
      <Stack spacing={3}>
        <HStack justify="space-between" align="start" spacing={3}>
          <Box minW={0}>
            <Text fontWeight="800" color="gray.800" noOfLines={1}>
              {item.serviceOrderCode}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              {item.customerName || "Khách hàng chưa rõ"} - {item.vehicleLicensePlate || "--"}
            </Text>
          </Box>

          <Badge colorScheme={queueStateMeta.colorScheme} borderRadius="full" px={2.5} py={1}>
            {queueStateMeta.label}
          </Badge>
        </HStack>

        <HStack spacing={2} flexWrap="wrap">
          <Badge colorScheme="purple" borderRadius="full" px={2.5}>
            Chuyên môn: {getDispatchTagLabel(item.dispatchTag)}
          </Badge>
          <Badge colorScheme="gray" borderRadius="full" px={2.5}>
            Chờ nhận: {formatMinutes(item.pendingMinutes)}
          </Badge>
          <Badge colorScheme="orange" borderRadius="full" px={2.5}>
            Đang xử lý: {formatMinutes(item.assignedMinutes)}
          </Badge>
        </HStack>

        <Grid templateColumns={{ base: "1fr", md: "1fr 1fr" }} gap={2}>
          <Box p={2} border="1px solid" borderColor="gray.100" borderRadius="md" bg="white">
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
              Nhân sự
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Tổ chính: {item.primaryTeamName || item.teamName || "Chưa giao tổ"}
            </Text>
            {Array.isArray(item.supportingTeamNames) && item.supportingTeamNames.length > 0 && (
              <Text fontSize="sm" color="gray.600" noOfLines={2}>
                Tổ phối hợp: {item.supportingTeamNames.join(", ")}
              </Text>
            )}
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Thợ chính: {item.technicianName || "Chưa phân công"}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Thợ phụ: {item.assistantTechnicianName || "--"}
            </Text>
          </Box>
          <Box p={2} border="1px solid" borderColor="gray.100" borderRadius="md" bg="white">
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
              Tiến độ
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Cầu nâng: {item.assignedBayCode || "Chưa chỉ định"}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Yêu cầu lúc: {formatDateTime(item.requestedAt)}
            </Text>
          </Box>
        </Grid>

        {alerts.length > 0 && (
          <Box border="1px solid" borderColor="red.200" bg="red.50" borderRadius="md" p={2}>
            <Text fontSize="xs" color="red.700" textTransform="uppercase" fontWeight="700" mb={1}>
              Cần can thiệp
            </Text>
            <VStack align="start" spacing={1}>
              {alerts.slice(0, 2).map((reason) => (
                <Text key={`${item.serviceOrderId}-${reason}`} fontSize="sm" color="red.600" noOfLines={2}>
                  - {reason}
                </Text>
              ))}
            </VStack>
          </Box>
        )}
      </Stack>
    </Box>
  );
}

function SuggestionPreview({ suggestion }) {
  if (!suggestion) return null;

  const loads = Array.isArray(suggestion.technicianLoads) ? suggestion.technicianLoads : [];
  return (
    <Box border="1px solid" borderColor="blue.100" borderRadius="xl" bg="#f8fbff" p={3}>
      <Text fontSize="sm" fontWeight="700" color="blue.700" mb={2}>
        Gợi ý phân công tự động
      </Text>
      <Stack spacing={1} fontSize="sm" color="gray.700">
        <Text>Thợ chính gợi ý: {suggestion.suggestedLeadTechnicianName || "--"}</Text>
        <Text>Thợ phụ gợi ý: {suggestion.suggestedAssistantTechnicianName || "--"}</Text>
        <Text>Cầu nâng gợi ý: {suggestion.suggestedBayCode || "--"}</Text>
      </Stack>
      {loads.length > 0 && (
        <Box mt={3}>
          <Text fontSize="xs" color="gray.500" mb={1}>
            Tải hiện tại theo tổ
          </Text>
          <VStack align="stretch" spacing={1}>
            {loads.slice(0, 5).map((row) => (
              <HStack key={row.technicianId} justify="space-between" fontSize="xs" color="gray.600">
                <Text noOfLines={1}>{row.technicianName || `KTV #${row.technicianId}`}</Text>
                <Badge colorScheme="gray" borderRadius="full">
                  {row.activeDiagnosisCount || 0} ca
                </Badge>
              </HStack>
            ))}
          </VStack>
        </Box>
      )}
    </Box>
  );
}

export default function DispatchOpsDashboard({
  mode = "manager",
  pageMode = "board",
  assignmentServiceOrderId = null,
  onOpenAssignment = null,
  onBack = null,
}) {
  const toast = useToast();
  const { user } = useAuth();
  const { selectedBranchId } = useBranch();
  const { searchQuery } = useSearch();

  const isTeamLeaderMode = mode === "team_leader";
  const isManagerMode = mode === "manager";
  const isAssignmentPage = pageMode === "assignment";

  const [boardItems, setBoardItems] = useState([]);
  const [teams, setTeams] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [roles, setRoles] = useState([]);
  const [bays, setBays] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [teamFilter, setTeamFilter] = useState("");
  const [dispatchTagFilter, setDispatchTagFilter] = useState("");
  const [activeColorTab, setActiveColorTab] = useState("WHITE");
  const [selectedServiceOrderId, setSelectedServiceOrderId] = useState(
    toPositiveNumber(assignmentServiceOrderId)
  );

  const [leaderTeamId, setLeaderTeamId] = useState("");
  const [leaderSupportingTeamIds, setLeaderSupportingTeamIds] = useState([]);
  const [leaderLeadTechnicianId, setLeaderLeadTechnicianId] = useState("");
  const [leaderAssistantTechnicianId, setLeaderAssistantTechnicianId] = useState("");
  const [leaderBayId, setLeaderBayId] = useState("");
  const [leaderSupportNote, setLeaderSupportNote] = useState("");
  const [leaderCancelReason, setLeaderCancelReason] = useState(CANCEL_REASONS[0]);
  const [leaderCancelNote, setLeaderCancelNote] = useState("");
  const [leaderTechnicalApprovalNote, setLeaderTechnicalApprovalNote] = useState("");

  const [managerTeamId, setManagerTeamId] = useState("");
  const [managerSupportingTeamIds, setManagerSupportingTeamIds] = useState([]);
  const [managerLeadTechnicianId, setManagerLeadTechnicianId] = useState("");
  const [managerAssistantTechnicianId, setManagerAssistantTechnicianId] = useState("");
  const [managerBayId, setManagerBayId] = useState("");
  const [managerOverrideReason, setManagerOverrideReason] = useState("");
  const [managerDecisionNote, setManagerDecisionNote] = useState("");
  const [managerFinalApprovalNote, setManagerFinalApprovalNote] = useState("");

  const [suggestion, setSuggestion] = useState(null);
  const [suggestionLoading, setSuggestionLoading] = useState(false);
  const [selectedRequestedServices, setSelectedRequestedServices] = useState("");
  const [selectedRequestedServicesLoading, setSelectedRequestedServicesLoading] = useState(false);

  const selectedBranchRaw = Number(selectedBranchId);
  const isAllBranchesSelected = Number.isFinite(selectedBranchRaw) && selectedBranchRaw === 0;
  const selectedBranch = toPositiveNumber(selectedBranchRaw);
  const userBranchId = toPositiveNumber(readValue(user, "branchId", "BranchId"));
  const forceCurrentBranch = isManagerMode && Boolean(userBranchId);
  const branchId = forceCurrentBranch
    ? userBranchId
    : (isAllBranchesSelected ? null : selectedBranch || userBranchId);

  const roleById = useMemo(() => {
    const map = new Map();
    (roles || []).forEach((role) => {
      const id = toPositiveNumber(readValue(role, "id", "Id"));
      if (id) map.set(id, role);
    });
    return map;
  }, [roles]);

  const currentStaff = useMemo(() => resolveCurrentStaff(staffs, user), [staffs, user]);
  const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id")) || toPositiveNumber(readValue(user, "id", "Id"));
  const currentStaffTeamId = toPositiveNumber(readValue(currentStaff, "teamId", "TeamId"));

  const activeTeams = useMemo(() => {
    const list = (Array.isArray(teams) ? teams : []).filter((team) => {
      if (readValue(team, "isDeleted", "IsDeleted") === true) return false;
      const teamBranchId = toPositiveNumber(readValue(team, "branchId", "BranchId"));
      if (!branchId) return true;
      return teamBranchId === branchId;
    });

    return [...list].sort((a, b) => {
      const aName = toText(readValue(a, "name", "Name"));
      const bName = toText(readValue(b, "name", "Name"));
      return aName.localeCompare(bName, "vi");
    });
  }, [teams, branchId]);
  const currentTeam = useMemo(() => {
    if (!currentStaffTeamId) return null;
    return activeTeams.find((team) => toPositiveNumber(readValue(team, "id", "Id")) === currentStaffTeamId) || null;
  }, [activeTeams, currentStaffTeamId]);

  const currentTeamSpecialty = normalizeCode(readValue(currentTeam, "specialtyTag", "SpecialtyTag"));

  const activeTechnicians = useMemo(() => {
    const filtered = (Array.isArray(staffs) ? staffs : []).filter((staff) => {
      if (!isStaffActive(staff)) return false;
      if (!isTechnicianCandidate(staff, roleById)) return false;

      const staffBranchId = toPositiveNumber(readValue(staff, "branchId", "BranchId"));
      if (!branchId) return true;
      return staffBranchId === branchId;
    });

    return sortStaffByName(filtered);
  }, [staffs, roleById, branchId]);

  const availableBays = useMemo(() => {
    const sorted = (Array.isArray(bays) ? bays : [])
      .filter((bay) => {
        if (readValue(bay, "isDeleted", "IsDeleted") === true) return false;

        const isActive = readValue(bay, "isActive", "IsActive");
        if (isActive === false) return false;

        const bayBranchId = toPositiveNumber(readValue(bay, "branchId", "BranchId"));
        if (!branchId) return true;
        return bayBranchId === branchId;
      })
      .sort((a, b) => {
        const aOccupied = Boolean(readValue(a, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
        const bOccupied = Boolean(readValue(b, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
        if (aOccupied !== bOccupied) return Number(aOccupied) - Number(bOccupied);

        const aCode = toText(readValue(a, "code", "Code", "name", "Name"));
        const bCode = toText(readValue(b, "code", "Code", "name", "Name"));
        return aCode.localeCompare(bCode, "vi");
      });

    return sorted;
  }, [bays, branchId]);

  const getTechniciansByTeam = useCallback(
    (targetTeamId) => {
      const parsedTeamId = toPositiveNumber(targetTeamId);
      if (!parsedTeamId) return activeTechnicians;
      return activeTechnicians.filter((staff) => toPositiveNumber(readValue(staff, "teamId", "TeamId")) === parsedTeamId);
    },
    [activeTechnicians]
  );

  const leaderTechnicianOptions = useMemo(() => getTechniciansByTeam(leaderTeamId), [getTechniciansByTeam, leaderTeamId]);
  const managerTechnicianOptions = useMemo(() => getTechniciansByTeam(managerTeamId), [getTechniciansByTeam, managerTeamId]);

  const assignmentTeamOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn tổ --" }];
    activeTeams.forEach((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Tổ #${id}` });
    });
    return options;
  }, [activeTeams]);

  const leaderSupportingTeamOptions = useMemo(() => {
    const primaryTeamId = toPositiveNumber(leaderTeamId);
    return activeTeams
      .map((team) => {
        const id = toPositiveNumber(readValue(team, "id", "Id"));
        const name = toText(readValue(team, "name", "Name"));
        return { id, name: name || (id ? `Tổ #${id}` : "") };
      })
      .filter((team) => team.id && (!primaryTeamId || team.id !== primaryTeamId));
  }, [activeTeams, leaderTeamId]);

  const managerSupportingTeamOptions = useMemo(() => {
    const primaryTeamId = toPositiveNumber(managerTeamId);
    return activeTeams
      .map((team) => {
        const id = toPositiveNumber(readValue(team, "id", "Id"));
        const name = toText(readValue(team, "name", "Name"));
        return { id, name: name || (id ? `Tổ #${id}` : "") };
      })
      .filter((team) => team.id && (!primaryTeamId || team.id !== primaryTeamId));
  }, [activeTeams, managerTeamId]);

  const leaderPrimaryTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn thợ chính --" }];
    leaderTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [leaderTechnicianOptions]);

  const leaderAssistantTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Không chọn --" }];
    leaderTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [leaderTechnicianOptions]);

  const managerPrimaryTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn thợ chính --" }];
    managerTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [managerTechnicianOptions]);

  const managerAssistantTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Không chọn --" }];
    managerTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [managerTechnicianOptions]);

  const baySelectionOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chưa chỉ định cầu --" }];
    availableBays.forEach((bay) => {
      const id = toPositiveNumber(readValue(bay, "id", "Id"));
      const code = toText(readValue(bay, "code", "Code")) || toText(readValue(bay, "name", "Name")) || `Cầu #${id}`;
      const occupied = Boolean(readValue(bay, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
      const operational = isOperationalBayAvailable(readValue(bay, "operationalStatus", "OperationalStatus"));
      const disabled = occupied || !operational;
      if (!id) return;
      options.push({
        value: String(id),
        label: `${code} ${disabled ? "(Đang bận)" : "(Trống)"}`,
        disabled,
      });
    });
    return options;
  }, [availableBays]);

  const cancelReasonOptions = useMemo(
    () => CANCEL_REASONS.map((reason) => ({ value: reason, label: reason })),
    []
  );

  const teamFilterOptions = useMemo(() => {
    const options = [{ value: "", label: "Tất cả tổ" }];
    activeTeams.forEach((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      options.push({
        value: id ? String(id) : "",
        label: name || `Tổ #${id}`,
      });
    });
    return options;
  }, [activeTeams]);

  const dispatchTagFilterOptions = useMemo(
    () => [{ value: "", label: "Tất cả chuyên môn" }, ...DISPATCH_TAG_OPTIONS.map((item) => ({ value: item.value, label: item.label }))],
    []
  );

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");

    const boardParams = { includeCompleted: false };
    const teamFilterId = toPositiveNumber(teamFilter);
    if (branchId) {
      boardParams.branchId = branchId;
    }
    if (teamFilterId) {
      boardParams.teamId = teamFilterId;
    }

    try {
      const [
        boardRes,
        ordersRes,
        customersRes,
        vehiclesRes,
        teamsRes,
        staffsRes,
        rolesRes,
        baysRes,
      ] = await Promise.all([
        serviceOrderApi.getDiagnosisDispatchBoard(boardParams),
        serviceOrderApi.getAll(),
        getAllCustomers(branchId || undefined),
        getAllVehicles(),
        serviceOrderApi.getTeams(),
        getAllStaff(branchId || undefined),
        getAllRoles(),
        serviceOrderApi.getBays(),
      ]);

      const orderList = Array.isArray(ordersRes) ? ordersRes : [];
      const customerList = Array.isArray(customersRes) ? customersRes : [];
      const vehicleList = Array.isArray(vehiclesRes) ? vehiclesRes : [];

      setTeams(Array.isArray(teamsRes) ? teamsRes : []);
      setStaffs(Array.isArray(staffsRes) ? staffsRes : []);
      setRoles(Array.isArray(rolesRes) ? rolesRes : []);
      setBays(Array.isArray(baysRes) ? baysRes : []);

      const orderMap = new Map(orderList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const customerMap = new Map(customerList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const vehicleMap = new Map(vehicleList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const teamMap = new Map(
        (Array.isArray(teamsRes) ? teamsRes : []).map((team) => [toPositiveNumber(readValue(team, "id", "Id")) || 0, team])
      );

      const normalized = (Array.isArray(boardRes) ? boardRes : [])
        .map((item) => normalizeBoardItem(item, orderMap, customerMap, vehicleMap, teamMap))
        .filter((item) => item.serviceOrderId > 0);

      setBoardItems(normalized);
    } catch (err) {
      setError(resolveApiErrorMessage(err, "Không thể tải bảng điều phối."));
    } finally {
      setLoading(false);
    }
  }, [branchId, teamFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (!isTeamLeaderMode) return;
    if (!currentStaffTeamId) return;
    if (!toPositiveNumber(teamFilter)) {
      setTeamFilter(String(currentStaffTeamId));
    }
    if (!toPositiveNumber(leaderTeamId)) {
      setLeaderTeamId(String(currentStaffTeamId));
    }
  }, [isTeamLeaderMode, currentStaffTeamId, teamFilter, leaderTeamId]);

  useEffect(() => {
    const selectedTeamId = toPositiveNumber(teamFilter);
    if (!selectedTeamId) return;
    const isExisting = activeTeams.some((team) => toPositiveNumber(readValue(team, "id", "Id")) === selectedTeamId);
    if (!isExisting) {
      setTeamFilter("");
    }
  }, [activeTeams, teamFilter]);

  const scopedItems = useMemo(() => {
    return boardItems.filter((item) => {
      if (!isTeamLeaderMode || !currentStaffTeamId) return true;

      if (item.teamId === currentStaffTeamId) return true;

      if (!item.teamId) {
        if (item.queueColor !== "WHITE" && item.queueColor !== "RED") return false;
        if (currentTeamSpecialty && item.dispatchTag && !dispatchTagIncludes(item.dispatchTag, currentTeamSpecialty)) return false;
        return true;
      }

      return false;
    });
  }, [boardItems, isTeamLeaderMode, currentStaffTeamId, currentTeamSpecialty]);

  const visibleItems = useMemo(() => {
    const query = toText(searchQuery).toLowerCase();
    const tagFilterCode = normalizeCode(dispatchTagFilter);

    return scopedItems.filter((item) => {
      if (tagFilterCode && !dispatchTagIncludes(item.dispatchTag, tagFilterCode)) return false;
      if (!query) return true;

      const haystack = [
        item.serviceOrderCode,
        item.customerName,
        item.vehicleLicensePlate,
        item.teamName,
        Array.isArray(item.supportingTeamNames) ? item.supportingTeamNames.join(" ") : "",
        item.technicianName,
        item.assistantTechnicianName,
        getDispatchTagLabel(item.dispatchTag),
      ]
        .map((value) => toText(value).toLowerCase())
        .join(" ");

      return haystack.includes(query);
    });
  }, [scopedItems, searchQuery, dispatchTagFilter]);

  const boardByColor = useMemo(() => {
    const grouped = { WHITE: [], YELLOW: [], RED: [], GREEN: [] };
    visibleItems.forEach((item) => {
      const color = COLUMN_ORDER.includes(item.queueColor) ? item.queueColor : "WHITE";
      grouped[color].push(item);
    });
    return grouped;
  }, [visibleItems]);

  const totalStats = useMemo(
    () => ({
      WHITE: boardByColor.WHITE.length,
      YELLOW: boardByColor.YELLOW.length,
      RED: boardByColor.RED.length,
      GREEN: boardByColor.GREEN.length,
    }),
    [boardByColor]
  );

  const activeTabItems = useMemo(() => boardByColor[activeColorTab] || [], [activeColorTab, boardByColor]);

  const selectedItem = useMemo(() => {
    const targetId = toPositiveNumber(selectedServiceOrderId);
    if (!targetId) return null;
    const sourceItems = isAssignmentPage ? scopedItems : activeTabItems;
    return sourceItems.find((item) => item.serviceOrderId === targetId) || null;
  }, [isAssignmentPage, scopedItems, activeTabItems, selectedServiceOrderId]);

  useEffect(() => {
    const routeOrderId = toPositiveNumber(assignmentServiceOrderId);
    if (!routeOrderId) return;
    if (routeOrderId !== toPositiveNumber(selectedServiceOrderId)) {
      setSelectedServiceOrderId(routeOrderId);
    }
  }, [assignmentServiceOrderId, selectedServiceOrderId]);

  useEffect(() => {
    if (isAssignmentPage || typeof onOpenAssignment === "function") return;
    if (activeTabItems.length === 0) {
      setSelectedServiceOrderId(null);
      return;
    }

    if (!selectedItem) {
      setSelectedServiceOrderId(activeTabItems[0].serviceOrderId);
    }
  }, [isAssignmentPage, activeTabItems, selectedItem, onOpenAssignment]);

  useEffect(() => {
    if (!isAssignmentPage) return;
    if (!selectedItem?.queueColor) return;
    const color = COLUMN_ORDER.includes(selectedItem.queueColor) ? selectedItem.queueColor : "WHITE";
    if (color !== activeColorTab) {
      setActiveColorTab(color);
    }
  }, [isAssignmentPage, selectedItem?.serviceOrderId, selectedItem?.queueColor, activeColorTab]);
  useEffect(() => {
    if (!selectedItem) return;

    if (isTeamLeaderMode) {
      const nextTeamId = toPositiveNumber(selectedItem.teamId) || currentStaffTeamId || toPositiveNumber(leaderTeamId);
      const nextSupportingTeamIds = normalizeTeamIds(
        selectedItem.supportingTeamIds || selectedItem.assignedTeamIds || [],
        nextTeamId
      );
      setLeaderTeamId(nextTeamId ? String(nextTeamId) : "");
      setLeaderSupportingTeamIds(nextSupportingTeamIds.map((id) => String(id)));
      setLeaderLeadTechnicianId(selectedItem.technicianId ? String(selectedItem.technicianId) : "");
      setLeaderAssistantTechnicianId(selectedItem.assistantTechnicianId ? String(selectedItem.assistantTechnicianId) : "");
      setLeaderBayId(selectedItem.assignedBayId ? String(selectedItem.assignedBayId) : "");
      return;
    }

    const nextManagerTeamId = toPositiveNumber(selectedItem.teamId);
    const nextManagerSupportingTeamIds = normalizeTeamIds(
      selectedItem.supportingTeamIds || selectedItem.assignedTeamIds || [],
      nextManagerTeamId
    );
    setManagerTeamId(nextManagerTeamId ? String(nextManagerTeamId) : "");
    setManagerSupportingTeamIds(nextManagerSupportingTeamIds.map((id) => String(id)));
    setManagerLeadTechnicianId(selectedItem.technicianId ? String(selectedItem.technicianId) : "");
    setManagerAssistantTechnicianId(selectedItem.assistantTechnicianId ? String(selectedItem.assistantTechnicianId) : "");
    setManagerBayId(selectedItem.assignedBayId ? String(selectedItem.assignedBayId) : "");
  }, [selectedItem?.serviceOrderId]);

  useEffect(() => {
    const orderId = toPositiveNumber(selectedItem?.serviceOrderId);
    if (!isAssignmentPage || !orderId) {
      setSelectedRequestedServices("");
      setSelectedRequestedServicesLoading(false);
      return;
    }

    const fallbackRequestedServices = toText(selectedItem?.customerRequestedServices);
    setSelectedRequestedServices(fallbackRequestedServices);
    setSelectedRequestedServicesLoading(true);

    let cancelled = false;

    serviceOrderApi
      .getDetails(orderId)
      .then((detailsRes) => {
        if (cancelled) return;
        const normalized = normalizeServiceOrderDetailsResponse(detailsRes);
        const extracted = extractRequestedServiceNames(normalized);
        setSelectedRequestedServices(extracted || fallbackRequestedServices);
      })
      .catch(() => {
        if (!cancelled) {
          setSelectedRequestedServices(fallbackRequestedServices);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setSelectedRequestedServicesLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [isAssignmentPage, selectedItem?.serviceOrderId, selectedItem?.customerRequestedServices]);

  useEffect(() => {
    const primaryTeamId = toPositiveNumber(leaderTeamId);
    setLeaderSupportingTeamIds((prev) =>
      normalizeTeamIds(prev, primaryTeamId).map((id) => String(id))
    );
  }, [leaderTeamId]);

  useEffect(() => {
    const primaryTeamId = toPositiveNumber(managerTeamId);
    setManagerSupportingTeamIds((prev) =>
      normalizeTeamIds(prev, primaryTeamId).map((id) => String(id))
    );
  }, [managerTeamId]);

  const loadDispatchSuggestion = useCallback(
    async (targetMode) => {
      if (!selectedItem) {
        toast({ title: "Chưa chọn lệnh", status: "warning", duration: 2500, isClosable: true });
        return;
      }

      const teamId = targetMode === "team_leader" ? toPositiveNumber(leaderTeamId) : toPositiveNumber(managerTeamId);
      if (!teamId) {
        toast({ title: "Vui lòng chọn tổ trước khi lấy gợi ý", status: "warning", duration: 2600, isClosable: true });
        return;
      }

      setSuggestionLoading(true);
      try {
        const response = await serviceOrderApi.getDiagnosisDispatchSuggestion(selectedItem.serviceOrderId, { teamId });
        setSuggestion(response || null);

        if (targetMode === "team_leader") {
          setLeaderLeadTechnicianId(toPositiveNumber(response?.suggestedLeadTechnicianId) ? String(response.suggestedLeadTechnicianId) : "");
          setLeaderAssistantTechnicianId(toPositiveNumber(response?.suggestedAssistantTechnicianId) ? String(response.suggestedAssistantTechnicianId) : "");
          setLeaderBayId(toPositiveNumber(response?.suggestedBayId) ? String(response.suggestedBayId) : "");
        } else {
          setManagerLeadTechnicianId(toPositiveNumber(response?.suggestedLeadTechnicianId) ? String(response.suggestedLeadTechnicianId) : "");
          setManagerAssistantTechnicianId(toPositiveNumber(response?.suggestedAssistantTechnicianId) ? String(response.suggestedAssistantTechnicianId) : "");
          setManagerBayId(toPositiveNumber(response?.suggestedBayId) ? String(response.suggestedBayId) : "");
        }

        toast({ title: "Đã áp dụng gợi ý phân công", status: "success", duration: 2200, isClosable: true });
      } catch (err) {
        toast({ title: "Không thể lấy gợi ý", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
      } finally {
        setSuggestionLoading(false);
      }
    },
    [selectedItem, leaderTeamId, managerTeamId, toast]
  );

  const handleLeaderAssign = async () => {
    if (!selectedItem) return;

    const leadId = toPositiveNumber(leaderLeadTechnicianId);
    const assistantId = toPositiveNumber(leaderAssistantTechnicianId);
    const teamId = toPositiveNumber(leaderTeamId) || currentStaffTeamId;
    const supportingTeamIds = normalizeTeamIds(leaderSupportingTeamIds, teamId);
    const bayId = toPositiveNumber(leaderBayId);

    if (!leadId || !teamId) {
      toast({ title: "Vui lòng chọn tổ và thợ chính", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (supportingTeamIds.length > 0 && !teamId) {
      toast({ title: "Cần chọn tổ chính trước khi thêm tổ phối hợp", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({ title: "Thợ phụ không được trùng thợ chính", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.acceptDiagnosisRequest(selectedItem.serviceOrderId, leadId, {
        teamId,
        teamLeaderId: currentStaffId,
        assistantTechnicianId: assistantId || null,
        supportingTeamIds,
        bayId: bayId || null,
      });

      toast({ title: "Đã nhận xe và phân công ca chuẩn đoán", status: "success", duration: 2500, isClosable: true });
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể phân công", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleLeaderSupport = async () => {
    if (!selectedItem || !currentStaffId) return;

    setSaving(true);
    try {
      await serviceOrderApi.requestDiagnosisSupport(selectedItem.serviceOrderId, {
        requestedByStaffId: currentStaffId,
        note: toText(leaderSupportNote) || null,
      });

      toast({ title: "Đã gửi yêu cầu hỗ trợ hội chẩn", status: "success", duration: 2500, isClosable: true });
      setLeaderSupportNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể gửi hỗ trợ", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleLeaderCancelRequest = async () => {
    if (!selectedItem || !currentStaffId) return;

    const reason = toText(leaderCancelReason);
    const note = toText(leaderCancelNote);
    const payloadReason = note ? `${reason}: ${note}` : reason;

    if (!payloadReason) {
      toast({ title: "Vui lòng nhập lý do hủy", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.requestDiagnosisCancel(selectedItem.serviceOrderId, {
        requestedByStaffId: currentStaffId,
        reason: payloadReason,
      });

      toast({ title: "Đã gửi yêu cầu hủy lệnh", status: "success", duration: 2500, isClosable: true });
      setLeaderCancelNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể gửi yêu cầu hủy", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleLeaderTechnicalApproval = async () => {
    if (!selectedItem || !currentStaffId) return;

    setSaving(true);
    try {
      await serviceOrderApi.approveDiagnosisTechnical(selectedItem.serviceOrderId, {
        teamLeaderId: currentStaffId,
        note: toText(leaderTechnicalApprovalNote) || null,
      });

      toast({ title: "Đã duyệt báo cáo kỹ thuật", status: "success", duration: 2500, isClosable: true });
      setLeaderTechnicalApprovalNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể duyệt kỹ thuật", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleManagerOverride = async () => {
    if (!selectedItem || !currentStaffId) return;

    const leadId = toPositiveNumber(managerLeadTechnicianId);
    const assistantId = toPositiveNumber(managerAssistantTechnicianId);
    const teamId = toPositiveNumber(managerTeamId);
    const supportingTeamIds = normalizeTeamIds(managerSupportingTeamIds, teamId);
    const bayId = toPositiveNumber(managerBayId);

    if (!leadId) {
      toast({ title: "Vui lòng chọn thợ chính", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    if (supportingTeamIds.length > 0 && !teamId) {
      toast({ title: "Vui lòng chọn tổ chính khi có tổ phối hợp", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({ title: "Thợ phụ không được trùng thợ chính", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.overrideDiagnosisAssignment(selectedItem.serviceOrderId, {
        managerId: currentStaffId,
        technicianId: leadId,
        assistantTechnicianId: assistantId || null,
        teamId: teamId || null,
        supportingTeamIds,
        bayId: bayId || null,
        reason: toText(managerOverrideReason) || null,
      });

      toast({ title: "Đã override phân công thành công", status: "success", duration: 2500, isClosable: true });
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể override", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleManagerCancelDecision = async (approve) => {
    if (!selectedItem || !currentStaffId) return;

    setSaving(true);
    try {
      await serviceOrderApi.decideDiagnosisCancel(selectedItem.serviceOrderId, {
        managerId: currentStaffId,
        approve,
        note: toText(managerDecisionNote) || null,
      });

      toast({
        title: approve ? "Đã duyệt hủy lệnh" : "Đã từ chối hủy lệnh",
        status: "success",
        duration: 2500,
        isClosable: true,
      });
      setManagerDecisionNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể xử lý yêu cầu hủy", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleManagerFinalApproval = async () => {
    if (!selectedItem || !currentStaffId) return;

    setSaving(true);
    try {
      await serviceOrderApi.approveDiagnosisFinal(selectedItem.serviceOrderId, {
        managerId: currentStaffId,
        note: toText(managerFinalApprovalNote) || null,
      });

      toast({ title: "Đã duyệt báo giá cuối cùng", status: "success", duration: 2500, isClosable: true });
      setManagerFinalApprovalNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể duyệt cuối", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const canLeaderAssign = Boolean(selectedItem) && !selectedItem?.finalApprovedByManagerId;
  const canLeaderSupport = Boolean(selectedItem?.technicianId) && !selectedItem?.finalApprovedByManagerId;
  const canLeaderCancel = Boolean(selectedItem?.technicianId) && !selectedItem?.isCancelPendingApproval;
  const canLeaderTechnicalApprove = Boolean(selectedItem) && !selectedItem?.technicalApprovedByTeamLeaderId;

  const canManagerOverride = Boolean(selectedItem);
  const canManagerCancelDecision = Boolean(selectedItem?.isCancelPendingApproval);
  const canManagerFinalApprove = Boolean(selectedItem?.technicalApprovedByTeamLeaderId) && !selectedItem?.finalApprovedByManagerId;
  const actionButtonBaseProps = {
    h: "38px",
    minH: "38px",
    px: 4,
    py: 0,
    borderRadius: "full",
    fontSize: "sm",
    fontWeight: "700",
    lineHeight: "1",
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    whiteSpace: "nowrap",
  };

  const handleQueueCardSelect = useCallback(
    (serviceOrderId) => {
      const targetId = toPositiveNumber(serviceOrderId);
      if (!targetId) return;

      if (!isAssignmentPage && typeof onOpenAssignment === "function") {
        onOpenAssignment(targetId);
        return;
      }

      setSelectedServiceOrderId(targetId);
    },
    [isAssignmentPage, onOpenAssignment]
  );

  return (
    <Box
      p={{ base: 4, md: 6 }}
      minH="100vh"
      bg="linear-gradient(180deg, #f4f7ff 0%, #eef4ff 100%)"
    >
      <VStack align="stretch" spacing={5}>
        <HStack justify="space-between" align={{ base: "start", md: "center" }} flexWrap="wrap" spacing={3}>
          <Box>
            <Heading size="lg" color="#1b2559">
              {isTeamLeaderMode ? "Điều phối chuẩn đoán - Tổ trưởng" : "Điều phối chuẩn đoán - Quản đốc"}
            </Heading>
            <Text color="gray.600" mt={1}>
              {isAssignmentPage
                ? "Trang phân công chi tiết cho từng lệnh, tập trung thao tác điều phối và phê duyệt."
                : "Theo dõi 4 giai đoạn từ tiếp nhận, phân công đến phê duyệt kỹ thuật và báo giá."}
            </Text>
          </Box>

          <HStack spacing={2} align="center">
            {isAssignmentPage && (
              <Button
                leftIcon={<ArrowBackIcon />}
                variant="ghost"
                colorScheme="blue"
                onClick={onBack}
              >
                Quay lại danh sách
              </Button>
            )}
            <Button
              leftIcon={<RepeatIcon />}
              colorScheme="blue"
              variant="outline"
              onClick={fetchData}
              isLoading={loading}
            >
              Làm mới bảng
            </Button>
          </HStack>
        </HStack>

        {!isAssignmentPage && (
          <Box
            bg="white"
            borderRadius="2xl"
            border="1px solid"
            borderColor="#d9e2f1"
            p={{ base: 3, md: 4 }}
            boxShadow="0 12px 30px rgba(15, 23, 42, 0.08)"
          >
            <HStack justify="space-between" align={{ base: "start", md: "center" }} flexWrap="wrap" spacing={2}>
              <Box>
                <Text fontSize="sm" fontWeight="700" color="#1e3a8a">
                  Trạng thái điều phối
                </Text>
                <Text fontSize="sm" color="gray.500">
                  Nhấn vào một trạng thái để phân loại danh sách lệnh.
                </Text>
              </Box>
              <Badge
                borderRadius="full"
                px={3}
                py={1}
                bg="#e0ecff"
                color="#1d4ed8"
                fontSize="xs"
                fontWeight="700"
              >
                Đang xem: {COLOR_META[activeColorTab]?.label || "--"} ({totalStats[activeColorTab] || 0})
              </Badge>
            </HStack>

            <SimpleGrid columns={{ base: 1, sm: 2, xl: 4 }} spacing={3} mt={4}>
              {COLUMN_ORDER.map((color) => {
                const isActive = activeColorTab === color;
                const meta = COLOR_META[color];
                const theme = STATUS_CARD_THEME[color] || STATUS_CARD_THEME.WHITE;
                return (
                  <Box
                    as="button"
                    type="button"
                    key={color}
                    aria-pressed={isActive}
                    onClick={() => setActiveColorTab(color)}
                    minH={{ base: "112px", md: "124px" }}
                    p={4}
                    borderRadius="xl"
                    border="2px solid"
                    borderColor={isActive ? theme.activeBorder : "#dbe4f2"}
                    bg={isActive ? theme.activeBg : "#ffffff"}
                    textAlign="left"
                    transition="all 0.18s ease"
                    boxShadow={isActive ? theme.activeShadow : "0 4px 14px rgba(30, 64, 175, 0.08)"}
                    _hover={{
                      borderColor: theme.activeBorder,
                      transform: "translateY(-2px)",
                    }}
                  >
                    <HStack justify="space-between" align="start">
                      <VStack align="start" spacing={1}>
                        <HStack spacing={2}>
                          <Box
                            w="10px"
                            h="10px"
                            borderRadius="full"
                            bg={theme.dot}
                            boxShadow={isActive ? "0 0 0 4px rgba(255, 255, 255, 0.72)" : "0 0 0 4px rgba(148, 163, 184, 0.2)"}
                          />
                          <Text fontSize={{ base: "sm", md: "md" }} fontWeight="700" color={isActive ? "#0f172a" : "#334155"}>
                            {meta.label}
                          </Text>
                        </HStack>
                        <Text fontSize="xs" color={isActive ? "#475569" : "#64748b"} noOfLines={2}>
                          {meta.description}
                        </Text>
                      </VStack>
                      {isActive && (
                        <Badge borderRadius="full" bg="white" color={theme.dot} border="1px solid" borderColor={theme.activeBorder}>
                          Lọc
                        </Badge>
                      )}
                    </HStack>

                    <Text mt={3} fontSize={{ base: "3xl", md: "4xl" }} lineHeight="1" fontWeight="800" letterSpacing="-0.02em" color={isActive ? theme.dot : "#0f172a"}>
                      {totalStats[color]}
                    </Text>
                  </Box>
                );
              })}
            </SimpleGrid>
          </Box>
        )}

        {!isAssignmentPage && (
          <Box bg="white" borderRadius="xl" border="1px solid" borderColor="gray.100" p={3}>
            <HStack spacing={3} align="center" flexWrap="wrap">
              <FilterDropdown
                id="dispatch-team-filter"
                label="Lọc theo tổ"
                value={teamFilter}
                options={teamFilterOptions}
                onChange={setTeamFilter}
                minWidth={300}
                disabled={isTeamLeaderMode && !!currentStaffTeamId}
              />
              <FilterDropdown
                id="dispatch-tag-filter"
                label="Lọc theo chuyên môn"
                value={dispatchTagFilter}
                options={dispatchTagFilterOptions}
                onChange={setDispatchTagFilter}
                minWidth={260}
              />
            </HStack>
          </Box>
        )}

        {error && (
          <Alert status="error" borderRadius="xl">
            <AlertIcon />
            {error}
          </Alert>
        )}

        <Grid templateColumns="1fr" gap={4} alignItems="start">
          {!isAssignmentPage && (
            <GridItem>
              <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" overflow="hidden" boxShadow="0 14px 32px rgba(15,23,42,0.08)">
                {loading ? (
                  <VStack py={12} spacing={3}>
                    <Spinner color="blue.500" />
                    <Text color="gray.500">Đang tải dữ liệu điều phối...</Text>
                  </VStack>
                ) : activeTabItems.length === 0 ? (
                  <Box py={12} textAlign="center" color="gray.400">
                    Không có lệnh phù hợp bộ lọc
                  </Box>
                ) : (
                  <VStack align="stretch" spacing={3} p={3} bg="#f8fafc">
                    {activeTabItems.map((item) => (
                      <DispatchQueueCard
                        key={`dispatch-${item.serviceOrderId}`}
                        item={item}
                        isSelected={false}
                        onSelect={handleQueueCardSelect}
                      />
                    ))}
                  </VStack>
                )}
              </Box>
            </GridItem>
          )}

          {isAssignmentPage && (
            <GridItem>
            <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" p={4} boxShadow="0 14px 32px rgba(15,23,42,0.08)">
              {!selectedItem ? (
                <Box border="1px dashed" borderColor="gray.200" borderRadius="xl" p={8} textAlign="center" color="gray.500">
                  Không tìm thấy lệnh cần phân công trong danh sách hiện tại.
                </Box>
              ) : (
                <VStack align="stretch" spacing={4}>
                  <Box p={3} border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50">
                    <Text fontSize="xs" color="gray.500" mb={1}>LỆNH SỬA CHỮA</Text>
                    <Heading size="md" color="#1b2559">{selectedItem.serviceOrderCode}</Heading>
                    <HStack mt={2} spacing={2} flexWrap="wrap">
                      <Badge colorScheme={(selectedItem.queueColor === "RED" ? "red" : selectedItem.queueColor === "YELLOW" ? "yellow" : selectedItem.queueColor === "GREEN" ? "green" : "gray")} borderRadius="full">
                        {selectedItem.queueColor}
                      </Badge>
                      <Badge colorScheme="purple" borderRadius="full">Chuyên môn: {getDispatchTagLabel(selectedItem.dispatchTag)}</Badge>
                      <Badge colorScheme={getQueueStateMeta(selectedItem.queueState, selectedItem.queueColor).colorScheme} borderRadius="full">
                        {getQueueStateMeta(selectedItem.queueState, selectedItem.queueColor).label}
                      </Badge>
                    </HStack>
                  </Box>

                  <Box p={3} border="1px solid" borderColor="orange.100" borderRadius="xl" bg="#fffaf0">
                    <Text fontSize="xs" color="orange.700" fontWeight="700" mb={2}>
                      THÔNG TIN PHÂN TỔ TỪ KHÁCH HÀNG
                    </Text>
                    <Stack spacing={2} fontSize="sm" color="gray.700">
                      <Text>
                        <strong>Dịch vụ yêu cầu:</strong>{" "}
                        {selectedRequestedServicesLoading
                          ? "Đang tải..."
                          : (selectedRequestedServices || selectedItem.customerRequestedServices || "Chưa ghi rõ")}
                      </Text>
                      <Text>
                        <strong>Mô tả khách hàng:</strong>{" "}
                        {selectedItem.customerMainNotes || "Không có"}
                      </Text>
                    </Stack>
                  </Box>

                  <Box p={3} border="1px solid" borderColor="blue.100" borderRadius="xl" bg="#f8fbff">
                    <Text fontSize="sm" color="gray.600">Yêu cầu lúc: {formatDateTime(selectedItem.requestedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Nhận ca lúc: {formatDateTime(selectedItem.assignedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Tổ chính: {selectedItem.primaryTeamName || selectedItem.teamName || "Chưa giao tổ"}</Text>
                    {Array.isArray(selectedItem.supportingTeamNames) && selectedItem.supportingTeamNames.length > 0 && (
                      <Text fontSize="sm" color="gray.600">Tổ phối hợp: {selectedItem.supportingTeamNames.join(", ")}</Text>
                    )}
                    <Text fontSize="sm" color="gray.600">Chờ nhận: {formatMinutes(selectedItem.pendingMinutes)}</Text>
                    <Text fontSize="sm" color="gray.600">Đang xử lý: {formatMinutes(selectedItem.assignedMinutes)}</Text>
                    <Text fontSize="sm" color="gray.600">Duyệt kỹ thuật: {selectedItem.technicalApprovedAt ? formatDateTime(selectedItem.technicalApprovedAt) : "Chưa duyệt"}</Text>
                    <Text fontSize="sm" color="gray.600">Duyệt cuối: {selectedItem.finalApprovedAt ? formatDateTime(selectedItem.finalApprovedAt) : "Chưa duyệt"}</Text>
                  </Box>

                  {extractAlertReasons(selectedItem).length > 0 && (
                    <Alert status="error" borderRadius="xl" flexDirection="column" alignItems="start" gap={1}>
                      <HStack>
                        <AlertIcon />
                        <Text fontWeight="700">Cảnh báo cần xử lý</Text>
                      </HStack>
                      {extractAlertReasons(selectedItem).map((reason) => (
                        <Text key={`alert-${reason}`} fontSize="sm">- {reason}</Text>
                      ))}
                    </Alert>
                  )}
                  {isTeamLeaderMode ? (
                    <VStack align="stretch" spacing={4}>
                      <Divider />

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>1) Nhận xe và phân công cặp thợ</Text>

                        <FormControl>
                          <FormLabel fontSize="sm" mb={1}>Tổ thực hiện</FormLabel>
                          <FilterDropdown
                            id="leader-assignment-team"
                            value={leaderTeamId}
                            options={assignmentTeamOptions}
                            onChange={setLeaderTeamId}
                            minWidth={300}
                            disabled={Boolean(currentStaffTeamId)}
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Tổ phối hợp (tuỳ chọn)</FormLabel>
                          <Box border="1px solid" borderColor="gray.200" borderRadius="xl" p={3} bg="white">
                            <CheckboxGroup
                              value={leaderSupportingTeamIds}
                              onChange={(values) => setLeaderSupportingTeamIds((values || []).map((value) => String(value)))}
                            >
                              <VStack align="start" spacing={2}>
                                {leaderSupportingTeamOptions.length === 0 ? (
                                  <Text fontSize="sm" color="gray.500">Chưa có tổ phối hợp phù hợp.</Text>
                                ) : (
                                  leaderSupportingTeamOptions.map((team) => (
                                    <Checkbox key={`leader-support-${team.id}`} value={String(team.id)}>
                                      {team.name}
                                    </Checkbox>
                                  ))
                                )}
                              </VStack>
                            </CheckboxGroup>
                            <Text fontSize="xs" color="gray.500" mt={2}>
                              Tổ chính chịu trách nhiệm điều phối cuối cùng.
                            </Text>
                          </Box>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ chính</FormLabel>
                          <FilterDropdown
                            id="leader-main-tech"
                            value={leaderLeadTechnicianId}
                            options={leaderPrimaryTechnicianOptions}
                            onChange={setLeaderLeadTechnicianId}
                            minWidth="100%"
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ phụ</FormLabel>
                          <FilterDropdown
                            id="leader-assistant-tech"
                            value={leaderAssistantTechnicianId}
                            options={leaderAssistantTechnicianOptions}
                            onChange={setLeaderAssistantTechnicianId}
                            minWidth="100%"
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Cầu nâng</FormLabel>
                          <FilterDropdown
                            id="leader-bay-select"
                            value={leaderBayId}
                            options={baySelectionOptions}
                            onChange={setLeaderBayId}
                            minWidth="100%"
                          />
                        </FormControl>

                        <HStack mt={3} spacing={2} align="stretch">
                          <Button
                            variant="unstyled"
                            {...actionButtonBaseProps}
                            border="2px solid"
                            borderColor="#2563eb"
                            bg="white"
                            color="#1d4ed8"
                            _hover={{ bg: "#eff6ff" }}
                            onClick={() => loadDispatchSuggestion("team_leader")}
                            isLoading={suggestionLoading}
                            isDisabled={!selectedItem || !toPositiveNumber(leaderTeamId)}
                          >
                            Gợi ý tự động
                          </Button>
                          <Button
                            variant="unstyled"
                            {...actionButtonBaseProps}
                            border="2px solid"
                            borderColor="#2563eb"
                            bg="#2563eb"
                            color="white"
                            _hover={{ bg: "#1d4ed8" }}
                            onClick={handleLeaderAssign}
                            isLoading={saving}
                            isDisabled={!canLeaderAssign || !toPositiveNumber(leaderLeadTechnicianId)}
                          >
                            Nhận xe và giao ca
                          </Button>
                        </HStack>

                        <Box mt={3}>
                          <SuggestionPreview suggestion={suggestion} />
                        </Box>
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>2) Yêu cầu hỗ trợ hội chẩn</Text>
                        <Textarea
                          size="sm"
                          value={leaderSupportNote}
                          onChange={(event) => setLeaderSupportNote(event.target.value)}
                          placeholder="Mô tả vấn đề cần quản đốc hỗ trợ..."
                        />
                        <Button
                          mt={3}
                          size="sm"
                          colorScheme="orange"
                          variant="outline"
                          onClick={handleLeaderSupport}
                          isLoading={saving}
                          isDisabled={!canLeaderSupport}
                        >
                          Gửi hỗ trợ
                        </Button>
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>3) Gửi hủy lệnh (chờ duyệt quản đốc)</Text>
                        <FormControl>
                          <FormLabel fontSize="sm" mb={1}>Lý do</FormLabel>
                          <FilterDropdown
                            id="leader-cancel-reason"
                            value={leaderCancelReason}
                            options={cancelReasonOptions}
                            onChange={setLeaderCancelReason}
                            minWidth="100%"
                          />
                        </FormControl>
                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Ghi chú bổ sung</FormLabel>
                          <Textarea
                            size="sm"
                            value={leaderCancelNote}
                            onChange={(event) => setLeaderCancelNote(event.target.value)}
                            placeholder="Thêm chi tiết phục vụ quyết định phân lại..."
                          />
                        </FormControl>
                        <Button
                          mt={3}
                          size="sm"
                          colorScheme="red"
                          variant="outline"
                          onClick={handleLeaderCancelRequest}
                          isLoading={saving}
                          isDisabled={!canLeaderCancel}
                        >
                          Gửi yêu cầu hủy
                        </Button>
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>4) Duyệt báo cáo kỹ thuật</Text>
                        <Textarea
                          size="sm"
                          value={leaderTechnicalApprovalNote}
                          onChange={(event) => setLeaderTechnicalApprovalNote(event.target.value)}
                          placeholder="Nhận xét kỹ thuật (nếu có)..."
                        />
                        <Button
                          mt={3}
                          size="sm"
                          colorScheme="green"
                          onClick={handleLeaderTechnicalApproval}
                          isLoading={saving}
                          isDisabled={!canLeaderTechnicalApprove}
                        >
                          Duyệt kỹ thuật
                        </Button>
                      </Box>
                    </VStack>
                  ) : (
                    <VStack align="stretch" spacing={4}>
                      <Divider />

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>1) Override phân công</Text>

                        <FormControl>
                          <FormLabel fontSize="sm" mb={1}>Tổ thực hiện</FormLabel>
                          <FilterDropdown
                            id="manager-assignment-team"
                            value={managerTeamId}
                            options={assignmentTeamOptions}
                            onChange={setManagerTeamId}
                            minWidth={300}
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Tổ phối hợp (tuỳ chọn)</FormLabel>
                          <Box border="1px solid" borderColor="gray.200" borderRadius="xl" p={3} bg="white">
                            <CheckboxGroup
                              value={managerSupportingTeamIds}
                              onChange={(values) => setManagerSupportingTeamIds((values || []).map((value) => String(value)))}
                            >
                              <VStack align="start" spacing={2}>
                                {managerSupportingTeamOptions.length === 0 ? (
                                  <Text fontSize="sm" color="gray.500">Chưa có tổ phối hợp phù hợp.</Text>
                                ) : (
                                  managerSupportingTeamOptions.map((team) => (
                                    <Checkbox key={`manager-support-${team.id}`} value={String(team.id)}>
                                      {team.name}
                                    </Checkbox>
                                  ))
                                )}
                              </VStack>
                            </CheckboxGroup>
                            <Text fontSize="xs" color="gray.500" mt={2}>
                              Tổ chính là tổ được giao trách nhiệm chính thức.
                            </Text>
                          </Box>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ chính</FormLabel>
                          <FilterDropdown
                            id="manager-main-tech"
                            value={managerLeadTechnicianId}
                            options={managerPrimaryTechnicianOptions}
                            onChange={setManagerLeadTechnicianId}
                            minWidth="100%"
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ phụ</FormLabel>
                          <FilterDropdown
                            id="manager-assistant-tech"
                            value={managerAssistantTechnicianId}
                            options={managerAssistantTechnicianOptions}
                            onChange={setManagerAssistantTechnicianId}
                            minWidth="100%"
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Cầu nâng</FormLabel>
                          <FilterDropdown
                            id="manager-bay-select"
                            value={managerBayId}
                            options={baySelectionOptions}
                            onChange={setManagerBayId}
                            minWidth="100%"
                          />
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Lý do override</FormLabel>
                          <Input
                            size="sm"
                            borderRadius="xl"
                            value={managerOverrideReason}
                            onChange={(event) => setManagerOverrideReason(event.target.value)}
                            placeholder="Ví dụ: xe chờ quá 10 phút, cần điều tải..."
                          />
                        </FormControl>

                        <HStack mt={3} spacing={2} align="stretch">
                          <Button
                            variant="unstyled"
                            {...actionButtonBaseProps}
                            border="2px solid"
                            borderColor="#2563eb"
                            bg="white"
                            color="#1d4ed8"
                            _hover={{ bg: "#eff6ff" }}
                            onClick={() => loadDispatchSuggestion("manager")}
                            isLoading={suggestionLoading}
                            isDisabled={!selectedItem || !toPositiveNumber(managerTeamId)}
                          >
                            Gợi ý tự động
                          </Button>
                          <Button
                            variant="unstyled"
                            {...actionButtonBaseProps}
                            border="2px solid"
                            borderColor="#2563eb"
                            bg="#2563eb"
                            color="white"
                            _hover={{ bg: "#1d4ed8" }}
                            onClick={handleManagerOverride}
                            isLoading={saving}
                            isDisabled={!canManagerOverride || !toPositiveNumber(managerLeadTechnicianId)}
                          >
                            Giao lại ngay
                          </Button>
                        </HStack>

                        <Box mt={3}>
                          <SuggestionPreview suggestion={suggestion} />
                        </Box>
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>2) Duyệt/Từ chối yêu cầu hủy</Text>
                        <Textarea
                          size="sm"
                          value={managerDecisionNote}
                          onChange={(event) => setManagerDecisionNote(event.target.value)}
                          placeholder="Ghi chú cho quyết định của quản đốc..."
                        />
                        <HStack mt={3} spacing={2}>
                          <Button
                            size="sm"
                            colorScheme="green"
                            onClick={() => handleManagerCancelDecision(true)}
                            isLoading={saving}
                            isDisabled={!canManagerCancelDecision}
                          >
                            Đồng ý hủy
                          </Button>
                          <Button
                            size="sm"
                            colorScheme="red"
                            variant="outline"
                            onClick={() => handleManagerCancelDecision(false)}
                            isLoading={saving}
                            isDisabled={!canManagerCancelDecision}
                          >
                            Từ chối hủy
                          </Button>
                        </HStack>
                        {!selectedItem.isCancelPendingApproval && (
                          <Text mt={2} fontSize="sm" color="gray.500">Không có yêu cầu hủy chờ duyệt.</Text>
                        )}
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>3) Duyệt báo giá cuối cùng</Text>
                        <Textarea
                          size="sm"
                          value={managerFinalApprovalNote}
                          onChange={(event) => setManagerFinalApprovalNote(event.target.value)}
                          placeholder="Ghi chú duyệt cuối (ca lớn / yêu cầu đặc biệt)..."
                        />
                        <Button
                          mt={3}
                          size="sm"
                          colorScheme="green"
                          onClick={handleManagerFinalApproval}
                          isLoading={saving}
                          isDisabled={!canManagerFinalApprove}
                        >
                          Duyệt báo giá cuối
                        </Button>
                        {selectedItem.finalApprovedAt && (
                          <Text mt={2} fontSize="sm" color="green.600">Lệnh này đã được duyệt cuối lúc {formatDateTime(selectedItem.finalApprovedAt)}.</Text>
                        )}
                      </Box>
                    </VStack>
                  )}
                </VStack>
              )}
            </Box>
          </GridItem>
          )}
        </Grid>
      </VStack>
    </Box>
  );
}
