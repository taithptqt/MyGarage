import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllStandardServices } from "@features/admin/api/serviceApi";
import { getAllVehicleBrands, getAllVehicleModels } from "@features/admin/api/vehicleCatalogApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { getStatusLabelVi } from "@shared/core/utils/statusLabelUtils";
import {
  extractLatestDiagnosisId,
  firstNonEmptyText,
  formatDateTime,
  formatMoney,
  normalizeRecommendationItemType,
  normalizeStatus,
  readField,
  resolveCurrentStaffId,
  toPositiveNumber,
} from "./quotationFlowUtils";
import "./ServiceOrderQuote.css";
import {
  User,
  Car,
  Info,
  FileText,
  Send,
  Plus,
  Check,
  X,
  Trash2,
  Clock,
  DollarSign,
  AlertTriangle,
  ChevronRight,
  ChevronDown,
  Download,
  Save,
  ShieldCheck,
  Mail,
  Eye,
  Upload,
  RefreshCw,
  Image as ImageIcon,
  AlertCircle,
  Phone,
  Activity,
  Wallet,
  CreditCard
} from "lucide-react";

const SERVICE_ROW_TOOLTIP_EXPAND = "Nhấn để xem phụ tùng thuộc dịch vụ này.";
const SERVICE_ROW_TOOLTIP_COLLAPSE = "Nhấn để thu gọn phụ tùng thuộc dịch vụ này.";
const SERVICE_ROW_TOOLTIP_STATIC = "Dòng dịch vụ chính trong báo giá.";

// Sub-components for better organization
function QuoteStepper({ status }) {
  const steps = [
    { id: "DRAFT", label: "Bản nháp", icon: <FileText size={18} /> },
    { id: "PENDING_INSURANCE", label: "Chờ BH duyệt", icon: <ShieldCheck size={18} /> },
    { id: "INSURANCE_APPROVED", label: "BH đã duyệt", icon: <Check size={18} /> },
    { id: "PENDING_CUSTOMER", label: "Đợi khách chốt", icon: <User size={18} /> },
    { id: "CUSTOMER_APPROVED", label: "Hoàn tất", icon: <Check size={18} /> },
  ];

  const currentIdx = steps.findIndex(s => s.id === status);

  return (
    <div className="quote-stepper">
      {steps.map((step, idx) => {
        let state = "pending";
        if (idx < currentIdx) state = "completed";
        else if (idx === currentIdx) state = "active";
        if (status === "CUSTOMER_APPROVED") state = "completed";

        return (
          <div key={step.id} className={`step-item ${state}`}>
            <div className="step-icon">{state === "completed" ? <Check size={18} /> : step.icon}</div>
            <div className="step-label">{step.label}</div>
          </div>
        );
      })}
    </div>
  );
}


function QuoteInfoCard({ icon, label, value, subValue }) {
  return (
    <div className="info-card-premium">
      <div className="info-card-icon">{icon}</div>
      <div className="info-card-content">
        <span>{label}</span>
        <strong>{value}</strong>
        {subValue && <small className="info-card-sub">{subValue}</small>}
      </div>
    </div>
  );
}


function extractServiceId(service) {
  return toPositiveNumber(readField(service, "id", "Id"));
}

function extractServiceName(service) {
  return String(readField(service, "name", "Name", "serviceName", "ServiceName") || "").trim();
}

function extractServicePrice(service) {
  const val = Number(readField(service, "basePrice", "BasePrice", "unitPrice", "UnitPrice", "price", "Price") || 0);
  return Number.isFinite(val) && val > 0 ? val : 0;
}

function extractServiceTax(service) {
  const val = Number(readField(service, "taxRate", "TaxRate") || 0);
  return Number.isFinite(val) && val >= 0 ? val : 0;
}

function extractProductId(product) {
  return toPositiveNumber(readField(product, "id", "Id", "productId", "ProductId"));
}

function extractProductName(product) {
  return String(readField(product, "name", "Name", "productName", "ProductName") || "").trim();
}

function extractProductPrice(product) {
  const val = Number(
    readField(product, "salePrice", "SalePrice", "retailPrice", "RetailPrice", "unitPrice", "UnitPrice", "sellingPrice", "SellingPrice") || 0,
  );
  return Number.isFinite(val) && val > 0 ? val : 0;
}

function extractProductTax(product) {
  const val = Number(readField(product, "taxRate", "TaxRate") || 0);
  return Number.isFinite(val) && val >= 0 ? val : 0;
}

function normalizeOrderDetailType(rawType) {
  const normalized = String(rawType || "").trim().toUpperCase();
  if (["LABOR", "SERVICE", "SERVICES", "WORK"].includes(normalized)) return "SERVICE";
  if (["PART", "PARTS", "PRODUCT", "MATERIAL", "INVENTORY"].includes(normalized)) return "PART";
  return normalized;
}

function isValidHttpUrl(value) {
  const raw = String(value || "").trim();
  if (!raw) return false;
  try {
    const parsed = new URL(raw);
    return parsed.protocol === "http:" || parsed.protocol === "https:";
  } catch {
    return false;
  }
}

function normalizeBooleanLike(value) {
  const normalized = String(value ?? "").trim().toLowerCase();
  return normalized === "true" || normalized === "1" || normalized === "yes";
}

function pickPreferredActiveQuotationId(quotationRows) {
  const rows = Array.isArray(quotationRows) ? quotationRows : [];
  if (rows.length === 0) return null;
  // Rows are already sorted by VersionNo desc, so pick latest version directly.
  return Number(readField(rows[0], "id", "Id")) || null;
}

function toCsvCell(value) {
  const normalized = String(value ?? "").replace(/"/g, "\"\"");
  return `"${normalized}"`;
}

function resolveCustomerDisplay(order, customerById) {
  const directText = firstNonEmptyText(
    readField(order, "customerName", "CustomerName", "customerFullName", "CustomerFullName", "guestName", "GuestName"),
    order?.customer?.fullName,
    order?.customer?.FullName,
    order?.customer?.name,
    order?.customer?.Name,
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
      "GuestVehicleLicensePlate",
    ),
    order?.vehicle?.licensePlate,
    order?.vehicle?.LicensePlate,
    order?.customerVehicle?.licensePlate,
    order?.customerVehicle?.LicensePlate,
  );
  if (directText) return directText;

  const vehicleId = toPositiveNumber(readField(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? vehicleById.get(vehicleId) : null;
  return firstNonEmptyText(vehicle?.licensePlate, vehicle?.LicensePlate, vehicle?.plateNumber, vehicle?.PlateNumber, "-");
}

function resolveVehicleBrandModelDisplay(order, vehicleById, brandById, modelById) {
  const directBrand = firstNonEmptyText(
    readField(order, "vehicleBrandName", "VehicleBrandName", "brandName", "BrandName"),
    order?.vehicle?.brandName,
    order?.vehicle?.BrandName,
    order?.customerVehicle?.brandName,
    order?.customerVehicle?.BrandName,
  );
  const directModel = firstNonEmptyText(
    readField(order, "vehicleModelName", "VehicleModelName", "modelName", "ModelName"),
    order?.vehicle?.modelName,
    order?.vehicle?.ModelName,
    order?.customerVehicle?.modelName,
    order?.customerVehicle?.ModelName,
  );
  if (directBrand || directModel) {
    return firstNonEmptyText(`${directBrand} ${directModel}`.trim(), directBrand, directModel, "-");
  }

  const vehicleId = toPositiveNumber(readField(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? vehicleById.get(vehicleId) : null;
  const brandId = toPositiveNumber(
    readField(order, "vehicleBrandId", "VehicleBrandId", "brandId", "BrandId"),
    readField(vehicle, "vehicleBrandId", "VehicleBrandId", "brandId", "BrandId"),
  );
  const modelId = toPositiveNumber(
    readField(order, "vehicleModelId", "VehicleModelId", "modelId", "ModelId"),
    readField(vehicle, "vehicleModelId", "VehicleModelId", "modelId", "ModelId"),
  );

  const brandFromCatalog = brandId ? brandById.get(brandId) : null;
  const modelFromCatalog = modelId ? modelById.get(modelId) : null;

  const fallbackBrand = firstNonEmptyText(
    vehicle?.brandName,
    vehicle?.BrandName,
    vehicle?.vehicleBrandName,
    vehicle?.VehicleBrandName,
    readField(brandFromCatalog, "name", "Name", "code", "Code"),
  );
  const fallbackModel = firstNonEmptyText(
    vehicle?.modelName,
    vehicle?.ModelName,
    vehicle?.vehicleModelName,
    vehicle?.VehicleModelName,
    readField(modelFromCatalog, "modelName", "ModelName", "name", "Name"),
  );
  return firstNonEmptyText(`${fallbackBrand} ${fallbackModel}`.trim(), fallbackBrand, fallbackModel, "-");
}

export default function ServiceOrderQuotePage() {
  const navigate = useNavigate();
  const { orderId } = useParams();
  const { user } = useAuth();

  const staffId = useMemo(() => resolveCurrentStaffId(user), [user]);
  const numericOrderId = Number(orderId);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [actionMessage, setActionMessage] = useState("");

  const [order, setOrder] = useState(null);
  const [recommendations, setRecommendations] = useState([]);
  const [serviceOrderDetails, setServiceOrderDetails] = useState([]);
  const [quotations, setQuotations] = useState([]);
  const [activeQuotationId, setActiveQuotationId] = useState(null);

  const [services, setServices] = useState([]);
  const [products, setProducts] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [vehicleBrands, setVehicleBrands] = useState([]);
  const [vehicleModels, setVehicleModels] = useState([]);

  const [cloneNote, setCloneNote] = useState("");
  const [sendEmailNote, setSendEmailNote] = useState("");
  const [insuranceEvidenceUrls, setInsuranceEvidenceUrls] = useState([]);
  const [insuranceApprovalNote, setInsuranceApprovalNote] = useState("");
  const [insuranceEvidenceUploading, setInsuranceEvidenceUploading] = useState(false);
  const [insuranceEvidenceUploadMessage, setInsuranceEvidenceUploadMessage] = useState("");
  const [removeIdsForClone, setRemoveIdsForClone] = useState([]);
  const [activeActionTab, setActiveActionTab] = useState("insurance"); // 'insurance', 'customer', 'clone'
  const [splitBillingDraft, setSplitBillingDraft] = useState({});
  const [cancelReason, setCancelReason] = useState("Khách vội");
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [showMandatoryCloneWarning, setShowMandatoryCloneWarning] = useState(false);
  const [mailSentNotice, setMailSentNotice] = useState("");
  const [collapsedPartGroups, setCollapsedPartGroups] = useState({});
  const [hoveredGroupKey, setHoveredGroupKey] = useState(null);

  const autoCreateTriedRef = useRef(false);
  const tabSelectionLockedRef = useRef(false);

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

  const vehicleBrandById = useMemo(() => {
    const map = new Map();
    (vehicleBrands || []).forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id"));
      if (id) map.set(id, item);
    });
    return map;
  }, [vehicleBrands]);

  const vehicleModelById = useMemo(() => {
    const map = new Map();
    (vehicleModels || []).forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id"));
      if (id) map.set(id, item);
    });
    return map;
  }, [vehicleModels]);

  const activeVehicleModelId = useMemo(() => {
    const fromOrder = toPositiveNumber(readField(order, "vehicleModelId", "VehicleModelId", "modelId", "ModelId"));
    if (fromOrder) return fromOrder;
    const vehicleId = toPositiveNumber(readField(order, "vehicleId", "VehicleId"));
    const vehicle = vehicleId ? vehicleById.get(vehicleId) : null;
    return toPositiveNumber(readField(vehicle, "vehicleModelId", "VehicleModelId", "modelId", "ModelId"));
  }, [order, vehicleById]);

  const serviceLookup = useMemo(() => {
    const map = new Map();
    (services || []).forEach((item) => {
      const id = extractServiceId(item);
      if (id) map.set(id, item);
    });
    return map;
  }, [services]);

  const productLookup = useMemo(() => {
    const map = new Map();
    (products || []).forEach((item) => {
      const id = extractProductId(item);
      if (id) map.set(id, item);
    });
    return map;
  }, [products]);

  const activeQuotation = useMemo(
    () => (quotations || []).find((item) => Number(readField(item, "id", "Id")) === Number(activeQuotationId)) || null,
    [quotations, activeQuotationId],
  );

  const recommendationById = useMemo(() => {
    const map = new Map();
    (recommendations || []).forEach((item) => {
      const recommendationId = toPositiveNumber(readField(item, "id", "Id"));
      if (recommendationId) map.set(recommendationId, item);
    });
    return map;
  }, [recommendations]);

  const supplementaryMetaByQuotationDetailId = useMemo(() => {
    const bySodQuotationDetailId = new Map();
    const byQuotationDetailNodeId = new Map();

    (Array.isArray(serviceOrderDetails) ? serviceOrderDetails : []).forEach((detail) => {
      const quotationDetailId = toPositiveNumber(readField(detail, "quotationDetailId", "QuotationDetailId"));
      if (!quotationDetailId) return;

      const serviceSourceRaw = String(readField(detail, "serviceSource", "ServiceSource") || "").trim().toUpperCase();
      const isSupplementary =
        normalizeBooleanLike(readField(detail, "isSupplementary", "IsSupplementary")) ||
        serviceSourceRaw === "SUPPLEMENTARY_TECHNICIAN" ||
        serviceSourceRaw === "SUPPLEMENTARY_CUSTOMER";

      const supplementaryReasonCode = String(readField(detail, "supplementaryReasonCode", "SupplementaryReasonCode") || "")
        .trim()
        .toUpperCase();

      bySodQuotationDetailId.set(quotationDetailId, {
        isSupplementary,
        serviceSource: serviceSourceRaw || (isSupplementary ? "SUPPLEMENTARY_CUSTOMER" : "INITIAL"),
        supplementaryReasonCode: supplementaryReasonCode || null,
      });
    });

    (Array.isArray(quotations) ? quotations : []).forEach((quotationRow) => {
      const versionNo = Number(readField(quotationRow, "versionNo", "VersionNo") || 0);
      const details = Array.isArray(quotationRow?.details) ? quotationRow.details : [];
      details.forEach((detail) => {
        const detailId = toPositiveNumber(readField(detail, "id", "Id"));
        if (!detailId) return;
        byQuotationDetailNodeId.set(detailId, {
          detailId,
          versionNo,
          sourceQuotationDetailId: toPositiveNumber(readField(detail, "sourceQuotationDetailId", "SourceQuotationDetailId")),
          diagnosisRecommendationId: toPositiveNumber(readField(detail, "diagnosisRecommendationId", "DiagnosisRecommendationId")),
        });
      });
    });

    const resolveFromLineage = (startDetailId) => {
      if (!startDetailId) return null;

      const visited = new Set();
      let cursorId = startDetailId;
      let hasDiagnosisInLineage = false;
      let rootNode = null;

      while (cursorId && !visited.has(cursorId)) {
        visited.add(cursorId);

        const sodMeta = bySodQuotationDetailId.get(cursorId);
        if (sodMeta) return sodMeta;

        const node = byQuotationDetailNodeId.get(cursorId);
        if (!node) break;

        if (node.diagnosisRecommendationId) hasDiagnosisInLineage = true;
        rootNode = node;
        cursorId = node.sourceQuotationDetailId || null;
      }

      if (!rootNode) return null;

      const isSupplementary = Number(rootNode.versionNo || 0) > 1;
      const supplementaryReasonCode = isSupplementary
        ? hasDiagnosisInLineage
          ? "TECH_FINDING"
          : "CUSTOMER_REQUEST"
        : null;

      return {
        isSupplementary,
        serviceSource: isSupplementary
          ? supplementaryReasonCode === "TECH_FINDING"
            ? "SUPPLEMENTARY_TECHNICIAN"
            : "SUPPLEMENTARY_CUSTOMER"
          : "INITIAL",
        supplementaryReasonCode,
      };
    };

    byQuotationDetailNodeId.forEach((_, detailId) => {
      if (bySodQuotationDetailId.has(detailId)) return;
      const resolved = resolveFromLineage(detailId);
      if (resolved) bySodQuotationDetailId.set(detailId, resolved);
    });

    return bySodQuotationDetailId;
  }, [quotations, serviceOrderDetails]);

  const quotationDetailRows = useMemo(() => {
    const source = Array.isArray(activeQuotation?.details) ? activeQuotation.details : [];
    if (source.length === 0) return [];

    const baseRows = source.map((row, originalIndex) => {
      const recommendationId = toPositiveNumber(readField(row, "diagnoseRecommendationId", "DiagnoseRecommendationId"));
      const recommendation = recommendationId ? recommendationById.get(recommendationId) : null;
      const itemType = normalizeRecommendationItemType(
        readField(recommendation, "itemType", "ItemType") || readField(row, "itemType", "ItemType"),
      );
      const parentRecommendationId = toPositiveNumber(readField(recommendation, "parentRecommendationId", "ParentRecommendationId"));
      const parentRecommendation = parentRecommendationId ? recommendationById.get(parentRecommendationId) : null;
      const explicitParentName = firstNonEmptyText(
        readField(parentRecommendation, "itemName", "ItemName", "standardServiceName", "StandardServiceName", "serviceName", "ServiceName"),
      );
      return {
        row,
        originalIndex,
        detailId: Number(readField(row, "id", "Id")) || null,
        recommendationId,
        itemType,
        itemName: String(readField(row, "itemName", "ItemName") || "").trim(),
        parentRecommendationId,
        explicitParentName,
        urgency: readField(recommendation, "urgency", "Urgency"),
      };
    });

    const serviceNameByRecommendationId = new Map();
    baseRows.forEach((entry) => {
      if (entry.itemType === "SERVICE" && entry.recommendationId) {
        serviceNameByRecommendationId.set(entry.recommendationId, entry.itemName || `Dịch vụ #${entry.recommendationId}`);
      }
    });

    let nearestServiceGroupKey = null;
    let nearestServiceName = "";
    const groupedRows = baseRows.map((entry) => {
      const normalizedUrgency = String(entry.urgency || "").toUpperCase();
      const priorityLabel = normalizedUrgency === "URGENT"
        ? "Cấp bách"
        : normalizedUrgency === "HIGH"
          ? "Quan trọng"
          : normalizedUrgency === "NORMAL"
            ? "Bình thường"
            : ""; // Empty if no urgency defined

      if (entry.itemType === "SERVICE") {
        const serviceGroupKey = entry.recommendationId || `service-${entry.originalIndex}`;
        nearestServiceGroupKey = serviceGroupKey;
        nearestServiceName = entry.itemName;
        return {
          ...entry,
          groupKey: serviceGroupKey,
          groupOrder: entry.originalIndex,
          parentName: "",
          isDependentPart: false,
          isMainService: true,
          classificationLabel: priorityLabel || "Dịch vụ chính",
          priorityLabel: priorityLabel || "Bình thường",
          classificationRank: 0,
        };
      }

      const hasExplicitParent = Boolean(entry.parentRecommendationId);
      const resolvedParentName = hasExplicitParent
        ? firstNonEmptyText(entry.explicitParentName, serviceNameByRecommendationId.get(entry.parentRecommendationId))
        : nearestServiceName;
      const resolvedGroupKey = hasExplicitParent
        ? entry.parentRecommendationId
        : nearestServiceGroupKey || `part-${entry.originalIndex}`;

      return {
        ...entry,
        groupKey: resolvedGroupKey,
        groupOrder: entry.originalIndex,
        parentName: resolvedParentName,
        isDependentPart: Boolean(resolvedParentName),
        isMainService: false,
        priorityLabel: priorityLabel, // Only show if explicit urgency exists for parts
        classificationRank: resolvedParentName ? 1 : 2,
      };
    });

    const groupOrderByKey = new Map();
    groupedRows.forEach((entry) => {
      if (!groupOrderByKey.has(entry.groupKey)) {
        groupOrderByKey.set(entry.groupKey, entry.groupOrder);
      }
    });

    const sortedRows = [...groupedRows].sort((a, b) => {
      const groupDiff = (groupOrderByKey.get(a.groupKey) || 0) - (groupOrderByKey.get(b.groupKey) || 0);
      if (groupDiff !== 0) return groupDiff;
      const rankDiff = a.classificationRank - b.classificationRank;
      if (rankDiff !== 0) return rankDiff;
      return a.originalIndex - b.originalIndex;
    });

    return sortedRows.map((entry, index) => {
      const prev = index > 0 ? sortedRows[index - 1] : null;
      const next = index < sortedRows.length - 1 ? sortedRows[index + 1] : null;
      const isGroupStart = !prev || prev.groupKey !== entry.groupKey;
      const isGroupEnd = !next || next.groupKey !== entry.groupKey;
      return {
        ...entry,
        isGroupStart,
        isGroupEnd,
      };
    });
  }, [activeQuotation, recommendationById]);

  const quotationRowByDetailId = useMemo(() => {
    const map = new Map();
    quotationDetailRows.forEach((entry) => {
      if (entry.detailId) map.set(entry.detailId, entry);
    });
    return map;
  }, [quotationDetailRows]);

  const partCountByGroup = useMemo(() => {
    const counts = new Map();
    quotationDetailRows.forEach((entry) => {
      if (!entry?.isDependentPart) return;
      const key = String(entry.groupKey);
      counts.set(key, (counts.get(key) || 0) + 1);
    });
    return counts;
  }, [quotationDetailRows]);

  useEffect(() => {
    setCollapsedPartGroups((prev) => {
      const next = { ...prev };
      const activeKeys = new Set();
      partCountByGroup.forEach((count, key) => {
        if (count > 0) {
          activeKeys.add(key);
          if (!(key in next)) next[key] = true;
        }
      });
      Object.keys(next).forEach((key) => {
        if (!activeKeys.has(key)) delete next[key];
      });
      return next;
    });
  }, [partCountByGroup]);

  useEffect(() => {
    const details = Array.isArray(activeQuotation?.details) ? activeQuotation.details : [];
    const quoteStatus = normalizeStatus(readField(activeQuotation, "status", "Status"));
    const totalInsuranceAmount = Number(readField(activeQuotation, "totalInsuranceAmount", "TotalInsuranceAmount") || 0);
    const shouldAutoFillInsuranceDefault =
      ["DRAFT", "WAITING_APPROVAL", "APPROVED", "QUOTING", "PENDING_INSURANCE"].includes(quoteStatus) &&
      totalInsuranceAmount <= 0 &&
      details.length > 0 &&
      details.every((row) => {
        const raw = Number(
          readField(row, "insuranceCoveredAmount", "InsuranceCoveredAmount", "insuranceRequestedAmount", "InsuranceRequestedAmount") || 0,
        );
        return !Number.isFinite(raw) || raw <= 0;
      });

    const nextDraft = {};
    details.forEach((row) => {
      const detailId = Number(readField(row, "id", "Id"));
      if (!detailId) return;
      const finalAmount = Number(readField(row, "finalAmount", "FinalAmount") || 0);
      const insuranceRaw = readField(
        row,
        "insuranceCoveredAmount",
        "InsuranceCoveredAmount",
        "insuranceRequestedAmount",
        "InsuranceRequestedAmount",
      );
      const insuranceCovered = shouldAutoFillInsuranceDefault
        ? finalAmount
        : insuranceRaw === null || insuranceRaw === undefined || String(insuranceRaw).trim() === ""
          ? finalAmount
          : Number(insuranceRaw);
      const safeInsurance = Number.isFinite(insuranceCovered)
        ? Math.min(Math.max(insuranceCovered, 0), Math.max(0, finalAmount))
        : Math.max(0, finalAmount);
      nextDraft[detailId] = safeInsurance;
    });
    setSplitBillingDraft(nextDraft);
  }, [activeQuotationId, activeQuotation]);

  useEffect(() => {
    const rawUrlString = String(readField(activeQuotation, "insuranceApprovalEvidenceUrl", "InsuranceApprovalEvidenceUrl") || "").trim();
    // Split by comma or semicolon to support existing data that might be multiple
    const initialUrls = rawUrlString ? rawUrlString.split(/[;|,]/).map(u => u.trim()).filter(Boolean) : [];
    setInsuranceEvidenceUrls(initialUrls);
    setInsuranceApprovalNote("");
    setInsuranceEvidenceUploadMessage("");
  }, [activeQuotationId, activeQuotation]);

  const loadData = useCallback(async () => {
    if (!Number.isFinite(numericOrderId) || numericOrderId <= 0) {
      setError("Mã lệnh sửa chữa không hợp lệ.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    setActionMessage("");

    try {
      const [orderRes, diagnosesRes, quotationsRes, detailRes, serviceRes, productRes, customerRes, vehicleRes, brandRes, modelRes] = await Promise.all([
        serviceOrderApi.getById(numericOrderId),
        serviceOrderApi.getDiagnoses(numericOrderId),
        serviceOrderApi.getQuotationsByServiceOrder(numericOrderId),
        serviceOrderApi.getDetails(numericOrderId).catch(() => []),
        getAllStandardServices().catch(() => []),
        inventoryApi.getProducts().catch(() => []),
        getAllCustomers().catch(() => []),
        getAllVehicles().catch(() => []),
        getAllVehicleBrands().catch(() => []),
        getAllVehicleModels().catch(() => []),
      ]);

      const latestDiagnosisId = extractLatestDiagnosisId(diagnosesRes);
      const recommendationRes = latestDiagnosisId
        ? await serviceOrderApi.getDiagnoseRecommendations(latestDiagnosisId).catch(() => [])
        : [];

      const quoteList = Array.isArray(quotationsRes) ? quotationsRes : [];
      const sortedQuoteList = [...quoteList].sort((a, b) => {
        const av = Number(readField(a, "versionNo", "VersionNo") || 0);
        const bv = Number(readField(b, "versionNo", "VersionNo") || 0);
        return bv - av;
      });

      setOrder(orderRes || null);
      setRecommendations(Array.isArray(recommendationRes) ? recommendationRes : []);
      setServiceOrderDetails(Array.isArray(detailRes) ? detailRes : []);
      setQuotations(sortedQuoteList);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setProducts(Array.isArray(productRes) ? productRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setVehicleBrands(Array.isArray(brandRes) ? brandRes : []);
      setVehicleModels(Array.isArray(modelRes) ? modelRes : []);

      const preferredActiveId = pickPreferredActiveQuotationId(sortedQuoteList);
      if (preferredActiveId) {
        setActiveQuotationId(preferredActiveId);
      } else {
        setActiveQuotationId(null);
      }
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu báo giá.");
    } finally {
      setLoading(false);
    }
  }, [numericOrderId]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const refreshQuotationStatusSilently = useCallback(async () => {
    if (!Number.isFinite(numericOrderId) || numericOrderId <= 0) return;

    try {
      const quotationsRes = await serviceOrderApi.getQuotationsByServiceOrder(numericOrderId);
      const quoteList = Array.isArray(quotationsRes) ? quotationsRes : [];
      const sortedQuoteList = [...quoteList].sort((a, b) => {
        const av = Number(readField(a, "versionNo", "VersionNo") || 0);
        const bv = Number(readField(b, "versionNo", "VersionNo") || 0);
        return bv - av;
      });

      const currentActiveId = Number(activeQuotationId);
      const prevActive = (quotations || []).find((item) => Number(readField(item, "id", "Id")) === currentActiveId) || null;
      const nextActive = sortedQuoteList.find((item) => Number(readField(item, "id", "Id")) === currentActiveId) || null;
      const prevStatus = normalizeStatus(readField(prevActive, "status", "Status"));
      const nextStatus = normalizeStatus(readField(nextActive, "status", "Status"));

      setQuotations(sortedQuoteList);
      if (sortedQuoteList.length > 0) {
        const preferredActiveId = pickPreferredActiveQuotationId(sortedQuoteList);
        setActiveQuotationId((prev) => {
          const prevId = Number(prev);
          const stillExists = sortedQuoteList.some((row) => Number(readField(row, "id", "Id")) === prevId);
          return stillExists ? prevId : preferredActiveId;
        });
      } else {
        setActiveQuotationId(null);
      }

      if (prevStatus !== nextStatus && nextStatus === "CUSTOMER_APPROVED") {
        setActionMessage("Khách đã duyệt báo giá online.");
      } else if (prevStatus !== nextStatus && nextStatus === "CUSTOMER_REJECTED") {
        setActionMessage("Khách đã từ chối báo giá online.");
      }
    } catch {
      // Silent refresh should not interrupt user actions.
    }
  }, [activeQuotationId, quotations]);

  useEffect(() => {
    if (loading || saving) return undefined;
    const timerId = setInterval(() => {
      void refreshQuotationStatusSilently();
    }, 30000);

    return () => clearInterval(timerId);
  }, [loading, saving, refreshQuotationStatusSilently]);

  useEffect(() => {
    autoCreateTriedRef.current = false;
    tabSelectionLockedRef.current = false;
  }, [numericOrderId]);

  useEffect(() => {
    if (tabSelectionLockedRef.current) return;
    if (!activeQuotationId || quotations.length === 0) return;

    const active = quotations.find((q) => Number(readField(q, "id", "Id")) === Number(activeQuotationId));
    if (!active) return;

    const status = normalizeStatus(readField(active, "status", "Status"));
    if (status === "INSURANCE_APPROVED" || status === "PENDING_CUSTOMER" || status === "CUSTOMER_APPROVED") {
      setActiveActionTab("customer");
      tabSelectionLockedRef.current = true;
    } else if (status === "DRAFT" || status === "PENDING_INSURANCE") {
      setActiveActionTab("insurance");
      tabSelectionLockedRef.current = true;
    }
  }, [activeQuotationId, quotations]);

  const buildQuotationItemsForDraft = useCallback(async () => {
    const recommendationParents = (Array.isArray(recommendations) ? recommendations : []).filter((item) => {
      const parentId = toPositiveNumber(readField(item, "parentRecommendationId", "ParentRecommendationId"));
      return !parentId;
    });

    const toSafeQuantity = (rawValue) => {
      const quantityRaw = Number(rawValue || 1);
      return Number.isFinite(quantityRaw) && quantityRaw > 0 ? quantityRaw : 1;
    };

    const draftItems = [];
    const serviceRowsForBom = [];
    const seenServiceIds = new Set();

    recommendationParents.forEach((rec) => {
      const itemType = normalizeRecommendationItemType(readField(rec, "itemType", "ItemType"));
      const standardServiceId = toPositiveNumber(readField(rec, "standardServiceId", "StandardServiceId"));
      const inventoryItemId = toPositiveNumber(readField(rec, "inventoryItemId", "InventoryItemId"));
      const quantity = toSafeQuantity(readField(rec, "quantity", "Quantity"));

      let itemId = null;
      let itemName = "";
      let unitPriceAtTime = 0;
      let taxRate = 0;
      let normalizedItemType = "PART";

      if (itemType === "SERVICE" && standardServiceId) {
        const service = serviceLookup.get(standardServiceId);
        itemId = standardServiceId;
        itemName = extractServiceName(service) || `Dịch vụ #${standardServiceId}`;
        unitPriceAtTime = extractServicePrice(service);
        taxRate = extractServiceTax(service);
        normalizedItemType = "LABOR";
        seenServiceIds.add(standardServiceId);
        serviceRowsForBom.push({ serviceId: standardServiceId, quantity, serviceName: itemName });
      } else if (inventoryItemId) {
        const product = productLookup.get(inventoryItemId);
        itemId = inventoryItemId;
        itemName = extractProductName(product) || `Vật tư #${inventoryItemId}`;
        unitPriceAtTime = extractProductPrice(product);
        taxRate = extractProductTax(product);
        normalizedItemType = "PART";
      }

      if (String(itemName || "").trim().length === 0) return;

      draftItems.push({
        diagnoseRecommendationId: toPositiveNumber(readField(rec, "id", "Id")),
        itemType: normalizedItemType,
        itemId,
        itemName,
        description: readField(rec, "technicianNotes", "TechnicianNotes") || null,
        quantity,
        unitPriceAtTime,
        costPriceAtTime: null,
        discountValue: 0,
        taxRate,
        payerType: "INSURANCE",
        insuranceCompanyId: null,
        insuranceCoveredAmount: null,
        customerCoveredAmount: null,
        insuranceRequestedAmount: null,
        customerCoPayAmount: null,
        isBlocking: true,
        isMandatory: true,
        estimatedMinutes: null,
        isReturnOldPart: false,
      });
    });

    const normalizedDetailRows = (Array.isArray(serviceOrderDetails) ? serviceOrderDetails : []).filter((row) => {
      const status = normalizeStatus(readField(row, "status", "Status"));
      return !["CANCELED", "CANCELLED", "REJECTED", "VOID"].includes(status);
    });

    const serviceDetailsFromOrder = normalizedDetailRows.filter((row) => {
      const rawType = normalizeOrderDetailType(readField(row, "itemType", "ItemType"));
      const serviceId = toPositiveNumber(readField(row, "standardServiceId", "StandardServiceId", "itemId", "ItemId"));
      return rawType === "SERVICE" && serviceId;
    });

    serviceDetailsFromOrder.forEach((row) => {
      const serviceId = toPositiveNumber(readField(row, "standardServiceId", "StandardServiceId", "itemId", "ItemId"));
      if (!serviceId || seenServiceIds.has(serviceId)) return;

      const quantity = toSafeQuantity(readField(row, "quantity", "Quantity"));
      const service = serviceLookup.get(serviceId);
      const itemName = firstNonEmptyText(
        readField(row, "itemName", "ItemName"),
        extractServiceName(service),
        `Dịch vụ #${serviceId}`,
      );
      const unitPriceAtTime = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime")) || extractServicePrice(service);
      const taxRate = Number(readField(row, "taxRate", "TaxRate"));
      const safeTaxRate = Number.isFinite(taxRate) && taxRate >= 0 ? taxRate : extractServiceTax(service);

      seenServiceIds.add(serviceId);
      serviceRowsForBom.push({ serviceId, quantity, serviceName: itemName });

      draftItems.push({
        diagnoseRecommendationId: null,
        itemType: "LABOR",
        itemId: serviceId,
        itemName,
        description: readField(row, "description", "Description") || "Dịch vụ khách hàng đã chọn",
        quantity,
        unitPriceAtTime,
        costPriceAtTime: null,
        discountValue: 0,
        taxRate: safeTaxRate,
        payerType: "INSURANCE",
        insuranceCompanyId: null,
        insuranceCoveredAmount: null,
        customerCoveredAmount: null,
        insuranceRequestedAmount: null,
        customerCoPayAmount: null,
        isBlocking: true,
        isMandatory: true,
        estimatedMinutes: null,
        isReturnOldPart: false,
      });
    });

    const standalonePartRows = normalizedDetailRows.filter((row) => {
      const rawType = normalizeOrderDetailType(readField(row, "itemType", "ItemType"));
      const itemId = toPositiveNumber(readField(row, "itemId", "ItemId"));
      return rawType === "PART" && itemId;
    });

    standalonePartRows.forEach((row) => {
      const itemId = toPositiveNumber(readField(row, "itemId", "ItemId"));
      if (!itemId) return;
      const product = productLookup.get(itemId);
      const quantity = toSafeQuantity(readField(row, "quantity", "Quantity"));
      const itemName = firstNonEmptyText(readField(row, "itemName", "ItemName"), extractProductName(product), `Phụ tùng #${itemId}`);
      const unitPriceAtTime = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime")) || extractProductPrice(product);
      const taxRateRaw = Number(readField(row, "taxRate", "TaxRate"));
      const taxRate = Number.isFinite(taxRateRaw) && taxRateRaw >= 0 ? taxRateRaw : extractProductTax(product);

      draftItems.push({
        diagnoseRecommendationId: null,
        itemType: "PART",
        itemId,
        itemName,
        description: readField(row, "description", "Description") || "Phụ tùng khách hàng đã chọn",
        quantity,
        unitPriceAtTime,
        costPriceAtTime: null,
        discountValue: 0,
        taxRate,
        payerType: "INSURANCE",
        insuranceCompanyId: null,
        insuranceCoveredAmount: null,
        customerCoveredAmount: null,
        insuranceRequestedAmount: null,
        customerCoPayAmount: null,
        isBlocking: true,
        isMandatory: Boolean(readField(row, "isMandatory", "IsMandatory")),
        estimatedMinutes: null,
        isReturnOldPart: Boolean(readField(row, "isReturnOldPart", "IsReturnOldPart")),
      });
    });

    if (activeVehicleModelId && serviceRowsForBom.length > 0) {
      const bomResponses = await Promise.all(
        serviceRowsForBom.map(async (entry) => {
          try {
            const rows = await serviceOrderApi.getServiceBoms(entry.serviceId, activeVehicleModelId);
            return { ...entry, rows: Array.isArray(rows) ? rows : [] };
          } catch (_) {
            return { ...entry, rows: [] };
          }
        }),
      );

      bomResponses.forEach((entry) => {
        (entry.rows || []).forEach((bomRow) => {
          const productId = toPositiveNumber(readField(bomRow, "productId", "ProductId"));
          if (!productId) return;
          const product = productLookup.get(productId);
          const bomQty = toSafeQuantity(readField(bomRow, "quantity", "Quantity"));
          const totalQuantity = Math.max(0.001, Number((bomQty * entry.quantity).toFixed(3)));
          const itemName = firstNonEmptyText(
            readField(bomRow, "productName", "ProductName"),
            extractProductName(product),
            `Phụ tùng #${productId}`,
          );

          draftItems.push({
            diagnoseRecommendationId: null,
            itemType: "PART",
            itemId: productId,
            itemName,
            description: `Phụ tùng BOM cho dịch vụ: ${entry.serviceName || `#${entry.serviceId}`}`,
            quantity: totalQuantity,
            unitPriceAtTime: extractProductPrice(product),
            costPriceAtTime: null,
            discountValue: 0,
            taxRate: extractProductTax(product),
            payerType: "INSURANCE",
            insuranceCompanyId: null,
            insuranceCoveredAmount: null,
            customerCoveredAmount: null,
            insuranceRequestedAmount: null,
            customerCoPayAmount: null,
            isBlocking: true,
            isMandatory: Boolean(readField(bomRow, "isMandatory", "IsMandatory")),
            estimatedMinutes: null,
            isReturnOldPart: false,
          });
        });
      });
    }

    const merged = new Map();
    draftItems.forEach((item) => {
      const nameKey = String(item.itemName || "").trim().toLowerCase();
      const key = `${item.itemType}-${item.itemId || 0}-${nameKey}`;
      if (!merged.has(key)) {
        merged.set(key, { ...item });
        return;
      }

      const existing = merged.get(key);
      existing.quantity = Number(existing.quantity || 0) + Number(item.quantity || 0);
      existing.isMandatory = Boolean(existing.isMandatory || item.isMandatory);
      if (!existing.description && item.description) {
        existing.description = item.description;
      }
      if (!existing.unitPriceAtTime && item.unitPriceAtTime) {
        existing.unitPriceAtTime = item.unitPriceAtTime;
      }
      merged.set(key, existing);
    });

    return [...merged.values()].filter((item) => String(item.itemName || "").trim().length > 0);
  }, [recommendations, serviceOrderDetails, serviceLookup, productLookup, activeVehicleModelId]);

  const handleCreateDraftQuotation = useCallback(async () => {
    if (!numericOrderId || !staffId) {
      setError("Không xác định được mã lệnh hoặc nhân sự thao tác.");
      return;
    }

    const items = await buildQuotationItemsForDraft();
    if (items.length === 0) {
      setError("Không có dữ liệu dịch vụ/phụ tùng để tạo báo giá.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");
    setMailSentNotice("");

    try {
      const created = await serviceOrderApi.createQuotation(numericOrderId, {
        title: `Báo giá v${(quotations?.length || 0) + 1}`,
        quotationType: "NORMAL",
        validUntil: null,
        serviceAdvisorId: staffId,
        customerNotes: null,
        internalNotes: "Tạo từ chẩn đoán + dịch vụ khách chọn + BOM phụ tùng",
        submitForApproval: false,
        items,
      });

      setActionMessage("Đã tạo báo giá nháp.");
      await loadData();
      const createdId = Number(readField(created, "id", "Id"));
      if (createdId > 0) setActiveQuotationId(createdId);
    } catch (err) {
      setError(err?.message || "Không thể tạo báo giá nháp.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, buildQuotationItemsForDraft, quotations?.length, loadData]);

  useEffect(() => {
    if (loading || saving) return;
    if (autoCreateTriedRef.current) return;
    if ((quotations || []).length > 0) return;
    if ((recommendations || []).length === 0 && (serviceOrderDetails || []).length === 0) return;
    if (!staffId) return;

    autoCreateTriedRef.current = true;
    void handleCreateDraftQuotation();
  }, [loading, saving, quotations, recommendations, serviceOrderDetails, staffId, handleCreateDraftQuotation]);

  const handleSubmitQuotation = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      const note = String(insuranceApprovalNote || "").trim();
      await serviceOrderApi.submitQuotation(quoteId, {
        actorStaffId: staffId,
        note: note || "Gửi báo giá sang bước chờ bảo hiểm duyệt",
      });
      setActionMessage("Đã chuyển báo giá sang trạng thái chờ bảo hiểm duyệt.");
      setMailSentNotice("");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể gửi báo giá cho bảo hiểm.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, insuranceApprovalNote, loadData]);

  const handleConfirmInsuranceApproval = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    const evidenceUrlsSent = insuranceEvidenceUrls.filter(u => isValidHttpUrl(u));
    if (evidenceUrlsSent.length === 0) {
      setError("Vui lòng tải lên hoặc nhập link bằng chứng phê duyệt bảo hiểm.");
      setActionMessage("");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      const note = String(insuranceApprovalNote || "").trim();
      await serviceOrderApi.confirmQuotationInsuranceApproval(quoteId, {
        actorStaffId: staffId,
        insuranceApprovalEvidenceUrl: evidenceUrlsSent.join(", "),
        note: note || "Bảo hiểm đã xác nhận duyệt",
      });
      setActionMessage("Đã xác nhận bảo hiểm duyệt và khóa dữ liệu bồi thường.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể xác nhận bảo hiểm duyệt.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, insuranceEvidenceUrls, insuranceApprovalNote, loadData]);

  const handleSendQuotationToCustomer = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      const note = String(sendEmailNote || "").trim();
      await serviceOrderApi.sendQuotationToCustomer(quoteId, {
        actorStaffId: staffId,
        note: note || "Gửi báo giá cho khách sau khi bảo hiểm đã duyệt",
      });
      setActionMessage("Đã gửi báo giá cho khách hàng.");
      setMailSentNotice("Gửi email báo giá thành công cho khách hàng.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể gửi báo giá cho khách hàng.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, sendEmailNote, loadData]);

  const handleUploadInsuranceEvidence = useCallback(async (file) => {
    if (!file) return;
    if (!staffId) return;
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId) return;

    setInsuranceEvidenceUploading(true);
    setInsuranceEvidenceUploadMessage("");
    setError("");

    try {
      const response = await serviceOrderApi.uploadQuotationInsuranceEvidence(quoteId, file);
      const uploadedUrl = String(response?.insuranceApprovalEvidenceUrl || response?.url || response?.Url || "").trim();
      if (!uploadedUrl) {
        throw new Error("Không nhận được URL ảnh sau khi upload.");
      }

      setInsuranceEvidenceUrls(prev => [...prev, uploadedUrl]);
      setInsuranceEvidenceUploadMessage("Đã tải ảnh lên thành công.");
    } catch (err) {
      setError(err?.message || "Không thể tải ảnh bằng chứng.");
      setInsuranceEvidenceUploadMessage("");
    } finally {
      setInsuranceEvidenceUploading(false);
    }
  }, [activeQuotation, staffId]);

  const handleExportQuotationExcel = useCallback(async () => {
    if (loading) return;
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId) return;
    setError("");
    setActionMessage("Đang chuẩn bị file Excel...");

    try {
      const { blob, fileName } = await serviceOrderApi.exportQuotationExcel(quoteId);

      let savedToDevice = false;
      if (typeof window.showSaveFilePicker === "function") {
        try {
          const fileHandle = await window.showSaveFilePicker({
            suggestedName: fileName,
            types: [
              {
                description: "Excel Workbook",
                accept: {
                  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": [".xlsx"],
                },
              },
            ],
          });
          const writable = await fileHandle.createWritable();
          await writable.write(blob);
          await writable.close();
          savedToDevice = true;
        } catch (saveErr) {
          if (saveErr?.name === "AbortError") {
            setActionMessage("Đã hủy chọn nơi lưu file.");
            return;
          } else {
            throw saveErr;
          }
        }
      }

      const objectUrl = URL.createObjectURL(blob);
      try {
        const link = document.createElement("a");
        link.href = objectUrl;
        link.download = fileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      } finally {
        setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
      }

      if (savedToDevice) {
        setActionMessage("Đã lưu file Excel vào thiết bị và tải file trên trình duyệt.");
      } else {
        setActionMessage("Đã tải file Excel trên trình duyệt.");
      }
    } catch (err) {
      if (err?.name === "AbortError") {
        setActionMessage("Đã hủy chọn nơi lưu file.");
        return;
      }

      setActionMessage("");
      setError(err?.message || "Không thể xuất Excel.");
    }
  }, [activeQuotation, loading]);

  const handleToggleCloneRemove = useCallback((detailId) => {
    setRemoveIdsForClone((prev) => {
      const row = quotationRowByDetailId.get(detailId);
      if (!row || !row.isMainService) {
        if (prev.includes(detailId)) return prev.filter((id) => id !== detailId);
        return [...prev, detailId];
      }

      const groupedPartIds = quotationDetailRows
        .filter((entry) => entry.groupKey === row.groupKey && !entry.isMainService)
        .map((entry) => entry.detailId)
        .filter((id) => Number.isFinite(id) && id > 0);
      const relatedIds = [detailId, ...groupedPartIds];

      if (prev.includes(detailId)) {
        return prev.filter((id) => !relatedIds.includes(id));
      }

      return [...new Set([...prev, ...relatedIds])];
    });
  }, [quotationDetailRows, quotationRowByDetailId]);

  const handleSplitInsuranceChange = useCallback((detailId, finalAmount, rawValue) => {
    const parsed = Number(String(rawValue || "").replace(/[^\d.-]/g, ""));
    const normalized = Number.isFinite(parsed) ? parsed : 0;
    const clamped = Math.min(Math.max(normalized, 0), Math.max(0, Number(finalAmount) || 0));
    setSplitBillingDraft((prev) => ({
      ...prev,
      [detailId]: clamped,
    }));
  }, []);

  const handleSaveSplitBilling = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    const details = Array.isArray(activeQuotation?.details) ? activeQuotation.details : [];
    const changedItems = details
      .map((row) => {
        const detailId = Number(readField(row, "id", "Id"));
        const finalAmount = Number(readField(row, "finalAmount", "FinalAmount") || 0);
        const originalInsurance = Number(
          readField(row, "insuranceCoveredAmount", "InsuranceCoveredAmount", "insuranceRequestedAmount", "InsuranceRequestedAmount") || 0,
        );
        const draftInsurance = Number(splitBillingDraft[detailId]);
        const safeDraft = Number.isFinite(draftInsurance)
          ? Math.min(Math.max(draftInsurance, 0), Math.max(0, finalAmount))
          : originalInsurance;
        const isChanged = Math.abs(safeDraft - originalInsurance) >= 0.01;
        return isChanged
          ? {
            quotationDetailId: detailId,
            insuranceCoveredAmount: safeDraft,
          }
          : null;
      })
      .filter(Boolean);

    if (changedItems.length === 0) {
      setActionMessage("Không có thay đổi tách bill để lưu.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");
    try {
      await serviceOrderApi.updateQuotationSplitBilling(quoteId, {
        actorStaffId: staffId,
        note: "Cập nhật phân bổ bảo hiểm/khách hàng",
        items: changedItems,
      });
      setActionMessage("Đã lưu tách bill và cập nhật tổng bảo hiểm/khách.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể lưu tách bill.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, splitBillingDraft, staffId, loadData]);

  const handleCloneQuotation = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;
    const cloneReason = String(cloneNote || "").trim();

    if (!cloneReason) {
      setError("Vui lòng nhập lý do trước khi tạo bản clone.");
      setActionMessage("");
      return;
    }

    const selectedMandatory = (Array.isArray(activeQuotation?.details) ? activeQuotation.details : []).some((row) => {
      const detailId = Number(readField(row, "id", "Id"));
      const isMandatory = Boolean(readField(row, "isMandatory", "IsMandatory"));
      return removeIdsForClone.includes(detailId) && isMandatory;
    });

    if (selectedMandatory && !showMandatoryCloneWarning) {
      setShowMandatoryCloneWarning(true);
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      const created = await serviceOrderApi.cloneQuotation(quoteId, {
        actorStaffId: staffId,
        note: cloneReason,
        removeQuotationDetailIds: removeIdsForClone,
      });

      const createdId = Number(readField(created, "id", "Id"));
      setActionMessage("Đã clone sang phiên bản mới.");
      setCloneNote("");
      setRemoveIdsForClone([]);
      setShowMandatoryCloneWarning(false);
      await loadData();
      if (createdId > 0) setActiveQuotationId(createdId);
    } catch (err) {
      setError(err?.message || "Không thể clone báo giá.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, cloneNote, removeIdsForClone, showMandatoryCloneWarning, loadData]);

  const handleCustomerDecision = useCallback(async (approved) => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.applyQuotationCustomerDecision(quoteId, {
        approved,
        actorStaffId: staffId,
        note: approved ? "Khách đồng ý báo giá" : "Khách từ chối báo giá",
      });

      setActionMessage(
        approved
          ? "Khách đã duyệt báo giá. Hệ thống sẽ đồng bộ vào SOD theo backend workflow."
          : "Khách từ chối báo giá. Bạn có thể clone sang phiên bản mới để đàm phán tiếp.",
      );
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể cập nhật quyết định khách hàng.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, loadData]);

  const handleCancelAll = useCallback(async () => {
    if (!numericOrderId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.requestDiagnosisCancel(numericOrderId, {
        requestedByStaffId: staffId,
        reason: cancelReason,
      });

      setActionMessage("Đã ghi nhận yêu cầu hủy toàn bộ dịch vụ.");
      setShowCancelModal(false);
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể gửi yêu cầu hủy toàn bộ.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, cancelReason, loadData]);

  const quotationStatus = normalizeStatus(readField(activeQuotation, "status", "Status"));
  const splitBillingLockedStatuses = new Set([
    "SUPERSEDED",
    "CANCELED",
    "INSURANCE_APPROVED",
    "PENDING_CUSTOMER",
    "CUSTOMER_APPROVED",
  ]);
  const canEditSplitBilling = Boolean(activeQuotation) && !splitBillingLockedStatuses.has(quotationStatus);
  const canSubmit = ["DRAFT"].includes(quotationStatus);
  const canConfirmInsuranceApproved = ["PENDING_INSURANCE"].includes(quotationStatus);
  const canSendToCustomer = ["INSURANCE_APPROVED", "PENDING_CUSTOMER"].includes(quotationStatus);
  const canCustomerDecision = ["PENDING_CUSTOMER"].includes(quotationStatus);
  const canClone = !["CUSTOMER_APPROVED", "CANCELED", "CANCELLED"].includes(quotationStatus);
  const evidenceUrlsValue = insuranceEvidenceUrls.join(", ");
  const allUrlsValid = insuranceEvidenceUrls.every(u => isValidHttpUrl(u));
  const evidenceUrlError = canConfirmInsuranceApproved && insuranceEvidenceUrls.length > 0 && !allUrlsValid;
  const hasCloneReason = String(cloneNote || "").trim().length > 0;

  const isSuperseded = quotationStatus === "SUPERSEDED";
  const customerDisplay = resolveCustomerDisplay(order, customerById);
  const plateDisplay = resolveVehiclePlateDisplay(order, vehicleById);
  const vehicleBrandModelDisplay = resolveVehicleBrandModelDisplay(order, vehicleById, vehicleBrandById, vehicleModelById);
  const vatSummary = useMemo(() => {
    const rows = Array.isArray(quotationDetailRows) ? quotationDetailRows : [];
    if (rows.length === 0) {
      const fallbackSubTotal = Math.max(0, Number(readField(activeQuotation, "subTotalAmount", "SubTotalAmount") || 0));
      const fallbackTax = Math.max(0, Number(readField(activeQuotation, "taxAmount", "TaxAmount") || 0));
      return {
        baseBeforeVat: fallbackSubTotal,
        totalVat: fallbackTax,
        totalWithVat: Math.max(0, Number(readField(activeQuotation, "finalAmount", "FinalAmount") || fallbackSubTotal + fallbackTax)),
        groups: [],
      };
    }

    const groups = new Map();
    let baseBeforeVat = 0;
    let totalVat = 0;

    rows.forEach((entry) => {
      const row = entry?.row || {};
      const status = normalizeStatus(readField(row, "status", "Status"));
      if (status === "CANCELED" || status === "CANCELLED") return;

      const quantity = Number(readField(row, "quantity", "Quantity") || 0);
      const unitPrice = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime") || 0);
      const subTotalRaw = Number(readField(row, "subTotal", "SubTotal"));
      const discountValue = Math.max(0, Number(readField(row, "discountValue", "DiscountValue") || 0));
      const taxRateRaw = Number(readField(row, "taxRate", "TaxRate") || 0);

      const subTotal = Number.isFinite(subTotalRaw) ? Math.max(0, subTotalRaw) : Math.max(0, quantity * unitPrice);
      const taxableBase = Math.max(0, subTotal - discountValue);
      const taxRate = Number.isFinite(taxRateRaw) && taxRateRaw > 0 ? taxRateRaw : 0;

      const taxAmountRaw = Number(readField(row, "taxAmount", "TaxAmount"));
      const taxAmount = Number.isFinite(taxAmountRaw)
        ? Math.max(0, taxAmountRaw)
        : Number(((taxableBase * taxRate) / 100).toFixed(2));

      baseBeforeVat += taxableBase;
      totalVat += taxAmount;

      const groupKey = taxRate.toFixed(4);
      if (!groups.has(groupKey)) {
        groups.set(groupKey, { taxRate, taxableBase: 0, taxAmount: 0 });
      }
      const group = groups.get(groupKey);
      group.taxableBase += taxableBase;
      group.taxAmount += taxAmount;
    });

    const normalizedGroups = [...groups.values()]
      .map((group) => ({
        taxRate: Number(group.taxRate.toFixed(4)),
        taxableBase: Number(group.taxableBase.toFixed(2)),
        taxAmount: Number(group.taxAmount.toFixed(2)),
      }))
      .sort((a, b) => a.taxRate - b.taxRate);

    const normalizedBase = Number(baseBeforeVat.toFixed(2));
    const normalizedVat = Number(totalVat.toFixed(2));
    return {
      baseBeforeVat: normalizedBase,
      totalVat: normalizedVat,
      totalWithVat: Number((normalizedBase + normalizedVat).toFixed(2)),
      groups: normalizedGroups,
    };
  }, [activeQuotation, quotationDetailRows]);
  const splitBillingSummary = useMemo(() => {
    if (!Array.isArray(quotationDetailRows) || quotationDetailRows.length === 0) {
      const dAmt = Number(readField(order, "insuranceDeductibleAmount", "InsuranceDeductibleAmount") || 0);
      const ins = Number(readField(activeQuotation, "totalInsuranceAmount", "TotalInsuranceAmount") || 0);
      const cust = Number(readField(activeQuotation, "totalCustomerAmount", "TotalCustomerAmount") || 0);
      return {
        insurance: ins,
        customer: cust,
        deductible: dAmt,
        totalCustomerPayable: cust + dAmt
      };
    }

    let insurance = 0;
    let customer = 0;
    const deductible = Number(readField(order, "insuranceDeductibleAmount", "InsuranceDeductibleAmount") || 0);

    quotationDetailRows.forEach((entry) => {
      const row = entry?.row || {};
      const detailId = Number(readField(row, "id", "Id"));
      if (!detailId) return;

      const quantity = Number(readField(row, "quantity", "Quantity") || 0);
      const unitPrice = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime") || 0);
      const finalAmount = Math.max(0, Number(readField(row, "finalAmount", "FinalAmount") || quantity * unitPrice));
      const insuranceRaw = Number(splitBillingDraft[detailId]);
      const safeInsurance = Number.isFinite(insuranceRaw)
        ? Math.min(Math.max(insuranceRaw, 0), finalAmount)
        : Math.max(0, finalAmount);
      insurance += safeInsurance;
      customer += Math.max(0, finalAmount - safeInsurance);
    });

    return {
      insurance,
      customer,
      deductible,
      totalCustomerPayable: customer + deductible
    };
  }, [activeQuotation, quotationDetailRows, splitBillingDraft, order]);
  const vatHeadlineLabel = useMemo(() => {
    if (!Array.isArray(vatSummary.groups) || vatSummary.groups.length === 0) return "Thuế VAT:";
    if (vatSummary.groups.length === 1) {
      return `Thuế VAT (${vatSummary.groups[0].taxRate.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%):`;
    }
    return "Thuế VAT (nhiều mức):";
  }, [vatSummary.groups]);
  const executionCostSummary = useMemo(() => {
    const rows = Array.isArray(quotationDetailRows) ? quotationDetailRows : [];
    let laborBeforeVat = 0;
    let partsAndMaterialBeforeVat = 0;

    rows.forEach((entry) => {
      const row = entry?.row || {};
      const status = normalizeStatus(readField(row, "status", "Status"));
      if (status === "CANCELED" || status === "CANCELLED") return;

      const quantity = Number(readField(row, "quantity", "Quantity") || 0);
      const unitPrice = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime") || 0);
      const subTotalRaw = Number(readField(row, "subTotal", "SubTotal"));
      const discountValue = Math.max(0, Number(readField(row, "discountValue", "DiscountValue") || 0));
      const subTotal = Number.isFinite(subTotalRaw) ? Math.max(0, subTotalRaw) : Math.max(0, quantity * unitPrice);
      const taxableBase = Math.max(0, subTotal - discountValue);
      const itemType = normalizeRecommendationItemType(readField(row, "itemType", "ItemType"));

      if (itemType === "SERVICE") laborBeforeVat += taxableBase;
      else partsAndMaterialBeforeVat += taxableBase;
    });

    return {
      laborBeforeVat: Number(laborBeforeVat.toFixed(2)),
      partsAndMaterialBeforeVat: Number(partsAndMaterialBeforeVat.toFixed(2)),
    };
  }, [quotationDetailRows]);
  const vatGroupsForSummary = useMemo(() => {
    if (Array.isArray(vatSummary.groups) && vatSummary.groups.length > 0) return vatSummary.groups;
    return [
      {
        taxRate: 0,
        taxableBase: vatSummary.baseBeforeVat,
        taxAmount: vatSummary.totalVat,
      },
    ];
  }, [vatSummary.baseBeforeVat, vatSummary.groups, vatSummary.totalVat]);

  const detailRowsByServiceType = useMemo(() => {
    const initialRows = [];
    const supplementaryRows = [];

    quotationDetailRows.forEach((entry) => {
      const detailId = toPositiveNumber(readField(entry?.row, "id", "Id"));
      const supplementaryMeta = detailId ? supplementaryMetaByQuotationDetailId.get(detailId) : null;
      const rowServiceSource = String(readField(entry?.row, "serviceSource", "ServiceSource") || "").trim().toUpperCase();
      const isSupplementary =
        Boolean(supplementaryMeta?.isSupplementary) ||
        rowServiceSource === "SUPPLEMENTARY_TECHNICIAN" ||
        rowServiceSource === "SUPPLEMENTARY_CUSTOMER" ||
        normalizeBooleanLike(readField(entry?.row, "isSupplementary", "IsSupplementary"));
      if (isSupplementary) supplementaryRows.push(entry);
      else initialRows.push(entry);
    });

    return { initialRows, supplementaryRows };
  }, [quotationDetailRows, supplementaryMetaByQuotationDetailId]);

  const buildDetailGroups = (rows) => {
    const groups = [];
    const groupByKey = new Map();

    rows.forEach((entry, index) => {
      const key = String(entry.groupKey ?? `group-${index}`);
      let group = groupByKey.get(key);
      if (!group) {
        group = { key, rows: [], serviceRow: null, partRows: [] };
        groupByKey.set(key, group);
        groups.push(group);
      }
      group.rows.push(entry);
      if (entry.isMainService && !group.serviceRow) {
        group.serviceRow = entry;
      } else {
        group.partRows.push(entry);
      }
    });

    groups.forEach((group) => {
      if (!group.serviceRow && group.rows.length > 0) {
        group.serviceRow = group.rows[0];
        group.partRows = group.rows.slice(1);
      }
    });

    return groups;
  };

  const renderDetailRows = (rows) => {
    const groups = buildDetailGroups(rows);
    return groups.flatMap((group, groupIndex) => {
      const groupKey = String(group.key);
      const inferredPartCount = group.partRows.length;
      const partCount = partCountByGroup.get(groupKey) || inferredPartCount;
      const canToggle = partCount > 0;
      const isCollapsed = canToggle ? Boolean(collapsedPartGroups[groupKey]) : false;
      const visibleRows = [group.serviceRow, ...(isCollapsed ? [] : group.partRows)].filter(Boolean);
      const isOpenGroup = canToggle && !isCollapsed;

      const groupRenderedRows = visibleRows.map((item, rowIndex) => {
        const row = item.row;
        const detailId = Number(readField(row, "id", "Id"));
        const qty = Number(readField(row, "quantity", "Quantity") || 1);
        const unitPrice = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime") || 0);
        const finalAmount = Number(readField(row, "finalAmount", "FinalAmount") || qty * unitPrice);
        const insuranceDraft = Number(splitBillingDraft[detailId]);
        const insuranceValue = Number.isFinite(insuranceDraft) ? insuranceDraft : finalAmount;
        const customerValue = Math.max(0, finalAmount - insuranceValue);
        const isGroupStart = rowIndex === 0;
        const isGroupEnd = rowIndex === visibleRows.length - 1;
        const serviceRowTooltip = item.isMainService
          ? (canToggle ? (isCollapsed ? SERVICE_ROW_TOOLTIP_EXPAND : SERVICE_ROW_TOOLTIP_COLLAPSE) : SERVICE_ROW_TOOLTIP_STATIC)
          : "";

        return (
          <tr
            key={detailId}
            className={[
              "group-row",
              item.isMainService ? "group-service-row clickable-service-row" : "group-part-row",
              isGroupStart ? "group-start" : "group-middle",
              isGroupEnd ? "group-end" : "",
              isOpenGroup ? "group-open" : "group-closed",
              hoveredGroupKey === groupKey ? "group-hover" : "",
            ].join(" ")}
            title={serviceRowTooltip}
            onClick={
              item.isMainService && canToggle
                ? () => setCollapsedPartGroups((p) => ({ ...p, [groupKey]: !p[groupKey] }))
                : undefined
            }
            onMouseEnter={() => setHoveredGroupKey(groupKey)}
            onMouseLeave={() => setHoveredGroupKey((prev) => (prev === groupKey ? null : prev))}
          >
            <td className="col-name" style={{ textAlign: "left" }}>
              {item.isMainService ? (
                <div className="service-name-cell" style={{ fontWeight: 700 }}>
                  {item.itemName}
                </div>
              ) : (
                <div className="part-name-cell" style={{ paddingLeft: 20, color: "#475569" }}>
                  <span style={{ opacity: 0.3, marginRight: 8 }}>└─</span> {item.itemName}
                </div>
              )}
            </td>
            <td className="col-priority">
              <span className={`priority-badge priority-${item.urgency || "NORMAL"}`}>
                {item.priorityLabel || "Bình thường"}
              </span>
            </td>
            <td className="text-right col-qty">{qty}</td>
            <td className="text-right col-money">{formatMoney(unitPrice)}</td>
            <td className="text-right col-money" style={{ fontWeight: 700 }}>{formatMoney(finalAmount)}</td>
            <td className="col-money">
              <input
                type="number"
                className="split-input"
                value={insuranceValue}
                onChange={(e) => handleSplitInsuranceChange(detailId, finalAmount, e.target.value)}
                disabled={!canEditSplitBilling}
                onClick={(e) => e.stopPropagation()}
              />
            </td>
            <td className="text-right" style={{ fontWeight: 700, color: "var(--quota-navy)" }}>{formatMoney(customerValue)}</td>
            <td className="col-clone-cell">
              <input
                type="checkbox"
                checked={removeIdsForClone.includes(detailId)}
                onChange={() => handleToggleCloneRemove(detailId)}
                disabled={!canClone}
                onClick={(e) => e.stopPropagation()}
              />
            </td>
          </tr>
        );
      });

      if (groupIndex < groups.length - 1) {
        groupRenderedRows.push(
          <tr key={`spacer-${groupKey}`} className="group-spacer-row">
            <td colSpan={8} />
          </tr>,
        );
      }

      return groupRenderedRows;
    });
  };

  return (
    <div className="reception-quote-page">
      <header className="reception-quote-head">
        <div>
          <h2 className="reception-quote-title">Trung tâm Báo giá Thương mại</h2>
          <div className="reception-quote-subtitle">
            <Clock size={14} style={{ marginRight: 4 }} />
            Mã lệnh: <strong>{readField(order, "code", "Code") || `#${orderId}`}</strong>
          </div>
        </div>
        <button type="button" className="btn-back-premium" onClick={() => navigate("/workshop/reception")}>
          <ChevronRight size={18} style={{ transform: "rotate(180deg)" }} />
          Quay lại danh sách
        </button>
      </header>

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang tải dữ liệu báo giá...</p>
        </div>
      ) : (
        <div className="reception-quote-layout">
          <QuoteStepper status={quotationStatus} />

          <div className="quote-info-grid">
            {/* Row 1 */}
            <QuoteInfoCard
              icon={<User size={24} />}
              label="Khách hàng"
              value={customerDisplay}
            />
            <QuoteInfoCard
              icon={<Car size={24} />}
              label="Thông tin xe"
              value={plateDisplay}
              subValue={vehicleBrandModelDisplay}
            />
            <QuoteInfoCard
              icon={<Activity size={24} />}
              label="Trạng thái lệnh"
              value={<span className="status-badge-inline">{getStatusLabelVi(order?.status, "")}</span>}
            />
            <QuoteInfoCard
              icon={<FileText size={24} />}
              label="Trạng thái báo giá"
              value={<span className="status-badge-inline">{getStatusLabelVi(quotationStatus, "")}</span>}
            />

            {/* Row 2 */}
            <QuoteInfoCard
              icon={<DollarSign size={24} />}
              label="Tổng giá trị báo giá"
              value={`${formatMoney(vatSummary.totalWithVat)} đ`}
            />
            <QuoteInfoCard
              icon={<ShieldCheck size={24} />}
              label="Bảo hiểm trả"
              value={`${formatMoney(readField(activeQuotation, "totalInsuranceAmount", "TotalInsuranceAmount"))} đ`}
            />
            <QuoteInfoCard
              icon={<Wallet size={24} />}
              label="Miễn thường"
              value={`${formatMoney(readField(order, "insuranceDeductibleAmount", "InsuranceDeductibleAmount"))} đ`}
            />
            <QuoteInfoCard
              icon={<CreditCard size={24} />}
              label="Khách phải trả"
              value={`${formatMoney(splitBillingSummary.totalCustomerPayable)} đ`}
            />
          </div>

          <div className="quote-main-content">
            <aside className="version-timeline-card">
              <h3>Lịch sử phiên bản</h3>
              <div className="version-list">
                {quotations.length === 0 ? (
                  <button type="button" className="btn-quota btn-quota-primary" onClick={handleCreateDraftQuotation} disabled={saving || !staffId}>
                    <Plus size={18} /> Tạo báo giá V1
                  </button>
                ) : (
                  quotations.map((row) => {
                    const id = Number(readField(row, "id", "Id"));
                    const versionNo = Number(readField(row, "versionNo", "VersionNo") || 0);
                    const status = normalizeStatus(readField(row, "status", "Status"));
                    const isActive = id === Number(activeQuotationId);

                    return (
                      <button
                        key={id}
                        type="button"
                        className={`version-btn ${isActive ? "active" : ""}`}
                        onClick={() => setActiveQuotationId(id)}
                      >
                        <div className="version-btn-body">
                          <span className="version-name">Phiên bản V{versionNo}</span>
                          <div className="version-status-line">
                            <span className="version-status-text">{getStatusLabelVi(status, status)}</span>
                          </div>
                          <span className="version-date">{formatDateTime(readField(row, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt"))}</span>
                        </div>
                      </button>
                    );
                  })
                )}
              </div>
            </aside>

            <main>
              {activeQuotation && (
                <section className="action-console-premium">
                  <div className="action-console-head">
                    <h3 style={{ margin: 0, fontWeight: 800 }}>Bảng điều khiển nghiệp vụ</h3>
                    <button
                      type="button"
                      className="btn-quota btn-quota-outline"
                      style={{ color: "var(--quota-danger)", borderColor: "#fee2e2" }}
                      onClick={() => setShowCancelModal(true)}
                      disabled={saving}
                    >
                      <Trash2 size={16} /> Hủy toàn bộ phiếu
                    </button>
                  </div>
                  {actionMessage ? (
                    <span className="reception-quote-note">
                      <Info size={14} />
                      <span>{actionMessage}</span>
                    </span>
                  ) : null}
                  {error ? (
                    <div className="reception-quote-error-banner">
                      <AlertCircle size={16} /> {error}
                    </div>
                  ) : null}

                  <div className="action-group" style={{ marginBottom: 12 }}>
                    <button type="button" className="btn-quota btn-quota-outline" onClick={handleExportQuotationExcel} disabled={loading || !activeQuotation}>
                      <Download size={16} /> Xuất Excel
                    </button>
                    <button type="button" className="btn-quota btn-quota-outline" onClick={handleSaveSplitBilling} disabled={saving || !canEditSplitBilling}>
                      <Save size={16} /> Lưu tách bill
                    </button>
                  </div>

                  <div className="quote-action-tabs">
                    <button
                      className={`action-tab-header ${activeActionTab === 'insurance' ? 'active' : ''}`}
                      onClick={() => {
                        tabSelectionLockedRef.current = true;
                        setActiveActionTab('insurance');
                      }}
                    >
                      <ShieldCheck size={16} /> Nghiệp vụ Bảo hiểm
                    </button>
                    <button
                      className={`action-tab-header ${activeActionTab === 'customer' ? 'active' : ''}`}
                      onClick={() => {
                        tabSelectionLockedRef.current = true;
                        setActiveActionTab('customer');
                      }}
                    >
                      <User size={16} /> Gửi khách hàng
                    </button>
                    <button
                      className={`action-tab-header ${activeActionTab === 'clone' ? 'active' : ''}`}
                      onClick={() => {
                        tabSelectionLockedRef.current = true;
                        setActiveActionTab('clone');
                      }}
                    >
                      <Plus size={16} /> Quản lý phiên bản
                    </button>
                  </div>

                  <div className="action-tab-content">
                    {activeActionTab === 'insurance' && (
                      <div className="tab-pane-fade-in">
                        <div className="input-field-group" style={{ marginBottom: 16 }}>
                          <label className="so-detail-card-label" style={{ fontWeight: 800 }}>Bằng chứng duyệt bảo hiểm (Ảnh/Chụp màn hình)</label>
                          <div className="upload-container-professional">
                            <input
                              type="file"
                              id="evidence-upload"
                              hidden
                              onChange={(e) => handleUploadInsuranceEvidence(e.target.files[0])}
                              disabled={insuranceEvidenceUploading || !canConfirmInsuranceApproved}
                            />
                            <div style={{ display: "flex", flexWrap: "wrap", gap: 12, alignItems: "center" }}>
                              <label htmlFor="evidence-upload" className={`btn-upload-compact ${(!canConfirmInsuranceApproved || insuranceEvidenceUploading) ? 'disabled' : ''}`}>
                                {insuranceEvidenceUploading ? <RefreshCw size={16} className="reception-quote-spin" /> : <><Upload size={16} /> Tải thêm ảnh</>}
                              </label>
                              {insuranceEvidenceUrls.map((url, index) => (
                                <div key={index} className="uploaded-photo-card" onClick={() => window.open(url, '_blank')}>
                                  <div className="photo-card-icon"><ImageIcon size={18} /></div>
                                  <div className="photo-card-info"><span className="photo-card-name">Nhấn để xem ảnh</span></div>
                                  <button className="photo-card-remove" onClick={(e) => { e.stopPropagation(); setInsuranceEvidenceUrls(prev => prev.filter((_, i) => i !== index)); }} disabled={!canConfirmInsuranceApproved}><Trash2 size={16} /></button>
                                </div>
                              ))}
                            </div>
                          </div>
                        </div>
                        <div className="input-field-group">
                          <label className="so-detail-card-label" style={{ fontWeight: 800 }}>Ghi chú làm việc với Bảo hiểm</label>
                          <textarea
                            className="reception-quote-approval-note-input"
                            rows={3}
                            value={insuranceApprovalNote}
                            onChange={(e) => setInsuranceApprovalNote(e.target.value)}
                            placeholder="Nhập ghi chú chi tiết về việc làm việc với bảo hiểm..."
                          />
                        </div>
                        <div className="action-group-tabbed" style={{ marginTop: 16 }}>
                          <button type="button" className="btn-quota btn-quota-primary" onClick={handleSubmitQuotation} disabled={saving || !canSubmit}>
                            <ShieldCheck size={16} /> Chuyển chờ bảo hiểm
                          </button>
                          <button type="button" className="btn-quota btn-quota-primary" onClick={handleConfirmInsuranceApproval} disabled={saving || !canConfirmInsuranceApproved || !allUrlsValid}>
                            <Check size={16} /> Xác nhận bảo hiểm duyệt
                          </button>
                        </div>
                      </div>
                    )}

                    {activeActionTab === 'customer' && (
                      <div className="tab-pane-fade-in">
                        <div className="input-field-group">
                          <label className="so-detail-card-label" style={{ fontWeight: 800 }}>Ghi chú gửi cho khách hàng</label>
                          <textarea
                            className="reception-quote-approval-note-input"
                            rows={3}
                            value={sendEmailNote}
                            onChange={(e) => setSendEmailNote(e.target.value)}
                            placeholder="Thông điệp gửi kèm khi gửi email báo giá cho khách hàng..."
                          />
                          {mailSentNotice && <div className="reception-quote-success" style={{ marginTop: 8 }}><Check size={14} /> {mailSentNotice}</div>}
                        </div>
                        <div className="action-group-tabbed" style={{ marginTop: 16 }}>
                          <button type="button" className="btn-quota btn-quota-primary" onClick={handleSendQuotationToCustomer} disabled={saving || !canSendToCustomer}>
                            <Mail size={16} /> Gửi báo giá cho khách
                          </button>
                        </div>
                      </div>
                    )}

                    {activeActionTab === 'clone' && (
                      <div className="tab-pane-fade-in">
                        <div className="input-field-group">
                          <label className="so-detail-card-label" style={{ fontWeight: 800 }}>Lý do tạo bản sao mới</label>
                          <div style={{ position: "relative" }}>
                            <textarea
                              className={`reception-quote-approval-note-input ${!hasCloneReason ? "border-danger" : ""}`}
                              rows={3}
                              value={cloneNote}
                              onChange={(e) => setCloneNote(e.target.value)}
                              placeholder="Giải trình lý do tại sao cần nhân bản báo giá này (Bắt buộc)..."
                            />
                            {!hasCloneReason && (
                              <small className="reception-quote-error" style={{ position: "absolute", bottom: -20, left: 0 }}>
                                * Bắt buộc khi clone
                              </small>
                            )}
                          </div>
                        </div>
                        <div className="action-group-tabbed" style={{ marginTop: 24 }}>
                          <button type="button" className="btn-quota btn-quota-outline" onClick={handleCloneQuotation} disabled={saving || !canClone || !hasCloneReason}>
                            <Plus size={16} /> Tạo bản mới (Sao chép)
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                </section>
              )}
            </main>

            {activeQuotation && (
              <>
                <section className="quote-table-container">
                  <div className="action-console-head" style={{ marginBottom: 15 }}>
                    <h3 style={{ margin: 0, fontWeight: 800 }}>Chi tiết hạng mục báo giá</h3>
                  </div>

                  <div className="premium-table-wrapper">
                    <div className="quote-detail-section">
                      <div className="quote-detail-section-title">Dịch vụ ban đầu</div>
                      <table className="premium-table">
                        <thead>
                          <tr>
                            <th className="col-name" style={{ textAlign: "left" }}>Hạng mục công việc / Vật tư</th>
                            <th className="col-priority">Độ ưu tiên</th>
                            <th className="text-right col-qty">SL</th>
                            <th className="text-right col-money">Đơn giá</th>
                            <th className="text-right col-money">Thành tiền</th>
                            <th className="text-right" style={{ width: 140 }}>Bảo hiểm đền</th>
                            <th className="text-right col-money">Khách bù</th>
                            <th className="col-clone-header" style={{ width: 140, fontSize: 11, color: "#64748b", textAlign: "center" }}>Loại khỏi bản mới</th>
                          </tr>
                        </thead>
                        <tbody>
                          {detailRowsByServiceType.initialRows.length > 0 ? (
                            renderDetailRows(detailRowsByServiceType.initialRows)
                          ) : (
                            <tr>
                              <td colSpan={8} className="quote-detail-empty">Không có dịch vụ ban đầu.</td>
                            </tr>
                          )}
                        </tbody>
                      </table>
                    </div>

                    {detailRowsByServiceType.supplementaryRows.length > 0 ? (
                      <div className="quote-detail-section quote-detail-section-supplementary">
                        <div className="quote-detail-section-title">Dịch vụ phát sinh</div>
                        <table className="premium-table">
                          <thead>
                            <tr>
                              <th className="col-name" style={{ textAlign: "left" }}>Hạng mục công việc / Vật tư</th>
                              <th className="col-priority">Độ ưu tiên</th>
                              <th className="text-right col-qty">SL</th>
                              <th className="text-right col-money">Đơn giá</th>
                              <th className="text-right col-money">Thành tiền</th>
                              <th className="text-right" style={{ width: 140 }}>Bảo hiểm đền</th>
                              <th className="text-right col-money">Khách bù</th>
                              <th className="col-clone-header" style={{ width: 140, fontSize: 11, color: "#64748b", textAlign: "center" }}>Loại khỏi bản mới</th>
                            </tr>
                          </thead>
                          <tbody>
                            {renderDetailRows(detailRowsByServiceType.supplementaryRows)}
                          </tbody>
                        </table>
                      </div>
                    ) : null}
                  </div>

                  <div className="quote-summary-footer">
                    <div className="summary-details">
                      <h3 className="summary-title">TỔNG KẾT CHI PHÍ SỬA CHỮA / QUOTATION SUMMARY</h3>

                      <div className="summary-subtitle">Giá trị trước thuế (Chưa VAT):</div>
                      <div className="summary-line">
                        <span className="summary-line-label">
                          <span className="summary-marker marker-labor" />
                          Tổng chi phí Nhân công
                        </span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">{formatMoney(executionCostSummary.laborBeforeVat)} đ</span>
                      </div>
                      <div className="summary-line">
                        <span className="summary-line-label">
                          <span className="summary-marker marker-parts" />
                          Tổng chi phí Phụ tùng / Vật tư
                        </span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">{formatMoney(executionCostSummary.partsAndMaterialBeforeVat)} đ</span>
                      </div>
                      <div className="summary-line summary-line-strong">
                        <span className="summary-line-label">TỔNG TIỀN HÀNG (CHƯA VAT)</span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">{formatMoney(vatSummary.baseBeforeVat)} đ</span>
                      </div>

                      <div className="summary-subtitle">{vatHeadlineLabel}</div>
                      {vatGroupsForSummary.map((group) => (
                        <div key={`vat-breakdown-${group.taxRate}`} className="summary-line">
                          <span className="summary-line-label">
                            <span className="summary-marker marker-vat" />
                            Thuế giá trị gia tăng ({group.taxRate.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%)
                          </span>
                          <span className="summary-line-dots" />
                          <span className="summary-line-value">{formatMoney(group.taxAmount)} đ</span>
                        </div>
                      ))}
                      <div className="summary-line summary-line-strong">
                        <span className="summary-line-label">TỔNG CHI PHÍ DỊCH VỤ & PHỤ TÙNG</span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">{formatMoney(vatSummary.totalWithVat)} đ</span>
                      </div>

                      <div className="summary-divider" />

                      <div className="summary-subtitle">Phân bổ thanh toán (Billing Split):</div>
                      <div className="summary-line">
                        <span className="summary-line-label">
                          <span className="summary-marker marker-insurance" />
                          (-) Bảo hiểm phê duyệt / Insurance Cover
                        </span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value text-insurance">-{formatMoney(splitBillingSummary.insurance)} đ</span>
                      </div>
                      <div className="summary-line">
                        <span className="summary-line-label">
                          <span className="summary-marker marker-deductible" />
                          (+) Phí Miễn thường / Chế tài / Deductible
                        </span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value">+{formatMoney(splitBillingSummary.deductible)} đ</span>
                      </div>

                      <div className="summary-divider" />

                      <div className="summary-line payable-row">
                        <span className="summary-line-label">TỔNG CỘNG KHÁCH HÀNG THANH TOÁN (PAYABLE)</span>
                        <span className="summary-line-dots" />
                        <span className="summary-line-value final-payable">{formatMoney(splitBillingSummary.totalCustomerPayable)} đ</span>
                      </div>
                    </div>
                  </div>
                </section>
              </>
            )}
          </div>
        </div>
      )}

      {/* Cancel Confirmation Modal */}
      {showCancelModal && (
        <div className="modal-overlay">
          <div className="modal-box" style={{ maxWidth: 450 }}>
            <div className="modal-header">
              <h2 style={{ display: "flex", alignItems: "center", gap: 10, margin: 0, fontSize: 18 }}>
                <AlertTriangle color="var(--quota-danger)" size={20} /> Xác nhận hủy báo giá
              </h2>
            </div>
            <div className="modal-body-content" style={{ padding: 20 }}>
              <p style={{ margin: 0, color: "#475569" }}>Bạn có chắc chắn muốn hủy toàn bộ các hạng mục trong lệnh sửa chữa này không? Hành động này không thể hoàn tác.</p>

              <div style={{ marginTop: 15 }}>
                <label className="so-detail-card-label" style={{ fontWeight: 800 }}>Lý do hủy hồ sơ</label>
                <select
                  className="reception-quote-approval-note-input"
                  style={{ marginTop: 5, width: "100%", height: 40 }}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                >
                  <option value="Khách vội">Khách vội</option>
                  <option value="Chê đắt">Chê đắt</option>
                  <option value="Đổi ý">Đổi ý</option>
                  <option value="Có việc gấp">Có việc gấp</option>
                  <option value="Khác">Lý do khác...</option>
                </select>
              </div>

              <div style={{ display: "flex", gap: 10, marginTop: 24, justifyContent: "flex-end" }}>
                <button type="button" className="btn-quota btn-quota-outline" onClick={() => setShowCancelModal(false)}>Hủy bỏ</button>
                <button type="button" className="btn-quota btn-quota-primary" style={{ background: "var(--quota-danger)", color: "white" }} onClick={handleCancelAll}>Xác nhận hủy</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}


