import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
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
import "./Workshop.css";

function extractServiceName(service) {
  return String(readField(service, "name", "Name", "serviceName", "ServiceName") || "").trim();
}

function extractProductName(product) {
  return String(readField(product, "name", "Name", "productName", "ProductName") || "").trim();
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

function buildRecommendationName(row, serviceLookup, productLookup) {
  const itemType = normalizeRecommendationItemType(readField(row, "itemType", "ItemType"));
  if (itemType === "SERVICE") {
    const id = toPositiveNumber(readField(row, "standardServiceId", "StandardServiceId"));
    const service = id ? serviceLookup.get(id) : null;
    return extractServiceName(service) || (id ? `Dịch vụ #${id}` : "Dịch vụ");
  }

  const id = toPositiveNumber(readField(row, "inventoryItemId", "InventoryItemId"));
  const product = id ? productLookup.get(id) : null;
  return extractProductName(product) || (id ? `Vật tư #${id}` : "Vật tư");
}

function buildRecommendationCode(row) {
  const itemType = normalizeRecommendationItemType(readField(row, "itemType", "ItemType"));
  if (itemType === "SERVICE") {
    return readField(row, "standardServiceId", "StandardServiceId") || "-";
  }
  return readField(row, "inventoryItemId", "InventoryItemId") || "-";
}

function toEditableQuantity(value) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return "1";
  return String(parsed);
}

export default function DiagnosisApprovalBoardPage({
  mode,
  title,
  backPath,
  backLabel,
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const { user } = useAuth();
  const searchParams = useMemo(() => new URLSearchParams(location.search || ""), [location.search]);

  const staffId = useMemo(() => resolveCurrentStaffId(user), [user]);
  const numericOrderId = Number(orderId);
  const isTeamLeaderMode = mode === "TEAM_LEADER";
  const isForemanMode = mode === "FOREMAN";
  const isManagerMode = mode === "MANAGER";
  const fromHistoryContext = searchParams.get("from") === "history" || searchParams.has("from-history");
  const returnToPath = String(location.state?.returnToPath || "").trim();
  const historyBackPath = `${String(backPath || "").replace(/\/+$/, "")}/history`;
  const resolvedBackPath = returnToPath || (fromHistoryContext ? historyBackPath : backPath);

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

  const [approveNote, setApproveNote] = useState("");
  const [rejectNote, setRejectNote] = useState("");
  const [quantityById, setQuantityById] = useState({});
  const [collapsedPartsByRoot, setCollapsedPartsByRoot] = useState({});

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
      setError("Mã lệnh sửa chữa không hợp lệ.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    setActionMessage("");

    try {
      const [orderRes, diagnosesRes, quotationsRes, serviceRes, productRes, customerRes, vehicleRes] = await Promise.all([
        serviceOrderApi.getById(numericOrderId),
        serviceOrderApi.getDiagnoses(numericOrderId),
        serviceOrderApi.getQuotationsByServiceOrder(numericOrderId).catch(() => []),
        getAllStandardServices().catch(() => []),
        inventoryApi.getProducts().catch(() => []),
        getAllCustomers().catch(() => []),
        getAllVehicles().catch(() => []),
      ]);

      const latestDiagnosisId = extractLatestDiagnosisId(diagnosesRes);
      const recommendationRes = latestDiagnosisId
        ? await serviceOrderApi.getDiagnosisRecommendations(latestDiagnosisId).catch(() => [])
        : [];
      const safeRecommendations = Array.isArray(recommendationRes) ? recommendationRes : [];

      setOrder(orderRes || null);
      setDiagnoses(Array.isArray(diagnosesRes) ? diagnosesRes : []);
      setRecommendations(safeRecommendations);
      setQuotations(Array.isArray(quotationsRes) ? quotationsRes : []);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setProducts(Array.isArray(productRes) ? productRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu duyệt chuẩn đoán.");
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

  const activeRecommendations = useMemo(
    () => (Array.isArray(recommendations) ? recommendations : [])
      .filter((item) => !resolveRecommendationStatus(item).startsWith("SOFT_DELETED")),
    [recommendations],
  );
  const visibleRecommendations = useMemo(
    () => activeRecommendations
      .filter((item) => !toPositiveNumber(readField(item, "parentRecommendationId", "ParentRecommendationId"))),
    [activeRecommendations],
  );
  const childRecommendationsByParentId = useMemo(() => {
    const map = new Map();
    activeRecommendations.forEach((item) => {
      const parentId = toPositiveNumber(readField(item, "parentRecommendationId", "ParentRecommendationId"));
      if (!parentId) return;
      if (!map.has(parentId)) map.set(parentId, []);
      map.get(parentId).push(item);
    });
    return map;
  }, [activeRecommendations]);
  const totalSuggestedItems = useMemo(
    () => activeRecommendations
      .filter((item) => toPositiveNumber(readField(item, "id", "Id"))),
    [activeRecommendations],
  );

  useEffect(() => {
    const next = {};
    activeRecommendations.forEach((item) => {
      const id = toPositiveNumber(readField(item, "id", "Id"));
      if (!id) return;
      next[id] = toEditableQuantity(readField(item, "quantity", "Quantity"));
    });
    setQuantityById(next);
  }, [activeRecommendations]);

  useEffect(() => {
    setCollapsedPartsByRoot((prev) => {
      const next = { ...prev };
      const activeKeys = new Set();
      visibleRecommendations.forEach((root) => {
        const rootId = toPositiveNumber(readField(root, "id", "Id"));
        if (!rootId) return;
        const childRows = childRecommendationsByParentId.get(rootId) || [];
        if (childRows.length > 0) {
          const key = String(rootId);
          activeKeys.add(key);
          if (!(key in next)) next[key] = true;
        }
      });
      Object.keys(next).forEach((key) => {
        if (!activeKeys.has(key)) delete next[key];
      });
      return next;
    });
  }, [visibleRecommendations, childRecommendationsByParentId]);

  const technicalStatus =
    normalizeStatus(readField(latestDiagnosis, "teamLeaderApprovalStatus", "TeamLeaderApprovalStatus")) || "PENDING";
  const finalStatus = normalizeStatus(readField(latestDiagnosis, "finalApprovalStatus", "FinalApprovalStatus")) || "PENDING";
  const isTeamLeaderEscalated =
    technicalStatus === "ESCALATED" ||
    Boolean(readField(latestDiagnosis, "isTeamLeaderApprovalEscalated", "IsTeamLeaderApprovalEscalated"));
  const isTeamLeaderDecisionLocked =
    technicalStatus === "APPROVED" || finalStatus === "APPROVED" || finalStatus === "REJECTED" || isTeamLeaderEscalated;
  const isForemanDecisionLocked = finalStatus === "APPROVED" || finalStatus === "REJECTED";
  const isForemanAwaitingTeamLeaderApproval = false;
  const quotationStatus = normalizeStatus(readField(latestQuotation, "status", "Status"));
  const financialStatus =
    normalizeStatus(
      readField(
        latestQuotation,
        "financialApprovalStatus",
        "FinancialApprovalStatus",
        "branchManagerApprovalStatus",
        "BranchManagerApprovalStatus",
        "branchManagerApprovalState",
        "BranchManagerApprovalState",
      ),
    ) || "PENDING";
  const canManagerFinancialApprove = isManagerMode && quotationStatus === "PENDING_BRANCH_MANAGER";

  const slaSeverity = resolveSlaSeverity(readField(latestDiagnosis, "createdAt", "CreatedAt"));
  const slaClassName =
    slaSeverity === "RED"
      ? "approval-sla-chip approval-sla-red"
      : slaSeverity === "YELLOW"
        ? "approval-sla-chip approval-sla-yellow"
        : "approval-sla-chip";

  const customerDisplay = resolveCustomerDisplay(order, customerById);
  const plateDisplay = resolveVehiclePlateDisplay(order, vehicleById);
  const symptom = readField(latestDiagnosis, "symptom", "Symptom") || readField(order, "symptom", "Symptom") || "-";
  const assignmentNote = readField(latestDiagnosis, "assignmentNote", "AssignmentNote") || readField(order, "assignmentNote", "AssignmentNote") || "-";

  const handleSaveRecommendation = useCallback(async (item) => {
    const recommendationId = toPositiveNumber(readField(item, "id", "Id"));
    if (!recommendationId || !staffId) return;

    const quantityRaw = Number(quantityById[recommendationId]);
    if (!Number.isFinite(quantityRaw) || quantityRaw <= 0) {
      setError("Số lượng phải lớn hơn 0.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.updateDiagnosisRecommendation(recommendationId, {
        diagnoseId: toPositiveNumber(readField(item, "diagnoseId", "DiagnoseId")),
        parentRecommendationId: toPositiveNumber(readField(item, "parentRecommendationId", "ParentRecommendationId")) || null,
        itemType: readField(item, "itemType", "ItemType"),
        standardServiceId: toPositiveNumber(readField(item, "standardServiceId", "StandardServiceId")) || null,
        inventoryItemId: toPositiveNumber(readField(item, "inventoryItemId", "InventoryItemId")) || null,
        quantity: quantityRaw,
        urgency: readField(item, "urgency", "Urgency") || null,
        evidenceMediaUrls: readField(item, "evidenceMediaUrls", "EvidenceMediaUrls") || null,
        technicianNotes: readField(item, "technicianNotes", "TechnicianNotes") || null,
        updatedBy: staffId,
      });

      setActionMessage("Đã cập nhật số lượng đề xuất.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể cập nhật số lượng đề xuất.");
    } finally {
      setSaving(false);
    }
  }, [quantityById, staffId, loadData]);

  const handleSoftDeleteRecommendation = useCallback(async (item) => {
    const recommendationId = toPositiveNumber(readField(item, "id", "Id"));
    if (!recommendationId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.deleteDiagnosisRecommendation(recommendationId);
      setActionMessage("Đã loại bỏ hạng mục đề xuất.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể xóa hạng mục đề xuất.");
    } finally {
      setSaving(false);
    }
  }, [staffId, loadData]);

  const handleTeamLeaderApprove = useCallback(async () => {
    if (!numericOrderId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.approveDiagnosisTechnical(numericOrderId, {
        teamLeaderId: staffId,
        note: approveNote || null,
      });

      setActionMessage("Tổ trưởng đã duyệt kỹ thuật thành công.");
      window.alert("Duyệt kỹ thuật thành công.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể duyệt kỹ thuật tổ trưởng.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, approveNote, loadData]);

  const handleTeamLeaderReject = useCallback(async () => {
    if (!numericOrderId || !staffId) return;
    if (!String(rejectNote || "").trim()) {
      setError("Vui lòng nhập lý do từ chối để trả phiếu khám lại.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.rejectDiagnosisTechnical(numericOrderId, {
        teamLeaderId: staffId,
        note: rejectNote,
      });

      setActionMessage("Tổ trưởng đã từ chối, phiếu được trả về để khám lại.");
      window.alert("Từ chối duyệt thành công.");
      setRejectNote("");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể từ chối duyệt kỹ thuật.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, rejectNote, loadData]);

  const handleForemanDecision = useCallback(async (approve) => {
    if (!numericOrderId || !staffId) return;
    if (isForemanAwaitingTeamLeaderApproval) {
      setError("Chỉ được gửi quản đốc duyệt sau khi tổ trưởng đã duyệt kỹ thuật.");
      return;
    }
    if (!approve && !String(rejectNote || "").trim()) {
      setError("Vui lòng nhập lý do từ chối để trả phiếu khám lại.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.decideDiagnosisForeman(numericOrderId, {
        foremanId: staffId,
        approve,
        note: approve ? (approveNote || null) : rejectNote,
      });

      setActionMessage(
        approve
          ? "Quản đốc đã duyệt và chuyển sang báo giá thương mại."
          : "Quản đốc đã từ chối, phiếu được trả về để khám lại.",
      );
      window.alert(approve ? "Duyệt và chuyển báo giá thành công." : "Từ chối duyệt thành công.");
      setRejectNote("");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể xử lý quyết định của quản đốc.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, approveNote, rejectNote, loadData, isForemanAwaitingTeamLeaderApproval]);

  const handleManagerFinancialApprove = useCallback(async () => {
    const quotationId = toPositiveNumber(readField(latestQuotation, "id", "Id"));
    if (!quotationId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");

    try {
      await serviceOrderApi.financialApproveQuotation(quotationId, {
        actorStaffId: staffId,
        note: approveNote || null,
      });

      setActionMessage("Đã duyệt tài chính báo giá thành công.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không thể duyệt tài chính báo giá.");
    } finally {
      setSaving(false);
    }
  }, [latestQuotation, staffId, approveNote, loadData]);

  return (
    <div className="workshop-table-card approval-page-shell">
      <div className="reception-quote-head">
        <div>
          <h2 className="reception-quote-title">{title}</h2>
          <p className="reception-quote-subtitle">
            Lệnh <strong>{readField(order, "code", "Code") || `#${orderId}`}</strong>
          </p>
        </div>
        <button type="button" className="btn-back" onClick={() => navigate(resolvedBackPath)}>
          {backLabel}
        </button>
      </div>

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang tải dữ liệu duyệt chuẩn đoán...</p>
        </div>
      ) : (
        <div className="reception-quote-layout">
          <section className="reception-quote-summary-card">
            <article>
              <span>Khách hàng</span>
              <strong>{customerDisplay}</strong>
            </article>
            <article>
              <span>Biển số</span>
              <strong>{plateDisplay}</strong>
            </article>
            <article>
              <span>SLA chờ duyệt</span>
              <strong><span className={slaClassName}>{slaSeverity === "RED" ? "Quá hạn > 30 phút" : slaSeverity === "YELLOW" ? "Cảnh báo > 15 phút" : "Trong SLA"}</span></strong>
            </article>
            <article>
              <span>Số đề xuất</span>
              <strong>{totalSuggestedItems.length}</strong>
            </article>
            <article>
              <span>Duyệt tổ trưởng</span>
              <strong>{getStatusLabelVi(technicalStatus, technicalStatus)}</strong>
            </article>
            <article>
              <span>{isManagerMode ? "Trạng thái báo giá" : "Duyệt quản đốc"}</span>
              <strong>{getStatusLabelVi(isManagerMode ? quotationStatus : finalStatus, isManagerMode ? quotationStatus : finalStatus)}</strong>
            </article>
            {isManagerMode ? (
              <article>
                <span>Duyệt tài chính</span>
                <strong>{getStatusLabelVi(financialStatus, financialStatus)}</strong>
              </article>
            ) : null}
          </section>

          <section className="reception-quote-action-card">
            <h3>Thông tin chuẩn đoán kỹ thuật</h3>
            <div className="approval-note-grid">
              <article>
                <span>Triệu chứng</span>
                <strong>{symptom}</strong>
              </article>
              <article>
                <span>Ghi chú phân công</span>
                <strong>{assignmentNote}</strong>
              </article>
              <article>
                <span>Cập nhật gần nhất</span>
                <strong>{formatDateTime(readField(latestDiagnosis, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt"))}</strong>
              </article>
            </div>
            {error ? <p className="reception-quote-error">{error}</p> : null}
            {actionMessage ? <p className="reception-quote-note">{actionMessage}</p> : null}
            {isTeamLeaderEscalated ? (
              <p className="reception-quote-error">
                Phiếu đã quá SLA duyệt của tổ trưởng và đã tự động chuyển lên quản đốc xử lý.
              </p>
            ) : null}
          </section>

          <section className="reception-quote-table-card">
            <div className="reception-quote-table-head">
              <h3>Đề xuất kỹ thuật của thợ</h3>
            </div>

            {visibleRecommendations.length === 0 ? (
              <p className="reception-quote-note">Chưa có đề xuất kỹ thuật.</p>
            ) : (
              <div className="workshop-table-card">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Hạng mục</th>
                      <th>Số lượng</th>
                      <th>Mức độ ưu tiên</th>
                      <th>Ghi chú kỹ thuật</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleRecommendations.map((item) => {
                      const id = toPositiveNumber(readField(item, "id", "Id"));
                      const qty = quantityById[id] || "1";
                      const urgency = firstNonEmptyText(readField(item, "urgency", "Urgency"), "-");
                      const childRows = childRecommendationsByParentId.get(id) || [];
                      const isCollapsed = Boolean(collapsedPartsByRoot[String(id)]);

                      return (
                        <React.Fragment key={id}>
                          <tr className="approval-row-root">
                            <td>
                              {buildRecommendationName(item, serviceLookup, productLookup)}
                              {childRows.length > 0 ? (
                                <button
                                  type="button"
                                  className="approval-part-toggle"
                                  onClick={() =>
                                    setCollapsedPartsByRoot((prev) => ({
                                      ...prev,
                                      [String(id)]: !prev[String(id)],
                                    }))
                                  }
                                >
                                  {isCollapsed ? `Hiện phụ tùng (${childRows.length})` : "Thu gọn phụ tùng"}
                                </button>
                              ) : null}
                            </td>
                            <td>
                              <input
                                type="number"
                                min="1"
                                step="1"
                                value={qty}
                                onChange={(event) => setQuantityById((prev) => ({ ...prev, [id]: event.target.value }))}
                                disabled={saving || isManagerMode}
                                className="approval-qty-input"
                              />
                            </td>
                            <td>{urgency}</td>
                            <td>{readField(item, "technicianNotes", "TechnicianNotes") || "-"}</td>
                            <td>
                              <div className="reception-quote-action-row">
                                {!isManagerMode ? (
                                  <>
                                    <button
                                      type="button"
                                      className="btn-primary"
                                      onClick={() => handleSaveRecommendation(item)}
                                      disabled={saving || !staffId}
                                    >
                                      Lưu SL
                                    </button>
                                    <button
                                      type="button"
                                      className="btn-danger"
                                      onClick={() => handleSoftDeleteRecommendation(item)}
                                      disabled={saving || !staffId}
                                    >
                                      Loại bỏ
                                    </button>
                                  </>
                                ) : (
                                  "-"
                                )}
                              </div>
                            </td>
                          </tr>
                          {!isCollapsed
                            ? childRows.map((child) => {
                              const childId = toPositiveNumber(readField(child, "id", "Id"));
                              const childKey = childId || `child-${id}-${readField(child, "itemName", "ItemName") || "part"}`;
                              const childQty = quantityById[childId] || "1";
                              const childUrgency = firstNonEmptyText(readField(child, "urgency", "Urgency"), "-");
                              return (
                                <tr key={childKey} className="approval-row-part">
                                  <td>└─ {buildRecommendationName(child, serviceLookup, productLookup)}</td>
                                  <td>
                                    <input
                                      type="number"
                                      min="1"
                                      step="1"
                                      value={childQty}
                                      onChange={(event) =>
                                        setQuantityById((prev) => ({ ...prev, [childId]: event.target.value }))
                                      }
                                      disabled={saving || isManagerMode}
                                      className="approval-qty-input"
                                    />
                                  </td>
                                  <td>{childUrgency}</td>
                                  <td>{readField(child, "technicianNotes", "TechnicianNotes") || "-"}</td>
                                  <td>
                                    <div className="reception-quote-action-row">
                                      {!isManagerMode ? (
                                        <>
                                          <button
                                            type="button"
                                            className="btn-primary"
                                            onClick={() => handleSaveRecommendation(child)}
                                            disabled={saving || !staffId}
                                          >
                                            Lưu SL
                                          </button>
                                          <button
                                            type="button"
                                            className="btn-danger"
                                            onClick={() => handleSoftDeleteRecommendation(child)}
                                            disabled={saving || !staffId}
                                          >
                                            Loại bỏ
                                          </button>
                                        </>
                                      ) : (
                                        "-"
                                      )}
                                    </div>
                                  </td>
                                </tr>
                              );
                            })
                            : null}
                        </React.Fragment>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="reception-quote-action-card">
            <h3>{isManagerMode ? "Xử lý duyệt tài chính" : "Xử lý duyệt kỹ thuật"}</h3>
            <div className="reception-quote-approval-note">
              <div className="reception-quote-approval-note-head">
                <span className="reception-quote-approval-note-title">Ghi chú nội bộ</span>
                <small className="reception-quote-approval-note-hint">Nêu lý do duyệt hoặc chỉnh sửa để truy vết lịch sử.</small>
              </div>
              <textarea
                className="reception-quote-approval-note-input"
                rows={3}
                value={approveNote}
                onChange={(event) => setApproveNote(event.target.value)}
                placeholder="Nhập ghi chú duyệt..."
                disabled={saving}
              />
            </div>

            {(isForemanMode && !isForemanDecisionLocked) || (isTeamLeaderMode && !isTeamLeaderDecisionLocked) ? (
              <div className="reception-quote-approval-note">
                <div className="reception-quote-approval-note-head">
                  <span className="reception-quote-approval-note-title">Lý do từ chối (nếu trả khám lại)</span>
                </div>
                <textarea
                  className="reception-quote-approval-note-input"
                  rows={3}
                  value={rejectNote}
                  onChange={(event) => setRejectNote(event.target.value)}
                  placeholder="Nhập lý do từ chối và yêu cầu khám lại..."
                  disabled={saving}
                />
              </div>
            ) : null}

            <div className="reception-quote-action-row">
              {isTeamLeaderMode && !isTeamLeaderDecisionLocked ? (
                <>
                  <button
                    type="button"
                    className="btn-success"
                    onClick={handleTeamLeaderApprove}
                    disabled={saving || !staffId || visibleRecommendations.length === 0 || isTeamLeaderDecisionLocked}
                  >
                    Duyệt kỹ thuật tổ trưởng
                  </button>
                  <button
                    type="button"
                    className="btn-danger"
                    onClick={handleTeamLeaderReject}
                    disabled={saving || !staffId || isTeamLeaderDecisionLocked}
                  >
                    Từ chối & Khám lại
                  </button>
                </>
              ) : null}

              {isForemanMode && !isForemanDecisionLocked ? (
                <>
                  {isForemanAwaitingTeamLeaderApproval ? (
                    <small className="reception-quote-note">Cần tổ trưởng duyệt kỹ thuật trước khi gửi quản đốc duyệt.</small>
                  ) : null}
                  <button
                    type="button"
                    className="btn-success"
                    onClick={() => handleForemanDecision(true)}
                    disabled={
                      saving
                      || !staffId
                      || visibleRecommendations.length === 0
                      || isForemanDecisionLocked
                      || isForemanAwaitingTeamLeaderApproval
                    }
                  >
                    Duyệt & Chuyển báo giá
                  </button>
                  <button
                    type="button"
                    className="btn-danger"
                    onClick={() => handleForemanDecision(false)}
                    disabled={saving || !staffId || isForemanDecisionLocked || isForemanAwaitingTeamLeaderApproval}
                  >
                    Từ chối & Khám lại
                  </button>
                </>
              ) : null}

              {isManagerMode ? (
                <button
                  type="button"
                  className="btn-success"
                  onClick={handleManagerFinancialApprove}
                  disabled={saving || !staffId || !canManagerFinancialApprove}
                >
                  Duyệt tài chính
                </button>
              ) : null}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
