import React, { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { Modal } from "@features/admin/components/AdminModals";
import {
  Badge,
  Box,
  Button,
  Divider,
  Grid,
  GridItem,
  Heading,
  HStack,
  Icon,
  Text,
  VStack,
} from "@chakra-ui/react";
import { MdAssignment, MdCheckCircle, MdDirectionsCar, MdHistory, MdPerson, MdEdit } from "react-icons/md";
import { formatOrderDateTime, parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";

export default function ServiceOrderDetailModal({ order, onClose, onRefresh, customers = [], vehicles = [], staff = [] }) {
  const navigate = useNavigate();

  const customer = customers.find((c) => c.id === order.customerId);
  const vehicle = vehicles.find((v) => v.id === order.vehicleId);
  const advisor = staff.find((s) => s.id === order.serviceAdvisorId);
  const parsedNotes = parseCustomerNotes(order?.customerNotes);
  const symptomNoteItems = (() => {
    try {
      const parsed = JSON.parse(String(order?.customerNotes || "").trim());
      const list = Array.isArray(parsed)
        ? parsed
        : Array.isArray(parsed?.symptoms)
          ? parsed.symptoms
          : [];
      return list.map((x) => String(x || "").trim()).filter(Boolean);
    } catch {
      return [];
    }
  })();

  const handleOpenPage = (path) => {
    onClose?.();
    navigate(path);
  };

  const handleRequestDiagnosis = async () => {
    if (!window.confirm(`Gửi yêu cầu chuẩn đoán cho phương tiện ${vehicle?.licensePlate || "N/A"} tơi Kỹ thuật viên?`)) return;
    try {
      await serviceOrderApi.requestDiagnosis(order.id);
      if (onRefresh) await onRefresh();
      const diagRes = await serviceOrderApi.getDiagnoses(order.id);
      setDiagnoses(Array.isArray(diagRes) ? diagRes : []);
    } catch (err) {
      alert("Lđ-i khi yêu cầu chuẩn đoán: " + err.message);
    }
  };

  const handleRecallDiagnosis = async () => {
    if (!window.confirm("Thu hôi yêu cầu chuẩn đoán?")) return;
    try {
      await serviceOrderApi.recallDiagnosisRequest(order.id);
      if (onRefresh) await onRefresh();
      const diagRes = await serviceOrderApi.getDiagnoses(order.id);
      setDiagnoses(Array.isArray(diagRes) ? diagRes : []);
    } catch (err) {
      alert("Lđ-i khi thu hôi yêu cầu chuẩn đoán: " + err.message);
    }
  };

  const [orderDetails, setOrderDetails] = useState([]);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [diagnoses, setDiagnoses] = useState([]);
  const [hasSignedCheckIn, setHasSignedCheckIn] = useState(false);
  const customerChosenDetails = useMemo(() => {
    const isLaborOrService = (d) => {
      const t = String(d?.itemType || "").toUpperCase();
      return t === "LABOR" || t === "SERVICE";
    };
    const candidates = (orderDetails || []).filter(
      (d) => isLaborOrService(d) && !d?.diagnoseRecommendationId
    );

    const uniqueMap = new Map();
    candidates.forEach((d) => {
      const key =
        d?.standardServiceId ? `std-${d.standardServiceId}` :
        d?.itemId ? `item-${d.itemId}` :
        `name-${String(d?.itemName || "").trim().toLowerCase()}`;
      if (!uniqueMap.has(key)) uniqueMap.set(key, d);
    });
    return Array.from(uniqueMap.values());
  }, [orderDetails]);

  useEffect(() => {
    const fetchDetails = async () => {
      setLoadingDetails(true);
      try {
        const [details, diagRes, conditionLogs] = await Promise.all([
          serviceOrderApi.getDetails(order.id),
          serviceOrderApi.getDiagnoses(order.id),
          vehicleConditionLogApi.getByServiceOrderId(order.id),
        ]);
        setOrderDetails(details || []);
        setDiagnoses(Array.isArray(diagRes) ? diagRes : []);
        const signedCheckInExists = Array.isArray(conditionLogs) && conditionLogs.some((log) => {
          const type = String(log?.logType || log?.LogType || "").toUpperCase();
          const signature = String(log?.customerSignatureUrl || log?.CustomerSignatureUrl || "").trim();
          return type === "CHECK_IN" && signature.length > 0;
        });
        setHasSignedCheckIn(signedCheckInExists);
      } catch (err) {
        console.error("Lđ-i tải chi tiết lệnh:", err);
        setHasSignedCheckIn(false);
      } finally {
        setLoadingDetails(false);
      }
    };
    if (order?.id) fetchDetails();
  }, [order?.id]);

  const isFromAppt = !!order.appointmentId;
  const isGuest = !order.appointmentId && customer && customer.isRegistered === false;
  const isReception = !order.appointmentId && (!customer || customer.isRegistered !== false);

  const latestDiagnosis = [...diagnoses].sort((a, b) => {
    const at = new Date(a?.createdAt || a?.updatedAt || 0).getTime();
    const bt = new Date(b?.createdAt || b?.updatedAt || 0).getTime();
    return bt - at;
  })[0] || null;
  const diagnosisStatus = String(latestDiagnosis?.status || "").toUpperCase();
  const isDiagnosisRequested = diagnosisStatus === "PENDING" || diagnosisStatus === "ASSIGNED";
  const isDiagnosisAssigned = diagnosisStatus === "ASSIGNED";
  const isDiagnosisCompleted = diagnosisStatus === "COMPLETED";
  let sourceText = "Không xác định";
  let sourceColor = "gray";
  let sourceIcon = MdAssignment;

  if (isFromAppt) {
    sourceText = "Từ Lịch hẹn trươc";
    sourceColor = "blue";
    sourceIcon = MdHistory;
  } else if (isGuest) {
    sourceText = "Khách vãng lai (Tạo nhanh)";
    sourceColor = "orange";
    sourceIcon = MdPerson;
  } else if (isReception) {
    sourceText = "Lễ tân tiếp nhận trực tiếp";
    sourceColor = "teal";
    sourceIcon = MdAssignment;
  }

  return (
    <Modal
      title={
        <HStack spacing={4} align="center">
          <Text>{`Chi tiết lệnh sửa chữa: ${order.code}`}</Text>
          <Button
            size="sm"
            fontSize="sm"
            leftIcon={<MdEdit />}
            colorScheme="yellow"
            variant="solid"
            onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/edit`)}
            px={4}
            boxShadow="0 4px 12px rgba(0,0,0,0.1)"
          >
            Chệnh sửa
          </Button>
        </HStack>
      }
      onClose={onClose}
      className="modal-lg always-dark"
    >
      <VStack align="stretch" spacing={6} px={8} pb={8} pt={2}>
        <HStack justify="space-between" bg="whiteAlpha.100" p={5} borderRadius="2xl" border="1px solid" borderColor="whiteAlpha.200">
          <VStack align="start" spacing={1}>
            <Text fontSize="sm" color="whiteAlpha.600" fontWeight="bold" textTransform="uppercase" letterSpacing="wider">
              Trạng thái hiện tại
            </Text>
            <Badge
              colorScheme={
                order.status === "NEW" || order.status === "RECEPTION"
                  ? "gray"
                  : order.status === "INSPECTING"
                    ? "purple"
                    : order.status === "WAITING_APPROVAL"
                      ? "orange"
                      : order.status === "REPAIRING"
                        ? "blue"
                        : order.status === "QC_PENDING"
                          ? "teal"
                          : order.status === "READY" || order.status === "COMPLETED"
                            ? "green"
                            : order.status === "CANCELLED"
                              ? "red"
                              : "blue"
              }
              variant="solid"
              fontSize="md"
              px={4}
              py={1.5}
              boxShadow="0 4px 12px rgba(0,0,0,0.1)"
            >
              {order.status}
            </Badge>
          </VStack>
          <VStack align="end" spacing={1}>
            <Text fontSize="sm" color="whiteAlpha.600" fontWeight="bold" textTransform="uppercase" letterSpacing="wider">
              Loại dịch vụ
            </Text>
            <Text fontWeight="extrabold" fontSize="lg" color="blue.300">
              {order.orderCategory || "REPAIR"}
            </Text>
          </VStack>
        </HStack>

        <Grid templateColumns="repeat(2, 1fr)" gap={6}>
          <GridItem>
            <VStack align="start" spacing={4}>
              <HStack color="blue.300">
                <Icon as={MdPerson} boxSize={5} />
                <Heading size="md" letterSpacing="tight">Thông tin khách hàng</Heading>
              </HStack>
              <Box pl={9}>
                <Text fontWeight="bold" fontSize="lg" color="white">
                  {order.customerName || customer?.fullName || "-"}
                </Text>
                <Text color="whiteAlpha.700" fontSize="md">
                  {order.customerPhone || customer?.phone || "-"}
                </Text>
              </Box>
            </VStack>
          </GridItem>

          <GridItem>
            <VStack align="start" spacing={4}>
              <HStack color="blue.300">
                <Icon as={MdDirectionsCar} boxSize={5} />
                <Heading size="md" letterSpacing="tight">Thông tin phương tiện</Heading>
              </HStack>
              <Box pl={9}>
                <HStack spacing={3} align="center" flexWrap="wrap">
                  <Text fontWeight="bold" fontSize="lg" color="white" textTransform="uppercase">
                    {order.vehicleLicensePlate || vehicle?.licensePlate || "N/A"}
                  </Text>
                  <Button
                    size="sm"
                    fontSize="sm"
                    colorScheme="cyan"
                    onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`)}
                  >
                    Nhật ký xe
                  </Button>
                </HStack>
                <Text color="whiteAlpha.700" fontSize="md">
                  {vehicle?.modelName || "-"} {vehicle?.color ? `- ${vehicle.color}` : ""}
                </Text>
                {vehicle?.vin ? <Text fontSize="xs" color="whiteAlpha.500" mt={1}>VIN: {vehicle.vin}</Text> : null}
              </Box>
            </VStack>
          </GridItem>
        </Grid>

        <Divider />

        <Grid templateColumns="repeat(4, 1fr)" gap={8} py={2}>
          <VStack align="start" spacing={2}>
            <HStack color="whiteAlpha.500" fontSize="xs" fontWeight="bold" letterSpacing="widest">
              <Icon as={MdHistory} />
              <Text textTransform="uppercase">Thời điểm vào</Text>
            </HStack>
            <Text fontSize="md" color="white" fontWeight="medium">{formatOrderDateTime(order.checkInTime)}</Text>
          </VStack>
          <VStack align="start" spacing={2}>
            <HStack color="whiteAlpha.500" fontSize="xs" fontWeight="bold" letterSpacing="widest">
              <Icon as={MdHistory} />
              <Text textTransform="uppercase">Hẹn trả xe</Text>
            </HStack>
            <Text fontSize="md" color={order.promisedHandoverTime ? "blue.300" : "whiteAlpha.400"} fontWeight="medium">
              {formatOrderDateTime(order.promisedHandoverTime)}
            </Text>
          </VStack>
          <VStack align="start" spacing={2}>
            <HStack color="whiteAlpha.500" fontSize="xs" fontWeight="bold" letterSpacing="widest">
              <Icon as={MdHistory} />
              <Text textTransform="uppercase">Dự kiến hoàn thành</Text>
            </HStack>
            <Text fontSize="md" color={order.estimatedHandover ? "blue.300" : "whiteAlpha.400"} fontWeight="medium">
              {formatOrderDateTime(order.estimatedHandover)}
            </Text>
          </VStack>
          <VStack align="start" spacing={2}>
            <HStack color="whiteAlpha.500" fontSize="xs" fontWeight="bold" letterSpacing="widest">
              <Icon as={MdCheckCircle} />
              <Text textTransform="uppercase">Cđ vấn dịch vụ</Text>
            </HStack>
            <Text fontSize="md" color="white" fontWeight="medium">{advisor?.fullName || "-"}</Text>
          </VStack>
        </Grid>

        <Divider />

        <VStack align="start" spacing={4}>
          <HStack color="blue.300">
            <Icon as={MdAssignment} boxSize={5} />
            <Heading size="md" letterSpacing="tight">Ghi chú & Nguôn gđc tiếp nhận</Heading>
          </HStack>
          <Box w="100%" pl={9}>
            <VStack align="stretch" spacing={3}>

              {/* Nguôn Gđc Lệnh */}
              <HStack
                bg={`${sourceColor}.900`}
                p={2}
                px={3}
                borderRadius="lg"
                border="1px solid"
                borderColor={`${sourceColor}.400`}
                w="fit-content"
              >
                <Icon as={sourceIcon} color={`${sourceColor}.300`} />
                <Text fontSize="xs" color={`${sourceColor}.100`} fontWeight="bold">
                  {sourceText.toUpperCase()}
                </Text>
              </HStack>

              {/* Dịch vụ cụ thể (Service Details) */}
              <Box p={4} bg="whiteAlpha.50" borderRadius="xl" borderLeft="4px solid" borderColor="purple.400">
                <Text fontSize="xs" color="whiteAlpha.500" fontWeight="bold" mb={2} letterSpacing="wider">
                  DỊCH VỤ CỤ THỂ KHÁCH ĐÃ CHỌN:
                </Text>
                {loadingDetails ? (
                  <Text fontSize="sm" color="whiteAlpha.400" fontStyle="italic">Đang tải...</Text>
                ) : customerChosenDetails.length > 0 ? (
                  <VStack align="start" spacing={2}>
                    {customerChosenDetails.map((d) => (
                      <HStack key={d.id} bg="whiteAlpha.100" px={3} py={1} borderRadius="md" w="100%" justify="space-between">
                        <Text fontSize="md" color="white" fontWeight="medium">{d.itemName}</Text>
                        <Text fontSize="sm" color="whiteAlpha.700" fontWeight="bold">
                          {(d.finalAmount || d.unitPriceAtTime || 0).toLocaleString()}đ
                        </Text>
                      </HStack>
                    ))}
                  </VStack>
                ) : parsedNotes.requestedServices ? (
                  <Text fontSize="md" color="white" fontWeight="medium">{parsedNotes.requestedServices}</Text>
                ) : (
                  <Text fontSize="sm" color="whiteAlpha.400" fontStyle="italic">Chưa có dịch vụ cụ thể nào được chọn.</Text>
                )}
              </Box>

              {/* Ghi chú vển bản */}
              <Box p={4} bg="whiteAlpha.50" borderRadius="xl" borderLeft="4px solid" borderColor="blue.400">
                <Text fontSize="xs" color="whiteAlpha.500" fontWeight="bold" mb={2} letterSpacing="wider">
                  GHI CHđs / YđSU CẦU THđSM:
                </Text>
                {symptomNoteItems.length > 0 ? (
                  <VStack align="start" spacing={1} pl={2}>
                    {symptomNoteItems.map((item, index) => (
                      <Text key={`${item}-${index}`} fontSize="md" color="white">
                        ệđ {item}
                      </Text>
                    ))}
                  </VStack>
                ) : (
                  <Text fontSize="md" fontStyle="italic" color="white">
                    {String(parsedNotes.mainNotes || "").toLowerCase().includes("không có")
                      ? "Không có ghi chú gì"
                      : `ệđ ${parsedNotes.mainNotes}`}
                  </Text>
                )}
              </Box>

            </VStack>
          </Box>
        </VStack>

        <Divider />

        <VStack align="start" spacing={4}>
          <HStack color="blue.300">
            <Icon as={MdAssignment} boxSize={5} />
            <Heading size="md" letterSpacing="tight">Xem chi tiết theo từng mục</Heading>
          </HStack>
          <Box w="100%" pl={9}>
            <Text fontSize="sm" color="whiteAlpha.700" mb={4}>
              Chọn mục cần xem để mở trang chi tiết tương ứng.
            </Text>
            <HStack
              spacing={2}
              align="center"
              flexWrap="wrap"
              sx={{
                "& button": {
                  h: "34px",
                  minH: "34px",
                  maxH: "34px",
                  px: 4,
                  borderRadius: "full",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  lineHeight: "1",
                  fontSize: "sm",
                  mt: 0,
                  mb: 0,
                  py: 0
                }
              }}
            >
              {isDiagnosisCompleted ? (
                <Button
                  size="sm"
                  colorScheme="blue"
                  onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/diagnosis`)}
                  whiteSpace="nowrap"
                >
                  Xem chuẩn đoán
                </Button>
              ) : isDiagnosisRequested || order.status === "INSPECTING" ? (
                <Box position="relative" display="inline-flex" alignItems="center">
                  <Button
                    size="sm"
                    colorScheme="orange"
                    variant="outline"
                    onClick={handleRecallDiagnosis}
                    whiteSpace="nowrap"
                  >
                    Thu hôi yêu cầu
                  </Button>
                  {isDiagnosisAssigned ? (
                    <Text
                      fontSize="xs"
                      color="orange.300"
                      textAlign="center"
                      position="absolute"
                      top="calc(100% + 2px)"
                      left="50%"
                      transform="translateX(-50%)"
                      whiteSpace="nowrap"
                    >
                      Đang chuẩn đoán
                    </Text>
                  ) : null}
                </Box>
              ) : (
                <Button
                  size="sm"
                  colorScheme="purple"
                  onClick={handleRequestDiagnosis}
                  isDisabled={!hasSignedCheckIn}
                  title={!hasSignedCheckIn ? "Cần khách ký check-in trươc khi yêu cầu chuẩn đoán." : ""}
                  whiteSpace="nowrap"
                >
                  Yêu cầu chuẩn đoán
                </Button>
              )}

              <Button
                size="sm"
                colorScheme="teal"
                onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/items`)}
                whiteSpace="nowrap"
              >
                Xem hạng mục & phụ tùng
              </Button>
              <Button
                size="sm"
                colorScheme="purple"
                onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/quote`)}
                whiteSpace="nowrap"
              >
                Xem báo giá
              </Button>
            </HStack>
          </Box>
        </VStack>
      </VStack>
    </Modal>
  );
}

























