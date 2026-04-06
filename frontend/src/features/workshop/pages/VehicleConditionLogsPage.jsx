import React, { useMemo, useState, useEffect, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Button,
  Container,
  Flex,
  Heading,
  HStack,
  Icon,
  IconButton,
  Input,
  Stack,
  Text,
  Textarea,
  VStack,
  SimpleGrid,
  Radio,
  RadioGroup,
  Checkbox,
  CheckboxGroup,
  Center,
  useToast,
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  Card,
  CardBody,
  FormLabel,
  FormControl,
  FormErrorMessage,
  Image,
  Badge,
} from "@chakra-ui/react";
import {
  MdArrowBack,
  MdSave,
  MdSpeed,
  MdWarning,
  MdPhotoCamera,
  MdDelete,
  MdEdit,
  MdLock,
  MdAdd,
  MdDirectionsCar,
  MdPerson,
} from "react-icons/md";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";
import { getAllVehicles } from "@features/admin/api/customerApi";

function parseDefectsData(raw) {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) return parsed;
    if (Array.isArray(parsed?.defectItems)) return parsed.defectItems;
    return [];
  } catch {
    return [];
  }
}

function createDefaultDefectItems() {
  const now = Date.now();
  return [
    { id: now + 1, part: "Góc trước trái", issue: "", images: [] },
    { id: now + 2, part: "Góc trước phải", issue: "", images: [] },
    { id: now + 3, part: "Góc sau trái", issue: "", images: [] },
    { id: now + 4, part: "Góc sau phải", issue: "", images: [] },
  ];
}

function hasInspectionData(log) {
  if (!log) return false;
  const hasOdo = log.odoReading !== null && log.odoReading !== undefined && String(log.odoReading).trim() !== "";
  const hasFuel = log.fuelLevel !== null && log.fuelLevel !== undefined && String(log.fuelLevel).trim() !== "";
  const hasValuables = Boolean(String(log.valuables || "").trim());
  const hasDefects = Boolean(String(log.defectsAndImages || "").trim());
  const rawWarnings = String(log.dashboardWarningLights || "").trim();
  const normalizedWarnings = rawWarnings.toUpperCase();
  const isRequestMarker = normalizedWarnings === "PENDING" || normalizedWarnings === "ASSIGNED" || normalizedWarnings === "COMPLETED";
  const hasRealWarnings = Boolean(rawWarnings) && !isRequestMarker;

  return hasOdo || hasFuel || hasValuables || hasDefects || hasRealWarnings;
}

function sanitizeWarningLightsValue(rawValue) {
  const value = String(rawValue || "").trim();
  const normalized = value.toUpperCase();
  if (normalized === "PENDING" || normalized === "ASSIGNED" || normalized === "COMPLETED") {
    return "";
  }
  return value;
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

function SignaturePad({ onSave, onClear }) {
  const canvasRef = useRef(null);
  const isDrawingRef = useRef(false);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    ctx.strokeStyle = "#2b3674";
    ctx.lineWidth = 3;
    ctx.lineCap = "round";
  }, []);

  const getPointer = (event) => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const touch = event.touches ? event.touches[0] : event;
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return {
      x: (touch.clientX - rect.left) * scaleX,
      y: (touch.clientY - rect.top) * scaleY,
    };
  };

  const startDraw = (event) => {
    event.preventDefault();
    isDrawingRef.current = true;
    const pos = getPointer(event);
    const ctx = canvasRef.current.getContext("2d");
    ctx.beginPath();
    ctx.moveTo(pos.x, pos.y);
  };

  const draw = (event) => {
    if (!isDrawingRef.current) return;
    event.preventDefault();
    const pos = getPointer(event);
    const ctx = canvasRef.current.getContext("2d");
    ctx.lineTo(pos.x, pos.y);
    ctx.stroke();
  };

  const stopDraw = () => {
    if (!isDrawingRef.current) return;
    isDrawingRef.current = false;
    onSave(canvasRef.current.toDataURL("image/png"));
  };

  const clear = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    onClear();
  };

  return (
    <Box>
      <Box border="2px solid" borderColor="blue.100" borderRadius="xl" bg="blue.50" overflow="hidden" h="170px">
        <canvas
          ref={canvasRef}
          width={640}
          height={170}
          onMouseDown={startDraw}
          onMouseMove={draw}
          onMouseUp={stopDraw}
          onMouseOut={stopDraw}
          onTouchStart={startDraw}
          onTouchMove={draw}
          onTouchEnd={stopDraw}
          style={{ width: "100%", height: "100%", touchAction: "none" }}
        />
      </Box>
      <HStack justify="flex-end" mt={2}>
        <Button size="sm" variant="ghost" onClick={clear}>
          Xóa chữ ký
        </Button>
      </HStack>
    </Box>
  );
}

function readField(source, keys, fallback = "") {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null) {
      return value;
    }
  }
  return fallback;
}

function firstText(...values) {
  for (const value of values) {
    const text = String(value || "").trim();
    if (text) return text;
  }
  return "";
}

function normalizeKeyword(text) {
  return String(text || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

function hasRequiredCornerCoverage(items) {
  const coverage = {
    frontLeft: false,
    frontRight: false,
    rearLeft: false,
    rearRight: false,
  };

  (items || []).forEach((item) => {
    const images = Array.isArray(item?.images) ? item.images : [];
    if (images.length === 0) return;

    const part = normalizeKeyword(item?.part);
    if (!coverage.frontLeft && (part.includes("goc truoc trai") || part.includes("truoc trai") || part.includes("front left"))) {
      coverage.frontLeft = true;
    }
    if (!coverage.frontRight && (part.includes("goc truoc phai") || part.includes("truoc phai") || part.includes("front right"))) {
      coverage.frontRight = true;
    }
    if (!coverage.rearLeft && (part.includes("goc sau trai") || part.includes("sau trai") || part.includes("rear left") || part.includes("back left"))) {
      coverage.rearLeft = true;
    }
    if (!coverage.rearRight && (part.includes("goc sau phai") || part.includes("sau phai") || part.includes("rear right") || part.includes("back right"))) {
      coverage.rearRight = true;
    }
  });

  return coverage.frontLeft && coverage.frontRight && coverage.rearLeft && coverage.rearRight;
}

export default function VehicleConditionLogsPage() {
  const { orderId } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const phase = "CHECK_IN";

  const [order, setOrder] = useState(null);
  const [allLogs, setAllLogs] = useState([]);
  const [targetLog, setTargetLog] = useState(null);
  const [requestLog, setRequestLog] = useState(null);
  const [loading, setLoading] = useState(true);
  const [errorHeader, setErrorHeader] = useState("");
  const [saving, setSaving] = useState(false);
  const [signatureDraft, setSignatureDraft] = useState("");
  const [showSignaturePad, setShowSignaturePad] = useState(false);
  const [resolvedVehicle, setResolvedVehicle] = useState(null);

  // Form states
  const [editingLogId, setEditingLogId] = useState(null);
  const [odo, setOdo] = useState("");
  const [odoError, setOdoError] = useState("");
  const [fuel, setFuel] = useState("50");
  const [warningLights, setWarningLights] = useState([]);
  const [valuables, setValuables] = useState("");
  const [defectItems, setDefectItems] = useState(createDefaultDefectItems);
  const [inputKey, setInputKey] = useState(Date.now());

  const commonWarningLights = [
    "Check Engine",
    "ABS",
    "Airbag",
    "Brake System",
    "Oil Pressure",
    "Battery/Charging",
    "Tire Pressure (TPMS)",
  ];

  const displayVehiclePlate = useMemo(
    () =>
      firstText(
        order?.vehicleLicensePlate,
        order?.VehicleLicensePlate,
        resolvedVehicle?.licensePlate,
        resolvedVehicle?.LicensePlate,
        "-"
      ),
    [order, resolvedVehicle]
  );

  const isTargetSigned = Boolean(String(targetLog?.customerSignatureUrl || "").trim());
  const shouldShowForm = !isTargetSigned;

  const prefillFormFromLog = useCallback((log) => {
    setEditingLogId(log?.id || null);
    setOdo(log?.odoReading !== null && log?.odoReading !== undefined ? String(log.odoReading) : "");
    setFuel(log?.fuelLevel !== null && log?.fuelLevel !== undefined ? String(log.fuelLevel) : "50");
    const safeWarningLights = sanitizeWarningLightsValue(log?.dashboardWarningLights);
    setWarningLights(safeWarningLights ? safeWarningLights.split(", ").filter(Boolean) : []);
    setValuables(log?.valuables || "");

    const parsed = parseDefectsData(log?.defectsAndImages);
    if (parsed.length > 0) {
      setDefectItems(
        parsed.map((d, idx) => ({
          id: Date.now() + idx,
          part: d?.part || "",
          issue: d?.issue || "",
          images: Array.isArray(d?.images)
            ? d.images.map((url, i) => ({ id: `${Date.now()}-${idx}-${i}`, url }))
            : [],
        }))
      );
      return;
    }

    setDefectItems(createDefaultDefectItems());
  }, []);

  const fetchInitialData = useCallback(async () => {
    setLoading(true);
    setErrorHeader("");

    try {
      const orderRes = await serviceOrderApi.getById(orderId);
      if (!orderRes) {
        setErrorHeader("Không tìm thấy lệnh sửa chữa này.");
        return;
      }
      setOrder(orderRes);

      try {
        const hasPlateOnOrder = Boolean(firstText(orderRes?.vehicleLicensePlate, orderRes?.VehicleLicensePlate));
        const vehicleId = Number(readField(orderRes, ["vehicleId", "VehicleId"], 0));
        if (!hasPlateOnOrder && Number.isFinite(vehicleId) && vehicleId > 0) {
          const vehiclesRes = await getAllVehicles();
          const vehicles = Array.isArray(vehiclesRes) ? vehiclesRes : [];
          const matchedVehicle =
            vehicles.find((v) => Number(readField(v, ["id", "Id"], 0)) === vehicleId) || null;
          setResolvedVehicle(matchedVehicle);
        } else {
          setResolvedVehicle(null);
        }
      } catch {
        setResolvedVehicle(null);
      }

      let logsRes = await vehicleConditionLogApi.getByServiceOrderId(orderId);
      let logs = Array.isArray(logsRes) ? logsRes : [];

      const hasAnyCheckIn = logs.some((x) => String(x.logType || "").toUpperCase() === phase);
      if (!hasAnyCheckIn) {
        try {
          await serviceOrderApi.createCheckInRequest(Number(orderId));
          logsRes = await vehicleConditionLogApi.getByServiceOrderId(orderId);
          logs = Array.isArray(logsRes) ? logsRes : logs;
        } catch {
          // Ignore duplicate creation and continue.
        }
      }

      setAllLogs(logs);

      const logsWithData = logs.filter(hasInspectionData);
      const requestOnlyLogs = logs.filter(isRequestOnlyLog);

      const latestPhaseRequestLog = requestOnlyLogs
        .filter((x) => String(x.logType || "").toUpperCase() === phase)
        .sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0))[0] || null;
      setRequestLog(latestPhaseRequestLog);

      const phaseLogs = logsWithData
        .filter((x) => String(x.logType || "").toUpperCase() === phase)
        .sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));
      const latestPhaseLog = phaseLogs[0] || null;
      const fallbackLog = latestPhaseLog || latestPhaseRequestLog;
      setTargetLog(fallbackLog);

      if (fallbackLog && !String(fallbackLog.customerSignatureUrl || "").trim()) {
        prefillFormFromLog(fallbackLog);
      } else if (!fallbackLog) {
        setEditingLogId(null);
        setOdo("");
        setFuel("50");
        setWarningLights([]);
        setValuables("");
        setDefectItems(createDefaultDefectItems());
      }
    } catch (err) {
      setErrorHeader("Đã xảy ra lỗi khi tải dữ liệu. Vui lòng thử lại.");
      toast({ title: "Lỗi tải thông tin", status: "error" });
    } finally {
      setLoading(false);
    }
  }, [orderId, phase, prefillFormFromLog, toast]);

  useEffect(() => {
    void fetchInitialData();
  }, [fetchInitialData]);

  const validateOdo = (value) => {
    const val = parseInt(value, 10);
    if (!value) {
      setOdoError("Vui lòng nhập số KM (ODO).");
      return false;
    }
    if (Number.isNaN(val) || val <= 0 || val >= 2000000) {
      setOdoError("Số KM phải lớn hơn 0 và nhỏ hơn 2.000.000.");
      return false;
    }

    setOdoError("");
    return true;
  };

  const handleImageUpload = (e, defectIndex) => {
    const files = Array.from(e.target.files || []);
    if (files.length === 0) return;

    files.forEach((file) => {
      const reader = new FileReader();
      reader.onloadend = () => {
        setDefectItems((prev) => {
          const next = [...prev];
          next[defectIndex].images = [
            ...(next[defectIndex].images || []),
            { id: `${Date.now()}-${Math.random()}`, url: reader.result, file },
          ];
          return next;
        });
      };
      reader.readAsDataURL(file);
    });

    setInputKey(Date.now());
  };

  const removeImage = (defectIndex, imageId) => {
    setDefectItems((prev) => {
      const next = [...prev];
      next[defectIndex].images = (next[defectIndex].images || []).filter((img) => img.id !== imageId);
      return next;
    });
  };

  const addDefect = () => {
    setDefectItems((prev) => [...prev, { id: Date.now(), part: "", issue: "", images: [] }]);
  };

  const removeDefect = (index) => {
    if (defectItems.length <= 1) {
      setDefectItems(createDefaultDefectItems());
      return;
    }
    setDefectItems((prev) => prev.filter((_, i) => i !== index));
  };

  const updateDefectField = (index, field, value) => {
    setDefectItems((prev) => {
      const next = [...prev];
      next[index][field] = value;
      return next;
    });
  };

  const persistCheckIn = async (signatureUrl = "") => {
    const processedDefectItems = await Promise.all(
      defectItems.map(async (item) => {
        const uploadedImageUrls = await Promise.all(
          (item.images || []).map(async (img) => {
            if (img.url && !String(img.url).startsWith("data:")) return img.url;
            const { url } = await vehicleConditionLogApi.uploadImage(img.file || img.url);
            return url;
          })
        );

        return {
          part: item.part || "Khác",
          issue: item.issue || "",
          images: uploadedImageUrls,
        };
      })
    );

    const payload = {
      serviceOrderId: Number(orderId),
      logType: phase,
      odoReading: Number(odo),
      fuelLevel: Number(fuel),
      dashboardWarningLights: warningLights.join(", "),
      valuables,
      defectsAndImages: JSON.stringify(processedDefectItems),
      customerSignatureUrl: signatureUrl,
      inspectorId: targetLog?.inspectorId || null,
    };

    const targetUpdateId = editingLogId || requestLog?.id || null;
    if (targetUpdateId) {
      await vehicleConditionLogApi.update(targetUpdateId, payload);
      return;
    }

    await vehicleConditionLogApi.create(payload);
  };

  const handleSave = async () => {
    if (!validateOdo(odo)) {
      toast({ title: odoError || "Vui lòng kiểm tra lại ODO", status: "warning" });
      return;
    }

    setSaving(true);
    try {
      await persistCheckIn("");
      toast({
        title: "Đã lưu tạm check-in",
        description: "Bạn có thể tiếp tục chỉnh sửa trước khi khách hàng ký.",
        status: "success",
        duration: 2800,
      });
      await fetchInitialData();
    } catch (err) {
      toast({
        title: "Lỗi khi lưu dữ liệu",
        description: err?.response?.data?.message || err?.message,
        status: "error",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleSignCheckIn = async () => {
    if (!validateOdo(odo)) {
      toast({ title: odoError || "Vui lòng kiểm tra lại ODO", status: "warning" });
      return;
    }

    if (!hasRequiredCornerCoverage(defectItems)) {
      toast({
        title: "Chưa đủ ảnh 4 góc xe",
        description: "Cần có ảnh cho đủ trước trái, trước phải, sau trái, sau phải trước khi ký.",
        status: "warning",
        duration: 4200,
      });
      return;
    }

    if (!signatureDraft) {
      toast({
        title: "Chưa có chữ ký khách hàng",
        description: "Vui lòng mở khung chữ ký và mời khách hàng ký xác nhận.",
        status: "warning",
      });
      return;
    }

    setSaving(true);
    try {
      const { url } = await vehicleConditionLogApi.uploadSignature(signatureDraft);
      await persistCheckIn(String(url || ""));
      toast({
        title: "Đã hoàn tất check-in",
        description: "Biên bản đã khóa chỉnh sửa. Có thể tiếp tục bước xử lý khác.",
        status: "success",
        duration: 3200,
      });
      await fetchInitialData();
      setShowSignaturePad(false);
      setSignatureDraft("");
    } catch (err) {
      toast({
        title: "Lỗi khi ký check-in",
        description: err?.response?.data?.message || err?.message,
        status: "error",
      });
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <Box p={10} textAlign="center">Đang tải...</Box>;

  if (errorHeader) {
    return (
      <Box p={10} textAlign="center">
        <VStack spacing={4}>
          <Icon as={MdWarning} color="red.500" boxSize={12} />
          <Heading size="md">{errorHeader}</Heading>
          <Button onClick={() => navigate("/workshop/reception")}>Quay lại Reception</Button>
        </VStack>
      </Box>
    );
  }

  const parsedDefects = parseDefectsData(targetLog?.defectsAndImages);
  const displayWarningLights = sanitizeWarningLightsValue(targetLog?.dashboardWarningLights);
  const titleText = "Check-in tình trạng xe";
  const infoCardProps = {
    borderRadius: "20px",
    border: "1px solid",
    borderColor: "#d7e3f5",
    bg: "white",
    boxShadow: "0 12px 28px rgba(27, 37, 89, 0.08)",
  };

  return (
    <Box bg="linear-gradient(180deg, #f5f8ff 0%, #eef3fb 100%)" minH="100vh" pb={10}>
      <Container maxW="1720px" pt={{ base: 4, md: 6 }}>
        <Box
          borderRadius="22px"
          border="1px solid"
          borderColor="#d7e3f5"
          bg="white"
          px={{ base: 4, md: 6 }}
          py={{ base: 4, md: 5 }}
          mb={6}
          boxShadow="0 12px 28px rgba(27, 37, 89, 0.08)"
        >
          <Breadcrumb fontSize="sm" color="gray.500" mb={3}>
            <BreadcrumbItem>
              <BreadcrumbLink onClick={() => navigate("/workshop/reception")}>Reception</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbItem isCurrentPage>
              <BreadcrumbLink>Check-in</BreadcrumbLink>
            </BreadcrumbItem>
          </Breadcrumb>
          <Flex justify="space-between" align={{ base: "start", md: "center" }} gap={4} flexWrap="wrap">
            <HStack spacing={4} align="start">
              <IconButton
                icon={<MdArrowBack />}
                onClick={() => navigate("/workshop/reception")}
                variant="ghost"
                borderRadius="full"
                aria-label="Quay lại"
              />
              <VStack align="start" spacing={1}>
                <Heading size="md" color="#1B2559" fontWeight="800">{titleText}</Heading>
                <Text fontSize="sm" color="gray.600">
                  Lệnh: <strong>{order?.code || `#${orderId}`}</strong> - Biển số: <strong>{displayVehiclePlate}</strong>
                </Text>
                <Badge
                  borderRadius="full"
                  bg="#e8f0ff"
                  color="#2b4fa8"
                  px={3}
                  py={1}
                  fontSize="11px"
                  fontWeight="700"
                >
                  Lễ tân xử lý trực tiếp check-in/check-out
                </Badge>
              </VStack>
            </HStack>

            {shouldShowForm ? (
              <HStack spacing={2} flexWrap="wrap" justify={{ base: "start", md: "end" }} align="center">
                <Button
                  leftIcon={<Icon as={MdSave} boxSize={4} />}
                  bg="#2f5fbf"
                  color="white"
                  mt={0}
                  h="40px"
                  minH="40px"
                  px={4}
                  display="inline-flex"
                  alignItems="center"
                  justifyContent="center"
                  lineHeight="1"
                  iconSpacing={2}
                  sx={{
                    "& .chakra-button__icon": {
                      display: "inline-flex",
                      alignItems: "center",
                      lineHeight: 0,
                    },
                  }}
                  onClick={handleSave}
                  isLoading={saving}
                  _hover={{ bg: "#234b9b" }}
                >
                  Lưu tạm
                </Button>
                <Button
                  leftIcon={<Icon as={MdEdit} boxSize={4} />}
                  colorScheme="green"
                  mt={0}
                  h="40px"
                  minH="40px"
                  px={4}
                  display="inline-flex"
                  alignItems="center"
                  justifyContent="center"
                  lineHeight="1"
                  iconSpacing={2}
                  sx={{
                    "& .chakra-button__icon": {
                      display: "inline-flex",
                      alignItems: "center",
                      lineHeight: 0,
                    },
                  }}
                  onClick={handleSignCheckIn}
                  isLoading={saving}
                >
                  Ký xác nhận
                </Button>
              </HStack>
            ) : isTargetSigned ? (
              <HStack spacing={2} color="#2f5fbf" bg="#edf4ff" px={3} py={2} borderRadius="full">
                <Icon as={MdLock} />
                <Text fontWeight="700">Đã ký, đã khóa chỉnh sửa</Text>
              </HStack>
            ) : null}
          </Flex>
        </Box>

        <Box w="100%">
          <Stack spacing={6}>
        {shouldShowForm ? (
          <Stack spacing={8}>
            <SimpleGrid columns={{ base: 1, md: 2 }} spacing={6}>
              <Card {...infoCardProps}>
                <CardBody>
                  <VStack align="stretch" spacing={6}>
                    <Heading size="xs" color="#2b3674" textTransform="uppercase">Thông số vận hành</Heading>

                    <FormControl isRequired isInvalid={!!odoError}>
                      <FormLabel fontSize="sm" fontWeight="bold">Số KM hiển thị trên Taplo (ODO)</FormLabel>
                      <HStack mb={2}>
                        <Icon as={MdSpeed} color={odoError ? "red.500" : "blue.500"} boxSize={5} />
                        <Input
                          type="number"
                          placeholder="Nhập số KM..."
                          value={odo}
                          onChange={(e) => {
                            const val = e.target.value;
                            setOdo(val);
                            validateOdo(val);
                          }}
                          onBlur={() => validateOdo(odo)}
                          borderRadius="xl"
                        />
                      </HStack>
                      {odoError ? <FormErrorMessage>{odoError}</FormErrorMessage> : null}
                    </FormControl>

                    <FormControl>
                      <FormLabel fontSize="sm" fontWeight="bold">Mức nhiên liệu hiện tại (%)</FormLabel>
                      <RadioGroup onChange={setFuel} value={fuel}>
                        <SimpleGrid columns={3} spacing={2}>
                          <Radio value="0">Cạn (0%)</Radio>
                          <Radio value="25">1/4 (25%)</Radio>
                          <Radio value="50">1/2 (50%)</Radio>
                          <Radio value="75">3/4 (75%)</Radio>
                          <Radio value="100">Đầy (100%)</Radio>
                        </SimpleGrid>
                      </RadioGroup>
                    </FormControl>
                  </VStack>
                </CardBody>
              </Card>

              <Card {...infoCardProps}>
                <CardBody>
                  <VStack align="stretch" spacing={6}>
                    <Heading size="xs" color="#2b3674" textTransform="uppercase">Hệ thống cảnh báo</Heading>
                    <FormControl>
                      <FormLabel fontSize="sm" fontWeight="bold">Đèn cảnh báo đang sáng</FormLabel>
                      <CheckboxGroup colorScheme="red" value={warningLights} onChange={setWarningLights}>
                        <SimpleGrid columns={2} spacing={2}>
                          {commonWarningLights.map((light) => (
                            <Checkbox key={light} value={light}>{light}</Checkbox>
                          ))}
                        </SimpleGrid>
                      </CheckboxGroup>
                    </FormControl>
                  </VStack>
                </CardBody>
              </Card>
            </SimpleGrid>

            <Card {...infoCardProps}>
              <CardBody>
                <VStack align="stretch" spacing={4}>
                  <Heading size="xs" color="#2b3674" textTransform="uppercase">Tài sản trên xe</Heading>
                  <Textarea
                    placeholder="Ghi chú các tài sản có giá trị (ví tiền, kính mát, điện thoại...)"
                    value={valuables}
                    onChange={(e) => setValuables(e.target.value)}
                    borderRadius="xl"
                    rows={4}
                  />
                </VStack>
              </CardBody>
            </Card>

            <VStack align="stretch" spacing={6}>
              <Heading size="md" color="#2b3674">Danh sách hư hại và hình ảnh</Heading>
              <Text fontSize="sm" color="red.500" fontWeight="600">
                Bắt buộc có ảnh cho đủ 4 góc: góc trước trái, góc trước phải, góc sau trái, góc sau phải.
              </Text>
              {defectItems.map((item, idx) => (
                <Card key={item.id} {...infoCardProps}>
                  <CardBody p={{ base: 3, md: 4 }}>
                    <VStack align="stretch" spacing={3}>
                      <HStack justify="space-between" align="end" spacing={3}>
                        <FormControl isRequired>
                          <FormLabel fontSize="sm" fontWeight="700" mb={1}>Vị trí / Bộ phận</FormLabel>
                          <Input
                            placeholder="VD: Cửa trước trái, đuôi xe..."
                            value={item.part}
                            onChange={(e) => updateDefectField(idx, "part", e.target.value)}
                            borderRadius="lg"
                            h="40px"
                          />
                        </FormControl>
                        <IconButton
                          icon={<MdDelete />}
                          size="sm"
                          colorScheme="red"
                          variant="ghost"
                          aria-label="Xóa"
                          onClick={() => removeDefect(idx)}
                        />
                      </HStack>

                      <FormControl isRequired>
                        <FormLabel fontSize="sm" fontWeight="700" mb={1}>Mô tả hư hại</FormLabel>
                        <Textarea
                          placeholder="VD: Trầy xước, móp nhẹ..."
                          value={item.issue}
                          onChange={(e) => updateDefectField(idx, "issue", e.target.value)}
                          borderRadius="lg"
                          rows={2}
                        />
                      </FormControl>

                      <Box>
                        <Text fontSize="sm" fontWeight="700" mb={2}>Hình ảnh chi tiết ({item.images?.length || 0})</Text>
                        <SimpleGrid columns={{ base: 3, md: 5, lg: 7 }} spacing={2}>
                          {(item.images || []).map((img) => (
                            <Box key={img.id} position="relative" role="group">
                              <Image src={img.url} borderRadius="md" boxSize="72px" objectFit="cover" border="1px solid" borderColor="gray.200" />
                              <IconButton
                                icon={<MdDelete />}
                                size="xs"
                                colorScheme="red"
                                position="absolute"
                                top={-1}
                                right={-1}
                                onClick={() => removeImage(idx, img.id)}
                                aria-label="Xóa ảnh"
                                boxShadow="sm"
                                opacity={0}
                                _groupHover={{ opacity: 1 }}
                              />
                            </Box>
                          ))}
                          <Center
                            border="2px dashed"
                            borderColor="gray.300"
                            borderRadius="md"
                            boxSize="72px"
                            cursor="pointer"
                            _hover={{ borderColor: "blue.400", bg: "blue.50", color: "blue.500" }}
                            transition="all 0.2s"
                            onClick={() => document.getElementById(`file-upload-${idx}`)?.click()}
                          >
                            <VStack spacing={0}>
                              <Icon as={MdPhotoCamera} boxSize={4} />
                              <Text fontSize="2xs" fontWeight="700">Thêm ảnh</Text>
                            </VStack>
                            <input
                              type="file"
                              id={`file-upload-${idx}`}
                              style={{ display: "none" }}
                              multiple
                              accept="image/*"
                              key={inputKey}
                              onChange={(e) => handleImageUpload(e, idx)}
                            />
                          </Center>
                        </SimpleGrid>
                      </Box>
                    </VStack>
                  </CardBody>
                </Card>
              ))}

              <Button
                leftIcon={<MdAdd />}
                variant="outline"
                w="full"
                py={6}
                borderRadius="2xl"
                onClick={addDefect}
                borderColor="gray.300"
                color="gray.600"
                _hover={{ borderColor: "blue.400", color: "blue.500", bg: "blue.50" }}
              >
                Thêm vị trí hư hại khác
              </Button>

              <Card {...infoCardProps}>
                <CardBody>
                  <VStack align="stretch" spacing={4}>
                    <Heading size="xs" color="#2b3674" textTransform="uppercase">
                      Chữ ký khách hàng
                    </Heading>
                    <Text fontSize="sm" color="gray.600">
                      Có thể lưu tạm trước. Khi ký xong, biên bản check-in sẽ bị khóa chỉnh sửa.
                    </Text>
                    {!showSignaturePad ? (
                      <Button
                        leftIcon={<MdEdit />}
                        variant="outline"
                        colorScheme="blue"
                        onClick={() => setShowSignaturePad(true)}
                      >
                        Mở khung chữ ký
                      </Button>
                    ) : (
                      <>
                        <SignaturePad onSave={setSignatureDraft} onClear={() => setSignatureDraft("")} />
                        <HStack justify="space-between">
                          <Text fontSize="xs" color={signatureDraft ? "green.500" : "red.500"} fontWeight="600">
                            {signatureDraft ? "Đã có chữ ký nháp" : "Chưa có chữ ký"}
                          </Text>
                          <Button size="sm" variant="ghost" onClick={() => setShowSignaturePad(false)}>
                            Ẩn khung ký
                          </Button>
                        </HStack>
                      </>
                    )}
                  </VStack>
                </CardBody>
              </Card>
            </VStack>
          </Stack>
        ) : (
          <Card {...infoCardProps}>
            <CardBody p={{ base: 3, md: 4 }}>
              <VStack align="stretch" spacing={4}>
                <HStack justify="space-between" align="center" flexWrap="wrap" gap={2}>
                  <HStack spacing={2} flexWrap="wrap">
                    <Badge colorScheme={phase === "CHECK_IN" ? "green" : "orange"} px={3} py={1} borderRadius="full">
                      {phase}
                    </Badge>
                    <Text fontWeight="800" color="#2b3674">{order?.code || `#${orderId}`}</Text>
                    <HStack spacing={1}><Icon as={MdDirectionsCar} color="gray.500" /><Text>{displayVehiclePlate}</Text></HStack>
                    <HStack spacing={1}><Icon as={MdPerson} color="gray.500" /><Text>{firstText(order?.customerName, order?.CustomerName, "-")}</Text></HStack>
                  </HStack>
                  <Badge px={3} py={1} borderRadius="full" bg="#edf7ef" color="#1f7a3f">Đã ký</Badge>
                </HStack>

                <SimpleGrid columns={{ base: 2, md: 4 }} spacing={3}>
                  <Box bg="#f7faff" p={3} borderRadius="lg" border="1px solid" borderColor="#d7e3f5">
                    <Text fontSize="xs" color="gray.500">ODO</Text>
                    <Text fontWeight="700">{targetLog?.odoReading?.toLocaleString() || "-"} KM</Text>
                  </Box>
                  <Box bg="#f7faff" p={3} borderRadius="lg" border="1px solid" borderColor="#d7e3f5">
                    <Text fontSize="xs" color="gray.500">Nhiên liệu</Text>
                    <Text fontWeight="700">{targetLog?.fuelLevel ?? "-"}%</Text>
                  </Box>
                  <Box bg="#f7faff" p={3} borderRadius="lg" border="1px solid" borderColor="#d7e3f5">
                    <Text fontSize="xs" color="gray.500">Đèn cảnh báo</Text>
                    <Text fontWeight="700" noOfLines={1}>{displayWarningLights || "Không có"}</Text>
                  </Box>
                  <Box bg="#f7faff" p={3} borderRadius="lg" border="1px solid" borderColor="#d7e3f5">
                    <Text fontSize="xs" color="gray.500">Tài sản trên xe</Text>
                    <Text fontWeight="700" noOfLines={1}>{targetLog?.valuables || "Không có"}</Text>
                  </Box>
                </SimpleGrid>

                <Box>
                  <HStack justify="space-between" mb={2}>
                    <Text fontSize="sm" fontWeight="700" color="#2b3674">Hư hại và hình ảnh</Text>
                    <Badge borderRadius="full" px={3} py={1} bg="#edf4ff" color="#2b4fa8">{parsedDefects.length} mục</Badge>
                  </HStack>
                  {parsedDefects.length === 0 ? (
                    <Text color="gray.500">Không có dữ liệu hư hại.</Text>
                  ) : (
                    <SimpleGrid columns={{ base: 1, md: 2 }} spacing={3}>
                      {parsedDefects.map((d, idx) => (
                        <Box key={`${d?.part || "part"}-${idx}`} border="1px solid" borderColor="#d7e3f5" borderRadius="lg" p={3} bg="#fbfcff">
                          <Text fontWeight="700" fontSize="sm">{d?.part || "Khác"}</Text>
                          <Text color="gray.600" fontSize="sm" noOfLines={2} mb={2}>{d?.issue || "Không có mô tả"}</Text>
                          <HStack spacing={2} flexWrap="wrap">
                            {(Array.isArray(d?.images) ? d.images : []).map((url, i) => (
                              <Image key={`${idx}-${i}`} src={url} borderRadius="md" boxSize="64px" objectFit="cover" border="1px solid" borderColor="gray.200" />
                            ))}
                          </HStack>
                        </Box>
                      ))}
                    </SimpleGrid>
                  )}
                </Box>

                <Box>
                  <Text fontSize="sm" fontWeight="700" color="#2b3674" mb={2}>Chữ ký khách hàng</Text>
                  {isTargetSigned ? (
                    <Image src={targetLog.customerSignatureUrl} maxH="88px" border="1px solid" borderColor="#d7e3f5" borderRadius="lg" p={2} bg="white" />
                  ) : (
                    <Text color="orange.500" fontWeight="600">Chưa có chữ ký khách hàng</Text>
                  )}
                </Box>
              </VStack>
            </CardBody>
          </Card>
        )}
          </Stack>
        </Box>
      </Container>
    </Box>
  );
}









