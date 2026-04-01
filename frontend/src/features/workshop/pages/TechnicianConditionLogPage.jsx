import React, { useMemo, useState, useEffect } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
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
import { useAuth } from "@shared/core/context/AuthContext";
import { getAllStaff } from "@features/admin/api/staffApi";

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

export default function TechnicianConditionLogPage() {
  const { orderId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const toast = useToast();

  const params = new URLSearchParams(location.search);
  const phase = String(params.get("phase") || "CHECK_IN").toUpperCase() === "CHECK_OUT" ? "CHECK_OUT" : "CHECK_IN";
  const editMode = params.get("edit") === "1";

  const [order, setOrder] = useState(null);
  const [staffId, setStaffId] = useState(null);
  const [allLogs, setAllLogs] = useState([]);
  const [targetLog, setTargetLog] = useState(null);
  const [requestLog, setRequestLog] = useState(null);
  const [loading, setLoading] = useState(true);
  const [errorHeader, setErrorHeader] = useState("");
  const [saving, setSaving] = useState(false);

  // Form states
  const [editingLogId, setEditingLogId] = useState(null);
  const [odo, setOdo] = useState("");
  const [odoError, setOdoError] = useState("");
  const [fuel, setFuel] = useState("50");
  const [warningLights, setWarningLights] = useState([]);
  const [valuables, setValuables] = useState("");
  const [defectItems, setDefectItems] = useState([{ id: Date.now(), part: "", issue: "", images: [] }]);
  const [inputKey, setInputKey] = useState(Date.now());
  const [checkInOdo, setCheckInOdo] = useState(null);

  const commonWarningLights = [
    "Check Engine",
    "ABS",
    "Airbag",
    "Brake System",
    "Oil Pressure",
    "Battery/Charging",
    "Tire Pressure (TPMS)",
  ];

  const actualLogs = useMemo(() => (allLogs || []).filter(hasInspectionData), [allLogs]);
  const signedCheckInLog = useMemo(() => {
    const checkIns = actualLogs
      .filter((x) => String(x.logType || "").toUpperCase() === "CHECK_IN")
      .sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));
    return checkIns.find((x) => String(x.customerSignatureUrl || "").trim()) || null;
  }, [actualLogs]);

  const isTargetSigned = Boolean(String(targetLog?.customerSignatureUrl || "").trim());
  const shouldShowForm = !targetLog || (editMode && !isTargetSigned);

  const prefillFormFromLog = (log) => {
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

    setDefectItems([{ id: Date.now(), part: "", issue: "", images: [] }]);
  };

  useEffect(() => {
    const fetchInitialData = async () => {
      try {
        if (!user) return;

        const [orderRes, staffRes, logsRes] = await Promise.all([
          serviceOrderApi.getById(orderId),
          getAllStaff(),
          vehicleConditionLogApi.getByServiceOrderId(orderId),
        ]);

        if (!orderRes) {
          setErrorHeader("Không tìm thấy lệnh sửa chữa này.");
        } else {
          setOrder(orderRes);
        }

        const logs = Array.isArray(logsRes) ? logsRes : [];
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
        setTargetLog(latestPhaseLog);

        const checkInLogs = logsWithData
          .filter((x) => String(x.logType || "").toUpperCase() === "CHECK_IN")
          .sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));
        setCheckInOdo(checkInLogs[0]?.odoReading ?? null);

        if (latestPhaseLog && editMode && !String(latestPhaseLog.customerSignatureUrl || "").trim()) {
          prefillFormFromLog(latestPhaseLog);
        } else {
          setEditingLogId(null);
          if (!latestPhaseLog) {
            setOdo("");
            setFuel("50");
            setWarningLights([]);
            setValuables("");
            setDefectItems([{ id: Date.now(), part: "", issue: "", images: [] }]);
          }
        }

        if (Array.isArray(staffRes) && user?.email) {
          const currentStaff = staffRes.find((s) => s && (s.email === user.email || s.fullName === user.fullName));
          if (currentStaff) setStaffId(currentStaff.id);
        }
      } catch (err) {
        setErrorHeader("Đã xảy ra lỗi khi tải dữ liệu. Vui lòng thử lại.");
        toast({ title: "Lđ-i tải thông tin", status: "error" });
      } finally {
        setLoading(false);
      }
    };

    fetchInitialData();
  }, [orderId, user, toast, phase, editMode]);

  const validateOdo = (value) => {
    const val = parseInt(value, 10);
    if (!value) {
      setOdoError("Vui lòng nhập sđ KM (ODO)");
      return false;
    }
    if (Number.isNaN(val) || val <= 0 || val >= 2000000) {
      setOdoError("Sđ KM phải lơn hơn 0 và nhỏ hơn 2,000,000");
      return false;
    }

    if (phase === "CHECK_OUT" && checkInOdo !== null && val < checkInOdo) {
      setOdoError(`ODO không được nhỏ hơn lúc nhận xe (${Number(checkInOdo).toLocaleString()} KM)`);
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
      setDefectItems([{ id: Date.now(), part: "", issue: "", images: [] }]);
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

  const handleSave = async () => {
    if (!validateOdo(odo)) {
      toast({ title: odoError || "Vui lòng kiểm tra lại chệ sđ ODO", status: "warning" });
      return;
    }

    if (phase === "CHECK_OUT" && !signedCheckInLog) {
      toast({
        title: "Chưa thể checkout",
        description: "Chỉ được tạo checkout sau khi khách đã ký check-in.",
        status: "error",
      });
      return;
    }

    setSaving(true);
    try {
      const processedDefectItems = await Promise.all(
        defectItems.map(async (item) => {
          const uploadedImageUrls = await Promise.all(
            (item.images || []).map(async (img) => {
              if (img.url && !String(img.url).startsWith("data:")) return img.url;
              const { url } = await vehicleConditionLogApi.uploadImage(img.url);
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
        inspectorId: staffId,
      };

      const targetUpdateId = editingLogId || requestLog?.id || null;
      if (targetUpdateId) {
        await vehicleConditionLogApi.update(targetUpdateId, payload);
      } else {
        await vehicleConditionLogApi.create(payload);
      }

      toast({
        title: editingLogId ? "Đã cập nhật thành công" : `Đã lưu ${phase} thành công`,
        status: "success",
        duration: 2500,
      });

      navigate("/workshop/technician");
    } catch (err) {
      toast({
        title: "Lđ-i khi lưu dữ liệu",
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
          <Button onClick={() => navigate("/workshop/technician")}>Quay lại Dashboard</Button>
        </VStack>
      </Box>
    );
  }

  const parsedDefects = parseDefectsData(targetLog?.defectsAndImages);
  const displayWarningLights = sanitizeWarningLightsValue(targetLog?.dashboardWarningLights);
  const titleText = `${phase === "CHECK_IN" ? "CHECK-IN" : "CHECK-OUT"} - KIđ,M TRA TđONH TRẠNG XE`;

  return (
    <Box bg="#f4f7fe" minH="100vh" pb={10}>
      <Box bg="white" px={8} py={4} mb={8} boxShadow="sm">
        <Breadcrumb fontSize="sm" color="gray.500" mb={2}>
          <BreadcrumbItem>
            <BreadcrumbLink onClick={() => navigate("/workshop/technician")}>DASHBOARD</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbItem isCurrentPage>
            <BreadcrumbLink>{phase === "CHECK_IN" ? "CHECK-IN" : "CHECK-OUT"}</BreadcrumbLink>
          </BreadcrumbItem>
        </Breadcrumb>
        <Flex justify="space-between" align="center" gap={3}>
          <HStack spacing={4}>
            <IconButton
              icon={<MdArrowBack />}
              onClick={() => navigate("/workshop/technician")}
              variant="ghost"
              borderRadius="full"
              aria-label="Quay lại"
            />
            <VStack align="start" spacing={0}>
              <Heading size="lg" color="#1B2559">{titleText}</Heading>
              <Text fontSize="sm" color="gray.500">
                Lệnh: <strong>{order?.code || `#${orderId}`}</strong> - Biển sđ: <strong>{order?.vehicleLicensePlate || "-"}</strong>
              </Text>
            </VStack>
          </HStack>

          {shouldShowForm ? (
            <Button
              leftIcon={<MdSave />}
              bg="#4318FF"
              color="white"
              onClick={handleSave}
              isLoading={saving}
              _hover={{ bg: "#3612D3" }}
            >
              {editingLogId ? "Lưu cập nhật" : "Lưu kết quả"}
            </Button>
          ) : isTargetSigned ? (
            <HStack spacing={2} color="gray.500">
              <Icon as={MdLock} />
              <Text fontWeight="700">Đã ký - khóa chỉnh sửa</Text>
            </HStack>
          ) : (
            <IconButton
              aria-label="Chệnh sửa"
              icon={<MdEdit />}
              colorScheme="blue"
              variant="outline"
              onClick={() => navigate(`/workshop/technician/orders/${orderId}/condition?phase=${phase}&edit=1`)}
            />
          )}
        </Flex>
      </Box>

      <Container maxW="container.xl">
        {shouldShowForm ? (
          <Stack spacing={8}>
            <SimpleGrid columns={{ base: 1, md: 2 }} spacing={6}>
              <Card borderRadius="2xl" border="none" boxShadow="0px 18px 40px rgba(112, 144, 176, 0.12)">
                <CardBody>
                  <VStack align="stretch" spacing={6}>
                    <Heading size="xs" color="#2b3674" textTransform="uppercase">Thông sđ vận hành</Heading>

                    <FormControl isRequired isInvalid={!!odoError}>
                      <FormLabel fontSize="sm" fontWeight="bold">Sđ KM hiển thị trên Taplo (ODO)</FormLabel>
                      <HStack mb={2}>
                        <Icon as={MdSpeed} color={odoError ? "red.500" : "blue.500"} boxSize={5} />
                        <Input
                          type="number"
                          placeholder="Nhập sđ KM..."
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

              <Card borderRadius="2xl" border="none" boxShadow="0px 18px 40px rgba(112, 144, 176, 0.12)">
                <CardBody>
                  <VStack align="stretch" spacing={6}>
                    <Heading size="xs" color="#2b3674" textTransform="uppercase">Hệ thđng cảnh báo</Heading>
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

            <Card borderRadius="2xl" border="none" boxShadow="0px 18px 40px rgba(112, 144, 176, 0.12)">
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
              {defectItems.map((item, idx) => (
                <Card key={item.id} borderRadius="2xl" border="none" boxShadow="0px 18px 40px rgba(112, 144, 176, 0.12)">
                  <CardBody>
                    <VStack align="stretch" spacing={4}>
                      <HStack justify="space-between" align="start">
                        <FormControl isRequired>
                          <FormLabel fontSize="sm" fontWeight="bold">Vị trí / Bộ phận</FormLabel>
                          <Input
                            placeholder="VD: Cửa trươc trái, Đuôi xe..."
                            value={item.part}
                            onChange={(e) => updateDefectField(idx, "part", e.target.value)}
                            borderRadius="xl"
                          />
                        </FormControl>
                        <IconButton icon={<MdDelete />} colorScheme="red" variant="ghost" aria-label="Xóa" onClick={() => removeDefect(idx)} />
                      </HStack>

                      <FormControl isRequired>
                        <FormLabel fontSize="sm" fontWeight="bold">Mô tả hư hại</FormLabel>
                        <Textarea
                          placeholder="VD: Trầy xươc, móp nhẹ..."
                          value={item.issue}
                          onChange={(e) => updateDefectField(idx, "issue", e.target.value)}
                          borderRadius="xl"
                        />
                      </FormControl>

                      <Box>
                        <Text fontSize="sm" fontWeight="bold" mb={2}>Hình ảnh chi tiết ({item.images?.length || 0})</Text>
                        <SimpleGrid columns={{ base: 3, md: 5, lg: 8 }} spacing={3}>
                          {(item.images || []).map((img) => (
                            <Box key={img.id} position="relative" role="group">
                              <Image src={img.url} borderRadius="md" boxSize="80px" objectFit="cover" border="1px solid" borderColor="gray.100" />
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
                            borderColor="gray.200"
                            borderRadius="md"
                            boxSize="80px"
                            cursor="pointer"
                            _hover={{ borderColor: "blue.400", bg: "blue.50", color: "blue.500" }}
                            transition="all 0.2s"
                            onClick={() => document.getElementById(`file-upload-${idx}`)?.click()}
                          >
                            <VStack spacing={0}>
                              <Icon as={MdPhotoCamera} boxSize={5} />
                              <Text fontSize="2xs" fontWeight="bold">Thêm ảnh</Text>
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
            </VStack>
          </Stack>
        ) : (
          <Card borderRadius="2xl" border="none" boxShadow="0px 18px 40px rgba(112, 144, 176, 0.12)">
            <CardBody>
              <VStack align="stretch" spacing={5}>
                <HStack justify="space-between" align="start" flexWrap="wrap" gap={3}>
                  <HStack spacing={3}>
                    <Badge colorScheme={phase === "CHECK_IN" ? "green" : "orange"} px={3} py={1} borderRadius="full">
                      {phase}
                    </Badge>
                    <Text fontWeight="800" color="#2b3674">{order?.code || `#${orderId}`}</Text>
                    <HStack spacing={1}><Icon as={MdDirectionsCar} color="gray.500" /><Text>{order?.vehicleLicensePlate || "-"}</Text></HStack>
                    <HStack spacing={1}><Icon as={MdPerson} color="gray.500" /><Text>{order?.customerName || "-"}</Text></HStack>
                  </HStack>
                </HStack>

                <SimpleGrid columns={{ base: 1, md: 2 }} spacing={4}>
                  <Box bg="gray.50" p={4} borderRadius="xl"><Text fontSize="xs" color="gray.500">ODO</Text><Text fontWeight="700">{targetLog?.odoReading?.toLocaleString() || "-"} KM</Text></Box>
                  <Box bg="gray.50" p={4} borderRadius="xl"><Text fontSize="xs" color="gray.500">Nhiên liệu</Text><Text fontWeight="700">{targetLog?.fuelLevel ?? "-"}%</Text></Box>
                </SimpleGrid>

                <Box bg="gray.50" p={4} borderRadius="xl">
                  <Text fontSize="xs" color="gray.500" mb={1}>Đèn cảnh báo</Text>
                  <Text fontWeight="600">{displayWarningLights || "Không có"}</Text>
                </Box>

                <Box bg="gray.50" p={4} borderRadius="xl">
                  <Text fontSize="xs" color="gray.500" mb={1}>Tài sản trên xe</Text>
                  <Text fontWeight="600">{targetLog?.valuables || "Không có"}</Text>
                </Box>

                <Box>
                  <Text fontSize="sm" fontWeight="700" color="#2b3674" mb={2}>Hư hại và hình ảnh</Text>
                  {parsedDefects.length === 0 ? (
                    <Text color="gray.500">Không có dữ liệu hư hại.</Text>
                  ) : (
                    <VStack align="stretch" spacing={3}>
                      {parsedDefects.map((d, idx) => (
                        <Box key={`${d?.part || "part"}-${idx}`} border="1px solid" borderColor="gray.200" borderRadius="xl" p={3}>
                          <Text fontWeight="700">{d?.part || "Khác"}</Text>
                          <Text color="gray.700" mb={2}>{d?.issue || "-"}</Text>
                          <SimpleGrid columns={{ base: 2, md: 4, lg: 6 }} spacing={2}>
                            {(Array.isArray(d?.images) ? d.images : []).map((url, i) => (
                              <Image key={`${idx}-${i}`} src={url} borderRadius="md" boxSize="90px" objectFit="cover" border="1px solid" borderColor="gray.100" />
                            ))}
                          </SimpleGrid>
                        </Box>
                      ))}
                    </VStack>
                  )}
                </Box>

                <Box>
                  <Text fontSize="sm" fontWeight="700" color="#2b3674" mb={2}>Chữ ký khách hàng</Text>
                  {isTargetSigned ? (
                    <Image src={targetLog.customerSignatureUrl} maxH="120px" border="1px solid" borderColor="gray.200" borderRadius="lg" p={2} bg="white" />
                  ) : (
                    <Text color="orange.500" fontWeight="600">Chưa có chữ ký khách hàng</Text>
                  )}
                </Box>
              </VStack>
            </CardBody>
          </Card>
        )}
      </Container>
    </Box>
  );
}







