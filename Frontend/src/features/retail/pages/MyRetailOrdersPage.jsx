import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import RetailOrderStatusBadge from "@features/retail/components/RetailOrderStatusBadge";
import { createPayosOrder, syncOrder } from "@features/payment/api/payosApi";
import {
  getBranches,
  getRetailOrderById,
} from "@features/retail/api/retailApi";
import { getMyRetailOrderPage } from "@features/retail/api/retailOrderPagingApi";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  formatVnd,
  getBranchLabel,
  getDisplayText,
  getRetailOrderStatusMeta,
} from "@features/retail/utils/retailFormat";
import "./RetailShared.css";
import "./MyRetailOrdersPage.css";

const ORDER_PAGE_SIZE = 20;
const PAGE_LABELS = { previous: "Trước", next: "Sau", page: "Trang" };

function formatDateTime(value) {
  if (!value) return "Chưa có";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa có";

  return date.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
}

function toSafeAmount(value) {
  const parsed = Number(value);
  if (Number.isFinite(parsed) && parsed > 0) return Math.floor(parsed);

  if (value == null) return 0;

  const normalizedDigits = String(value).replace(/[^\d]/g, "");
  const fallback = Number(normalizedDigits);
  return Number.isFinite(fallback) && fallback > 0 ? Math.floor(fallback) : 0;
}

function buildFrontendUrl(pathname) {
  if (typeof window === "undefined") return pathname;
  return `${window.location.origin}${pathname}`;
}

function getOrderItems(payload) {
  const source =
    payload?.items ??
    payload?.Items ??
    payload?.retailOrderItems ??
    payload?.RetailOrderItems;

  return Array.isArray(source) ? source : [];
}

function formatQuantity(value) {
  const quantity = Number(value);
  if (!Number.isFinite(quantity)) return "--";

  const rounded = Math.round(quantity);
  if (Math.abs(quantity - rounded) < 0.00001) return String(rounded);

  return quantity.toLocaleString("vi-VN", { maximumFractionDigits: 2 });
}

function formatRate(value) {
  const rate = Number(value);
  if (!Number.isFinite(rate)) return "0%";
  return `${rate.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%`;
}

export default function MyRetailOrdersPage() {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [orders, setOrders] = useState([]);
  const [page, setPage] = useState(1);
  const [summary, setSummary] = useState({ total: 0, draftCount: 0, confirmedCount: 0, totalValue: 0 });
  const [refreshVersion, setRefreshVersion] = useState(0);
  const anchorOrderId = Number(searchParams.get("orderId")) || 0;
  const [branches, setBranches] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [highlightOrderId, setHighlightOrderId] = useState(null);
  const [detailOrder, setDetailOrder] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const [payingOrderId, setPayingOrderId] = useState(null);

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError("");

      try {
        const [orderData, branchData] = await Promise.all([
          getMyRetailOrderPage({ page, pageSize: ORDER_PAGE_SIZE, anchorId: anchorOrderId }),
          getBranches(),
        ]);

        if (ignore) return;

        const nextOrders = Array.isArray(orderData?.items) ? orderData.items : [];
        const nextBranches = Array.isArray(branchData) ? branchData : [];

        setOrders(nextOrders);
        setSummary({ total: Number(orderData?.total) || 0, draftCount: Number(orderData?.draftCount) || 0,
          confirmedCount: Number(orderData?.confirmedCount) || 0, totalValue: Number(orderData?.totalValue) || 0 });
        setPage(Number(orderData?.page) || 1);
        setBranches(nextBranches);
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không tải được danh sách đơn phụ tùng.");
          setOrders([]);
          setBranches([]);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, [page, refreshVersion, anchorOrderId]);

  const branchById = useMemo(
    () => new Map(branches.map((branch) => [String(branch.id), branch])),
    [branches]
  );

  const orderStats = useMemo(() => {
    return [
      { value: `${summary.total}`, label: "đơn phụ tùng đã tạo" },
      { value: `${summary.draftCount}`, label: "đơn đang chờ thanh toán" },
      { value: `${summary.confirmedCount}`, label: "đơn đã xác nhận" },
      { value: formatVnd(summary.totalValue), label: "tổng giá trị hiển thị" },
    ];
  }, [summary]);

  useEffect(() => {
    if (loading || orders.length === 0) return;

    const targetOrderId = Number(searchParams.get("orderId") || 0);
    if (!Number.isFinite(targetOrderId) || targetOrderId <= 0) return;

    const targetElement = document.getElementById(`retail-order-${targetOrderId}`);
    if (!targetElement) return;

    targetElement.scrollIntoView({ behavior: "smooth", block: "center" });
    setHighlightOrderId(targetOrderId);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.delete("orderId");
    setSearchParams(nextParams, { replace: true });

  }, [loading, orders, searchParams, setSearchParams]);

  useEffect(() => {
    if (highlightOrderId == null) return undefined;
    const timer = window.setTimeout(() => setHighlightOrderId(null), 2200);
    return () => window.clearTimeout(timer);
  }, [highlightOrderId]);

  useEffect(() => {
    const queryOrderId = Number(searchParams.get("orderId") || 0);
    const storedRetailOrderId = Number(localStorage.getItem("payos_last_retail_order_id") || 0);
    const targetOrderId = Number.isFinite(queryOrderId) && queryOrderId > 0
      ? queryOrderId
      : (Number.isFinite(storedRetailOrderId) && storedRetailOrderId > 0 ? storedRetailOrderId : 0);
    const payosOrderId = Number(localStorage.getItem("payos_last_order_id") || 0);

    if (targetOrderId <= 0) return;

    let disposed = false;

    async function syncAndRefreshRetailStatus() {
      if (Number.isFinite(payosOrderId) && payosOrderId > 0) {
        try {
          await syncOrder(payosOrderId);
        } catch {
          // ignore and continue polling retail status
        }
      }

      const maxAttempts = 20;
      for (let attempt = 0; attempt < maxAttempts && !disposed; attempt += 1) {
        try {
          const targetOrder = await getRetailOrderById(targetOrderId);
          if (disposed) return;
          setOrders((previous) => previous.map((order) => Number(order?.id) === targetOrderId ? { ...order, ...targetOrder } : order));
          const normalizedStatus = String(targetOrder?.status || "").toUpperCase();
          if (normalizedStatus === "CONFIRMED") {
            localStorage.removeItem("payos_last_order_id");
            localStorage.removeItem("payos_last_retail_order_id");
            setRefreshVersion((value) => value + 1);
            break;
          }

          if (attempt % 4 === 3 && Number.isFinite(payosOrderId) && payosOrderId > 0) {
            try {
              await syncOrder(payosOrderId);
            } catch {
              // keep polling
            }
          }
        } catch {
          // keep retrying
        }

        await new Promise((resolve) => window.setTimeout(resolve, 1500));
      }
    }

    void syncAndRefreshRetailStatus();

    return () => {
      disposed = true;
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const closeOrderDetail = () => {
    if (detailLoading || payingOrderId != null) return;
    setDetailOrder(null);
    setDetailError("");
  };

  useEffect(() => {
    if (!detailOrder) return;

    const handleKeyDown = (event) => {
      if (event.key !== "Escape" || detailLoading) return;
      setDetailOrder(null);
      setDetailError("");
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [detailOrder, detailLoading]);

  const openOrderDetail = async (order) => {
    if (!order?.id) return;

    const normalizedStatus = String(order?.status || "DRAFT").toUpperCase();
    const branch = branchById.get(String(order?.branchId));
    const fallbackBranchLabel = branch ? getBranchLabel(branch) : `Chi nhánh #${order?.branchId ?? "?"}`;

    setDetailError("");
    setDetailOrder({
      id: order.id,
      orderCode: order.orderCode || `RO-${order.id}`,
      branchLabel: fallbackBranchLabel,
      status: normalizedStatus,
      customerNotes: order.customerNotes || "",
      createdAt: order.createdAt,
      updatedAt: order.updatedAt,
      subTotal: Number(order?.subTotal || 0),
      taxAmount: Number(order?.taxAmount || 0),
      tierDiscountAmount: Number(order?.tierDiscountAmount || 0),
      voucherDiscountAmount: Number(order?.voucherDiscountAmount || 0),
      totalDiscountAmount: Number(order?.totalDiscountAmount || 0),
      finalAmount: Number(order?.finalAmount || 0),
      items: [],
    });
    setDetailLoading(true);

    try {
      const detail = await getRetailOrderById(order.id);
      const detailItems = getOrderItems(detail);
      const detailBranchId = detail?.branchId ?? detail?.BranchId ?? order?.branchId;
      const detailBranch = branchById.get(String(detailBranchId));

      const normalizedItems = detailItems.map((item, index) => ({
        id: item?.id ?? item?.Id ?? `${order.id}-${index}`,
        productId: item?.productId ?? item?.ProductId ?? null,
        productName: getDisplayText(item?.productName ?? item?.ProductName),
        productSku: getDisplayText(item?.productSku ?? item?.ProductSku),
        quantity: Number(item?.quantity ?? item?.Quantity ?? 0),
        unitPriceAtTime: Number(item?.unitPriceAtTime ?? item?.UnitPriceAtTime ?? 0),
        subTotal: Number(item?.subTotal ?? item?.SubTotal ?? 0),
        taxRate: Number(item?.taxRate ?? item?.TaxRate ?? 0),
        taxAmount: Number(item?.taxAmount ?? item?.TaxAmount ?? 0),
        finalAmount: Number(item?.finalAmount ?? item?.FinalAmount ?? 0),
      }));

      setDetailOrder({
        id: detail?.id ?? detail?.Id ?? order.id,
        orderCode:
          detail?.orderCode ??
          detail?.OrderCode ??
          order?.orderCode ??
          `RO-${order.id}`,
        branchLabel: detailBranch ? getBranchLabel(detailBranch) : fallbackBranchLabel,
        status: String(detail?.status ?? detail?.Status ?? normalizedStatus).toUpperCase(),
        customerNotes: detail?.customerNotes ?? detail?.CustomerNotes ?? order?.customerNotes ?? "",
        createdAt: detail?.createdAt ?? detail?.CreatedAt ?? order?.createdAt,
        updatedAt: detail?.updatedAt ?? detail?.UpdatedAt ?? order?.updatedAt,
        subTotal: Number(detail?.subTotal ?? detail?.SubTotal ?? order?.subTotal ?? 0),
        taxAmount: Number(detail?.taxAmount ?? detail?.TaxAmount ?? order?.taxAmount ?? 0),
        tierDiscountAmount: Number(
          detail?.tierDiscountAmount ?? detail?.TierDiscountAmount ?? order?.tierDiscountAmount ?? 0
        ),
        voucherDiscountAmount: Number(
          detail?.voucherDiscountAmount ??
            detail?.VoucherDiscountAmount ??
            order?.voucherDiscountAmount ??
            0
        ),
        totalDiscountAmount: Number(
          detail?.totalDiscountAmount ??
            detail?.TotalDiscountAmount ??
            order?.totalDiscountAmount ??
            0
        ),
        finalAmount: Number(detail?.finalAmount ?? detail?.FinalAmount ?? order?.finalAmount ?? 0),
        items: normalizedItems,
      });
    } catch (loadDetailError) {
      setDetailError(loadDetailError?.message || "Không tải được chi tiết đơn phụ tùng.");
    } finally {
      setDetailLoading(false);
    }
  };

  const handlePayRetailOrder = async (order) => {
    const retailOrderId = Number(order?.id);
    const totalAmount = toSafeAmount(order?.finalAmount);
    const orderCode = order?.orderCode || `RO-${retailOrderId || ""}`;
    const payosDescription = `TT ${String(orderCode).slice(0, 22)}`.slice(0, 25);

    if (!Number.isFinite(retailOrderId) || retailOrderId <= 0) {
      const message = "Không xác định được mã đơn retail để thanh toán.";
      setError(message);
      setDetailError(message);
      return;
    }

    if (totalAmount < 1000) {
      const message = "Tổng thanh toán tối thiểu là 1,000đ để tạo liên kết PayOS.";
      setError(message);
      setDetailError(message);
      return;
    }

    setError("");
    setDetailError("");
    setPayingOrderId(retailOrderId);

    try {
      const paymentResult = await createPayosOrder({
        totalAmount,
        description: payosDescription,
        payerType: "CUSTOMER",
        payerId: user?.id || null,
        retailOrderId,
        paymentType: "PAYMENT",
        returnUrl: buildFrontendUrl("/payment/success"),
        cancelUrl: buildFrontendUrl("/payment/cancel"),
        items: [
          {
            name: "Đơn phụ tùng MyGarage",
            quantity: 1,
            price: totalAmount,
          },
        ],
      });

      if (!paymentResult?.ok) {
        throw new Error(paymentResult?.data?.message || "Không tạo được link thanh toán PayOS.");
      }

      const paymentOrder = paymentResult?.data;
      if (!paymentOrder?.checkoutUrl) {
        throw new Error("Không nhận được checkoutUrl từ PayOS.");
      }

      const retryParams = new URLSearchParams({
        amount: String(totalAmount),
        description: payosDescription,
        retailOrderId: String(retailOrderId),
      });

      localStorage.setItem("payos_last_payment_search", `?${retryParams.toString()}`);
      localStorage.setItem("payos_last_checkout_kind", "RETAIL");
      localStorage.setItem("payos_last_retail_order_id", String(retailOrderId));

      if (paymentOrder?.paymentRecordId) {
        localStorage.setItem("payos_last_payment_id", String(paymentOrder.paymentRecordId));
      } else {
        localStorage.removeItem("payos_last_payment_id");
      }

      if (paymentOrder?.id) {
        localStorage.setItem("payos_last_order_id", String(paymentOrder.id));
      } else {
        localStorage.removeItem("payos_last_order_id");
      }

      window.location.href = paymentOrder.checkoutUrl;
    } catch (paymentError) {
      const message = paymentError?.message || "Không thể tạo liên kết thanh toán PayOS.";
      setError(message);
      if (Number(detailOrder?.id) === retailOrderId) {
        setDetailError(message);
      }
    } finally {
      setPayingOrderId(null);
    }
  };

  const detailStatusMeta = detailOrder ? getRetailOrderStatusMeta(detailOrder.status) : null;

  return (
    <div className="parts-shop-page retail-orders-page">
      <section className="parts-shop-hero parts-shop-hero--orders">
        <img
          className="parts-shop-hero-image"
          src="/assets/booking/showcase-1.jpg"
          alt="Không gian premium dành cho trải nghiệm hậu mãi và phụ tùng của MyGarage"
        />
        <div className="parts-shop-hero-overlay" />
        <Header overlay />
        <div className="parts-shop-hero-content">
          <p className="parts-shop-hero-kicker">MYGARAGE RETAIL ORDERS</p>
          <h1>Theo dõi toàn bộ đơn phụ tùng của bạn</h1>
          <p className="parts-shop-hero-sub">
            Trang này bám đúng workflow bán lẻ hiện có của repo: đơn được tạo từ Part Shop,
            status hiện tại đang theo backend DRAFT hoặc CONFIRMED, và thanh toán vẫn đi qua PayOS.
          </p>
          <div className="parts-shop-hero-stats">
            {orderStats.map((item) => (
              <article key={item.label} className="parts-shop-hero-stat">
                <strong>{item.value}</strong>
                <span>{item.label}</span>
              </article>
            ))}
          </div>
        </div>
      </section>

      <div className="parts-shop">
        <section className="parts-shop-header">
          <h2>Đơn phụ tùng của tôi</h2>
          <p>
            Mỗi đơn hiển thị theo dữ liệu backend hiện có: mã đơn, chi nhánh, trạng thái,
            số tiền cuối cùng, ghi chú và thời gian tạo/cập nhật.
          </p>
        </section>

        {error ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div>
        ) : null}

        <section className="parts-shop-card retail-orders-panel">
          {loading ? (
            <div className="parts-shop-empty">Đang tải đơn phụ tùng...</div>
          ) : orders.length === 0 ? (
            <div className="parts-shop-empty parts-shop-empty-panel">
              <p>Bạn chưa có đơn phụ tùng nào.</p>
              <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-primary">
                Mua phụ tùng ngay
              </Link>
            </div>
          ) : (
            <div className="retail-order-list">
              {orders.map((order) => {
                const normalizedStatus = String(order?.status || "DRAFT").toUpperCase();
                const statusMeta = getRetailOrderStatusMeta(normalizedStatus);
                const branch = branchById.get(String(order?.branchId));
                const canRetryPayment =
                  normalizedStatus === "DRAFT" && Number(order?.finalAmount || 0) >= 1000;

                return (
                  <article
                    id={`retail-order-${order.id}`}
                    key={order.id}
                    className={`retail-order-card retail-order-card--clickable ${Number(highlightOrderId) === Number(order.id) ? "retail-order-card--focus" : ""}`}
                    role="button"
                    tabIndex={0}
                    onClick={() => openOrderDetail(order)}
                    onKeyDown={(event) => {
                      if (event.key !== "Enter" && event.key !== " ") return;
                      event.preventDefault();
                      openOrderDetail(order);
                    }}
                  >
                    <div className="retail-order-card-top">
                      <div>
                        <div className="retail-order-card-code">
                          {order.orderCode || `RO-${order.id}`}
                        </div>
                        <p className="retail-order-card-desc">{statusMeta.description}</p>
                        <p className="retail-order-card-hint">Nhấn vào đơn để xem chi tiết thanh toán</p>
                      </div>
                      <RetailOrderStatusBadge status={normalizedStatus} />
                    </div>

                    <dl className="retail-order-grid">
                      <div>
                        <dt>Chi nhánh</dt>
                        <dd>{branch ? getBranchLabel(branch) : `Chi nhánh #${order.branchId}`}</dd>
                      </div>
                      <div>
                        <dt>Tổng thanh toán</dt>
                        <dd>{formatVnd(order.finalAmount)}</dd>
                      </div>
                      <div>
                        <dt>Tạo lúc</dt>
                        <dd>{formatDateTime(order.createdAt)}</dd>
                      </div>
                      <div>
                        <dt>Cập nhật</dt>
                        <dd>{formatDateTime(order.updatedAt)}</dd>
                      </div>
                    </dl>

                    {order.customerNotes ? (
                      <div className="retail-order-notes">
                        <span>Ghi chú khách hàng</span>
                        <p>{order.customerNotes}</p>
                      </div>
                    ) : null}

                    <div
                      className="retail-order-actions"
                      onClick={(event) => event.stopPropagation()}
                    >
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-ghost"
                        onClick={() => openOrderDetail(order)}
                      >
                        Xem chi tiết
                      </button>
                      <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-ghost">
                        Mua thêm phụ tùng
                      </Link>
                      {canRetryPayment ? (
                        <button
                          type="button"
                          className="parts-shop-btn parts-shop-btn-primary"
                          onClick={() => handlePayRetailOrder(order)}
                          disabled={payingOrderId != null}
                        >
                          Thanh toán đơn này
                        </button>
                      ) : null}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
          {summary.total > ORDER_PAGE_SIZE && (
            <nav className="parts-shop-pagination" aria-label={PAGE_LABELS.page}>
              <button type="button" className="parts-shop-page-button" disabled={loading || page <= 1}
                onClick={() => setPage((value) => value - 1)}>{PAGE_LABELS.previous}</button>
              <span>{PAGE_LABELS.page} {page} / {Math.ceil(summary.total / ORDER_PAGE_SIZE)}</span>
              <button type="button" className="parts-shop-page-button" disabled={loading || page * ORDER_PAGE_SIZE >= summary.total}
                onClick={() => setPage((value) => value + 1)}>{PAGE_LABELS.next}</button>
            </nav>
          )}
        </section>
      </div>

      {detailOrder ? (
        <div
          className="parts-shop-modal-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target !== event.currentTarget || detailLoading) return;
            closeOrderDetail();
          }}
        >
          <section
            className="parts-shop-modal retail-order-detail-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="retail-order-detail-title"
          >
            <div className="parts-shop-modal-head">
              <span className="parts-shop-section-kicker">Chi tiết đơn phụ tùng</span>
              <h3 id="retail-order-detail-title">{detailOrder.orderCode || `RO-${detailOrder.id}`}</h3>
              <p>
                Xem đầy đủ sản phẩm, số lượng, đơn giá, VAT và tổng thanh toán trước khi tiếp tục.
              </p>
            </div>

            {detailLoading ? (
              <div className="parts-shop-empty">Đang tải chi tiết đơn...</div>
            ) : detailError ? (
              <div className="parts-shop-alert parts-shop-alert-error">{detailError}</div>
            ) : (
              <>
                <div className="parts-shop-modal-summary">
                  <div className="parts-shop-modal-row">
                    <span>Chi nhánh</span>
                    <strong>{detailOrder.branchLabel}</strong>
                  </div>
                  <div className="parts-shop-modal-row">
                    <span>Trạng thái</span>
                    <strong>{detailStatusMeta?.label || detailOrder.status || "--"}</strong>
                  </div>
                  <div className="parts-shop-modal-row">
                    <span>Tạo lúc</span>
                    <strong>{formatDateTime(detailOrder.createdAt)}</strong>
                  </div>
                  <div className="parts-shop-modal-row">
                    <span>Cập nhật</span>
                    <strong>{formatDateTime(detailOrder.updatedAt)}</strong>
                  </div>
                  <div className="parts-shop-modal-row">
                    <span>Tạm tính</span>
                    <strong>{formatVnd(detailOrder.subTotal)}</strong>
                  </div>
                  <div className="parts-shop-modal-row">
                    <span>Giảm giá</span>
                    <strong>{formatVnd(detailOrder.totalDiscountAmount)}</strong>
                  </div>
                  <div className="parts-shop-modal-row">
                    <span>Thuế VAT</span>
                    <strong>{formatVnd(detailOrder.taxAmount)}</strong>
                  </div>
                  <div className="parts-shop-modal-row parts-shop-modal-row-total">
                    <span>Tổng thanh toán</span>
                    <strong>{formatVnd(detailOrder.finalAmount)}</strong>
                  </div>
                </div>

                <div className="parts-shop-modal-items">
                  <h4>Danh sách sản phẩm</h4>
                  {Array.isArray(detailOrder.items) && detailOrder.items.length > 0 ? (
                    <div className="parts-shop-modal-item-list">
                      {detailOrder.items.map((item) => (
                        <article key={item.id} className="parts-shop-modal-item">
                          <div className="parts-shop-modal-item-main">
                            <strong>{item.productName || `Sản phẩm #${item.productId ?? "?"}`}</strong>
                            <small>{item.productSku ? `SKU ${item.productSku}` : `ID ${item.productId ?? "?"}`}</small>
                          </div>
                          <div className="parts-shop-modal-item-metrics">
                            <span>SL: {formatQuantity(item.quantity)}</span>
                            <span>x {formatVnd(item.unitPriceAtTime)}</span>
                            <strong>{formatVnd(item.subTotal)}</strong>
                          </div>
                          <div className="parts-shop-modal-item-tax">
                            <span>VAT {formatRate(item.taxRate)}: {formatVnd(item.taxAmount)}</span>
                            <strong>Dòng tiền: {formatVnd(item.finalAmount)}</strong>
                          </div>
                        </article>
                      ))}
                    </div>
                  ) : (
                    <div className="parts-shop-empty parts-shop-empty-panel">
                      Chưa có dòng sản phẩm cho đơn này.
                    </div>
                  )}
                </div>

                {detailOrder.customerNotes ? (
                  <div className="parts-shop-modal-note">
                    <span>Ghi chú đơn hàng</span>
                    <p>{detailOrder.customerNotes}</p>
                  </div>
                ) : null}
              </>
            )}

            <div className="parts-shop-modal-actions">
              {String(detailOrder?.status || "").toUpperCase() === "DRAFT" &&
              Number(detailOrder?.finalAmount || 0) >= 1000 ? (
                <button
                  type="button"
                  className="parts-shop-btn parts-shop-btn-primary"
                  onClick={() => handlePayRetailOrder(detailOrder)}
                  disabled={payingOrderId != null}
                >
                  Thanh toán đơn này
                </button>
              ) : null}
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-ghost"
                onClick={closeOrderDetail}
                disabled={detailLoading || payingOrderId != null}
              >
                Đóng
              </button>
            </div>
          </section>
        </div>
      ) : null}

      <Footer />
    </div>
  );
}
