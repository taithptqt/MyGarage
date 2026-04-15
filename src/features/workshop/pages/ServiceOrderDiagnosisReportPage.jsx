import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStandardServices } from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { getAllStaff } from "@features/admin/api/staffApi";
import { parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import { getStatusLabelVi } from "@shared/core/utils/statusLabelUtils";
import "./ServiceOrderDiagnosisReportPage.css";

function firstText(...values) {
  for (const value of values) {
    const text = String(value || "").trim();
    if (text) return text;
  }
  return "";
}

function toPositiveNumber(...values) {
  for (const value of values) {
    const parsed = Number(value);
    if (Number.isFinite(parsed) && parsed > 0) return parsed;
  }
  return 0;
}

function formatDateTimeValue(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";
  return parsed.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function formatMoney(value) {
  const amount = Number(value || 0);
  return `${amount.toLocaleString("vi-VN")}Ä‘`;
}

function normalizeImageUrls(value) {
  if (Array.isArray(value)) {
    return value.map((item) => String(item || "").trim()).filter(Boolean);
  }
  const text = String(value || "").trim();
  if (!text) return [];
  try {
    const parsed = JSON.parse(text);
    if (Array.isArray(parsed)) return parsed.map((item) => String(item || "").trim()).filter(Boolean);
  } catch {
    // Use text split below
  }
  return text
    .split(/[,\n]/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

function resolveDiagnosisTimestamp(diagnosis) {
  const candidates = [
    diagnosis?.diagnosedAt,
    diagnosis?.DiagnosedAt,
    diagnosis?.updatedAt,
    diagnosis?.UpdatedAt,
    diagnosis?.createdAt,
    diagnosis?.CreatedAt,
  ];
  for (const candidate of candidates) {
    const time = new Date(candidate || 0).getTime();
    if (Number.isFinite(time) && time > 0) return time;
  }
  return 0;
}

function resolveSeverityLabel(rawSeverity) {
  const normalized = String(rawSeverity || "").trim().toUpperCase();
  if (!normalized) return "--";
  const labels = {
    LOW: "Tháº¥p",
    MEDIUM: "Trung bÃ¬nh",
    HIGH: "Cao",
    CRITICAL: "NghiÃªm trá»ng",
  };
  return labels[normalized] || normalized;
}

function isJsonLikeText(value) {
  const text = String(value || "").trim();
  if (!text) return false;
  if (!((text.startsWith("{") && text.endsWith("}")) || (text.startsWith("[") && text.endsWith("]")))) {
    return false;
  }
  try {
    const parsed = JSON.parse(text);
    return typeof parsed === "object" && parsed !== null;
  } catch {
    return false;
  }
}

function sanitizeRootCauseText(value) {
  if (value === null || value === undefined) return "";
  if (typeof value === "object") return "";
  const text = String(value).trim();
  if (!text) return "";
  if (isJsonLikeText(text)) return "";
  return text;
}

function sanitizeSymptomText(value) {
  if (value === null || value === undefined) return "";
  if (typeof value === "object") return "";
  const text = String(value).trim();
  if (!text) return "";
  if (isJsonLikeText(text)) return "";
  return text;
}

function isLaborOrService(detail) {
  const itemType = String(detail?.itemType ?? detail?.ItemType ?? "").trim().toUpperCase();
  const standardServiceId = toPositiveNumber(detail?.standardServiceId, detail?.StandardServiceId);
  if (itemType === "PART" || itemType === "PRODUCT") return false;
  return itemType === "LABOR" || itemType === "SERVICE" || itemType === "STANDARD_SERVICE" || standardServiceId > 0;
}

function hasDiagnosisRecommendationLink(detail) {
  const recommendationId = toPositiveNumber(
    detail?.diagnosisRecommendationId,
    detail?.DiagnosisRecommendationId,
    detail?.diagnoseRecommendationId,
    detail?.DiagnoseRecommendationId
  );
  return recommendationId > 0;
}

function normalizeRecommendationItemType(raw) {
  const token = String(raw || "").trim().toUpperCase();
  if (token.includes("PART") || token.includes("MATERIAL") || token.includes("PRODUCT")) return "PART";
  return "SERVICE";
}

function extractServiceName(service) {
  return firstText(service?.name, service?.Name, service?.serviceName, service?.ServiceName);
}

function extractProductName(product) {
  return firstText(product?.name, product?.Name, product?.productName, product?.ProductName);
}

function getCollectionLength(value) {
  if (Array.isArray(value)) return value.length;
  if (!value || typeof value !== "object") return 0;
  const candidates = [value.items, value.Items, value.data, value.Data, value.rows, value.Rows, value.results, value.Results];
  for (const item of candidates) {
    if (Array.isArray(item)) return item.length;
  }
  return 0;
}

export default function ServiceOrderDiagnosisReportPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const numericOrderId = Number(orderId);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [order, setOrder] = useState(null);
  const [diagnoses, setDiagnoses] = useState([]);
  const [orderDetails, setOrderDetails] = useState([]);
  const [recommendations, setRecommendations] = useState([]);
  const [services, setServices] = useState([]);
  const [products, setProducts] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [quotationCount, setQuotationCount] = useState(0);
  const [collapsedPartsByService, setCollapsedPartsByService] = useState({});

  const loadData = useCallback(async () => {
    if (!Number.isFinite(numericOrderId) || numericOrderId <= 0) {
      setError("MÃ£ lá»‡nh sá»­a chá»¯a khÃ´ng há»£p lá»‡.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const [orderRes, diagnosesRes, detailsRes, quotationsRes, servicesRes, productsRes, staffsRes] = await Promise.all([
        serviceOrderApi.getById(numericOrderId),
        serviceOrderApi.getDiagnoses(numericOrderId),
        serviceOrderApi.getDetails(numericOrderId).catch(() => []),
        serviceOrderApi.getQuotationsByServiceOrder(numericOrderId).catch(() => []),
        getAllStandardServices().catch(() => []),
        inventoryApi.getProducts().catch(() => []),
        getAllStaff().catch(() => []),
      ]);

      const diagnosisList = Array.isArray(diagnosesRes) ? diagnosesRes : [];
      const latestDiagnosis = [...diagnosisList].sort((a, b) => resolveDiagnosisTimestamp(b) - resolveDiagnosisTimestamp(a))[0] || null;
      const latestDiagnosisId = toPositiveNumber(latestDiagnosis?.id, latestDiagnosis?.Id);

      const recommendationsRes = latestDiagnosisId
        ? await serviceOrderApi.getDiagnoseRecommendations(latestDiagnosisId).catch(() => [])
        : [];

      setOrder(orderRes || null);
      setDiagnoses(diagnosisList);
      setOrderDetails(Array.isArray(detailsRes) ? detailsRes : []);
      setRecommendations(Array.isArray(recommendationsRes) ? recommendationsRes : []);
      setServices(Array.isArray(servicesRes) ? servicesRes : []);
      setProducts(Array.isArray(productsRes) ? productsRes : []);
      setStaffs(Array.isArray(staffsRes) ? staffsRes : []);
      setQuotationCount(getCollectionLength(quotationsRes));
    } catch (err) {
      setError(err?.message || "KhÃ´ng thá»ƒ táº£i trang káº¿t quáº£ chuáº©n Ä‘oÃ¡n.");
    } finally {
      setLoading(false);
    }
  }, [numericOrderId]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const latestDiagnosis = useMemo(() => {
    if (!Array.isArray(diagnoses) || diagnoses.length === 0) return null;
    return [...diagnoses].sort((a, b) => resolveDiagnosisTimestamp(b) - resolveDiagnosisTimestamp(a))[0] || null;
  }, [diagnoses]);

  const serviceLookup = useMemo(() => {
    const map = new Map();
    (Array.isArray(services) ? services : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id);
      if (id) map.set(id, item);
    });
    return map;
  }, [services]);

  const productLookup = useMemo(() => {
    const map = new Map();
    (Array.isArray(products) ? products : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id, item?.productId, item?.ProductId);
      if (id) map.set(id, item);
    });
    return map;
  }, [products]);

  const staffLookup = useMemo(() => {
    const map = new Map();
    (Array.isArray(staffs) ? staffs : []).forEach((item) => {
      const id = toPositiveNumber(item?.id, item?.Id, item?.staffId, item?.StaffId);
      if (id) map.set(id, item);
    });
    return map;
  }, [staffs]);

  const parsedCustomerNotes = useMemo(
    () => parseCustomerNotes(order?.customerNotes ?? order?.CustomerNotes),
    [order?.CustomerNotes, order?.customerNotes]
  );

  const customerIssueText = useMemo(
    () => firstText(parsedCustomerNotes.mainNotes, ""),
    [parsedCustomerNotes.mainNotes]
  );

  const diagnosisRootCauseText = useMemo(
    () => sanitizeRootCauseText(latestDiagnosis?.rootCause ?? latestDiagnosis?.RootCause),
    [latestDiagnosis?.RootCause, latestDiagnosis?.rootCause]
  );

  const diagnosisSeverityText = useMemo(
    () => resolveSeverityLabel(latestDiagnosis?.severity ?? latestDiagnosis?.Severity),
    [latestDiagnosis?.Severity, latestDiagnosis?.severity]
  );

  const diagnosisStatusLabel = useMemo(
    () => getStatusLabelVi(latestDiagnosis?.status ?? latestDiagnosis?.Status),
    [latestDiagnosis?.Status, latestDiagnosis?.status]
  );

  const diagnosisUpdatedAt = useMemo(
    () =>
      latestDiagnosis?.diagnosedAt ||
      latestDiagnosis?.DiagnosedAt ||
      latestDiagnosis?.updatedAt ||
      latestDiagnosis?.UpdatedAt ||
      latestDiagnosis?.createdAt ||
      latestDiagnosis?.CreatedAt ||
      null,
    [
      latestDiagnosis?.CreatedAt,
      latestDiagnosis?.DiagnosedAt,
      latestDiagnosis?.UpdatedAt,
      latestDiagnosis?.createdAt,
      latestDiagnosis?.diagnosedAt,
      latestDiagnosis?.updatedAt,
    ]
  );

  const diagnosedTechnicianLabel = useMemo(() => {
    const explicitName = firstText(
      latestDiagnosis?.technicianName,
      latestDiagnosis?.TechnicianName,
      latestDiagnosis?.diagnosedByName,
      latestDiagnosis?.DiagnosedByName
    );
    if (explicitName) return explicitName;
    const technicianId = toPositiveNumber(latestDiagnosis?.technicianId, latestDiagnosis?.TechnicianId);
    if (!technicianId) return "";
    const matched = staffLookup.get(technicianId);
    return firstText(matched?.fullName, matched?.FullName, `NhÃ¢n sá»± #${technicianId}`);
  }, [
    latestDiagnosis?.DiagnosedByName,
    latestDiagnosis?.TechnicianId,
    latestDiagnosis?.TechnicianName,
    latestDiagnosis?.diagnosedByName,
    latestDiagnosis?.technicianId,
    latestDiagnosis?.technicianName,
    staffLookup,
  ]);

  const teamLeaderLabel = useMemo(() => {
    const explicitName = firstText(
      latestDiagnosis?.assignedTeamLeaderName,
      latestDiagnosis?.AssignedTeamLeaderName,
      latestDiagnosis?.teamLeaderName,
      latestDiagnosis?.TeamLeaderName
    );
    if (explicitName) return explicitName;
    const leaderId = toPositiveNumber(
      latestDiagnosis?.assignedTeamLeaderId,
      latestDiagnosis?.AssignedTeamLeaderId,
      latestDiagnosis?.teamLeaderId,
      latestDiagnosis?.TeamLeaderId
    );
    return leaderId > 0 ? `NhÃ¢n sá»± #${leaderId}` : "";
  }, [
    latestDiagnosis?.AssignedTeamLeaderId,
    latestDiagnosis?.AssignedTeamLeaderName,
    latestDiagnosis?.TeamLeaderId,
    latestDiagnosis?.TeamLeaderName,
    latestDiagnosis?.assignedTeamLeaderId,
    latestDiagnosis?.assignedTeamLeaderName,
    latestDiagnosis?.teamLeaderId,
    latestDiagnosis?.teamLeaderName,
  ]);

  const selectedServiceRows = useMemo(() => {
    const candidates = (Array.isArray(orderDetails) ? orderDetails : []).filter(
      (detail) => isLaborOrService(detail) && !hasDiagnosisRecommendationLink(detail)
    );
    return candidates.map((detail) => {
      const standardServiceId = toPositiveNumber(detail?.standardServiceId, detail?.StandardServiceId);
      const fallbackItemId = toPositiveNumber(detail?.itemId, detail?.ItemId);
      const service = serviceLookup.get(standardServiceId || fallbackItemId);
      const resolvedName = firstText(
        detail?.itemName,
        detail?.ItemName,
        extractServiceName(service),
        standardServiceId ? `Dá»‹ch vá»¥ #${standardServiceId}` : "",
        fallbackItemId ? `Dá»‹ch vá»¥ #${fallbackItemId}` : "",
        "Dá»‹ch vá»¥"
      );

      return {
        id:
          toPositiveNumber(detail?.id, detail?.Id, detail?.standardServiceId, detail?.StandardServiceId, detail?.itemId, detail?.ItemId) ||
          `svc-${resolvedName}`,
        name: resolvedName,
        payerLabel: firstText(detail?.payerTypeDisplayName, detail?.PayerTypeDisplayName, detail?.payerType, detail?.PayerType, ""),
        amount: Number(detail?.finalAmount ?? detail?.FinalAmount ?? detail?.unitPriceAtTime ?? detail?.UnitPriceAtTime ?? 0),
        quantity: Number(detail?.quantity ?? detail?.Quantity ?? 1),
        note: firstText(detail?.description, detail?.Description, detail?.note, detail?.Note, ""),
      };
    });
  }, [orderDetails, serviceLookup]);

  const recommendedServiceGroups = useMemo(() => {
    const source = Array.isArray(recommendations) ? recommendations : [];
    const childrenByParentId = new Map();
    source.forEach((item) => {
      const parentId = toPositiveNumber(item?.parentRecommendationId, item?.ParentRecommendationId);
      if (!parentId) return;
      if (!childrenByParentId.has(parentId)) childrenByParentId.set(parentId, []);
      childrenByParentId.get(parentId).push(item);
    });

    return source
      .filter((item) => !toPositiveNumber(item?.parentRecommendationId, item?.ParentRecommendationId))
      .filter((item) => normalizeRecommendationItemType(item?.itemType ?? item?.ItemType) === "SERVICE")
      .map((item) => {
        const recommendationId = toPositiveNumber(item?.id, item?.Id);
        const standardServiceId = toPositiveNumber(item?.standardServiceId, item?.StandardServiceId);
        const service = standardServiceId ? serviceLookup.get(standardServiceId) : null;
        const label =
          firstText(
            item?.itemName,
            item?.ItemName,
            item?.standardServiceName,
            item?.StandardServiceName,
            item?.serviceName,
            item?.ServiceName,
            extractServiceName(service),
            item?.inventoryItemName,
            item?.InventoryItemName
          ) ||
          `Dá»‹ch vá»¥ #${standardServiceId || recommendationId || "N/A"}`;

        const quantityRaw = Number(item?.quantity ?? item?.Quantity ?? 1);
        const quantity = Number.isFinite(quantityRaw) && quantityRaw > 0 ? quantityRaw : 1;
        const technicianNotes = firstText(item?.technicianNotes, item?.TechnicianNotes, "");
        const evidenceUrls = normalizeImageUrls(item?.evidenceMediaUrls ?? item?.EvidenceMediaUrls);
        const childItems = childrenByParentId.get(recommendationId) || [];
        const requiredParts = childItems
          .filter((child) => normalizeRecommendationItemType(child?.itemType ?? child?.ItemType) === "PART")
          .map((child, index) => {
            const partQtyRaw = Number(child?.quantity ?? child?.Quantity ?? 1);
            const inventoryItemId = toPositiveNumber(child?.inventoryItemId, child?.InventoryItemId);
            const product = inventoryItemId ? productLookup.get(inventoryItemId) : null;
            return {
              id: toPositiveNumber(child?.id, child?.Id) || `${recommendationId || "svc"}-part-${index}`,
              label: firstText(
                child?.itemName,
                child?.ItemName,
                child?.inventoryItemName,
                child?.InventoryItemName,
                extractProductName(product),
                child?.serviceName,
                child?.ServiceName,
                "Phá»¥ tÃ¹ng chÆ°a Ä‘áº·t tÃªn"
              ),
              quantity: Number.isFinite(partQtyRaw) && partQtyRaw > 0 ? partQtyRaw : 1,
              note: firstText(child?.technicianNotes, child?.TechnicianNotes, ""),
            };
          });

        return {
          id: recommendationId || `rec-${label}`,
          serviceLabel: label,
          quantity,
          technicianNotes,
          evidenceUrls,
          requiredParts,
        };
      });
  }, [productLookup, recommendations, serviceLookup]);

  useEffect(() => {
    setCollapsedPartsByService((prev) => {
      const next = { ...prev };
      const activeKeys = new Set();
      recommendedServiceGroups.forEach((group, index) => {
        if ((group.requiredParts || []).length > 0) {
          const key = String(group.id || index);
          activeKeys.add(key);
          if (!(key in next)) next[key] = true;
        }
      });
      Object.keys(next).forEach((key) => {
        if (!activeKeys.has(key)) delete next[key];
      });
      return next;
    });
  }, [recommendedServiceGroups]);

  const receptionDiagnosisNote = useMemo(
    () => firstText(latestDiagnosis?.assignmentNote, latestDiagnosis?.AssignmentNote, ""),
    [latestDiagnosis?.AssignmentNote, latestDiagnosis?.assignmentNote]
  );

  const finalApprovalStatus = String(
    latestDiagnosis?.finalApprovalStatus ?? latestDiagnosis?.FinalApprovalStatus ?? ""
  )
    .trim()
    .toUpperCase();
  const isForemanApproved =
    finalApprovalStatus === "APPROVED" ||
    toPositiveNumber(latestDiagnosis?.approvedByForemanId, latestDiagnosis?.ApprovedByForemanId) > 0 ||
    Boolean(latestDiagnosis?.approvedByForemanAt ?? latestDiagnosis?.ApprovedByForemanAt);
  const canShowQuoteButton = isForemanApproved || quotationCount > 0;

  const goBack = () => {
    if (location.state?.from) {
      navigate(location.state.from);
      return;
    }
    navigate("/workshop/reception");
  };

  if (loading) {
    return (
      <div className="workshop-table-card diagnosis-report-shell">
        <div className="diagnosis-report-state">Äang táº£i trang chuáº©n Ä‘oÃ¡n...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="workshop-table-card diagnosis-report-shell">
        <div className="diagnosis-report-state diagnosis-report-state-error">
          <p>{error}</p>
          <button type="button" className="diagnosis-report-btn diagnosis-report-btn-primary" onClick={loadData}>
            Táº£i láº¡i
          </button>
        </div>
      </div>
    );
  }

  if (!latestDiagnosis) {
    return (
      <div className="workshop-table-card diagnosis-report-shell">
        <div className="diagnosis-report-state">
          <p>Lá»‡nh nÃ y chÆ°a cÃ³ káº¿t quáº£ chuáº©n Ä‘oÃ¡n.</p>
          <button type="button" className="diagnosis-report-btn diagnosis-report-btn-primary" onClick={goBack}>
            Quay láº¡i danh sÃ¡ch lá»‡nh
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="workshop-table-card diagnosis-report-shell">
      <header className="diagnosis-report-header">
        <div>
          <p className="diagnosis-report-eyebrow">Lá»‡nh sá»­a chá»¯a #{order?.id || numericOrderId}</p>
          <h1>Káº¿t quáº£ chuáº©n Ä‘oÃ¡n lá»… tÃ¢n</h1>
          <p className="diagnosis-report-subtitle">{order?.code ? `MÃ£ lá»‡nh: ${order.code}` : "Theo dÃµi chi tiáº¿t chuáº©n Ä‘oÃ¡n."}</p>
        </div>
        <div className="diagnosis-report-action-row">
          {canShowQuoteButton ? (
            <button
              type="button"
              className="diagnosis-report-btn diagnosis-report-btn-accent"
              onClick={() => navigate(`/workshop/reception/orders/${numericOrderId}/quote`)}
            >
              Xem bÃ¡o giÃ¡
            </button>
          ) : null}
          <button type="button" className="diagnosis-report-btn diagnosis-report-btn-primary" onClick={goBack}>
            Quay láº¡i
          </button>
        </div>
      </header>

      <section className="diagnosis-report-chip-grid">
        <article className="diagnosis-report-chip">
          <span>Tráº¡ng thÃ¡i chuáº©n Ä‘oÃ¡n</span>
          <strong>{diagnosisStatusLabel}</strong>
        </article>
        <article className="diagnosis-report-chip">
          <span>Má»©c Ä‘á»™</span>
          <strong>{diagnosisSeverityText}</strong>
        </article>
        <article className="diagnosis-report-chip">
          <span>Thá»i Ä‘iá»ƒm cáº­p nháº­t</span>
          <strong>{formatDateTimeValue(diagnosisUpdatedAt)}</strong>
        </article>
        <article className="diagnosis-report-chip">
          <span>Tráº¡ng thÃ¡i lá»‡nh</span>
          <strong>{getStatusLabelVi(order?.status ?? order?.Status)}</strong>
        </article>
      </section>

      <section className="diagnosis-report-grid">
        <article className="diagnosis-report-card">
          <h2>ThÃ´ng tin tiáº¿p nháº­n</h2>
          <div className="diagnosis-report-field">
            <span>Váº¥n Ä‘á» khÃ¡ch mÃ´ táº£</span>
            <strong>{customerIssueText}</strong>
          </div>
          <div className="diagnosis-report-field">
            <span>NguyÃªn nhÃ¢n gá»‘c</span>
            <strong>{diagnosisRootCauseText}</strong>
          </div>
          <div className="diagnosis-report-field">
            <span>Ghi chÃº Ä‘iá»u phá»‘i lá»… tÃ¢n</span>
            <strong>{receptionDiagnosisNote}</strong>
          </div>
        </article>

        <article className="diagnosis-report-card">
          <h2>NhÃ¢n sá»± phá»¥ trÃ¡ch chuáº©n Ä‘oÃ¡n</h2>
          <div className="diagnosis-report-field">
            <span>Thá»£ Ä‘Ã£ chuáº©n Ä‘oÃ¡n</span>
            <strong>{diagnosedTechnicianLabel}</strong>
          </div>
          <div className="diagnosis-report-field">
            <span>Tá»• trÆ°á»Ÿng phá»¥ trÃ¡ch</span>
            <strong>{teamLeaderLabel}</strong>
          </div>
        </article>
      </section>

      <section className="diagnosis-report-grid diagnosis-report-grid-lower">
        <article className="diagnosis-report-card">
          <h2>Dá»‹ch vá»¥ khÃ¡ch Ä‘Ã£ chá»n</h2>
          {selectedServiceRows.length === 0 ? (
            <p className="diagnosis-report-empty">KhÃ´ng cÃ³ dá»¯ liá»‡u dá»‹ch vá»¥ khÃ¡ch chá»n.</p>
          ) : (
            <div className="diagnosis-report-table-wrap">
              <table className="diagnosis-report-table diagnosis-selected-service-table">
                <colgroup>
                  <col className="diagnosis-selected-service-col-name" />
                  <col className="diagnosis-selected-service-col-qty" />
                </colgroup>
                <thead>
                  <tr>
                    <th>Dá»‹ch vá»¥</th>
                    <th>SL</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedServiceRows.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <div className="diagnosis-report-cell-main">{row.name}</div>
                      </td>
                      <td>{row.quantity}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <article className="diagnosis-report-card">
          <h2>Dá»‹ch vá»¥ vÃ  phá»¥ tÃ¹ng thá»£ Ä‘á» xuáº¥t</h2>
          {recommendedServiceGroups.length === 0 ? (
            <p className="diagnosis-report-empty">ChÆ°a cÃ³ Ä‘á» xuáº¥t tá»« thá»£ chuáº©n Ä‘oÃ¡n.</p>
          ) : (
            <div className="diagnosis-recommendation-group-list">
              {recommendedServiceGroups.map((group, index) => {
                const groupKey = String(group.id || index);
                const isCollapsed = Boolean(collapsedPartsByService[groupKey]);
                return (
                  <article key={group.id || index} className="diagnosis-recommendation-group">
                    <header className="diagnosis-recommendation-group-header">
                      <h3>{group.serviceLabel}</h3>
                      <div className="diagnosis-recommendation-group-head-actions">
                        {group.requiredParts.length > 0 ? (
                          <button
                            type="button"
                            className="diagnosis-recommendation-toggle"
                            onClick={() =>
                              setCollapsedPartsByService((prev) => ({
                                ...prev,
                                [groupKey]: !prev[groupKey],
                              }))
                            }
                          >
                            {isCollapsed ? `Hiện phụ tùng (${group.requiredParts.length})` : "Thu gọn phụ tùng"}
                          </button>
                        ) : null}
                        <span>Số lượng: {group.quantity}</span>
                      </div>
                    </header>

                    <div className="diagnosis-recommendation-group-body">
                      <section className="diagnosis-recommendation-section">
                        <h4>Ghi chú của thợ cho dịch vụ này</h4>
                        <p>{group.technicianNotes || "Không có ghi chú."}</p>
                      </section>

                      <section className="diagnosis-recommendation-section">
                        <h4>Hình ảnh dịch vụ</h4>
                        {group.evidenceUrls.length === 0 ? (
                          <p>Không có hình ảnh.</p>
                        ) : (
                          <div className="diagnosis-recommendation-image-grid">
                            {group.evidenceUrls.map((url, imageIndex) => (
                              <a
                                key={`${group.id || index}-${url}-${imageIndex}`}
                                href={url}
                                target="_blank"
                                rel="noreferrer"
                                className="diagnosis-recommendation-image-tile"
                              >
                                <img src={url} alt={`Ảnh dịch vụ ${index + 1}.${imageIndex + 1}`} loading="lazy" />
                              </a>
                            ))}
                          </div>
                        )}
                      </section>

                      <section className="diagnosis-recommendation-section diagnosis-recommendation-parts-field">
                        <h4>Phụ tùng / vật tư</h4>
                        {group.requiredParts.length === 0 ? (
                          <p>Không có phụ tùng / vật tư đi kèm.</p>
                        ) : isCollapsed ? (
                          <p>Đang thu gọn danh sách phụ tùng.</p>
                        ) : (
                          <div className="diagnosis-recommendation-parts-wrap">
                            <table className="diagnosis-report-table diagnosis-recommendation-parts-table">
                              <colgroup>
                                <col className="diagnosis-recommendation-col-name" />
                                <col className="diagnosis-recommendation-col-qty" />
                                <col className="diagnosis-recommendation-col-note" />
                              </colgroup>
                              <thead>
                                <tr>
                                  <th>Phụ tùng</th>
                                  <th>Số lượng</th>
                                  <th>Ghi chú</th>
                                </tr>
                              </thead>
                              <tbody>
                                {group.requiredParts.map((part) => (
                                  <tr key={part.id}>
                                    <td>{part.label}</td>
                                    <td>{part.quantity}</td>
                                    <td>{part.note || ""}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        )}
                      </section>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </article>
      </section>
    </div>
  );
}
