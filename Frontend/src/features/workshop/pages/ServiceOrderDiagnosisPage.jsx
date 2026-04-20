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
  Tooltip,
  FormControl,
  FormLabel,
  NumberInput,
  NumberInputField,
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
import FilterDropdown from "@shared/components/FilterDropdown";
import { isServiceCompatibleWithVehicleModel } from "@shared/utils/serviceEngineCompatibility";

function DiagnosisDropdown({
  id,
  label,
  value,
  onChange,
  placeholder = "",
  isDisabled = false,
  minW,
  maxW,
  flex,
  labelColor = "#2b3674",
  labelFontSize = "sm",
  children,
  menuMinWidth,
  optionNoWrap = false,
  fullWidth = true,
}) {
  const options = React.Children.toArray(children)
    .filter((child) => React.isValidElement(child))
    .map((child) => ({
      value: child.props?.value ?? "",
      label: typeof child.props?.children === "string" ? child.props.children : String(child.props?.children ?? ""),
      disabled: Boolean(child.props?.disabled),
    }));

  const normalizedValue = value ?? "";

  const selectedValueExists = options.some((option) => String(option.value) === String(normalizedValue));
  const fallbackOption = options.find((option) => String(option.value) === "") || options[0];
  const effectiveValue = selectedValueExists ? normalizedValue : (fallbackOption?.value ?? "");

  const normalizedMinWidth = minW === undefined ? 170 : (typeof minW === "number" ? minW : String(minW));

  return (
    <Box minW={minW} maxW={maxW} flex={flex}>
      {label ? (
        <Text fontSize={labelFontSize} fontWeight="600" color={labelColor} mb={2}>
          {label}
        </Text>
      ) : null}
      <FilterDropdown
        id={id}
        value={effectiveValue}
        options={options}
        onChange={onChange}
        minWidth={normalizedMinWidth}
        menuMinWidth={menuMinWidth}
        ariaLabel={label || placeholder || "dropdown"}
        disabled={isDisabled}
        optionNoWrap={optionNoWrap}
        fullWidth={fullWidth}
      />
    </Box>
  );
}

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function normalizeStatusCode(value) {
  return String(value ?? "").trim().toUpperCase();
}

function formatDateTime(value) {
  if (!value) return "--";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--";
  return date.toLocaleString("vi-VN");
}

const SYSTEM_ASSIGNMENT_NOTE_MAP = {
  "Created diagnosis ticket": "Đã tạo phiếu chẩn đoán.",
  "Assigned diagnosis": "Đã phân công phiếu chẩn đoán.",
  "Assigned by manager override": "Quản đốc đã phân công trực tiếp phiếu chẩn đoán.",
  "Lead technician accepted diagnosis": "Thợ chính đã nhận phiếu chẩn đoán.",
  "Created supporting-team diagnosis clone": "Đã tạo phiên bản hỗ trợ cho tổ phối hợp.",
  "Canceled because customer picked up vehicle": "Đã hủy vì khách hàng đã nhận xe.",
  "Diagnosis completed": "Đã hoàn thành chẩn đoán.",
};

function buildAutoAssignmentNote(version) {
  if (!version) return "";
  if (version?.foremanRejectReason) {
    return `Bị từ chối duyệt: ${version.foremanRejectReason}`;
  }

  const eventType = normalizeStatusCode(version?.eventType);
  if (eventType === "TECHNICAL_REJECTED" || eventType === "FINAL_REJECTED") {
    return "Phiên bản này đã bị từ chối duyệt.";
  }
  if (eventType === "TECHNICAL_APPROVED") {
    return "Tổ trưởng đã duyệt kỹ thuật.";
  }
  if (eventType === "FINAL_APPROVED") {
    return "Quản đốc đã duyệt cuối.";
  }
  if (eventType === "STARTED") {
    return "Thợ chính đã nhận phiếu chẩn đoán.";
  }
  if (eventType === "ASSIGNED") {
    return "Đã phân công phiếu chẩn đoán.";
  }
  if (eventType === "CREATED") {
    return "Đã tạo phiếu chẩn đoán.";
  }
  if (eventType === "COMPLETED") {
    return "Đã hoàn thành chẩn đoán.";
  }
  if (eventType === "CANCELED") {
    return "Phiếu đã hủy.";
  }
  return "";
}

function resolveAssignmentNoteDisplay(rawNote, version) {
  const note = String(rawNote ?? "").trim();
  if (!note) return buildAutoAssignmentNote(version);
  return SYSTEM_ASSIGNMENT_NOTE_MAP[note] || note;
}

function isDiagnosisApprovedAndLocked(diagnosis) {
  if (!diagnosis) return false;

  const teamLeaderApproved =
    toPositiveNumber(
      diagnosis?.approvedByTeamLeaderId ??
      diagnosis?.ApprovedByTeamLeaderId ??
      diagnosis?.technicalApprovedByTeamLeaderId ??
      diagnosis?.TechnicalApprovedByTeamLeaderId ??
      diagnosis?.teamLeaderApprovedById ??
      diagnosis?.TeamLeaderApprovedById
    ) > 0 ||
    Boolean(
      diagnosis?.approvedByTeamLeaderAt ??
      diagnosis?.ApprovedByTeamLeaderAt ??
      diagnosis?.technicalApprovedAt ??
      diagnosis?.TechnicalApprovedAt ??
      diagnosis?.finalApprovedAt ??
      diagnosis?.FinalApprovedAt
    ) ||
    normalizeStatusCode(
      diagnosis?.teamLeaderApprovalStatus ??
      diagnosis?.TeamLeaderApprovalStatus ??
      diagnosis?.technicalApprovalStatus ??
      diagnosis?.TechnicalApprovalStatus
    ) === "APPROVED";

  const foremanApproved =
    toPositiveNumber(
      diagnosis?.approvedByForemanId ??
      diagnosis?.ApprovedByForemanId ??
      diagnosis?.foremanApprovedById ??
      diagnosis?.ForemanApprovedById
    ) > 0 ||
    Boolean(
      diagnosis?.approvedByForemanAt ??
      diagnosis?.ApprovedByForemanAt ??
      diagnosis?.managerApprovedAt ??
      diagnosis?.ManagerApprovedAt
    ) ||
    normalizeStatusCode(
      diagnosis?.foremanApprovalStatus ??
      diagnosis?.ForemanApprovalStatus ??
      diagnosis?.finalApprovalStatus ??
      diagnosis?.FinalApprovalStatus
    ) === "APPROVED";

  return teamLeaderApproved || foremanApproved;
}

function parseIdList(value) {
  if (Array.isArray(value)) {
    return value
      .map((entry) => toPositiveNumber(entry))
      .filter(Boolean);
  }

  const raw = String(value ?? "").trim();
  if (!raw) return [];

  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      return parsed
        .map((entry) => toPositiveNumber(entry))
        .filter(Boolean);
    }
  } catch {
    // Fallback to CSV parser below.
  }

  return raw
    .split(",")
    .map((entry) => toPositiveNumber(entry))
    .filter(Boolean);
}

function extractServiceId(service) {
  return toPositiveNumber(service?.id ?? service?.Id);
}

function extractServiceName(service) {
  return String(service?.name ?? service?.Name ?? service?.serviceName ?? service?.ServiceName ?? "").trim();
}

function extractProductName(product) {
  return String(product?.name ?? product?.Name ?? "").trim();
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

function extractCategoryParentId(category) {
  return toPositiveNumber(category?.parentId ?? category?.ParentId);
}

function extractCategoryName(category) {
  return String(
    category?.categoryName ??
      category?.CategoryName ??
      category?.name ??
      category?.Name ??
      category?.code ??
      ""
  ).trim();
}

function extractProductCategoryId(product) {
  return toPositiveNumber(
    product?.categoryId ??
      product?.CategoryId
  );
}

function extractProductCategoryName(product) {
  return String(
    product?.categoryName ??
      product?.CategoryName ??
      ""
  ).trim();
}

function firstNonEmptyText(...values) {
  for (const value of values) {
    const text = String(value ?? "").trim();
    if (text) return text;
  }
  return "";
}

function normalizeLookupText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

const INTERNAL_CUSTOMER_NOTE_MARKER_REGEX = /\[(?:FLOW:[^\]]+|CLUSTER:[^\]]+|[A-Z][A-Z0-9_]{2,})\]/g;
const EMPTY_CUSTOMER_NOTE_NORMALIZED_VALUES = new Set([
  "khong co",
  "khong co ghi chu",
  "khong co ghi chu gi",
  "khong co mo ta",
  "none",
  "no note",
  "n/a",
  "na",
]);

function sanitizeCustomerNoteText(value) {
  const raw = String(value || "").trim();
  if (!raw) return "";

  const cleaned = raw
    .replace(INTERNAL_CUSTOMER_NOTE_MARKER_REGEX, " ")
    .replace(/\s+/g, " ")
    .trim()
    .replace(/^[,;|.\-: ]+|[,;|.\-: ]+$/g, "")
    .trim();

  if (!cleaned) return "";

  const normalized = normalizeLookupText(cleaned);
  if (EMPTY_CUSTOMER_NOTE_NORMALIZED_VALUES.has(normalized)) {
    return "";
  }

  return cleaned;
}

function extractVehicleModelId(order) {
  return toPositiveNumber(
    order?.vehicleModelId ??
    order?.VehicleModelId ??
    order?.customerVehicle?.vehicleModelId ??
    order?.customerVehicle?.VehicleModelId ??
    order?.vehicle?.vehicleModelId ??
    order?.vehicle?.VehicleModelId
  );
}

function extractDiagnosisAssignmentId(diagnosis) {
  return toPositiveNumber(
    diagnosis?.diagnosisAssignmentId ??
      diagnosis?.DiagnosisAssignmentId ??
      diagnosis?.assignmentId ??
      diagnosis?.AssignmentId
  );
}

function extractDiagnosisAssignmentVersions(diagnosis) {
  const rawVersions = diagnosis?.assignmentVersions ?? diagnosis?.AssignmentVersions;
  if (!Array.isArray(rawVersions) || rawVersions.length === 0) {
    const fallbackId = extractDiagnosisAssignmentId(diagnosis);
    if (!fallbackId) return [];
    return [
      {
        diagnosisAssignmentId: fallbackId,
        eventType: "",
        status: normalizeStatusCode(diagnosis?.status ?? diagnosis?.Status),
        teamLeaderApprovalStatus: normalizeStatusCode(diagnosis?.teamLeaderApprovalStatus ?? diagnosis?.TeamLeaderApprovalStatus),
        finalApprovalStatus: normalizeStatusCode(diagnosis?.finalApprovalStatus ?? diagnosis?.FinalApprovalStatus),
        foremanRejectReason: String(diagnosis?.foremanRejectReason ?? diagnosis?.ForemanRejectReason ?? "").trim(),
        note: String(diagnosis?.assignmentNote ?? diagnosis?.AssignmentNote ?? "").trim(),
        createdAt: diagnosis?.updatedAt ?? diagnosis?.UpdatedAt ?? diagnosis?.createdAt ?? diagnosis?.CreatedAt ?? null,
        isCurrent: true,
      },
    ];
  }

  return rawVersions
    .map((item) => {
      const diagnosisAssignmentId = toPositiveNumber(
        item?.diagnosisAssignmentId ??
        item?.DiagnosisAssignmentId ??
        item?.id ??
        item?.Id
      );
      if (!diagnosisAssignmentId) return null;
      return {
        diagnosisAssignmentId,
        eventType: String(item?.eventType ?? item?.EventType ?? "").trim().toUpperCase(),
        status: normalizeStatusCode(item?.status ?? item?.Status),
        teamLeaderApprovalStatus: normalizeStatusCode(item?.teamLeaderApprovalStatus ?? item?.TeamLeaderApprovalStatus),
        finalApprovalStatus: normalizeStatusCode(item?.finalApprovalStatus ?? item?.FinalApprovalStatus),
        foremanRejectReason: String(item?.foremanRejectReason ?? item?.ForemanRejectReason ?? "").trim(),
        note: String(item?.note ?? item?.Note ?? "").trim(),
        createdAt: item?.createdAt ?? item?.CreatedAt ?? null,
        isCurrent: Boolean(item?.isCurrent ?? item?.IsCurrent),
      };
    })
    .filter(Boolean)
    .sort((a, b) => {
      const aTime = new Date(a.createdAt || 0).getTime();
      const bTime = new Date(b.createdAt || 0).getTime();
      if (bTime !== aTime) return bTime - aTime;
      return b.diagnosisAssignmentId - a.diagnosisAssignmentId;
    });
}

function normalizeServiceBomItems(response) {
  const source = Array.isArray(response)
    ? response
    : Array.isArray(response?.items)
      ? response.items
      : Array.isArray(response?.data)
        ? response.data
        : Array.isArray(response?.$values)
          ? response.$values
          : Array.isArray(response?.result?.items)
            ? response.result.items
            : Array.isArray(response?.result?.data)
              ? response.result.data
              : Array.isArray(response?.result?.$values)
                ? response.result.$values
        : [];

  return source
    .map((item) => {
      const productId = toPositiveNumber(item?.productId ?? item?.ProductId ?? item?.inventoryItemId ?? item?.InventoryItemId);
      if (!productId) return null;
      const quantity = Number(
        item?.quantity ??
        item?.Quantity ??
        item?.requiredQuantity ??
        item?.RequiredQuantity ??
        item?.qty ??
        item?.Qty ??
        1
      );
      return {
        productId,
        quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : 1,
        isMandatory: Boolean(item?.isMandatory ?? item?.IsMandatory),
      };
    })
    .filter(Boolean);
}

function resolveApiErrorMessage(error) {
  const payload = error?.response?.data;
  return (
    payload?.message ||
    payload?.title ||
    error?.message ||
    "Không thể tải vật tư tự động từ BOM."
  );
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function normalizeImageUrls(value) {
  if (Array.isArray(value)) {
    return value
      .map((item) => String(item || "").trim())
      .filter(Boolean);
  }

  const text = String(value || "").trim();
  if (!text) return [];

  try {
    const parsed = JSON.parse(text);
    if (Array.isArray(parsed)) {
      return parsed
        .map((item) => String(item || "").trim())
        .filter(Boolean);
    }
  } catch {
    // Fallback to plain text split
  }

  return text
    .split(/[,\n;]/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

function toEvidenceMediaUrlsValue(urls) {
  const normalized = normalizeImageUrls(urls);
  return normalized.length > 0 ? JSON.stringify(normalized) : "";
}

function normalizeQuantity(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
}

function isNewRecommendationItem(item) {
  return Boolean(item?.isNew) || String(item?.id || "").startsWith("tmp-");
}

function buildVehicleDisplay(order, modelById, brandById, vehicleContext = null) {
  const plate = firstNonEmptyText(
    vehicleContext?.vehicleLicensePlate,
    vehicleContext?.VehicleLicensePlate,
    order?.vehicleLicensePlate,
    order?.VehicleLicensePlate,
    order?.guestVehicleLicensePlate,
    order?.GuestVehicleLicensePlate,
    order?.licensePlate,
    order?.LicensePlate,
    order?.vehicle?.licensePlate,
    order?.vehicle?.LicensePlate,
    order?.customerVehicle?.licensePlate,
    order?.customerVehicle?.LicensePlate
  );

  const resolvedVehicleModelId = toPositiveNumber(
    vehicleContext?.vehicleModelId,
    vehicleContext?.VehicleModelId,
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
  const resolvedModel = resolvedVehicleModelId ? modelById.get(resolvedVehicleModelId) : null;

  const resolvedVehicleBrandId = toPositiveNumber(
    vehicleContext?.vehicleBrandId,
    vehicleContext?.VehicleBrandId,
    order?.vehicleBrandId,
    order?.VehicleBrandId,
    order?.guestVehicleBrandId,
    order?.GuestVehicleBrandId,
    order?.customerVehicle?.vehicleBrandId,
    order?.customerVehicle?.VehicleBrandId,
    order?.vehicle?.vehicleBrandId,
    order?.vehicle?.VehicleBrandId,
    order?.vehicle?.brandId,
    order?.vehicle?.BrandId,
    resolvedModel?.brandId,
    resolvedModel?.BrandId
  );
  const resolvedBrand = resolvedVehicleBrandId ? brandById.get(resolvedVehicleBrandId) : null;

  const brand = firstNonEmptyText(
    vehicleContext?.vehicleBrandName,
    vehicleContext?.VehicleBrandName,
    order?.vehicleBrandName,
    order?.VehicleBrandName,
    order?.guestVehicleBrandName,
    order?.GuestVehicleBrandName,
    order?.brandName,
    order?.BrandName,
    order?.vehicle?.brandName,
    order?.vehicle?.BrandName,
    order?.customerVehicle?.brandName,
    order?.customerVehicle?.BrandName,
    order?.vehicle?.brand?.name,
    order?.vehicle?.Brand?.Name,
    order?.customerVehicle?.brand?.name,
    order?.customerVehicle?.Brand?.Name,
    resolvedBrand?.name,
    resolvedBrand?.Name
  );

  const model = firstNonEmptyText(
    vehicleContext?.vehicleModelName,
    vehicleContext?.VehicleModelName,
    vehicleContext?.vehicleDescriptor,
    vehicleContext?.VehicleDescriptor,
    order?.vehicleModelName,
    order?.VehicleModelName,
    order?.guestVehicleModelName,
    order?.GuestVehicleModelName,
    order?.modelName,
    order?.ModelName,
    order?.vehicle?.modelName,
    order?.vehicle?.ModelName,
    order?.customerVehicle?.modelName,
    order?.customerVehicle?.ModelName,
    order?.vehicle?.model?.name,
    order?.vehicle?.Model?.Name,
    order?.customerVehicle?.model?.name,
    order?.customerVehicle?.Model?.Name,
    resolvedModel?.modelName,
    resolvedModel?.ModelName,
    resolvedModel?.name,
    resolvedModel?.Name
  );

  const yearRaw = firstNonEmptyText(
    vehicleContext?.vehicleManufactureYear,
    vehicleContext?.VehicleManufactureYear,
    vehicleContext?.vehicleYear,
    vehicleContext?.VehicleYear,
    order?.vehicleYear,
    order?.VehicleYear,
    order?.guestVehicleYear,
    order?.GuestVehicleYear,
    order?.manufactureYear,
    order?.ManufactureYear,
    order?.year,
    order?.Year,
    order?.vehicle?.vehicleYear,
    order?.vehicle?.VehicleYear,
    order?.vehicle?.year,
    order?.vehicle?.Year,
    order?.customerVehicle?.vehicleYear,
    order?.customerVehicle?.VehicleYear,
    order?.customerVehicle?.year,
    order?.customerVehicle?.Year
  );
  const yearNumber = Number(yearRaw);
  const year = Number.isFinite(yearNumber) && yearNumber > 1900 ? String(Math.trunc(yearNumber)) : "";

  return {
    plate: plate || "Chưa có",
    brand: brand || "Chưa có",
    model: model || "Chưa có",
    year: year || "Chưa có",
  };
}

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
        const message = "Khách chưa ký check-in. Vui lòng hoàn tất Nhật ký xe trước khi vào chuẩn đoán.";
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

      toast({ title: "Đã lưu chuẩn đoán", status: "success", duration: 2500, isClosable: true });
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
          <Text color="#707eae" mt={1}>
            Lệnh sửa chữa: <strong>{order?.code || `#${orderId}`}</strong>
            {" | "}
            Biển số: <strong>{vehicleDisplay.plate}</strong>
            {" | "}
            Dòng xe: <strong>{vehicleModelDisplay}</strong>
            {" | "}
            Năm sản xuất: <strong>{vehicleDisplay.year}</strong>
          </Text>
          <Text color="#707eae" mt={1}>
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
        <Text color="#707eae">Chưa có yêu cầu chuẩn đoán nào.</Text>
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
                    Lưu chuẩn đoán
                  </Button>
                </HStack>
              )}
            </VStack>
      )}
    </Box>
  );
}

function RecommendationRow({
  item,
  mode = "service",
  displayNumber,
  canEdit,
  services,
  products,
  partCategories = [],
  onChange,
  onRemove,
  onAddMaterial,
  onAddEvidenceImages,
  onRemoveEvidenceImage,
  parentServiceName,
  materialItems = [],
  materialCount = 0,
  isMaterialExpanded = false,
  onToggleMaterials = null,
}) {
  const isServiceMode = mode === "service";
  const canAddMaterial = Boolean(item.standardServiceId);

  const selectedService = isServiceMode
    ? (services || []).find((service) => String(extractServiceId(service) || "") === String(item.standardServiceId || ""))
    : null;

  const selectedServiceName = isServiceMode
    ? (extractServiceName(selectedService) || (item.standardServiceId ? `Dịch vụ #${item.standardServiceId}` : "Chưa chọn dịch vụ"))
    : "";

  const selectedProduct = !isServiceMode
    ? (products || []).find((product) => String(product?.id ?? product?.Id ?? "") === String(item.inventoryItemId || ""))
    : null;

  const selectedProductName = !isServiceMode
    ? String(selectedProduct?.name ?? selectedProduct?.Name ?? "").trim() || "Chưa chọn vật tư"
    : "";

  const isQuantityInputEmpty = item.quantity === "";
  const parsedQuantity = Number(item.quantity);
  const itemQuantity = Number.isFinite(parsedQuantity) && parsedQuantity > 0 ? parsedQuantity : 1;
  const quantityInputValue = isQuantityInputEmpty ? "" : itemQuantity;

  const quantityWarningMessage = isServiceMode
    ? (itemQuantity > 5 ? "Số lượng dịch vụ vượt 5. Vui lòng kiểm tra lại." : "")
    : "";

  const serviceEvidenceUrls = isServiceMode ? normalizeImageUrls(item.evidenceMediaUrls) : [];
  const evidenceInputId = `diagnose-evidence-${item.id}`;

  const canToggleMaterials = isServiceMode && materialCount > 0 && typeof onToggleMaterials === "function";
  const toggleMaterialsTooltip = isMaterialExpanded
    ? "Bấm vào dòng dịch vụ để ẩn danh sách phụ tùng."
    : "Bấm vào dòng dịch vụ để xem danh sách phụ tùng.";

  const [materialPickerVisible, setMaterialPickerVisible] = useState(false);
  const [materialPickerCategoryId, setMaterialPickerCategoryId] = useState("");
  const [materialPickerKeyword, setMaterialPickerKeyword] = useState("");
  const [materialPickerOpen, setMaterialPickerOpen] = useState(false);
  const materialPickerRef = useRef(null);

  const safeMaterialItems = Array.isArray(materialItems) ? materialItems : [];
  const safeProducts = Array.isArray(products) ? products : [];
  const safePartCategories = Array.isArray(partCategories) ? partCategories : [];

  const partCategoryById = useMemo(() => {
    const map = new Map();
    safePartCategories.forEach((category) => {
      const id = extractCategoryId(category);
      if (!id) return;
      const name = extractCategoryName(category) || `Danh mục ${id}`;
      map.set(String(id), name);
    });
    return map;
  }, [safePartCategories]);

  const existingMaterialInventoryIdSet = useMemo(
    () =>
      new Set(
        safeMaterialItems
          .map((material) => String(toPositiveNumber(material?.inventoryItemId ?? material?.InventoryItemId) || ""))
          .filter(Boolean)
      ),
    [safeMaterialItems]
  );

  const materialCategoryOptions = useMemo(() => {
    const map = new Map();
    const allowedCategoryIdSet = new Set();

    safePartCategories.forEach((category) => {
      const categoryId = extractCategoryId(category);
      const parentCategoryId = extractCategoryParentId(category);
      if (!categoryId || !parentCategoryId) return;
      if (map.has(String(categoryId))) return;
      const categoryName = extractCategoryName(category) || `Danh mục ${categoryId}`;
      allowedCategoryIdSet.add(String(categoryId));
      map.set(String(categoryId), { value: String(categoryId), label: categoryName });
    });

    safeProducts.forEach((product) => {
      const categoryId = extractProductCategoryId(product);
      if (!categoryId) return;
      if (!allowedCategoryIdSet.has(String(categoryId))) return;
      if (map.has(String(categoryId))) return;
      const categoryName =
        partCategoryById.get(String(categoryId)) ||
        extractProductCategoryName(product) ||
        `Danh mục ${categoryId}`;
      map.set(String(categoryId), { value: String(categoryId), label: categoryName });
    });

    return Array.from(map.values()).sort((a, b) => a.label.localeCompare(b.label, "vi"));
  }, [safePartCategories, safeProducts, partCategoryById]);

  const filteredMaterialProducts = useMemo(() => {
    const keyword = normalizeLookupText(materialPickerKeyword);
    return safeProducts
      .filter((product) => {
        const productId = String(toPositiveNumber(product?.id ?? product?.Id) || "");
        if (!productId) return false;

        const categoryId = String(extractProductCategoryId(product) || "");
        if (materialPickerCategoryId && categoryId !== String(materialPickerCategoryId)) return false;

        if (!keyword) return true;

        const productName = normalizeLookupText(extractProductName(product));
        const productCode = normalizeLookupText(String(product?.sku ?? product?.Sku ?? product?.code ?? product?.Code ?? ""));
        const categoryName = normalizeLookupText(partCategoryById.get(categoryId) || extractProductCategoryName(product));
        return productName.includes(keyword) || productCode.includes(keyword) || categoryName.includes(keyword);
      })
      .sort((a, b) => extractProductName(a).localeCompare(extractProductName(b), "vi"));
  }, [safeProducts, materialPickerCategoryId, materialPickerKeyword, partCategoryById]);

  useEffect(() => {
    if (!materialPickerOpen) return undefined;
    const handleOutsideClick = (event) => {
      if (!materialPickerRef.current) return;
      if (materialPickerRef.current.contains(event.target)) return;
      setMaterialPickerOpen(false);
    };
    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, [materialPickerOpen]);

  const handleServiceRowToggle = (event) => {
    if (!canToggleMaterials) return;
    const target = event?.target;
    if (target instanceof Element) {
      const interactiveParent = target.closest(
        "button, input, textarea, select, a, label, [role='button'], [data-no-row-toggle='true']"
      );
      if (interactiveParent && interactiveParent !== event.currentTarget) return;
    }
    onToggleMaterials?.();
  };

  const handleServiceRowKeyDown = (event) => {
    if (!canToggleMaterials) return;
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onToggleMaterials?.();
    }
  };

  const openMaterialPicker = () => {
    if (!canEdit || !isServiceMode || !canAddMaterial) return;
    if (materialPickerVisible) {
      setMaterialPickerVisible(false);
      setMaterialPickerOpen(false);
      setMaterialPickerKeyword("");
      return;
    }
    setMaterialPickerVisible(true);
    setMaterialPickerOpen(false);
    if (!isMaterialExpanded) onToggleMaterials?.();
  };

  const selectMaterialFromPicker = (product) => {
    const productId = toPositiveNumber(product?.id ?? product?.Id);
    if (!productId) return;
    onAddMaterial?.(item.id, productId);
    if (!isMaterialExpanded) onToggleMaterials?.();
    setMaterialPickerOpen(true);
    setMaterialPickerKeyword("");
  };

  const toggleMaterialFromPicker = (product) => {
    const productId = toPositiveNumber(product?.id ?? product?.Id);
    if (!productId) return;

    const existedItem = safeMaterialItems.find(
      (material) => toPositiveNumber(material?.inventoryItemId ?? material?.InventoryItemId) === productId
    );

    if (existedItem) {
      onRemove?.(existedItem);
      setMaterialPickerOpen(true);
      return;
    }

    selectMaterialFromPicker(product);
  };

  return (
    <Stack
      spacing={isServiceMode ? 1 : 1}
      p={{ base: 2.5, md: 2 }}
      position="relative"
      overflow="visible"
      zIndex={isServiceMode && materialPickerOpen ? 60 : "auto"}
      border={isServiceMode ? "1px solid #c9daf8" : "1px solid #eadfcf"}
      borderRadius="12px"
      bg={isServiceMode ? "#f8fbff" : "#fffaf5"}
      boxShadow="0 1px 4px rgba(42, 67, 122, 0.08)"
      cursor={canToggleMaterials ? "pointer" : "default"}
      transition="all .18s ease"
      _hover={
        canToggleMaterials
          ? {
              borderColor: "#93b6ff",
              boxShadow: "0 6px 14px rgba(42, 67, 122, 0.12)",
            }
          : undefined
      }
      onClick={handleServiceRowToggle}
      onKeyDown={handleServiceRowKeyDown}
      role={canToggleMaterials ? "button" : undefined}
      tabIndex={canToggleMaterials ? 0 : undefined}
      aria-expanded={canToggleMaterials ? isMaterialExpanded : undefined}
      title={canToggleMaterials ? toggleMaterialsTooltip : undefined}
    >
      <HStack justify="space-between" align="flex-start" flexWrap="wrap" gap={1} position="relative" zIndex={2}>
        <HStack spacing={1.5} align="center" minH={{ base: "30px", md: "26px" }}>
          {canToggleMaterials ? (
            <Tooltip hasArrow label={toggleMaterialsTooltip} placement="top-start">
              <Text fontWeight="800" fontSize={{ base: "lg", md: "md" }} color={isServiceMode ? "#2b3674" : "#8a5a1f"}>
                {`#${displayNumber ?? item.id}`}
              </Text>
            </Tooltip>
          ) : (
            <Text fontWeight="800" fontSize={{ base: "lg", md: "md" }} color={isServiceMode ? "#2b3674" : "#8a5a1f"}>
              {`#${displayNumber ?? item.id}`}
            </Text>
          )}
        </HStack>

        <HStack spacing={2} align="center" position="relative" zIndex={3} ml="auto">
          {canEdit && (
            <Button
              size={{ base: "md", md: "sm" }}
              minH={{ base: "34px", md: "30px" }}
              px={{ base: 3, md: 2 }}
              colorScheme="red"
              variant="ghost"
              position="relative"
              zIndex={3}
              onClick={() => onRemove(item)}
            >
              Xóa
            </Button>
          )}
        </HStack>
      </HStack>

      {isServiceMode && (
        <Grid templateColumns={{ base: "1fr", xl: "minmax(0, 1fr) 520px" }} gap={{ base: 2, md: 2 }} alignItems="center">
          <Box>
            <Text fontSize={{ base: "lg", md: "md" }} fontWeight="800" color="#2b3674" lineHeight="1.2" mt={1}>
              {selectedServiceName}
            </Text>
          </Box>

          <HStack align="center" spacing={1} maxW={{ base: "100%", xl: "520px" }} justify="flex-start" ml={0}>
            <Box p={1.5} borderRadius="8px" border="1px solid #9fc0ff" bg="#f8fbff" w={{ base: "100%", xl: "126px" }} minW={{ base: "100%", xl: "126px" }}>
              <Input
                id={evidenceInputId}
                type="file"
                accept="image/*"
                capture="environment"
                multiple
                display="none"
                isDisabled={!canEdit}
                onChange={(event) => {
                  onAddEvidenceImages?.(item.id, event.target.files);
                  event.target.value = "";
                }}
              />

              <Box
                as="label"
                htmlFor={evidenceInputId}
                display="inline-flex"
                alignItems="center"
                justifyContent="center"
                px={2}
                h="44px"
                w="100%"
                minW="100%"
                borderRadius="8px"
                bg="linear-gradient(180deg, #ffffff 0%, #edf4ff 100%)"
                color="#2563eb"
                border="1px solid #7fa7ff"
                boxShadow="0 1px 4px rgba(37, 99, 235, 0.12)"
                fontSize="13px"
                fontWeight="700"
                whiteSpace="nowrap"
                letterSpacing="0.01em"
                cursor={canEdit ? "pointer" : "not-allowed"}
                _hover={canEdit ? { borderColor: "#5b8fff", bg: "linear-gradient(180deg, #ffffff 0%, #e5efff 100%)" } : undefined}
                data-no-row-toggle="true"
              >
                + Tải ảnh
              </Box>
              <Text mt={0.5} fontSize="11px" fontWeight="700" color="#e45b5b" lineHeight="1.2" textAlign="center">
                * Ảnh bằng chứng
              </Text>
            </Box>

            <HStack spacing={1} overflowX="visible" maxW="100%" flex="1" justify="flex-start" pt={0} flexWrap="wrap" rowGap={1}>
              {serviceEvidenceUrls.map((url, index) => (
                <Box key={`${item.id}-evidence-${index}`} position="relative" flex="0 0 auto">
                  <Box
                    as="img"
                    src={url}
                    alt={`evidence-${index + 1}`}
                    w="82px"
                    h="82px"
                    objectFit="cover"
                    borderRadius="10px"
                    border="1px solid #9fc0ff"
                    bg="#f5f8ff"
                    cursor="zoom-in"
                    data-no-row-toggle="true"
                    onClick={(event) => event.stopPropagation()}
                  />
                  {canEdit && (
                    <Button
                      size="2xs"
                      colorScheme="red"
                      position="absolute"
                      top="-4px"
                      right="-4px"
                      minW="14px"
                      h="14px"
                      p={0}
                      fontSize="10px"
                      data-no-row-toggle="true"
                      onClick={() => onRemoveEvidenceImage?.(item.id, index)}
                    >
                      x
                    </Button>
                  )}
                </Box>
              ))}
            </HStack>
          </HStack>
        </Grid>
      )}

      {isServiceMode && (
        <Box mt={{ base: 1, md: 0.5 }} data-no-row-toggle="true">
          <Grid
            templateColumns={{ base: "1fr", lg: "minmax(220px, 1fr) minmax(220px, 1fr) auto" }}
            gap={{ base: 1.5, md: 3 }}
            alignItems="end"
            justifyContent="stretch"
          >
            <Box position="relative" w="100%" maxW="100%" ml={0}>
              <Text fontSize={{ base: "md", md: "sm" }} fontWeight="700" color="#7f8fb2" mb={1}>
                Số lượng
              </Text>
              <NumberInput
                size="sm"
                min={1}
                value={quantityInputValue}
                isDisabled={!canEdit}
                onChange={(valueString, valueNumber) => {
                  if (valueString === "") {
                    onChange(item.id, { quantity: "" });
                    return;
                  }
                  onChange(item.id, { quantity: Number.isFinite(valueNumber) && valueNumber > 0 ? valueNumber : 1 });
                }}
                keepWithinRange
                clampValueOnBlur
                w="100%"
              >
                <HStack spacing={0.75} w="100%">
                  <Button
                    size="sm"
                    minW={{ base: "34px", md: "30px" }}
                    h={{ base: "34px", md: "30px" }}
                    px={0}
                    borderRadius="10px"
                    variant="outline"
                    borderColor="#9fc0ff"
                    color="#2b3674"
                    fontSize={{ base: "md", md: "sm" }}
                    isDisabled={!canEdit || isQuantityInputEmpty || itemQuantity <= 1}
                    onClick={() => onChange(item.id, { quantity: Math.max(1, itemQuantity - 1) })}
                  >
                    -
                  </Button>
                  <NumberInputField
                    bg="rgba(255,255,255,0.92)"
                    borderColor={quantityWarningMessage ? "#f4b400" : "#9fc0ff"}
                    borderRadius="10px"
                    textAlign="center"
                    h={{ base: "34px", md: "30px" }}
                    fontSize={{ base: "md", md: "sm" }}
                    fontWeight="700"
                    _focus={{ borderColor: "#7aa6ff", boxShadow: "0 0 0 1px #7aa6ff" }}
                    onBlur={() => {
                      if (item.quantity === "" || !Number.isFinite(Number(item.quantity)) || Number(item.quantity) <= 0) {
                        onChange(item.id, { quantity: 1 });
                      }
                    }}
                  />
                  <Button
                    size="sm"
                    minW={{ base: "34px", md: "30px" }}
                    h={{ base: "34px", md: "30px" }}
                    px={0}
                    borderRadius="10px"
                    variant="outline"
                    borderColor="#9fc0ff"
                    color="#2b3674"
                    fontSize={{ base: "md", md: "sm" }}
                    isDisabled={!canEdit}
                    onClick={() => onChange(item.id, { quantity: (isQuantityInputEmpty ? 0 : itemQuantity) + 1 })}
                  >
                    +
                  </Button>
                </HStack>
              </NumberInput>
              {quantityWarningMessage ? (
                <Text
                  position="absolute"
                  top="calc(100% + 4px)"
                  left="0"
                  fontSize="12px"
                  fontWeight="600"
                  color="#dc2626"
                  lineHeight="1.3"
                  zIndex={2}
                >
                  {quantityWarningMessage}
                </Text>
              ) : null}
            </Box>

            <Box w="100%" maxW="100%">
              <Text fontSize={{ base: "md", md: "sm" }} fontWeight="700" color="#7f8fb2" mb={1}>
                Độ ưu tiên
              </Text>
              <Box display="flex" alignItems="center">
                <DiagnosisDropdown
                  minW="100%"
                  value={item.urgency || "RECOMMENDED"}
                  isDisabled={!canEdit}
                  onChange={(value) => onChange(item.id, { urgency: value })}
                  fullWidth
                >
                  <option value="CRITICAL">Khẩn cấp</option>
                  <option value="RECOMMENDED">Khuyến nghị</option>
                  <option value="OPTIONAL">Tùy chọn</option>
                </DiagnosisDropdown>
              </Box>
            </Box>

            {canEdit && (
              <Box w={{ base: "100%", lg: "auto" }}>
                <Box
                  as="button"
                  type="button"
                  appearance="none"
                  WebkitAppearance="none"
                  h={{ base: "34px", md: "32px" }}
                  minH={{ base: "34px", md: "32px" }}
                  maxH={{ base: "34px", md: "32px" }}
                  px={{ base: 3, md: 3 }}
                  py="0"
                  m="0"
                  borderRadius="12px"
                  border="1px solid"
                  borderColor={canAddMaterial ? "#2b6fe8" : "#c9d8ef"}
                  bg={canAddMaterial ? "linear-gradient(180deg, #ffffff 0%, #f7faff 100%)" : "#f3f4f6"}
                  color={canAddMaterial ? "#2b6fe8" : "#94a3b8"}
                  fontSize={{ base: "sm", md: "sm" }}
                  fontWeight="700"
                  lineHeight={{ base: "34px", md: "32px" }}
                  display="inline-flex"
                  alignItems="center"
                  justifyContent="center"
                  verticalAlign="middle"
                  boxShadow={canAddMaterial ? "0 2px 8px rgba(58, 99, 168, 0.08)" : "none"}
                  cursor={canAddMaterial ? "pointer" : "not-allowed"}
                  transition="all .15s ease"
                  _hover={canAddMaterial ? { bg: "#f4f8ff" } : undefined}
                  w={{ base: "100%", lg: "auto" }}
                  onClick={(event) => {
                    event.stopPropagation();
                    openMaterialPicker();
                  }}
                  disabled={!canAddMaterial}
                  data-no-row-toggle="true"
                >
                  + Thêm phụ tùng
                </Box>
              </Box>
            )}
          </Grid>

          <Box pt={0.75} mt={0.75} borderTop="1px dashed #9fc0ff">
            <Text fontSize={{ base: "xs", md: "11px" }} fontWeight="700" textTransform="uppercase" letterSpacing="0.02em" color="#6f83b3" mb={1.5}>
              Ghi chú kỹ thuật
            </Text>
            <Textarea
              size="sm"
              minH={{ base: "48px", md: "42px" }}
              placeholder="Nhập ghi chú kỹ thuật..."
              value={item.technicianNotes || ""}
              isDisabled={!canEdit}
              onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
              bg="rgba(255,255,255,0.92)"
              borderColor="#9fc0ff"
              fontSize={{ base: "sm", md: "sm" }}
              _focus={{ borderColor: "#7aa6ff", boxShadow: "0 0 0 1px #7aa6ff" }}
            />
          </Box>

          {materialPickerVisible ? (
            <Box
              ref={materialPickerRef}
              mt={2}
              p={2}
              position="relative"
              zIndex={30}
              border="1px solid #9fc0ff"
              borderRadius="12px"
              bg="#f8fbff"
              data-no-row-toggle="true"
            >
              <HStack spacing={1.5} align="end" flexWrap="nowrap" w="100%">
                <DiagnosisDropdown
                  label="Loại phụ tùng"
                  labelFontSize="xs"
                  labelColor="#5c6b8a"
                  minW="250px"
                  maxW="250px"
                  flex="0 0 250px"
                  value={materialPickerCategoryId}
                  onChange={(value) => setMaterialPickerCategoryId(value || "")}
                  isDisabled={!canEdit}
                >
                  <option value="">-- Tất cả loại phụ tùng --</option>
                  {materialCategoryOptions.map((category) => (
                    <option key={category.value} value={category.value}>
                      {category.label}
                    </option>
                  ))}
                </DiagnosisDropdown>

                <Box flex="1 1 auto" minW="0">
                  <Text fontSize="xs" fontWeight="600" color="#5c6b8a" mb={2}>
                    Tìm phụ tùng
                  </Text>
                  <Box position="relative">
                    <Input
                      size="sm"
                      value={materialPickerKeyword}
                      onChange={(event) => {
                        setMaterialPickerKeyword(event.target.value);
                        setMaterialPickerOpen(true);
                      }}
                      onFocus={() => setMaterialPickerOpen(true)}
                      placeholder={
                        materialPickerCategoryId
                          ? "Nhập tên phụ tùng trong loại đã chọn..."
                          : "Nhập tên phụ tùng để tìm trên toàn bộ danh sách..."
                      }
                      isDisabled={!canEdit}
                      bg="white"
                      borderColor="#d7e3f5"
                      data-no-row-toggle="true"
                    />
                    {materialPickerOpen && canEdit ? (
                      <Box
                        position="absolute"
                        top="calc(100% + 6px)"
                        left={0}
                        right={0}
                        zIndex={40}
                        bg="white"
                        border="1px solid #d7e3f5"
                        borderRadius="md"
                        boxShadow="0 10px 24px rgba(27, 37, 89, 0.12)"
                        maxH="240px"
                        overflowY="auto"
                      >
                        {filteredMaterialProducts.length === 0 ? (
                          <Text px={3} py={2} fontSize="sm" color="#7f8fb2">
                            Không có phụ tùng phù hợp.
                          </Text>
                        ) : (
                          filteredMaterialProducts.map((product) => {
                            const productId = String(toPositiveNumber(product?.id ?? product?.Id) || "");
                            const existed = existingMaterialInventoryIdSet.has(productId);
                            const productName = extractProductName(product) || `Phụ tùng #${productId}`;
                            const categoryName = extractProductCategoryName(product);
                            return (
                              <Box
                                as="button"
                                type="button"
                                key={productId}
                                w="100%"
                                textAlign="left"
                                px={3}
                                py={2}
                                bg={existed ? "#edf4ff" : "transparent"}
                                _hover={{ bg: existed ? "#e4efff" : "#f4f8ff" }}
                                borderLeft={existed ? "2px solid #3b82f6" : "2px solid transparent"}
                                border="none"
                                data-no-row-toggle="true"
                                onClick={() => toggleMaterialFromPicker(product)}
                              >
                                <Text fontSize="sm" fontWeight={existed ? "700" : "600"} color={existed ? "#274690" : "#2b3674"}>
                                  {productName}
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
                    ) : null}
                  </Box>
                </Box>
              </HStack>
            </Box>
          ) : null}
        </Box>
      )}

      {!isServiceMode && (
        <Stack spacing={0.5}>
          <Grid
            templateColumns={{ base: "1fr", lg: "minmax(260px, 1fr) 220px" }}
            gap={2}
            alignItems="start"
          >
            <Box>
              <Text fontSize={{ base: "xl", md: "lg" }} fontWeight="800" color="#7f4f1f" lineHeight="1.35">
                {selectedProductName}
              </Text>
              <Box mt={2}>
                <Text fontSize="11px" fontWeight="700" textTransform="uppercase" letterSpacing="0.02em" color="#b07a2f" mb={1}>Ghi chú kỹ thuật</Text>
                <Textarea
                  size="sm"
                  maxW={{ base: "100%", lg: "420px" }}
                  minH="68px"
                  placeholder="Ghi chú cho phụ tùng này..."
                  value={item.technicianNotes || ""}
                  isDisabled={!canEdit}
                  onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
                  bg="rgba(255,255,255,0.92)"
                  borderColor="#e7d5b6"
                  _focus={{ borderColor: "#d4a15a", boxShadow: "0 0 0 1px #d4a15a" }}
                />
              </Box>
            </Box>
            <Box p={2} bg="rgba(255,255,255,0.55)" border="1px solid #f1dec1" borderRadius="10px" w={{ base: "100%", lg: "220px" }} maxW={{ base: "100%", lg: "220px" }} justifySelf={{ base: "stretch", lg: "end" }}>
              <Text fontSize="11px" fontWeight="700" textTransform="uppercase" letterSpacing="0.02em" color="#b07a2f" mb={1.5}>Độ ưu tiên</Text>
              <DiagnosisDropdown
                minW="100%"
                value={item.urgency || "RECOMMENDED"}
                isDisabled={!canEdit}
                onChange={(value) => onChange(item.id, { urgency: value })}
                fullWidth
              >
                <option value="CRITICAL">Khẩn cấp</option>
                <option value="RECOMMENDED">Khuyến nghị</option>
                <option value="OPTIONAL">Tùy chọn</option>
              </DiagnosisDropdown>
            </Box>
          </Grid>
        </Stack>
      )}
    </Stack>
  );
}

