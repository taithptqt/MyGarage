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
  Select,
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
} from "react-icons/md";
import { request } from "@app/apiClient";

const TAB_CHECKIN = "CHECKIN";
const TAB_CHECKOUT = "CHECKOUT";
const TAB_DIAGNOSIS = "DIAGNOSIS";
const TAB_REPAIR = "REPAIR";

export default function TechnicianDashboard() {
  const { searchQuery } = useSearch();
  const { user } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState(TAB_CHECKIN);

  const [staffId, setStaffId] = useState(null);
  const [assignments, setAssignments] = useState([]);
  const [diagnosisRequests, setDiagnosisRequests] = useState([]);
  const [checkInRequests, setCheckInRequests] = useState([]);
  const [checkOutRequests, setCheckOutRequests] = useState([]);
  const [dismissedRequestIds, setDismissedRequestIds] = useState([]);
  const [dismissedCheckInIds, setDismissedCheckInIds] = useState([]);
  const [dismissedCheckOutIds, setDismissedCheckOutIds] = useState([]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [rejectingAssignment, setRejectingAssignment] = useState(null);
  const [rejectReason, setRejectReason] = useState("");



  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [oRes, sRes, assignmentsRes, detailsRes, diagnosisRes, checkinRes, checkoutRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllStaff(),
        request({ url: "/api/service-staff-assignments", method: "GET" }),
        request({ url: "/api/service-order-details", method: "GET" }),
        serviceOrderApi.getDiagnosisRequests(),
        serviceOrderApi.getCheckInRequests(),
        serviceOrderApi.getCheckOutRequests(),
      ]);

      const orders = Array.isArray(oRes) ? oRes : [];
      const allStaff = Array.isArray(sRes) ? sRes : [];
      const allAssignments = Array.isArray(assignmentsRes) ? assignmentsRes : [];
      const allDetails = Array.isArray(detailsRes) ? detailsRes : [];
      const allDiagnosis = Array.isArray(diagnosisRes) ? diagnosisRes : [];
      const allCheckin = Array.isArray(checkinRes) ? checkinRes : [];
      const allCheckout = Array.isArray(checkoutRes) ? checkoutRes : [];

      const currentStaff = allStaff.find(
        (s) => s.email === user.email || s.fullName === user.fullName
      );

      if (!currentStaff) {
        setAssignments([]);
        setDiagnosisRequests([]);
        setCheckInRequests([]);
        setCheckOutRequests([]);
        return;
      }

      setStaffId(currentStaff.id);

      const myAssignedList = allAssignments
        .filter((a) => Number(a.technicianId) === Number(currentStaff.id))
        .filter((a) => a.status !== "REJECTED" && a.status !== "COMPLETED");

      const enrichedAssignments = myAssignedList.map((a) => {
        const detail = allDetails.find((d) => d.id === a.serviceOrderDetailId);
        const order = orders.find((o) => o.id === detail?.serviceOrderId);
        return { ...a, detail, order };
      });

      setAssignments(enrichedAssignments.filter((a) => a.order && !["CANCELLED", "COMPLETED"].includes(a.order.status)));

      const diagGroups = new Map();
      allDiagnosis
        .filter((d) => ["PENDING", "ASSIGNED"].includes(String(d.status || "").toUpperCase()))
        .forEach((d) => {
          const key = Number(d.serviceOrderId);
          if (!diagGroups.has(key)) diagGroups.set(key, []);
          diagGroups.get(key).push(d);
        });

      const diagRequests = Array.from(diagGroups.entries())
        .map(([orderId, items]) => {
          const order = orders.find((o) => Number(o.id) === Number(orderId));
          if (!order) return null;
          const hasAssigned = items.some((x) => String(x.status || "").toUpperCase() === "ASSIGNED");
          const isAssignedToMe = items.some(
            (x) => String(x.status || "").toUpperCase() === "ASSIGNED" && Number(x.technicianId) === Number(currentStaff.id)
          );
          return {
            serviceOrderId: orderId,
            order,
            diagnoses: items.sort((a, b) => Number(a.id) - Number(b.id)),
            status: hasAssigned ? "ASSIGNED" : "PENDING",
            isAssignedToMe,
            isAssignedToOther: hasAssigned && !isAssignedToMe,
          };
        })
        .filter(Boolean);

      setDiagnosisRequests(diagRequests);

      const checkinRequestsMapped = allCheckin
        .filter((x) => ["PENDING", "ASSIGNED"].includes(String(x.dashboardWarningLights || "").toUpperCase()))
        .map((x) => {
          const order = orders.find((o) => Number(o.id) === Number(x.serviceOrderId));
          const status = String(x.dashboardWarningLights || "").toUpperCase();
          return {
            request: x,
            order,
            status,
            isAssignedToMe: Number(x.inspectorId) === Number(currentStaff.id),
            isAssignedToOther: status === "ASSIGNED" && Number(x.inspectorId) !== Number(currentStaff.id),
          };
        })
        .filter((x) => x.order);

      setCheckInRequests(checkinRequestsMapped);

      const checkoutRequestsMapped = allCheckout
        .filter((x) => ["PENDING", "ASSIGNED"].includes(String(x.dashboardWarningLights || "").toUpperCase()))
        .map((x) => {
          const order = orders.find((o) => Number(o.id) === Number(x.serviceOrderId));
          const status = String(x.dashboardWarningLights || "").toUpperCase();
          return {
            request: x,
            order,
            status,
            isAssignedToMe: Number(x.inspectorId) === Number(currentStaff.id),
            isAssignedToOther: status === "ASSIGNED" && Number(x.inspectorId) !== Number(currentStaff.id),
          };
        })
        .filter((x) => x.order);

      setCheckOutRequests(checkoutRequestsMapped);
    } catch (err) {
      setError("Không thể tải dữ liệu kỹ thuật viên: " + err.message);
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleUpdateAssignmentStatus = async (assignmentId, newStatus, reason = "") => {
    try {
      const assignment = assignments.find((a) => a.id === assignmentId);
      if (!assignment) return;

      const payload = {
        ...assignment,
        status: newStatus,
        pauseLog: reason || assignment.pauseLog,
        updatedBy: staffId,
        updatedAt: new Date().toISOString(),
      };

      delete payload.detail;
      delete payload.order;

      await serviceOrderApi.updateAssignment(assignmentId, payload);

      toast({
        title: newStatus === "REJECTED" ? "Đã từ chối công việc" : "Đã xác nhận công việc",
        status: newStatus === "REJECTED" ? "warning" : "success",
        duration: 3000,
        isClosable: true,
      });

      fetchData();
    } catch (err) {
      toast({ title: "Lỗi cập nhật", description: err.message, status: "error" });
    }
  };

  const handleFinishRepair = async (assignment) => {
    await handleUpdateAssignmentStatus(assignment.id, "COMPLETED");
  };

  const handleAcceptDiagnosis = async (requestItem) => {
    try {
      await serviceOrderApi.acceptDiagnosisRequest(requestItem.order.id, staffId);
      toast({ title: "Đã nhận yêu cầu chuẩn đoán", status: "success", duration: 2500 });
      await fetchData();
    } catch (err) {
      toast({ title: "Không thể nhận yêu cầu", description: err.message, status: "error" });
    }
  };

  const handleAcceptCheckIn = async (requestItem) => {
    try {
      await serviceOrderApi.acceptCheckInRequest(requestItem.order.id, staffId);
      toast({ title: "Đã nhận yêu cầu check-in", status: "success", duration: 2500 });
      await fetchData();
    } catch (err) {
      toast({ title: "Không thể nhận yêu cầu check-in", description: err.message, status: "error" });
    }
  };

  const handleAcceptCheckOut = async (requestItem) => {
    try {
      await serviceOrderApi.acceptCheckOutRequest(requestItem.order.id, staffId);
      toast({ title: "Đã nhận yêu cầu checkout", status: "success", duration: 2500 });
      await fetchData();
    } catch (err) {
      toast({ title: "Không thể nhận yêu cầu checkout", description: err.message, status: "error" });
    }
  };



  const filteredAssignments = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return assignments;
    return assignments.filter(
      (a) =>
        (a.order?.code || "").toLowerCase().includes(query) ||
        (a.order?.customerName || "").toLowerCase().includes(query) ||
        (a.detail?.itemName || "").toLowerCase().includes(query)
    );
  }, [assignments, searchQuery]);

  const filteredDiagnosisRequests = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    return diagnosisRequests
      .filter((x) => !dismissedRequestIds.includes(x.serviceOrderId))
      .filter((x) => {
        if (!query) return true;
        return (
          (x.order?.code || "").toLowerCase().includes(query) ||
          (x.order?.vehicleLicensePlate || "").toLowerCase().includes(query) ||
          (x.order?.customerName || "").toLowerCase().includes(query)
        );
      });
  }, [diagnosisRequests, dismissedRequestIds, searchQuery]);

  const filteredCheckInRequests = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    return checkInRequests
      .filter((x) => !dismissedCheckInIds.includes(x.request.id))
      .filter((x) => {
        if (!query) return true;
        return (
          (x.order?.code || "").toLowerCase().includes(query) ||
          (x.order?.vehicleLicensePlate || "").toLowerCase().includes(query) ||
          (x.order?.customerName || "").toLowerCase().includes(query)
        );
      });
  }, [checkInRequests, dismissedCheckInIds, searchQuery]);

  const filteredCheckOutRequests = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    return checkOutRequests
      .filter((x) => !dismissedCheckOutIds.includes(x.request.id))
      .filter((x) => {
        if (!query) return true;
        return (
          (x.order?.code || "").toLowerCase().includes(query) ||
          (x.order?.vehicleLicensePlate || "").toLowerCase().includes(query) ||
          (x.order?.customerName || "").toLowerCase().includes(query)
        );
      });
  }, [checkOutRequests, dismissedCheckOutIds, searchQuery]);

  const newAssignments = filteredAssignments.filter(
    (a) => !a.status || a.status === "ASSIGNED" || a.status === "NEW"
  );
  const activeAssignments = filteredAssignments.filter(
    (a) => a.status === "IN_PROGRESS" || a.status === "CONFIRMED"
  );

  return (
    <Box p={6} bg="#f4f7fe" minH="100vh">
      <VStack align="stretch" spacing={8}>
        <Box>
          <Heading size="lg" color="#2b3674" mb={2}>Bảng điều khiển Kỹ thuật viên</Heading>
        </Box>

        <HStack spacing={3} align="center" flexWrap="wrap" sx={{ "& > button": { h: "44px", minH: "44px", px: 6, borderRadius: "full", display: "inline-flex", alignItems: "center", justifyContent: "center", lineHeight: "1", fontWeight: 700, mt: 0, mb: 0 } }}>
          <Button variant={activeTab === TAB_CHECKIN ? "solid" : "outline"} colorScheme="blue" onClick={() => setActiveTab(TAB_CHECKIN)}>
            Check-in ({filteredCheckInRequests.length})
          </Button>
          <Button variant={activeTab === TAB_CHECKOUT ? "solid" : "outline"} colorScheme="orange" onClick={() => setActiveTab(TAB_CHECKOUT)}>
            Check-out ({filteredCheckOutRequests.length})
          </Button>
          <Button variant={activeTab === TAB_DIAGNOSIS ? "solid" : "outline"} colorScheme="purple" onClick={() => setActiveTab(TAB_DIAGNOSIS)}>
            Chuẩn đoán ({filteredDiagnosisRequests.length})
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
            {activeTab === TAB_CHECKIN && (
              <Box>
                <HStack mb={4}><Icon as={MdInfo} color="blue.400" /><Heading size="md" color="#2b3674">Yêu cầu check-in</Heading></HStack>
                {filteredCheckInRequests.length === 0 ? (
                  <Box bg="white" p={10} textAlign="center" borderRadius="2xl" border="2px dashed" borderColor="gray.100"><Text color="gray.400">Không có yêu cầu check-in.</Text></Box>
                ) : (
                  <VStack align="stretch" spacing={3}>
                    {filteredCheckInRequests.map((item) => (
                      <HStack
                        key={item.request.id}
                        bg="white"
                        p={4}
                        borderRadius="2xl"
                        border="1px solid"
                        borderColor="gray.200"
                        boxShadow="sm"
                        _hover={item.isAssignedToMe ? { borderColor: "blue.200", boxShadow: "0 8px 24px rgba(43,54,116,0.08)" } : undefined}
                        transition="all 0.2s ease"
                        justify="space-between"
                        align="center"
                        flexWrap="wrap"
                        gap={3}
                        cursor={item.isAssignedToMe ? "pointer" : "default"}
                        onClick={() => {
                          if (item.isAssignedToMe) {
                            navigate(`/workshop/technician/orders/${item.order.id}/condition?phase=CHECK_IN`);
                          }
                        }}
                      >
                        <HStack spacing={6} flex="1" minW={0} align="center" flexWrap="wrap">
                          <Box minW="170px">
                            <Text fontSize="xs" color="gray.500" mb={0.5}>Mã lệnh</Text>
                            <Text fontWeight="bold" color="#2b3674">{item.order?.code}</Text>
                          </Box>
                          <HStack spacing={2} minW="190px" align="start">
                            <Icon as={MdDirectionsCar} color="gray.500" />
                            <Box>
                              <Text fontSize="xs" color="gray.500" mb={0.5}>Biển số</Text>
                              <Text fontWeight="bold">{item.order?.vehicleLicensePlate || "N/A"}</Text>
                            </Box>
                          </HStack>
                          <HStack spacing={2} minW="220px" flex="1" minH="32px" align="start">
                            <Icon as={MdPerson} color="gray.500" />
                            <Box minW={0}>
                              <Text fontSize="xs" color="gray.500" mb={0.5}>Khách hàng</Text>
                              <Text noOfLines={1}>{item.order?.customerName || "-"}</Text>
                            </Box>
                          </HStack>
                        </HStack>

                        <HStack spacing={2} minW={{ base: "100%", md: "150px" }} justify={{ base: "flex-start", md: "flex-end" }} onClick={(e) => e.stopPropagation()}>
                          {item.status === "PENDING" ? (
                            <Button colorScheme="green" size="sm" minW="132px" onClick={() => handleAcceptCheckIn(item)}>Nhận yêu cầu</Button>
                          ) : item.isAssignedToMe ? (
                            <Text fontSize="sm" color="blue.500" fontWeight="700">Nhấn vào dòng để check-in</Text>
                          ) : (
                            <Button size="sm" isDisabled>Đã có thợ khác nhận</Button>
                          )}
                        </HStack>
                      </HStack>
                    ))}
                  </VStack>
                )}
              </Box>
            )}

            {activeTab === TAB_CHECKOUT && (
              <Box>
                <HStack mb={4}><Icon as={MdInfo} color="orange.400" /><Heading size="md" color="#2b3674">Yêu cầu checkout</Heading></HStack>
                {filteredCheckOutRequests.length === 0 ? (
                  <Box bg="white" p={10} textAlign="center" borderRadius="2xl" border="2px dashed" borderColor="gray.100"><Text color="gray.400">Không có yêu cầu checkout.</Text></Box>
                ) : (
                  <VStack align="stretch" spacing={3}>
                    {filteredCheckOutRequests.map((item) => (
                      <HStack
                        key={item.request.id}
                        bg="white"
                        p={4}
                        borderRadius="2xl"
                        border="1px solid"
                        borderColor="gray.200"
                        boxShadow="sm"
                        _hover={item.isAssignedToMe ? { borderColor: "orange.200", boxShadow: "0 8px 24px rgba(43,54,116,0.08)" } : undefined}
                        transition="all 0.2s ease"
                        justify="space-between"
                        align="center"
                        flexWrap="wrap"
                        gap={3}
                        cursor={item.isAssignedToMe ? "pointer" : "default"}
                        onClick={() => {
                          if (item.isAssignedToMe) {
                            navigate(`/workshop/technician/orders/${item.order.id}/condition?phase=CHECK_OUT`);
                          }
                        }}
                      >
                        <HStack spacing={6} flex="1" minW={0} align="center" flexWrap="wrap">
                          <Box minW="170px">
                            <Text fontSize="xs" color="gray.500" mb={0.5}>Mã lệnh</Text>
                            <Text fontWeight="bold" color="#2b3674">{item.order?.code}</Text>
                          </Box>
                          <HStack spacing={2} minW="190px" align="start">
                            <Icon as={MdDirectionsCar} color="gray.500" />
                            <Box>
                              <Text fontSize="xs" color="gray.500" mb={0.5}>Biển số</Text>
                              <Text fontWeight="bold">{item.order?.vehicleLicensePlate || "N/A"}</Text>
                            </Box>
                          </HStack>
                          <HStack spacing={2} minW="220px" flex="1" minH="32px" align="start">
                            <Icon as={MdPerson} color="gray.500" />
                            <Box minW={0}>
                              <Text fontSize="xs" color="gray.500" mb={0.5}>Khách hàng</Text>
                              <Text noOfLines={1}>{item.order?.customerName || "-"}</Text>
                            </Box>
                          </HStack>
                        </HStack>

                        <HStack spacing={2} minW={{ base: "100%", md: "150px" }} justify={{ base: "flex-start", md: "flex-end" }} onClick={(e) => e.stopPropagation()}>
                          {item.status === "PENDING" ? (
                            <Button colorScheme="green" size="sm" minW="132px" onClick={() => handleAcceptCheckOut(item)}>Nhận yêu cầu</Button>
                          ) : item.isAssignedToMe ? (
                            <Text fontSize="sm" color="orange.500" fontWeight="700">Nhấn vào dòng để checkout</Text>
                          ) : (
                            <Button size="sm" isDisabled>Đã có thợ khác nhận</Button>
                          )}
                        </HStack>
                      </HStack>
                    ))}
                  </VStack>
                )}
              </Box>
            )}

            {activeTab === TAB_DIAGNOSIS && (
              <Box>
                <HStack mb={4}><Icon as={MdInfo} color="purple.400" /><Heading size="md" color="#2b3674">Yêu cầu chuẩn đoán</Heading></HStack>
                {filteredDiagnosisRequests.length === 0 ? (
                  <Box bg="white" p={10} textAlign="center" borderRadius="2xl" border="2px dashed" borderColor="gray.100"><Text color="gray.400">Không có yêu cầu chuẩn đoán.</Text></Box>
                ) : (
                  <VStack align="stretch" spacing={3}>
                    {filteredDiagnosisRequests.map((item) => {
                      const status = String(item.status || "").toUpperCase();
                      return (
                        <HStack
                          key={item.serviceOrderId}
                          bg="white"
                          p={4}
                          borderRadius="2xl"
                          border="1px solid"
                          borderColor="gray.200"
                          boxShadow="sm"
                          _hover={{ borderColor: "purple.200", boxShadow: "0 8px 24px rgba(43,54,116,0.08)" }}
                          transition="all 0.2s ease"
                          justify="space-between"
                          align="center"
                          flexWrap="wrap"
                          gap={3}
                          cursor={item.isAssignedToMe ? "pointer" : "default"}
                          onClick={() => {
                            if (item.isAssignedToMe) {
                              navigate(`/workshop/technician/orders/${item.serviceOrderId}/diagnosis`);
                            }
                          }}
                        >
                          <HStack spacing={6} flex="1" minW={0} align="center" flexWrap="wrap">
                            <Box minW="170px">
                              <Text fontSize="xs" color="gray.500" mb={0.5}>Mã lệnh</Text>
                              <Text fontWeight="bold" color="#2b3674">{item.order?.code}</Text>
                            </Box>
                            <HStack spacing={2} minW="190px" align="start">
                              <Icon as={MdDirectionsCar} color="gray.500" />
                              <Box>
                                <Text fontSize="xs" color="gray.500" mb={0.5}>Biển số</Text>
                                <Text fontWeight="bold">{item.order?.vehicleLicensePlate || "N/A"}</Text>
                              </Box>
                            </HStack>
                            <HStack spacing={2} minW="220px" flex="1" minH="32px" align="start">
                              <Icon as={MdPerson} color="gray.500" />
                              <Box minW={0}>
                                <Text fontSize="xs" color="gray.500" mb={0.5}>Khách hàng</Text>
                                <Text noOfLines={1}>{item.order?.customerName || "-"}</Text>
                              </Box>
                            </HStack>
                          </HStack>

                          <HStack
                            spacing={2}
                            minW={{ base: "100%", md: "150px" }}
                            justify={{ base: "flex-start", md: "flex-end" }}
                            onClick={(e) => e.stopPropagation()}
                          >
                            {status === "PENDING" ? (
                              <Button
                                colorScheme="green"
                                size="sm"
                                minW="132px"
                                onClick={() => handleAcceptDiagnosis(item)}
                              >
                                Nhận yêu cầu
                              </Button>
                            ) : item.isAssignedToMe ? (
                              <Text fontSize="sm" color="gray.500">Nhấn vào dòng để mở</Text>
                            ) : (
                              <Button size="sm" isDisabled>Đã có thợ khác nhận</Button>
                            )}
                          </HStack>
                        </HStack>
                      );
                    })}
                  </VStack>
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
                      {newAssignments.map((a) => (
                        <AssignmentCard key={a.id} assignment={a} onConfirm={() => handleUpdateAssignmentStatus(a.id, "IN_PROGRESS")} onReject={() => setRejectingAssignment(a)} />
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
                      {activeAssignments.map((a) => (
                        <AssignmentCard key={a.id} assignment={a} isActive onDiagnosis={(orderId) => navigate(`/workshop/technician/orders/${orderId}/condition`)} onFinish={() => handleFinishRepair(a)} />
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
              <Text fontSize="sm" fontWeight="bold">Công việc: {rejectingAssignment.detail?.itemName}</Text>
              <Text fontSize="xs" color="gray.600">Lệnh: {rejectingAssignment.order?.code}</Text>
            </Box>
            <Box>
              <Text fontWeight="bold" mb={2}>Lý do từ chối *</Text>
              <Textarea value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} />
            </Box>
            <HStack justify="flex-end" spacing={3}>
              <Button variant="ghost" onClick={() => setRejectingAssignment(null)}>Hủy</Button>
              <Button colorScheme="red" isDisabled={!rejectReason.trim()} onClick={() => {
                handleUpdateAssignmentStatus(rejectingAssignment.id, "REJECTED", rejectReason);
                setRejectingAssignment(null);
                setRejectReason("");
              }}>
                Gửi lý do & Từ chối
              </Button>
            </HStack>
          </VStack>
        </Modal>
      )}


    </Box>
  );
}

const AssignmentCard = ({ assignment, isActive, onConfirm, onReject, onDiagnosis, onFinish }) => {
  const { order, detail } = assignment;
  const isDiagnosing = order?.status === "DIAGNOSING";

  return (
    <Box bg="white" p={5} borderRadius="2xl" boxShadow="0px 8px 24px rgba(17, 38, 146, 0.04)" border="1px solid" borderColor="gray.50" position="relative">
      <Flex justify="space-between" mb={4}>
        <Badge colorScheme={isActive ? "blue" : "gray"} variant="subtle" borderRadius="full" px={3}>{assignment.roleInTask || "Phụ trách"}</Badge>
        <Text fontSize="xs" fontWeight="bold" color="blue.500">{order?.code}</Text>
      </Flex>

      <VStack align="stretch" spacing={3} mb={6}>
        <HStack spacing={3} align="center" flexWrap="wrap" sx={{ "& > button": { h: "44px", minH: "44px", px: 6, borderRadius: "full", display: "inline-flex", alignItems: "center", justifyContent: "center", lineHeight: "1", fontWeight: 700, mt: 0, mb: 0 } }}><Icon as={MdBuild} color="blue.400" /><Text fontWeight="extrabold" fontSize="md" color="#2b3674" noOfLines={1}>{detail?.itemName || "Dịch vụ không xác định"}</Text></HStack>

        <Stack spacing={2} bg="gray.50" p={3} borderRadius="xl">
          <HStack justify="space-between"><HStack spacing={2}><Icon as={MdDirectionsCar} color="gray.400" /><Text fontSize="sm" fontWeight="bold">{order?.vehicleLicensePlate || "N/A"}</Text></HStack><Badge variant="outline" fontSize="2xs">{order?.status}</Badge></HStack>
          <HStack spacing={2}><Icon as={MdPerson} color="gray.400" /><Text fontSize="sm" color="gray.600">{order?.customerName || "-"}</Text></HStack>
        </Stack>

        <Box>
          <Text fontSize="xs" color="gray.400" fontWeight="bold" mb={1}>GHI CHÚ DỊCH VỤ:</Text>
          <Text fontSize="sm" color="gray.600" noOfLines={2}>{detail?.note || order?.customerNotes || "Không có ghi chú"}</Text>
        </Box>
      </VStack>

      <Stack spacing={2}>
        {!isActive ? (
          <HStack spacing={3} align="center" flexWrap="wrap" sx={{ "& > button": { h: "44px", minH: "44px", px: 6, borderRadius: "full", display: "inline-flex", alignItems: "center", justifyContent: "center", lineHeight: "1", fontWeight: 700, mt: 0, mb: 0 } }}>
            <Button flex={1} colorScheme="green" size="sm" leftIcon={<MdCheckCircle />} onClick={onConfirm}>Xác nhận</Button>
            <Button flex={1} colorScheme="red" variant="ghost" size="sm" leftIcon={<MdCancel />} onClick={onReject}>Từ chối</Button>
          </HStack>
        ) : (
          <>
            {isDiagnosing ? (
              <Button colorScheme="blue" color="white" size="sm" onClick={() => onDiagnosis(order.id)}>Kiểm tra tình trạng xe</Button>
            ) : (
              <Button colorScheme="green" size="sm" onClick={onFinish}>Hoàn thành hạng mục</Button>
            )}
            <Button variant="outline" size="sm">Chi tiết xe</Button>
          </>
        )}
      </Stack>
    </Box>
  );
};









