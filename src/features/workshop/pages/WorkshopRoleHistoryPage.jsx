import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
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
    processedLabel: "Đã hoàn tất chẩn đoán",
  },
  TEAM_LEADER: {
    title: "Lịch sử xử lý của tổ trưởng",
    subtitle: "Theo dõi các yêu cầu đã được tổ trưởng duyệt kỹ thuật.",
    dashboardPath: "/workshop/team-leader",
    openOrderPath: (orderId) => `/workshop/team-leader/dispatch/${orderId}`,
    processedLabel: "Đã duyệt kỹ thuật",
  },
  FOREMAN: {
    title: "Lịch sử xử lý của quản đốc",
    subtitle: "Theo dõi các yêu cầu đã được quản đốc duyệt cuối.",
    dashboardPath: "/workshop/foreman",
    openOrderPath: (orderId) => `/workshop/foreman/dispatch/${orderId}`,
    processedLabel: "Đã duyệt cuối",
  },
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
  const queueState = normalizeCode(readValue(item, "queueState", "QueueState"));
  const queueColor = normalizeCode(readValue(item, "queueColor", "QueueColor"));
  if (["DIAGNOSED", "COMPLETED", "DONE"].includes(queueState)) return true;
  if (queueColor === "GREEN") return true;
  if (readValue(item, "finalApprovedAt", "FinalApprovedAt")) return true;
  if (readValue(item, "technicalApprovedAt", "TechnicalApprovedAt")) return true;
  return Boolean(readValue(item, "technicianCompletedAt", "TechnicianCompletedAt"));
}

function resolveCompletedAt(item) {
  return (
    readValue(item, "finalApprovedAt", "FinalApprovedAt") ||
    readValue(item, "technicalApprovedAt", "TechnicalApprovedAt") ||
    readValue(item, "technicianCompletedAt", "TechnicianCompletedAt") ||
    readValue(item, "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt") ||
    readValue(item, "assignedAt", "AssignedAt")
  );
}

function toRow(item) {
  const serviceOrderId = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId")) || 0;
  const code = toText(readValue(item, "serviceOrderCode", "ServiceOrderCode")) || `SO-${serviceOrderId}`;
  const customerName = toText(readValue(item, "customerName", "CustomerName")) || "Khách hàng chưa rõ";
  const plate = toText(readValue(item, "vehicleLicensePlate", "VehicleLicensePlate")) || "--";
  const brand = toText(readValue(item, "vehicleBrandName", "VehicleBrandName"));
  const model = toText(readValue(item, "vehicleModelName", "VehicleModelName"));
  const descriptor = toText(readValue(item, "vehicleDescriptor", "VehicleDescriptor")) || [brand, model].filter(Boolean).join(" ");
  const teamName = toText(readValue(item, "primaryTeamName", "PrimaryTeamName", "teamName", "TeamName")) || "--";
  const completedAt = resolveCompletedAt(item);

  return {
    serviceOrderId,
    serviceOrderCode: code,
    customerName,
    vehicleLicensePlate: plate,
    vehicleDescriptor: descriptor || "Chưa rõ dòng xe",
    queueState: toText(readValue(item, "queueState", "QueueState")) || "--",
    teamName,
    assignedAt: readValue(item, "assignedAt", "AssignedAt"),
    startedAt: readValue(item, "startedAt", "StartedAt", "technicianAcceptedAt", "TechnicianAcceptedAt"),
    technicalApprovedAt: readValue(item, "technicalApprovedAt", "TechnicalApprovedAt"),
    finalApprovedAt: readValue(item, "finalApprovedAt", "FinalApprovedAt"),
    completedAt,
    raw: item,
  };
}

function getProcessedAtLabel(roleCode) {
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
      const [boardRes, staffsRes] = await Promise.all([
        serviceOrderApi.getDiagnosisDispatchBoard({ includeCompleted: true }),
        getAllStaff(),
      ]);

      const board = Array.isArray(boardRes) ? boardRes : [];
      const staffs = Array.isArray(staffsRes) ? staffsRes : [];
      const currentStaff = resolveCurrentStaff(staffs, user);
      const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id"));
      const currentTeamId = toPositiveNumber(readValue(currentStaff, "teamId", "TeamId"));

      if (!currentStaffId && (roleCode === "TECH" || roleCode === "TEAM_LEADER")) {
        setRows([]);
        setError("Không xác định được hồ sơ nhân sự của tài khoản hiện tại.");
        return;
      }

      const completed = board.filter(isCompletedRecord);
      const filteredByRole = completed.filter((item) => {
        const leadId = toPositiveNumber(readValue(item, "technicianId", "TechnicianId"));
        const assistantId = toPositiveNumber(readValue(item, "assistantTechnicianId", "AssistantTechnicianId"));
        const technicalApprovedByTeamLeaderId = toPositiveNumber(
          readValue(item, "technicalApprovedByTeamLeaderId", "TechnicalApprovedByTeamLeaderId")
        );
        const primaryTeamId = toPositiveNumber(readValue(item, "primaryTeamId", "PrimaryTeamId", "teamId", "TeamId"));
        const finalApprovedAt = readValue(item, "finalApprovedAt", "FinalApprovedAt");
        const technicalApprovedAt = readValue(item, "technicalApprovedAt", "TechnicalApprovedAt");

        if (roleCode === "TECH") {
          return leadId === currentStaffId || assistantId === currentStaffId;
        }

        if (roleCode === "TEAM_LEADER") {
          if (technicalApprovedByTeamLeaderId && technicalApprovedByTeamLeaderId === currentStaffId) return true;
          return Boolean(technicalApprovedAt) && Boolean(currentTeamId) && primaryTeamId === currentTeamId;
        }

        if (roleCode === "FOREMAN") {
          return Boolean(finalApprovedAt);
        }

        return false;
      });

      const mapped = filteredByRole
        .map(toRow)
        .sort((a, b) => {
          const left = new Date(a.completedAt || 0).getTime();
          const right = new Date(b.completedAt || 0).getTime();
          return right - left;
        });

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
    const unique = Array.from(new Set(rows.map((row) => toText(row.queueState)).filter(Boolean)));
    return unique.sort((a, b) => a.localeCompare(b, "vi"));
  }, [rows]);

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
      const byStatus = statusFilter === "ALL" || toText(row.queueState) === statusFilter;
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
                  ...statusOptions.map((status) => ({ value: status, label: status })),
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
                  const today = toDateText(new Date());
                  setDateFrom(today);
                  setDateTo(today);
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
                  key={`${row.serviceOrderId}-${row.completedAt || ""}`}
                  p={4}
                  borderRadius="lg"
                  border="1px solid #e6ecf8"
                  bg="#fbfdff"
                  _hover={{ borderColor: "#c9d9f3", boxShadow: "0 6px 18px rgba(27, 37, 89, 0.08)" }}
                  transition="all 0.18s ease"
                >
                  <Stack spacing={3}>
                    <Flex justify="space-between" align={{ base: "start", md: "center" }} direction={{ base: "column", md: "row" }} gap={3}>
                      <Box>
                        <HStack spacing={2} flexWrap="wrap">
                          <Text fontWeight="800" color="#1b2559">{row.serviceOrderCode}</Text>
                          <Badge colorScheme="green" borderRadius="full" px={2.5} py={0.5}>
                            {roleConfig.processedLabel}
                          </Badge>
                        </HStack>
                        <Text mt={1} color="#4a5568">{row.customerName}</Text>
                        <HStack mt={1} spacing={2} flexWrap="wrap">
                          <Badge colorScheme="blue" borderRadius="full">Biển số: {row.vehicleLicensePlate}</Badge>
                          <Badge colorScheme="gray" borderRadius="full">Dòng xe: {row.vehicleDescriptor || "--"}</Badge>
                          <Badge colorScheme="purple" borderRadius="full">Tổ: {row.teamName || "--"}</Badge>
                        </HStack>
                      </Box>
                      <Button
                        size="sm"
                        colorScheme="blue"
                        variant="outline"
                        onClick={() => navigate(roleConfig.openOrderPath(row.serviceOrderId))}
                      >
                        Mở chi tiết
                      </Button>
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
                        <strong>{getProcessedAtLabel(roleCode)}:</strong> {formatDateTime(row.completedAt)}
                      </Text>
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

