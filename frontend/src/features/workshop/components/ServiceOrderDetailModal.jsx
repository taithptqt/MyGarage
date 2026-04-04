import React, { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { Modal } from "@features/admin/components/AdminModals";
import {
  Badge,
  Box,
  Button,
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
import "./ServiceOrderDetailModal.css";

const ORDER_STATUS_COLOR_SCHEMES = {
  NEW: "gray",
  RECEPTION: "gray",
  INSPECTING: "purple",
  WAITING_APPROVAL: "orange",
  REPAIRING: "blue",
  QC_PENDING: "teal",
  READY: "green",
  COMPLETED: "green",
  CANCELLED: "red",
};

function getOrderStatusColorScheme(status) {
  return ORDER_STATUS_COLOR_SCHEMES[String(status || "").toUpperCase()] || "blue";
}

function DetailField({ label, value, emphasize = false }) {
  return (
    <Box className="so-detail-field">
      <Text className="so-detail-field-label">{label}</Text>
      <Text className={`so-detail-field-value${emphasize ? " is-emphasis" : ""}`}>{value || "-"}</Text>
    </Box>
  );
}

function formatAmountVnd(value) {
  return `${Number(value || 0).toLocaleString()}đ`;
}

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
    if (!window.confirm(`Gửi yêu cầu chẩn đoán cho phương tiện ${vehicle?.licensePlate || "N/A"} tới Kỹ thuật viên?`)) return;
    try {
      await serviceOrderApi.requestDiagnosis(order.id);
      if (onRefresh) await onRefresh();
      const diagRes = await serviceOrderApi.getDiagnoses(order.id);
      setDiagnoses(Array.isArray(diagRes) ? diagRes : []);
    } catch (err) {
      alert("Lỗi khi yêu cầu chẩn đoán: " + err.message);
    }
  };

  const handleRecallDiagnosis = async () => {
    if (!window.confirm("Thu hồi yêu cầu chẩn đoán?")) return;
    try {
      await serviceOrderApi.recallDiagnosisRequest(order.id);
      if (onRefresh) await onRefresh();
      const diagRes = await serviceOrderApi.getDiagnoses(order.id);
      setDiagnoses(Array.isArray(diagRes) ? diagRes : []);
    } catch (err) {
      alert("Lỗi khi thu hồi yêu cầu chẩn đoán: " + err.message);
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
      (d) => isLaborOrService(d) && !d?.diagnoseRecommendationId,
    );

    const uniqueMap = new Map();
    candidates.forEach((d) => {
      const key =
        d?.standardServiceId ? `std-${d.standardServiceId}`
          : d?.itemId ? `item-${d.itemId}`
            : `name-${String(d?.itemName || "").trim().toLowerCase()}`;
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
        console.error("Lỗi tải chi tiết lệnh:", err);
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
  const statusColorScheme = getOrderStatusColorScheme(order?.status);

  let sourceText = "Không xác định";
  let sourceColor = "gray";
  let sourceIcon = MdAssignment;

  if (isFromAppt) {
    sourceText = "Từ lịch hẹn trước";
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

  const fallbackMainNote = String(parsedNotes.mainNotes || "").toLowerCase().includes("không có")
    ? "Không có ghi chú gì"
    : String(parsedNotes.mainNotes || "").trim();

  return (
    <Modal
      title={`Chi tiết lệnh sửa chữa: ${order.code}`}
      onClose={onClose}
      className="modal-lg diagnosis-modal-light service-order-detail-modal"
    >
      <Box className="so-detail-shell">
        <Box className="so-detail-hero">
          <Box>
            <Text className="so-detail-kicker">Mã lệnh: {order.code || "-"}</Text>
            <HStack spacing={2} flexWrap="wrap">
              <Badge colorScheme={statusColorScheme} px={3} py={1} borderRadius="full">
                {order.status || "N/A"}
              </Badge>
              <Badge colorScheme="blue" variant="subtle" px={3} py={1} borderRadius="full">
                {order.orderCategory || "REPAIR"}
              </Badge>
            </HStack>
          </Box>
          <HStack spacing={2} flexWrap="wrap" className="so-detail-hero-actions">
            <Button
              size="sm"
              leftIcon={<MdEdit />}
              colorScheme="yellow"
              variant="solid"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/edit`)}
            >
              Chỉnh sửa
            </Button>
          </HStack>
        </Box>

        <Box className="so-detail-grid so-detail-grid--two">
          <Box className="so-detail-card">
            <HStack className="so-detail-card-heading" spacing={2}>
              <Icon as={MdPerson} boxSize={4} />
              <Heading size="sm">Thông tin khách hàng</Heading>
            </HStack>
            <Box className="so-detail-fields">
              <DetailField label="Họ tên" value={order.customerName || customer?.fullName || "-"} emphasize />
              <DetailField label="Số điện thoại" value={order.customerPhone || customer?.phone || "-"} />
            </Box>
          </Box>

          <Box className="so-detail-card">
            <HStack className="so-detail-card-heading" spacing={2}>
              <Icon as={MdDirectionsCar} boxSize={4} />
              <Heading size="sm">Thông tin phương tiện</Heading>
            </HStack>
            <Box className="so-detail-fields">
              <DetailField
                label="Biển số"
                value={(order.vehicleLicensePlate || vehicle?.licensePlate || "N/A").toUpperCase()}
                emphasize
              />
              <DetailField
                label="Mẫu xe"
                value={`${vehicle?.modelName || "-"}${vehicle?.color ? ` - ${vehicle.color}` : ""}`}
              />
              <DetailField label="VIN" value={vehicle?.vin || "-"} />
              <Box className="so-detail-field">
                <Text className="so-detail-field-label">Nhật ký phương tiện</Text>
                <Box>
                  <Button
                    size="sm"
                    colorScheme="cyan"
                    variant="outline"
                    onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/vehicleconditionlogs`)}
                  >
                    Mở nhật ký xe
                  </Button>
                </Box>
              </Box>
            </Box>
          </Box>
        </Box>

        <Box className="so-detail-grid so-detail-grid--four">
          <Box className="so-detail-card so-detail-stat-card">
            <Text className="so-detail-stat-label">Thời điểm vào</Text>
            <Text className="so-detail-stat-value">{formatOrderDateTime(order.checkInTime)}</Text>
          </Box>
          <Box className="so-detail-card so-detail-stat-card">
            <Text className="so-detail-stat-label">Hẹn trả xe</Text>
            <Text className="so-detail-stat-value">{formatOrderDateTime(order.promisedHandoverTime)}</Text>
          </Box>
          <Box className="so-detail-card so-detail-stat-card">
            <Text className="so-detail-stat-label">Dự kiến hoàn thành</Text>
            <Text className="so-detail-stat-value">{formatOrderDateTime(order.estimatedHandover)}</Text>
          </Box>
          <Box className="so-detail-card so-detail-stat-card">
            <Text className="so-detail-stat-label">Cố vấn dịch vụ</Text>
            <Text className="so-detail-stat-value">{advisor?.fullName || "-"}</Text>
          </Box>
        </Box>

        <Box className="so-detail-card">
          <HStack className="so-detail-card-heading" spacing={2}>
            <Icon as={MdAssignment} boxSize={4} />
            <Heading size="sm">Ghi chú và nguồn tiếp nhận</Heading>
          </HStack>

          <Badge
            colorScheme={sourceColor}
            variant="subtle"
            px={3}
            py={1}
            borderRadius="full"
            className="so-detail-source-chip"
          >
            <HStack spacing={1.5}>
              <Icon as={sourceIcon} boxSize={3.5} />
              <Text fontSize="xs" fontWeight="700">{sourceText}</Text>
            </HStack>
          </Badge>

          <Box className="so-detail-note-block service">
            <Text className="so-detail-note-block-label">Dịch vụ khách đã chọn</Text>
            {loadingDetails ? (
              <Text className="so-detail-note-muted">Đang tải...</Text>
            ) : customerChosenDetails.length > 0 ? (
              <VStack align="stretch" spacing={2}>
                {customerChosenDetails.map((d) => (
                  <HStack key={d.id} className="so-detail-service-row" justify="space-between">
                    <Text className="so-detail-service-name">{d.itemName}</Text>
                    <Text className="so-detail-service-amount">{formatAmountVnd(d.finalAmount || d.unitPriceAtTime || 0)}</Text>
                  </HStack>
                ))}
              </VStack>
            ) : parsedNotes.requestedServices ? (
              <Text className="so-detail-note-text">{parsedNotes.requestedServices}</Text>
            ) : (
              <Text className="so-detail-note-muted">Chưa có dịch vụ cụ thể nào được chọn.</Text>
            )}
          </Box>

          <Box className="so-detail-note-block note">
            <Text className="so-detail-note-block-label">Ghi chú và yêu cầu thêm</Text>
            {symptomNoteItems.length > 0 ? (
              <ul className="so-detail-note-list">
                {symptomNoteItems.map((item, index) => (
                  <li key={`${item}-${index}`}>{item}</li>
                ))}
              </ul>
            ) : (
              <Text className="so-detail-note-text">{fallbackMainNote || "Không có ghi chú gì"}</Text>
            )}
          </Box>
        </Box>

        <Box className="so-detail-card">
          <HStack className="so-detail-card-heading" spacing={2}>
            <Icon as={MdCheckCircle} boxSize={4} />
            <Heading size="sm">Xem chi tiết theo từng mục</Heading>
          </HStack>
          <Text className="so-detail-note-muted">
            Chọn mục cần xem để mở trang chi tiết tương ứng.
          </Text>

          <Box className="so-detail-quick-actions">
            {isDiagnosisCompleted ? (
              <Button
                size="sm"
                colorScheme="blue"
                onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/diagnosis`)}
              >
                Xem chẩn đoán
              </Button>
            ) : isDiagnosisRequested || order.status === "INSPECTING" ? (
              <Box className="so-detail-diagnosis-action">
                <Button
                  size="sm"
                  colorScheme="orange"
                  variant="outline"
                  onClick={handleRecallDiagnosis}
                >
                  Thu hồi yêu cầu
                </Button>
                {isDiagnosisAssigned ? (
                  <Text className="so-detail-inline-hint">Đang chẩn đoán</Text>
                ) : null}
              </Box>
            ) : (
              <Button
                size="sm"
                colorScheme="purple"
                onClick={handleRequestDiagnosis}
                isDisabled={!hasSignedCheckIn}
                title={!hasSignedCheckIn ? "Cần khách ký check-in trước khi yêu cầu chẩn đoán." : ""}
              >
                Yêu cầu chẩn đoán
              </Button>
            )}

            <Button
              size="sm"
              colorScheme="teal"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/items`)}
            >
              Xem hạng mục và phụ tùng
            </Button>
            <Button
              size="sm"
              colorScheme="purple"
              onClick={() => handleOpenPage(`/workshop/reception/orders/${order.id}/quote`)}
            >
              Xem báo giá
            </Button>
          </Box>
        </Box>
      </Box>
    </Modal>
  );
}
