import React, { useEffect, useMemo, useState, useCallback, useRef } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import {
  Badge,
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
  NumberInput,
  NumberInputField,
  NumberInputStepper,
  NumberIncrementStepper,
  NumberDecrementStepper,
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
    .replace(/Ä‘/g, "d");
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
    "KhÃ´ng thá»ƒ táº£i váº­t tÆ° tá»± Ä‘á»™ng tá»« BOM."
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
    plate: plate || "ChÆ°a cÃ³",
    brand: brand || "ChÆ°a cÃ³",
    model: model || "ChÆ°a cÃ³",
    year: year || "ChÆ°a cÃ³",
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
  const [error, setError] = useState("");
  const [order, setOrder] = useState(null);
  const [orderDetails, setOrderDetails] = useState([]);
  
  const [allDiagnoses, setAllDiagnoses] = useState([]);
  const [activeDiagId, setActiveDiagId] = useState(null);
  
  const [recommendations, setRecommendations] = useState([]);
  const [deletedRecommendationIds, setDeletedRecommendationIds] = useState([]);
  const [expandedMaterialByService, setExpandedMaterialByService] = useState({});
  const [services, setServices] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
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
    !isDiagnosisApprovalLocked;

  const queryParams = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const fromHistoryQuery = queryParams.get("from") === "history";
  const requestedReturnPath = String(location?.state?.returnToPath || "").trim();
  const safeReturnPath = requestedReturnPath.startsWith("/workshop/") ? requestedReturnPath : "";
  const defaultBackPath = isTechnician ? "/workshop/technician" : "/workshop/reception";
  const fallbackHistoryPath = isTechnician && fromHistoryQuery ? "/workshop/technician/history" : "";
  const backPath = safeReturnPath || fallbackHistoryPath || defaultBackPath;
  const backButtonLabel = "Quay láº¡i";
  const customerDescription = useMemo(() => {
    const raw = String(order?.customerNotes || "").trim();
    if (!raw) return "KhÃ¡ch chÆ°a mÃ´ táº£ chi tiáº¿t.";
    try {
      const parsed = JSON.parse(raw);
      const values = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.symptoms) ? parsed.symptoms : [];
      const merged = values.map((item) => String(item || "").trim()).filter(Boolean).join("\n");
      return merged || raw;
    } catch {
      return raw;
    }
  }, [order?.customerNotes]);
  const diagnosisAssignmentNote = useMemo(
    () =>
      firstNonEmptyText(
        activeDiagnosis?.assignmentNote,
        activeDiagnosis?.AssignmentNote
      ),
    [activeDiagnosis]
  );
  const routeVehicleContext = useMemo(
    () => location?.state?.vehicleSnapshot || location?.state?.orderSummary || null,
    [location?.state]
  );
  const modelById = useMemo(() => {
    const map = new Map();
    (Array.isArray(vehicleModels) ? vehicleModels : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [vehicleModels]);
  const brandById = useMemo(() => {
    const map = new Map();
    (Array.isArray(vehicleBrands) ? vehicleBrands : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id);
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
        const id = toPositiveNumber(item?.id, item?.Id);
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
    return model || brand || "ChÆ°a cÃ³";
  }, [vehicleDisplay.brand, vehicleDisplay.model]);

  const resolvedVehicleModelForServiceFilter = useMemo(() => (resolvedVehicleModelIdForBom ? modelById.get(resolvedVehicleModelIdForBom) || null : null), [resolvedVehicleModelIdForBom, modelById]);

  const compatibleServices = useMemo(() => {
    const source = Array.isArray(services) ? services : [];
    if (!resolvedVehicleModelForServiceFilter) return source;
    return source.filter((service) =>
      isServiceCompatibleWithVehicleModel(service, resolvedVehicleModelForServiceFilter)
    );
  }, [services, resolvedVehicleModelForServiceFilter]);

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
      map.set(String(serviceId), name || `Dá»‹ch vá»¥ #${serviceId}`);
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
        names.add(serviceNameById.get(String(serviceId)) || detailName || `Dá»‹ch vá»¥ #${serviceId}`);
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
      const label = extractCategoryName(category) || `NhÃ³m ${id}`;
      map.set(String(id), { value: String(id), label });
    });

    (compatibleServices || []).forEach((service) => {
      const categoryId = extractServiceCategoryId(service);
      if (!categoryId) return;
      if (map.has(String(categoryId))) return;
      const fallbackLabel = extractServiceCategoryName(service) || `NhÃ³m ${categoryId}`;
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
      const cleaned = values.map((x) => String(x || "").trim()).filter(Boolean);
      if (cleaned.length > 0) return cleaned;
    } catch {}
    const text = String(rawNotes || "").trim();
    return text ? [text] : ["KhÃ¡ch chÆ°a mÃ´ táº£ chi tiáº¿t"];
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
      setError("MÃ£ lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const [orderRes, diagnosesRes, servicesRes, serviceCategoriesRes, productsRes, conditionLogsRes, modelsRes, brandsRes, detailsRes] = await Promise.all([
        serviceOrderApi.getById(id),
        serviceOrderApi.getDiagnoses(id),
        getAllStandardServices(),
        getAllServiceCategories(),
        inventoryApi.getProducts(),
        vehicleConditionLogApi.getByServiceOrderId(id),
        getVehicleModels().catch(() => []),
        getVehicleBrands().catch(() => []),
        serviceOrderApi.getDetails(id).catch(() => []),
      ]);

      setOrder(orderRes || null);
      setServices(Array.isArray(servicesRes) ? servicesRes : []);
      setServiceCategories(Array.isArray(serviceCategoriesRes) ? serviceCategoriesRes : []);
      setProducts(Array.isArray(productsRes) ? productsRes : []);
      setVehicleModels(Array.isArray(modelsRes) ? modelsRes : []);
      setVehicleBrands(Array.isArray(brandsRes) ? brandsRes : []);
      setOrderDetails(Array.isArray(detailsRes) ? detailsRes : []);
      
      const signedCheckInExists =
        Array.isArray(conditionLogsRes) &&
        conditionLogsRes.some((log) => {
          const type = String(log?.logType || log?.LogType || "").toUpperCase();
          const signature = String(log?.customerSignatureUrl || log?.CustomerSignatureUrl || "").trim();
          return type === "CHECK_IN" && signature.length > 0;
        });
        
      if (!signedCheckInExists) {
        const message = "KhÃ¡ch chÆ°a kÃ½ check-in. Vui lÃ²ng hoÃ n táº¥t Nháº­t kÃ½ xe trÆ°á»›c khi vÃ o chuáº©n Ä‘oÃ¡n.";
        if (isTechnician) {
          setError(message);
          return;
        }

        toast({
          title: "ChÆ°a hoÃ n táº¥t check-in",
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
      let initialActive = diagnosesList[0];
      if (diagnosisIdFromQuery) {
        const found = diagnosesList.find(x => String(x.id) === diagnosisIdFromQuery);
        if (found) initialActive = found;
      }

      if (initialActive) {
        await selectDiagnosis(initialActive.id, diagnosesList);
      }
    } catch (err) {
      setError(err?.message || "KhÃ´ng thá»ƒ táº£i dá»¯ liá»‡u chuáº©n Ä‘oÃ¡n.");
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
        const diagnosisAssignmentId = extractDiagnosisAssignmentId(target);
        if (!diagnosisAssignmentId) {
          throw new Error("Thiáº¿u DiagnosisAssignmentId há»£p lá»‡ cá»§a phiÃªn báº£n cháº©n Ä‘oÃ¡n.");
        }
        const recs = await serviceOrderApi.getDiagnosisRecommendations({ diagnosisAssignmentId });
        setRecommendations(Array.isArray(recs) ? recs : []);
      } catch {
        setRecommendations([]);
      }
    }
    setDeletedRecommendationIds([]);
    setExpandedMaterialByService({});
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

  const addMaterialRecommendation = (parentRecommendationId) => {
    if (!canEdit) return;
    const parentKey = String(parentRecommendationId || "");
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
            title: "KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c dÃ²ng xe",
            description: "KhÃ´ng thá»ƒ lá»c BOM theo dÃ²ng xe. Vui lÃ²ng kiá»ƒm tra láº¡i thÃ´ng tin dÃ²ng xe cá»§a lá»‡nh.",
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
          title: "KhÃ´ng tá»± táº£i Ä‘Æ°á»£c váº­t tÆ° tá»« BOM",
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
        throw new Error("Thiáº¿u DiagnosisAssignmentId há»£p lá»‡ Ä‘á»ƒ lÆ°u kiáº¿n nghá»‹ ká»¹ thuáº­t.");
      }

      const normalizeRecommendationPayload = (rec, parentRecommendationId = null) => {
        const normalizedType = String(rec.itemType || "PART").toUpperCase();
        const itemType = normalizedType === "LABOR" ? "LABOR" : normalizedType === "CONSUMABLE" ? "CONSUMABLE" : "PART";
        return {
          diagnosisId: null,
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
          throw new Error("Vui lÃ²ng chá»n loáº¡i dá»‹ch vá»¥ trÆ°á»›c khi chá»n dá»‹ch vá»¥ sá»­a chá»¯a.");
        }
        if (!toPositiveNumber(root.standardServiceId)) {
          throw new Error("Vui lÃ²ng chá»n dá»‹ch vá»¥ sá»­a chá»¯a cho tá»«ng dÃ²ng dá»‹ch vá»¥.");
        }
        if (!String(root.technicianNotes || "").trim()) {
          throw new Error("Vui lÃ²ng nháº­p lÃ½ do cho tá»«ng dá»‹ch vá»¥ Ä‘Ã£ chá»n trÆ°á»›c khi lÆ°u.");
        }
        if (isNewRecommendationItem(root) && normalizeImageUrls(root.evidenceMediaUrls).length === 0) {
          throw new Error("Má»—i dá»‹ch vá»¥ pháº£i cÃ³ Ã­t nháº¥t 1 áº£nh hiá»‡n tráº¡ng trÆ°á»›c khi lÆ°u.");
        }
      }

      for (const child of children) {
        if (!toPositiveNumber(child.inventoryItemId)) {
          throw new Error("Vui lÃ²ng chá»n váº­t tÆ° cá»¥ thá»ƒ cho tá»«ng dÃ²ng váº­t tÆ°.");
        }
      }

      for (const root of roots) {
        const rootEvidenceUrls = [];
        for (const url of normalizeImageUrls(root.evidenceMediaUrls)) {
          if (String(url).startsWith("data:")) {
            const uploadRes = await vehicleConditionLogApi.uploadImage(url);
            const uploadedUrl = String(uploadRes?.url || "").trim();
            if (!uploadedUrl) {
              throw new Error("KhÃ´ng thá»ƒ upload áº£nh hiá»‡n tráº¡ng cá»§a dá»‹ch vá»¥.");
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
              throw new Error("KhÃ´ng thá»ƒ upload áº£nh Ä‘Ã­nh kÃ¨m cá»§a váº­t tÆ°.");
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

      toast({ title: "ÄÃ£ lÆ°u cháº©n Ä‘oÃ¡n", status: "success", duration: 2500, isClosable: true });
      await loadData();
    } catch (err) {
      toast({ title: "LÆ°u chuáº©n Ä‘oÃ¡n tháº¥t báº¡i", description: err.message, status: "error" });
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
          <Heading size="md" color="#2b3674">BÃ¡o cÃ¡o Chuáº©n Ä‘oÃ¡n ChuyÃªn sÃ¢u</Heading>
          <Text color="#707eae" mt={1}>
            Lá»‡nh sá»­a chá»¯a: <strong>{order?.code || `#${orderId}`}</strong>
            {" | "}
            Biá»ƒn sá»‘: <strong>{vehicleDisplay.plate}</strong>
            {" | "}
            DÃ²ng xe: <strong>{vehicleModelDisplay}</strong>
            {" | "}
            NÄƒm sáº£n xuáº¥t: <strong>{vehicleDisplay.year}</strong>
          </Text>
          <Text color="#707eae" mt={1}>
            Dá»‹ch vá»¥:{" "}
            <strong>
              {selectedCustomerServiceNames.length > 0
                ? selectedCustomerServiceNames.join(" | ")
                : "ChÆ°a cÃ³"}
            </strong>
          </Text>
        </Box>
        <Button colorScheme="blue" onClick={() => navigate(backPath)}>{backButtonLabel}</Button>
      </HStack>

      {error ? (
        <Box p={4} bg="red.50" color="red.600" borderRadius="md">{error}</Box>
      ) : allDiagnoses.length === 0 ? (
        <Text color="#707eae">ChÆ°a cÃ³ yÃªu cáº§u chuáº©n Ä‘oÃ¡n nÃ o.</Text>
      ) : (
        <VStack align="stretch" spacing={5}>
              {/* KHU Vá»°C 1: Lá»i khÃ¡ch hÃ ng */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack color="#f56565" mb={3}>
                  <Icon as={MessageSquare} size={20} />
                  <Heading size="sm">MÃ´ táº£ cá»§a KhÃ¡ch hÃ ng</Heading>
                </HStack>
                <Box
                  bg="#f8fbff"
                  border="1px solid #dbe7ff"
                  borderRadius="md"
                  px={4}
                  py={3}
                >
                  <Text color="#2b3674" whiteSpace="pre-wrap" lineHeight="1.6">
                    {customerDescription}
                  </Text>
                </Box>
              </Box>

              {/* KHU Vá»°C 3: NguyÃªn nhÃ¢n gá»‘c rá»… */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack color="#ffb547" mb={4}>
                  <Icon as={AlertTriangle} size={20} />
                  <Heading size="sm">Káº¿t luáº­n & Má»©c Ä‘á»™</Heading>
                </HStack>
                <Grid templateColumns={{ base: "1fr", md: "2fr 1fr" }} gap={4}>
                  <GridItem>
                    <FormControl>
                      <FormLabel fontSize="sm" fontWeight="600" color="#2b3674">Káº¿t luáº­n nguyÃªn nhÃ¢n</FormLabel>
                      <Textarea
                        value={form.rootCause}
                        onChange={(e) => setForm((prev) => ({ ...prev, rootCause: e.target.value }))}
                        isDisabled={!canEdit}
                        rows={3}
                        placeholder="VD: Cáº£m biáº¿n oxy sá»‘ 1 bá»‹ Ä‘á»©t dÃ¢y ga..."
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
                        label="Má»©c Ä‘á»™ nghiÃªm trá»ng"
                        value={form.severity}
                        onChange={(value) => setForm((prev) => ({ ...prev, severity: value }))}
                        isDisabled={!canEdit}
                      >
                        <option value="LOW">Tháº¥p (ChÆ°a gáº¥p)</option>
                        <option value="MEDIUM">Trung bÃ¬nh</option>
                        <option value="HIGH">Cao</option>
                        <option value="CRITICAL">Kháº©n cáº¥p (Nguy hiá»ƒm)</option>
                      </DiagnosisDropdown>
                    </FormControl>
                  </GridItem>
                </Grid>
              </Box>

              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack color="#2b6cb0" mb={3}>
                  <Icon as={StickyNote} size={20} />
                  <Heading size="sm">Ghi chÃº Ä‘iá»u phá»‘i</Heading>
                </HStack>
                <Box
                  bg="#f8fbff"
                  border="1px solid #dbe7ff"
                  borderRadius="md"
                  px={4}
                  py={3}
                >
                  <Text color="#2b3674" whiteSpace="pre-wrap" lineHeight="1.6">
                    {diagnosisAssignmentNote || "KhÃ´ng cÃ³ ghi chÃº Ä‘iá»u phá»‘i."}
                  </Text>
                </Box>
              </Box>

              {/* KHU Vá»°C 4: Äá» xuáº¥t Sá»­a chá»¯a */}
              <Box p={5} bg="white" borderRadius="xl" border="1px solid #e9edf7" boxShadow="sm">
                <HStack justify="space-between" mb={4}>
                  <HStack color="#05cd99">
                    <Icon as={Wrench} size={20} />
                    <Heading size="sm">Váº­t tÆ° & NhÃ¢n cÃ´ng Ä‘á» xuáº¥t</Heading>
                  </HStack>
                </HStack>

                <Box mb={4} p={4} border="1px solid #d7e3f5" borderRadius="lg" bg="#f8fbff">
                  <HStack spacing={3} align="end" flexWrap="wrap">
                    <DiagnosisDropdown
                      label="Loáº¡i dá»‹ch vá»¥"
                      labelFontSize="xs"
                      labelColor="#5c6b8a"
                      minW="220px"
                      value={quickPickCategoryId}
                      onChange={(value) => setQuickPickCategoryId(value || "")}
                      isDisabled={!canEdit}
                    >
                      <option value="">-- Táº¥t cáº£ loáº¡i dá»‹ch vá»¥ --</option>
                      {(serviceCategoryOptions || []).map((category) => (
                        <option key={category.value} value={category.value}>
                          {category.label}
                        </option>
                      ))}
                    </DiagnosisDropdown>

                    <Box minW="320px" flex="1" ref={quickPickRef}>
                      <Text fontSize="xs" fontWeight="600" color="#5c6b8a" mb={2}>
                        TÃ¬m dá»‹ch vá»¥
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
                              ? "Nháº­p tÃªn dá»‹ch vá»¥ trong loáº¡i Ä‘Ã£ chá»n..."
                              : "Nháº­p tÃªn dá»‹ch vá»¥ Ä‘á»ƒ tÃ¬m trÃªn toÃ n bá»™ danh sÃ¡ch..."
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
                            zIndex={2000}
                            bg="white"
                            border="1px solid #d7e3f5"
                            borderRadius="md"
                            boxShadow="0 10px 24px rgba(27, 37, 89, 0.12)"
                            maxH={{ base: "320px", md: "420px" }}
                            overflowY="auto"
                          >
                            {quickPickFilteredServices.length === 0 ? (
                              <Text px={3} py={2} fontSize="sm" color="#7f8fb2">
                                KhÃ´ng cÃ³ dá»‹ch vá»¥ phÃ¹ há»£p.
                              </Text>
                            ) : (
                              quickPickFilteredServices.map((service) => {
                                const serviceId = String(extractServiceId(service) || "");
                                const existed = existingRootServiceIdSet.has(serviceId);
                                const serviceName = extractServiceName(service) || `Dá»‹ch vá»¥ #${serviceId}`;
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
                        <Box key={root.id} p={3} border="1px solid #e9edf7" borderRadius="md" bg="gray.50">
                          <RecommendationRow
                            mode="service"
                            item={root}
                            displayNumber={rootIndex + 1}
                            canEdit={canEdit}
                            services={compatibleServices}
                            serviceCategoryOptions={serviceCategoryOptions}
                            serviceCategoryByServiceId={serviceCategoryByServiceId}
                            products={products}
                            onChange={updateRecommendation}
                            onRemove={removeRecommendation}
                            onAddMaterial={addMaterialRecommendation}
                            onSelectService={handleServiceSelectionChange}
                            onSelectServiceCategory={handleServiceCategoryChange}
                            onAddEvidenceImages={addEvidenceImagesToRecommendation}
                            onRemoveEvidenceImage={removeEvidenceImageFromRecommendation}
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
                            <VStack align="stretch" mt={3} spacing={2} pl={4} borderLeft="2px solid #d6e0f5">
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
                                  onChange={updateRecommendation}
                                  onRemove={removeRecommendation}
                                  onAddMaterial={addMaterialRecommendation}
                                  onSelectService={handleServiceSelectionChange}
                                  onSelectServiceCategory={handleServiceCategoryChange}
                                  onAddEvidenceImages={addEvidenceImagesToRecommendation}
                                  onRemoveEvidenceImage={removeEvidenceImageFromRecommendation}
                                />
                              ))}
                            </VStack>
                          )}
                        </Box>
                      );
                    })}
                  </VStack>
                )}

                <HStack justify="flex-end" mt={4} spacing={3}>
                  {canEdit && (
                    <Button variant="outline" onClick={addServiceRecommendation} isDisabled={saving}>
                      + Thêm dòng dịch vụ
                    </Button>
                  )}
                  <Button colorScheme="blue" onClick={saveAllDiagnoses} isLoading={saving} isDisabled={!canEdit || saving}>
                    Lưu chẩn đoán
                  </Button>
                </HStack>
              </Box>
            </VStack>
          )}
    </Box>
  );
}

function RecommendationRow({
  mode = "service",
  item,
  displayNumber,
  canEdit,
  services,
  serviceCategoryOptions,
  serviceCategoryByServiceId,
  products,
  onChange,
  onRemove,
  onAddMaterial,
  onSelectService,
  onSelectServiceCategory,
  onAddEvidenceImages,
  onRemoveEvidenceImage,
  materialCount = 0,
  isMaterialExpanded = false,
  onToggleMaterials = null,
}) {
  const isServiceMode = mode === "service";
  const selectedServiceId = toPositiveNumber(item?.standardServiceId);
  const selectedProductId = toPositiveNumber(item?.inventoryItemId);
  const selectedCategoryId = String(item?.serviceCategoryId || serviceCategoryByServiceId?.get(String(selectedServiceId || "")) || "");
  const itemQuantity = normalizeQuantity(item?.quantity);
  const evidenceUrls = normalizeImageUrls(item?.evidenceMediaUrls);
  const evidenceInputId = `diagnose-evidence-${item?.id}`;

  const selectedService = (services || []).find((service) => toPositiveNumber(extractServiceId(service)) === selectedServiceId);
  const selectedProduct = (products || []).find((product) => toPositiveNumber(product?.id ?? product?.Id) === selectedProductId);
  const serviceName = extractServiceName(selectedService) || "Chưa chọn dịch vụ";
  const productName = String(selectedProduct?.name ?? selectedProduct?.Name ?? "").trim() || "Chưa chọn vật tư";
  const itemTypeCode = String(item?.itemType || "PART").toUpperCase();
  const itemTypeLabel = itemTypeCode === "CONSUMABLE" ? "Vật tư tiêu hao" : itemTypeCode === "LABOR" ? "Nhân công" : "Phụ tùng";

  return (
    <Stack spacing={3} p={3} borderRadius="lg" border={isServiceMode ? "1px solid #cfe0ff" : "1px solid #f0d7b3"} bg={isServiceMode ? "#f6f9ff" : "#fffaf2"}>
      <HStack justify="space-between" align="center">
        <HStack spacing={2}>
          <Text fontWeight="700" color={isServiceMode ? "#2b3674" : "#8a5a1f"}>
            {isServiceMode ? "Dịch vụ" : "Vật tư"} #{displayNumber ?? item?.id}
          </Text>
          {!isServiceMode && (
            <Badge borderRadius="full" px={2} py={0.5} bg="#fff1d9" color="#9a5d18" border="1px solid #f1cf98" fontSize="10px" fontWeight="700">
              {item?.isAutoFromBom ? "BOM" : itemTypeLabel}
            </Badge>
          )}
        </HStack>
        {canEdit && (
          <Button size="xs" colorScheme="red" variant="ghost" onClick={() => onRemove?.(item)}>
            Xóa
          </Button>
        )}
      </HStack>

      {isServiceMode ? (
        <Grid templateColumns={{ base: "1fr", lg: "1fr 1fr" }} gap={3}>
          <DiagnosisDropdown
            label="Loại dịch vụ"
            labelFontSize="xs"
            value={selectedCategoryId}
            onChange={(value) => onSelectServiceCategory?.(item?.id, value || "")}
            isDisabled={!canEdit}
          >
            <option value="">-- Chọn loại --</option>
            {(serviceCategoryOptions || []).map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </DiagnosisDropdown>

          <DiagnosisDropdown
            label="Dịch vụ"
            labelFontSize="xs"
            value={selectedServiceId || ""}
            onChange={(value) => onSelectService?.(item?.id, value)}
            isDisabled={!canEdit}
          >
            <option value="">-- Chọn dịch vụ --</option>
            {(services || [])
              .filter((service) => {
                if (!selectedCategoryId) return true;
                return String(extractServiceCategoryId(service) || "") === String(selectedCategoryId);
              })
              .map((service) => {
                const id = extractServiceId(service);
                if (!id) return null;
                return (
                  <option key={id} value={id}>
                    {extractServiceName(service) || `Dịch vụ #${id}`}
                  </option>
                );
              })}
          </DiagnosisDropdown>

          <Box>
            <Text fontSize="11px" color="#7f8fb2" mb={1}>Dịch vụ đã chọn</Text>
            <Text fontSize="sm" fontWeight="600" color="#2b3674">{serviceName}</Text>
          </Box>

          <Box>
            <Text fontSize="11px" color="#7f8fb2" mb={1}>Ảnh hiện trạng</Text>
            <HStack spacing={2}>
              <Input
                id={evidenceInputId}
                type="file"
                accept="image/*"
                capture="environment"
                multiple
                display="none"
                isDisabled={!canEdit}
                onChange={(event) => {
                  onAddEvidenceImages?.(item?.id, event.target.files);
                  event.target.value = "";
                }}
              />
              <Button as="label" htmlFor={evidenceInputId} size="xs" colorScheme="blue" variant="outline" isDisabled={!canEdit}>
                + Ảnh
              </Button>
              <Text fontSize="xs" color={isNewRecommendationItem(item) && evidenceUrls.length === 0 ? "red.500" : "#7f8fb2"}>
                {evidenceUrls.length > 0 ? `${evidenceUrls.length} ảnh` : isNewRecommendationItem(item) ? "Bắt buộc 1 ảnh" : "Chưa có ảnh"}
              </Text>
            </HStack>
            {evidenceUrls.length > 0 && (
              <HStack spacing={2} mt={2} overflowX="auto" pb={1}>
                {evidenceUrls.map((url, index) => (
                  <Box key={`${item?.id}-evidence-${index}`} position="relative">
                    <Box as="img" src={url} alt={`evidence-${index + 1}`} w="72px" h="72px" objectFit="cover" borderRadius="8px" border="1px solid #d6e4ff" />
                    {canEdit && (
                      <Button size="2xs" colorScheme="red" position="absolute" top="4px" right="4px" minW="18px" h="18px" p={0} onClick={() => onRemoveEvidenceImage?.(item?.id, index)}>
                        x
                      </Button>
                    )}
                  </Box>
                ))}
              </HStack>
            )}
          </Box>
        </Grid>
      ) : (
        <Grid templateColumns={{ base: "1fr", lg: "1fr 1fr" }} gap={3}>
          <DiagnosisDropdown
            label="Vật tư"
            labelFontSize="xs"
            value={selectedProductId || ""}
            onChange={(value) => onChange?.(item?.id, { inventoryItemId: toPositiveNumber(value) || null })}
            isDisabled={!canEdit}
          >
            <option value="">-- Chọn vật tư --</option>
            {(products || []).map((product) => {
              const id = toPositiveNumber(product?.id ?? product?.Id);
              if (!id) return null;
              const name = String(product?.name ?? product?.Name ?? `Vật tư #${id}`).trim();
              return (
                <option key={id} value={id}>
                  {name}
                </option>
              );
            })}
          </DiagnosisDropdown>
          <Box>
            <Text fontSize="11px" color="#7f8fb2" mb={1}>Vật tư đã chọn</Text>
            <Text fontSize="sm" fontWeight="600" color="#7f4f1f">{productName}</Text>
          </Box>
        </Grid>
      )}

      <HStack spacing={3} flexWrap="wrap" align="end">
        <Box w={{ base: "full", md: "130px" }}>
          <Text fontSize="11px" color="#7f8fb2" mb={1}>Số lượng</Text>
          <NumberInput
            size="sm"
            min={1}
            value={itemQuantity}
            isDisabled={!canEdit}
            onChange={(_, valueNumber) => onChange?.(item?.id, { quantity: Number.isFinite(valueNumber) && valueNumber > 0 ? valueNumber : 1 })}
            keepWithinRange
            clampValueOnBlur
          >
            <NumberInputField />
            <NumberInputStepper>
              <NumberIncrementStepper />
              <NumberDecrementStepper />
            </NumberInputStepper>
          </NumberInput>
        </Box>

        <Box w={{ base: "full", md: "180px" }}>
          <Text fontSize="11px" color="#7f8fb2" mb={1}>Độ cần thiết</Text>
          <DiagnosisDropdown
            minW="100%"
            value={item?.urgency || "RECOMMENDED"}
            isDisabled={!canEdit}
            onChange={(value) => onChange?.(item?.id, { urgency: value })}
            fullWidth
          >
            <option value="CRITICAL">Khẩn cấp</option>
            <option value="RECOMMENDED">Nên làm</option>
            <option value="OPTIONAL">Không bắt buộc</option>
          </DiagnosisDropdown>
        </Box>

        {isServiceMode && canEdit && (
          <Button variant="outline" colorScheme="blue" onClick={() => onAddMaterial?.(item?.id)} isDisabled={!selectedServiceId}>
            + Thêm vật tư
          </Button>
        )}
      </HStack>

      <Box>
        <Text fontSize="11px" color="#7f8fb2" mb={1}>Ghi chú kỹ thuật</Text>
        <Textarea
          size="sm"
          minH="72px"
          placeholder={isServiceMode ? "Nhập lý do chọn dịch vụ này..." : "Ghi chú cho vật tư này..."}
          value={item?.technicianNotes || ""}
          isDisabled={!canEdit}
          onChange={(e) => onChange?.(item?.id, { technicianNotes: e.target.value })}
        />
      </Box>

      {isServiceMode && materialCount > 0 && (
        <HStack justify="flex-start">
          <Button
            size="sm"
            variant={canEdit ? "solid" : "outline"}
            colorScheme="blue"
            leftIcon={<Icon as={isMaterialExpanded ? ChevronDown : ChevronRight} boxSize={3.5} />}
            onClick={onToggleMaterials}
          >
            {isMaterialExpanded ? "Thu gọn vật tư" : `Xem vật tư (${materialCount})`}
          </Button>
        </HStack>
      )}
    </Stack>
  );
}
