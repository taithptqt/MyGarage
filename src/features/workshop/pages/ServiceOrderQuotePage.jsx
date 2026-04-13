import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStandardServices } from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/admin/api/inventoryApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getStatusLabelVi } from "@shared/core/utils/statusLabelUtils";
import "./Workshop.css";

function resolveBackConfig(pathname) {
  const normalizedPath = String(pathname || "").toLowerCase();
  if (normalizedPath.includes("/workshop/manager/")) return { path: "/workshop/manager", label: "Quay lại quản đốc" };
  if (normalizedPath.includes("/workshop/foreman/")) return { path: "/workshop/foreman", label: "Quay lại quản đốc" };
  if (normalizedPath.includes("/workshop/team-leader/")) return { path: "/workshop/team-leader", label: "Quay lại tổ trưởng" };
  return { path: "/workshop/reception", label: "Quay lại lễ tân" };
}

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function normalizeStatus(value) {
  return String(value || "").trim().toUpperCase();
}

function formatMoney(value) {
  return Number(value || 0).toLocaleString("vi-VN");
}

function resolveCurrentStaffId(user) {
  const candidates = [
    user?.id,
    user?.staffId,
    user?.StaffId,
    user?.Id,
    user?.profile?.id,
    user?.profile?.staffId,
  ];
  for (const candidate of candidates) {
    const parsed = Number(candidate);
    if (Number.isFinite(parsed) && parsed > 0) return parsed;
  }
  return null;
}

function readField(row, ...keys) {
  for (const key of keys) {
    if (row && row[key] !== undefined && row[key] !== null) return row[key];
  }
  return null;
}

function firstNonEmptyText(...candidates) {
  for (const candidate of candidates) {
    const text = String(candidate ?? "").trim();
    if (text) return text;
  }
  return "";
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
  return firstNonEmptyText(
    customer?.fullName,
    customer?.FullName,
    customer?.name,
    customer?.Name
  );
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
  return firstNonEmptyText(
    vehicle?.licensePlate,
    vehicle?.LicensePlate,
    vehicle?.plateNumber,
    vehicle?.PlateNumber
  );
}

function extractLatestDiagnosisId(diagnoses) {
  const list = Array.isArray(diagnoses) ? diagnoses : [];
  if (list.length === 0) return null;

  const sorted = [...list].sort((a, b) => {
    const aTime = new Date(readField(a, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
    const bTime = new Date(readField(b, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
    return bTime - aTime;
  });

  return toPositiveNumber(readField(sorted[0], "id", "Id"));
}

function normalizeRecommendationItemType(raw) {
  const token = String(raw || "").trim().toUpperCase();
  if (token.includes("PART") || token.includes("MATERIAL") || token.includes("PRODUCT")) return "PART";
  return "SERVICE";
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
    readField(product, "salePrice", "SalePrice", "retailPrice", "RetailPrice", "unitPrice", "UnitPrice", "sellingPrice", "SellingPrice") || 0
  );
  return Number.isFinite(val) && val > 0 ? val : 0;
}

function statusLabel(status) {
  const token = normalizeStatus(status);
  if (token === "DRAFT") return "DRAFT";
  if (token === "WAITING_APPROVAL") return "WAITING_APPROVAL";
  if (token === "APPROVED") return "APPROVED";
  if (token === "REJECTED") return "REJECTED";
  if (token === "SUPERSEDED") return "SUPERSEDED";
  return token || "UNKNOWN";
}

export default function ServiceOrderQuotePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const { user } = useAuth();
  const normalizedPath = String(location.pathname || "").toLowerCase();
  const roleCode = String(user?.roleCode || "").trim().toUpperCase();

  const staffId = useMemo(() => resolveCurrentStaffId(user), [user]);
  const numericOrderId = Number(orderId);
  const backConfig = useMemo(() => resolveBackConfig(location.pathname), [location.pathname]);
  const isTeamLeaderView = roleCode === "TEAM_LEADER" || normalizedPath.includes("/team-leader/");
  const isForemanOrManagerView =
    ["FOREMAN", "MANAGER", "ADMIN"].includes(roleCode) ||
    normalizedPath.includes("/foreman/") ||
    normalizedPath.includes("/manager/");
  const isReceptionView = roleCode === "RECEPTIONIST" || normalizedPath.includes("/reception/");

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [actionMessage, setActionMessage] = useState("");

  const [order, setOrder] = useState(null);
  const [recommendations, setRecommendations] = useState([]);
  const [quotations, setQuotations] = useState([]);
  const [activeQuotationId, setActiveQuotationId] = useState(null);
  const [decisionByDetailId, setDecisionByDetailId] = useState({});
  const [approveNote, setApproveNote] = useState("");
  const autoCreateTriedRef = useRef(false);

  const [services, setServices] = useState([]);
  const [products, setProducts] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);

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
      const id = extractServiceId(item);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [services]);

  const productLookup = useMemo(() => {
    const map = new Map();
    (products || []).forEach((item) => {
      const id = extractProductId(item);
      if (!id) return;
      map.set(id, item);
    });
    return map;
  }, [products]);

  const activeQuotation = useMemo(
    () => (quotations || []).find((item) => Number(readField(item, "id", "Id")) === Number(activeQuotationId)) || null,
    [quotations, activeQuotationId]
  );

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
        serviceOrderApi.getQuotationsByServiceOrder(numericOrderId),
        getAllStandardServices().catch(() => []),
        inventoryApi.getProducts().catch(() => []),
        getAllCustomers().catch(() => []),
        getAllVehicles().catch(() => []),
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
      setQuotations(sortedQuoteList);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setProducts(Array.isArray(productRes) ? productRes : []);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);

      if (sortedQuoteList.length > 0) {
        const latestId = Number(readField(sortedQuoteList[0], "id", "Id"));
        setActiveQuotationId(latestId);
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

  useEffect(() => {
    const next = {};
    const details = Array.isArray(activeQuotation?.details) ? activeQuotation.details : [];
    details.forEach((item) => {
      const detailId = Number(readField(item, "id", "Id"));
      if (!detailId) return;
      const status = normalizeStatus(readField(item, "approvalStatus", "ApprovalStatus"));
      next[detailId] = status === "REJECTED" ? "REJECTED" : "APPROVED";
    });
    setDecisionByDetailId(next);
  }, [activeQuotationId, activeQuotation]);

  const buildQuotationItemsFromRecommendations = useCallback(() => {
    const source = (Array.isArray(recommendations) ? recommendations : []).filter((item) => {
      const parentId = toPositiveNumber(readField(item, "parentRecommendationId", "ParentRecommendationId"));
      return !parentId;
    });

    return source
      .map((rec) => {
        const itemType = normalizeRecommendationItemType(readField(rec, "itemType", "ItemType"));
        const standardServiceId = toPositiveNumber(readField(rec, "standardServiceId", "StandardServiceId"));
        const inventoryItemId = toPositiveNumber(readField(rec, "inventoryItemId", "InventoryItemId"));
        const quantityRaw = Number(readField(rec, "quantity", "Quantity") || 1);
        const quantity = Number.isFinite(quantityRaw) && quantityRaw > 0 ? quantityRaw : 1;

        let itemId = null;
        let itemName = "";
        let unitPriceAtTime = 0;
        let taxRate = 0;

        if (itemType === "SERVICE" && standardServiceId) {
          const service = serviceLookup.get(standardServiceId);
          itemId = standardServiceId;
          itemName = extractServiceName(service) || `Dịch vụ #${standardServiceId}`;
          unitPriceAtTime = extractServicePrice(service);
          taxRate = extractServiceTax(service);
        } else if (inventoryItemId) {
          const product = productLookup.get(inventoryItemId);
          itemId = inventoryItemId;
          itemName = extractProductName(product) || `Vật tư #${inventoryItemId}`;
          unitPriceAtTime = extractProductPrice(product);
          taxRate = 0;
        }

        return {
          diagnoseRecommendationId: toPositiveNumber(readField(rec, "id", "Id")),
          itemType,
          itemId,
          itemName: itemName || "Hạng mục đề xuất",
          description: readField(rec, "technicianNotes", "TechnicianNotes") || null,
          quantity,
          unitPriceAtTime,
          costPriceAtTime: null,
          discountValue: 0,
          taxRate,
          payerType: "CUSTOMER",
          insuranceCompanyId: null,
          insuranceRequestedAmount: null,
          customerCoPayAmount: null,
          isBlocking: true,
          estimatedMinutes: null,
          isReturnOldPart: false,
        };
      })
      .filter((item) => String(item.itemName || "").trim().length > 0);
  }, [recommendations, serviceLookup, productLookup]);

  const handleCreateDraftQuotation = useCallback(async () => {
    if (!numericOrderId || !staffId) {
      setError("Không xác định được mã lệnh hoặc nhân sự thao tác.");
      return;
    }

    const items = buildQuotationItemsFromRecommendations();
    if (items.length === 0) {
      setError("Không có đề xuất chẩn đoán để tạo báo giá nháp.");
      return;
    }

    setSaving(true);
    setError("");
    setActionMessage("");
    try {
      const created = await serviceOrderApi.createQuotation(numericOrderId, {
        title: `Báo giá v${(quotations?.length || 0) + 1}`,
        quotationType: "NORMAL",
        validUntil: null,
        serviceAdvisorId: staffId,
        customerNotes: null,
        internalNotes: "Tạo từ đề xuất chẩn đoán",
        submitForApproval: false,
        items,
      });

      setActionMessage("Đã tạo báo giá nháp mới.");
      await loadData();
      const createdId = Number(readField(created, "id", "Id"));
      if (createdId > 0) setActiveQuotationId(createdId);
    } catch (err) {
      setError(err?.message || "Không tạo được báo giá nháp.");
    } finally {
      setSaving(false);
    }
  }, [numericOrderId, staffId, buildQuotationItemsFromRecommendations, quotations?.length, loadData]);

  useEffect(() => {
    autoCreateTriedRef.current = false;
  }, [numericOrderId]);

  useEffect(() => {
    if (loading || saving) return;
    if (autoCreateTriedRef.current) return;
    if (!(isTeamLeaderView || isReceptionView || roleCode === "ADMIN")) return;
    if ((quotations || []).length > 0) return;
    if ((recommendations || []).length === 0) return;
    if (!staffId) return;

    autoCreateTriedRef.current = true;
    void handleCreateDraftQuotation();
  }, [loading, saving, quotations, recommendations, staffId, handleCreateDraftQuotation, isTeamLeaderView, isReceptionView, roleCode]);

  const handleSubmitQuotation = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");
    try {
      await serviceOrderApi.submitQuotation(quoteId, { actorStaffId: staffId, note: "Gửi cho khách duyệt" });
      setActionMessage("Đã chuyển báo giá sang WAITING_APPROVAL.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không gửi được báo giá.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, loadData]);

  const handleSupersedeQuotation = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    setSaving(true);
    setError("");
    setActionMessage("");
    try {
      await serviceOrderApi.supersedeQuotation(quoteId, { actorStaffId: staffId, note: "Thay bằng phiên bản mới" });
      setActionMessage("Đã supersede báo giá hiện tại.");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không supersede được báo giá.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, loadData]);

  const handleApproveQuotation = useCallback(async () => {
    const quoteId = Number(readField(activeQuotation, "id", "Id"));
    if (!quoteId || !staffId) return;

    const details = Array.isArray(activeQuotation?.details) ? activeQuotation.details : [];
    const decisions = details
      .map((row) => {
        const detailId = Number(readField(row, "id", "Id"));
        if (!detailId) return null;
        const selected = normalizeStatus(decisionByDetailId[detailId]) || "APPROVED";
        return {
          quotationDetailId: detailId,
          approved: selected !== "REJECTED",
          rejectReason: selected === "REJECTED" ? "CUSTOMER_REJECTED" : null,
        };
      })
      .filter(Boolean);

    setSaving(true);
    setError("");
    setActionMessage("");
    try {
      await serviceOrderApi.approveQuotation(quoteId, {
        approvedBy: staffId,
        note: approveNote || null,
        decisions,
      });
      setActionMessage("Đã duyệt báo giá và đẩy hạng mục APPROVED qua SOD.");
      setApproveNote("");
      await loadData();
    } catch (err) {
      setError(err?.message || "Không duyệt được báo giá.");
    } finally {
      setSaving(false);
    }
  }, [activeQuotation, staffId, decisionByDetailId, approveNote, loadData]);

  const quotationStatus = normalizeStatus(readField(activeQuotation, "status", "Status"));
  const canSubmit = quotationStatus === "DRAFT";
  const canApprove = ["DRAFT", "WAITING_APPROVAL"].includes(quotationStatus);
  const canFinalApprove = quotationStatus === "WAITING_APPROVAL";
  const canSupersede = !["APPROVED", "SUPERSEDED"].includes(normalizeStatus(readField(activeQuotation, "status", "Status")));
  const customerDisplay = resolveCustomerDisplay(order, customerById) || "-";
  const vehiclePlateDisplay = resolveVehiclePlateDisplay(order, vehicleById) || "-";

  return (
    <div className="workshop-table-card reception-quote-page">
      <div className="reception-quote-head">
        <div>
          <h2 className="reception-quote-title">Báo giá theo luồng mới</h2>
          <p className="reception-quote-subtitle">
            Lệnh <strong>{readField(order, "code", "Code") || `#${orderId}`}</strong>
          </p>
        </div>
        <button type="button" className="btn-back" onClick={() => navigate(backConfig.path)}>
          {backConfig.label}
        </button>
      </div>

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
            <p>Đang tải dữ liệu...</p>
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
              <strong>{vehiclePlateDisplay}</strong>
            </article>
            <article>
              <span>Trạng thái lệnh</span>
              <strong>{getStatusLabelVi(readField(order, "status", "Status"), readField(order, "status", "Status") || "-")}</strong>
            </article>
            <article>
              <span>Số đề xuất chẩn đoán</span>
              <strong>{(recommendations || []).length}</strong>
            </article>
          </section>

          <section className="reception-quote-action-card">
            <h3>Thao tác báo giá</h3>
            <p className="reception-quote-note">
              Hệ thống tự tạo báo giá nháp từ đề xuất chẩn đoán và chờ thao tác duyệt theo vai trò.
            </p>
            {error ? <p className="reception-quote-error">{error}</p> : null}
            {actionMessage ? <p className="reception-quote-note">{actionMessage}</p> : null}
          </section>

          <section className="reception-quote-action-card">
            <h3>Danh sách báo giá</h3>
            {(quotations || []).length === 0 ? (
              <p className="reception-quote-note">Chưa có báo giá.</p>
            ) : (
              <div className="reception-quote-action-row" style={{ flexWrap: "wrap", gap: 8 }}>
                {quotations.map((row) => {
                  const id = Number(readField(row, "id", "Id"));
                  const versionNo = Number(readField(row, "versionNo", "VersionNo") || 0);
                  const status = statusLabel(readField(row, "status", "Status"));
                  return (
                    <button
                      key={id}
                      type="button"
                      className={id === Number(activeQuotationId) ? "btn-primary" : "toolbar-action-btn toolbar-action-outline"}
                      onClick={() => setActiveQuotationId(id)}
                      title={`Trạng thái: ${getStatusLabelVi(status, status)}`}
                    >
                      Báo giá - V{versionNo}
                    </button>
                  );
                })}
              </div>
            )}
          </section>

          {activeQuotation ? (
            <>
              <section className="reception-quote-action-card">
                <h3>Xử lý báo giá đang chọn</h3>
                <div className="reception-quote-action-row">
                  {isForemanOrManagerView ? (
                    <button type="button" className="btn-success" onClick={handleApproveQuotation} disabled={saving || !canFinalApprove || !staffId}>
                      Duyệt lần cuối
                    </button>
                  ) : null}
                  {isForemanOrManagerView ? (
                    <button type="button" className="btn-danger" onClick={handleSupersedeQuotation} disabled={saving || !canSupersede || !staffId}>
                      Thay thế phiên bản
                    </button>
                  ) : null}
                </div>
                <div className="reception-quote-approval-note">
                  <div className="reception-quote-approval-note-head">
                    <span className="reception-quote-approval-note-title">Ghi chú duyệt</span>
                    <small className="reception-quote-approval-note-hint">Nêu rõ chỉnh sửa hoặc lý do duyệt để theo dõi lịch sử.</small>
                  </div>
                  <textarea
                    className="reception-quote-approval-note-input"
                    rows={3}
                    value={approveNote}
                    onChange={(event) => setApproveNote(event.target.value)}
                    placeholder="Nhập ghi chú nội bộ..."
                    disabled={saving || !canApprove || (!isTeamLeaderView && !isForemanOrManagerView && !isReceptionView && roleCode !== "ADMIN")}
                  />
                </div>
              </section>

              <section className="reception-quote-table-card">
                <div className="reception-quote-table-head">
                  <h3>Chi tiết báo giá</h3>
                  <strong>{formatMoney(readField(activeQuotation, "finalAmount", "FinalAmount"))} đ</strong>
                </div>
                {Array.isArray(activeQuotation.details) && activeQuotation.details.length > 0 ? (
                  <div className="workshop-table-card">
                    <table className="admin-table">
                      <thead>
                        <tr>
                          <th>Hạng mục</th>
                          <th>SL</th>
                          <th>Đơn giá</th>
                          <th>Thành tiền</th>
                          <th>Duyệt</th>
                        </tr>
                      </thead>
                      <tbody>
                        {activeQuotation.details.map((row) => {
                          const detailId = Number(readField(row, "id", "Id"));
                          const name = String(readField(row, "itemName", "ItemName") || "-");
                          const qty = Number(readField(row, "quantity", "Quantity") || 1);
                          const unitPrice = Number(readField(row, "unitPriceAtTime", "UnitPriceAtTime") || 0);
                          const finalAmount = Number(readField(row, "finalAmount", "FinalAmount") || qty * unitPrice);
                          const current = normalizeStatus(decisionByDetailId[detailId]) || "APPROVED";
                          const locked = !canApprove || saving;
                          return (
                            <tr key={detailId}>
                              <td>{name}</td>
                              <td>{qty}</td>
                              <td>{formatMoney(unitPrice)}</td>
                              <td>{formatMoney(finalAmount)}</td>
                              <td>
                                <select
                                  value={current}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const value = normalizeStatus(event.target.value) || "APPROVED";
                                    setDecisionByDetailId((prev) => ({ ...prev, [detailId]: value }));
                                  }}
                                >
                                  <option value="APPROVED">APPROVED</option>
                                  <option value="REJECTED">REJECTED</option>
                                </select>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p className="reception-quote-note">Báo giá này chưa có dòng chi tiết.</p>
                )}
              </section>

              {(isTeamLeaderView || isReceptionView || roleCode === "ADMIN") ? (
                <section className="reception-quote-submit-footer">
                  <button type="button" className="btn-primary" onClick={handleSubmitQuotation} disabled={saving || !canSubmit || !staffId}>
                    Gửi quản đốc duyệt
                  </button>
                </section>
              ) : null}
            </>
          ) : null}
        </div>
      )}
    </div>
  );
}
