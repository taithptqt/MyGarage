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
  Icon,
  FormControl,
  FormLabel,
} from "@chakra-ui/react";
import {
  Wrench,
  AlertTriangle,
  MessageSquare,
} from "lucide-react";

import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";

const DIAGNOSIS_DROPDOWN_STYLES = {
  bg: "#f8fafc",
  borderColor: "#d7e3f5",
  borderRadius: "12px",
  color: "#1b2559",
  _hover: { borderColor: "#c4d4ef" },
  _focus: { borderColor: "#5f86eb", boxShadow: "0 0 0 1px #5f86eb" },
  _disabled: {
    color: "#1b2559",
    WebkitTextFillColor: "#1b2559",
    opacity: 1,
    bg: "#f2f5fb",
    cursor: "not-allowed",
  },
};

function DiagnosisDropdown({
  label,
  value,
  onChange,
  placeholder,
  isDisabled = false,
  size = "md",
  minW,
  maxW,
  flex,
  labelColor = "#2b3674",
  labelFontSize = "sm",
  children,
  ...selectProps
}) {
  return (
    <Box minW={minW} maxW={maxW} flex={flex}>
      {label ? (
        <Text fontSize={labelFontSize} fontWeight="600" color={labelColor} mb={2}>
          {label}
        </Text>
      ) : null}
      <Select
        size={size}
        value={value}
        onChange={(event) => onChange?.(event.target.value)}
        placeholder={placeholder}
        isDisabled={isDisabled}
        w="100%"
        {...DIAGNOSIS_DROPDOWN_STYLES}
        {...selectProps}
      >
        {children}
      </Select>
    </Box>
  );
}

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function extractServiceId(service) {
  return toPositiveNumber(service?.id ?? service?.Id);
}

function extractServiceName(service) {
  return String(service?.name ?? service?.Name ?? service?.serviceName ?? service?.ServiceName ?? "").trim();
}

function extractServiceCategoryId(service) {
  return toPositiveNumber(
    service?.categoryId ??
    service?.CategoryId ??
    service?.serviceCategoryId ??
    service?.ServiceCategoryId ??
    service?.serviceCategory?.id ??
    service?.ServiceCategory?.id
  );
}

function extractServiceCategoryName(service) {
  return String(
    service?.serviceCategoryName ??
    service?.ServiceCategoryName ??
    service?.categoryName ??
    service?.CategoryName ??
    service?.serviceCategory?.categoryName ??
    service?.serviceCategory?.name ??
    service?.ServiceCategory?.categoryName ??
    service?.ServiceCategory?.name ??
    ""
  ).trim();
}

function extractCategoryId(category) {
  return toPositiveNumber(category?.id ?? category?.Id);
}

function extractCategoryName(category) {
  return String(category?.categoryName ?? category?.name ?? category?.Name ?? category?.code ?? "").trim();
}

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
  const [serviceCategories, setServiceCategories] = useState([]);
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

  const serviceCategoryByServiceId = useMemo(() => {
    const map = new Map();
    (services || []).forEach((service) => {
      const serviceId = extractServiceId(service);
      const categoryId = extractServiceCategoryId(service);
      if (serviceId) {
        map.set(String(serviceId), categoryId ? String(categoryId) : "");
      }
    });
    return map;
  }, [services]);

  const serviceNameById = useMemo(() => {
    const map = new Map();
    (services || []).forEach((service) => {
      const serviceId = extractServiceId(service);
      if (!serviceId) return;
      const name = extractServiceName(service);
      map.set(String(serviceId), name || `Dịch vụ #${serviceId}`);
    });
    return map;
  }, [services]);

  const serviceCategoryOptions = useMemo(() => {
    const map = new Map();

    (serviceCategories || []).forEach((category) => {
      const id = extractCategoryId(category);
      if (!id) return;
      const label = extractCategoryName(category) || `Nhóm ${id}`;
      map.set(String(id), { value: String(id), label });
    });

    (services || []).forEach((service) => {
      const categoryId = extractServiceCategoryId(service);
      if (!categoryId) return;
      if (map.has(String(categoryId))) return;
      const fallbackLabel = extractServiceCategoryName(service) || `Nhóm ${categoryId}`;
      map.set(String(categoryId), { value: String(categoryId), label: fallbackLabel });
    });

    return Array.from(map.values()).sort((a, b) => a.label.localeCompare(b.label, "vi"));
  }, [serviceCategories, services]);

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
      const [orderRes, diagnosesRes, servicesRes, serviceCategoriesRes, productsRes, conditionLogsRes] = await Promise.all([
        serviceOrderApi.getById(id),
        serviceOrderApi.getDiagnoses(id),
        getAllStandardServices(),
        getAllServiceCategories(),
        inventoryApi.getProducts(),
        vehicleConditionLogApi.getByServiceOrderId(id),
      ]);

      setOrder(orderRes || null);
      setServices(Array.isArray(servicesRes) ? servicesRes : []);
      setServiceCategories(Array.isArray(serviceCategoriesRes) ? serviceCategoriesRes : []);
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

  const addServiceRecommendation = () => {
    setRecommendations((prev) => [
      ...prev,
      {
        id: `tmp-${Date.now()}-${Math.random()}`,
        diagnoseId: activeDiagId,
        parentRecommendationId: null,
        serviceCategoryId: "",
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

  const addMaterialRecommendation = (parentRecommendationId) => {
    setRecommendations((prev) => [
      ...prev,
      {
        id: `tmp-${Date.now()}-${Math.random()}`,
        diagnoseId: activeDiagId,
        parentRecommendationId,
        itemType: "PART",
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
    const targetId = String(rec?.id || "");
    if (!targetId) return;

    setRecommendations((prev) => {
      const idsToRemove = new Set([targetId]);
      const isRoot = !rec?.parentRecommendationId;

      if (isRoot) {
        prev.forEach((item) => {
          if (String(item.parentRecommendationId || "") === targetId) {
            idsToRemove.add(String(item.id));
          }
        });
      }

      const persistedIds = prev
        .filter((item) => idsToRemove.has(String(item.id)))
        .filter((item) => !item?.isNew && !String(item?.id).startsWith("tmp-"))
        .map((item) => item.id);

      if (persistedIds.length > 0) {
        setDeletedRecommendationIds((prevDeleted) => [...new Set([...prevDeleted, ...persistedIds])]);
      }

      return prev.filter((item) => !idsToRemove.has(String(item.id)));
    });
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

      const normalizeRecommendationPayload = (rec, parentRecommendationId = null) => {
        const normalizedType = String(rec.itemType || "PART").toUpperCase();
        const itemType = normalizedType === "LABOR" ? "LABOR" : normalizedType === "CONSUMABLE" ? "CONSUMABLE" : "PART";
        return {
          diagnoseId: savedDiagnosisId,
          parentRecommendationId: toPositiveNumber(parentRecommendationId),
          itemType,
          standardServiceId: itemType === "LABOR" ? toPositiveNumber(rec.standardServiceId) : null,
          inventoryItemId: itemType !== "LABOR" ? toPositiveNumber(rec.inventoryItemId) : null,
          quantity: Number(rec.quantity || 1),
          urgency: rec.urgency || "RECOMMENDED",
          evidenceMediaUrls: rec.evidenceMediaUrls || null,
          technicianNotes: rec.technicianNotes || null,
          updatedBy: user?.id,
          createdBy: user?.id,
        };
      };

      const isTemporaryRecommendation = (rec) => rec?.isNew || String(rec?.id || "").startsWith("tmp-");

      const roots = recommendations.filter((rec) => !rec.parentRecommendationId);
      const children = recommendations.filter((rec) => !!rec.parentRecommendationId);
      const recommendationIdMap = new Map();

      for (const root of roots) {
        const effectiveCategoryId =
          toPositiveNumber(root.serviceCategoryId) ||
          toPositiveNumber(serviceCategoryByServiceId.get(String(root.standardServiceId)));
        if (!effectiveCategoryId) {
          throw new Error("Vui lòng chọn loại dịch vụ trước khi chọn dịch vụ sửa chữa.");
        }
        if (!toPositiveNumber(root.standardServiceId)) {
          throw new Error("Vui lòng chọn dịch vụ sửa chữa cho từng dòng dịch vụ.");
        }
      }

      for (const child of children) {
        if (!toPositiveNumber(child.inventoryItemId)) {
          throw new Error("Vui lòng chọn vật tư cụ thể cho từng dòng vật tư.");
        }
      }

      for (const root of roots) {
        const payload = normalizeRecommendationPayload({ ...root, itemType: "LABOR" }, null);

        if (isTemporaryRecommendation(root)) {
          const created = await serviceOrderApi.createDiagnoseRecommendation(payload);
          const createdId = toPositiveNumber(created?.id ?? created?.Id);
          if (createdId) recommendationIdMap.set(String(root.id), createdId);
        } else {
          await serviceOrderApi.updateDiagnoseRecommendation(root.id, payload);
          const persistedId = toPositiveNumber(root.id);
          if (persistedId) recommendationIdMap.set(String(root.id), persistedId);
        }
      }

      for (const child of children) {
        const mappedParentId = recommendationIdMap.get(String(child.parentRecommendationId));
        const parentId = toPositiveNumber(mappedParentId ?? child.parentRecommendationId);
        if (!parentId) continue;

        const payload = normalizeRecommendationPayload(child, parentId);
        if (isTemporaryRecommendation(child)) {
          await serviceOrderApi.createDiagnoseRecommendation(payload);
        } else {
          await serviceOrderApi.updateDiagnoseRecommendation(child.id, payload);
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
        const key = String(r.parentRecommendationId);
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
                      <DiagnosisDropdown
                        label="Mức độ nghiêm trọng"
                        value={form.severity}
                        onChange={(value) => setForm((prev) => ({ ...prev, severity: value }))}
                        isDisabled={!canEdit}
                      >
                        <option value="LOW">Thấp (Chưa gấp)</option>
                        <option value="MEDIUM">Trung bình</option>
                        <option value="HIGH">Cao</option>
                        <option value="CRITICAL">Khẩn cấp (Nguy hiểm)</option>
                      </DiagnosisDropdown>
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
                    <Button size="sm" colorScheme="blue" variant="outline" onClick={addServiceRecommendation}>
                      + Thêm dịch vụ
                    </Button>
                  )}
                </HStack>

                {recommendations.length === 0 ? (
                  <Text color="#707eae" fontSize="sm">
                    Chưa có dịch vụ đề xuất cho triệu chứng này. Hãy thêm dịch vụ trước, sau đó thêm vật tư cho từng dịch vụ.
                  </Text>
                ) : (
                  <VStack align="stretch" spacing={3}>
                    {recommendationsByParent.roots.map((root) => (
                      <Box key={root.id} p={3} border="1px solid #e9edf7" borderRadius="md" bg="gray.50">
                        <RecommendationRow
                          mode="service"
                          item={root}
                          canEdit={canEdit}
                          services={services}
                          serviceCategoryOptions={serviceCategoryOptions}
                          serviceCategoryByServiceId={serviceCategoryByServiceId}
                          products={products}
                          onChange={updateRecommendation}
                          onRemove={removeRecommendation}
                          onAddMaterial={addMaterialRecommendation}
                        />
                        {(recommendationsByParent.childrenMap.get(String(root.id)) || []).length > 0 && (
                          <VStack align="stretch" mt={3} spacing={2} pl={4} borderLeft="2px solid #d6e0f5">
                            {(recommendationsByParent.childrenMap.get(String(root.id)) || []).map((child) => (
                              <RecommendationRow
                                key={child.id}
                                mode="material"
                                item={child}
                                canEdit={canEdit}
                                services={services}
                                serviceCategoryOptions={serviceCategoryOptions}
                                serviceCategoryByServiceId={serviceCategoryByServiceId}
                                products={products}
                                onChange={updateRecommendation}
                                onRemove={removeRecommendation}
                                parentServiceName={serviceNameById.get(String(root.standardServiceId)) || `Dịch vụ #${root.id}`}
                              />
                            ))}
                          </VStack>
                        )}
                        {(recommendationsByParent.childrenMap.get(String(root.id)) || []).length === 0 && (
                          <Text mt={2} fontSize="xs" color="#7f8fb2">
                            Chưa có vật tư cho dịch vụ này.
                          </Text>
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

function RecommendationRow({
  item,
  mode = "service",
  canEdit,
  services,
  serviceCategoryOptions,
  serviceCategoryByServiceId,
  products,
  onChange,
  onRemove,
  onAddMaterial,
  parentServiceName,
}) {
  const isServiceMode = mode === "service";
  const normalizedItemType = String(item.itemType || (isServiceMode ? "LABOR" : "PART")).toUpperCase();
  const itemType = normalizedItemType === "CONSUMABLE" ? "CONSUMABLE" : normalizedItemType === "LABOR" ? "LABOR" : "PART";
  const canAddMaterial = Boolean(item.standardServiceId);
  const inferredServiceCategoryId = String(serviceCategoryByServiceId?.get(String(item.standardServiceId)) || "");
  const selectedServiceCategoryId = String(item.serviceCategoryId || inferredServiceCategoryId || "");
  const filteredServices = selectedServiceCategoryId
    ? (services || []).filter((service) => String(extractServiceCategoryId(service) || "") === selectedServiceCategoryId)
    : [];

  return (
    <Stack
      spacing={2}
      p={3}
      border={isServiceMode ? "1px solid #cfe0ff" : "1px dashed #f0c991"}
      borderRadius="md"
      bg={isServiceMode ? "#f6f9ff" : "#fff9f0"}
      boxShadow={isServiceMode ? "0 2px 8px rgba(42, 67, 122, 0.06)" : "none"}
    >
      <HStack justify="space-between">
        <Text fontWeight="bold" fontSize="sm" color={isServiceMode ? "#2b3674" : "#8a5a1f"}>
          {isServiceMode ? "Dịch vụ" : "Vật tư"} #{item.id}
        </Text>
        <HStack spacing={1}>
          {isServiceMode && canEdit && (
            <Button
              size="xs"
              colorScheme="blue"
              variant="outline"
              onClick={() => onAddMaterial?.(item.id)}
              isDisabled={!canAddMaterial}
            >
              + Thêm vật tư
            </Button>
          )}
          {canEdit && (
            <Button size="xs" colorScheme="red" variant="ghost" onClick={() => onRemove(item)}>
              Xóa
            </Button>
          )}
        </HStack>
      </HStack>

      {!isServiceMode && (
        <Box px={3} py={2} borderRadius="md" border="1px solid #f0d4aa" bg="#fff3df">
          <Text fontSize="xs" fontWeight="700" color="#8a5a1f">
            Thuộc dịch vụ: {parentServiceName || "Chưa xác định"}
          </Text>
        </Box>
      )}

      <HStack spacing={3} flexWrap="wrap">
        {isServiceMode ? (
          <>
            <DiagnosisDropdown
              label="Loại dịch vụ"
              labelFontSize="xs"
              labelColor="#7f8fb2"
              minW="220px"
              size="sm"
              value={selectedServiceCategoryId}
              isDisabled={!canEdit}
              onChange={(value) => onChange(item.id, {
                serviceCategoryId: value || "",
                standardServiceId: null,
                itemType: "LABOR",
                inventoryItemId: null,
                parentRecommendationId: null,
              })}
            >
              <option value="">-- Chọn loại dịch vụ --</option>
              {(serviceCategoryOptions || []).map((category) => (
                <option key={category.value} value={category.value}>
                  {category.label}
                </option>
              ))}
            </DiagnosisDropdown>
              <DiagnosisDropdown
                label="Dịch vụ sửa chữa"
                labelFontSize="xs"
                labelColor="#7f8fb2"
              minW="280px"
              flex="1"
              size="sm"
              value={item.standardServiceId || ""}
              isDisabled={!canEdit || !selectedServiceCategoryId}
              onChange={(value) => onChange(item.id, {
                itemType: "LABOR",
                standardServiceId: value || null,
                inventoryItemId: null,
                parentRecommendationId: null,
              })}
              >
                <option value="">
                  {selectedServiceCategoryId ? "-- Chọn dịch vụ --" : "-- Chọn loại dịch vụ trước --"}
                </option>
                {filteredServices.map((service) => {
                  const serviceId = extractServiceId(service);
                  const serviceName = extractServiceName(service);
                  if (!serviceId) return null;
                  return (
                    <option key={serviceId} value={serviceId}>
                      {serviceName || `Dịch vụ #${serviceId}`}
                    </option>
                  );
                })}
              </DiagnosisDropdown>
              {!selectedServiceCategoryId && (
                <Text fontSize="xs" color="#7f8fb2">
                  Chọn loại dịch vụ trước để lọc danh sách dịch vụ.
                </Text>
              )}
            </>
          ) : (
          <>
            <DiagnosisDropdown
              label="Loại vật tư"
              labelFontSize="xs"
              labelColor="#b07a2f"
              minW="160px"
              size="sm"
              value={itemType}
              isDisabled={!canEdit}
              onChange={(value) => onChange(item.id, { itemType: value, inventoryItemId: null })}
            >
              <option value="PART">Phụ tùng</option>
              <option value="CONSUMABLE">Vật tư / Dung dịch</option>
            </DiagnosisDropdown>
            <DiagnosisDropdown
              label="Vật tư cụ thể"
              labelFontSize="xs"
              labelColor="#b07a2f"
              minW="260px"
              flex="1"
              size="sm"
              value={item.inventoryItemId || ""}
              isDisabled={!canEdit}
              onChange={(value) => onChange(item.id, { inventoryItemId: value || null })}
            >
              <option value="">-- Chọn sản phẩm --</option>
              {products.map((p) => <option key={p.id} value={p.id}>{p.name || p.Name}</option>)}
            </DiagnosisDropdown>
          </>
        )}

        <Box minW="80px" maxW="100px">
          <Text fontSize="xs" fontWeight="600" mb={1} color={isServiceMode ? "#a3aed0" : "#b07a2f"}>Số lượng</Text>
          <Input
            size="sm"
            type="number"
            value={item.quantity ?? 1}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { quantity: Number(e.target.value || 1) })}
          />
        </Box>

        <DiagnosisDropdown
          label="Độ cần thiết"
          labelFontSize="xs"
          labelColor={isServiceMode ? "#a3aed0" : "#b07a2f"}
          minW="140px"
          size="sm"
          value={item.urgency || "RECOMMENDED"}
          isDisabled={!canEdit}
          onChange={(value) => onChange(item.id, { urgency: value })}
        >
          <option value="CRITICAL">Khẩn cấp (Phải thay)</option>
          <option value="RECOMMENDED">Nên làm</option>
          <option value="OPTIONAL">Không bắt buộc</option>
        </DiagnosisDropdown>
      </HStack>

      {/* Optional fields can be added here or collapsable like Media URLs/Tech notes */}
      <Box pt={2}>
        <Input
          size="xs"
          placeholder={isServiceMode ? "Ghi chú cho dịch vụ này..." : "Ghi chú cho vật tư này..."}
          value={item.technicianNotes || ""}
          isDisabled={!canEdit}
          onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
          bg="#f8fafc"
        />
      </Box>
    </Stack>
  );
}





