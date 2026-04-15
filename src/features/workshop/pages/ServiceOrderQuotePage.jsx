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
import "./Workshop.css";

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
  const [removeIdsForClone, setRemoveIdsForClone] = useState([]);
  const [splitBillingDraft, setSplitBillingDraft] = useState({});
  const [cancelReason, setCancelReason] = useState("Khách vội");
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [showMandatoryCloneWarning, setShowMandatoryCloneWarning] = useState(false);
  const [mailSentNotice, setMailSentNotice] = useState("");
  const [collapsedPartGroups, setCollapsedPartGroups] = useState({});

  const autoCreateTriedRef = useRef(false);

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
          classificationLabel: "Dịch vụ chính",
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
        classificationLabel: resolvedParentName ? "Phụ tùng phụ thuộc" : "Phụ tùng",
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
      ["DRAFT", "WAITING_APPROVAL", "APPROVED", "QUOTING", "PENDING_INSURANCE", "SENT_TO_CUSTOMER", "PENDING_CUSTOMER"].includes(quoteStatus) &&
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

      if (sortedQuoteList.length > 0) {
        setActiveQuotationId(Number(readField(sortedQuoteList[0], "id", "Id")));
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
        setActiveQuotationId((prev) => {
          const prevId = Number(prev);
          const stillExists = sortedQuoteList.some((row) => Number(readField(row, "id", "Id")) === prevId);
          return stillExists ? prevId : Number(readField(sortedQuoteList[0], "id", "Id"));
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
  }, [numericOrderId, activeQuotationId, quotations]);

  useEffect(() => {
    if (loading || saving) return undefined;
    const timerId = setInterval(() => {
      void refreshQuotationStatusSilently();
    }, 15000);

    return () => clearInterval(timerId);
  }, [loading, saving, refreshQuotationStatusSilently]);

  useEffect(() => {
    autoCreateTriedRef.current = false;
  }, [numericOrderId]);

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
      const note = String(sendEmailNote || "").trim();
      await serviceOrderApi.submitQuotation(quoteId, {
        actorStaffId: staffId,
        note: note || "Gửi báo giá qua email",
      });
      setActionMessage("Đã gửi email báo giá cho khách hàng.");
      setMailSentNotice("Gửi email báo giá thành công cho khách hàng.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể gửi báo giá.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, sendEmailNote, loadData]);

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
  const canSubmit = ["DRAFT", "WAITING_APPROVAL", "APPROVED", "QUOTING", "PENDING_INSURANCE"].includes(quotationStatus);
  const canCustomerDecision = ["APPROVED", "SENT_TO_CUSTOMER", "PENDING_CUSTOMER"].includes(quotationStatus);
  const canClone = !["CUSTOMER_APPROVED", "CANCELED", "CANCELLED"].includes(quotationStatus);
  const hasCloneReason = String(cloneNote || "").trim().length > 0;

  const isSuperseded = quotationStatus === "SUPERSEDED";
  const customerDisplay = resolveCustomerDisplay(order, customerById);
  const plateDisplay = resolveVehiclePlateDisplay(order, vehicleById);
  const vehicleBrandModelDisplay = resolveVehicleBrandModelDisplay(order, vehicleById, vehicleBrandById, vehicleModelById);
  const splitBillingSummary = useMemo(() => {
    if (!Array.isArray(quotationDetailRows) || quotationDetailRows.length === 0) {
      return {
        insurance: Number(readField(activeQuotation, "totalInsuranceAmount", "TotalInsuranceAmount") || 0),
        customer: Number(readField(activeQuotation, "totalCustomerAmount", "TotalCustomerAmount") || 0),
      };
    }

    let insurance = 0;
    let customer = 0;

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

    return { insurance, customer };
  }, [activeQuotation, quotationDetailRows, splitBillingDraft]);

  return (
    <div className="workshop-table-card reception-quote-page">
      <div className="reception-quote-head">
        <div>
          <h2 className="reception-quote-title">Báo giá thương mại</h2>
          <p className="reception-quote-subtitle">
            Lệnh <strong>{readField(order, "code", "Code") || `#${orderId}`}</strong>
          </p>
        </div>
        <button type="button" className="btn-back" onClick={() => navigate("/workshop/reception")}>Quay lại</button>
      </div>

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang tải dữ liệu báo giá...</p>
        </div>
      ) : (
        <div className="reception-quote-layout">
          <section className="reception-quote-summary-card">
            <article>
              <span>Khách hàng</span>
              <strong>{customerDisplay}</strong>
            </article>
            <article>
              <span>Thông tin xe</span>
              <strong>{plateDisplay}</strong>
              <small style={{ display: "block", marginTop: 4, color: "#5f6f8a", fontWeight: 600 }}>
                {vehicleBrandModelDisplay}
              </small>
            </article>
            <article>
              <span>Trạng thái lệnh</span>
              <strong>{getStatusLabelVi(readField(order, "status", "Status"), readField(order, "status", "Status") || "-")}</strong>
            </article>
            <article>
              <span>Số phiên bản báo giá</span>
              <strong>{quotations.length}</strong>
            </article>
          </section>

          <section className="reception-quote-action-card">
            <h3>Điều phối báo giá</h3>
            <p className="reception-quote-note">Luồng này dành cho CVDV/Lễ tân. Chỉ khi khách duyệt mới ghi nhận vào SOD.</p>
            {error ? <p className="reception-quote-error">{error}</p> : null}
            {actionMessage ? <p className="reception-quote-note">{actionMessage}</p> : null}
            {isSuperseded ? (
              <p className="reception-quote-warning">Phiên bản đang chọn thuộc lịch sử và đã bị thay thế bởi phiên bản mới hơn.</p>
            ) : null}
          </section>

          <section className="reception-quote-action-card">
            <h3>Danh sách phiên bản</h3>
            {quotations.length === 0 ? (
              <div className="reception-quote-action-row">
                <button type="button" className="btn-primary" onClick={handleCreateDraftQuotation} disabled={saving || !staffId}>
                  Tạo báo giá V1
                </button>
              </div>
            ) : (
              <div className="reception-quote-action-row" style={{ flexWrap: "wrap", gap: 8 }}>
                {quotations.map((row) => {
                  const id = Number(readField(row, "id", "Id"));
                  const versionNo = Number(readField(row, "versionNo", "VersionNo") || 0);
                  const status = normalizeStatus(readField(row, "status", "Status"));
                  const statusLabel = getStatusLabelVi(status, status || "-");

                  return (
                    <button
                      key={id}
                      type="button"
                      className={id === Number(activeQuotationId) ? "btn-primary" : "toolbar-action-btn toolbar-action-outline"}
                      onClick={() => setActiveQuotationId(id)}
                      title={`Trạng thái: ${statusLabel}`}
                    >
                      Báo giá V{versionNo} ({statusLabel})
                    </button>
                  );
                })}
              </div>
            )}
          </section>

          {activeQuotation ? (
            <>
              <section className="reception-quote-action-card">
                <h3>Thao tác thương mại</h3>
                <div className="reception-quote-action-row">
                  <button type="button" className="btn-primary" onClick={handleSaveSplitBilling} disabled={saving || !staffId}>
                    Lưu tách bill
                  </button>
                  <button type="button" className="btn-primary" onClick={handleSubmitQuotation} disabled={saving || !canSubmit || !staffId}>
                    Gửi khách hàng
                  </button>
                  <button type="button" className="btn-primary" onClick={handleCloneQuotation} disabled={saving || !canClone || !staffId || !hasCloneReason}>
                    Tạo bản mới
                  </button>
                  <button type="button" className="btn-success" onClick={() => handleCustomerDecision(true)} disabled={saving || !canCustomerDecision || !staffId}>
                    Khách duyệt
                  </button>
                  <button type="button" className="btn-danger" onClick={() => handleCustomerDecision(false)} disabled={saving || !canCustomerDecision || !staffId}>
                    Khách từ chối
                  </button>
                  <button type="button" className="btn-danger" onClick={() => setShowCancelModal(true)} disabled={saving || !staffId}>
                    Hủy toàn bộ phiếu
                  </button>
                </div>
                {mailSentNotice ? <p className="reception-quote-success">{mailSentNotice}</p> : null}

                <div className="reception-quote-approval-note">
                  <div className="reception-quote-approval-note-head">
                    <span className="reception-quote-approval-note-title">Ghi chú gửi email cho khách</span>
                    <small className="reception-quote-approval-note-hint">Đoạn này sẽ xuất hiện trong email báo giá.</small>
                  </div>
                  <textarea
                    className="reception-quote-approval-note-input"
                    rows={2}
                    value={sendEmailNote}
                    onChange={(event) => setSendEmailNote(event.target.value)}
                    placeholder="Ví dụ: Em đã áp dụng giảm 10% tiền công như đã trao đổi."
                    disabled={saving}
                  />
                </div>

                <div className="reception-quote-approval-note">
                  <div className="reception-quote-approval-note-head">
                    <span className="reception-quote-approval-note-title">Ghi chú clone phiên bản</span>
                    <small className="reception-quote-approval-note-hint">Ghi rõ lý do thay đổi để theo dõi V1/V2/V3.</small>
                  </div>
                  <textarea
                    className="reception-quote-approval-note-input"
                    rows={2}
                    value={cloneNote}
                    onChange={(event) => setCloneNote(event.target.value)}
                    placeholder="Ví dụ: Khách muốn bỏ hạng mục làm đẹp"
                    disabled={saving}
                  />
                  {!hasCloneReason ? <small className="reception-quote-error">Vui lòng nhập lý do để bật chức năng clone.</small> : null}
                </div>

                <div className="reception-quote-summary-card" style={{ marginTop: 12 }}>
                  <article>
                    <span>Trạng thái báo giá</span>
                    <strong>{getStatusLabelVi(quotationStatus, quotationStatus || "-")}</strong>
                  </article>
                  <article>
                    <span>Duyệt kỹ thuật</span>
                    <strong>{readField(activeQuotation, "teamLeaderApprovalStatus", "TeamLeaderApprovalStatus") || "-"}</strong>
                  </article>
                  <article>
                    <span>Duyệt quản đốc</span>
                    <strong>{readField(activeQuotation, "finalApprovalStatus", "FinalApprovalStatus") || "-"}</strong>
                  </article>
                  <article>
                    <span>Khách quyết định</span>
                    <strong>{readField(activeQuotation, "customerDecisionStatus", "CustomerDecisionStatus") || "-"}</strong>
                  </article>
                  <article>
                    <span>Cập nhật</span>
                    <strong>{formatDateTime(readField(activeQuotation, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt"))}</strong>
                  </article>
                  <article>
                    <span>Bảo hiểm đền</span>
                    <strong>{formatMoney(splitBillingSummary.insurance)} đ</strong>
                  </article>
                  <article>
                    <span>Khách tự trả</span>
                    <strong>{formatMoney(splitBillingSummary.customer)} đ</strong>
                  </article>
                </div>
                {(() => {
                  const warnings = readField(activeQuotation, "warnings", "Warnings");
                  return Array.isArray(warnings) && warnings.length > 0 ? (
                    <div className="reception-quote-warning" style={{ marginTop: 10 }}>
                      {warnings.join(" | ")}
                    </div>
                  ) : null;
                })()}
              </section>

              <section className="reception-quote-table-card">
                <div className="reception-quote-table-head">
                  <h3>Chi tiết báo giá</h3>
                  <strong>{formatMoney(readField(activeQuotation, "finalAmount", "FinalAmount"))} d</strong>
                </div>

                {quotationDetailRows.length > 0 ? (
                  <div className="workshop-table-card">
                    <table className="admin-table quoteflow-fixed-table">
                      <colgroup>
                        <col className="quoteflow-col-item" />
                        <col className="quoteflow-col-type" />
                        <col className="quoteflow-col-qty" />
                        <col className="quoteflow-col-price" />
                        <col className="quoteflow-col-total" />
                        <col className="quoteflow-col-insurance" />
                        <col className="quoteflow-col-customer" />
                        <col className="quoteflow-col-clone" />
                      </colgroup>
                      <thead>
                        <tr>
                          <th>Hạng mục</th>
                          <th>Phân loại</th>
                          <th>SL</th>
                          <th>Đơn giá</th>
                          <th>Tổng tiền</th>
                          <th>Bảo hiểm đền</th>
                          <th>Khách bù</th>
                          <th className="quoteflow-col-clone">Loại khỏi bản mới</th>
                        </tr>
                      </thead>
                      <tbody>
                        {quotationDetailRows.map((item) => {
                          const groupKey = String(item.groupKey);
                          const hasDependentParts = (partCountByGroup.get(groupKey) || 0) > 0;
                          const isPartCollapsed = hasDependentParts && Boolean(collapsedPartGroups[groupKey]);
                          if (item.isDependentPart && isPartCollapsed) {
                            return null;
                          }
                          const row = item.row;
                          const detailId = Number(readField(row, "id", "Id"));
                          const name = String(readField(row, "itemName", "ItemName") || "-");
                          const qty = Number(readField(row, "quantity", "Quantity") || 1);
                          const unitPrice = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime") || 0);
                          const finalAmount = Number(readField(row, "finalAmount", "FinalAmount") || qty * unitPrice);
                          const insuranceRaw = readField(
                            row,
                            "insuranceCoveredAmount",
                            "InsuranceCoveredAmount",
                            "insuranceRequestedAmount",
                            "InsuranceRequestedAmount",
                          );
                          const originalInsuranceCovered =
                            insuranceRaw === null || insuranceRaw === undefined || String(insuranceRaw).trim() === ""
                              ? finalAmount
                              : Number(insuranceRaw);
                          const insuranceCoveredDraft = Number(splitBillingDraft[detailId]);
                          const insuranceCovered = Number.isFinite(insuranceCoveredDraft)
                            ? Math.min(Math.max(insuranceCoveredDraft, 0), Math.max(0, finalAmount))
                            : originalInsuranceCovered;
                          const customerCovered = Math.max(0, finalAmount - insuranceCovered);
                          const isMandatory = Boolean(readField(row, "isMandatory", "IsMandatory"));

                          return (
                            <tr
                              key={detailId}
                              className={[
                                "quoteflow-group-row",
                                item.isMainService ? "quoteflow-row-service" : item.isDependentPart ? "quoteflow-row-part" : "",
                                item.isGroupStart ? "quoteflow-group-start" : "",
                                item.isGroupEnd ? "quoteflow-group-end" : "",
                              ]
                                .filter(Boolean)
                                .join(" ")}
                            >
                              <td>
                                <div className={item.isDependentPart ? "quoteflow-item-name quoteflow-item-name-part" : "quoteflow-item-name"}>
                                  {item.isDependentPart ? `└─ ${name}` : name}
                                </div>
                                {item.isMainService && hasDependentParts ? (
                                  <button
                                    type="button"
                                    className="quoteflow-part-toggle"
                                    onClick={() =>
                                      setCollapsedPartGroups((prev) => ({
                                        ...prev,
                                        [groupKey]: !prev[groupKey],
                                      }))
                                    }
                                  >
                                    {isPartCollapsed ? `Hiện phụ tùng (${partCountByGroup.get(groupKey) || 0})` : "Thu gọn phụ tùng"}
                                  </button>
                                ) : null}
                                {isMandatory ? <span className="quoteflow-mandatory-chip">Bắt buộc</span> : null}
                              </td>
                              <td>{item.classificationLabel}</td>
                              <td>{qty}</td>
                              <td>{formatMoney(unitPrice)}</td>
                              <td>{formatMoney(finalAmount)}</td>
                              <td>
                                <input
                                  type="number"
                                  min={0}
                                  max={Math.max(0, finalAmount)}
                                  value={String(insuranceCovered)}
                                  onChange={(event) => handleSplitInsuranceChange(detailId, finalAmount, event.target.value)}
                                  disabled={saving || !staffId}
                                  style={{ width: 140, padding: "6px 8px", borderRadius: 8, border: "1px solid #cfd8e6" }}
                                />
                              </td>
                              <td>{formatMoney(customerCovered)}</td>
                              <td className="quoteflow-col-clone-cell">
                                <label className="quoteflow-clone-check">
                                  <input
                                    type="checkbox"
                                    checked={removeIdsForClone.includes(detailId)}
                                    onChange={() => handleToggleCloneRemove(detailId)}
                                    disabled={saving || !canClone}
                                  />
                                </label>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p className="reception-quote-note">Phiên bản này chưa có dòng chi tiết.</p>
                )}
              </section>
            </>
          ) : null}
        </div>
      )}

      {showMandatoryCloneWarning ? (
        <div className="quoteflow-modal-backdrop">
          <div className="quoteflow-modal">
            <h4>Cảnh báo hạng mục bắt buộc</h4>
            <p>
              Bạn đang loại bỏ hạng mục có cờ <strong>Bắt buộc (An toàn)</strong>. Hệ thống sẽ phát sinh yêu cầu ngoại lệ cho quản đốc duyệt.
            </p>
            <div className="reception-quote-action-row">
              <button type="button" className="btn-danger" onClick={handleCloneQuotation} disabled={saving || !staffId}>
                Tiếp tục clone
              </button>
              <button type="button" className="btn-primary" onClick={() => setShowMandatoryCloneWarning(false)} disabled={saving}>
                Quay lại
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {showCancelModal ? (
        <div className="quoteflow-modal-backdrop">
          <div className="quoteflow-modal">
            <h4>Hủy toàn bộ phiếu</h4>
            <p>Chọn lý do hủy để phục vụ thống kê CSKH.</p>
            <select value={cancelReason} onChange={(event) => setCancelReason(event.target.value)} disabled={saving}>
              <option value="Khách vội">Khách vội</option>
              <option value="Chê đắt">Chê đắt</option>
              <option value="Đổi ý">Đổi ý</option>
              <option value="Có việc gấp">Có việc gấp</option>
            </select>
            <div className="reception-quote-action-row">
              <button type="button" className="btn-danger" onClick={handleCancelAll} disabled={saving || !staffId}>
                Xác nhận hủy
              </button>
              <button type="button" className="btn-primary" onClick={() => setShowCancelModal(false)} disabled={saving}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
