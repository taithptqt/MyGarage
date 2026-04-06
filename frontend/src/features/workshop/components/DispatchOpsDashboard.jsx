
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
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useSearch } from "@shared/core/context/SearchContext";

const COLOR_META = {
  WHITE: {
    label: "WHITE - Cho tiep nhan",
    helper: "Xe moi dang cho to truong nhan",
    badge: "gray",
    borderColor: "#cbd5e0",
  },
  YELLOW: {
    label: "YELLOW - Dang chan doan",
    helper: "Da co to nhan va dang xu ly",
    badge: "yellow",
    borderColor: "#d69e2e",
  },
  RED: {
    label: "RED - Canh bao",
    helper: "Can can thiep quan doc / ho tro ky thuat",
    badge: "red",
    borderColor: "#e53e3e",
  },
  GREEN: {
    label: "GREEN - Da chan doan",
    helper: "Da co ket qua chan doan",
    badge: "green",
    borderColor: "#38a169",
  },
};

const COLUMN_ORDER = ["WHITE", "YELLOW", "RED", "GREEN"];
const DEFAULT_CANCEL_REASON = "Sai chuyen mon";
const CANCEL_REASONS = ["Sai chuyen mon", "Qua tai cau", "Thieu thiet bi", "Khac"];

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
  if (!Number.isFinite(value) || value <= 0) return "0 phut";
  if (value < 60) return `${Math.floor(value)} phut`;
  const hours = Math.floor(value / 60);
  const rest = Math.floor(value % 60);
  return rest > 0 ? `${hours} gio ${rest} phut` : `${hours} gio`;
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
    roleName.includes("to truong")
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
    reasons.push("To truong da bam ho tro/hoi chan");
  }

  if (item.isCancelPendingApproval) {
    reasons.push("Dang cho quan doc duyet huy lenh");
  }

  if (item.queueColor === "RED" && !item.technicianId && item.pendingMinutes >= 10) {
    reasons.push("Xe cho nhan qua 10 phut");
  }

  if (item.queueColor === "RED" && item.technicianId && item.assignedMinutes >= 45) {
    reasons.push("Chan doan qua 45 phut chua xong");
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
function DispatchColumn({ color, items, selectedServiceOrderId, onSelect }) {
  const meta = COLOR_META[color] || COLOR_META.WHITE;

  return (
    <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={4} minH="520px">
      <HStack justify="space-between" mb={1}>
        <Text fontWeight="800" color="gray.700" fontSize="sm">
          {meta.label}
        </Text>
        <Badge colorScheme={meta.badge} borderRadius="full" px={2.5} py={1}>
          {items.length}
        </Badge>
      </HStack>
      <Text fontSize="xs" color="gray.500" mb={3}>
        {meta.helper}
      </Text>
      <VStack align="stretch" spacing={3}>
        {items.length === 0 ? (
          <Box border="1px dashed" borderColor="gray.200" borderRadius="lg" p={5} textAlign="center">
            <Text fontSize="sm" color="gray.400">
              Khong co lenh
            </Text>
          </Box>
        ) : (
          items.map((item) => {
            const isSelected = Number(item.serviceOrderId) === Number(selectedServiceOrderId);
            const reasons = extractAlertReasons(item);
            return (
              <Box
                key={item.serviceOrderId}
                border="1px solid"
                borderColor={isSelected ? "blue.400" : "gray.200"}
                borderLeftWidth="4px"
                borderLeftColor={meta.borderColor}
                bg={isSelected ? "blue.50" : "white"}
                borderRadius="lg"
                p={3}
                cursor="pointer"
                transition="all 0.15s ease"
                _hover={{ borderColor: "blue.300", boxShadow: "sm" }}
                onClick={() => onSelect(item.serviceOrderId)}
              >
                <HStack justify="space-between" align="start" spacing={3}>
                  <Box minW={0}>
                    <Text fontWeight="800" color="gray.800" noOfLines={1}>
                      {item.serviceOrderCode}
                    </Text>
                    <Text fontSize="xs" color="gray.600" noOfLines={1}>
                      {item.customerName || "Khach hang chua ro"}
                    </Text>
                    <Text fontSize="xs" color="gray.500" noOfLines={1}>
                      Bien so: {item.vehicleLicensePlate || "--"}
                    </Text>
                  </Box>
                  <Badge variant="subtle" colorScheme="purple" borderRadius="full" whiteSpace="nowrap">
                    {item.dispatchTag || "UNSET"}
                  </Badge>
                </HStack>

                <Divider my={2} borderColor="gray.100" />

                <Text fontSize="xs" color="gray.600" noOfLines={1}>
                  To: {item.teamName || "Chua giao to"}
                </Text>
                <Text fontSize="xs" color="gray.600" noOfLines={1}>
                  Tho: {item.technicianName || "Chua nhan"}
                </Text>
                <HStack mt={2} spacing={2} flexWrap="wrap">
                  <Badge variant="outline" colorScheme="gray" borderRadius="full">
                    Cho: {formatMinutes(item.pendingMinutes)}
                  </Badge>
                  <Badge variant="outline" colorScheme="orange" borderRadius="full">
                    Xu ly: {formatMinutes(item.assignedMinutes)}
                  </Badge>
                </HStack>
                {reasons.length > 0 && (
                  <VStack align="stretch" spacing={1} mt={2}>
                    {reasons.slice(0, 2).map((reason) => (
                      <Text key={`${item.serviceOrderId}-${reason}`} fontSize="xs" color="red.500" noOfLines={1}>
                        ! {reason}
                      </Text>
                    ))}
                  </VStack>
                )}
              </Box>
            );
          })
        )}
      </VStack>
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
  const [selectedServiceOrderId, setSelectedServiceOrderId] = useState(null);

  const [foremanTechnicianId, setForemanTechnicianId] = useState("");
  const [supportNote, setSupportNote] = useState("");
  const [cancelReason, setCancelReason] = useState(DEFAULT_CANCEL_REASON);
  const [cancelNote, setCancelNote] = useState("");

  const [managerTeamId, setManagerTeamId] = useState("");
  const [managerTechnicianId, setManagerTechnicianId] = useState("");
  const [overrideReason, setOverrideReason] = useState("");
  const [decisionNote, setDecisionNote] = useState("");

  const branchId = toPositiveNumber(selectedBranchId);

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
      setError(err?.message || "Khong the tai du lieu dieu phoi.");
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

    const stillExists = filteredItems.some((item) => Number(item.serviceOrderId) === Number(selectedServiceOrderId));
    if (!stillExists) {
      setSelectedServiceOrderId(filteredItems[0].serviceOrderId);
    }
  }, [filteredItems, selectedServiceOrderId]);

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

  const runAction = useCallback(
    async (runner, successMessage) => {
      setSaving(true);
      try {
        await runner();
        toast({ title: successMessage, status: "success", duration: 3000, isClosable: true });
        await fetchData();
      } catch (err) {
        toast({
          title: "Thao tac that bai",
          description: err?.message || "Khong the xu ly yeu cau.",
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
      toast({ title: "Khong xac dinh duoc staff hien tai", status: "warning", duration: 3000 });
      return;
    }

    const technicianId = toPositiveNumber(foremanTechnicianId);
    if (!technicianId) {
      toast({ title: "Can chon tho phu trach", status: "warning", duration: 2500 });
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
      "Da nhan xe va phan cong tho"
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
      "Da gui yeu cau ho tro"
    );
  }, [selectedItem, currentStaffId, supportNote, runAction]);

  const cancelReasonPayload = useMemo(() => {
    const selectedReason = toText(cancelReason);
    const note = toText(cancelNote);
    if (selectedReason === "Khac") {
      return note;
    }
    return note ? `${selectedReason}: ${note}` : selectedReason;
  }, [cancelReason, cancelNote]);

  const handleForemanCancelRequest = useCallback(async () => {
    if (!selectedItem || !currentStaffId) return;

    const reason = toText(cancelReasonPayload);
    if (!reason) {
      toast({ title: "Can nhap ly do huy", status: "warning", duration: 2500 });
      return;
    }

    await runAction(
      () =>
        serviceOrderApi.requestDiagnosisCancel(selectedItem.serviceOrderId, {
          requestedByStaffId: currentStaffId,
          reason,
        }),
      "Da gui huy lenh cho quan doc duyet"
    );
  }, [selectedItem, currentStaffId, cancelReasonPayload, runAction, toast]);

  const handleManagerOverride = useCallback(async () => {
    if (!selectedItem || !currentStaffId) return;

    const technicianId = toPositiveNumber(managerTechnicianId);
    if (!technicianId) {
      toast({ title: "Can chon ky thuat vien", status: "warning", duration: 2500 });
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
      "Da override phan cong"
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
        approve ? "Da dong y huy lenh" : "Da tu choi huy lenh"
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
      <VStack align="stretch" spacing={5}>
        <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={5}>
          <HStack justify="space-between" align="start" spacing={6} flexWrap="wrap">
            <Box>
              <Heading size="md" color="gray.700">
                Dieu phoi chan doan - {mode === "manager" ? "Quan doc" : "To truong"}
              </Heading>
              <Text mt={1} fontSize="sm" color="gray.500">
                Bang van hanh theo mau White/Yellow/Red/Green va SOP 5 buoc.
              </Text>
            </Box>
            <HStack spacing={3} align="end" flexWrap="wrap">
              {mode === "manager" && (
                <FormControl maxW="220px">
                  <FormLabel fontSize="xs" color="gray.500" mb={1}>
                    Loc theo to
                  </FormLabel>
                  <Select size="sm" value={teamFilter} onChange={(e) => setTeamFilter(e.target.value)}>
                    <option value="">Tat ca to</option>
                    {activeTeams.map((team) => {
                      const id = toPositiveNumber(readValue(team, "id", "Id"));
                      const name = toText(readValue(team, "name", "Name"));
                      const tag = toText(readValue(team, "specialtyTag", "SpecialtyTag"));
                      return (
                        <option key={id || name} value={id || ""}>
                          {name || `To #${id}`} {tag ? `(${tag})` : ""}
                        </option>
                      );
                    })}
                  </Select>
                </FormControl>
              )}

              <FormControl maxW="220px">
                <FormLabel fontSize="xs" color="gray.500" mb={1}>
                  Loc theo chuyen mon
                </FormLabel>
                <Select size="sm" value={dispatchTagFilter} onChange={(e) => setDispatchTagFilter(e.target.value)}>
                  <option value="">Tat ca chuyen mon</option>
                  {dispatchTagOptions.map((tag) => (
                    <option key={tag} value={tag}>
                      {tag}
                    </option>
                  ))}
                </Select>
              </FormControl>

              {mode === "manager" && (
                <FormControl display="flex" alignItems="center" maxW="190px">
                  <FormLabel htmlFor="dispatch-include-completed" mb="0" fontSize="xs" color="gray.500">
                    Hien thi da xong
                  </FormLabel>
                  <Switch
                    id="dispatch-include-completed"
                    isChecked={includeCompleted}
                    onChange={(e) => setIncludeCompleted(e.target.checked)}
                  />
                </FormControl>
              )}

              <Button size="sm" colorScheme="blue" variant="outline" onClick={fetchData} isLoading={loading || saving}>
                Tai lai
              </Button>
            </HStack>
          </HStack>

          {showForemanWarning && (
            <Alert status="warning" mt={4} borderRadius="md">
              <AlertIcon />
              Tai khoan to truong chua duoc gan TeamId trong bang staff. Can cap nhat TeamId de nhan lenh.
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
            return (
              <Box key={`stat-${color}`} bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={4}>
                <Stat>
                  <StatLabel fontSize="xs" color="gray.500">
                    {meta.label}
                  </StatLabel>
                  <StatNumber fontSize="2xl" color="gray.700">
                    {count}
                  </StatNumber>
                </Stat>
              </Box>
            );
          })}
        </SimpleGrid>

        {loading ? (
          <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={10} textAlign="center">
            <Spinner size="lg" color="blue.500" />
            <Text mt={3} color="gray.500">
              Dang tai bang dieu phoi...
            </Text>
          </Box>
        ) : (
          <Grid templateColumns={{ base: "1fr", xl: "2.2fr 1fr" }} gap={5} alignItems="start">
            <GridItem>
              <SimpleGrid columns={{ base: 1, md: 2, xl: 4 }} spacing={4}>
                {COLUMN_ORDER.map((color) => (
                  <DispatchColumn
                    key={`column-${color}`}
                    color={color}
                    items={groupedByColor[color] || []}
                    selectedServiceOrderId={selectedServiceOrderId}
                    onSelect={setSelectedServiceOrderId}
                  />
                ))}
              </SimpleGrid>
            </GridItem>

            <GridItem>
              <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={4} position={{ xl: "sticky" }} top={{ xl: "16px" }}>
                {!selectedItem ? (
                  <Box border="1px dashed" borderColor="gray.200" borderRadius="lg" p={5} textAlign="center">
                    <Text color="gray.500" fontSize="sm">
                      Chon mot lenh de xem chi tiet va thao tac.
                    </Text>
                  </Box>
                ) : (
                  <VStack align="stretch" spacing={4}>
                    <Box>
                      <Text fontSize="xs" color="gray.500" mb={1}>
                        LENH SUA CHUA
                      </Text>
                      <Heading size="md" color="gray.700">
                        {selectedItem.serviceOrderCode}
                      </Heading>
                      <HStack mt={2} spacing={2} flexWrap="wrap">
                        <Badge colorScheme={(COLOR_META[selectedItem.queueColor] || COLOR_META.WHITE).badge} borderRadius="full">
                          {selectedItem.queueColor}
                        </Badge>
                        <Badge colorScheme="purple" borderRadius="full">
                          {selectedItem.dispatchTag || "UNSET"}
                        </Badge>
                        <Badge colorScheme="blue" borderRadius="full">
                          {selectedItem.queueState || "--"}
                        </Badge>
                      </HStack>
                    </Box>

                    <Divider />

                    <Stack spacing={2}>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Khach hang:</strong> {selectedItem.customerName || "--"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Bien so:</strong> {selectedItem.vehicleLicensePlate || "--"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>To:</strong> {selectedItem.teamName || "Chua giao to"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Tho:</strong> {selectedItem.technicianName || "Chua nhan"}
                      </Text>
                      <Text fontSize="sm" color="gray.700">
                        <strong>Yeu cau:</strong> {selectedItem.customerNotes || "Khong co"}
                      </Text>
                    </Stack>

                    <Box bg="gray.50" borderRadius="md" p={3}>
                      <Text fontSize="xs" color="gray.500">
                        Requested at: {formatDateTime(selectedItem.requestedAt)}
                      </Text>
                      <Text fontSize="xs" color="gray.500">
                        Assigned at: {formatDateTime(selectedItem.assignedAt)}
                      </Text>
                      <Text fontSize="xs" color="gray.500">
                        Pending: {formatMinutes(selectedItem.pendingMinutes)}
                      </Text>
                      <Text fontSize="xs" color="gray.500">
                        In progress: {formatMinutes(selectedItem.assignedMinutes)}
                      </Text>
                    </Box>

                    {extractAlertReasons(selectedItem).length > 0 && (
                      <Alert status="error" borderRadius="md" flexDirection="column" alignItems="flex-start" gap={1}>
                        <HStack>
                          <AlertIcon />
                          <Text fontWeight="700" fontSize="sm">
                            Co canh bao
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
                      <Box bg="orange.50" border="1px solid" borderColor="orange.200" borderRadius="md" p={3}>
                        <Text fontSize="xs" color="orange.700" fontWeight="700">
                          Yeu cau huy dang cho duyet
                        </Text>
                        <Text fontSize="xs" color="orange.700" mt={1}>
                          Ly do: {selectedItem.cancelReason || "--"}
                        </Text>
                      </Box>
                    )}

                    {mode === "foreman" && (
                      <VStack align="stretch" spacing={4}>
                        <Divider />
                        <Box border="1px solid" borderColor="gray.100" borderRadius="md" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            1) Nhan xe va phan cong tho
                          </Text>
                          <FormControl>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Ky thuat vien phu trach
                            </FormLabel>
                            <Select
                              size="sm"
                              value={foremanTechnicianId}
                              onChange={(e) => setForemanTechnicianId(e.target.value)}
                            >
                              <option value="">-- Chon ky thuat vien --</option>
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
                            Nhan xe
                          </Button>
                        </Box>

                        <Box border="1px solid" borderColor="gray.100" borderRadius="md" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            2) Ho tro / Hoi chan
                          </Text>
                          <Textarea
                            size="sm"
                            value={supportNote}
                            onChange={(e) => setSupportNote(e.target.value)}
                            placeholder="Mo ta van de can quan doc ho tro..."
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
                            Gui ho tro
                          </Button>
                        </Box>

                        <Box border="1px solid" borderColor="gray.100" borderRadius="md" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            3) Gui huy lenh (cho duyet)
                          </Text>
                          <FormControl>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Ly do bat buoc
                            </FormLabel>
                            <Select size="sm" value={cancelReason} onChange={(e) => setCancelReason(e.target.value)}>
                              {CANCEL_REASONS.map((reason) => (
                                <option key={`cancel-reason-${reason}`} value={reason}>
                                  {reason}
                                </option>
                              ))}
                            </Select>
                          </FormControl>
                          <FormControl mt={2}>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Ghi chu bo sung
                            </FormLabel>
                            <Textarea
                              size="sm"
                              value={cancelNote}
                              onChange={(e) => setCancelNote(e.target.value)}
                              placeholder="Them chi tiet de quan doc phe duyet..."
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
                            Gui huy lenh
                          </Button>
                          {isCancelWindowOver && (
                            <Text mt={2} fontSize="xs" color="red.500">
                              Luu y: Da qua 10 phut tu luc nhan xe. Backend co the tu choi yeu cau huy.
                            </Text>
                          )}
                        </Box>
                      </VStack>
                    )}
                    {mode === "manager" && (
                      <VStack align="stretch" spacing={4}>
                        <Divider />
                        <Box border="1px solid" borderColor="gray.100" borderRadius="md" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            Override phan cong
                          </Text>
                          <FormControl>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              To thuc hien
                            </FormLabel>
                            <Select size="sm" value={managerTeamId} onChange={(e) => setManagerTeamId(e.target.value)}>
                              <option value="">-- Chon to --</option>
                              {activeTeams.map((team) => {
                                const id = toPositiveNumber(readValue(team, "id", "Id"));
                                const name = toText(readValue(team, "name", "Name"));
                                const tag = toText(readValue(team, "specialtyTag", "SpecialtyTag"));
                                return (
                                  <option key={`override-team-${id}`} value={id || ""}>
                                    {name || `To #${id}`} {tag ? `(${tag})` : ""}
                                  </option>
                                );
                              })}
                            </Select>
                          </FormControl>

                          <FormControl mt={2}>
                            <FormLabel fontSize="xs" mb={1} color="gray.500">
                              Ky thuat vien
                            </FormLabel>
                            <Select
                              size="sm"
                              value={managerTechnicianId}
                              onChange={(e) => setManagerTechnicianId(e.target.value)}
                            >
                              <option value="">-- Chon ky thuat vien --</option>
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
                              Ly do override
                            </FormLabel>
                            <Input
                              size="sm"
                              value={overrideReason}
                              onChange={(e) => setOverrideReason(e.target.value)}
                              placeholder="Vi du: xe cho qua 10 phut, can dieu tai..."
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
                            Override ngay
                          </Button>
                        </Box>

                        <Box border="1px solid" borderColor="gray.100" borderRadius="md" p={3}>
                          <Text fontWeight="700" color="gray.700" mb={2}>
                            Duyet huy lenh
                          </Text>
                          <Textarea
                            size="sm"
                            value={decisionNote}
                            onChange={(e) => setDecisionNote(e.target.value)}
                            placeholder="Ghi chu phe duyet/tu choi (neu can)..."
                          />
                          <HStack mt={3} spacing={2}>
                            <Button
                              size="sm"
                              colorScheme="green"
                              onClick={() => handleManagerCancelDecision(true)}
                              isLoading={saving}
                              isDisabled={!canManagerDecide}
                            >
                              Dong y huy
                            </Button>
                            <Button
                              size="sm"
                              colorScheme="red"
                              variant="outline"
                              onClick={() => handleManagerCancelDecision(false)}
                              isLoading={saving}
                              isDisabled={!canManagerDecide}
                            >
                              Tu choi huy
                            </Button>
                          </HStack>
                          {!selectedItem.isCancelPendingApproval && (
                            <Text mt={2} fontSize="xs" color="gray.500">
                              Khong co yeu cau huy dang cho duyet.
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
