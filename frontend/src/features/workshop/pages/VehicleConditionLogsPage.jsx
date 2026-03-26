import React, { useEffect, useMemo, useState, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  Box,
  Button,
  Heading,
  HStack,
  Spinner,
  Text,
  SimpleGrid,
  Badge,
  Stack,
  Icon,
  Progress,
  Divider,
  Image,
  VStack,
  Flex,
  Tooltip,
  useToast,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalBody,
  ModalCloseButton,
  useDisclosure,
} from "@chakra-ui/react";
import {
  MdSpeed,
  MdLocalGasStation,
  MdWarning,
  MdCardGiftcard,
  MdBuild,
  MdHistory,
  MdAssignment,
  MdPhotoCamera,
  MdSave,
  MdDelete,
  MdEdit,
} from "react-icons/md";
import { serviceOrderApi } from "../api/serviceOrderApi";
import { vehicleConditionLogApi } from "../api/vehicleConditionLogApi";
import { formatOrderDateTime } from "../utils/serviceOrderDetailUtils";

function pick(obj, keys, fallback = null) {
  for (const k of keys) {
    if (obj && obj[k] !== undefined && obj[k] !== null) return obj[k];
  }
  return fallback;
}

function normalizeLog(row) {
  return {
    id: pick(row, ["id", "Id"]),
    serviceOrderId: pick(row, ["serviceOrderId", "ServiceOrderId"]),
    logType: pick(row, ["logType", "LogType"]),
    odoReading: pick(row, ["odoReading", "OdoReading"]),
    fuelLevel: pick(row, ["fuelLevel", "FuelLevel"]),
    dashboardWarningLights: pick(row, ["dashboardWarningLights", "DashboardWarningLights"], ""),
    valuables: pick(row, ["valuables", "Valuables"], ""),
    defectsAndImages: pick(row, ["defectsAndImages", "DefectsAndImages"], ""),
    customerSignatureUrl: pick(row, ["customerSignatureUrl", "CustomerSignatureUrl"], ""),
    inspectorId: pick(row, ["inspectorId", "InspectorId"]),
    createdAt: pick(row, ["createdAt", "CreatedAt"]),
    createdBy: pick(row, ["createdBy", "CreatedBy"]),
    updatedAt: pick(row, ["updatedAt", "UpdatedAt"]),
    updatedBy: pick(row, ["updatedBy", "UpdatedBy"]),
  };
}

function isRequestOnlyLog(log) {
  if (!log) return false;
  const hasOdo = log.odoReading !== null && log.odoReading !== undefined && String(log.odoReading).trim() !== "";
  const hasFuel = log.fuelLevel !== null && log.fuelLevel !== undefined && String(log.fuelLevel).trim() !== "";
  const hasValuables = Boolean(String(log.valuables || "").trim());
  const hasDefects = Boolean(String(log.defectsAndImages || "").trim());
  const hasSignature = Boolean(String(log.customerSignatureUrl || "").trim());
  const marker = String(log.dashboardWarningLights || "").trim().toUpperCase();
  const isMarker = marker === "PENDING" || marker === "ASSIGNED" || marker === "COMPLETED";
  return !hasOdo && !hasFuel && !hasValuables && !hasDefects && !hasSignature && isMarker;
}

const SignaturePad = ({ onSave, onClear }) => {
  const canvasRef = useRef(null);
  const isDrawing = useRef(false);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    ctx.strokeStyle = "#2b3674";
    ctx.lineWidth = 3;
    ctx.lineCap = "round";
  }, []);

  const getPos = (e) => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const touch = e.touches ? e.touches[0] : e;
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return {
      x: (touch.clientX - rect.left) * scaleX,
      y: (touch.clientY - rect.top) * scaleY,
    };
  };

  const startDrawing = (e) => {
    e.preventDefault();
    isDrawing.current = true;
    const pos = getPos(e);
    const ctx = canvasRef.current.getContext("2d");
    ctx.beginPath();
    ctx.moveTo(pos.x, pos.y);
  };

  const draw = (e) => {
    if (!isDrawing.current) return;
    e.preventDefault();
    const pos = getPos(e);
    const ctx = canvasRef.current.getContext("2d");
    ctx.lineTo(pos.x, pos.y);
    ctx.stroke();
  };

  const stopDrawing = () => {
    if (!isDrawing.current) return;
    isDrawing.current = false;
    const dataUrl = canvasRef.current.toDataURL("image/png");
    onSave(dataUrl);
  };

  const clear = () => {
    const canvas = canvasRef.current;
    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    onClear();
  };

  return (
    <Box w="full" mt={2}>
      <Box 
        border="2px solid" 
        borderColor="blue.100" 
        borderRadius="xl" 
        bg="blue.50" 
        position="relative"
        overflow="hidden"
        h="180px"
        cursor="crosshair"
      >
        <canvas
          ref={canvasRef}
          width={600}
          height={180}
          onMouseDown={startDrawing}
          onMouseMove={draw}
          onMouseUp={stopDrawing}
          onMouseOut={stopDrawing}
          onTouchStart={startDrawing}
          onTouchMove={draw}
          onTouchEnd={stopDrawing}
          style={{ width: '100%', height: '100%', touchAction: 'none' }}
        />
        <Button 
          size="xs" 
          position="absolute" 
          bottom={2} 
          right={2} 
          onClick={clear}
          variant="ghost"
          colorScheme="gray"
        >
          Xóa chữ ký
        </Button>
      </Box>
    </Box>
  );
};

const FuelLevelDisplay = ({ level }) => {
  const percentage = level || 0;
  let color = "red";
  if (percentage > 25) color = "orange";
  if (percentage > 50) color = "yellow";
  if (percentage > 75) color = "green";

  return (
    <Box w="full">
      <HStack justify="space-between" mb={1}>
        <HStack spacing={1}>
          <Icon as={MdLocalGasStation} color={`${color}.500`} />
          <Text fontSize="xs" fontWeight="bold">Mức nhiên liệu</Text>
        </HStack>
        <Text fontSize="xs" fontWeight="bold">{percentage}%</Text>
      </HStack>
      <Progress value={percentage} colorScheme={color} size="xs" borderRadius="full" bg="gray.100" />
    </Box>
  );
};

const WarningLights = ({ lights }) => {
  if (!lights) return null;
  const list = String(lights || "").split(",").map((s) => s.trim()).filter(Boolean);
  if (list.length === 0) return null;

  return (
    <Box>
      <HStack spacing={1} mb={2}>
        <Icon as={MdWarning} color="orange.400" />
        <Text fontSize="xs" fontWeight="bold">Đèn cảnh báo</Text>
      </HStack>
      <Flex flexWrap="wrap" gap={1}>
        {list.map((light, idx) => (
          <Badge key={idx} colorScheme="orange" variant="subtle" fontSize="2xs" px={2} borderRadius="full">
            {light}
          </Badge>
        ))}
      </Flex>
    </Box>
  );
};

const ConditionCard = ({ log, onUpdateSignature, onImageClick }) => {
  const isCheckIn = log.logType?.toLowerCase().includes("in");
  const toast = useToast();
  const [showSignPad, setShowSignPad] = useState(false);
  const [signature, setSignature] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  // Parse defectsAndImages JSON with robust error handling
  let defectsData = { mainDescription: "", items: [], customerSignature: null, defectItems: [] };
  try {
    const rawData = log.defectsAndImages;
    if (rawData) {
      const parsed = typeof rawData === "string" ? JSON.parse(rawData) : rawData;
      const finalObj = typeof parsed === "string" ? JSON.parse(parsed) : parsed;
      
      if (Array.isArray(finalObj)) {
        defectsData.defectItems = finalObj;
      } else if (finalObj && typeof finalObj === "object") {
        // Legacy object-based structure
        defectsData = { ...defectsData, ...finalObj };
      }
    }
  } catch (e) {
    console.error("Error parsing defectsData:", e);
    defectsData.mainDescription = String(log.defectsAndImages || "");
  }

  // Ensure items is always an array
  if (!Array.isArray(defectsData.items)) {
    defectsData.items = [];
  }

  const finalSignature = log.customerSignatureUrl || defectsData.customerSignature;

  const handleSignConfirm = async () => {
    if (!signature) {
      toast({ title: "Vui lòng ký vào khung", status: "warning" });
      return;
    }

    setSubmitting(true);
    try {
      // 1. Upload signature to server to get URL
      const { url } = await vehicleConditionLogApi.uploadSignature(signature);

      // 2. Prepare updated defects (removing base64 to avoid trash data)
      const updatedDefects = {
        ...defectsData,
        signedAt: new Date().toISOString()
      };
      // Explicitly remove base64 if it existed from previous logic
      if (updatedDefects.customerSignature) {
        delete updatedDefects.customerSignature;
      }

      const payload = {
        ...log,
        defectsAndImages: JSON.stringify(updatedDefects),
        customerSignatureUrl: url
      };

      await vehicleConditionLogApi.update(log.id, payload);
      toast({ title: "Đã lưu chữ ký thành công", status: "success" });
      setShowSignPad(false);
      if (onUpdateSignature) onUpdateSignature();
    } catch (err) {
      toast({ title: "Lỗi khi lưu chữ ký", status: "error" });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Box
      bg="white"
      borderRadius="2xl"
      p={5}
      boxShadow="0px 18px 40px rgba(112, 144, 176, 0.08)"
      border="1px solid"
      borderColor="gray.50"
      position="relative"
    >
      <HStack justify="space-between" mb={4}>
        <Badge
          colorScheme={isCheckIn ? "green" : "orange"}
          fontSize="xs"
          px={3}
          py={1}
          borderRadius="lg"
          variant="solid"
        >
          {log.logType || "Ghi nhận"}
        </Badge>
        <Text fontSize="xs" color="gray.400" fontWeight="bold">
          {formatOrderDateTime(log.createdAt)}
        </Text>
      </HStack>

      <VStack align="stretch" spacing={5}>
        <HStack spacing={4}>
          <HStack flex={1} bg="gray.50" p={3} borderRadius="xl">
            <Icon as={MdSpeed} color="blue.500" boxSize={5} />
            <Box>
              <Text fontSize="2xs" color="gray.500" fontWeight="bold" textTransform="uppercase">ODO</Text>
              <Text fontSize="md" fontWeight="bold" color="#2b3674">{log.odoReading?.toLocaleString() || "0"} KM</Text>
            </Box>
          </HStack>
          <Box flex={1.5}>
            <FuelLevelDisplay level={log.fuelLevel} />
          </Box>
        </HStack>

        <Box bg="gray.50" p={3} borderRadius="xl">
          <HStack spacing={2} mb={1}>
            <Icon as={MdCardGiftcard} color="purple.500" />
            <Text fontSize="xs" fontWeight="bold" color="#2b3674" textTransform="uppercase">Tài sản trên xe</Text>
          </HStack>
          <Text fontSize="sm" color={log.valuables ? "gray.700" : "gray.400"}>
            {log.valuables || "Không có tài sản gửi kèm"}
          </Text>
        </Box>

        <Box>
          <HStack spacing={2} mb={2}>
            <Icon as={MdBuild} color="red.500" />
            <Text fontSize="xs" fontWeight="bold" color="#2b3674" textTransform="uppercase">Hư hại & Hình ảnh</Text>
          </HStack>
          
          {defectsData.mainDescription && (
            <Text fontSize="sm" color="gray.700" mb={3} bg="gray.50" p={2} borderRadius="md">
              {defectsData.mainDescription}
            </Text>
          )}

          {/* NEW Structured Defect Items */}
          {defectsData.defectItems && defectsData.defectItems.length > 0 && (
            <VStack spacing={4} align="stretch" mt={2}>
              {defectsData.defectItems.map((item, idx) => (
                <Box key={idx} bg="gray.50" p={3} borderRadius="xl" border="1px solid" borderColor="gray.100" boxShadow="sm">
                  <HStack justify="space-between" mb={2}>
                    <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2}>{item.part || "Khác"}</Badge>
                  </HStack>
                  <Text fontSize="sm" fontWeight="bold" color="#2b3674" mb={1}>{item.issue}</Text>
                  
                  {item.images && item.images.length > 0 ? (
                    <SimpleGrid columns={{ base: 3, md: 4 }} spacing={2} mt={2}>
                      {item.images.map((url, imgIdx) => (
                        <Image 
                          key={imgIdx} 
                          src={url} 
                          borderRadius="md" 
                          boxSize="80px" 
                          objectFit="cover" 
                          cursor="pointer"
                          _hover={{ opacity: 0.8, transform: "scale(1.05)" }}
                          transition="all 0.2s"
                          onClick={() => onImageClick && onImageClick(url)}
                          border="1px solid"
                          borderColor="gray.200"
                        />
                      ))}
                    </SimpleGrid>
                  ) : (
                    <Text fontSize="xs" color="gray.400" fontStyle="italic">Không có hình ảnh đính kèm</Text>
                  )}
                </Box>
              ))}
            </VStack>
          )}

          {/* LEGACY Items Display (Backward Compatibility) */}
          {defectsData.items && defectsData.items.length > 0 && (
            <VStack spacing={3} align="stretch" mt={2}>
              {defectsData.items.map((item, idx) => (
                <HStack key={idx} spacing={4} align="start" bg="gray.50" p={2} borderRadius="lg">
                  <Box 
                    w="120px" 
                    h="90px" 
                    borderRadius="md" 
                    overflow="hidden" 
                    flexShrink={0} 
                    border="1px solid" 
                    borderColor="gray.200" 
                    boxShadow="sm"
                    cursor="pointer"
                    _hover={{ opacity: 0.8, transform: "scale(1.02)" }}
                    transition="all 0.2s"
                    onClick={() => onImageClick && onImageClick(item.url)}
                  >
                    <Image src={item.url} w="full" h="full" objectFit="cover" />
                  </Box>
                  <Text fontSize="sm" color="gray.600" noOfLines={5} py={1}>{item.description || "(Không có mô tả)"}</Text>
                </HStack>
              ))}
            </VStack>
          )}

          {!defectsData.mainDescription && (!defectsData.items || defectsData.items.length === 0) && (!defectsData.defectItems || defectsData.defectItems.length === 0) && (
            <Text fontSize="sm" color="gray.400 italic">Không có ghi chú hư hại</Text>
          )}
        </Box>

        <WarningLights lights={log.dashboardWarningLights} />

        <Divider />

        {finalSignature ? (
          <Box>
            <HStack justify="space-between" mb={2}>
               <Text fontSize="xs" fontWeight="bold" color="#2b3674" textTransform="uppercase">Chữ ký khách hàng</Text>
               <Icon as={MdSave} color="green.500" />
            </HStack>
            <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="xl" p={2} textAlign="center">
              <Image
                src={finalSignature}
                maxH="100px"
                mx="auto"
                fallback={<Text fontSize="xs" color="gray.400">Không thể tải chữ ký</Text>}
              />
            </Box>
          </Box>
        ) : (
          <Box>
            {!showSignPad ? (
              <Button 
                w="full" 
                size="sm" 
                colorScheme="blue" 
                variant="outline" 
                leftIcon={<MdEdit />}
                onClick={() => setShowSignPad(true)}
                borderRadius="xl"
              >
                KHÁCH HÀNG KÝ DUYỆT
              </Button>
            ) : (
              <VStack spacing={3}>
                 <Text fontSize="xs" fontWeight="bold" color="red.500" w="full">Mời khách hàng ký vào khung dưới:</Text>
                 <SignaturePad onSave={setSignature} onClear={() => setSignature(null)} />
                 <HStack w="full">
                    <Button size="sm" flex={1} variant="ghost" onClick={() => setShowSignPad(false)}>Hủy</Button>
                    <Button 
                      size="sm" 
                      flex={2} 
                      colorScheme="green" 
                      onClick={handleSignConfirm}
                      isLoading={submitting}
                    >
                      Xác nhận chữ ký
                    </Button>
                 </HStack>
              </VStack>
            )}
          </Box>
        )}
      </VStack>
    </Box>
  );
};

export default function VehicleConditionLogsPage() {
  const navigate = useNavigate();
  const { orderId } = useParams();

  const [order, setOrder] = useState(null);
  const [allLogs, setAllLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const { isOpen, onOpen, onClose } = useDisclosure();
  const [selectedImage, setSelectedImage] = useState(null);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      setError("");
      try {
        const [orderRes, logsRes] = await Promise.all([
          serviceOrderApi.getById(orderId),
          vehicleConditionLogApi.getByServiceOrderId(orderId),
        ]);
        setOrder(orderRes || null);
        setAllLogs(Array.isArray(logsRes) ? logsRes.map(normalizeLog) : []);
      } catch (err) {
        setError(err?.message || "Không thể tải nhật ký xe.");
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [orderId]);

  const displayLogs = useMemo(() => {
    return (allLogs || []).filter((log) => !isRequestOnlyLog(log));
  }, [allLogs]);

  const showingVehicleHistoryFallback = false; // Simplified for performance

  return (
    <Box p={6} bg="#f4f7fe" minH="100vh">
      <HStack justify="space-between" mb={6} flexWrap="wrap" gap={3}>
        <Box>
          <HStack spacing={2} mb={1}>
            <Icon as={MdAssignment} boxSize={6} color="blue.500" />
            <Heading size="lg" color="#2b3674">Nhật ký xe</Heading>
          </HStack>
          <HStack spacing={4}>
            <Text color="#707eae" fontWeight="bold">
              Mã lệnh: <Badge variant="solid" colorScheme="blue">{order?.code || `#${orderId}`}</Badge>
            </Text>
            {order?.vehicleLicensePlate && (
              <Text color="#707eae" fontWeight="bold">
                Biển số: <Badge variant="outline" colorScheme="gray">{order.vehicleLicensePlate}</Badge>
              </Text>
            )}
          </HStack>
        </Box>
        <Button
          leftIcon={<MdSpeed />}
          colorScheme="brand"
          bg="blue.500"
          _hover={{ bg: "blue.600" }}
          color="white"
          onClick={() => navigate("/workshop/reception")}
          borderRadius="xl"
          px={6}
        >
          Quay lại Reception
        </Button>
      </HStack>

      {loading ? (
        <Flex justify="center" align="center" py={20}>
          <Spinner thickness="4px" speed="0.65s" emptyColor="gray.200" color="blue.500" size="xl" />
        </Flex>
      ) : error ? (
        <Box bg="red.50" p={4} borderRadius="lg" border="1px solid" borderColor="red.100">
          <Text color="red.500" fontWeight="bold">{error}</Text>
        </Box>
      ) : (
        <>
          {showingVehicleHistoryFallback && (
            <HStack bg="orange.50" p={4} borderRadius="xl" mb={6} border="1px solid" borderColor="orange.100" spacing={3}>
              <Icon as={MdHistory} boxSize={5} color="orange.500" />
              <Box>
                <Text color="orange.800" fontWeight="bold" fontSize="sm">
                  Đang hiển thị lịch sử xe
                </Text>
                <Text color="orange.700" fontSize="xs">
                  Lệnh hiện tại chưa có biên bản. Dưới đây là các lần ghi nhận trước đó của xe này để bạn tham khảo.
                </Text>
              </Box>
            </HStack>
          )}

          {displayLogs.length === 0 ? (
            <Flex
              direction="column"
              align="center"
              justify="center"
              bg="white"
              borderRadius="2xl"
              p={10}
              border="2px dashed"
              borderColor="gray.200"
            >
              <Icon as={MdAssignment} boxSize={12} color="gray.300" mb={4} />
              <Text color="gray.500" fontWeight="bold">Chưa có dữ liệu nhật ký cho xe này.</Text>
              <Text color="gray.400" fontSize="sm">Các bản ghi Check-in/Check-out sẽ xuất hiện tại đây.</Text>
            </Flex>
          ) : (
            <SimpleGrid columns={{ base: 1, lg: 2 }} spacing={6}>
              {displayLogs.map((log) => (
                <ConditionCard 
                  key={log.id} 
                  log={log} 
                  onImageClick={(url) => {
                    setSelectedImage(url);
                    onOpen();
                  }}
                  onUpdateSignature={() => {
                    // Refetch data to show the new signature
                    window.location.reload();
                  }}
                />
              ))}
            </SimpleGrid>
          )}
        </>
      )}

      {/* Image Zoom Modal */}
      <Modal isOpen={isOpen} onClose={onClose} size="full" isCentered>
        <ModalOverlay bg="blackAlpha.800" backdropFilter="blur(10px)" />
        <ModalContent bg="transparent" boxShadow="none">
          <ModalCloseButton color="white" size="lg" zIndex={2} bg="blackAlpha.500" borderRadius="full" m={4} />
          <ModalBody px={5} py={20} display="flex" alignItems="center" justifyContent="center" onClick={onClose}>
            <Image 
              src={selectedImage} 
              maxH="85vh" 
              maxW="90vw" 
              objectFit="contain" 
              borderRadius="xl"
              boxShadow="2xl"
              mx="auto"
            />
          </ModalBody>
        </ModalContent>
      </Modal>
    </Box>
  );
}
