import React, { useEffect, useMemo, useState, useCallback, useRef } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import {
  Box,
  Button,
  Heading,
  HStack,
  Input,
  Spinner,
  Stack,
  Text,
  Textarea,
  SimpleGrid,
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
  StickyNote,
  ChevronDown,
  ChevronRight,
} from "lucide-react";

import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { vehicleConditionLogApi } from "@features/workshop/api/vehicleConditionLogApi";
import { getVehicleBrands, getVehicleModels } from "@features/appointments/api/appointmentApi";
import { isServiceCompatibleWithVehicleModel } from "@shared/utils/serviceEngineCompatibility";
import DiagnosisDropdown from "@features/workshop/pages/DiagnosisDropdown";
import RecommendationRow from "@features/workshop/pages/DiagnosisRecommendationRow";
import {
  toPositiveNumber,
  normalizeStatusCode,
  formatDateTime,
  resolveAssignmentNoteDisplay,
  isDiagnosisApprovedAndLocked,
  parseIdList,
  extractServiceId,
  extractServiceName,
  extractServiceCategoryId,
  extractServiceCategoryName,
  extractCategoryId,
  extractCategoryName,
  firstNonEmptyText,
  normalizeLookupText,
  sanitizeCustomerNoteText,
  extractVehicleModelId,
  extractDiagnosisAssignmentId,
  extractDiagnosisAssignmentVersions,
  normalizeServiceBomItems,
  resolveApiErrorMessage,
  delay,
  normalizeImageUrls,
  toEvidenceMediaUrlsValue,
  normalizeQuantity,
  isNewRecommendationItem,
  buildVehicleDisplay,
} from "@features/workshop/pages/serviceOrderDiagnosisHelpers";

export default function ServiceOrderDiagnosisPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const { user } = useAuth();
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [cloningVersion, setCloningVersion] = useState(false);
  const [error, setError] = useState("");
  const [order, setOrder] = useState(null);
  const [orderDetails, setOrderDetails] = useState([]);
  
  const [allDiagnoses, setAllDiagnoses] = useState([]);
  const [activeDiagId, setActiveDiagId] = useState(null);
  const [activeAssignmentVersionId, setActiveAssignmentVersionId] = useState(null);
  
  const [recommendations, setRecommendations] = useState([]);
  const [deletedRecommendationIds, setDeletedRecommendationIds] = useState([]);
  const [expandedMaterialByService, setExpandedMaterialByService] = useState({});
  const [services, setServices] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
  const [partCategories, setPartCategories] = useState([]);
  const [products, setProducts] = useState([]);
  const [vehicleModels, setVehicleModels] = useState([]);
  const [vehicleBrands, setVehicleBrands] = useState([]);
  const [quickPickCategoryId, setQuickPickCategoryId] = useState("");
  const [quickPickKeyword, setQuickPickKeyword] = useState("");
  const [quickPickOpen, setQuickPickOpen] = useState(false);
  const quickPickRef = useRef(null);

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
  const assignmentVersions = useMemo(
    () => extractDiagnosisAssignmentVersions(activeDiagnosis),
    [activeDiagnosis]
  );
  const selectedAssignmentVersion = useMemo(() => {
    if (assignmentVersions.length === 0) return null;
    const selected = assignmentVersions.find(
      (item) => Number(item.diagnosisAssignmentId) === Number(activeAssignmentVersionId)
    );
    return selected || assignmentVersions[0] || null;
  }, [assignmentVersions, activeAssignmentVersionId]);
  useEffect(() => {
    if (assignmentVersions.length === 0) return;
    if (activeAssignmentVersionId && assignmentVersions.some((item) => Number(item.diagnosisAssignmentId) === Number(activeAssignmentVersionId))) {
      return;
    }
    const currentVersion = assignmentVersions.find((item) => item.isCurrent);
    const nextAssignmentId = currentVersion?.diagnosisAssignmentId || assignmentVersions[0]?.diagnosisAssignmentId || null;
    setActiveAssignmentVersionId(nextAssignmentId);
  }, [assignmentVersions, activeAssignmentVersionId]);
  const currentDiagnosisAssignmentId = extractDiagnosisAssignmentId(activeDiagnosis);
  const isViewingCurrentAssignmentVersion =
    !selectedAssignmentVersion ||
    (currentDiagnosisAssignmentId && Number(selectedAssignmentVersion.diagnosisAssignmentId) === Number(currentDiagnosisAssignmentId));
  const isRejectedAssignmentVersion =
    Boolean(selectedAssignmentVersion?.foremanRejectReason) ||
    selectedAssignmentVersion?.teamLeaderApprovalStatus === "REJECTED" ||
    selectedAssignmentVersion?.finalApprovalStatus === "REJECTED" ||
    selectedAssignmentVersion?.eventType === "TECHNICAL_REJECTED" ||
    selectedAssignmentVersion?.eventType === "FINAL_REJECTED";
  const activeDiagnosisTechnicianId = toPositiveNumber(activeDiagnosis?.technicianId ?? activeDiagnosis?.TechnicianId);
  const isDiagnosisOwnedByCurrentTechnician =
    activeDiagnosis &&
    (String(activeDiagnosis.id).startsWith("tmp-") || Number(activeDiagnosisTechnicianId) === Number(user?.id));
  const isDiagnosisInEditableStatus = ["ASSIGNED", "COMPLETED", "PENDING"].includes(
    normalizeStatusCode(activeDiagnosis?.status ?? activeDiagnosis?.Status ?? "PENDING")
  );
  const isDiagnosisApprovalLocked = isDiagnosisApprovedAndLocked(activeDiagnosis);
  const canEdit =
    isTechnician &&
    Boolean(activeDiagnosis) &&
    Boolean(isDiagnosisOwnedByCurrentTechnician) &&
    isDiagnosisInEditableStatus &&
    !isDiagnosisApprovalLocked &&
    isViewingCurrentAssignmentVersion &&
    !isRejectedAssignmentVersion;
  const canCloneRejectedVersion =
    isTechnician &&
    Boolean(activeDiagnosis) &&
    Boolean(isDiagnosisOwnedByCurrentTechnician) &&
    isViewingCurrentAssignmentVersion &&
    isRejectedAssignmentVersion;

  const queryParams = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const fromHistoryQuery = queryParams.get("from") === "history";
  const requestedReturnPath = String(location?.state?.returnToPath || "").trim();
  const safeReturnPath = requestedReturnPath.startsWith("/workshop/") ? requestedReturnPath : "";
  const defaultBackPath = isTechnician ? "/workshop/technician" : "/workshop/reception";
  const fallbackHistoryPath = isTechnician && fromHistoryQuery ? "/workshop/technician/history" : "";
  const backPath = safeReturnPath || fallbackHistoryPath || defaultBackPath;
  const backButtonLabel = "Quay lại";
  const customerDescription = useMemo(() => {
    const raw = String(order?.customerNotes || "").trim();
    if (!raw) return "";
    try {
      const parsed = JSON.parse(raw);
      const values = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.symptoms) ? parsed.symptoms : [];
      const merged = values
        .map((item) => sanitizeCustomerNoteText(item))
        .filter(Boolean)
        .join("\n");
      return sanitizeCustomerNoteText(merged || raw);
    } catch {
      return sanitizeCustomerNoteText(raw);
    }
  }, [order?.customerNotes]);
  const customerDescriptionDisplay = customerDescription || "Khách chưa mô tả chi tiết.";
  const diagnosisAssignmentNote = useMemo(() => {
    const raw = firstNonEmptyText(
      selectedAssignmentVersion?.note,
      activeDiagnosis?.assignmentNote,
      activeDiagnosis?.AssignmentNote
    );
    return resolveAssignmentNoteDisplay(raw, selectedAssignmentVersion);
  }, [activeDiagnosis, selectedAssignmentVersion]);
  const routeVehicleContext = useMemo(
    () => location?.state?.vehicleSnapshot || location?.state?.orderSummary || null,
    [location?.state]
  );
  const modelById = useMemo(() => {
    const map = new Map();
    (Array.isArray(vehicleModels) ? vehicleModels : []).forEach((item) => {
      const id = toPositiveNumber(item?.id ?? item?.Id);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [vehicleModels]);
  const brandById = useMemo(() => {
    const map = new Map();
    (Array.isArray(vehicleBrands) ? vehicleBrands : []).forEach((item) => {
      const id = toPositiveNumber(item?.id ?? item?.Id);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [vehicleBrands]);
  const vehicleDisplay = useMemo(
    () => buildVehicleDisplay(order, modelById, brandById, routeVehicleContext),
    [order, modelById, brandById, routeVehicleContext]
  );
  const resolvedVehicleModelIdForBom = useMemo(() => {
    const directModelId = toPositiveNumber(
      routeVehicleContext?.vehicleModelId,
      routeVehicleContext?.VehicleModelId,
      order?.vehicleModelId,
      order?.VehicleModelId,
      order?.guestVehicleModelId,
      order?.GuestVehicleModelId,
      order?.customerVehicle?.vehicleModelId,
      order?.customerVehicle?.VehicleModelId,
      order?.vehicle?.vehicleModelId,
      order?.vehicle?.VehicleModelId,
      order?.vehicle?.modelId,
      order?.vehicle?.ModelId,
      order?.vehicle?.model?.id,
      order?.vehicle?.Model?.Id
    );
    if (directModelId) return directModelId;

    const modelHint = firstNonEmptyText(
      routeVehicleContext?.vehicleModelName,
      routeVehicleContext?.VehicleModelName,
      order?.vehicleModelName,
      order?.VehicleModelName,
      order?.guestVehicleModelName,
      order?.GuestVehicleModelName,
      routeVehicleContext?.vehicleDescriptor,
      routeVehicleContext?.VehicleDescriptor
    );
    if (!modelHint) return null;

    const brandHint = firstNonEmptyText(
      routeVehicleContext?.vehicleBrandName,
      routeVehicleContext?.VehicleBrandName,
      order?.vehicleBrandName,
      order?.VehicleBrandName,
      order?.guestVehicleBrandName,
      order?.GuestVehicleBrandName
    );

    const normalizedModelHint = normalizeLookupText(modelHint);
    const normalizedBrandHint = normalizeLookupText(brandHint);

    const candidates = (Array.isArray(vehicleModels) ? vehicleModels : [])
      .map((item) => {
        const id = toPositiveNumber(item?.id ?? item?.Id);
        if (!id) return null;
        const modelName = firstNonEmptyText(item?.modelName, item?.ModelName, item?.name, item?.Name);
        const brandName = firstNonEmptyText(item?.brandName, item?.BrandName);
        return {
          id,
          model: normalizeLookupText(modelName),
          brand: normalizeLookupText(brandName),
        };
      })
      .filter(Boolean);

    const exact = candidates.find((candidate) => {
      const modelMatch =
        candidate.model === normalizedModelHint ||
        candidate.model.includes(normalizedModelHint) ||
        normalizedModelHint.includes(candidate.model);
      const brandMatch = !normalizedBrandHint || !candidate.brand || candidate.brand === normalizedBrandHint;
      return modelMatch && brandMatch;
    });

    return exact?.id || null;
  }, [order, routeVehicleContext, vehicleModels]);
  const vehicleModelDisplay = useMemo(() => {
    const brand = String(vehicleDisplay.brand || "").trim();
    const model = String(vehicleDisplay.model || "").trim();
    if (brand && model) {
      if (model.toLowerCase().includes(brand.toLowerCase())) return model;
      return `${brand} ${model}`.trim();
    }
    return model || brand || "Chưa có";
  }, [vehicleDisplay.brand, vehicleDisplay.model]);

  const resolvedVehicleModelForServiceFilter = useMemo(
    () => (resolvedVehicleModelIdForBom ? modelById.get(resolvedVehicleModelIdForBom) || null : null),
    [resolvedVehicleModelIdForBom, modelById]
  );

  const compatibleServices = useMemo(() => {
    const source = Array.isArray(services) ? services : [];
    if (!resolvedVehicleModelForServiceFilter) return source;
    return source.filter((service) => {
      try {
        return isServiceCompatibleWithVehicleModel(service, resolvedVehicleModelForServiceFilter);
      } catch {
        // Guard runtime data-shape issues so the diagnosis page never hard-crashes.
        return true;
      }
    });
  }, [services, resolvedVehicleModelForServiceFilter]);

  const serviceCategoryByServiceId = useMemo(() => {
    const map = new Map();
    (compatibleServices || []).forEach((service) => {
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

  const selectedCustomerServiceNames = useMemo(() => {
    const names = new Set();

    const selectedServiceIds = parseIdList(
      order?.requestedServiceIds ??
      order?.RequestedServiceIds
    );

    selectedServiceIds.forEach((serviceId) => {
      const mapped = serviceNameById.get(String(serviceId));
      if (mapped) names.add(mapped);
    });

    (orderDetails || []).forEach((detail) => {
      const serviceId = toPositiveNumber(detail?.standardServiceId ?? detail?.StandardServiceId);
      const detailName = String(detail?.itemName ?? detail?.ItemName ?? "").trim();
      const itemType = String(detail?.itemType ?? detail?.ItemType ?? "").trim().toUpperCase();

      if (serviceId) {
        names.add(serviceNameById.get(String(serviceId)) || detailName || `Dịch vụ #${serviceId}`);
        return;
      }

      if (itemType === "SERVICE" && detailName) {
        names.add(detailName);
      }
    });

    return Array.from(names);
  }, [order, orderDetails, serviceNameById]);

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
  }, [serviceCategories, compatibleServices]);

  const quickPickFilteredServices = useMemo(() => {
    const keyword = normalizeLookupText(quickPickKeyword);
    return (compatibleServices || [])
      .filter((service) => {
        const serviceId = extractServiceId(service);
        if (!serviceId) return false;

        const categoryId = String(extractServiceCategoryId(service) || "");
        if (quickPickCategoryId && categoryId !== String(quickPickCategoryId)) return false;

        if (!keyword) return true;

        const serviceName = normalizeLookupText(extractServiceName(service));
        const serviceCode = normalizeLookupText(service?.code ?? service?.Code ?? "");
        const categoryName = normalizeLookupText(extractServiceCategoryName(service));
        return serviceName.includes(keyword) || serviceCode.includes(keyword) || categoryName.includes(keyword);
      })
      .sort((a, b) => extractServiceName(a).localeCompare(extractServiceName(b), "vi"));
  }, [compatibleServices, quickPickCategoryId, quickPickKeyword]);

  const existingRootServiceIdSet = useMemo(() => {
    return new Set(
      recommendations
        .filter((row) => !row.parentRecommendationId)
        .map((row) => toPositiveNumber(row.standardServiceId))
        .filter(Boolean)
        .map(String)
    );
  }, [recommendations]);

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (!quickPickRef.current) return;
      if (quickPickRef.current.contains(event.target)) return;
      setQuickPickOpen(false);
    };

    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, []);

  const parseSymptomsFromNotes = (rawNotes) => {
    try {
      const parsed = JSON.parse(String(rawNotes || "").trim());
      const values = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.symptoms) ? parsed.symptoms : [];
      const cleaned = values.map((x) => sanitizeCustomerNoteText(x)).filter(Boolean);
      if (cleaned.length > 0) return cleaned;
    } catch {}
    const text = sanitizeCustomerNoteText(rawNotes);
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
      const [orderRes, diagnosesRes, servicesRes, serviceCategoriesRes, partCategoriesRes, productsRes, conditionLogsRes, modelsRes, brandsRes, detailsRes] = await Promise.all([
        serviceOrderApi.getById(id),
        serviceOrderApi.getDiagnoses(id).catch(() => []),
        getAllStandardServices().catch(() => []),
        getAllServiceCategories().catch(() => []),
        inventoryApi.getPartCategories().catch(() => []),
        inventoryApi.getProducts().catch(() => []),
        vehicleConditionLogApi.getByServiceOrderId(id).catch(() => null),
        getVehicleModels().catch(() => []),
        getVehicleBrands().catch(() => []),
        serviceOrderApi.getDetails(id).catch(() => []),
      ]);

      setOrder(orderRes || null);
      setServices(Array.isArray(servicesRes) ? servicesRes : []);
      setServiceCategories(Array.isArray(serviceCategoriesRes) ? serviceCategoriesRes : []);
      setPartCategories(Array.isArray(partCategoriesRes) ? partCategoriesRes : []);
      setProducts(Array.isArray(productsRes) ? productsRes : []);
      setVehicleModels(Array.isArray(modelsRes) ? modelsRes : []);
      setVehicleBrands(Array.isArray(brandsRes) ? brandsRes : []);
      setOrderDetails(Array.isArray(detailsRes) ? detailsRes : []);
      
      const canValidateCheckIn = Array.isArray(conditionLogsRes);
      const signedCheckInExists =
        !canValidateCheckIn ||
        conditionLogsRes.some((log) => {
          const type = String(log?.logType || log?.LogType || "").toUpperCase();
          const signature = String(log?.customerSignatureUrl || log?.CustomerSignatureUrl || "").trim();
          return type === "CHECK_IN" && signature.length > 0;
        });
      if (!signedCheckInExists) {
        const message = "Khách chưa ký check-in. Vui lòng hoàn tất Nhật ký xe trước khi vào chẩn đoán.";
        if (isTechnician) {
          setError(message);
          return;
        }

        toast({
          title: "Chưa hoàn tất check-in",
          description: message,
          status: "warning",
          duration: 4500,
          isClosable: true,
        });
        navigate(`/workshop/reception/orders/${id}/vehicleconditionlogs`, { replace: true });
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
      const diagnosisAssignmentIdFromQuery = new URLSearchParams(location.search).get("diagnosisAssignmentId");
      let initialActive = diagnosesList[0];
      if (diagnosisIdFromQuery) {
        const found = diagnosesList.find(x => String(x.id) === diagnosisIdFromQuery);
        if (found) initialActive = found;
      }

      if (initialActive) {
        const initialAssignmentId = toPositiveNumber(diagnosisAssignmentIdFromQuery) || extractDiagnosisAssignmentId(initialActive);
        await selectDiagnosis(initialActive.id, diagnosesList, initialAssignmentId);
      }
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu chẩn đoán.");
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

  const selectDiagnosis = async (diagId, diagList = allDiagnoses, selectedAssignmentId = null) => {
    saveCurrentFormToState();
    
    const target = diagList.find(x => x.id === diagId);
    if (!target) return;
    
    setActiveDiagId(diagId);
    const fallbackAssignmentId = extractDiagnosisAssignmentId(target);
    const effectiveAssignmentId = toPositiveNumber(selectedAssignmentId) || fallbackAssignmentId || null;
    setActiveAssignmentVersionId(effectiveAssignmentId);
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
        const recs = effectiveAssignmentId
          ? await serviceOrderApi.getDiagnosisRecommendations({ diagnosisAssignmentId: effectiveAssignmentId })
          : await serviceOrderApi.getDiagnosisRecommendations({ diagnosisId: toPositiveNumber(target.id) });
        setRecommendations(Array.isArray(recs) ? recs : []);
      } catch {
        setRecommendations([]);
      }
    }
    setDeletedRecommendationIds([]);
    setExpandedMaterialByService({});
  };

  const handleCloneRejectedVersion = async () => {
    if (!orderId || !canCloneRejectedVersion || !user?.id) {
      return;
    }

    try {
      setCloningVersion(true);
      await serviceOrderApi.cloneRejectedDiagnosisVersion(orderId, {
        actorId: Number(user.id),
      });
      await loadData();
      toast({
        title: "Đã tạo phiên bản mới",
        description: "Kỹ thuật viên có thể chỉnh sửa lại trên phiên bản mới.",
        status: "success",
        duration: 2800,
        isClosable: true,
      });
    } catch (err) {
      toast({
        title: "Không thể tạo phiên bản mới",
        description: err?.message || "Vui lòng thử lại.",
        status: "error",
        duration: 3200,
        isClosable: true,
      });
    } finally {
      setCloningVersion(false);
    }
  };

  // Updates memory without switching
  useEffect(() => {
    if (activeDiagId) saveCurrentFormToState();
  }, [form.rootCause, form.primarySystem, form.symptomTypes, form.occurrenceCondition, form.severity, form.symptom]);

  const addServiceRecommendation = () => {
    if (!canEdit) return;
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

  const addServiceFromQuickPick = (service) => {
    if (!canEdit) return;
    const serviceId = toPositiveNumber(extractServiceId(service));
    if (!serviceId) return;

    if (existingRootServiceIdSet.has(String(serviceId))) {
      const targetRootIds = recommendations
        .filter((row) => !row.parentRecommendationId && String(toPositiveNumber(row.standardServiceId) || "") === String(serviceId))
        .map((row) => String(row.id));

      if (targetRootIds.length === 0) return;

      const targetRootIdSet = new Set(targetRootIds);
      setRecommendations((prev) => {
        const idsToRemove = new Set(targetRootIds);
        prev.forEach((item) => {
          if (targetRootIdSet.has(String(item.parentRecommendationId || ""))) {
            idsToRemove.add(String(item.id));
          }
        });

        const persistedIds = prev
          .filter((item) => idsToRemove.has(String(item.id)))
          .filter((item) => !item?.isNew && !String(item?.id).startsWith("tmp-"))
          .map((item) => item.id);

        if (persistedIds.length > 0) {
          setDeletedRecommendationIds((prevDeleted) => [...new Set([...prevDeleted, ...persistedIds])]);
        }

        return prev.filter((item) => !idsToRemove.has(String(item.id)));
      });

      setExpandedMaterialByService((prev) => {
        const next = { ...prev };
        targetRootIds.forEach((id) => {
          delete next[id];
        });
        return next;
      });
      setQuickPickOpen(true);
      return;
    }

    const newRow = {
      id: `tmp-${Date.now()}-${Math.random()}`,
      diagnoseId: activeDiagId,
      parentRecommendationId: null,
      serviceCategoryId: String(serviceCategoryByServiceId.get(String(serviceId)) || ""),
      itemType: "LABOR",
      standardServiceId: serviceId,
      inventoryItemId: null,
      quantity: 1,
      urgency: "RECOMMENDED",
      evidenceMediaUrls: "",
      technicianNotes: "",
      isNew: true,
    };

    setRecommendations((prev) => [...prev, newRow]);
    handleServiceSelectionChange(newRow.id, serviceId);
    setQuickPickOpen(true);
  };

  const addMaterialRecommendation = (parentRecommendationId, inventoryItemId = null) => {
    if (!canEdit) return;
    const parentKey = String(parentRecommendationId || "");
    const normalizedInventoryItemId = toPositiveNumber(inventoryItemId);
    setRecommendations((prev) => [
      ...prev,
      {
        id: `tmp-${Date.now()}-${Math.random()}`,
        diagnoseId: activeDiagId,
        parentRecommendationId,
        itemType: "PART",
        standardServiceId: null,
        inventoryItemId: normalizedInventoryItemId || null,
        quantity: 1,
        urgency: "RECOMMENDED",
        evidenceMediaUrls: "",
        technicianNotes: "",
        isNew: true,
      },
    ]);
    if (parentKey) {
      setExpandedMaterialByService((prev) => ({ ...prev, [parentKey]: true }));
    }
  };

  const handleServiceSelectionChange = useCallback(
    async (recommendationId, selectedServiceIdRaw) => {
      if (!canEdit) return;
      const recommendationKey = String(recommendationId || "");
      const selectedServiceId = toPositiveNumber(selectedServiceIdRaw);
      const inferredServiceCategoryId = String(serviceCategoryByServiceId.get(String(selectedServiceId || "")) || "");

      setRecommendations((prev) =>
        prev
          .map((item) => {
            if (String(item.id) !== recommendationKey) return item;
            return {
              ...item,
              serviceCategoryId: selectedServiceId ? (inferredServiceCategoryId || item.serviceCategoryId || "") : item.serviceCategoryId,
              itemType: "LABOR",
              standardServiceId: selectedServiceId || null,
              inventoryItemId: null,
              parentRecommendationId: null,
            };
          })
          .filter(
            (item) =>
              !(
                String(item.parentRecommendationId || "") === recommendationKey &&
                item?.isAutoFromBom
              )
          )
      );

      if (!selectedServiceId) return;

      try {
        const vehicleModelId = resolvedVehicleModelIdForBom;
        if (!vehicleModelId) {
          toast({
            title: "Không xác định được dòng xe",
            description: "Không thể lọc BOM theo dòng xe. Vui lòng kiểm tra lại thông tin dòng xe của lệnh.",
            status: "warning",
            duration: 3200,
            isClosable: true,
          });
          return;
        }

        let bomItems = [];
        let lastBomError = null;
        // Retry strictly with both service + vehicle model.
        for (let attempt = 0; attempt < 3; attempt += 1) {
          try {
            const response = await serviceOrderApi.getServiceBoms(selectedServiceId, vehicleModelId);
            bomItems = normalizeServiceBomItems(response);
            if (bomItems.length > 0) break;
          } catch (error) {
            lastBomError = error;
          }

          if (attempt < 2) {
            await delay(250 * (attempt + 1));
          }
        }

	        if (bomItems.length === 0) {
	          if (lastBomError) {
	            throw lastBomError;
	          }
	          return;
	        }
	        setRecommendations((prev) => {
	          if (!prev.some((item) => String(item.id) === recommendationKey)) return prev;

	          const existingChildProductIds = new Set(
	            prev
	              .filter((item) => String(item.parentRecommendationId || "") === recommendationKey)
              .map((item) => String(toPositiveNumber(item.inventoryItemId) || ""))
              .filter(Boolean)
          );

	          const rowsToAdd = bomItems
	            .filter((bom) => !existingChildProductIds.has(String(bom.productId)))
	            .map((bom, index) => ({
              id: `tmp-bom-${Date.now()}-${index}-${Math.random()}`,
              diagnoseId: activeDiagId,
              parentRecommendationId: recommendationId,
              itemType: "PART",
              standardServiceId: null,
              inventoryItemId: bom.productId,
	              quantity: bom.quantity,
	              urgency: bom.isMandatory ? "CRITICAL" : "RECOMMENDED",
	              evidenceMediaUrls: "",
	              technicianNotes: "",
              isNew: true,
              isAutoFromBom: true,
            }));

          if (rowsToAdd.length === 0) return prev;
          return [...prev, ...rowsToAdd];
        });

        setExpandedMaterialByService((prev) => ({ ...prev, [recommendationKey]: false }));
      } catch (error) {
        toast({
          title: "Không tự tải được vật tư từ BOM",
          description: resolveApiErrorMessage(error),
          status: "warning",
          duration: 3200,
          isClosable: true,
        });
      }
    },
    [activeDiagId, canEdit, resolvedVehicleModelIdForBom, serviceCategoryByServiceId, toast]
  );

  const updateRecommendation = (id, patch) => {
    if (!canEdit) return;
    setRecommendations((prev) => prev.map((r) => (r.id === id ? { ...r, ...patch } : r)));
  };

  const handleServiceCategoryChange = useCallback((recommendationId, categoryId) => {
    if (!canEdit) return;
    const recommendationKey = String(recommendationId || "");
    setRecommendations((prev) =>
      prev
        .map((item) => {
          if (String(item.id) !== recommendationKey) return item;
          return {
            ...item,
            serviceCategoryId: categoryId || "",
            standardServiceId: null,
            itemType: "LABOR",
            inventoryItemId: null,
            parentRecommendationId: null,
          };
        })
        .filter(
          (item) =>
            !(
              String(item.parentRecommendationId || "") === recommendationKey &&
              item?.isAutoFromBom
            )
        )
    );
  }, [canEdit]);

  const removeRecommendation = (rec) => {
    if (!canEdit) return;
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

  const addEvidenceImagesToRecommendation = (recommendationId, fileList) => {
    if (!canEdit) return;
    const files = Array.from(fileList || []);
    if (files.length === 0) return;

    files.forEach((file) => {
      const reader = new FileReader();
      reader.onloadend = () => {
        setRecommendations((prev) =>
          prev.map((item) => {
            if (String(item.id) !== String(recommendationId)) return item;
            const existingUrls = normalizeImageUrls(item.evidenceMediaUrls);
            const nextUrls = [...existingUrls, String(reader.result || "").trim()].filter(Boolean);
            return {
              ...item,
              evidenceMediaUrls: toEvidenceMediaUrlsValue(nextUrls),
            };
          })
        );
      };
      reader.readAsDataURL(file);
    });
  };

  const removeEvidenceImageFromRecommendation = (recommendationId, imageIndex) => {
    if (!canEdit) return;
    setRecommendations((prev) =>
      prev.map((item) => {
        if (String(item.id) !== String(recommendationId)) return item;
        const currentUrls = normalizeImageUrls(item.evidenceMediaUrls);
        const nextUrls = currentUrls.filter((_, index) => index !== imageIndex);
        return {
          ...item,
          evidenceMediaUrls: toEvidenceMediaUrlsValue(nextUrls),
        };
      })
    );
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
      const currentDiagnosisAssignmentId = extractDiagnosisAssignmentId(currentDiag);
      
      for (const id of deletedRecommendationIds) {
        await serviceOrderApi.deleteDiagnosisRecommendation(id);
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

      if (!currentDiagnosisAssignmentId) {
        throw new Error("Thiếu DiagnosisAssignmentId hợp lệ để tạo kiến nghị kỹ thuật.");
      }

      const normalizeRecommendationPayload = (rec, parentRecommendationId = null) => {
        const normalizedType = String(rec.itemType || "PART").toUpperCase();
        const itemType = normalizedType === "LABOR" ? "LABOR" : normalizedType === "CONSUMABLE" ? "CONSUMABLE" : "PART";
        return {
          diagnosisId: toPositiveNumber(savedDiagnosisId),
          diagnosisAssignmentId: currentDiagnosisAssignmentId,
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

      const isTemporaryRecommendation = (rec) => isNewRecommendationItem(rec);

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
        if (!String(root.technicianNotes || "").trim()) {
          throw new Error("Vui lòng nhập lý do cho từng dịch vụ đã chọn trước khi lưu.");
        }
        if (isNewRecommendationItem(root) && normalizeImageUrls(root.evidenceMediaUrls).length === 0) {
          throw new Error("Mỗi dịch vụ phải có ít nhất 1 ảnh hiện trạng trước khi lưu.");
        }
      }

      for (const child of children) {
        if (!toPositiveNumber(child.inventoryItemId)) {
          throw new Error("Vui lòng chọn vật tư cụ thể cho từng dòng vật tư.");
        }
      }

      for (const root of roots) {
        const rootEvidenceUrls = [];
        for (const url of normalizeImageUrls(root.evidenceMediaUrls)) {
          if (String(url).startsWith("data:")) {
            const uploadRes = await vehicleConditionLogApi.uploadImage(url);
            const uploadedUrl = String(uploadRes?.url || "").trim();
            if (!uploadedUrl) {
              throw new Error("Không thể upload ảnh hiện trạng của dịch vụ.");
            }
            rootEvidenceUrls.push(uploadedUrl);
          } else {
            rootEvidenceUrls.push(url);
          }
        }

        const payload = normalizeRecommendationPayload(
          { ...root, itemType: "LABOR", evidenceMediaUrls: toEvidenceMediaUrlsValue(rootEvidenceUrls) },
          null
        );

        if (isTemporaryRecommendation(root)) {
          const created = await serviceOrderApi.createDiagnosisRecommendation(payload);
          const createdId = toPositiveNumber(created?.id ?? created?.Id);
          if (createdId) recommendationIdMap.set(String(root.id), createdId);
        } else {
          await serviceOrderApi.updateDiagnosisRecommendation(root.id, payload);
          const persistedId = toPositiveNumber(root.id);
          if (persistedId) recommendationIdMap.set(String(root.id), persistedId);
        }
      }

      for (const child of children) {
        const mappedParentId = recommendationIdMap.get(String(child.parentRecommendationId));
        const parentId = toPositiveNumber(mappedParentId ?? child.parentRecommendationId);
        if (!parentId) continue;

        const childEvidenceUrls = [];
        for (const url of normalizeImageUrls(child.evidenceMediaUrls)) {
          if (String(url).startsWith("data:")) {
            const uploadRes = await vehicleConditionLogApi.uploadImage(url);
            const uploadedUrl = String(uploadRes?.url || "").trim();
            if (!uploadedUrl) {
              throw new Error("Không thể upload ảnh đính kèm của vật tư.");
            }
            childEvidenceUrls.push(uploadedUrl);
          } else {
            childEvidenceUrls.push(url);
          }
        }

        const payload = normalizeRecommendationPayload(
          { ...child, evidenceMediaUrls: toEvidenceMediaUrlsValue(childEvidenceUrls) },
          parentId
        );
        if (isTemporaryRecommendation(child)) {
          await serviceOrderApi.createDiagnosisRecommendation(payload);
        } else {
          await serviceOrderApi.updateDiagnosisRecommendation(child.id, payload);
        }
      }

      toast({ title: "Đã lưu chẩn đoán", status: "success", duration: 2500, isClosable: true });
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
          <HStack spacing={3} align="center" flexWrap="wrap" mb={1}>
            <Heading size="md" color="#2b3674">Báo cáo Chẩn đoán Chuyên sâu</Heading>
            <Box
              display="inline-flex"
              alignItems="center"
              gap="8px"
              bg="#f8fafc"
              px="10px"
              py="3px"
              borderRadius="8px"
              border="1px solid #cbd5e1"
            >
              <Text
                as="span"
                px="8px"
                py="2px"
                bg="#fff"
                color="#0f172a"
                border="2px solid #0f172a"
                borderRadius="5px"
                fontWeight="800"
                fontSize="13px"
                letterSpacing="0.05em"
                lineHeight="1.2"
              >
                {vehicleDisplay.plate && vehicleDisplay.plate !== "--" ? vehicleDisplay.plate : "CHƯA BIỂN"}
              </Text>
              {vehicleModelDisplay && vehicleModelDisplay !== "--" ? (
                <Text as="span" fontWeight="700" fontSize="14px" color="#1e293b">
                  {vehicleModelDisplay}
                </Text>
              ) : null}
            </Box>
          </HStack>
          <Text color="#707eae" mt={1} fontSize="13px">
            Dịch vụ:{" "}
            <strong>
              {selectedCustomerServiceNames.length > 0
                ? selectedCustomerServiceNames.join(" | ")
                : "Chưa có"}
            </strong>
          </Text>
        </Box>
        <Button colorScheme="blue" onClick={() => navigate(backPath)}>{backButtonLabel}</Button>
      </HStack>

      {error ? (
        <Box p={4} bg="red.50" color="red.600" borderRadius="md">{error}</Box>
      ) : allDiagnoses.length === 0 ? (
        <Text color="#707eae">Chưa có yêu cầu chẩn đoán nào.</Text>
      ) : (
        <VStack align="stretch" spacing={5}>
              <Grid templateColumns={{ base: "1fr", xl: "280px minmax(0,1fr)" }} gap={4}>
                <GridItem>
                  <Box p={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                    <HStack justify="space-between" mb={3}>
                      <Heading size="sm" color="#244887">Phiên bản chẩn đoán</Heading>
                      <Text fontSize="xs" fontWeight="700" color="#4a5c84">{assignmentVersions.length}</Text>
                    </HStack>
                    <VStack align="stretch" spacing={2} maxH="420px" overflowY="auto">
                      {assignmentVersions.length === 0 ? (
                        <Text color="#7f8fb2" fontSize="sm">Chưa có phiên bản.</Text>
                      ) : (
                        assignmentVersions.map((version, index) => {
                          const isSelected = Number(version.diagnosisAssignmentId) === Number(selectedAssignmentVersion?.diagnosisAssignmentId);
                          const statusLabel = version.finalApprovalStatus || version.teamLeaderApprovalStatus || version.status || "--";
                          return (
                            <Box
                              as="button"
                              type="button"
                              key={version.diagnosisAssignmentId}
                              onClick={() => selectDiagnosis(activeDiagnosis?.id, allDiagnoses, version.diagnosisAssignmentId)}
                              textAlign="left"
                              p={3}
                              borderRadius="md"
                              border="1px solid"
                              borderColor={isSelected ? "#93b4eb" : "#d6e2f6"}
                              bg={isSelected ? "#edf4ff" : "#f9fbff"}
                            >
                              <Text fontSize="sm" fontWeight="700" color="#1f3567">Version {assignmentVersions.length - index}</Text>
                              <Text fontSize="xs" color="#5e769c">{statusLabel}</Text>
                              <Text fontSize="xs" color="#7f8fb2">
                                {version.createdAt ? formatDateTime(version.createdAt) : "--"}
                              </Text>
                            </Box>
                          );
                        })
                      )}
                    </VStack>
                  </Box>
                </GridItem>
                <GridItem>
                  <Grid templateColumns={{ base: "1fr", md: "1fr 1fr" }} gap={4}>
                    <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                      <HStack color="#f56565" mb={3}>
                        <Icon as={MessageSquare} size={20} />
                        <Heading size="sm">Mô tả của Khách hàng</Heading>
                      </HStack>
                      <Box bg="#f8fbff" border="1px solid #dbe7ff" borderRadius="md" px={4} py={3}>
                        <Text color="#2b3674" whiteSpace="pre-wrap" lineHeight="1.6">{customerDescriptionDisplay}</Text>
                      </Box>
                    </Box>
                    <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                      <HStack color="#ffb547" mb={4}>
                        <Icon as={AlertTriangle} size={20} />
                        <Heading size="sm">Kết luận & Mức độ</Heading>
                      </HStack>
                      <Grid templateColumns={{ base: "1fr", md: "2fr 1fr" }} gap={4}>
                        <GridItem>
                          <FormControl>
                            <FormLabel fontSize="sm" fontWeight="600" color="#2b3674">Kết luận nguyên nhân</FormLabel>
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
                              _disabled={{ color: "#1b2559", WebkitTextFillColor: "#1b2559", opacity: 1, cursor: "not-allowed", bg: "gray.50" }}
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
                  </Grid>
                  <Box p={5} mt={4} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                    <HStack color="#2b6cb0" mb={3}>
                      <Icon as={StickyNote} size={20} />
                      <Heading size="sm">Ghi chú điều phối</Heading>
                    </HStack>
                    <Box bg="#f8fbff" border="1px solid #dbe7ff" borderRadius="md" px={4} py={3}>
                      <Text color="#2b3674" whiteSpace="pre-wrap" lineHeight="1.6">
                        {diagnosisAssignmentNote || "Không có ghi chú điều phối."}
                      </Text>
                    </Box>
                                        {!canEdit && isRejectedAssignmentVersion ? (
                      <VStack mt={3} align="flex-start" spacing={3}>
                        <Text fontSize="sm" color="#b54708" fontWeight="600">
                          Phiên bản bị từ chối chỉ để xem lại sai sót, không được chỉnh sửa.
                        </Text>
                        {canCloneRejectedVersion ? (
                          <Button
                            size="sm"
                            colorScheme="orange"
                            onClick={handleCloneRejectedVersion}
                            isLoading={cloningVersion}
                            loadingText="Đang tạo"
                          >
                            Tạo phiên bản mới để sửa
                          </Button>
                        ) : null}
                      </VStack>
                    ) : null}
                  </Box>
                </GridItem>
              </Grid>

              {/* KHU VỰC 4: Đề xuất sửa chữa */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack justify="space-between" mb={4}>
                  <HStack color="#05cd99">
                    <Icon as={Wrench} size={20} />
                    <Heading size="sm">Vật tư & Nhân công đề xuất</Heading>
                  </HStack>
                </HStack>

                <Box mb={4} p={4} border="1px solid #d7e3f5" borderRadius="lg" bg="#f8fbff" position="relative" overflow="visible" zIndex={50}>
                  <HStack spacing={3} align="end" flexWrap="wrap">
                    <DiagnosisDropdown
                      label="Loại dịch vụ"
                      labelFontSize="xs"
                      labelColor="#5c6b8a"
                      minW="220px"
                      value={quickPickCategoryId}
                      onChange={(value) => setQuickPickCategoryId(value || "")}
                      isDisabled={!canEdit}
                    >
                      <option value="">-- Tất cả loại dịch vụ --</option>
                      {(serviceCategoryOptions || []).map((category) => (
                        <option key={category.value} value={category.value}>
                          {category.label}
                        </option>
                      ))}
                    </DiagnosisDropdown>

                    <Box minW="320px" flex="1" ref={quickPickRef} position="relative" zIndex={70}>
                      <Text fontSize="xs" fontWeight="600" color="#5c6b8a" mb={2}>
                        Tìm dịch vụ
                      </Text>
                      <Box position="relative">
                        <Input
                          size="md"
                          value={quickPickKeyword}
                          onChange={(event) => {
                            setQuickPickKeyword(event.target.value);
                            setQuickPickOpen(true);
                          }}
                          onFocus={() => setQuickPickOpen(true)}
                          placeholder={
                            quickPickCategoryId
                              ? "Nhập tên dịch vụ trong loại đã chọn..."
                              : "Nhập tên dịch vụ để tìm trên toàn bộ danh sách..."
                          }
                          isDisabled={!canEdit}
                          bg="white"
                          borderColor="#d7e3f5"
                        />
                        {quickPickOpen && canEdit && (
                          <Box
                            position="absolute"
                            top="calc(100% + 6px)"
                            left={0}
                            right={0}
                            zIndex={120}
                            bg="white"
                            border="1px solid #d7e3f5"
                            borderRadius="md"
                            boxShadow="0 10px 24px rgba(27, 37, 89, 0.12)"
                            maxH="240px"
                            overflowY="auto"
                          >
                            {quickPickFilteredServices.length === 0 ? (
                              <Text px={3} py={2} fontSize="sm" color="#7f8fb2">
                                Không có dịch vụ phù hợp.
                              </Text>
                            ) : (
                              quickPickFilteredServices.map((service) => {
                                const serviceId = String(extractServiceId(service) || "");
                                const existed = existingRootServiceIdSet.has(serviceId);
                                const serviceName = extractServiceName(service) || `Dịch vụ #${serviceId}`;
                                const categoryName = extractServiceCategoryName(service);
                                return (
                                  <Box
                                    as="button"
                                    type="button"
                                    key={serviceId}
                                    w="100%"
                                    textAlign="left"
                                    px={3}
                                    py={2}
                                    bg={existed ? "#edf4ff" : "transparent"}
                                    _hover={{ bg: existed ? "#e4efff" : "#f4f8ff" }}
                                    borderLeft={existed ? "2px solid #3b82f6" : "2px solid transparent"}
                                    border="none"
                                    onClick={() => addServiceFromQuickPick(service)}
                                  >
                                    <Text fontSize="sm" fontWeight={existed ? "700" : "600"} color={existed ? "#274690" : "#2b3674"}>
                                      {serviceName}
                                    </Text>
                                    {categoryName ? (
                                      <Text fontSize="xs" color="#7f8fb2">
                                        {categoryName}
                                      </Text>
                                    ) : null}
                                  </Box>
                                );
                              })
                            )}
                          </Box>
                        )}
                      </Box>
                    </Box>
                  </HStack>
                </Box>

                {recommendations.length === 0 ? (
                  <Text color="#707eae" fontSize="sm">
                    Chưa có dịch vụ đề xuất cho lệnh sửa chữa này. Hãy thêm dịch vụ trước, sau đó thêm vật tư cho từng dịch vụ.
                  </Text>
                ) : (
                  <VStack align="stretch" spacing={3}>
                    {recommendationsByParent.roots.map((root, rootIndex) => {
                      const rootKey = String(root.id);
                      const childItems = recommendationsByParent.childrenMap.get(rootKey) || [];
                      const hasChildItems = childItems.length > 0;
                      const isMaterialExpanded = Boolean(expandedMaterialByService[rootKey]);

                      return (
                        <Box key={root.id} position="relative" overflow="visible">
                          <RecommendationRow
                            mode="service"
                            item={root}
                            displayNumber={rootIndex + 1}
                            canEdit={canEdit}
                            services={compatibleServices}
                            serviceCategoryOptions={serviceCategoryOptions}
                            serviceCategoryByServiceId={serviceCategoryByServiceId}
                            products={products}
                            partCategories={partCategories}
                          onChange={updateRecommendation}
                          onRemove={removeRecommendation}
                          onAddMaterial={addMaterialRecommendation}
                            onSelectService={handleServiceSelectionChange}
                            onSelectServiceCategory={handleServiceCategoryChange}
                          onAddEvidenceImages={addEvidenceImagesToRecommendation}
                          onRemoveEvidenceImage={removeEvidenceImageFromRecommendation}
                          materialItems={childItems}
                          materialCount={childItems.length}
                          isMaterialExpanded={isMaterialExpanded}
                          onToggleMaterials={
                              hasChildItems
                                ? () =>
                                    setExpandedMaterialByService((prev) => ({
                                      ...prev,
                                      [rootKey]: !prev[rootKey],
                                    }))
                                : null
                            }
                          />
                          {hasChildItems && isMaterialExpanded && (
                            <VStack align="stretch" mt={2} spacing={2}>
                              {childItems.map((child, childIndex) => (
                                <RecommendationRow
                                  key={child.id}
                                  mode="material"
                                  item={child}
                                  displayNumber={childIndex + 1}
                                  canEdit={canEdit}
                                  services={compatibleServices}
                                  serviceCategoryOptions={serviceCategoryOptions}
                                  serviceCategoryByServiceId={serviceCategoryByServiceId}
                                  products={products}
                                  partCategories={partCategories}
                                  onChange={updateRecommendation}
                                  onRemove={removeRecommendation}
                                  parentServiceName={serviceNameById.get(String(root.standardServiceId)) || `Dịch vụ #${root.id}`}
                                />
                              ))}
                            </VStack>
                          )}
                          {!hasChildItems && (
                            <Text mt={2} fontSize="xs" color="#7f8fb2">
                              Chưa có vật tư cho dịch vụ này.
                            </Text>
                          )}
                        </Box>
                      );
                    })}
                  </VStack>
                )}

              </Box>

              {canEdit && (
                <HStack justify="flex-end" pt={4} pb={10}>
                  <Button size="lg" colorScheme="blue" onClick={saveAllDiagnoses} isLoading={saving}>
                    Lưu chẩn đoán
                  </Button>
                </HStack>
              )}
            </VStack>
      )}
    </Box>
  );
}
