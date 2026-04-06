
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
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import FilterDropdown from "@shared/components/FilterDropdown";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useSearch } from "@shared/core/context/SearchContext";

const COLOR_META = {
  WHITE: {
    label: "WHITE - Chờ tiếp nhận",
    helper: "Xe mới đang chờ tổ trưởng nhận",
    badge: "gray",
    borderColor: "#94a3b8",
  },
  YELLOW: {
    label: "YELLOW - Đang chẩn đoán",
    helper: "Đã có tổ nhận và đang xử lý",
    badge: "yellow",
    borderColor: "#d69e2e",
  },
  RED: {
    label: "RED - Cảnh báo",
    helper: "Cần can thiệp quản đốc / hỗ trợ kỹ thuật",
    badge: "red",
    borderColor: "#e53e3e",
  },
  GREEN: {
    label: "GREEN - Đã chẩn đoán",
    helper: "Đã có kết quả chẩn đoán",
    badge: "green",
    borderColor: "#38a169",
  },
};

const STATUS_TONE = {
  WHITE: {
    tabBg: "gray.100",
    tabBorder: "gray.300",
    tabText: "gray.700",
    activeBg: "#1f2937",
    activeText: "white",
    rowBg: "gray.50",
    rowHover: "gray.100",
  },
  YELLOW: {
    tabBg: "#fff7db",
    tabBorder: "#f6d365",
    tabText: "#7b5d00",
    activeBg: "#d69e2e",
    activeText: "white",
    rowBg: "#fffbeb",
    rowHover: "#fef3c7",
  },
  RED: {
    tabBg: "#fff1f2",
    tabBorder: "#fca5a5",
    tabText: "#b91c1c",
    activeBg: "#e53e3e",
    activeText: "white",
    rowBg: "#fff5f5",
    rowHover: "#fee2e2",
  },
  GREEN: {
    tabBg: "#ecfdf3",
    tabBorder: "#86efac",
    tabText: "#166534",
    activeBg: "#38a169",
    activeText: "white",
    rowBg: "#f0fdf4",
    rowHover: "#dcfce7",
  },
};

const COLUMN_ORDER = ["WHITE", "YELLOW", "RED", "GREEN"];
const DEFAULT_CANCEL_REASON = "Sai chuyên môn";
const CANCEL_REASONS = ["Sai chuyên môn", "Quá tải cầu", "Thiếu thiết bị", "Khác"];
const DISPATCH_TAG_LABELS = {
  BODY_PAINT: "Đồng sơn / Chăm sóc xe",
  ELECTRICAL_AC: "Điện - Máy lạnh",
  ENGINE_CHASSIS: "Máy - Gầm",
  MAINTENANCE: "Bảo dưỡng nhanh",
};
const SHARED_SELECT_PROPS = {
  size: "sm",
  bg: "white",
  borderColor: "gray.200",
  borderRadius: "xl",
  focusBorderColor: "blue.400",
  iconColor: "gray.500",
  _hover: { borderColor: "blue.300" },
};

function readValue(source, ...keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null) return value;
  }
  return null;
}
function toText(value) {
  if (value === undefined || value === null) return "";
  return String(value).trim();
}

function normalizeCode(value) {
  return toText(value).toUpperCase();
}

function getDispatchTagLabel(tag) {
  const normalized = normalizeCode(tag);
  if (!normalized) return "Chưa phân loại";
  return DISPATCH_TAG_LABELS[normalized] || normalized;
}

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function parseDate(input) {
  if (!input) return null;
  const date = new Date(input);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatDateTime(input) {
  const date = parseDate(input);
  if (!date) return "--";
  return date.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatMinutes(minutes) {
  const value = Number(minutes);
  if (!Number.isFinite(value) || value <= 0) return "0 phút";
  if (value < 60) return `${Math.floor(value)} phút`;
  const hours = Math.floor(value / 60);
  const rest = Math.floor(value % 60);
  return rest > 0 ? `${hours} giờ ${rest} phút` : `${hours} giờ`;
}

function isTechnicianRole(role) {
  const roleCode = normalizeCode(readValue(role, "roleCode", "RoleCode"));
  const roleName = toText(readValue(role, "roleName", "RoleName")).toLowerCase();
  return (
    roleCode === "TECH" ||
    roleCode === "TECHNICIAN" ||
    roleCode === "TECH_LEAD" ||
    roleCode === "FOREMAN" ||
    roleName.includes("tech") ||
    roleName.includes("ky thuat") ||
    roleName.includes("kỹ thuật") ||
    roleName.includes("to truong") ||
    roleName.includes("tổ trưởng")
  );
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

  const userPhone = toText(readValue(user, "phone", "Phone")).replace(/\s+/g, "");
  if (userPhone) {
    const byPhone = staffs.find((item) => toText(readValue(item, "phone", "Phone")).replace(/\s+/g, "") === userPhone);
    if (byPhone) return byPhone;
  }

  const userName = toText(readValue(user, "fullName", "FullName")).toLowerCase();
  if (userName) {
    const byName = staffs.find((item) => toText(readValue(item, "fullName", "FullName")).toLowerCase() === userName);
    if (byName) return byName;
  }

  return null;
}

function extractAlertReasons(item) {
  const reasons = [];
  if (!item) return reasons;

  if (item.isSupportRequested) {
    reasons.push("Tổ trưởng đã bấm hỗ trợ/hội chẩn");
  }

  if (item.isCancelPendingApproval) {
    reasons.push("Đang chờ quản đốc duyệt hủy lệnh");
  }

  if (item.queueColor === "RED" && !item.technicianId && item.pendingMinutes >= 10) {
    reasons.push("Xe chờ nhận quá 10 phút");
  }

  if (item.queueColor === "RED" && item.technicianId && item.assignedMinutes >= 45) {
    reasons.push("Chẩn đoán quá 45 phút chưa xong");
  }

  return reasons;
}

function safeSortByName(list) {
  return [...list].sort((a, b) => {
    const aName = toText(readValue(a, "fullName", "FullName"));
    const bName = toText(readValue(b, "fullName", "FullName"));
    return aName.localeCompare(bName, "vi");
  });
}

function resolveApiErrorMessage(err, fallbackMessage) {
  const statusCode = Number(err?.response?.status || 0);
  if (statusCode === 404) {
    return "Không tìm thấy API tương ứng (404). Vui lòng kiểm tra backend đã cập nhật route mới.";
  }

  const backendMessage = toText(err?.response?.data?.message);
  if (backendMessage) return backendMessage;

  const clientMessage = toText(err?.message);
  if (clientMessage) return clientMessage;

  return fallbackMessage;
}

function normalizeBoardItem(raw, orderMap) {
  const serviceOrderId = toPositiveNumber(readValue(raw, "serviceOrderId", "ServiceOrderId")) || 0;
  const order = orderMap.get(serviceOrderId) || null;
  const serviceOrderCode =
    toText(readValue(raw, "serviceOrderCode", "ServiceOrderCode")) ||
    toText(readValue(order, "code", "Code")) ||
    `SO-${serviceOrderId}`;

  return {
    serviceOrderId,
    serviceOrderCode,
    serviceOrderStatus:
      toText(readValue(raw, "serviceOrderStatus", "ServiceOrderStatus")) ||
      toText(readValue(order, "status", "Status")),
    queueColor: normalizeCode(readValue(raw, "queueColor", "QueueColor")) || "WHITE",
    queueState: toText(readValue(raw, "queueState", "QueueState")),
    dispatchTag: normalizeCode(readValue(raw, "dispatchTag", "DispatchTag")),
    teamId: toPositiveNumber(readValue(raw, "teamId", "TeamId")),
    teamName: toText(readValue(raw, "teamName", "TeamName")),
    technicianId: toPositiveNumber(readValue(raw, "technicianId", "TechnicianId")),
    technicianName: toText(readValue(raw, "technicianName", "TechnicianName")),
    requestedAt: readValue(raw, "requestedAt", "RequestedAt"),
    assignedAt: readValue(raw, "assignedAt", "AssignedAt"),
    pendingMinutes: Number(readValue(raw, "pendingMinutes", "PendingMinutes") || 0),
    assignedMinutes: Number(readValue(raw, "assignedMinutes", "AssignedMinutes") || 0),
    isSupportRequested: Boolean(readValue(raw, "isSupportRequested", "IsSupportRequested")),
    isCancelPendingApproval: Boolean(readValue(raw, "isCancelPendingApproval", "IsCancelPendingApproval")),
    cancelReason: toText(readValue(raw, "cancelReason", "CancelReason")),
    customerName: toText(readValue(order, "customerName", "CustomerName")),
    vehicleLicensePlate: toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")),
    customerNotes: toText(readValue(order, "customerNotes", "CustomerNotes")),
  };
}
function DispatchRow({ color, item, selectedServiceOrderId, onSelect }) {
  const meta = COLOR_META[color] || COLOR_META.WHITE;
  const tone = STATUS_TONE[color] || STATUS_TONE.WHITE;
  const reasons = extractAlertReasons(item);
  const isSelected = Number(item.serviceOrderId) === Number(selectedServiceOrderId);

  return (
    <Box
      mx={3}
      my={2}
      border="1px solid"
      borderColor={isSelected ? "blue.300" : "gray.100"}
      borderLeftWidth="4px"
      borderLeftColor={meta.borderColor}
      borderRadius="xl"
      bg={isSelected ? tone.rowBg : "white"}
      boxShadow={isSelected ? "0 8px 22px rgba(30, 64, 175, 0.12)" : "0 2px 10px rgba(15, 23, 42, 0.04)"}
      cursor="pointer"
      transition="all 0.18s ease"
      _hover={{ bg: tone.rowHover, borderColor: "blue.200", transform: "translateY(-1px)" }}
      onClick={() => onSelect(item.serviceOrderId)}
    >
      <Grid
        templateColumns={{ base: "1fr", md: "1.2fr 1.2fr", xl: "1.2fr 1.2fr 1fr 0.9fr 1fr" }}
        gap={3}
        alignItems="start"
        px={{ base: 3, md: 4 }}
        py={3}
      >
        <Box minW={0}>
          <HStack spacing={2} mb={1} flexWrap="wrap">
            <Text fontWeight="800" color="gray.800" noOfLines={1}>
              {item.serviceOrderCode}
            </Text>
            <Badge variant="subtle" colorScheme="purple" borderRadius="full" px={2.5} py={1} fontSize="10px">
              {getDispatchTagLabel(item.dispatchTag)}
            </Badge>
          </HStack>
          <Text fontSize="xs" color="gray.500" noOfLines={1}>
            Trạng thái: {item.queueState || "--"}
          </Text>
        </Box>

        <Box minW={0}>
          <Text fontSize="sm" color="gray.700" fontWeight="700" noOfLines={1}>
            {item.customerName || "Khách hàng chưa rõ"}
          </Text>
          <Text fontSize="xs" color="gray.500" noOfLines={1}>
            Biển số: {item.vehicleLicensePlate || "--"}
          </Text>
        </Box>

        <Box minW={0}>
          <Text fontSize="xs" color="gray.500" noOfLines={1}>
            Tổ: {item.teamName || "Chưa giao tổ"}
          </Text>
          <Text fontSize="xs" color="gray.500" noOfLines={1}>
            Thợ: {item.technicianName || "Chưa nhận"}
          </Text>
        </Box>

        <VStack align="start" spacing={1}>
          <Badge variant="outline" colorScheme="gray" borderRadius="full" px={2.5}>
            Chờ: {formatMinutes(item.pendingMinutes)}
          </Badge>
          <Badge variant="outline" colorScheme="orange" borderRadius="full" px={2.5}>
            Xử lý: {formatMinutes(item.assignedMinutes)}
          </Badge>
        </VStack>

        <Box minW={0}>
          {reasons.length > 0 ? (
            <VStack align="stretch" spacing={1}>
              {reasons.slice(0, 2).map((reason) => (
                <Text key={`${item.serviceOrderId}-${reason}`} fontSize="xs" color="red.500" noOfLines={2}>
                  • {reason}
                </Text>
              ))}
            </VStack>
          ) : (
            <Text fontSize="xs" color="gray.400">
              Không có cảnh báo
            </Text>
          )}
        </Box>
      </Grid>
    </Box>
  );
}

export default function DispatchOpsDashboard({ mode = "manager" }) {
  const toast = useToast();
  const { user } = useAuth();
  const { selectedBranchId } = useBranch();
  const { searchQuery } = useSearch();

  const [boardItems, setBoardItems] = useState([]);
  const [orders, setOrders] = useState([]);
  const [teams, setTeams] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [roles, setRoles] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [includeCompleted, setIncludeCompleted] = useState(false);
  const [teamFilter, setTeamFilter] = useState("");
  const [dispatchTagFilter, setDispatchTagFilter] = useState("");
  const [activeColorTab, setActiveColorTab] = useState(COLUMN_ORDER[0]);
  const [selectedServiceOrderId, setSelectedServiceOrderId] = useState(null);

  const [foremanTechnicianId, setForemanTechnicianId] = useState("");
  const [supportNote, setSupportNote] = useState("");
  const [cancelReason, setCancelReason] = useState(DEFAULT_CANCEL_REASON);
  const [cancelNote, setCancelNote] = useState("");

  const [managerTeamId, setManagerTeamId] = useState("");
  const [managerTechnicianId, setManagerTechnicianId] = useState("");
  const [overrideReason, setOverrideReason] = useState("");
  const [decisionNote, setDecisionNote] = useState("");

  const contextBranchId = toPositiveNumber(selectedBranchId);
  const userBranchId = toPositiveNumber(readValue(user, "branchId", "BranchId"));
  const branchId = contextBranchId || userBranchId;

  const activeTeams = useMemo(() => {
    return [...(Array.isArray(teams) ? teams : [])]
      .filter((team) => {
        const isDeleted = readValue(team, "isDeleted", "IsDeleted");
        if (isDeleted === true) return false;
        if (branchId) {
          return toPositiveNumber(readValue(team, "branchId", "BranchId")) === branchId;
        }
        return true;
      })
      .sort((a, b) => {
        const nameA = toText(readValue(a, "name", "Name"));
        const nameB = toText(readValue(b, "name", "Name"));
        return nameA.localeCompare(nameB, "vi");
      });
  }, [teams, branchId]);

  const roleById = useMemo(() => {
    const map = new Map();
    (roles || []).forEach((role) => {
      const roleId = toPositiveNumber(readValue(role, "id", "Id"));
      if (roleId) map.set(roleId, role);
    });
    return map;
  }, [roles]);

  const activeStaffs = useMemo(() => {
    return (Array.isArray(staffs) ? staffs : []).filter((staff) => {
      const isDeleted = readValue(staff, "isDeleted", "IsDeleted");
      if (isDeleted === true) return false;
      const status = normalizeCode(readValue(staff, "status", "Status"));
      if (status === "INACTIVE" || status === "TERMINATED") return false;
      if (branchId) {
        return toPositiveNumber(readValue(staff, "branchId", "BranchId")) === branchId;
      }
      return true;
    });
  }, [staffs, branchId]);

  const currentStaff = useMemo(() => resolveCurrentStaff(activeStaffs, user), [activeStaffs, user]);
  const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id"));
  const foremanTeamId = toPositiveNumber(readValue(currentStaff, "teamId", "TeamId"));

  const technicians = useMemo(() => {
    return safeSortByName(
      activeStaffs.filter((staff) => {
        const roleId = toPositiveNumber(readValue(staff, "roleId", "RoleId"));
        const role = roleId ? roleById.get(roleId) : null;
        return isTechnicianRole(role);
      })
    );
  }, [activeStaffs, roleById]);

  const techniciansByTeam = useMemo(() => {
    const map = new Map();
    technicians.forEach((staff) => {
      const teamId = toPositiveNumber(readValue(staff, "teamId", "TeamId")) || 0;
      if (!map.has(teamId)) map.set(teamId, []);
      map.get(teamId).push(staff);
    });
    map.forEach((list, key) => {
      map.set(key, safeSortByName(list));
    });
    return map;
  }, [technicians]);
  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const branchQueryId = branchId || undefined;
      const [teamRes, staffRes, roleRes, orderRes] = await Promise.all([
        serviceOrderApi.getTeams({ branchId: branchQueryId, status: "ACTIVE" }).catch(() => serviceOrderApi.getTeams({ branchId: branchQueryId })),
        getAllStaff(branchQueryId),
        getAllRoles(),
        serviceOrderApi.getAll(),
      ]);

      const nextTeams = Array.isArray(teamRes) ? teamRes : [];
      const nextStaffs = Array.isArray(staffRes) ? staffRes : [];
      const nextRoles = Array.isArray(roleRes) ? roleRes : [];
      const nextOrders = Array.isArray(orderRes) ? orderRes : [];

      setTeams(nextTeams);
      setStaffs(nextStaffs);
      setRoles(nextRoles);
      setOrders(nextOrders);

      const staffInContext = resolveCurrentStaff(nextStaffs, user);
      const staffTeamId = toPositiveNumber(readValue(staffInContext, "teamId", "TeamId"));
      if (mode === "foreman" && !staffTeamId) {
        setBoardItems([]);
        return;
      }

      const boardParams = {
        includeCompleted: mode === "manager" ? includeCompleted : false,
      };

      if (branchQueryId) {
        boardParams.branchId = branchQueryId;
      }

      if (mode === "foreman" && staffTeamId) {
        boardParams.teamId = staffTeamId;
      }

      if (mode === "manager") {
        const selectedTeam = toPositiveNumber(teamFilter);
        if (selectedTeam) {
          boardParams.teamId = selectedTeam;
        }
      }

      const boardRes = await serviceOrderApi.getDiagnosisDispatchBoard(boardParams);
      setBoardItems(Array.isArray(boardRes) ? boardRes : []);
    } catch (err) {
      setError(resolveApiErrorMessage(err, "Không thể tải dữ liệu điều phối."));
    } finally {
      setLoading(false);
    }
  }, [branchId, includeCompleted, mode, teamFilter, user]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const orderById = useMemo(() => {
    const map = new Map();
    (orders || []).forEach((order) => {
      const id = toPositiveNumber(readValue(order, "id", "Id"));
      if (id) map.set(id, order);
    });
    return map;
  }, [orders]);

  const normalizedItems = useMemo(() => {
    return (Array.isArray(boardItems) ? boardItems : [])
      .map((item) => normalizeBoardItem(item, orderById))
      .filter((item) => item.serviceOrderId > 0);
  }, [boardItems, orderById]);

  const dispatchTagOptions = useMemo(() => {
    const set = new Set();
    activeTeams.forEach((team) => {
      const tag = normalizeCode(readValue(team, "specialtyTag", "SpecialtyTag"));
      if (tag) set.add(tag);
    });
    normalizedItems.forEach((item) => {
      if (item.dispatchTag) set.add(item.dispatchTag);
    });
    return [...set].sort((a, b) => a.localeCompare(b));
  }, [activeTeams, normalizedItems]);

  const teamFilterOptions = useMemo(() => {
    const base = [{ value: "", label: "Tất cả tổ" }];
    const dynamic = activeTeams.map((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      const tag = toText(readValue(team, "specialtyTag", "SpecialtyTag"));
      return {
        value: id ? String(id) : "",
        label: `${name || `Tổ #${id}`}${tag ? ` (${getDispatchTagLabel(tag)})` : ""}`,
      };
    });
    return [...base, ...dynamic];
  }, [activeTeams]);

  const dispatchTagFilterOptions = useMemo(() => {
    const base = [{ value: "", label: "Tất cả chuyên môn" }];
    const dynamic = dispatchTagOptions.map((tag) => ({
      value: tag,
      label: getDispatchTagLabel(tag),
    }));
    return [...base, ...dynamic];
  }, [dispatchTagOptions]);

  const filteredItems = useMemo(() => {
    const query = toText(searchQuery).toLowerCase();
    const tag = normalizeCode(dispatchTagFilter);

    return normalizedItems.filter((item) => {
      if (tag && item.dispatchTag !== tag) return false;

      if (!query) return true;
      const haystack = [
        item.serviceOrderCode,
        item.customerName,
        item.vehicleLicensePlate,
        item.teamName,
        item.technicianName,
        item.queueState,
        item.dispatchTag,
        getDispatchTagLabel(item.dispatchTag),
      ]
        .map((value) => toText(value).toLowerCase())
        .join(" ");
      return haystack.includes(query);
    });
  }, [dispatchTagFilter, normalizedItems, searchQuery]);

  const groupedByColor = useMemo(() => {
    const groups = {
      WHITE: [],
      YELLOW: [],
      RED: [],
      GREEN: [],
    };

    filteredItems.forEach((item) => {
      const color = COLUMN_ORDER.includes(item.queueColor) ? item.queueColor : "WHITE";
      groups[color].push(item);
    });

    COLUMN_ORDER.forEach((color) => {
      groups[color].sort((a, b) => {
        const urgencyB = (Number(b.pendingMinutes) || 0) + (Number(b.assignedMinutes) || 0);
        const urgencyA = (Number(a.pendingMinutes) || 0) + (Number(a.assignedMinutes) || 0);
        if (urgencyB !== urgencyA) return urgencyB - urgencyA;
        const bDate = parseDate(b.requestedAt) || parseDate(b.assignedAt);
        const aDate = parseDate(a.requestedAt) || parseDate(a.assignedAt);
        return (bDate?.getTime() || 0) - (aDate?.getTime() || 0);
      });
    });

    return groups;
  }, [filteredItems]);

  useEffect(() => {
    if (!filteredItems.length) {
      setSelectedServiceOrderId(null);
      return;
    }

    const tabItems = groupedByColor[activeColorTab] || [];
    if (!tabItems.length) {
      setSelectedServiceOrderId(null);
      return;
    }

    const stillExists = tabItems.some((item) => Number(item.serviceOrderId) === Number(selectedServiceOrderId));
    if (!stillExists) {
      setSelectedServiceOrderId(tabItems[0].serviceOrderId);
    }
  }, [activeColorTab, filteredItems, groupedByColor, selectedServiceOrderId]);

  const selectedItem = useMemo(() => {
    if (!selectedServiceOrderId) return null;
    return filteredItems.find((item) => Number(item.serviceOrderId) === Number(selectedServiceOrderId)) || null;
  }, [filteredItems, selectedServiceOrderId]);

  useEffect(() => {
    if (!selectedItem) {
      setForemanTechnicianId("");
      setManagerTeamId("");
      setManagerTechnicianId("");
      return;
    }

    setSupportNote("");
    setCancelReason(DEFAULT_CANCEL_REASON);
    setCancelNote("");
    setOverrideReason("");
    setDecisionNote("");

    const selectedTeamId = toPositiveNumber(selectedItem.teamId);

    if (mode === "foreman") {
      const currentTeam = foremanTeamId || selectedTeamId;
      const options = currentTeam ? techniciansByTeam.get(currentTeam) || [] : technicians;
      const assignedTech = toPositiveNumber(selectedItem.technicianId);
      if (assignedTech && options.some((staff) => toPositiveNumber(readValue(staff, "id", "Id")) === assignedTech)) {
        setForemanTechnicianId(String(assignedTech));
      } else if (options.length > 0) {
        const defaultTechId = toPositiveNumber(readValue(options[0], "id", "Id"));
        setForemanTechnicianId(defaultTechId ? String(defaultTechId) : "");
      } else {
        setForemanTechnicianId("");
      }
    }

    if (mode === "manager") {
      const teamId = selectedTeamId;
      if (teamId) {
        setManagerTeamId(String(teamId));
        const options = techniciansByTeam.get(teamId) || [];
        const assignedTech = toPositiveNumber(selectedItem.technicianId);
        if (assignedTech && options.some((staff) => toPositiveNumber(readValue(staff, "id", "Id")) === assignedTech)) {
          setManagerTechnicianId(String(assignedTech));
        } else if (options.length > 0) {
          const firstId = toPositiveNumber(readValue(options[0], "id", "Id"));
          setManagerTechnicianId(firstId ? String(firstId) : "");
        } else {
          setManagerTechnicianId("");
        }
      } else {
        setManagerTeamId("");
        const assignedTech = toPositiveNumber(selectedItem.technicianId);
        setManagerTechnicianId(assignedTech ? String(assignedTech) : "");
      }
    }
  }, [selectedItem, mode, foremanTeamId, techniciansByTeam, technicians]);

  const managerTechnicianOptions = useMemo(() => {
    const selectedTeam = toPositiveNumber(managerTeamId);
    if (selectedTeam) {
      return techniciansByTeam.get(selectedTeam) || [];
    }
    return technicians;
  }, [managerTeamId, techniciansByTeam, technicians]);

  const foremanTechnicianOptions = useMemo(() => {
    const teamId = foremanTeamId || toPositiveNumber(selectedItem?.teamId);
    if (teamId) {
      return techniciansByTeam.get(teamId) || [];
    }
    return technicians;
  }, [foremanTeamId, selectedItem, techniciansByTeam, technicians]);

  const counts = useMemo(() => {
    return COLUMN_ORDER.reduce((acc, color) => {
      acc[color] = groupedByColor[color]?.length || 0;
      return acc;
    }, {});
  }, [groupedByColor]);

  const activeTabItems = useMemo(() => groupedByColor[activeColorTab] || [], [activeColorTab, groupedByColor]);
  const activeTabMeta = COLOR_META[activeColorTab] || COLOR_META.WHITE;

  const runAction = useCallback(
    async (runner, successMessage) => {
      setSaving(true);
      try {
        await runner();
        toast({ title: successMessage, status: "success", duration: 3000, isClosable: true });
        await fetchData();
      } catch (err) {
        toast({
          title: "Thao tác thất bại",
          description: resolveApiErrorMessage(err, "Không thể xử lý yêu cầu."),
          status: "error",
          duration: 4500,
          isClosable: true,
        });
      } finally {
        setSaving(false);
      }
    },
    [fetchData, toast]
  );
  const handleForemanReceive = useCallback(async () => {
    if (!selectedItem) return;
    if (!currentStaffId) {
      toast({ title: "Không xác định được staff hiện tại", status: "warning", duration: 3000 });
      return;
    }

    const technicianId = toPositiveNumber(foremanTechnicianId);
    if (!technicianId) {
      toast({ title: "Cần chọn thợ phụ trách", status: "warning", duration: 2500 });
      return;
    }

    const teamId = foremanTeamId || toPositiveNumber(selectedItem.teamId);

    await runAction(
      () =>
        serviceOrderApi.acceptDiagnosisRequest(selectedItem.serviceOrderId, technicianId, {
          teamId: teamId || undefined,
          teamLeaderId: currentStaffId,
          isManagerOverride: false,
        }),
      "Đã nhận xe và phân công thợ"
    );
  }, [selectedItem, currentStaffId, foremanTechnicianId, foremanTeamId, runAction, toast]);

  const handleForemanSupport = useCallback(async () => {
    if (!selectedItem || !currentStaffId) return;

    await runAction(
      () =>
        serviceOrderApi.requestDiagnosisSupport(selectedItem.serviceOrderId, {
          requestedByStaffId: currentStaffId,
          note: toText(supportNote) || null,
        }),
      "Đã gửi yêu cầu hỗ trợ"
    );
  }, [selectedItem, currentStaffId, supportNote, runAction]);

  const cancelReasonPayload = useMemo(() => {
    const selectedReason = toText(cancelReason);
    const note = toText(cancelNote);
    if (selectedReason === "Khác") {
      return note;
    }
    return note ? `${selectedReason}: ${note}` : selectedReason;
  }, [cancelReason, cancelNote]);

  const handleForemanCancelRequest = useCallback(async () => {
    if (!selectedItem || !currentStaffId) return;

    const reason = toText(cancelReasonPayload);
    if (!reason) {
      toast({ title: "Cần nhập lý do hủy", status: "warning", duration: 2500 });
      return;
    }

    await runAction(
      () =>
        serviceOrderApi.requestDiagnosisCancel(selectedItem.serviceOrderId, {
          requestedByStaffId: currentStaffId,
          reason,
        }),
      "Đã gửi hủy lệnh cho quản đốc duyệt"
    );
  }, [selectedItem, currentStaffId, cancelReasonPayload, runAction, toast]);

  const handleManagerOverride = useCallback(async () => {
    if (!selectedItem || !currentStaffId) return;

    const technicianId = toPositiveNumber(managerTechnicianId);
    if (!technicianId) {
      toast({ title: "Cần chọn kỹ thuật viên", status: "warning", duration: 2500 });
      return;
    }

    const teamId = toPositiveNumber(managerTeamId);

    await runAction(
      () =>
        serviceOrderApi.overrideDiagnosisAssignment(selectedItem.serviceOrderId, {
          managerId: currentStaffId,
          technicianId,
          teamId: teamId || undefined,
          reason: toText(overrideReason) || null,
        }),
      "Đã override phân công"
    );
  }, [selectedItem, currentStaffId, managerTechnicianId, managerTeamId, overrideReason, runAction, toast]);

  const handleManagerCancelDecision = useCallback(
    async (approve) => {
      if (!selectedItem || !currentStaffId) return;

      await runAction(
        () =>
          serviceOrderApi.decideDiagnosisCancel(selectedItem.serviceOrderId, {
            managerId: currentStaffId,
            approve,
            note: toText(decisionNote) || null,
          }),
        approve ? "Đã đồng ý hủy lệnh" : "Đã từ chối hủy lệnh"
      );
    },
    [selectedItem, currentStaffId, decisionNote, runAction]
  );

  const showForemanWarning = mode === "foreman" && !foremanTeamId;
  const canForemanReceive = Boolean(selectedItem) && !saving;
  const canForemanSupport = Boolean(selectedItem?.technicianId) && !saving;
  const canForemanCancel = Boolean(selectedItem?.technicianId) && !selectedItem?.isCancelPendingApproval && !saving;
  const canManagerOverride = Boolean(selectedItem) && !saving;
  const canManagerDecide = Boolean(selectedItem?.isCancelPendingApproval) && !saving;

  const isCancelWindowOver = Number(selectedItem?.assignedMinutes || 0) > 10;

  return (
    <Box>
      <VStack align="stretch" spacing={6}>
        <Box
          bg="linear-gradient(135deg, #ffffff 0%, #f8fbff 100%)"
          border="1px solid"
          borderColor="blue.50"
          borderRadius="2xl"
          p={{ base: 4, md: 5 }}
          boxShadow="0 12px 32px rgba(15, 23, 42, 0.06)"
        >
          <HStack justify="space-between" align="start" spacing={6} flexWrap="wrap">
            <Box>
              <Heading size="md" color="gray.700">
                Điều phối chẩn đoán - {mode === "manager" ? "Quản đốc" : "Tổ trưởng"}
              </Heading>
              <Text mt={1} fontSize="sm" color="gray.500">
                Bảng vận hành theo màu White/Yellow/Red/Green và SOP 5 bước.
              </Text>
            </Box>
            <HStack spacing={3} align="end" flexWrap="wrap" bg="whiteAlpha.700" px={3} py={2} borderRadius="xl">
              {mode === "manager" && (
                <FilterDropdown
                  id="dispatch-team-filter"
                  label="Lọc theo tổ"
                  value={teamFilter}
                  options={teamFilterOptions}
                  onChange={setTeamFilter}
                  minWidth={220}
                />
              )}

              <FilterDropdown
                id="dispatch-specialty-filter"
                label="Lọc theo chuyên môn"
                value={dispatchTagFilter}
                options={dispatchTagFilterOptions}
                onChange={setDispatchTagFilter}
                minWidth={220}
              />

              {mode === "manager" && (
                <FormControl display="flex" alignItems="center" maxW="190px">
                  <FormLabel htmlFor="dispatch-include-completed" mb="0" fontSize="xs" color="gray.500">
                    Hiển thị đã xong
                  </FormLabel>
                  <Switch
                    id="dispatch-include-completed"
                    isChecked={includeCompleted}
                    onChange={(e) => setIncludeCompleted(e.target.checked)}
                  />
                </FormControl>
              )}

            </HStack>
          </HStack>

          {showForemanWarning && (
            <Alert status="warning" mt={4} borderRadius="md">
              <AlertIcon />
              Tài khoản tổ trưởng chưa được gán TeamId trong bảng staff. Cần cập nhật TeamId để nhận lệnh.
            </Alert>
          )}

          {error && (
            <Alert status="error" mt={4} borderRadius="md">
              <AlertIcon />
              {error}
            </Alert>
          )}
        </Box>

        <SimpleGrid columns={{ base: 1, md: 2, xl: 4 }} spacing={4}>
          {COLUMN_ORDER.map((color) => {
            const count = counts[color] || 0;
            const meta = COLOR_META[color] || COLOR_META.WHITE;
            const tone = STATUS_TONE[color] || STATUS_TONE.WHITE;
            const isActive = color === activeColorTab;
            return (
              <Box
                key={`stat-${color}`}
                bg={isActive ? tone.rowBg : "white"}
                border="1px solid"
                borderColor={isActive ? meta.borderColor : "gray.100"}
                borderRadius="xl"
                p={4}
                position="relative"
                overflow="hidden"
                boxShadow={isActive ? "0 10px 28px rgba(30, 64, 175, 0.14)" : "0 8px 24px rgba(15, 23, 42, 0.05)"}
                cursor="pointer"
                transition="all 0.18s ease"
                _hover={{ transform: "translateY(-2px)", borderColor: meta.borderColor }}
                onClick={() => setActiveColorTab(color)}
              >
                <Box position="absolute" left="0" top="0" right="0" h="3px" bg={meta.borderColor} />
                <Stat>
                  <StatLabel fontSize="xs" color="gray.500">
                    {meta.label}
                  </StatLabel>
                  <StatNumber fontSize="2xl" color="gray.800" mb={1}>
                    {count}
                  </StatNumber>
                  <Text fontSize="xs" color={tone.tabText} noOfLines={1}>
                    {meta.helper}
                  </Text>
                </Stat>
              </Box>
            );
          })}
        </SimpleGrid>

        {loading ? (
          <Box
            bg="white"
            border="1px solid"
            borderColor="gray.100"
            borderRadius="2xl"
            p={10}
            textAlign="center"
            boxShadow="0 10px 24px rgba(15, 23, 42, 0.05)"
          >
            <Spinner size="lg" color="blue.500" />
            <Text mt={3} color="gray.500">
              Đang tải bảng điều phối...
            </Text>
          </Box>
        ) : (
          <Grid templateColumns={{ base: "1fr", "2xl": "1.8fr 1fr" }} gap={5} alignItems="start">
            <GridItem>
              <Box
                bg="white"
                border="1px solid"
                borderColor="gray.100"
                borderRadius="2xl"
                overflow="hidden"
                boxShadow="0 14px 34px rgba(15, 23, 42, 0.06)"
              >
                <Box px={4} py={3} bg="white" borderBottom="1px solid" borderColor="gray.100">
                  <Text fontSize="xs" fontWeight="700" color="gray.700">
                    {activeTabMeta.label}
                  </Text>
                  <Text fontSize="xs" color="gray.500">
                    {activeTabMeta.helper}
                  </Text>
                </Box>

                <Box display={{ base: "none", lg: "block" }} bg="gray.50" borderBottom="1px solid" borderColor="gray.100">
                  <Grid templateColumns="1.2fr 1.2fr 1fr 0.9fr 1fr" gap={3} px={4} py={2}>
                    <Text fontSize="xs" fontWeight="700" color="gray.600">
                      Lệnh
                    </Text>
                    <Text fontSize="xs" fontWeight="700" color="gray.600">
                      Khách hàng / Biển số
                    </Text>
                    <Text fontSize="xs" fontWeight="700" color="gray.600">
                      Tổ / Thợ
                    </Text>
                    <Text fontSize="xs" fontWeight="700" color="gray.600">
                      Thời gian
                    </Text>
                    <Text fontSize="xs" fontWeight="700" color="gray.600">
                      Cảnh báo
                    </Text>
                  </Grid>
                </Box>

                {activeTabItems.length === 0 ? (
                  <Box p={10} textAlign="center" bg="linear-gradient(180deg, #ffffff 0%, #f8fbff 100%)">
                    <Text fontSize="sm" color="gray.400">
                      Không có lệnh trong nhóm này
                    </Text>
                  </Box>
                ) : (
                  <VStack align="stretch" spacing={0}>
                    {activeTabItems.map((item) => (
                      <DispatchRow
                        key={`row-${item.serviceOrderId}`}
                        color={activeColorTab}
                        item={item}
                        selectedServiceOrderId={selectedServiceOrderId}
                        onSelect={setSelectedServiceOrderId}
                      />
                    ))}
                  </VStack>
                )}
              </Box>
            </GridItem>

            <GridItem>
              <Box
                bg="white"
                border="1px solid"
                borderColor="gray.100"
                borderRadius="2xl"
                p={4}
                position={{ xl: "sticky" }}
                top={{ xl: "16px" }}
                boxShadow="0 14px 34px rgba(15, 23, 42, 0.06)"
              >
                {!selectedItem ? (
                  <Box border="1px dashed" borderColor="gray.200" borderRadius="xl" p={8} textAlign="center" bg="gray.50">
                    <Text color="gray.500" fontSize="sm">
                      Chọn một lệnh để xem chi tiết và thao tác.
                    </Text>
                  </Box>
                ) : (
                  <VStack align="stretch" spacing={4}>
                    <Box bg="gray.50" borderRadius="xl" p={3} border="1px solid" borderColor="gray.100">
                      <Text fontSize="xs" color="gray.500" mb={1}>
                        LỆNH SỬA CHỮA
                      </Text>
                      <Heading size="md" color="gray.700">
                        {selectedItem.serviceOrderCode}
                      </Heading>
                      <HStack mt={2} spacing={2} flexWrap="wrap">
                        <Badge colorScheme={(COLOR_META[selectedItem.queueColor] || COLOR_META.WHITE).badge} borderRadius="full">
                          {selectedItem.queueColor}
                        </Badge>
                        <Badge colorScheme="purple" borderRadius="full">
                          {getDispatchTagLabel(selectedItem.dispatchTag)}
                        </Badge>
                        <Badge colorScheme="blue" borderRadius="full">
                          {selectedItem.queueState || "--"}
                        </Badge>
                      </HStack>
                    </Box>

                    <Divider />

                    <Stack spacing={2} bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={3}>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Khách hàng:</strong> {selectedItem.customerName || "--"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Biển số:</strong> {selectedItem.vehicleLicensePlate || "--"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Tổ:</strong> {selectedItem.teamName || "Chưa giao tổ"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Thợ:</strong> {selectedItem.technicianName || "Chưa nhận"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Yêu cầu:</strong> {selectedItem.customerNotes || "Không có"}
                      </Text>
                    </Stack>

                    <Box bg="linear-gradient(180deg, #f8fbff 0%, #f1f5ff 100%)" borderRadius="xl" border="1px solid" borderColor="blue.100" p={3}>
                      <Text fontSize="xs" color="gray.500">
                        Thời điểm tạo: {formatDateTime(selectedItem.requestedAt)}
                      </Text>
                      <Text fontSize="xs" color="gray.500">
                        Thời điểm nhận: {formatDateTime(selectedItem.assignedAt)}
                      </Text>
                      <Text fontSize="xs" color="gray.500">
                        Chờ: {formatMinutes(selectedItem.pendingMinutes)}
                      </Text>
                      <Text fontSize="xs" color="gray.500">
                        Đang xử lý: {formatMinutes(selectedItem.assignedMinutes)}
                      </Text>
                    </Box>

                    {extractAlertReasons(selectedItem).length > 0 && (
                      <Alert status="error" borderRadius="xl" flexDirection="column" alignItems="flex-start" gap={1}>
                        <HStack>
                          <AlertIcon />
                          <Text fontWeight="700" fontSize="sm">
                            Có cảnh báo
                          </Text>
                        </HStack>
                        {extractAlertReasons(selectedItem).map((reason) => (
                          <Text key={`reason-${reason}`} fontSize="xs">
                            - {reason}
                          </Text>
                        ))}
                      </Alert>
                    )}

                    {selectedItem.isCancelPendingApproval && (
                      <Box bg="orange.50" border="1px solid" borderColor="orange.200" borderRadius="xl" p={3}>
                        <Text fontSize="xs" color="orange.700" fontWeight="700">
                          Yêu cầu hủy đang chờ duyệt
                        </Text>
                        <Text fontSize="xs" color="orange.700" mt={1}>
                          Lý do: {selectedItem.cancelReason || "--"}
                        </Text>
                      </Box>
                    )}

                    {mode === "foreman" && (
                      <VStack align="stretch" spacing={4}>
                        <Divider />
                        <Box border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            1) Nhận xe và phân công thợ
                          </Text>
                          <FormControl>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Kỹ thuật viên phụ trách
                            </FormLabel>
                            <Select
                              {...SHARED_SELECT_PROPS}
                              value={foremanTechnicianId}
                              onChange={(e) => setForemanTechnicianId(e.target.value)}
                            >
                              <option value="">-- Chọn kỹ thuật viên --</option>
                              {foremanTechnicianOptions.map((staff) => {
                                const id = toPositiveNumber(readValue(staff, "id", "Id"));
                                const name = toText(readValue(staff, "fullName", "FullName"));
                                return (
                                  <option key={`foreman-tech-${id}`} value={id || ""}>
                                    {name || `Staff #${id}`}
                                  </option>
                                );
                              })}
                            </Select>
                          </FormControl>
                          <Button
                            mt={3}
                            size="sm"
                            colorScheme="blue"
                            onClick={handleForemanReceive}
                            isLoading={saving}
                            isDisabled={!canForemanReceive || !foremanTechnicianId || showForemanWarning}
                          >
                            Nhận xe
                          </Button>
                        </Box>

                        <Box border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            2) Hỗ trợ / Hội chẩn
                          </Text>
                          <Textarea
                            size="sm"
                            value={supportNote}
                            onChange={(e) => setSupportNote(e.target.value)}
                            placeholder="Mô tả vấn đề cần quản đốc hỗ trợ..."
                          />
                          <Button
                            mt={3}
                            size="sm"
                            colorScheme="orange"
                            variant="outline"
                            onClick={handleForemanSupport}
                            isLoading={saving}
                            isDisabled={!canForemanSupport || showForemanWarning}
                          >
                            Gửi hỗ trợ
                          </Button>
                        </Box>

                        <Box border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            3) Gửi hủy lệnh (chờ duyệt)
                          </Text>
                          <FormControl>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Lý do bắt buộc
                            </FormLabel>
                            <Select {...SHARED_SELECT_PROPS} value={cancelReason} onChange={(e) => setCancelReason(e.target.value)}>
                              {CANCEL_REASONS.map((reason) => (
                                <option key={`cancel-reason-${reason}`} value={reason}>
                                  {reason}
                                </option>
                              ))}
                            </Select>
                          </FormControl>
                          <FormControl mt={2}>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Ghi chú bổ sung
                            </FormLabel>
                            <Textarea
                              size="sm"
                              value={cancelNote}
                              onChange={(e) => setCancelNote(e.target.value)}
                              placeholder="Thêm chi tiết để quản đốc phê duyệt..."
                            />
                          </FormControl>
                          <Button
                            mt={3}
                            size="sm"
                            colorScheme="red"
                            variant="outline"
                            onClick={handleForemanCancelRequest}
                            isLoading={saving}
                            isDisabled={!canForemanCancel || !cancelReasonPayload || showForemanWarning}
                          >
                            Gửi hủy lệnh
                          </Button>
                          {isCancelWindowOver && (
                            <Text mt={2} fontSize="xs" color="red.500">
                              Lưu ý: Đã quá 10 phút từ lúc nhận xe. Backend có thể từ chối yêu cầu hủy.
                            </Text>
                          )}
                        </Box>
                      </VStack>
                    )}
                    {mode === "manager" && (
                      <VStack align="stretch" spacing={4}>
                        <Divider />
                        <Box border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            Override phân công
                          </Text>
                          <FormControl>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Tổ thực hiện
                            </FormLabel>
                            <Select {...SHARED_SELECT_PROPS} value={managerTeamId} onChange={(e) => setManagerTeamId(e.target.value)}>
                              <option value="">-- Chọn tổ --</option>
                              {activeTeams.map((team) => {
                                const id = toPositiveNumber(readValue(team, "id", "Id"));
                                const name = toText(readValue(team, "name", "Name"));
                                const tag = toText(readValue(team, "specialtyTag", "SpecialtyTag"));
                                return (
                                  <option key={`override-team-${id}`} value={id || ""}>
                                    {name || `Tổ #${id}`} {tag ? `(${getDispatchTagLabel(tag)})` : ""}
                                  </option>
                                );
                              })}
                            </Select>
                          </FormControl>

                          <FormControl mt={2}>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Kỹ thuật viên
                            </FormLabel>
                            <Select
                              {...SHARED_SELECT_PROPS}
                              value={managerTechnicianId}
                              onChange={(e) => setManagerTechnicianId(e.target.value)}
                            >
                              <option value="">-- Chọn kỹ thuật viên --</option>
                              {managerTechnicianOptions.map((staff) => {
                                const id = toPositiveNumber(readValue(staff, "id", "Id"));
                                const name = toText(readValue(staff, "fullName", "FullName"));
                                return (
                                  <option key={`override-tech-${id}`} value={id || ""}>
                                    {name || `Staff #${id}`}
                                  </option>
                                );
                              })}
                            </Select>
                          </FormControl>

                          <FormControl mt={2}>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Lý do override
                            </FormLabel>
                            <Input
                              size="sm"
                              value={overrideReason}
                              onChange={(e) => setOverrideReason(e.target.value)}
                              placeholder="Ví dụ: xe chờ quá 10 phút, cần điều tải..."
                            />
                          </FormControl>

                          <Button
                            mt={3}
                            size="sm"
                            colorScheme="blue"
                            onClick={handleManagerOverride}
                            isLoading={saving}
                            isDisabled={!canManagerOverride || !managerTechnicianId}
                          >
                            Giao lại ngay
                          </Button>
                        </Box>

                        <Box border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            Duyệt hủy lệnh
                          </Text>
                          <Textarea
                            size="sm"
                            value={decisionNote}
                            onChange={(e) => setDecisionNote(e.target.value)}
                            placeholder="Ghi chú phê duyệt/từ chối (nếu cần)..."
                          />
                          <HStack mt={3} spacing={2}>
                            <Button
                              size="sm"
                              colorScheme="green"
                              onClick={() => handleManagerCancelDecision(true)}
                              isLoading={saving}
                              isDisabled={!canManagerDecide}
                            >
                              Đồng ý hủy
                            </Button>
                            <Button
                              size="sm"
                              colorScheme="red"
                              variant="outline"
                              onClick={() => handleManagerCancelDecision(false)}
                              isLoading={saving}
                              isDisabled={!canManagerDecide}
                            >
                              Từ chối hủy
                            </Button>
                          </HStack>
                          {!selectedItem.isCancelPendingApproval && (
                            <Text mt={2} fontSize="xs" color="gray.500">
                              Không có yêu cầu hủy đang chờ duyệt.
                            </Text>
                          )}
                        </Box>
                      </VStack>
                    )}
                  </VStack>
                )}
              </Box>
            </GridItem>
          </Grid>
        )}
      </VStack>
    </Box>
  );
}

