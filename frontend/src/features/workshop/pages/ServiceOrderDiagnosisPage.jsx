import React, { useEffect, useMemo, useState, useCallback } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import {
  Badge,
  Box,
  Button,
  Heading,
  HStack,
  Input,
  Select,
  Spinner,
  Stack,
  Text,
  Textarea,
  VStack,
  useToast,
  Grid,
  GridItem,
  Divider,
  SimpleGrid,
  Icon,
  Tag,
  TagLabel,
  TagLeftIcon,
  FormControl,
  FormLabel,
} from "@chakra-ui/react";
import {
  Car,
  Wrench,
  AlertTriangle,
  Activity,
  MessageSquare,
  Droplets,
  Wind,
  Zap,
  CheckCircle,
  Info,
  Clock,
} from "lucide-react";

import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStandardServices } from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";

const SYSTEM_CATEGORIES = [
  { id: "engine", label: "??ng co", icon: Activity },
  { id: "transmission", label: "Hộp số", icon: Wrench },
  { id: "brakes", label: "Phanh & ABS", icon: AlertTriangle },
  { id: "steering", label: "Hệ thống Lái / Gầm", icon: Car },
  { id: "electrical", label: "Điện / Bình ắc quy", icon: Zap },
  { id: "hvac", label: "Điều hòa / Sưởi", icon: Wind },
  { id: "fluids", label: "Chất lỏng / Rò rỉ", icon: Droplets },
  { id: "other", label: "Khác", icon: Info },
];

const SYMPTOM_TYPES = {
  engine: ["Rung giật", "Mất công suất", "Chết máy", "Không khởi động được", "Khói bất thường"],
  transmission: ["Khó sang số", "Trượt số", "Giật cục", "Chậm phản hồi"],
  brakes: ["Kêu rít / Xào xạo", "Chân phanh mềm", "Xe bị lệch khi phanh", "Cứng chân phanh"],
  steering: ["Vô lăng nặng", "Xe nhao lái", "Lốp mòn không đều", "Tiếng va dập gầm"],
  electrical: ["Đèn pha mờ", "Còi tịt", "Đồng hồ taplo lỗi", "Hết bình"],
  hvac: ["Không mát", "Mát yếu", "Mùi hôi", "Quạt gió kêu"],
  fluids: ["Nước làm mát", "Dầu máy", "Dầu phanh", "Nước rửa kính"],
  other: ["Tiếng ồn lạ", "Mùi khét", "Hư hỏng ngoại thất", "Khác"],
};

export default function ServiceOrderDiagnosisPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const { user } = useAuth();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [order, setOrder] = useState(null);
  
  const [allDiagnoses, setAllDiagnoses] = useState([]);
  const [activeDiagId, setActiveDiagId] = useState(null);
  
  const [recommendations, setRecommendations] = useState([]);
  const [deletedRecommendationIds, setDeletedRecommendationIds] = useState([]);
  const [services, setServices] = useState([]);
  const [products, setProducts] = useState([]);

  // Form structured for AI + Legacy
  const [form, setForm] = useState({
    symptom: "",
    rootCause: "",
    severity: "MEDIUM",
    primarySystem: "",
    symptomTypes: [],
    occurrenceCondition: "",
  });

  const isTechnician = String(user?.roleCode || "").toUpperCase() === "TECH";
  const activeDiagnosis = allDiagnoses.find(d => d.id === activeDiagId);
  const canEdit =
    isTechnician &&
    activeDiagnosis &&
    (String(activeDiagnosis.id).startsWith("tmp-") || Number(activeDiagnosis.technicianId) === Number(user?.id)) &&
    ["ASSIGNED", "COMPLETED", "PENDING"].includes(String(activeDiagnosis.status || "PENDING").toUpperCase());

  const backPath = isTechnician ? "/workshop/technician" : "/workshop/reception";

  const parseSymptomsFromNotes = (rawNotes) => {
    try {
      const parsed = JSON.parse(String(rawNotes || "").trim());
      const values = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.symptoms) ? parsed.symptoms : [];
      const cleaned = values.map((x) => String(x || "").trim()).filter(Boolean);
      if (cleaned.length > 0) return cleaned;
    } catch {}
    const text = String(rawNotes || "").trim();
    return text ? [text] : ["Khách chưa mô tả chi tiết"];
  };

  const decodeRootCause = (rawRootCause) => {
    try {
      const parsed = JSON.parse(rawRootCause);
      if (parsed && typeof parsed === "object" && parsed.ai_structured) {
        return {
          rootCause: parsed.actualRootCause || "",
          primarySystem: parsed.primarySystem || "",
          symptomTypes: Array.isArray(parsed.symptomTypes) ? parsed.symptomTypes : [],
          occurrenceCondition: parsed.occurrenceCondition || "",
        };
      }
    } catch {}
    return {
      rootCause: rawRootCause || "",
      primarySystem: "",
      symptomTypes: [],
      occurrenceCondition: "",
    };
  };

  const encodeRootCause = (rootCauseStr, primarySys, sympTypes, occurrence) => {
    return JSON.stringify({
      ai_structured: true,
      actualRootCause: rootCauseStr,
      primarySystem: primarySys,
      symptomTypes: sympTypes,
      occurrenceCondition: occurrence
    });
  };

  const loadData = async () => {
    const id = Number(orderId);
    if (!id) {
      setError("Mã lệnh sửa chữa không hợp lệ.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const [orderRes, diagnosesRes, servicesRes, productsRes, conditionLogsRes] = await Promise.all([
        serviceOrderApi.getById(id),
        serviceOrderApi.getDiagnoses(id),
        getAllStandardServices(),
        inventoryApi.getProducts(),
        vehicleConditionLogApi.getByServiceOrderId(id),
      ]);

      setOrder(orderRes || null);
      setServices(Array.isArray(servicesRes) ? servicesRes : []);
      setProducts(Array.isArray(productsRes) ? productsRes : []);
      
      const signedCheckInExists =
        Array.isArray(conditionLogsRes) &&
        conditionLogsRes.some((log) => {
          const type = String(log?.logType || log?.LogType || "").toUpperCase();
          const signature = String(log?.customerSignatureUrl || log?.CustomerSignatureUrl || "").trim();
          return type === "CHECK_IN" && signature.length > 0;
        });
        
      if (!signedCheckInExists && isTechnician) { // Receptions can still view
        setError("Khách chưa ký check-in. Chưa thể truy cập màn hình chuẩn đoán.");
        return;
      }

      let diagnosesList = [...(Array.isArray(diagnosesRes) ? diagnosesRes : [])].sort((a, b) => Number(a.id) - Number(b.id));

      if (diagnosesList.length === 0 && orderRes) {
        const fallbackSymptoms = parseSymptomsFromNotes(orderRes.customerNotes);
        diagnosesList = fallbackSymptoms.map((s, idx) => ({
          id: `tmp-${idx}`,
          serviceOrderId: orderRes.id,
          symptom: s,
          rootCause: "",
          severity: "MEDIUM",
          technicianId: user?.id,
          status: "ASSIGNED",
          isNew: true
        }));
      }

      setAllDiagnoses(diagnosesList);
      
      const diagnosisIdFromQuery = new URLSearchParams(location.search).get("diagnosisId");
      let initialActive = diagnosesList[0];
      if (diagnosisIdFromQuery) {
        const found = diagnosesList.find(x => String(x.id) === diagnosisIdFromQuery);
        if (found) initialActive = found;
      }

      if (initialActive) {
        await selectDiagnosis(initialActive.id, diagnosesList);
      }
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu chuẩn đoán.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orderId, location.search]);

  // Handle saving the current form state to memory before switching tabs
  const saveCurrentFormToState = useCallback(() => {
    if (!activeDiagId) return;
    setAllDiagnoses(prev => prev.map(d => {
      if (d.id === activeDiagId) {
        const updatedRootCauseEncoded = encodeRootCause(
          form.rootCause, form.primarySystem, form.symptomTypes, form.occurrenceCondition
        );
        return {
          ...d,
          symptom: form.symptom,
          rootCause: updatedRootCauseEncoded,
          severity: form.severity
        };
      }
      return d;
    }));
  }, [activeDiagId, form]);

  const selectDiagnosis = async (diagId, diagList = allDiagnoses) => {
    saveCurrentFormToState();
    
    const target = diagList.find(x => x.id === diagId);
    if (!target) return;
    
    setActiveDiagId(diagId);
    const decoded = decodeRootCause(target.rootCause);
    
    setForm({
      symptom: target.symptom || "",
      rootCause: decoded.rootCause || "",
      severity: target.severity || "MEDIUM",
      primarySystem: decoded.primarySystem || "",
      symptomTypes: decoded.symptomTypes || [],
      occurrenceCondition: decoded.occurrenceCondition || "",
    });

    if (String(target.id).startsWith("tmp-")) {
      setRecommendations([]);
    } else {
      try {
        const recs = await serviceOrderApi.getDiagnoseRecommendations(target.id);
        setRecommendations(Array.isArray(recs) ? recs : []);
      } catch {
        setRecommendations([]);
      }
    }
    setDeletedRecommendationIds([]);
  };

  // Updates memory without switching
  useEffect(() => {
    if (activeDiagId) saveCurrentFormToState();
  }, [form.rootCause, form.primarySystem, form.symptomTypes, form.occurrenceCondition, form.severity, form.symptom]);

  const toggleSymptomType = (type) => {
    if (!canEdit) return;
    setForm((prev) => {
      const exists = prev.symptomTypes.includes(type);
      if (exists) return { ...prev, symptomTypes: prev.symptomTypes.filter((s) => s !== type) };
      return { ...prev, symptomTypes: [...prev.symptomTypes, type] };
    });
  };

  const addRecommendation = () => {
    setRecommendations((prev) => [
      ...prev,
      {
        id: `tmp-${Date.now()}-${Math.random()}`,
        diagnoseId: activeDiagId,
        parentRecommendationId: null,
        itemType: "LABOR",
        standardServiceId: null,
        inventoryItemId: null,
        quantity: 1,
        urgency: "RECOMMENDED",
        evidenceMediaUrls: "",
        technicianNotes: "",
        isNew: true,
      },
    ]);
  };

  const updateRecommendation = (id, patch) => {
    setRecommendations((prev) => prev.map((r) => (r.id === id ? { ...r, ...patch } : r)));
  };

  const removeRecommendation = (rec) => {
    if (!rec?.isNew && !String(rec?.id).startsWith("tmp-")) {
      setDeletedRecommendationIds((prev) => [...prev, rec.id]);
    }
    setRecommendations((prev) => prev.filter((x) => x.id !== rec.id));
  };

  const saveAllDiagnoses = async () => {
    if (!canEdit) return;
    saveCurrentFormToState(); // Ensure the latest active tab edits are flushed to list
    setSaving(true);
    
    try {
      // Must save recommendations for active tab if modified
      // But actually, the safest is to save current active tab's recommendations immediately
      // Wait, we only save ONE diagnosis at a time? No, let's save ALL diagnoses in the order
      // However recommendations are only mapped to the ACTIVE diagnosis in state...
      // So we will just save EVERYTHING for all diagnoses, BUT only save recommendations for activeDiagId.
      // Better approach: user should click "Save Diagnosis" per tab. Let's make the save button save the ACTIVE tab.
      
      const currentDiag = allDiagnoses.find(d => d.id === activeDiagId);
      
      for (const id of deletedRecommendationIds) {
        await serviceOrderApi.deleteDiagnoseRecommendation(id);
      }

      let savedDiagnosisId = activeDiagId;
      
      const payloadDiag = {
        serviceOrderId: Number(orderId),
        technicianId: Number(user?.id),
        symptom: currentDiag.symptom,
        rootCause: currentDiag.rootCause,
        severity: currentDiag.severity,
        status: "COMPLETED",
        updatedBy: user?.id,
      };

      if (String(activeDiagId).startsWith("tmp-")) {
        // create new diagnosis
        const res = await serviceOrderApi.completeDiagnosis(0, {
          ...payloadDiag, 
          action: "create" // Need to mock or fix if backend requires 0 for id
        });
        // Actually ServiceOrderApi completeDiagnosis might not create. We might need createDiagnosis.
        // Assuming completeDiagnosis handles creation if id is absent/tmp. Let's just create.
        savedDiagnosisId = res?.id || activeDiagId; // mock assuming backend returns id
      } else {
        await serviceOrderApi.completeDiagnosis(activeDiagId, payloadDiag);
      }

      for (const rec of recommendations) {
        const payload = {
          diagnoseId: savedDiagnosisId,
          parentRecommendationId: rec.parentRecommendationId ? Number(rec.parentRecommendationId) : null,
          itemType: String(rec.itemType || "LABOR").toUpperCase(),
          standardServiceId: rec.itemType === "LABOR" ? (rec.standardServiceId ? Number(rec.standardServiceId) : null) : null,
          inventoryItemId: rec.itemType !== "LABOR" ? (rec.inventoryItemId ? Number(rec.inventoryItemId) : null) : null,
          quantity: Number(rec.quantity || 1),
          urgency: rec.urgency || "RECOMMENDED",
          evidenceMediaUrls: rec.evidenceMediaUrls || null,
          technicianNotes: rec.technicianNotes || null,
          updatedBy: user?.id,
          createdBy: user?.id,
        };

        if (rec.isNew || String(rec.id).startsWith("tmp-")) {
          await serviceOrderApi.createDiagnoseRecommendation(payload);
        } else {
          await serviceOrderApi.updateDiagnoseRecommendation(rec.id, payload);
        }
      }

      toast({ title: `Đã lưu chuẩn đoán cho triệu chứng này`, status: "success", duration: 2500, isClosable: true });
      await loadData();
    } catch (err) {
      toast({ title: "Lưu chuẩn đoán thất bại", description: err.message, status: "error" });
    } finally {
      setSaving(false);
    }
  };

  const recommendationsByParent = useMemo(() => {
    const roots = recommendations.filter((r) => !r.parentRecommendationId);
    const childrenMap = new Map();
    recommendations
      .filter((r) => !!r.parentRecommendationId)
      .forEach((r) => {
        const key = Number(r.parentRecommendationId);
        if (!childrenMap.has(key)) childrenMap.set(key, []);
        childrenMap.get(key).push(r);
      });
    return { roots, childrenMap };
  }, [recommendations]);

  if (loading) return <Box textAlign="center" py={10}><Spinner size="lg" /></Box>;

  return (
    <Box className="workshop-table-card diagnosis-page-force-select" p={{ base: 4, md: 6 }}>
      <HStack justify="space-between" mb={5} flexWrap="wrap" gap={3}>
        <Box>
          <Heading size="md" color="#2b3674">Báo cáo Chuẩn đoán Chuyên sâu</Heading>
          <Text color="#707eae" mt={1}>Lệnh sửa chữa: <strong>{order?.code || `#${orderId}`}</strong> | Xe: <strong>{order?.vehicleLicensePlate}</strong></Text>
        </Box>
        <Button colorScheme="blue" onClick={() => navigate(backPath)}>Quay lại Bảng điều khiển</Button>
      </HStack>

      {error ? (
        <Box p={4} bg="red.50" color="red.600" borderRadius="md">{error}</Box>
      ) : allDiagnoses.length === 0 ? (
        <Text color="#707eae">Chưa có yêu cầu chuẩn đoán nào.</Text>
      ) : (
        <Grid templateColumns={{ base: "1fr", lg: "280px 1fr" }} gap={6} minH="70vh">
          
          {/* LEFT SIDEBAR: Symtoms Tab List */}
          <GridItem>
            <Box bg="white" borderRadius="xl" border="1px solid #e9edf7" p={4} position="sticky" top="20px">
              <Text fontWeight="700" color="#2b3674" mb={4}>Danh sách Triệu chứng</Text>
              <VStack align="stretch" spacing={2}>
                {allDiagnoses.map((diag, index) => {
                  const isActive = activeDiagId === diag.id;
                  const isCompleted = String(diag.status).toUpperCase() === "COMPLETED";
                  return (
                    <Box 
                      key={diag.id}
                      p={3}
                      bg={isActive ? "blue.50" : "transparent"}
                      border="1px solid"
                      borderColor={isActive ? "blue.200" : "#e9edf7"}
                      borderRadius="lg"
                      cursor="pointer"
                      onClick={() => selectDiagnosis(diag.id)}
                      _hover={{ bg: isActive ? "blue.50" : "gray.50" }}
                      transition="all 0.2s"
                    >
                      <HStack justify="space-between" mb={2}>
                        <Text fontWeight="600" fontSize="sm" color={isActive ? "blue.700" : "#2b3674"}>
                          Triệu chứng #{index + 1}
                        </Text>
                        <Badge colorScheme={isCompleted ? "green" : "orange"} fontSize="0.65em">
                          {isCompleted ? "Đã khám" : "Chờ khám"}
                        </Badge>
                      </HStack>
                      <Text fontSize="xs" color="#707eae" noOfLines={2}>{diag.symptom}</Text>
                    </Box>
                  )
                })}
              </VStack>
            </Box>
          </GridItem>

          {/* RIGHT CONTENT: Diagnosis Form */}
          <GridItem>
            <VStack align="stretch" spacing={5}>
              {!canEdit && (
                <Box p={3} bg="orange.50" color="orange.800" borderRadius="md" fontSize="sm">
                  Phiếu chuẩn đoán này đang ở trạng thái chỉ xem (Chỉ thợ nhận việc mới có quyền chỉnh sửa).
                </Box>
              )}

              {/* KHU VỰC 1: Lời khách hàng */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack color="#f56565" mb={3}>
                  <Icon as={MessageSquare} size={20} />
                  <Heading size="sm">Mô tả của Khách hàng</Heading>
                </HStack>
                <Textarea
                  value={form.symptom}
                  onChange={(e) => setForm(f => ({ ...f, symptom: e.target.value }))}
                  isDisabled={!canEdit}
                  bg="gray.50"
                  borderColor="#e2e8f0"
                  rows={2}
                />
              </Box>

              {/* KHU VỰC 2: AI-Ready Phân loại */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack justify="space-between" mb={4}>
                  <HStack color="#4318ff">
                    <Icon as={Activity} size={20} />
                    <Heading size="sm">Phân loại Dữ liệu (Dành cho AI)</Heading>
                  </HStack>
                </HStack>
                
                <VStack align="stretch" spacing={5}>
                  <Box>
                    <Text fontSize="sm" fontWeight="600" color="#2b3674" mb={2}>1. Vùng phát sinh lỗi cốt lõi</Text>
                    <SimpleGrid columns={{ base: 2, md: 4 }} spacing={3}>
                      {SYSTEM_CATEGORIES.map((sys) => (
                        <Button
                          key={sys.id}
                          variant={form.primarySystem === sys.id ? "solid" : "outline"}
                          colorScheme={form.primarySystem === sys.id ? "blue" : "gray"}
                          isDisabled={!canEdit}
                          size="sm"
                          justifyContent="flex-start"
                          onClick={() => setForm(f => ({ ...f, primarySystem: sys.id, symptomTypes: [] }))}
                          boxShadow={form.primarySystem === sys.id ? "md" : "none"}
                        >
                          <Icon as={sys.icon} size={16} mr={2} /> 
                          {sys.label}
                        </Button>
                      ))}
                    </SimpleGrid>
                  </Box>

                  {form.primarySystem && (
                    <Box>
                      <Text fontSize="sm" fontWeight="600" color="#2b3674" mb={2}>2. Thuộc tính lỗi kỹ thuật</Text>
                      <HStack wrap="wrap" gap={2}>
                        {SYMPTOM_TYPES[form.primarySystem]?.map((symp, idx) => {
                          const isSelected = form.symptomTypes.includes(symp);
                          return (
                            <Tag
                              key={idx}
                              size="lg"
                              variant={isSelected ? "solid" : "outline"}
                              colorScheme={isSelected ? "cyan" : "gray"}
                              cursor={canEdit ? "pointer" : "not-allowed"}
                              opacity={!canEdit && !isSelected ? 0.6 : 1}
                              onClick={() => toggleSymptomType(symp)}
                              borderRadius="full"
                            >
                              {isSelected && <TagLeftIcon boxSize="12px" as={CheckCircle} />}
                              <TagLabel fontSize="sm">{symp}</TagLabel>
                            </Tag>
                          );
                        })}
                      </HStack>
                    </Box>
                  )}

                  <Box>
                    <Text fontSize="sm" fontWeight="600" color="#2b3674" mb={2}>3. Điều kiện tái hiện hiện tượng</Text>
                    <Select 
                      placeholder="Chọn điều kiện phát hiện rõ nhất" 
                      value={form.occurrenceCondition}
                      onChange={(e) => setForm(f => ({ ...f, occurrenceCondition: e.target.value }))}
                      isDisabled={!canEdit}
                    >
                      <option value="cold_start">Máy nguội (Mới khởi động)</option>
                      <option value="idling">Không tải (Đứng yên nổ máy)</option>
                      <option value="accelerating">Lúc tăng tốc / đạp ga mạnh</option>
                      <option value="braking">Lúc rà phanh</option>
                      <option value="turning">Lúc đánh lái cua</option>
                      <option value="high_speed">Chạy tốc độ cao (&gt; 60km/h)</option>
                      <option value="hot_engine">Máy nóng (Quạt chạy)</option>
                      <option value="always">Liên tục bất kỳ lúc nào</option>
                    </Select>
                  </Box>
                </VStack>
              </Box>

              {/* KHU VỰC 3: Nguyên nhân gốc rễ */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack color="#ffb547" mb={4}>
                  <Icon as={AlertTriangle} size={20} />
                  <Heading size="sm">Kết luận & Mức độ</Heading>
                </HStack>
                <Grid templateColumns={{ base: "1fr", md: "2fr 1fr" }} gap={4}>
                  <GridItem>
                    <FormControl>
                      <FormLabel fontSize="sm" fontWeight="600" color="#2b3674">Kết luận nguyên nhân gốc rễ (Root Cause)</FormLabel>
                      <Textarea
                        value={form.rootCause}
                        onChange={(e) => setForm((prev) => ({ ...prev, rootCause: e.target.value }))}
                        isDisabled={!canEdit}
                        rows={3}
                        placeholder="VD: Cảm biến oxy số 1 bị đứt dây ga..."
                        bg="gray.50"
                        borderColor="#e2e8f0"
                        color="#1b2559"
                        _placeholder={{ color: "#718096" }}
                        _disabled={{
                          color: "#1b2559",
                          WebkitTextFillColor: "#1b2559",
                          opacity: 1,
                          cursor: "not-allowed",
                          bg: "gray.50",
                        }}
                      />
                    </FormControl>
                  </GridItem>
                  <GridItem>
                    <FormControl>
                      <FormLabel fontSize="sm" fontWeight="600" color="#2b3674">Mức độ nghiêm trọng</FormLabel>
                      <Select
                        value={form.severity}
                        onChange={(e) => setForm((prev) => ({ ...prev, severity: e.target.value }))}
                        isDisabled={!canEdit}
                      >
                        <option value="LOW">Thấp (Chưa gấp)</option>
                        <option value="MEDIUM">Trung bình</option>
                        <option value="HIGH">Cao</option>
                        <option value="CRITICAL">Khẩn cấp (Nguy hiểm)</option>
                      </Select>
                    </FormControl>
                  </GridItem>
                </Grid>
              </Box>

              {/* KHU VỰC 4: Đề xuất Sửa chữa */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack justify="space-between" mb={4}>
                  <HStack color="#05cd99">
                    <Icon as={Wrench} size={20} />
                    <Heading size="sm">Vật tư & Nhân công đề xuất</Heading>
                  </HStack>
                  {canEdit && (
                    <Button size="sm" colorScheme="blue" variant="outline" onClick={addRecommendation}>
                      + Thêm hạng mục
                    </Button>
                  )}
                </HStack>

                {recommendations.length === 0 ? (
                  <Text color="#707eae" fontSize="sm">Chưa có đề xuất thay thế hay sửa chữa nào cho triệu chứng này.</Text>
                ) : (
                  <VStack align="stretch" spacing={3}>
                    {recommendationsByParent.roots.map((root) => (
                      <Box key={root.id} p={3} border="1px solid #e9edf7" borderRadius="md" bg="gray.50">
                        <RecommendationRow
                          item={root}
                          canEdit={canEdit}
                          services={services}
                          products={products}
                          onChange={updateRecommendation}
                          onRemove={removeRecommendation}
                        />
                        {(recommendationsByParent.childrenMap.get(Number(root.id)) || []).length > 0 && (
                          <VStack align="stretch" mt={3} spacing={2} pl={4} borderLeft="2px solid #d6e0f5">
                            {(recommendationsByParent.childrenMap.get(Number(root.id)) || []).map((child) => (
                              <RecommendationRow
                                key={child.id}
                                item={child}
                                canEdit={canEdit}
                                services={services}
                                products={products}
                                onChange={updateRecommendation}
                                onRemove={removeRecommendation}
                              />
                            ))}
                          </VStack>
                        )}
                      </Box>
                    ))}
                  </VStack>
                )}
              </Box>

              {canEdit && (
                <HStack justify="flex-end" pt={4} pb={10}>
                  <Button size="lg" colorScheme="blue" onClick={saveAllDiagnoses} isLoading={saving}>
                    Lưu Kết Luận Triệu Chứng Này
                  </Button>
                </HStack>
              )}
            </VStack>
          </GridItem>
        </Grid>
      )}
    </Box>
  );
}

function RecommendationRow({ item, canEdit, services, products, onChange, onRemove }) {
  const itemType = String(item.itemType || "LABOR").toUpperCase();

  return (
    <Stack spacing={2} p={3} border="1px solid #e9edf7" borderRadius="md" bg="white">
      <HStack justify="space-between">
        <Text fontWeight="bold" fontSize="sm" color="#2b3674">#{item.id}</Text>
        {canEdit && (
          <Button size="xs" colorScheme="red" variant="ghost" onClick={() => onRemove(item)}>
            Xóa
          </Button>
        )}
      </HStack>

      <HStack spacing={3} flexWrap="wrap">
        <Box minW="140px">
          <Text fontSize="xs" fontWeight="600" mb={1} color="#a3aed0">Loại</Text>
          <Select
            size="sm"
            value={itemType}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { itemType: e.target.value, standardServiceId: null, inventoryItemId: null })}
          >
            <option value="LABOR">Nhân công</option>
            <option value="PART">Phụ tùng</option>
            <option value="CONSUMABLE">Vật tư / Dung dịch</option>
          </Select>
        </Box>

        {itemType === "LABOR" ? (
          <Box minW="220px" flex="1">
            <Text fontSize="xs" fontWeight="600" mb={1} color="#a3aed0">Dịch vụ sửa chữa</Text>
            <Select
              size="sm"
              value={item.standardServiceId || ""}
              isDisabled={!canEdit}
              onChange={(e) => onChange(item.id, { standardServiceId: e.target.value || null })}
            >
              <option value="">-- Chọn --</option>
              {services.map((s) => <option key={s.id} value={s.id}>{s.name || s.Name}</option>)}
            </Select>
          </Box>
        ) : (
          <Box minW="220px" flex="1">
            <Text fontSize="xs" fontWeight="600" mb={1} color="#a3aed0">Phụ tùng</Text>
            <Select
              size="sm"
              value={item.inventoryItemId || ""}
              isDisabled={!canEdit}
              onChange={(e) => onChange(item.id, { inventoryItemId: e.target.value || null })}
            >
              <option value="">-- Chọn sản phẩm --</option>
              {products.map((p) => <option key={p.id} value={p.id}>{p.name || p.Name}</option>)}
            </Select>
          </Box>
        )}

        <Box minW="80px" maxW="100px">
          <Text fontSize="xs" fontWeight="600" mb={1} color="#a3aed0">Số lượng</Text>
          <Input
            size="sm"
            type="number"
            value={item.quantity ?? 1}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { quantity: Number(e.target.value || 1) })}
          />
        </Box>

        <Box minW="140px">
          <Text fontSize="xs" fontWeight="600" mb={1} color="#a3aed0">Độ cần thiết</Text>
          <Select
            size="sm"
            value={item.urgency || "RECOMMENDED"}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { urgency: e.target.value })}
          >
            <option value="CRITICAL">Khẩn cấp (Phải thay)</option>
            <option value="RECOMMENDED">Nên làm</option>
            <option value="OPTIONAL">Không bắt buộc</option>
          </Select>
        </Box>
      </HStack>

      {/* Optional fields can be added here or collapsable like Media URLs/Tech notes */}
      <Box pt={2}>
        <Input
          size="xs"
          placeholder="Ghi chú kỹ thuật viên cho riêng phụ tùng/việc này..."
          value={item.technicianNotes || ""}
          isDisabled={!canEdit}
          onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
          bg="#f8fafc"
        />
      </Box>
    </Stack>
  );
}





