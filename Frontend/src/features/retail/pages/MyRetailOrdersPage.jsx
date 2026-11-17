import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import RetailOrderStatusBadge from "@features/retail/components/RetailOrderStatusBadge";
import { createPayosOrder, syncOrder } from "@features/payment/api/payosApi";
import {
  cancelRetailOrder,
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
  RETAIL_CART_STORAGE_KEY,
} from "@features/retail/utils/retailFormat";
import {
  Package,
  Clock,
  CheckCircle2,
  ShieldCheck,
  CreditCard,
  Eye,
  Calendar,
  MapPin,
  Receipt,
  Search,
  X,
  ArrowRight,
  ShoppingBag,
  Sparkles,
  RefreshCw,
  AlertCircle,
  ChevronRight,
  Lock,
  RotateCcw,
  XCircle,
} from "lucide-react";
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

function isOrderTimeout(createdAt) {
  if (!createdAt) return false;
  const createdTime = new Date(createdAt).getTime();
  if (Number.isNaN(createdTime)) return false;
  return Date.now() - createdTime > 15 * 60 * 1000;
}

function resolveEffectiveOrderStatus(order) {
  const rawStatus = String(order?.status || "DRAFT").toUpperCase();
  if (rawStatus === "DRAFT" && isOrderTimeout(order?.createdAt)) {
    return "CANCELED";
  }
  return rawStatus;
}

function formatRate(value) {
  const rate = Number(value);
  if (!Number.isFinite(rate)) return "0%";
  return `${rate.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%`;
}

export default function MyRetailOrdersPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [orders, setOrders] = useState([]);
  const [page, setPage] = useState(1);
  const [summary, setSummary] = useState({ total: 0, draftCount: 0, confirmedCount: 0, canceledCount: 0, totalValue: 0 });
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
  const [cancellingOrderId, setCancellingOrderId] = useState(null);

  // Filter and Search states
  const [statusFilter, setStatusFilter] = useState("ALL"); // ALL | DRAFT | CONFIRMED | CANCELED
  const [searchTerm, setSearchTerm] = useState("");

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

        const canceledCount = nextOrders.filter((o) => {
          const st = resolveEffectiveOrderStatus(o);
          return st === "CANCELED" || st === "CANCELLED";
        }).length;

        setOrders(nextOrders);
        setSummary({
          total: Number(orderData?.total) || 0,
          draftCount: Number(orderData?.draftCount) || 0,
          confirmedCount: Number(orderData?.confirmedCount) || 0,
          canceledCount,
          totalValue: Number(orderData?.totalValue) || 0,
        });
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

  const filteredOrders = useMemo(() => {
    return orders.filter((order) => {
      const normalizedStatus = resolveEffectiveOrderStatus(order);
      if (statusFilter !== "ALL") {
        if (statusFilter === "CANCELED") {
          if (normalizedStatus !== "CANCELED" && normalizedStatus !== "CANCELLED") return false;
        } else if (normalizedStatus !== statusFilter) {
          return false;
        }
      }

      if (searchTerm.trim()) {
        const query = searchTerm.trim().toLowerCase();
        const code = String(order.orderCode || `RO-${order.id}`).toLowerCase();
        const branch = branchById.get(String(order?.branchId));
        const branchName = branch ? getBranchLabel(branch).toLowerCase() : "";
        const notes = String(order.customerNotes || "").toLowerCase();

        if (!code.includes(query) && !branchName.includes(query) && !notes.includes(query)) {
          return false;
        }
      }

      return true;
    });
  }, [orders, statusFilter, searchTerm, branchById]);

  const handleCancelOrder = async (order, event) => {
    if (event) event.stopPropagation();
    const orderId = Number(order?.id);
    if (!Number.isFinite(orderId) || orderId <= 0) return;

    const code = order.orderCode || `RO-${orderId}`;
    if (!window.confirm(`Bạn có chắc chắn muốn hủy đơn hàng #${code} không? Lượt voucher (nếu có) sẽ được hoàn trả lại.`)) {
      return;
    }

    setCancellingOrderId(orderId);
    try {
      await cancelRetailOrder(orderId, "Khách hàng chủ động hủy trên danh sách đơn");
      setRefreshVersion((v) => v + 1);
      if (detailOrder?.id === orderId) {
        setDetailOrder((prev) => (prev ? { ...prev, status: "CANCELED" } : null));
      }
    } catch (cancelErr) {
      alert(cancelErr?.message || "Không thể hủy đơn hàng lúc này.");
    } finally {
      setCancellingOrderId(null);
    }
  };

  const handleReorder = async (order, event) => {
    if (event) event.stopPropagation();
    const orderId = Number(order?.id);
    if (!Number.isFinite(orderId) || orderId <= 0) return;

    try {
      let items = order.items || [];
      if (!items.length) {
        const detail = await getRetailOrderById(orderId);
        items = getOrderItems(detail);
      }

      if (!items.length) {
        alert("Đơn hàng không có sản phẩm nào để mua lại.");
        return;
      }

      const raw = localStorage.getItem(RETAIL_CART_STORAGE_KEY);
      let existingCart = [];
      try {
        existingCart = raw ? JSON.parse(raw) : [];
        if (!Array.isArray(existingCart)) existingCart = [];
      } catch {
        existingCart = [];
      }

      const existingMap = new Map(existingCart.map((it) => [Number(it.id), it]));

      items.forEach((item) => {
        const id = Number(item.productId || item.id);
        if (!Number.isFinite(id) || id <= 0) return;
        const qty = Math.max(1, Number(item.quantity || item.qty || 1));

        if (existingMap.has(id)) {
          const current = existingMap.get(id);
          existingMap.set(id, {
            ...current,
            qty: (current.qty || 1) + qty,
            selected: true,
          });
        } else {
          existingMap.set(id, {
            id,
            name: item.productName || item.name || `Phụ tùng #${id}`,
            sku: item.productSku || item.sku || null,
            unit: item.unit || null,
            retailPrice: Number(item.unitPriceAtTime || item.retailPrice || 0),
            qty,
            selected: true,
            categoryId: item.categoryId || null,
            imageUrl: item.imageUrl || null,
          });
        }
      });

      localStorage.setItem(RETAIL_CART_STORAGE_KEY, JSON.stringify(Array.from(existingMap.values())));
      navigate("/parts-cart");
    } catch (reorderErr) {
      console.error("Reorder failed:", reorderErr);
      navigate("/parts-cart");
    }
  };

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
    const targetOrderId =
      Number.isFinite(queryOrderId) && queryOrderId > 0
        ? queryOrderId
        : Number.isFinite(storedRetailOrderId) && storedRetailOrderId > 0
        ? storedRetailOrderId
        : 0;
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
          setOrders((previous) =>
            previous.map((order) =>
              Number(order?.id) === targetOrderId ? { ...order, ...targetOrder } : order
            )
          );
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
    <div className="retail-orders-page">
      {/* LUXURY HERO SECTION */}
      <section className="retail-orders-hero">
        <img
          className="retail-orders-hero-bg"
          src="/assets/booking/showcase-1.jpg"
          alt="Không gian dịch vụ phụ tùng cao cấp MyGarage"
        />
        <div className="retail-orders-hero-mask" />
        <Header overlay />

        <div className="retail-orders-hero-container">
          <div className="retail-orders-hero-left">
            <span className="retail-orders-hero-badge">
              <Sparkles size={14} /> MYGARAGE GENUINE CARE
            </span>
            <h1 className="retail-orders-hero-title">Lịch Sử & Quản Lý Đơn Hàng</h1>
            <p className="retail-orders-hero-desc">
              Theo dõi chi tiết tiến trình đặt hàng linh kiện, phụ tùng và phụ kiện chính hãng tại MyGarage.
              Tra cứu thông tin, bảo hành và thanh toán trực tuyến an toàn, minh bạch.
            </p>
            <div className="retail-orders-hero-actions">
              <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-primary">
                <ShoppingBag size={17} /> Khám phá Part Shop
              </Link>
              <Link to="/booking" className="parts-shop-btn parts-shop-btn-ghost">
                <Calendar size={17} /> Đặt lịch dịch vụ
              </Link>
            </div>
          </div>

          <div className="retail-orders-hero-right">
            <div className="retail-orders-dashboard-card">
              <div className="retail-dash-header">
                <div className="retail-dash-title">
                  <Package size={18} />
                  <span>Tổng quan đơn hàng</span>
                </div>
                <div className="retail-dash-badge">
                  <span className="retail-dash-dot" />
                  <span>Trực tuyến</span>
                </div>
              </div>

              <div className="retail-dash-grid">
                <article className="retail-dash-item">
                  <div className="retail-dash-icon-wrap icon-total">
                    <Package size={20} />
                  </div>
                  <div className="retail-dash-info">
                    <strong>{summary.total}</strong>
                    <span>Tổng đơn đã tạo</span>
                  </div>
                </article>

                <article className="retail-dash-item">
                  <div className="retail-dash-icon-wrap icon-draft">
                    <Clock size={20} />
                  </div>
                  <div className="retail-dash-info">
                    <strong>{summary.draftCount}</strong>
                    <span>Chờ thanh toán</span>
                  </div>
                </article>

                <article className="retail-dash-item">
                  <div className="retail-dash-icon-wrap icon-confirmed">
                    <CheckCircle2 size={20} />
                  </div>
                  <div className="retail-dash-info">
                    <strong>{summary.confirmedCount}</strong>
                    <span>Đã xác nhận</span>
                  </div>
                </article>

                <article className="retail-dash-item">
                  <div className="retail-dash-icon-wrap icon-value">
                    <Receipt size={20} />
                  </div>
                  <div className="retail-dash-info">
                    <strong className="text-gold">{formatVnd(summary.totalValue)}</strong>
                    <span>Tổng giá trị đơn</span>
                  </div>
                </article>
              </div>

              <div className="retail-dash-footer">
                <ShieldCheck size={16} />
                <span>Thanh toán bảo mật qua PayOS & hỗ trợ bảo hành chính hãng</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* MAIN CONTENT AREA */}
      <main className="retail-orders-main">
        <div className="retail-orders-container">
          {/* TOOLBAR: FILTER TABS & SEARCH */}
          <div className="retail-orders-toolbar">
            <div className="retail-orders-tabs" role="tablist" aria-label="Lọc đơn hàng">
              <button
                type="button"
                role="tab"
                aria-selected={statusFilter === "ALL"}
                className={`retail-tab-btn ${statusFilter === "ALL" ? "is-active" : ""}`}
                onClick={() => setStatusFilter("ALL")}
              >
                <span>Tất cả</span>
                <span className="retail-tab-count">{summary.total}</span>
              </button>

              <button
                type="button"
                role="tab"
                aria-selected={statusFilter === "DRAFT"}
                className={`retail-tab-btn ${statusFilter === "DRAFT" ? "is-active" : ""}`}
                onClick={() => setStatusFilter("DRAFT")}
              >
                <Clock size={15} />
                <span>Chờ thanh toán</span>
                <span className="retail-tab-count">{summary.draftCount}</span>
              </button>

              <button
                type="button"
                role="tab"
                aria-selected={statusFilter === "CONFIRMED"}
                className={`retail-tab-btn ${statusFilter === "CONFIRMED" ? "is-active" : ""}`}
                onClick={() => setStatusFilter("CONFIRMED")}
              >
                <CheckCircle2 size={15} />
                <span>Đã xác nhận</span>
                <span className="retail-tab-count">{summary.confirmedCount}</span>
              </button>

              <button
                type="button"
                role="tab"
                aria-selected={statusFilter === "CANCELED"}
                className={`retail-tab-btn ${statusFilter === "CANCELED" ? "is-active" : ""}`}
                onClick={() => setStatusFilter("CANCELED")}
              >
                <XCircle size={15} />
                <span>Đã hủy</span>
                <span className="retail-tab-count">{summary.canceledCount || 0}</span>
              </button>
            </div>

            <div className="retail-orders-search-wrap">
              <Search size={17} className="retail-search-icon" />
              <input
                type="text"
                className="retail-search-input"
                placeholder="Tìm mã đơn, chi nhánh, ghi chú..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
              {searchTerm && (
                <button
                  type="button"
                  className="retail-search-clear"
                  onClick={() => setSearchTerm("")}
                  aria-label="Xóa tìm kiếm"
                >
                  <X size={15} />
                </button>
              )}
            </div>
          </div>

          {error && (
            <div className="retail-alert retail-alert-error">
              <AlertCircle size={18} />
              <span>{error}</span>
            </div>
          )}

          {/* ORDER CARDS SECTION */}
          {loading ? (
            <div className="retail-orders-loading">
              <RefreshCw size={28} className="retail-spin" />
              <span>Đang tải danh sách đơn phụ tùng của bạn...</span>
            </div>
          ) : filteredOrders.length === 0 ? (
            <div className="retail-orders-empty-card">
              <div className="retail-empty-icon-wrap">
                <ShoppingBag size={42} />
              </div>
              <h3>Không tìm thấy đơn phụ tùng nào</h3>
              <p>
                {searchTerm || statusFilter !== "ALL"
                  ? "Không có đơn hàng nào khớp với điều kiện tìm kiếm hoặc bộ lọc hiện tại."
                  : "Bạn chưa có đơn đặt phụ tùng nào tại hệ thống MyGarage."}
              </p>
              <div className="retail-empty-actions">
                {searchTerm || statusFilter !== "ALL" ? (
                  <button
                    type="button"
                    className="parts-shop-btn parts-shop-btn-ghost"
                    onClick={() => {
                      setStatusFilter("ALL");
                      setSearchTerm("");
                    }}
                  >
                    Xóa bộ lọc
                  </button>
                ) : null}
                <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-primary">
                  Khám phá cửa hàng phụ tùng
                </Link>
              </div>
            </div>
          ) : (
            <div className="retail-orders-grid">
              {filteredOrders.map((order) => {
                const normalizedStatus = resolveEffectiveOrderStatus(order);
                const isCancelled = normalizedStatus === "CANCELED" || normalizedStatus === "CANCELLED";
                const isDraft = normalizedStatus === "DRAFT";
                const statusMeta = getRetailOrderStatusMeta(normalizedStatus);
                const branch = branchById.get(String(order?.branchId));
                const canRetryPayment =
                  isDraft && Number(order?.finalAmount || 0) >= 1000;
                const isFocused = Number(highlightOrderId) === Number(order.id);

                return (
                  <article
                    id={`retail-order-${order.id}`}
                    key={order.id}
                    className={`retail-card ${isFocused ? "is-focused" : ""}`}
                    onClick={() => openOrderDetail(order)}
                  >
                    {/* CARD HEADER */}
                    <div className="retail-card-head">
                      <div className="retail-card-head-left">
                        <span className="retail-card-code-badge">
                          #{order.orderCode || `RO-${order.id}`}
                        </span>
                        <span className="retail-card-meta-pill">
                          <MapPin size={13} />
                          {branch ? getBranchLabel(branch) : `Chi nhánh #${order.branchId}`}
                        </span>
                        <span className="retail-card-meta-pill">
                          <Calendar size={13} />
                          {formatDateTime(order.createdAt)}
                        </span>
                      </div>
                      <RetailOrderStatusBadge status={normalizedStatus} />
                    </div>

                    {/* CARD BODY */}
                    <div className="retail-card-body">
                      <div className="retail-card-desc-wrap">
                        <p className="retail-card-desc">{statusMeta.description}</p>
                        {order.customerNotes ? (
                          <div className="retail-card-notes">
                            <span className="notes-tag">Ghi chú của bạn:</span>
                            <p className="notes-content">"{order.customerNotes}"</p>
                          </div>
                        ) : null}
                      </div>

                      <div className="retail-card-price-box">
                        <span className="price-label">Tổng thanh toán</span>
                        <span className="price-val">{formatVnd(order.finalAmount)}</span>
                      </div>
                    </div>

                    {/* CARD FOOTER */}
                    <div className="retail-card-foot" onClick={(e) => e.stopPropagation()}>
                      <div className="retail-card-foot-meta">
                        <span>Cập nhật: {formatDateTime(order.updatedAt)}</span>
                      </div>

                      <div className="retail-card-foot-actions">
                        <button
                          type="button"
                          className="retail-btn-action retail-btn-detail"
                          onClick={() => openOrderDetail(order)}
                        >
                          <Eye size={15} />
                          <span>Chi tiết</span>
                        </button>

                        {canRetryPayment && (
                          <>
                            <button
                              type="button"
                              className="retail-btn-action retail-btn-pay"
                              onClick={() => handlePayRetailOrder(order)}
                              disabled={payingOrderId != null || cancellingOrderId != null}
                            >
                              <CreditCard size={15} />
                              <span>
                                {payingOrderId === order.id ? "Đang xử lý..." : "Thanh toán PayOS"}
                              </span>
                            </button>
                            <button
                              type="button"
                              className="retail-btn-action retail-btn-cancel"
                              onClick={(e) => handleCancelOrder(order, e)}
                              disabled={payingOrderId != null || cancellingOrderId === order.id}
                              title="Hủy đơn hàng này"
                            >
                              <XCircle size={15} />
                              <span>{cancellingOrderId === order.id ? "Đang hủy..." : "Hủy đơn"}</span>
                            </button>
                          </>
                        )}

                        {isCancelled && (
                          <button
                            type="button"
                            className="retail-btn-action retail-btn-reorder"
                            onClick={(e) => handleReorder(order, e)}
                            title="Thêm lại các sản phẩm vào giỏ để đặt lại"
                          >
                            <RotateCcw size={15} />
                            <span>Mua lại</span>
                          </button>
                        )}

                        <Link to="/parts-shop" className="retail-btn-action retail-btn-link">
                          <span>Mua thêm</span>
                          <ArrowRight size={14} />
                        </Link>
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          )}

          {/* PAGINATION */}
          {summary.total > ORDER_PAGE_SIZE && (
            <nav className="retail-pagination" aria-label={PAGE_LABELS.page}>
              <button
                type="button"
                className="retail-page-btn"
                disabled={loading || page <= 1}
                onClick={() => setPage((value) => value - 1)}
              >
                {PAGE_LABELS.previous}
              </button>
              <span className="retail-page-text">
                {PAGE_LABELS.page} <strong>{page}</strong> / {Math.ceil(summary.total / ORDER_PAGE_SIZE)}
              </span>
              <button
                type="button"
                className="retail-page-btn"
                disabled={loading || page * ORDER_PAGE_SIZE >= summary.total}
                onClick={() => setPage((value) => value + 1)}
              >
                {PAGE_LABELS.next}
              </button>
            </nav>
          )}
        </div>
      </main>

      {/* DETAIL MODAL */}
      {detailOrder ? (
        <div
          className="retail-modal-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target !== event.currentTarget || detailLoading) return;
            closeOrderDetail();
          }}
        >
          <div
            className="retail-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="retail-modal-title"
          >
            {/* MODAL HEADER */}
            <div className="retail-modal-header">
              <div className="retail-modal-header-info">
                <div className="retail-modal-tag">
                  <Package size={14} />
                  <span>Chi tiết đơn phụ tùng</span>
                </div>
                <div className="retail-modal-title-row">
                  <h3 id="retail-modal-title">#{detailOrder.orderCode || `RO-${detailOrder.id}`}</h3>
                  <RetailOrderStatusBadge status={detailOrder.status} />
                </div>
              </div>

              <button
                type="button"
                className="retail-modal-close"
                onClick={closeOrderDetail}
                disabled={detailLoading || payingOrderId != null}
                aria-label="Đóng"
              >
                <X size={20} />
              </button>
            </div>

            {/* MODAL BODY */}
            <div className="retail-modal-body">
              {detailLoading ? (
                <div className="retail-modal-loading">
                  <RefreshCw size={24} className="retail-spin" />
                  <span>Đang tải thông tin chi tiết đơn hàng...</span>
                </div>
              ) : detailError ? (
                <div className="retail-alert retail-alert-error">
                  <AlertCircle size={18} />
                  <span>{detailError}</span>
                </div>
              ) : (
                <>
                  {/* METRIC STRIP */}
                  <div className="retail-modal-strip">
                    <div className="retail-modal-strip-item">
                      <div className="strip-icon">
                        <MapPin size={18} />
                      </div>
                      <div className="strip-info">
                        <span className="strip-label">Chi nhánh nhận đơn</span>
                        <strong className="strip-val">{detailOrder.branchLabel}</strong>
                      </div>
                    </div>

                    <div className="retail-modal-strip-item">
                      <div className="strip-icon">
                        <Calendar size={18} />
                      </div>
                      <div className="strip-info">
                        <span className="strip-label">Thời gian tạo đơn</span>
                        <strong className="strip-val">{formatDateTime(detailOrder.createdAt)}</strong>
                      </div>
                    </div>

                    <div className="retail-modal-strip-item">
                      <div className="strip-icon">
                        <ShieldCheck size={18} />
                      </div>
                      <div className="strip-info">
                        <span className="strip-label">Tình trạng đơn</span>
                        <strong className="strip-val">
                          {detailStatusMeta?.label || detailOrder.status || "--"}
                        </strong>
                      </div>
                    </div>
                  </div>

                  {/* ITEM LIST */}
                  <div className="retail-modal-items-section">
                    <h4 className="retail-modal-section-title">
                      Danh sách sản phẩm ({detailOrder.items?.length || 0})
                    </h4>

                    {Array.isArray(detailOrder.items) && detailOrder.items.length > 0 ? (
                      <div className="retail-modal-item-list">
                        {detailOrder.items.map((item) => (
                          <div key={item.id} className="retail-modal-item-card">
                            <div className="item-card-left">
                              <h5 className="item-name">
                                {item.productName || `Sản phẩm #${item.productId ?? "?"}`}
                              </h5>
                              <div className="item-tags">
                                {item.productSku && (
                                  <span className="item-sku">SKU: {item.productSku}</span>
                                )}
                                <span className="item-unit-price">
                                  Đơn giá: {formatVnd(item.unitPriceAtTime)}
                                </span>
                              </div>
                            </div>

                            <div className="item-card-right">
                              <div className="item-qty-tag">x{formatQuantity(item.quantity)}</div>
                              <div className="item-price-calc">
                                <span className="item-sub">Tạm tính: {formatVnd(item.subTotal)}</span>
                                <span className="item-tax">
                                  VAT ({formatRate(item.taxRate)}): {formatVnd(item.taxAmount)}
                                </span>
                                <strong className="item-final">{formatVnd(item.finalAmount)}</strong>
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="retail-modal-no-items">
                        <p>Chưa có danh sách sản phẩm hiển thị cho đơn này.</p>
                      </div>
                    )}
                  </div>

                  {/* CUSTOMER NOTE IF PRESENT */}
                  {detailOrder.customerNotes && (
                    <div className="retail-modal-note-box">
                      <span className="note-title">Ghi chú của bạn:</span>
                      <p className="note-desc">{detailOrder.customerNotes}</p>
                    </div>
                  )}

                  {/* FINANCIAL BREAKDOWN */}
                  <div className="retail-modal-financials">
                    <div className="financial-row">
                      <span>Tiền hàng tạm tính</span>
                      <strong>{formatVnd(detailOrder.subTotal)}</strong>
                    </div>

                    {Number(detailOrder.totalDiscountAmount) > 0 && (
                      <div className="financial-row text-success">
                        <span>Giảm giá & ưu đãi</span>
                        <strong>-{formatVnd(detailOrder.totalDiscountAmount)}</strong>
                      </div>
                    )}

                    <div className="financial-row">
                      <span>Thuế VAT</span>
                      <strong>{formatVnd(detailOrder.taxAmount)}</strong>
                    </div>

                    <div className="financial-row financial-total">
                      <span>Tổng thanh toán</span>
                      <strong className="total-highlight">{formatVnd(detailOrder.finalAmount)}</strong>
                    </div>
                  </div>
                </>
              )}
            </div>

            {/* MODAL FOOTER */}
            <div className="retail-modal-footer">
              {resolveEffectiveOrderStatus(detailOrder) === "DRAFT" &&
              Number(detailOrder?.finalAmount || 0) >= 1000 ? (
                <>
                  <button
                    type="button"
                    className="parts-shop-btn parts-shop-btn-primary modal-pay-btn"
                    onClick={() => handlePayRetailOrder(detailOrder)}
                    disabled={payingOrderId != null || cancellingOrderId != null}
                  >
                    <Lock size={16} />
                    <span>
                      {payingOrderId === detailOrder.id ? "Đang xử lý..." : "Thanh toán ngay qua PayOS"}
                    </span>
                  </button>
                  <button
                    type="button"
                    className="parts-shop-btn modal-cancel-btn"
                    onClick={(e) => handleCancelOrder(detailOrder, e)}
                    disabled={payingOrderId != null || cancellingOrderId != null}
                  >
                    <XCircle size={16} />
                    <span>
                      {cancellingOrderId === detailOrder.id ? "Đang hủy..." : "Hủy đơn hàng"}
                    </span>
                  </button>
                </>
              ) : null}

              {(resolveEffectiveOrderStatus(detailOrder) === "CANCELED" ||
                resolveEffectiveOrderStatus(detailOrder) === "CANCELLED") ? (
                <button
                  type="button"
                  className="parts-shop-btn parts-shop-btn-primary modal-pay-btn"
                  onClick={(e) => handleReorder(detailOrder, e)}
                >
                  <RotateCcw size={16} />
                  <span>Mua lại đơn này</span>
                </button>
              ) : null}

              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-ghost modal-close-btn"
                onClick={closeOrderDetail}
                disabled={detailLoading || payingOrderId != null}
              >
                Đóng cửa sổ
              </button>
            </div>
          </div>
        </div>
      ) : null}

      <Footer />
    </div>
  );
}
