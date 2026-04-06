import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  AlertIcon,
  Badge,
  Box,
  Button,
  Divider,
  FormControl,
  FormLabel,
  Grid,
  GridItem,
  Heading,
  HStack,
  Input,
  Select,
  SimpleGrid,
  Spinner,
  Stack,
  Stat,
  StatLabel,
  StatNumber,
  Switch,
  Text,
  Textarea,
  VStack,
  useToast,
} from "@chakra-ui/react";
import { RepeatIcon } from "@chakra-ui/icons";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
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

const STATUS_TAB_META = {
  WHITE: {
    label: "Mới tiếp nhận",
    description: "Chờ tổ trưởng nhận xe và phân công",
    tabBg: COLOR_META.WHITE.tabBg,
    tabBorder: COLOR_META.WHITE.tabBorder,
    tabText: COLOR_META.WHITE.tabText,
    activeBg: COLOR_META.WHITE.activeBg,
    activeText: COLOR_META.WHITE.activeText,
  },
  IN_PROGRESS: {
    label: "Đang xử lý",
    description: "Gộp Đang chuẩn đoán và Cần can thiệp",
    tabBg: "#fff6e8",
    tabBorder: "#f6ad55",
    tabText: "#9c4221",
    activeBg: "#dd6b20",
    activeText: "white",
  },
  GREEN: {
    label: "Đã chuẩn đoán",
    description: "Đã có kết quả kỹ thuật để duyệt",
    tabBg: COLOR_META.GREEN.tabBg,
    tabBorder: COLOR_META.GREEN.tabBorder,
    tabText: COLOR_META.GREEN.tabText,
    activeBg: COLOR_META.GREEN.activeBg,
    activeText: COLOR_META.GREEN.activeText,
  },
};

const STATUS_TAB_ORDER = ["WHITE", "IN_PROGRESS", "GREEN"];

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

const EMPTY_NOTE_TOKENS = new Set([
  "khong",
  "khong co",
  "khong co ghi chu",
  "khong co thong tin",
  "none",
  "n/a",
  "na",
  "null",
  "undefined",
  "-",
  "--",
]);

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

function normalizeNoteText(value) {
  return toText(value)
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function isEmptyNote(value) {
  const normalized = normalizeNoteText(value);
  return !normalized || EMPTY_NOTE_TOKENS.has(normalized);
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

function getDispatchTagLabel(tag) {
  const code = normalizeCode(tag);
  if (!code) return "Chưa phân loại";
  return DISPATCH_TAG_LABELS[code] || code;
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

function formatAdditionalDescription(value) {
  const raw = toText(value);
  if (!raw) return "Không có";

  const collected = [];
  const collect = (source) => {
    if (source === null || source === undefined) return;

    if (Array.isArray(source)) {
      source.forEach((item) => collect(item));
      return;
    }

    if (typeof source === "object") {
      ["additionalDescription", "additionalNote", "description", "note", "notes", "customerNotes", "symptom", "symptoms", "message", "text"].forEach((key) => {
        collect(source[key]);
      });
      return;
    }

    const text = toText(source);
    if (text) collected.push(text);
  };

  try {
    collect(JSON.parse(raw));
  } catch {
    collect(raw);
  }

  const unique = [];
  const seen = new Set();
  collected.forEach((item) => {
    const key = normalizeNoteText(item);
    if (!key || isEmptyNote(key) || seen.has(key)) return;
    seen.add(key);
    unique.push(item);
  });

  return unique.length > 0 ? unique.join(", ") : "Không có";
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

function normalizeBoardItem(raw, orderMap, customerMap, vehicleMap) {
  const serviceOrderId = toPositiveNumber(readValue(raw, "serviceOrderId", "ServiceOrderId")) || 0;
  const order = orderMap.get(serviceOrderId) || null;

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
    teamId: toPositiveNumber(readValue(raw, "teamId", "TeamId")),
    teamName: toText(readValue(raw, "teamName", "TeamName")),
    technicianId: toPositiveNumber(readValue(raw, "technicianId", "TechnicianId")),
    technicianName: toText(readValue(raw, "technicianName", "TechnicianName")),
    assistantTechnicianId: toPositiveNumber(readValue(raw, "assistantTechnicianId", "AssistantTechnicianId")),
    assistantTechnicianName: toText(readValue(raw, "assistantTechnicianName", "AssistantTechnicianName")),
    assignedBayId: toPositiveNumber(readValue(raw, "assignedBayId", "AssignedBayId")),
    assignedBayCode: toText(readValue(raw, "assignedBayCode", "AssignedBayCode")),
    requestedAt: readValue(raw, "requestedAt", "RequestedAt"),
    assignedAt: readValue(raw, "assignedAt", "AssignedAt"),
    startedAt: readValue(raw, "startedAt", "StartedAt"),
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
    customerNotes: toText(readValue(order, "customerNotes", "CustomerNotes")),
  };
}
function DispatchQueueCard({ item, isSelected, onSelect }) {
  const meta = COLOR_META[item.queueColor] || COLOR_META.WHITE;
  const alerts = extractAlertReasons(item);

  return (
    <Box
      border="1px solid"
      borderColor={isSelected ? "blue.300" : "gray.100"}
      borderLeftWidth="4px"
      borderLeftColor={meta.cardAccent}
      borderRadius="xl"
      bg={isSelected ? "#eef4ff" : meta.cardBg}
      p={3}
      cursor="pointer"
      transition="all 0.18s ease"
      boxShadow={isSelected ? "0 10px 24px rgba(45, 76, 138, 0.14)" : "0 3px 10px rgba(15,23,42,0.05)"}
      _hover={{ borderColor: "blue.200", transform: "translateY(-1px)" }}
      onClick={() => onSelect(item.serviceOrderId)}
    >
      <Stack spacing={2}>
        <HStack justify="space-between" align="start" spacing={3}>
          <Box minW={0}>
            <HStack spacing={2} flexWrap="wrap">
              <Text fontWeight="800" color="gray.800" noOfLines={1}>
                {item.serviceOrderCode}
              </Text>
              <Badge colorScheme="purple" borderRadius="full" px={2}>
                {getDispatchTagLabel(item.dispatchTag)}
              </Badge>
              <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2}>
                {item.queueState || "--"}
              </Badge>
            </HStack>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              {item.customerName || "Khách hàng chưa rõ"} - {item.vehicleLicensePlate || "--"}
            </Text>
          </Box>

          <VStack align="end" spacing={1} minW="130px">
            <Badge colorScheme="gray" borderRadius="full" px={2.5}>
              Chờ: {formatMinutes(item.pendingMinutes)}
            </Badge>
            <Badge colorScheme="orange" borderRadius="full" px={2.5}>
              Xử lý: {formatMinutes(item.assignedMinutes)}
            </Badge>
          </VStack>
        </HStack>

        <Grid templateColumns={{ base: "1fr", md: "1fr 1fr" }} gap={2}>
          <Text fontSize="sm" color="gray.600" noOfLines={1}>
            Tổ: {item.teamName || "Chưa giao tổ"}
          </Text>
          <Text fontSize="sm" color="gray.600" noOfLines={1}>
            Cầu nâng: {item.assignedBayCode || "Chưa chỉ định"}
          </Text>
          <Text fontSize="sm" color="gray.600" noOfLines={1}>
            Thợ chính: {item.technicianName || "Chưa phân công"}
          </Text>
          <Text fontSize="sm" color="gray.600" noOfLines={1}>
            Thợ phụ: {item.assistantTechnicianName || "--"}
          </Text>
        </Grid>

        {alerts.length > 0 ? (
          <VStack align="start" spacing={1}>
            {alerts.slice(0, 2).map((reason) => (
              <Text key={`${item.serviceOrderId}-${reason}`} fontSize="sm" color="red.500" noOfLines={2}>
                - {reason}
              </Text>
            ))}
          </VStack>
        ) : (
          <Text fontSize="sm" color="gray.400">
            Không có cảnh báo
          </Text>
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

export default function DispatchOpsDashboard({ mode = "manager" }) {
  const toast = useToast();
  const { user } = useAuth();
  const { selectedBranchId } = useBranch();
  const { searchQuery } = useSearch();

  const isTeamLeaderMode = mode === "team_leader";

  const [boardItems, setBoardItems] = useState([]);
  const [teams, setTeams] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [roles, setRoles] = useState([]);
  const [bays, setBays] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [includeCompleted, setIncludeCompleted] = useState(false);
  const [teamFilter, setTeamFilter] = useState("");
  const [dispatchTagFilter, setDispatchTagFilter] = useState("");
  const [activeColorTab, setActiveColorTab] = useState("WHITE");
  const [selectedServiceOrderId, setSelectedServiceOrderId] = useState(null);

  const [leaderTeamId, setLeaderTeamId] = useState("");
  const [leaderLeadTechnicianId, setLeaderLeadTechnicianId] = useState("");
  const [leaderAssistantTechnicianId, setLeaderAssistantTechnicianId] = useState("");
  const [leaderBayId, setLeaderBayId] = useState("");
  const [leaderSupportNote, setLeaderSupportNote] = useState("");
  const [leaderCancelReason, setLeaderCancelReason] = useState(CANCEL_REASONS[0]);
  const [leaderCancelNote, setLeaderCancelNote] = useState("");
  const [leaderTechnicalApprovalNote, setLeaderTechnicalApprovalNote] = useState("");

  const [managerTeamId, setManagerTeamId] = useState("");
  const [managerLeadTechnicianId, setManagerLeadTechnicianId] = useState("");
  const [managerAssistantTechnicianId, setManagerAssistantTechnicianId] = useState("");
  const [managerBayId, setManagerBayId] = useState("");
  const [managerOverrideReason, setManagerOverrideReason] = useState("");
  const [managerDecisionNote, setManagerDecisionNote] = useState("");
  const [managerFinalApprovalNote, setManagerFinalApprovalNote] = useState("");

  const [suggestion, setSuggestion] = useState(null);
  const [suggestionLoading, setSuggestionLoading] = useState(false);

  const selectedBranchRaw = Number(selectedBranchId);
  const isAllBranchesSelected = Number.isFinite(selectedBranchRaw) && selectedBranchRaw === 0;
  const selectedBranch = toPositiveNumber(selectedBranchRaw);
  const userBranchId = toPositiveNumber(readValue(user, "branchId", "BranchId"));
  const branchId = isAllBranchesSelected ? null : selectedBranch || userBranchId;

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

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");

    const boardParams = { includeCompleted };
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

      const normalized = (Array.isArray(boardRes) ? boardRes : [])
        .map((item) => normalizeBoardItem(item, orderMap, customerMap, vehicleMap))
        .filter((item) => item.serviceOrderId > 0);

      setBoardItems(normalized);
    } catch (err) {
      setError(resolveApiErrorMessage(err, "Không thể tải bảng điều phối."));
    } finally {
      setLoading(false);
    }
  }, [branchId, includeCompleted, teamFilter]);

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

  const visibleItems = useMemo(() => {
    const query = toText(searchQuery).toLowerCase();
    const tagFilterCode = normalizeCode(dispatchTagFilter);

    return boardItems.filter((item) => {
      if (tagFilterCode && normalizeCode(item.dispatchTag) !== tagFilterCode) return false;

      if (isTeamLeaderMode && currentStaffTeamId) {
        if (item.teamId === currentStaffTeamId) {
          // pass
        } else if (!item.teamId) {
          if (item.queueColor !== "WHITE" && item.queueColor !== "RED") return false;
          if (currentTeamSpecialty && item.dispatchTag && normalizeCode(item.dispatchTag) !== currentTeamSpecialty) return false;
        } else {
          return false;
        }
      }

      if (!query) return true;

      const haystack = [
        item.serviceOrderCode,
        item.customerName,
        item.vehicleLicensePlate,
        item.teamName,
        item.technicianName,
        item.assistantTechnicianName,
        getDispatchTagLabel(item.dispatchTag),
      ]
        .map((value) => toText(value).toLowerCase())
        .join(" ");

      return haystack.includes(query);
    });
  }, [boardItems, searchQuery, dispatchTagFilter, isTeamLeaderMode, currentStaffTeamId, currentTeamSpecialty]);

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
      total: visibleItems.length,
    }),
    [boardByColor, visibleItems.length]
  );

  const tabCounts = useMemo(
    () => ({
      WHITE: boardByColor.WHITE.length,
      IN_PROGRESS: boardByColor.YELLOW.length + boardByColor.RED.length,
      GREEN: boardByColor.GREEN.length,
    }),
    [boardByColor]
  );

  const activeTabItems = useMemo(() => {
    if (activeColorTab === "IN_PROGRESS") {
      return [...boardByColor.RED, ...boardByColor.YELLOW];
    }
    return boardByColor[activeColorTab] || [];
  }, [activeColorTab, boardByColor]);

  const selectedItem = useMemo(() => {
    const targetId = toPositiveNumber(selectedServiceOrderId);
    if (!targetId) return null;
    return visibleItems.find((item) => item.serviceOrderId === targetId) || null;
  }, [visibleItems, selectedServiceOrderId]);

  useEffect(() => {
    if (activeTabItems.length === 0) {
      setSelectedServiceOrderId(null);
      return;
    }

    if (!selectedItem) {
      setSelectedServiceOrderId(activeTabItems[0].serviceOrderId);
    }
  }, [activeTabItems, selectedItem]);
  useEffect(() => {
    if (activeTabItems.length > 0) return;
    const firstAvailable = STATUS_TAB_ORDER.find((tabKey) => (tabCounts[tabKey] || 0) > 0);
    if (firstAvailable) {
      setActiveColorTab(firstAvailable);
    }
  }, [activeTabItems.length, tabCounts]);

  useEffect(() => {
    if (!selectedItem) return;

    if (isTeamLeaderMode) {
      const nextTeamId = toPositiveNumber(selectedItem.teamId) || currentStaffTeamId || toPositiveNumber(leaderTeamId);
      setLeaderTeamId(nextTeamId ? String(nextTeamId) : "");
      setLeaderLeadTechnicianId(selectedItem.technicianId ? String(selectedItem.technicianId) : "");
      setLeaderAssistantTechnicianId(selectedItem.assistantTechnicianId ? String(selectedItem.assistantTechnicianId) : "");
      setLeaderBayId(selectedItem.assignedBayId ? String(selectedItem.assignedBayId) : "");
      return;
    }

    setManagerTeamId(selectedItem.teamId ? String(selectedItem.teamId) : "");
    setManagerLeadTechnicianId(selectedItem.technicianId ? String(selectedItem.technicianId) : "");
    setManagerAssistantTechnicianId(selectedItem.assistantTechnicianId ? String(selectedItem.assistantTechnicianId) : "");
    setManagerBayId(selectedItem.assignedBayId ? String(selectedItem.assignedBayId) : "");
  }, [selectedItem?.serviceOrderId]);

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
    const bayId = toPositiveNumber(leaderBayId);

    if (!leadId || !teamId) {
      toast({ title: "Vui lòng chọn tổ và thợ chính", status: "warning", duration: 2600, isClosable: true });
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
    const bayId = toPositiveNumber(managerBayId);

    if (!leadId) {
      toast({ title: "Vui lòng chọn thợ chính", status: "warning", duration: 2500, isClosable: true });
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
              Theo dõi 4 giai đoạn từ tiếp nhận, phân công đến phê duyệt kỹ thuật và báo giá.
            </Text>
          </Box>

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

        <SimpleGrid columns={{ base: 2, lg: 5 }} spacing={3}>
          <Stat bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={3}>
            <StatLabel color="gray.500">Tổng lệnh</StatLabel>
            <StatNumber color="#1b2559">{totalStats.total}</StatNumber>
          </Stat>
          {COLUMN_ORDER.map((color) => (
            <Stat key={color} bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={3}>
              <StatLabel color="gray.500">{COLOR_META[color].label}</StatLabel>
              <StatNumber color="#1b2559">{totalStats[color]}</StatNumber>
            </Stat>
          ))}
        </SimpleGrid>

        <Grid templateColumns={{ base: "1fr", xl: "1fr auto auto" }} gap={3} bg="white" borderRadius="xl" border="1px solid" borderColor="gray.100" p={3}>
          <FormControl maxW={{ base: "100%", xl: "300px" }}>
            <FormLabel fontSize="sm" mb={1} color="gray.600">Lọc theo tổ</FormLabel>
            <Select
              size="sm"
              borderRadius="xl"
              value={teamFilter}
              onChange={(event) => setTeamFilter(event.target.value)}
              isDisabled={isTeamLeaderMode && !!currentStaffTeamId}
            >
              <option value="">Tất cả tổ</option>
              {activeTeams.map((team) => {
                const id = toPositiveNumber(readValue(team, "id", "Id"));
                const name = toText(readValue(team, "name", "Name"));
                const specialty = getDispatchTagLabel(readValue(team, "specialtyTag", "SpecialtyTag"));
                return (
                  <option key={`team-filter-${id}`} value={id || ""}>
                    {name || `Tổ #${id}`} - {specialty}
                  </option>
                );
              })}
            </Select>
          </FormControl>

          <FormControl maxW={{ base: "100%", xl: "280px" }}>
            <FormLabel fontSize="sm" mb={1} color="gray.600">Lọc theo chuyên môn</FormLabel>
            <Select
              size="sm"
              borderRadius="xl"
              value={dispatchTagFilter}
              onChange={(event) => setDispatchTagFilter(event.target.value)}
            >
              <option value="">Tất cả chuyên môn</option>
              {DISPATCH_TAG_OPTIONS.map((item) => (
                <option key={`tag-filter-${item.value}`} value={item.value}>
                  {item.label}
                </option>
              ))}
            </Select>
          </FormControl>

          <FormControl display="flex" alignItems="center" gap={2} justifyContent={{ base: "start", xl: "end" }}>
            <FormLabel mb="0" fontSize="sm" color="gray.600">Hiển thị cả GREEN</FormLabel>
            <Switch colorScheme="green" isChecked={includeCompleted} onChange={(event) => setIncludeCompleted(event.target.checked)} />
          </FormControl>
        </Grid>

        {error && (
          <Alert status="error" borderRadius="xl">
            <AlertIcon />
            {error}
          </Alert>
        )}

        <Grid templateColumns={{ base: "1fr", xl: "2fr 1fr" }} gap={4} alignItems="start">
          <GridItem>
            <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" overflow="hidden" boxShadow="0 14px 32px rgba(15,23,42,0.08)">
              <HStack spacing={2} p={3} overflowX="auto" bg="gray.50" borderBottom="1px solid" borderColor="gray.100">
                {STATUS_TAB_ORDER.map((tabKey) => {
                  const meta = STATUS_TAB_META[tabKey];
                  const isActive = activeColorTab === tabKey;
                  const count = tabCounts[tabKey] || 0;
                  return (
                    <Button
                      key={`tab-${tabKey}`}
                      size="sm"
                      borderRadius="full"
                      border="1px solid"
                      borderColor={meta.tabBorder}
                      bg={isActive ? meta.activeBg : meta.tabBg}
                      color={isActive ? meta.activeText : meta.tabText}
                      _hover={{ filter: "brightness(0.98)" }}
                      onClick={() => setActiveColorTab(tabKey)}
                    >
                      {meta.label} ({count})
                    </Button>
                  );
                })}
              </HStack>

              <Box px={4} py={2} borderBottom="1px solid" borderColor="gray.100" bg="white">
                <Text fontSize="sm" fontWeight="700" color="gray.700">
                  {(STATUS_TAB_META[activeColorTab] || STATUS_TAB_META.WHITE).label}
                </Text>
                <Text fontSize="sm" color="gray.500">
                  {(STATUS_TAB_META[activeColorTab] || STATUS_TAB_META.WHITE).description}
                </Text>
              </Box>

              {loading ? (
                <VStack py={12} spacing={3}>
                  <Spinner color="blue.500" />
                  <Text color="gray.500">Đang tải dữ liệu điều phối...</Text>
                </VStack>
              ) : activeTabItems.length === 0 ? (
                <Box py={12} textAlign="center" color="gray.400">
                  Không có lệnh trong nhóm này
                </Box>
              ) : (
                <VStack align="stretch" spacing={2} p={3}>
                  {activeTabItems.map((item) => (
                    <DispatchQueueCard
                      key={`dispatch-${item.serviceOrderId}`}
                      item={item}
                      isSelected={item.serviceOrderId === selectedItem?.serviceOrderId}
                      onSelect={setSelectedServiceOrderId}
                    />
                  ))}
                </VStack>
              )}
            </Box>
          </GridItem>

          <GridItem>
            <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" p={4} boxShadow="0 14px 32px rgba(15,23,42,0.08)">
              {!selectedItem ? (
                <Box border="1px dashed" borderColor="gray.200" borderRadius="xl" p={8} textAlign="center" color="gray.500">
                  Chọn một lệnh ở bên trái để xem chi tiết và thao tác.
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
                      <Badge colorScheme="purple" borderRadius="full">{getDispatchTagLabel(selectedItem.dispatchTag)}</Badge>
                      <Badge colorScheme="blue" borderRadius="full">{selectedItem.queueState || "--"}</Badge>
                    </HStack>
                  </Box>

                  <Stack spacing={2} fontSize="sm" color="gray.700">
                    <Text><strong>Khách hàng:</strong> {selectedItem.customerName || "--"}</Text>
                    <Text><strong>Biển số:</strong> {selectedItem.vehicleLicensePlate || "--"}</Text>
                    <Text><strong>Tổ phụ trách:</strong> {selectedItem.teamName || "Chưa giao tổ"}</Text>
                    <Text><strong>Thợ chính:</strong> {selectedItem.technicianName || "Chưa phân công"}</Text>
                    <Text><strong>Thợ phụ:</strong> {selectedItem.assistantTechnicianName || "--"}</Text>
                    <Text><strong>Cầu nâng:</strong> {selectedItem.assignedBayCode || "Chưa chỉ định"}</Text>
                    <Text><strong>Ghi chú khách:</strong> {formatAdditionalDescription(selectedItem.customerNotes)}</Text>
                  </Stack>

                  <Box p={3} border="1px solid" borderColor="blue.100" borderRadius="xl" bg="#f8fbff">
                    <Text fontSize="sm" color="gray.600">Yêu cầu lúc: {formatDateTime(selectedItem.requestedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Nhận ca lúc: {formatDateTime(selectedItem.assignedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Bắt đầu lúc: {formatDateTime(selectedItem.startedAt)}</Text>
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
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={leaderTeamId}
                            onChange={(event) => setLeaderTeamId(event.target.value)}
                            isDisabled={Boolean(currentStaffTeamId)}
                          >
                            <option value="">-- Chọn tổ --</option>
                            {activeTeams.map((team) => {
                              const id = toPositiveNumber(readValue(team, "id", "Id"));
                              const name = toText(readValue(team, "name", "Name"));
                              return (
                                <option key={`leader-team-${id}`} value={id || ""}>
                                  {name || `Tổ #${id}`}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ chính</FormLabel>
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={leaderLeadTechnicianId}
                            onChange={(event) => setLeaderLeadTechnicianId(event.target.value)}
                          >
                            <option value="">-- Chọn thợ chính --</option>
                            {leaderTechnicianOptions.map((staff) => {
                              const id = toPositiveNumber(readValue(staff, "id", "Id"));
                              const name = toText(readValue(staff, "fullName", "FullName"));
                              return (
                                <option key={`leader-main-${id}`} value={id || ""}>
                                  {name || `Nhân sự #${id}`}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ phụ</FormLabel>
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={leaderAssistantTechnicianId}
                            onChange={(event) => setLeaderAssistantTechnicianId(event.target.value)}
                          >
                            <option value="">-- Không chọn --</option>
                            {leaderTechnicianOptions.map((staff) => {
                              const id = toPositiveNumber(readValue(staff, "id", "Id"));
                              const name = toText(readValue(staff, "fullName", "FullName"));
                              return (
                                <option key={`leader-assistant-${id}`} value={id || ""}>
                                  {name || `Nhân sự #${id}`}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Cầu nâng</FormLabel>
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={leaderBayId}
                            onChange={(event) => setLeaderBayId(event.target.value)}
                          >
                            <option value="">-- Chưa chỉ định cầu --</option>
                            {availableBays.map((bay) => {
                              const id = toPositiveNumber(readValue(bay, "id", "Id"));
                              const code = toText(readValue(bay, "code", "Code")) || toText(readValue(bay, "name", "Name")) || `Cầu #${id}`;
                              const occupied = Boolean(readValue(bay, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
                              const operational = isOperationalBayAvailable(readValue(bay, "operationalStatus", "OperationalStatus"));
                              const disabled = occupied || !operational;
                              const statusText = disabled ? "(Đang bận)" : "(Trống)";
                              return (
                                <option key={`leader-bay-${id}`} value={id || ""} disabled={disabled}>
                                  {code} {statusText}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <HStack mt={3} spacing={2}>
                          <Button
                            size="sm"
                            colorScheme="blue"
                            variant="outline"
                            onClick={() => loadDispatchSuggestion("team_leader")}
                            isLoading={suggestionLoading}
                            isDisabled={!selectedItem || !toPositiveNumber(leaderTeamId)}
                          >
                            Gợi ý tự động
                          </Button>
                          <Button
                            size="sm"
                            colorScheme="blue"
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
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={leaderCancelReason}
                            onChange={(event) => setLeaderCancelReason(event.target.value)}
                          >
                            {CANCEL_REASONS.map((reason) => (
                              <option key={`cancel-reason-${reason}`} value={reason}>{reason}</option>
                            ))}
                          </Select>
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
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={managerTeamId}
                            onChange={(event) => setManagerTeamId(event.target.value)}
                          >
                            <option value="">-- Chọn tổ --</option>
                            {activeTeams.map((team) => {
                              const id = toPositiveNumber(readValue(team, "id", "Id"));
                              const name = toText(readValue(team, "name", "Name"));
                              const specialty = getDispatchTagLabel(readValue(team, "specialtyTag", "SpecialtyTag"));
                              return (
                                <option key={`manager-team-${id}`} value={id || ""}>
                                  {name || `Tổ #${id}`} - {specialty}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ chính</FormLabel>
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={managerLeadTechnicianId}
                            onChange={(event) => setManagerLeadTechnicianId(event.target.value)}
                          >
                            <option value="">-- Chọn thợ chính --</option>
                            {managerTechnicianOptions.map((staff) => {
                              const id = toPositiveNumber(readValue(staff, "id", "Id"));
                              const name = toText(readValue(staff, "fullName", "FullName"));
                              return (
                                <option key={`manager-main-${id}`} value={id || ""}>
                                  {name || `Nhân sự #${id}`}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ phụ</FormLabel>
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={managerAssistantTechnicianId}
                            onChange={(event) => setManagerAssistantTechnicianId(event.target.value)}
                          >
                            <option value="">-- Không chọn --</option>
                            {managerTechnicianOptions.map((staff) => {
                              const id = toPositiveNumber(readValue(staff, "id", "Id"));
                              const name = toText(readValue(staff, "fullName", "FullName"));
                              return (
                                <option key={`manager-assistant-${id}`} value={id || ""}>
                                  {name || `Nhân sự #${id}`}
                                </option>
                              );
                            })}
                          </Select>
                        </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Cầu nâng</FormLabel>
                          <Select
                            size="sm"
                            borderRadius="xl"
                            value={managerBayId}
                            onChange={(event) => setManagerBayId(event.target.value)}
                          >
                            <option value="">-- Chưa chỉ định cầu --</option>
                            {availableBays.map((bay) => {
                              const id = toPositiveNumber(readValue(bay, "id", "Id"));
                              const code = toText(readValue(bay, "code", "Code")) || toText(readValue(bay, "name", "Name")) || `Cầu #${id}`;
                              const occupied = Boolean(readValue(bay, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
                              const operational = isOperationalBayAvailable(readValue(bay, "operationalStatus", "OperationalStatus"));
                              const disabled = occupied || !operational;
                              return (
                                <option key={`manager-bay-${id}`} value={id || ""} disabled={disabled}>
                                  {code} {disabled ? "(Đang bận)" : "(Trống)"}
                                </option>
                              );
                            })}
                          </Select>
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

                        <HStack mt={3} spacing={2}>
                          <Button
                            size="sm"
                            colorScheme="blue"
                            variant="outline"
                            onClick={() => loadDispatchSuggestion("manager")}
                            isLoading={suggestionLoading}
                            isDisabled={!selectedItem || !toPositiveNumber(managerTeamId)}
                          >
                            Gợi ý tự động
                          </Button>
                          <Button
                            size="sm"
                            colorScheme="blue"
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
        </Grid>
      </VStack>
    </Box>
  );
}
