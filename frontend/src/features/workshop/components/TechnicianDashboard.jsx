import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { getAllStaff } from "@features/admin/api/staffApi";
import { Modal } from "@features/admin/components/AdminModals";
import { useSearch } from "@shared/core/context/SearchContext";
import {
  Badge,
  Box,
  Button,
  Divider,
  Flex,
  Heading,
  HStack,
  Icon,
  SimpleGrid,
  Stack,
  Text,
  Textarea,
  useToast,
  VStack,
} from "@chakra-ui/react";
import {
  MdBuild,
  MdCancel,
  MdCheckCircle,
  MdDirectionsCar,
  MdInfo,
  MdPerson,
  MdPlayArrow,
  MdPriorityHigh,
  MdSupportAgent,
} from "react-icons/md";
import { request } from "@app/apiClient";

const TAB_DIAGNOSIS = "DIAGNOSIS";
const TAB_REPAIR = "REPAIR";
const INTERNAL_ASSIGNMENT_STATUS_REJECTED = "REJECTED";

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

export default function TechnicianDashboard() {
  const { searchQuery } = useSearch();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState(TAB_DIAGNOSIS);
  const [staffId, setStaffId] = useState(null);
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
      const [ordersRes, staffRes, assignmentsRes, detailsRes, boardRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllStaff(),
        request({ url: "/api/service-staff-assignments", method: "GET" }),
        request({ url: "/api/service-order-details", method: "GET" }),
        serviceOrderApi.getDiagnosisDispatchBoard({ includeCompleted: true }),
      ]);

      const orders = Array.isArray(ordersRes) ? ordersRes : [];
      const staffs = Array.isArray(staffRes) ? staffRes : [];
      const allAssignments = Array.isArray(assignmentsRes) ? assignmentsRes : [];
      const allDetails = Array.isArray(detailsRes) ? detailsRes : [];
      const board = Array.isArray(boardRes) ? boardRes : [];

      const currentStaff = resolveCurrentStaff(staffs, user);
      const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id"));
      setStaffId(currentStaffId);

      if (!currentStaffId) {
        setAssignments([]);
        setDiagnosisTasks([]);
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
          return assignment.order && orderStatus !== "CANCELLED" && orderStatus !== "COMPLETED";
        });

      setAssignments(myAssignments);

      const myDiagnosisTasks = board
        .map((item) => {
          const serviceOrderId = toPositiveNumber(readValue(item, "serviceOrderId", "ServiceOrderId"));
          const order = orders.find((row) => toPositiveNumber(readValue(row, "id", "Id")) === serviceOrderId) || null;

          const leadId = toPositiveNumber(readValue(item, "technicianId", "TechnicianId"));
          const assistantId = toPositiveNumber(readValue(item, "assistantTechnicianId", "AssistantTechnicianId"));
          const isLead = leadId === currentStaffId;
          const isAssistant = assistantId === currentStaffId;
          if (!isLead && !isAssistant) return null;

          return {
            serviceOrderId,
            serviceOrderCode: toText(readValue(item, "serviceOrderCode", "ServiceOrderCode")) || toText(readValue(order, "code", "Code")) || `SO-${serviceOrderId}`,
            customerName: toText(readValue(item, "customerName", "CustomerName")) || toText(readValue(order, "customerName", "CustomerName")) || "Khách hàng chưa rõ",
            vehicleLicensePlate: toText(readValue(item, "vehicleLicensePlate", "VehicleLicensePlate")) || toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) || "--",
            queueColor: normalizeCode(readValue(item, "queueColor", "QueueColor")) || "WHITE",
            queueState: toText(readValue(item, "queueState", "QueueState")) || "--",
            dispatchTag: normalizeCode(readValue(item, "dispatchTag", "DispatchTag")) || "",
            assignedBayId: toPositiveNumber(readValue(item, "assignedBayId", "AssignedBayId")),
            assignedBayCode: toText(readValue(item, "assignedBayCode", "AssignedBayCode")),
            pendingMinutes: Number(readValue(item, "pendingMinutes", "PendingMinutes") || 0),
            assignedMinutes: Number(readValue(item, "assignedMinutes", "AssignedMinutes") || 0),
            assignedAt: readValue(item, "assignedAt", "AssignedAt"),
            startedAt: readValue(item, "startedAt", "StartedAt"),
            technicalApprovedAt: readValue(item, "technicalApprovedAt", "TechnicalApprovedAt"),
            finalApprovedAt: readValue(item, "finalApprovedAt", "FinalApprovedAt"),
            isSupportRequested: Boolean(readValue(item, "isSupportRequested", "IsSupportRequested")),
            isCancelPendingApproval: Boolean(readValue(item, "isCancelPendingApproval", "IsCancelPendingApproval")),
            leadName: toText(readValue(item, "technicianName", "TechnicianName")),
            assistantName: toText(readValue(item, "assistantTechnicianName", "AssistantTechnicianName")),
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

    try {
      await serviceOrderApi.startDiagnosis(task.serviceOrderId, {
        technicianId: staffId,
        bayId: task.assignedBayId || null,
      });

      toast({ title: "Đã bắt đầu chuẩn đoán", status: "success", duration: 2500, isClosable: true });
      await fetchData();
    } catch (err) {
      toast({ title: "Không thể bắt đầu chuẩn đoán", description: resolveApiErrorMessage(err), status: "error" });
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
      const customerName = toText(readValue(item.order, "customerName", "CustomerName")).toLowerCase();
      const itemName = toText(readValue(item.detail, "itemName", "ItemName")).toLowerCase();
      return orderCode.includes(query) || customerName.includes(query) || itemName.includes(query);
    });
  }, [assignments, searchQuery]);

  const filteredDiagnosisTasks = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return diagnosisTasks;

    return diagnosisTasks.filter((task) => {
      const haystack = [task.serviceOrderCode, task.vehicleLicensePlate, task.customerName]
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

  return (
    <Box p={6} bg="#f4f7fe" minH="100vh">
      <VStack align="stretch" spacing={8}>
        <Box>
          <Heading size="lg" color="#2b3674" mb={2}>Bảng điều khiển Kỹ thuật viên</Heading>
          <Text color="gray.500">Nhận ca từ tổ trưởng, bắt đầu chuẩn đoán tại cầu nâng và báo hỗ trợ khi cần.</Text>
        </Box>

        <HStack spacing={3} align="center" flexWrap="wrap">
          <Button variant={activeTab === TAB_DIAGNOSIS ? "solid" : "outline"} colorScheme="purple" onClick={() => setActiveTab(TAB_DIAGNOSIS)}>
            Chuẩn đoán ({filteredDiagnosisTasks.length})
          </Button>
          <Button variant={activeTab === TAB_REPAIR ? "solid" : "outline"} colorScheme="teal" onClick={() => setActiveTab(TAB_REPAIR)}>
            Sửa chữa ({activeAssignments.length})
          </Button>
        </HStack>

        {loading ? (
          <Flex justify="center" py={20}><Text>Đang tải...</Text></Flex>
        ) : error ? (
          <Box bg="red.50" p={4} borderRadius="lg"><Text color="red.500">{error}</Text></Box>
        ) : (
          <>
            {activeTab === TAB_DIAGNOSIS && (
              <Box>
                <HStack mb={4}><Icon as={MdInfo} color="purple.400" /><Heading size="md" color="#2b3674">Ca chuẩn đoán đã phân công</Heading></HStack>
                {filteredDiagnosisTasks.length === 0 ? (
                  <Box bg="white" p={10} textAlign="center" borderRadius="2xl" border="2px dashed" borderColor="gray.100">
                    <Text color="gray.400">Hiện chưa có ca chuẩn đoán được giao cho bạn.</Text>
                  </Box>
                ) : (
                  <SimpleGrid columns={{ base: 1, lg: 2 }} spacing={4}>
                    {filteredDiagnosisTasks.map((task) => {
                      const canStart = !task.startedAt && !task.technicalApprovedAt && !task.finalApprovedAt;
                      const teammateName = task.roleInPair === "Thợ chính" ? task.assistantName : task.leadName;
                      const waitingFinal = task.technicalApprovedAt && !task.finalApprovedAt;
                      const finalized = Boolean(task.finalApprovedAt);

                      return (
                        <Box key={`diag-task-${task.serviceOrderId}`} bg="white" p={4} borderRadius="2xl" border="1px solid" borderColor="gray.200" boxShadow="sm">
                          <HStack justify="space-between" align="start" mb={3}>
                            <Box>
                              <Text fontSize="xs" color="gray.500" mb={1}>Mã lệnh</Text>
                              <Heading size="sm" color="#2b3674">{task.serviceOrderCode}</Heading>
                            </Box>
                            <VStack spacing={1} align="end">
                              <Badge colorScheme={task.queueColor === "RED" ? "red" : task.queueColor === "YELLOW" ? "yellow" : task.queueColor === "GREEN" ? "green" : "gray"}>
                                {task.queueColor}
                              </Badge>
                              <Badge variant="subtle" colorScheme="blue">{task.roleInPair}</Badge>
                            </VStack>
                          </HStack>

                          <VStack align="stretch" spacing={2} mb={4}>
                            <HStack spacing={2}><Icon as={MdDirectionsCar} color="gray.500" /><Text fontSize="sm">{task.vehicleLicensePlate || "--"}</Text></HStack>
                            <HStack spacing={2}><Icon as={MdPerson} color="gray.500" /><Text fontSize="sm">{task.customerName || "Khách hàng chưa rõ"}</Text></HStack>
                            <Text fontSize="sm" color="gray.600">Đồng đội: {teammateName || "--"}</Text>
                            <Text fontSize="sm" color="gray.600">Cầu nâng: {task.assignedBayCode || "Chưa chỉ định"}</Text>
                            <Text fontSize="sm" color="gray.600">Nhận ca: {formatDateTime(task.assignedAt)}</Text>
                            <Text fontSize="sm" color="gray.600">Bắt đầu: {formatDateTime(task.startedAt)}</Text>
                            <Text fontSize="sm" color="gray.600">Thời gian chờ: {formatMinutes(task.pendingMinutes)}</Text>
                            <Text fontSize="sm" color="gray.600">Thời gian xử lý: {formatMinutes(task.assignedMinutes)}</Text>
                          </VStack>

                          {task.queueColor === "RED" && (
                            <HStack mb={3} color="red.500" fontSize="sm">
                              <Icon as={MdPriorityHigh} />
                              <Text>Cảnh báo đỏ, ưu tiên xử lý ngay.</Text>
                            </HStack>
                          )}

                          {waitingFinal && (
                            <HStack mb={3} color="blue.500" fontSize="sm">
                              <Icon as={MdInfo} />
                              <Text>Đã duyệt kỹ thuật, đang chờ quản đốc duyệt cuối.</Text>
                            </HStack>
                          )}

                          {finalized && (
                            <HStack mb={3} color="green.500" fontSize="sm">
                              <Icon as={MdCheckCircle} />
                              <Text>Đã duyệt cuối lúc {formatDateTime(task.finalApprovedAt)}.</Text>
                            </HStack>
                          )}

                          <HStack spacing={2} flexWrap="wrap">
                            <Button
                              size="sm"
                              leftIcon={<MdPlayArrow />}
                              colorScheme="green"
                              onClick={() => handleStartDiagnosis(task)}
                              isDisabled={!canStart}
                            >
                              Bắt đầu chuẩn đoán
                            </Button>
                            <Button
                              size="sm"
                              leftIcon={<MdBuild />}
                              colorScheme="blue"
                              variant="outline"
                              onClick={() => navigate(`/workshop/technician/orders/${task.serviceOrderId}/diagnosis`)}
                            >
                              Mở phiếu chuẩn đoán
                            </Button>
                            <Button
                              size="sm"
                              leftIcon={<MdSupportAgent />}
                              colorScheme="orange"
                              variant="ghost"
                              onClick={() => {
                                setSupportTarget(task);
                                setSupportNote("");
                              }}
                            >
                              Yêu cầu hỗ trợ
                            </Button>
                          </HStack>
                        </Box>
                      );
                    })}
                  </SimpleGrid>
                )}
              </Box>
            )}

            {activeTab === TAB_REPAIR && (
              <>
                <Box>
                  <HStack mb={4}><Icon as={MdInfo} color="orange.400" /><Heading size="md" color="#2b3674">Yêu cầu sửa chữa mới</Heading></HStack>
                  {newAssignments.length === 0 ? (
                    <Box bg="white" p={10} textAlign="center" borderRadius="2xl" border="2px dashed" borderColor="gray.100"><Text color="gray.400">Không có yêu cầu sửa chữa mới.</Text></Box>
                  ) : (
                    <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} spacing={6}>
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

                <Box>
                  <HStack mb={4}><Icon as={MdBuild} color="teal.400" /><Heading size="md" color="#2b3674">Đang sửa chữa</Heading></HStack>
                  {activeAssignments.length === 0 ? (
                    <Box bg="white" p={10} textAlign="center" borderRadius="2xl" border="2px dashed" borderColor="gray.100"><Text color="gray.400">Bạn chưa bắt đầu công việc sửa chữa nào.</Text></Box>
                  ) : (
                    <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} spacing={6}>
                      {activeAssignments.map((assignment) => (
                        <AssignmentCard
                          key={readValue(assignment, "id", "Id")}
                          assignment={assignment}
                          isActive
                          onOpenDiagnosis={(orderId) => navigate(`/workshop/technician/orders/${orderId}/diagnosis`)}
                          onFinish={() => handleFinishRepair(assignment)}
                        />
                      ))}
                    </SimpleGrid>
                  )}
                </Box>
              </>
            )}
          </>
        )}
      </VStack>

      {rejectingAssignment && (
        <Modal title="Từ chối công việc" onClose={() => setRejectingAssignment(null)}>
          <VStack align="stretch" spacing={4} p={2}>
            <Box bg="orange.50" p={3} borderRadius="md" borderLeft="4px solid" borderColor="orange.400">
              <Text fontSize="sm" fontWeight="bold">Công việc: {toText(readValue(rejectingAssignment.detail, "itemName", "ItemName")) || "N/A"}</Text>
              <Text fontSize="xs" color="gray.600">Lệnh: {toText(readValue(rejectingAssignment.order, "code", "Code")) || "N/A"}</Text>
            </Box>
            <Box>
              <Text fontWeight="bold" mb={2}>Lý do từ chối *</Text>
              <Textarea value={rejectReason} onChange={(event) => setRejectReason(event.target.value)} />
            </Box>
            <HStack justify="flex-end" spacing={3}>
              <Button variant="ghost" onClick={() => setRejectingAssignment(null)}>Hủy</Button>
              <Button
                colorScheme="red"
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
            <Box bg="blue.50" p={3} borderRadius="md" borderLeft="4px solid" borderColor="blue.400">
              <Text fontSize="sm" fontWeight="bold">Lệnh: {supportTarget.serviceOrderCode}</Text>
              <Text fontSize="xs" color="gray.600">Xe: {supportTarget.vehicleLicensePlate || "--"}</Text>
            </Box>
            <Box>
              <Text fontWeight="bold" mb={2}>Nội dung cần hỗ trợ *</Text>
              <Textarea
                value={supportNote}
                onChange={(event) => setSupportNote(event.target.value)}
                placeholder="Mô tả vấn đề kỹ thuật cần tổ trưởng/quản đốc hỗ trợ..."
              />
            </Box>
            <HStack justify="flex-end" spacing={3}>
              <Button variant="ghost" onClick={() => setSupportTarget(null)}>Hủy</Button>
              <Button colorScheme="orange" isDisabled={!toText(supportNote)} onClick={handleSubmitSupport}>Gửi hỗ trợ</Button>
            </HStack>
          </VStack>
        </Modal>
      )}
    </Box>
  );
}

const AssignmentCard = ({ assignment, isActive, onConfirm, onReject, onOpenDiagnosis, onFinish }) => {
  const order = assignment.order;
  const detail = assignment.detail;
  const isDiagnosing = normalizeCode(readValue(order, "status", "Status")) === "DIAGNOSING";

  return (
    <Box bg="white" p={5} borderRadius="2xl" boxShadow="0px 8px 24px rgba(17, 38, 146, 0.04)" border="1px solid" borderColor="gray.50">
      <Flex justify="space-between" mb={4}>
        <Badge colorScheme={isActive ? "blue" : "gray"} variant="subtle" borderRadius="full" px={3}>{toText(readValue(assignment, "roleInTask", "RoleInTask")) || "Phụ trách"}</Badge>
        <Text fontSize="xs" fontWeight="bold" color="blue.500">{toText(readValue(order, "code", "Code")) || "N/A"}</Text>
      </Flex>

      <VStack align="stretch" spacing={3} mb={6}>
        <HStack spacing={3} align="center"><Icon as={MdBuild} color="blue.400" /><Text fontWeight="extrabold" fontSize="md" color="#2b3674" noOfLines={1}>{toText(readValue(detail, "itemName", "ItemName")) || "Dịch vụ không xác định"}</Text></HStack>

        <Stack spacing={2} bg="gray.50" p={3} borderRadius="xl">
          <HStack justify="space-between"><HStack spacing={2}><Icon as={MdDirectionsCar} color="gray.400" /><Text fontSize="sm" fontWeight="bold">{toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) || "N/A"}</Text></HStack><Badge variant="outline" fontSize="2xs">{toText(readValue(order, "status", "Status")) || "--"}</Badge></HStack>
          <HStack spacing={2}><Icon as={MdPerson} color="gray.400" /><Text fontSize="sm" color="gray.600">{toText(readValue(order, "customerName", "CustomerName")) || "-"}</Text></HStack>
        </Stack>

        <Box>
          <Text fontSize="xs" color="gray.400" fontWeight="bold" mb={1}>GHI CHÚ DỊCH VỤ:</Text>
          <Text fontSize="sm" color="gray.600" noOfLines={2}>{toText(readValue(detail, "note", "Note")) || toText(readValue(order, "customerNotes", "CustomerNotes")) || "Không có ghi chú"}</Text>
        </Box>
      </VStack>

      <Stack spacing={2}>
        {!isActive ? (
          <HStack spacing={3}>
            <Button flex={1} colorScheme="green" size="sm" leftIcon={<MdCheckCircle />} onClick={onConfirm}>Xác nhận</Button>
            <Button flex={1} colorScheme="red" variant="ghost" size="sm" leftIcon={<MdCancel />} onClick={onReject}>Từ chối</Button>
          </HStack>
        ) : (
          <>
            {isDiagnosing ? (
              <Button colorScheme="blue" color="white" size="sm" onClick={() => onOpenDiagnosis?.(toPositiveNumber(readValue(order, "id", "Id")))}>
                Mở màn hình chuẩn đoán
              </Button>
            ) : (
              <Button colorScheme="green" size="sm" onClick={onFinish}>Hoàn thành hạng mục</Button>
            )}
          </>
        )}
      </Stack>
    </Box>
  );
};
