import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Minus, Plus, Settings, Trash2 } from "lucide-react";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllStandardServices } from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { getStatusLabelVi } from "@shared/core/utils/statusLabelUtils";
import {
  extractLatestDiagnosisId,
  firstNonEmptyText,
  formatDateTime,
  normalizeRecommendationItemType,
  normalizeStatus,
  readField,
  resolveCurrentStaffId,
  resolveRecommendationStatus,
  resolveSlaSeverity,
  toPositiveNumber,
} from "./quotationFlowUtils";
import "./ForemanApproval.css";

function extractServiceName(service) {
  return String(readField(service, "name", "Name", "serviceName", "ServiceName") || "").trim();
}

function extractProductName(product) {
  return String(readField(product, "name", "Name", "productName", "ProductName") || "").trim();
}

function toEditableQuantity(value) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return "1";
  return String(parsed);
}

function getQuantityWarningMessage(itemTypeToken, quantityValue) {
  const quantity = Number(quantityValue);
  if (!Number.isFinite(quantity) || quantity <= 0) return "";
  if (itemTypeToken === "SERVICE" && quantity > 5) return "Số lượng dịch vụ vượt 5. Vui lòng kiểm tra lại.";
  if (itemTypeToken !== "SERVICE" && quantity > 20) return "Số lượng phụ tùng vượt 20. Vui lòng kiểm tra lại.";
  return "";
}

function detectAssignmentNoteFromSystem(noteText, noteSource) {
  const sourceToken = normalizeStatus(noteSource);
  if (sourceToken.includes("SYSTEM") || sourceToken.includes("AUTO")) return true;

  const text = String(noteText || "").trim().toLowerCase();
  if (!text) return false;
  return text.includes("h? thợng") || text.includes("t? đếng") || text.startsWith("??");
}

function resolveCustomerDisplay(order, customerById) {
  const directText = firstNonEmptyText(
    readField(order, "customerName", "CustomerName", "customerFullName", "CustomerFullName", "guestName", "GuestName"),
    order?.customer?.fullName,
    order?.customer?.FullName,
    order?.customer?.name,
    order?.customer?.Name
  );
  if (directText) return directText;

  const customerId = toPositiveNumber(readField(order, "customerId", "CustomerId"));
  const customer = customerId ? customerById.get(customerId) : null;
  return firstNonEmptyText(customer?.fullName, customer?.FullName, customer?.name, customer?.Name, "-");
}

function resolveVehiclePlateDisplay(order, vehicleById) {
  const directText = firstNonEmptyText(
    readField(
      order,
      "vehicleLicensePlate",
      "VehicleLicensePlate",
      "vehiclePlate",
      "VehiclePlate",
      "licensePlate",
      "LicensePlate",
      "guestVehicleLicensePlate",
      "GuestVehicleLicensePlate"
    ),
    order?.vehicle?.licensePlate,
    order?.vehicle?.LicensePlate,
    order?.customerVehicle?.licensePlate,
    order?.customerVehicle?.LicensePlate
  );
  if (directText) return directText;

  const vehicleId = toPositiveNumber(readField(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? vehicleById.get(vehicleId) : null;
  return firstNonEmptyText(vehicle?.licensePlate, vehicle?.LicensePlate, vehicle?.plateNumber, vehicle?.PlateNumber, "-");
}

function recommendationDisplayName(row, serviceLookup, productLookup) {
  const type = normalizeRecommendationItemType(readField(row, "itemType", "ItemType"));
  if (type === "SERVICE") {
    const serviceId = toPositiveNumber(readField(row, "standardServiceId", "StandardServiceId"));
    const service = serviceId ? serviceLookup.get(serviceId) : null;
    return extractServiceName(service) || readField(row, "itemName", "ItemName") || (serviceId ? `Dịch vụ #${serviceId}` : "Dịch vụ");
  }

  const productId = toPositiveNumber(readField(row, "inventoryItemId", "InventoryItemId"));
  const product = productId ? productLookup.get(productId) : null;
  return extractProductName(product) || readField(row, "itemName", "ItemName") || (productId ? `Vật tư #${productId}` : "Vật tư");
}

function resolveRootCauseText(raw) {
  const text = String(raw ?? "").trim();
  if (!text) return "";
  if (!text.startsWith("{")) return text;

  try {
    const parsed = JSON.parse(text);
    const actualRootCause = String(parsed?.actualRootCause || "").trim();
    const primarySystem = String(parsed?.primarySystem || "").trim();
    const occurrenceCondition = String(parsed?.occurrenceCondition || "").trim();
    const symptomTypes = Array.isArray(parsed?.symptomTypes) ? parsed.symptomTypes.filter(Boolean).join(", ") : "";
    const merged = [actualRootCause, primarySystem, symptomTypes, occurrenceCondition].filter(Boolean).join(" | ");
    return merged || "";
  } catch {
    return "";
  }
}

function getUrgencyLabelVi(rawUrgency) {
  const token = String(rawUrgency || "").trim().toUpperCase();
  if (!token) return "-";

  if (token === "CRITICAL" || token === "SAFETY" || token === "SAFETY_CRITICAL") return "Kh?n cấp";
  if (token === "RECOMMENDED" || token === "NORMAL") return "Khuy?n ngh?";
  if (token === "LOW") return "Th?p";

  return token;
}

function normalizeUrgencyToken(rawUrgency) {
  const token = String(rawUrgency || "").trim().toUpperCase();
  if (!token) return "RECOMMENDED";
  if (token === "SAFETY" || token === "SAFETY_CRITICAL" || token === "CRITICAL") return "CRITICAL";
  if (token === "NORMAL" || token === "RECOMMENDED") return "RECOMMENDED";
  if (token === "LOW") return "LOW";
  return token;
}

function getUrgencyTone(rawUrgency) {
  const token = String(rawUrgency || "").trim().toUpperCase();
  if (token === "CRITICAL" || token === "SAFETY" || token === "SAFETY_CRITICAL") return "critical";
  if (token === "RECOMMENDED" || token === "NORMAL") return "recommended";
  if (token === "LOW") return "low";
  return "default";
}

function parseEvidenceMediaUrls(rawValue) {
  if (!rawValue) return [];
  if (Array.isArray(rawValue)) return rawValue.map((x) => String(x || "").trim()).filter(Boolean);

  const text = String(rawValue).trim();
  if (!text) return [];

  try {
    const parsed = JSON.parse(text);
    if (Array.isArray(parsed)) return parsed.map((x) => String(x || "").trim()).filter(Boolean);

    if (parsed && typeof parsed === "object") {
      const candidate = parsed.urls || parsed.mediaUrls || parsed.images || parsed.items;
      if (Array.isArray(candidate)) return candidate.map((x) => String(x || "").trim()).filter(Boolean);
    }
  } catch {
    // Fall back to split by separators.
  }

  return text
    .split(/[\n,;]+/)
    .map((x) => x.trim())
    .filter(Boolean);
}

export default function ForemanDiagnosisApprovalPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const { user } = useAuth();
  const searchParams = useMemo(() => new URLSearchParams(location.search || ""), [location.search]);
  const fromHistoryContext = searchParams.get("from") === "history" || searchParams.has("from-history");
  const returnToPath = String(location.state?.returnToPath || "").trim();
  const backPath = returnToPath || (fromHistoryContext ? "/workshop/foreman/history" : "/workshop/foreman");

  const numericOrderId = Number(orderId);
  const staffId = useMemo(() => resolveCurrentStaffId(user), [user]);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [actionMessage, setActionMessage] = useState("");

  const [order, setOrder] = useState(null);
  const [diagnoses, setDiagnoses] = useState([]);
  const [recommendations, setRecommendations] = useState([]);
  const [quotations, setQuotations] = useState([]);

  const [services, setServices] = useState([]);
  const [products, setProducts] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);

  const [quantityById, setQuantityById] = useState({});
  const [urgencyById, setUrgencyById] = useState({});
  const [removedIds, setRemovedIds] = useState([]);
  const [approveNote, setApproveNote] = useState("");
  const [rejectNote, setRejectNote] = useState("");
  const [showRejectBox, setShowRejectBox] = useState(false);

  const customerById = useMemo(() => {
    const map = new Map();
    (customers || []).forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id", "customerId", "CustomerId"));
      if (id) map.set(id, item);
    });
    return map;
  }, [customers]);

  const vehicleById = useMemo(() => {
    const map = new Map();
    (vehicles || []).forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id", "vehicleId", "VehicleId"));
      if (id) map.set(id, item);
    });
    return map;
  }, [vehicles]);

  const serviceLookup = useMemo(() => {
    const map = new Map();
    (services || []).forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id"));
      if (id) map.set(id, item);
    });
    return map;
  }, [services]);

  const productLookup = useMemo(() => {
    const map = new Map();
    (products || []).forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id", "productId", "ProductId"));
      if (id) map.set(id, item);
    });
    return map;
  }, [products]);

  const loadData = useCallback(async () => {
    if (!Number.isFinite(numericOrderId) || numericOrderId <= 0) {
      setError("Mã lệnh sửa chờa không hợp lệ.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    setActionMessage("");

    try {
      const [
        orderRes,
        diagnosesRes,
        quotationsRes,
        serviceRes,
        productRes,
        customerRes,
        vehicleRes,
      ] = await Promise.all([
        serviceOrderApi.getById(numericOrderId),
        serviceOrderApi.getDiagnoses(numericOrderId),
        serviceOrderApi.getQuotationsByServiceOrder(numericOrderId).catch(() => []),
        getAllStandardServices().catch(() => []),
        inventoryApi.getProducts().catch(() => []),
        getAllCustomers().catch(() => []),
        getAllVehicles().catch(() => []),
      ]);

      const latestDiagnosisId = extractLatestDiagnosisId(diagnosesRes);
      const recommendationsRes = latestDiagnosisId
        ? await serviceOrderApi.getDiagnosisRecommendations(latestDiagnosisId).catch(() => [])
        : [];

      const safeRecommendations = Array.isArray(recommendationsRes) ? recommendationsRes : [];
      const nextQty = {};
      const nextUrgency = {};
      safeRecommendations.forEach((row) => {
        const id = toPositiveNumber(readField(row, "id", "Id"));
        if (!id) return;
        nextQty[id] = toEditableQuantity(readField(row, "quantity", "Quantity"));
        nextUrgency[id] = normalizeUrgencyToken(readField(row, "urgency", "Urgency"));
      });

      setOrder(orderRes || null);
      setDiagnoses(Array.isArray(diagnosesRes) ? diagnosesRes : []);
      setRecommendations(safeRecommendations);
      setQuotations(Array.isArray(quotationsRes) ? quotationsRes : []);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setProducts(Array.isArray(productRes) ? productRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setQuantityById(nextQty);
      setUrgencyById(nextUrgency);
      setRemovedIds([]);
      setShowRejectBox(false);
      setRejectNote("");
    } catch (err) {
      setError(err?.message || "Kh?ng thể t?i dữ liệu duyệt chuẩn do?n.");
    } finally {
      setLoading(false);
    }
  }, [numericOrderId]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const latestDiagnosis = useMemo(() => {
    const list = Array.isArray(diagnoses) ? diagnoses : [];
    if (list.length === 0) return null;

    const sorted = [...list].sort((a, b) => {
      const aTime = new Date(readField(a, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
      const bTime = new Date(readField(b, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
      return bTime - aTime;
    });

    return sorted[0] || null;
  }, [diagnoses]);

  const latestQuotation = useMemo(() => {
    const list = Array.isArray(quotations) ? quotations : [];
    if (list.length === 0) return null;

    const sorted = [...list].sort((a, b) => {
      const versionA = Number(readField(a, "versionNo", "VersionNo") || 0);
      const versionB = Number(readField(b, "versionNo", "VersionNo") || 0);
      if (versionB !== versionA) return versionB - versionA;

      const timeA = new Date(readField(a, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
      const timeB = new Date(readField(b, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
      return timeB - timeA;
    });

    return sorted[0] || null;
  }, [quotations]);

  const activeRecommendationList = useMemo(
    () =>
      (Array.isArray(recommendations) ? recommendations : []).filter(
        (item) => !resolveRecommendationStatus(item).startsWith("SOFT_DELETED")
      ),
    [recommendations]
  );

  const removedIdSet = useMemo(() => new Set(removedIds), [removedIds]);

  const treeRows = useMemo(() => {
    const visible = activeRecommendationList.filter((row) => {
      const id = toPositiveNumber(readField(row, "id", "Id"));
      return id && !removedIdSet.has(id);
    });

    const byId = new Map();
    const childrenByParent = new Map();

    visible.forEach((row) => {
      const id = toPositiveNumber(readField(row, "id", "Id"));
      if (!id) return;
      byId.set(id, row);
    });

    visible.forEach((row) => {
      const id = toPositiveNumber(readField(row, "id", "Id"));
      const parentId = toPositiveNumber(readField(row, "parentRecommendationId", "ParentRecommendationId"));
      if (!id || !parentId || !byId.has(parentId)) return;
      if (!childrenByParent.has(parentId)) childrenByParent.set(parentId, []);
      childrenByParent.get(parentId).push({ source: row, rowId: id, isSynthetic: false });
    });

    const parents = visible.filter((row) => !toPositiveNumber(readField(row, "parentRecommendationId", "ParentRecommendationId")));

    return parents.map((parent) => {
      const parentId = toPositiveNumber(readField(parent, "id", "Id"));
      const children = parentId ? childrenByParent.get(parentId) || [] : [];
      return {
        parentId,
        parent,
        children: children.length > 0 ? children : [{ source: parent, rowId: parentId, isSynthetic: true }],
      };
    });
  }, [activeRecommendationList, removedIdSet]);

  const technicalStatus =
    normalizeStatus(readField(latestDiagnosis, "teamLeaderApprovalStatus", "TeamLeaderApprovalStatus")) || "PENDING";
  const finalStatus =
    normalizeStatus(readField(latestDiagnosis, "finalApprovalStatus", "FinalApprovalStatus")) || "PENDING";
  const quotationStatus =
    normalizeStatus(readField(latestQuotation, "status", "Status")) || "DRAFT";
  const quotationTechnicalStatus =
    normalizeStatus(readField(latestQuotation, "technicalApprovalStatus", "TechnicalApprovalStatus")) || "PENDING";
  const canApproveQuotationTechnical =
    Boolean(latestQuotation) &&
    quotationStatus === "WAITING_APPROVAL" &&
    quotationTechnicalStatus === "PENDING";
  const teamLeaderPendingMinutes = Number(
    readField(latestDiagnosis, "teamLeaderApprovalPendingMinutes", "TeamLeaderApprovalPendingMinutes") || 0
  );
  const isTeamLeaderEscalated =
    technicalStatus === "ESCALATED" ||
    Boolean(readField(latestDiagnosis, "isTeamLeaderApprovalEscalated", "IsTeamLeaderApprovalEscalated"));
  const isTeamLeaderWarning =
    Boolean(readField(latestDiagnosis, "isTeamLeaderApprovalWarning", "IsTeamLeaderApprovalWarning")) && !isTeamLeaderEscalated;
  const isForemanDecisionLocked = finalStatus === "APPROVED" || finalStatus === "REJECTED";
  const isForemanBypassMode = technicalStatus !== "APPROVED" && !isTeamLeaderEscalated;
  const isRecommendationEditingLocked = saving || isForemanDecisionLocked;

  const assignmentNote =
    readField(latestDiagnosis, "assignmentNote", "AssignmentNote") ||
    readField(order, "assignmentNote", "AssignmentNote") ||
    "";
  const assignmentSource =
    readField(latestDiagnosis, "assignmentNoteSource", "AssignmentNoteSource") ||
    readField(order, "assignmentNoteSource", "AssignmentNoteSource") ||
    "";
  const isAssignmentAuto = detectAssignmentNoteFromSystem(assignmentNote, assignmentSource);

  const symptom = readField(latestDiagnosis, "symptom", "Symptom") || readField(order, "symptom", "Symptom") || "-";
  const rootCauseRaw = readField(latestDiagnosis, "rootCause", "RootCause");
  const rootCauseText = resolveRootCauseText(rootCauseRaw);
  const diagnosisNoteText = assignmentNote || "Kh?ng c? ghi chờ t? l? t?n.";

  const customerDisplay = resolveCustomerDisplay(order, customerById);
  const plateDisplay = resolveVehiclePlateDisplay(order, vehicleById);
  const totalSuggestions = useMemo(
    () =>
      activeRecommendationList.filter((item) => {
        const recommendationId = toPositiveNumber(readField(item, "id", "Id"));
        return recommendationId && !removedIdSet.has(recommendationId);
      }).length,
    [activeRecommendationList, removedIdSet],
  );

  const slaSeverity = resolveSlaSeverity(readField(latestDiagnosis, "createdAt", "CreatedAt"));
  const slaLabel =
    slaSeverity === "RED" ? "Qu? h?n > 30 ph?t" : slaSeverity === "YELLOW" ? "Cầnh b?o > 15 ph?t" : "Trong SLA";

  const changeQuantity = useCallback((rowId, updater) => {
    if (!rowId) return;
    setQuantityById((prev) => {
      const current = Number(prev[rowId] || 1);
      const nextRaw = typeof updater === "function" ? updater(current) : updater;
      const next = Math.max(1, Number(nextRaw || 1));
      return { ...prev, [rowId]: String(next) };
    });
  }, []);

  const editQuantityInput = useCallback((rowId, rawValue) => {
    if (!rowId) return;
    const normalized = String(rawValue ?? "").replace(/[^\d]/g, "");
    setQuantityById((prev) => ({ ...prev, [rowId]: normalized }));
  }, []);

  const commitQuantityInput = useCallback((rowId) => {
    if (!rowId) return;
    setQuantityById((prev) => {
      const raw = String(prev[rowId] ?? "").trim();
      const parsed = Number(raw);
      const safeQty = Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : 1;
      return { ...prev, [rowId]: String(safeQty) };
    });
  }, []);

  const changeUrgency = useCallback((rowId, nextUrgency) => {
    if (!rowId) return;
    setUrgencyById((prev) => ({ ...prev, [rowId]: normalizeUrgencyToken(nextUrgency) }));
  }, []);

  const toggleRemoveRow = useCallback((rowId) => {
    if (!rowId) return;
    setRemovedIds((prev) => (prev.includes(rowId) ? prev.filter((id) => id !== rowId) : [...prev, rowId]));
  }, []);

  const toggleRemoveGroup = useCallback((group) => {
    if (!group) return;
    const ids = new Set();
    const parentId = toPositiveNumber(group.parentId);
    if (parentId) ids.add(parentId);
    (group.children || []).forEach((child) => {
      const rowId = toPositiveNumber(child?.rowId);
      if (rowId) ids.add(rowId);
    });

    const targets = Array.from(ids);
    if (targets.length === 0) return;

    setRemovedIds((prev) => {
      const allSelected = targets.every((id) => prev.includes(id));
      if (allSelected) return prev.filter((id) => !ids.has(id));
      return [...new Set([...prev, ...targets])];
    });
  }, []);

  const decreaseQuantity = useCallback((rowId) => {
    if (!rowId) return;
    changeQuantity(rowId, (current) => Math.max(1, current - 1));
  }, [changeQuantity]);

  const persistDraftChanges = useCallback(async () => {
    if (!staffId) return;

    const updatePromises = [];
    const deletePromises = [];

    activeRecommendationList.forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id"));
      if (!id) return;

      if (removedIdSet.has(id)) {
        deletePromises.push(serviceOrderApi.deleteDiagnosisRecommendation(id));
        return;
      }

      const originalQty = toEditableQuantity(readField(item, "quantity", "Quantity"));
      const draftQty = toEditableQuantity(quantityById[id]);
      const originalUrgency = normalizeUrgencyToken(readField(item, "urgency", "Urgency"));
      const draftUrgency = normalizeUrgencyToken(urgencyById[id]);
      if (draftQty === originalQty && draftUrgency === originalUrgency) return;

      updatePromises.push(
        serviceOrderApi.updateDiagnosisRecommendation(id, {
          diagnoseId: toPositiveNumber(readField(item, "diagnoseId", "DiagnoseId")),
          parentRecommendationId:
            toPositiveNumber(readField(item, "parentRecommendationId", "ParentRecommendationId")) || null,
          itemType: readField(item, "itemType", "ItemType"),
          standardServiceId: toPositiveNumber(readField(item, "standardServiceId", "StandardServiceId")) || null,
          inventoryItemId: toPositiveNumber(readField(item, "inventoryItemId", "InventoryItemId")) || null,
          quantity: Number(draftQty),
          urgency: draftUrgency,
          evidenceMediaUrls: readField(item, "evidenceMediaUrls", "EvidenceMediaUrls") || null,
          technicianNotes: readField(item, "technicianNotes", "TechnicianNotes") || null,
          updatedBy: staffId,
        })
      );
    });

    for (const task of updatePromises) {
      // eslint-disable-next-line no-await-in-loop
      await task;
    }
    for (const task of deletePromises) {
      // eslint-disable-next-line no-await-in-loop
      await task;
    }
  }, [activeRecommendationList, quantityById, urgencyById, removedIdSet, staffId]);

  const handleApprove = useCallback(async () => {
    if (!numericOrderId || !staffId) return;
    if (isForemanDecisionLocked) {
      setError("Phiếu này đã có quyết định duyệt cuối, không thể duyệt lại.");
      return;
    }
    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await persistDraftChanges();
      await serviceOrderApi.decideDiagnosisForeman(numericOrderId, {
        foremanId: staffId,
        approve: true,
        note: approveNote || null,
      });
      setActionMessage("Qu?n d?c d? duyệt v? chuy?n sang lu?ng b?o giờ thuong m?i.");
      window.alert("Duyệt & chuy?n b?o giờ thợnh cầng.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Kh?ng thợ duyệt chuy?n b?o giờ.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, approveNote, persistDraftChanges, loadData, isForemanDecisionLocked]);

  const handleReject = useCallback(async () => {
    if (!numericOrderId || !staffId) return;
    if (isForemanDecisionLocked) {
      setError("Phiếu này đã có quyết định duyệt cuối, không thể từ chối lại.");
      return;
    }
    if (!String(rejectNote || "").trim()) {
      setError("Vui l?ng nhợp lệ do từ chối d? tr? phi?u kh?m lại.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await persistDraftChanges();
      await serviceOrderApi.decideDiagnosisForeman(numericOrderId, {
        foremanId: staffId,
        approve: false,
        note: rejectNote,
      });
      setActionMessage("từ chối v? chuy?n lại cho kỹ thuật kh?m lại.");
      window.alert("T? chời duyệt thành công.");
      setRejectNote("");
      setShowRejectBox(false);
      await loadData();
    } catch (err) {
      setError(err?.message || "Kh?ng thể từ chối phi?u chuẩn do?n.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, rejectNote, persistDraftChanges, loadData, isForemanDecisionLocked]);

  const handleApproveQuotationTechnical = useCallback(async () => {
    const quotationId = toPositiveNumber(readField(latestQuotation, "id", "Id"));
    if (!quotationId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.approveQuotation(quotationId, {
        approvedBy: staffId,
        note: String(approveNote || "").trim() || "Duyệt kỹ thuật b?o giờ",
        decisions: [],
      });

      setActionMessage("duyệt kỹ thuật b?o giờ thợnh cầng.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Kh?ng thợ duyệt kỹ thuật b?o giờ.");
    } finally {
      setSaving(false);
    }
  }, [latestQuotation, staffId, approveNote, loadData]);

  return (
    <div className="foreman-approval-page">
      <div className="foreman-approval-container">
        <header className="foreman-approval-header">
          <div>
            <h1>Duyệt chuẩn do?n - Qu?n d?c</h1>
            <p>
              L?nh <strong>{readField(order, "code", "Code") || `#${orderId}`}</strong>
            </p>
          </div>
          <button type="button" className="fa-back-btn" onClick={() => navigate(backPath)}>
            <ArrowLeft size={16} />
            Quay lại
          </button>
        </header>

        {loading ? (
          <section className="fa-card">
            <p className="fa-loading">?ang t?i dữ liệu duyệt chuẩn do?n...</p>
          </section>
        ) : (
          <>
            <section className="fa-card fa-summary-grid">
              <article className="fa-summary-item">
                <span>Kh?ch h?ng</span>
                <strong>{customerDisplay}</strong>
              </article>
              <article className="fa-summary-item">
                <span>Bi?n s?</span>
                <strong>{plateDisplay}</strong>
              </article>
              <article className="fa-summary-item">
                <span>SLA chờ duyệt</span>
                <strong className={`fa-sla-chip ${slaSeverity === "RED" ? "red" : slaSeverity === "YELLOW" ? "yellow" : "green"}`}>{slaLabel}</strong>
              </article>
              <article className="fa-summary-item">
                <span>S? đề xuất</span>
                <strong>{totalSuggestions}</strong>
              </article>
              <article className="fa-summary-item">
                <span>Duyệt t? tru?ng</span>
                <strong>{getStatusLabelVi(technicalStatus, technicalStatus)}</strong>
              </article>
              <article className="fa-summary-item">
                <span>Duyệt quản đốc</span>
                <strong>{getStatusLabelVi(finalStatus, finalStatus)}</strong>
              </article>
            </section>

            <section className="fa-card">
              <h2>Th?ng tin chuẩn do?n kỹ thuật</h2>
              <div className={`fa-info-grid ${assignmentNote ? "with-assignment" : "without-assignment"}`}>
                <div className="fa-readonly-box">
                  <label>Tri?u chọng</label>
                  <p>{symptom}</p>
                </div>

                <div className="fa-readonly-box">
                  <label>C?p nh?t g?n nh?t</label>
                  <p>{formatDateTime(readField(latestDiagnosis, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt"))}</p>
                </div>

              </div>

              <div className="fa-diagnosis-note-grid">
                <div className="fa-root-cause-wrap">
                  <label htmlFor="fa-root-cause">Nguy?n nh?n</label>
                  <textarea id="fa-root-cause" rows={2} value={rootCauseText} readOnly />
                </div>
                <div className="fa-root-cause-wrap">
                  <label htmlFor="fa-assignment-note">Ghi chú</label>
                  {isAssignmentAuto ? (
                    <p className="fa-note-system">
                      <Settings size={14} />
                      <span>{diagnosisNoteText}</span>
                    </p>
                  ) : (
                    <textarea id="fa-assignment-note" rows={2} value={diagnosisNoteText} readOnly />
                  )}
                </div>
              </div>

              {error ? <p className="fa-error">{error}</p> : null}
              {actionMessage ? <p className="fa-success">{actionMessage}</p> : null}
              {isTeamLeaderWarning ? (
                <p className="fa-note">
                  Cầnh b?o SLA: Phiếu d? chờ t? tru?ng duyệt {teamLeaderPendingMinutes} ph?t.
                </p>
              ) : null}
              {isTeamLeaderEscalated ? (
                <p className="fa-error">
                  Cảnh báo SLA: Phiếu đã quá hạn duyệt tổ trưởng ({teamLeaderPendingMinutes} phút) và được chuyển lên quản đốc xử lý.
                </p>
              ) : null}
              {isForemanBypassMode ? (
                <p className="fa-note">
                  Phiếu chưa có duyệt tổ trưởng. Quản đốc đang ở chế độ duyệt vượt cấp (VIP/khẩn).
                </p>
              ) : null}
            </section>

            <section className="fa-card">
              <h2>Đề xuất kỹ thuật của thợ</h2>
              <div className="fa-table-wrap">
                <table className="fa-table">
                  <thead>
                    <tr>
                      <th>Hạng mục</th>
                      <th className="fa-col-center">Số lượng</th>
                      <th>Mức độ ưu tiên</th>
                      <th className="fa-col-center">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {treeRows.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="fa-empty-cell">Chưa có đề xuất kỹ thuật.</td>
                      </tr>
                    ) : (
                      treeRows.map((group, groupIndex) => {
                        const parentRow = group.parent;
                        const parentRowId = toPositiveNumber(group.parentId);
                        const parentLabel = recommendationDisplayName(parentRow, serviceLookup, productLookup);
                        const parentUrgencyRaw = readField(parentRow, "urgency", "Urgency");
                        const parentUrgency = getUrgencyLabelVi(parentUrgencyRaw);
                        const parentUrgencyTone = getUrgencyTone(parentUrgencyRaw);
                        const parentNoteRaw = readField(parentRow, "technicianNotes", "TechnicianNotes");
                        const parentNote = String(parentNoteRaw || "").trim();
                        const parentEvidenceUrls = parseEvidenceMediaUrls(readField(parentRow, "evidenceMediaUrls", "EvidenceMediaUrls"));
                        const parentType = normalizeRecommendationItemType(readField(parentRow, "itemType", "ItemType")) === "SERVICE"
                          ? "Dịch vụ"
                          : "Hạng mục";

                        const serviceOrderNo = groupIndex + 1;
                        const parentQty = Number(
                          quantityById[parentRowId] || readField(parentRow, "quantity", "Quantity") || 1
                        );
                        const parentTypeToken = normalizeRecommendationItemType(readField(parentRow, "itemType", "ItemType"));
                        const parentQtyWarningMessage = getQuantityWarningMessage(parentTypeToken, parentQty);
                        const visibleChildren = (group.children || []).filter((child) => !child.isSynthetic);
                          const parentUrgencyValue = normalizeUrgencyToken(urgencyById[group.parentId] ?? parentUrgencyRaw);

                        return (
                          <React.Fragment key={group.parentId || `group-parent-${parentLabel}`}>
                            <tr className="fa-parent-row">
                              <td>
                                <div className="fa-item-cell">
                                  <span className="fa-item-kind service">{parentType} #{serviceOrderNo}</span>
                                  <span className="fa-item-name">{parentLabel}</span>
                                </div>
                                {parentType === "Dịch vụ" ? (
                                  <div className="fa-service-meta">
                                    <p className="fa-service-note">
                                      <strong>Ghi chú kỹ thuật:</strong>
                                      <span>{parentNote || " Chưa có ghi chú kỹ thuật."}</span>
                                    </p>
                                    {parentEvidenceUrls.length > 0 ? (
                                      <div className="fa-evidence-list">
                                        {parentEvidenceUrls.map((url, index) => (
                                          <a
                                            key={`${url}-${index}`}
                                            href={url}
                                            target="_blank"
                                            rel="noreferrer"
                                            className="fa-evidence-item"
                                            title="Mở ảnh kỹ thuật"
                                          >
                                            <img src={url} alt={`Ảnh kỹ thuật ${index + 1}`} loading="lazy" />
                                          </a>
                                        ))}
                                      </div>
                                    ) : (
                                      <p className="fa-service-note-empty">Chưa có ảnh kỹ thuật.</p>
                                    )}
                                  </div>
                                ) : null}
                              </td>
                              <td className="fa-col-center">
                                {parentRowId ? (
                                  <>
                                    <div className="fa-qty-control">
                                      <button type="button" onClick={() => decreaseQuantity(parentRowId)} disabled={isRecommendationEditingLocked} aria-label="Giảm số lượng dịch vụ">
                                        <Minus size={14} />
                                      </button>
                                      <input
                                        type="number"
                                        min={1}
                                        step={1}
                                  value={quantityById[parentRowId] ?? String(parentQty)}
                                        onChange={(event) => editQuantityInput(parentRowId, event.target.value)}
                                        onBlur={() => commitQuantityInput(parentRowId)}
                                        disabled={isRecommendationEditingLocked}
                                        className={`fa-qty-input ${parentQtyWarningMessage ? "fa-qty-warning" : ""}`}
                                        aria-label="Nhập trực tiếp số lượng dịch vụ"
                                      />
                                      <button
                                        type="button"
                                        onClick={() => changeQuantity(parentRowId, (current) => current + 1)}
                                        disabled={isRecommendationEditingLocked}
                                        aria-label="Tăng số lượng dịch vụ"
                                      >
                                        <Plus size={14} />
                                      </button>
                                    </div>
                                    {parentQtyWarningMessage ? <p className="fa-qty-warning-text">{parentQtyWarningMessage}</p> : null}
                                  </>
                                ) : (
                                  "-"
                                )}
                              </td>
                              <td>
                                {group.parentId ? (
                                  <select
                                    className="fa-urgency-select"
                                    value={parentUrgencyValue}
                                    onChange={(event) => changeUrgency(group.parentId, event.target.value)}
                                    disabled={isRecommendationEditingLocked}
                                  >
                                    <option value="CRITICAL">Khẩn cấp</option>
                                    <option value="RECOMMENDED">Khuyến nghị</option>
                                    <option value="LOW">Thấp</option>
                                  </select>
                                ) : (
                                  <span className={`fa-urgency-chip ${parentUrgencyTone}`}>{parentUrgency}</span>
                                )}
                              </td>
                              <td className="fa-col-center">
                                {group.parentId ? (
                                  <button
                                    type="button"
                                    className="fa-trash-btn"
                                    title="Loại bỏ dịch vụ đề xuất"
                                    onClick={() => toggleRemoveGroup(group)}
                                    disabled={isRecommendationEditingLocked}
                                  >
                                    <Trash2 size={16} />
                                  </button>
                                ) : (
                                  "-"
                                )}
                              </td>
                            </tr>

                            {visibleChildren.map((child, childIndex) => {
                              const row = child.source;
                              const rowId = child.rowId;
                              const qty = Number(quantityById[rowId] || readField(row, "quantity", "Quantity") || 1);
                              const itemName = recommendationDisplayName(row, serviceLookup, productLookup);
                              const urgencyRaw = readField(row, "urgency", "Urgency");
                              const urgency = getUrgencyLabelVi(urgencyRaw);
                              const urgencyTone = getUrgencyTone(urgencyRaw);
                              const urgencyValue = normalizeUrgencyToken(urgencyById[rowId] ?? urgencyRaw);
                              const childType = normalizeRecommendationItemType(readField(row, "itemType", "ItemType")) === "SERVICE"
                                ? "Dịch vụ"
                                : "Vật tư";
                              const childTypeToken = normalizeRecommendationItemType(readField(row, "itemType", "ItemType"));
                              const childQtyWarningMessage = getQuantityWarningMessage(childTypeToken, qty);

                              const itemOrderNo = childIndex + 1;

                              return (
                                <tr key={rowId} className="fa-child-row">
                                  <td className="fa-child-name">
                                    <div className="fa-item-cell">
                                      <span className={`fa-item-kind ${childType === "Vật tư" ? "part" : "service"}`}>{childType} #{itemOrderNo}</span>
                                      <span className="fa-item-name">+- {itemName}</span>
                                    </div>
                                  </td>
                                  <td className="fa-col-center">
                                    <>
                                      <div className="fa-qty-control">
                                        <button type="button" onClick={() => decreaseQuantity(rowId)} disabled={isRecommendationEditingLocked} aria-label="Giảm số lượng">
                                          <Minus size={14} />
                                        </button>
                                        <input
                                          type="number"
                                          min={1}
                                          step={1}
                                        value={quantityById[rowId] ?? String(qty)}
                                          onChange={(event) => editQuantityInput(rowId, event.target.value)}
                                          onBlur={() => commitQuantityInput(rowId)}
                                          disabled={isRecommendationEditingLocked}
                                          className={`fa-qty-input ${childQtyWarningMessage ? "fa-qty-warning" : ""}`}
                                          aria-label="Nhập trực tiếp số lượng"
                                        />
                                        <button
                                          type="button"
                                          onClick={() => changeQuantity(rowId, (current) => current + 1)}
                                          disabled={isRecommendationEditingLocked}
                                          aria-label="Tăng số lượng"
                                        >
                                          <Plus size={14} />
                                        </button>
                                      </div>
                                      {childQtyWarningMessage ? <p className="fa-qty-warning-text">{childQtyWarningMessage}</p> : null}
                                    </>
                                  </td>
                                  <td>
                                    {!child.isSynthetic ? (
                                      <select
                                        className="fa-urgency-select"
                                        value={urgencyValue}
                                        onChange={(event) => changeUrgency(rowId, event.target.value)}
                                        disabled={isRecommendationEditingLocked}
                                      >
                                        <option value="CRITICAL">Khẩn cấp</option>
                                        <option value="RECOMMENDED">Khuyến nghị</option>
                                        <option value="LOW">Thấp</option>
                                      </select>
                                    ) : (
                                      <span className={`fa-urgency-chip ${urgencyTone}`}>{urgency}</span>
                                    )}
                                  </td>
                                  <td className="fa-col-center">
                                    {!child.isSynthetic ? (
                                      <button
                                        type="button"
                                        className="fa-trash-btn"
                                        title="Loại bỏ vật tư này"
                                        onClick={() => toggleRemoveRow(rowId)}
                                        disabled={isRecommendationEditingLocked}
                                      >
                                        <Trash2 size={16} />
                                      </button>
                                    ) : (
                                      "-"
                                    )}
                                  </td>
                                </tr>
                              );
                            })}
                          </React.Fragment>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="fa-card">
              <h2>Xử lý duyệt kỹ thuật</h2>
              <div className="fa-form-block">
                <label htmlFor="fa-approve-note">Ghi chú duyệt</label>
                <textarea
                  id="fa-approve-note"
                  rows={4}
                  value={approveNote}
                  onChange={(event) => setApproveNote(event.target.value)}
                  placeholder="Nhập ghi chú duyệt nội bộ..."
                  disabled={saving || isForemanDecisionLocked}
                />
              </div>

              {showRejectBox ? (
                <div className="fa-form-block">
                  <label htmlFor="fa-reject-note">Lý do từ chối (nếu trả khám lại)</label>
                  <textarea
                    id="fa-reject-note"
                    rows={4}
                    value={rejectNote}
                    onChange={(event) => setRejectNote(event.target.value)}
                    placeholder="Nhập lý do từ chối và yêu cầu khám lại..."
                    disabled={saving || isForemanDecisionLocked}
                  />
                </div>
              ) : null}

              {!isForemanDecisionLocked ? (
                <div className="fa-actions">
                  <button
                    type="button"
                    className="btn-primary"
                    onClick={handleApprove}
                    disabled={saving || !staffId || isForemanDecisionLocked}
                  >
                    Duyệt & Chuyển báo giá
                  </button>
                  {showRejectBox ? (
                    <>
                      <button
                        type="button"
                        className="btn-danger-outline"
                        onClick={handleReject}
                        disabled={saving || !staffId || isForemanDecisionLocked}
                      >
                        Xác nhận từ chối & Khám lại
                      </button>
                      <button
                        type="button"
                        className="btn-ghost"
                        onClick={() => {
                          setShowRejectBox(false);
                          setRejectNote("");
                        }}
                        disabled={saving}
                      >
                        H?y nhập lý do
                      </button>
                    </>
                  ) : (
                    <button
                      type="button"
                      className="btn-danger-outline"
                      onClick={() => setShowRejectBox(true)}
                      disabled={saving || !staffId || isForemanDecisionLocked}
                    >
                      T? chời & Kh?m lại
                    </button>
                  )}
                </div>
              ) : null}

              {canApproveQuotationTechnical ? (
                <div className="fa-actions" style={{ marginTop: 12 }}>
                  <button
                    type="button"
                    className="btn-primary"
                    onClick={handleApproveQuotationTechnical}
                    disabled={saving || !staffId}
                  >
                    Duyệt kỹ thuật b?o giờ
                  </button>
                </div>
              ) : null}
            </section>
          </>
        )}
      </div>
    </div>
  );
}


