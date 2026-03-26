import React, { useEffect, useMemo, useState } from "react";
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
} from "@chakra-ui/react";
import { serviceOrderApi } from "../api/serviceOrderApi";
import { getAllStandardServices } from "../../admin/api/serviceApi";
import { inventoryApi } from "../../admin/api/inventoryApi";
import { useAuth } from "../../../context/AuthContext";
import { vehicleConditionLogApi } from "../api/vehicleConditionLogApi";

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
  const [diagnosis, setDiagnosis] = useState(null);
  const [recommendations, setRecommendations] = useState([]);
  const [deletedRecommendationIds, setDeletedRecommendationIds] = useState([]);
  const [services, setServices] = useState([]);
  const [products, setProducts] = useState([]);

  const [form, setForm] = useState({
    symptom: "",
    rootCause: "",
    severity: "MEDIUM",
  });

  const isTechnician = String(user?.roleCode || "").toUpperCase() === "TECH";
  const canEdit =
    isTechnician &&
    diagnosis &&
    Number(diagnosis.technicianId) === Number(user?.id) &&
    ["ASSIGNED", "COMPLETED"].includes(String(diagnosis.status || "").toUpperCase());

  const backPath = isTechnician ? "/workshop/technician" : "/workshop/reception";

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
      if (!signedCheckInExists) {
        setError("Khách chưa ký check-in. Chưa thể truy cập màn hình chuẩn đoán.");
        setDiagnosis(null);
        setRecommendations([]);
        return;
      }

      const diagnosisIdFromQuery = Number(new URLSearchParams(location.search).get("diagnosisId"));
      const sortedDiagnoses = [...(Array.isArray(diagnosesRes) ? diagnosesRes : [])]
        .sort((a, b) => {
          const at = new Date(a?.createdAt || a?.updatedAt || 0).getTime();
          const bt = new Date(b?.createdAt || b?.updatedAt || 0).getTime();
          return bt - at;
        });
      const latestDiagnosis =
        sortedDiagnoses.find((x) => Number(x.id) === diagnosisIdFromQuery) ||
        sortedDiagnoses[0] ||
        null;

      setDiagnosis(latestDiagnosis);

      if (latestDiagnosis) {
        setForm({
          symptom: latestDiagnosis.symptom || orderRes?.customerNotes || "",
          rootCause: latestDiagnosis.rootCause || "",
          severity: latestDiagnosis.severity || "MEDIUM",
        });

        const recs = await serviceOrderApi.getDiagnoseRecommendations(latestDiagnosis.id);
        setRecommendations(Array.isArray(recs) ? recs : []);
      } else {
        setRecommendations([]);
      }
      setDeletedRecommendationIds([]);
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu chẩn đoán.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [orderId, location.search]);

  const addRecommendation = () => {
    setRecommendations((prev) => [
      ...prev,
      {
        id: `tmp-${Date.now()}-${Math.random()}`,
        diagnoseId: diagnosis?.id,
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
    if (!rec?.isNew && Number(rec?.id) > 0) {
      setDeletedRecommendationIds((prev) => [...prev, rec.id]);
    }
    setRecommendations((prev) => prev.filter((x) => x.id !== rec.id));
  };

  const saveCompletedDiagnosis = async () => {
    if (!diagnosis?.id || !canEdit) return;

    setSaving(true);
    try {
      for (const id of deletedRecommendationIds) {
        await serviceOrderApi.deleteDiagnoseRecommendation(id);
      }

      for (const rec of recommendations) {
        const payload = {
          diagnoseId: diagnosis.id,
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

      await serviceOrderApi.completeDiagnosis(diagnosis.id, {
        serviceOrderId: diagnosis.serviceOrderId,
        technicianId: diagnosis.technicianId,
        symptom: form.symptom,
        rootCause: form.rootCause,
        severity: form.severity,
        status: "COMPLETED",
        updatedBy: user?.id,
      });

      toast({ title: "Đã lưu hoàn tất chẩn đoán", status: "success", duration: 2500, isClosable: true });
      await loadData();
    } catch (err) {
      toast({ title: "Lưu chẩn đoán thất bại", description: err.message, status: "error" });
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

  if (loading) {
    return (
      <Box textAlign="center" py={10}>
        <Spinner size="lg" />
      </Box>
    );
  }

  return (
    <div className="workshop-table-card" style={{ padding: "24px" }}>
      <HStack justify="space-between" mb={5} flexWrap="wrap" gap={3}>
        <Box>
          <Heading size="md">Chi tiết chuẩn đoán</Heading>
          <Text color="#707eae" mt={1}>
            Lệnh: <strong>{order?.code || `#${orderId}`}</strong>
          </Text>
        </Box>
        <Button colorScheme="blue" onClick={() => navigate(backPath)}>
          Quay lại
        </Button>
      </HStack>

      {error ? (
        <Text color="red.400">{error}</Text>
      ) : !diagnosis ? (
        <Text color="#707eae">Chưa có yêu cầu chuẩn đoán cho lệnh này.</Text>
      ) : (
        <VStack align="stretch" spacing={4}>
          <Box p={4} bg="gray.50" borderRadius="md" border="1px solid #e9edf7">
            <HStack justify="space-between" mb={2}>
              <Text fontWeight="bold">Trạng thái chuẩn đoán</Text>
              <Badge colorScheme={String(diagnosis.status || "").toUpperCase() === "COMPLETED" ? "green" : "orange"}>
                {String(diagnosis.status || "").toUpperCase()}
              </Badge>
            </HStack>
            <Text fontSize="sm" color="#707eae">
              Kỹ thuật viên nhận: ID {diagnosis.technicianId}
            </Text>
          </Box>

          <Box p={4} bg="white" borderRadius="md" border="1px solid #e9edf7">
            <Text fontWeight="bold" mb={2}>Triệu chứng ban đầu</Text>
            <Textarea
              value={form.symptom}
              onChange={(e) => setForm((prev) => ({ ...prev, symptom: e.target.value }))}
              isDisabled={!canEdit}
            />

            <Text fontWeight="bold" mt={4} mb={2}>Nguyên nhân gốc rễ</Text>
            <Textarea
              value={form.rootCause}
              onChange={(e) => setForm((prev) => ({ ...prev, rootCause: e.target.value }))}
              isDisabled={!canEdit}
            />

            <Text fontWeight="bold" mt={4} mb={2}>Mức độ nghiêm trọng</Text>
            <Select
              value={form.severity}
              onChange={(e) => setForm((prev) => ({ ...prev, severity: e.target.value }))}
              isDisabled={!canEdit}
            >
              <option value="LOW">LOW</option>
              <option value="MEDIUM">MEDIUM</option>
              <option value="HIGH">HIGH</option>
              <option value="CRITICAL">CRITICAL</option>
            </Select>
          </Box>

          <Box p={4} bg="white" borderRadius="md" border="1px solid #e9edf7">
            <HStack justify="space-between" mb={3}>
              <Text fontWeight="bold">Đề xuất sửa chữa / phụ tùng</Text>
              {canEdit ? (
                <Button size="sm" colorScheme="blue" onClick={addRecommendation}>Thêm dòng</Button>
              ) : null}
            </HStack>

            {recommendations.length === 0 ? (
              <Text color="#707eae">Chưa có đề xuất.</Text>
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
                    {(recommendationsByParent.childrenMap.get(Number(root.id)) || []).length > 0 ? (
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
                    ) : null}
                  </Box>
                ))}
              </VStack>
            )}
          </Box>

          {canEdit ? (
            <HStack justify="flex-end">
              <Button colorScheme="blue" onClick={saveCompletedDiagnosis} isLoading={saving}>
                Lưu hoàn tất
              </Button>
            </HStack>
          ) : (
            <Text fontSize="sm" color="#707eae">
              Bạn chỉ có quyền xem. Chỉ kỹ thuật viên đã nhận yêu cầu mới được chỉnh sửa.
            </Text>
          )}
        </VStack>
      )}
    </div>
  );
}

function RecommendationRow({ item, canEdit, services, products, onChange, onRemove }) {
  const itemType = String(item.itemType || "LABOR").toUpperCase();

  return (
    <Stack spacing={2} p={3} border="1px solid #e9edf7" borderRadius="md" bg="white">
      <HStack justify="space-between">
        <Text fontWeight="bold" fontSize="sm">#{item.id}</Text>
        {canEdit ? (
          <Button size="xs" colorScheme="red" variant="outline" onClick={() => onRemove(item)}>
            Xóa
          </Button>
        ) : null}
      </HStack>

      <HStack spacing={3} flexWrap="wrap">
        <Box minW="180px">
          <Text fontSize="xs" mb={1}>Loại dòng</Text>
          <Select
            size="sm"
            value={itemType}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { itemType: e.target.value, standardServiceId: null, inventoryItemId: null })}
          >
            <option value="LABOR">LABOR</option>
            <option value="PART">PART</option>
            <option value="CONSUMABLE">CONSUMABLE</option>
          </Select>
        </Box>

        {itemType === "LABOR" ? (
          <Box minW="250px">
            <Text fontSize="xs" mb={1}>Dịch vụ</Text>
            <Select
              size="sm"
              value={item.standardServiceId || ""}
              isDisabled={!canEdit}
              onChange={(e) => onChange(item.id, { standardServiceId: e.target.value || null })}
            >
              <option value="">-- Chọn dịch vụ --</option>
              {services.map((s) => (
                <option key={s.id} value={s.id}>{s.name || s.Name}</option>
              ))}
            </Select>
          </Box>
        ) : (
          <Box minW="250px">
            <Text fontSize="xs" mb={1}>Phụ tùng / Vật tư</Text>
            <Select
              size="sm"
              value={item.inventoryItemId || ""}
              isDisabled={!canEdit}
              onChange={(e) => onChange(item.id, { inventoryItemId: e.target.value || null })}
            >
              <option value="">-- Chọn sản phẩm --</option>
              {products.map((p) => (
                <option key={p.id} value={p.id}>{p.name || p.Name}</option>
              ))}
            </Select>
          </Box>
        )}

        <Box minW="120px">
          <Text fontSize="xs" mb={1}>Số lượng</Text>
          <Input
            size="sm"
            type="number"
            value={item.quantity ?? 1}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { quantity: Number(e.target.value || 1) })}
          />
        </Box>

        <Box minW="180px">
          <Text fontSize="xs" mb={1}>Ưu tiên</Text>
          <Select
            size="sm"
            value={item.urgency || "RECOMMENDED"}
            isDisabled={!canEdit}
            onChange={(e) => onChange(item.id, { urgency: e.target.value })}
          >
            <option value="CRITICAL">CRITICAL</option>
            <option value="RECOMMENDED">RECOMMENDED</option>
            <option value="OPTIONAL">OPTIONAL</option>
          </Select>
        </Box>
      </HStack>

      <Box>
        <Text fontSize="xs" mb={1}>Media bằng chứng (JSON hoặc URL)</Text>
        <Input
          size="sm"
          value={item.evidenceMediaUrls || ""}
          isDisabled={!canEdit}
          onChange={(e) => onChange(item.id, { evidenceMediaUrls: e.target.value })}
        />
      </Box>

      <Box>
        <Text fontSize="xs" mb={1}>Ghi chú kỹ thuật viên</Text>
        <Textarea
          size="sm"
          value={item.technicianNotes || ""}
          isDisabled={!canEdit}
          onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
        />
      </Box>
    </Stack>
  );
}
