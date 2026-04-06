import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import { createPayosOrder } from "@features/payment/api/payosApi";
import RetailImage from "@features/retail/components/RetailImage";
import {
  checkoutRetailOrder,
  getBranches,
  getCategories,
  getProductImages,
  getProducts,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  formatVnd,
  getBranchLabel,
  getCategoryLabel,
  getProductDescription,
  getProductImageAlt,
  getProductName,
  normalizeText,
  resolveProductImage,
  sortProductImages,
  toPositiveInt,
} from "@features/retail/utils/retailFormat";
import "./RetailShared.css";
import "./PartsSalesPage.css";

const SHOP_HIGHLIGHTS = [
  { value: "Chính hãng", label: "Ưu tiên danh mục phụ tùng phù hợp với workflow tồn kho" },
  { value: "1 lần", label: "Tạo đơn và chuyển sang thanh toán ngay trên cùng flow khách hàng" },
  { value: "Minh bạch", label: "Xem rõ SKU, chi nhánh nhận hàng và ghi chú đơn bán lẻ" },
];
const PRODUCTS_PER_PAGE = 9;

function CatalogPagination({ currentPage, totalPages, onPageChange }) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav className="parts-shop-pagination" aria-label="Phân trang phụ tùng">
      <button
        type="button"
        className="parts-shop-page-button"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage <= 1}
      >
        Trước
      </button>

      <div className="parts-shop-page-numbers">
        {Array.from({ length: totalPages }, (_, index) => {
          const page = index + 1;
          return (
            <button
              type="button"
              key={page}
              className={`parts-shop-page-button ${page === currentPage ? "active" : ""}`}
              aria-current={page === currentPage ? "page" : undefined}
              onClick={() => onPageChange(page)}
            >
              {page}
            </button>
          );
        })}
      </div>

      <button
        type="button"
        className="parts-shop-page-button"
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage >= totalPages}
      >
        Sau
      </button>
    </nav>
  );
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

function openPayosPopupWindow() {
  if (typeof window === "undefined") return null;

  const popup = window.open("", "mygarage_payos_checkout", "width=980,height=760");
  if (!popup) return null;

  popup.document.write(
    "<!doctype html><html><head><meta charset='utf-8' /><title>MyGarage PayOS</title></head><body style='font-family:Segoe UI,Arial,sans-serif;padding:20px;color:#0f172a;background:#f8fafc'><h3 style='margin:0 0 8px'>MyGarage</h3><p style='margin:0'>Đang khởi tạo cổng thanh toán PayOS...</p></body></html>"
  );
  popup.document.close();
  return popup;
}

export default function PartsSalesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const catalogPanelRef = useRef(null);
  const pendingScrollPageRef = useRef(null);
  const [products, setProducts] = useState([]);
  const [productImages, setProductImages] = useState([]);
  const [categories, setCategories] = useState([]);
  const [branches, setBranches] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [catFilter, setCatFilter] = useState("all");
  const [sortBy, setSortBy] = useState("name-asc");
  const [currentPage, setCurrentPage] = useState(1);
  const [draftQty, setDraftQty] = useState({});
  const [branchId, setBranchId] = useState("");
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [payingOrder, setPayingOrder] = useState(false);
  const [pendingOrder, setPendingOrder] = useState(null);
  const [bannerMessage, setBannerMessage] = useState("");
  const {
    cart,
    subtotal,
    totalItems,
    addToCart,
    clearCart,
    removeFromCart,
    setItemQuantity,
    updateCartQty,
  } = useRetailCart();

  useEffect(() => {
    let ignore = false;

    async function loadData() {
      setLoading(true);
      setError("");

      try {
        const [productData, productImageData, categoryData, branchData] = await Promise.all([
          getProducts(),
          getProductImages(),
          getCategories(),
          getBranches(),
        ]);

        if (ignore) return;

        const nextProducts = Array.isArray(productData)
          ? productData.filter((item) => item?.isActive !== false)
          : [];
        const nextProductImages = Array.isArray(productImageData) ? productImageData : [];
        const nextCategories = Array.isArray(categoryData)
          ? categoryData.filter((item) => item?.isDeleted !== true)
          : [];
        const nextBranches = Array.isArray(branchData)
          ? branchData.filter((item) => item?.isDeleted !== true)
          : [];

        setProducts(nextProducts);
        setProductImages(nextProductImages);
        setCategories(nextCategories);
        setBranches(nextBranches);

        if (nextBranches[0]?.id) {
          setBranchId((current) => current || String(nextBranches[0].id));
        }
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải danh sách phụ tùng lúc này.");
          setProducts([]);
          setProductImages([]);
          setCategories([]);
          setBranches([]);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    loadData();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (currentPage !== 1) {
      pendingScrollPageRef.current = 1;
      setCurrentPage(1);
    }
  }, [search, catFilter, sortBy]);

  function scrollCatalogIntoView() {
    if (typeof window === "undefined") return;

    const container = catalogPanelRef.current;
    if (!container) return;

    const { top } = container.getBoundingClientRect();
    if (top >= -40 && top <= 80) {
      return;
    }

    container.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  const categoriesById = useMemo(
    () => new Map(categories.map((item) => [String(item.id), item])),
    [categories]
  );

  const productImagesByProductId = useMemo(() => {
    const groupedImages = new Map();

    productImages.forEach((item) => {
      const key = String(item?.productId ?? "");
      if (!key) return;

      if (!groupedImages.has(key)) {
        groupedImages.set(key, []);
      }

      groupedImages.get(key).push(item);
    });

    groupedImages.forEach((items, key) => {
      groupedImages.set(key, sortProductImages(items));
    });

    return groupedImages;
  }, [productImages]);

  const filteredProducts = useMemo(() => {
    const normalizedQuery = normalizeText(search);
    const rows = products.filter((product) => {
      const relatedImages = productImagesByProductId.get(String(product?.id)) || [];
      const matchesCategory =
        catFilter === "all" || String(product?.categoryId) === String(catFilter);

      if (!matchesCategory) return false;
      if (!normalizedQuery) return true;

      const haystack = [
        product?.name,
        product?.sku,
        getProductDescription(product, relatedImages, { fallback: false }),
        categoriesById.get(String(product?.categoryId))?.categoryName,
      ]
        .map(normalizeText)
        .join(" ");

      return haystack.includes(normalizedQuery);
    });

    rows.sort((left, right) => {
      if (sortBy === "price-asc") {
        return Number(left?.retailPrice || 0) - Number(right?.retailPrice || 0);
      }
      if (sortBy === "price-desc") {
        return Number(right?.retailPrice || 0) - Number(left?.retailPrice || 0);
      }
      if (sortBy === "name-desc") {
        return getProductName(right).localeCompare(getProductName(left), "vi");
      }
      return getProductName(left).localeCompare(getProductName(right), "vi");
    });

    return rows;
  }, [catFilter, categoriesById, productImagesByProductId, products, search, sortBy]);

  const totalPages = useMemo(
    () => Math.ceil(filteredProducts.length / PRODUCTS_PER_PAGE),
    [filteredProducts.length]
  );
  const safeCurrentPage = totalPages === 0 ? 1 : Math.min(currentPage, totalPages);

  useEffect(() => {
    if (totalPages === 0) {
      if (currentPage !== 1) {
        setCurrentPage(1);
      }
      return;
    }

    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  useEffect(() => {
    if (pendingScrollPageRef.current == null) {
      return undefined;
    }

    if (Number(currentPage) !== Number(pendingScrollPageRef.current)) {
      return undefined;
    }

    pendingScrollPageRef.current = null;

    const frameId = window.requestAnimationFrame(() => {
      scrollCatalogIntoView();
    });

    return () => window.cancelAnimationFrame(frameId);
  }, [currentPage]);

  const paginatedProducts = useMemo(() => {
    const startIndex = (safeCurrentPage - 1) * PRODUCTS_PER_PAGE;
    return filteredProducts.slice(startIndex, startIndex + PRODUCTS_PER_PAGE);
  }, [filteredProducts, safeCurrentPage]);

  const paginationSummary = useMemo(() => {
    if (!filteredProducts.length) {
      return "";
    }

    const start = (safeCurrentPage - 1) * PRODUCTS_PER_PAGE + 1;
    const end = Math.min(filteredProducts.length, safeCurrentPage * PRODUCTS_PER_PAGE);
    return `Hiển thị ${start}-${end} trên ${filteredProducts.length} phụ tùng`;
  }, [filteredProducts.length, safeCurrentPage]);

  function handlePageChange(nextPage) {
    const normalizedPage = Math.min(
      Math.max(1, Number(nextPage) || 1),
      Math.max(totalPages, 1)
    );

    if (normalizedPage === safeCurrentPage) {
      return;
    }

    pendingScrollPageRef.current = normalizedPage;
    setCurrentPage(normalizedPage);
  }

  const selectedBranch = useMemo(
    () => branches.find((item) => String(item.id) === String(branchId)) || null,
    [branchId, branches]
  );

  const summaryStats = useMemo(
    () => [
      { value: `${products.length}+`, label: "mã phụ tùng đang mở bán" },
      { value: `${categories.length}`, label: "nhóm danh mục đang hiển thị" },
      { value: `${branches.length}`, label: "chi nhánh nhận hàng khả dụng" },
    ],
    [branches.length, categories.length, products.length]
  );

  useEffect(() => {
    function handlePaymentMessage(event) {
      if (event.origin !== window.location.origin) return;

      const data = event.data;
      if (!data || data.type !== "MYGARAGE_RETAIL_PAYMENT_COMPLETED") return;

      const paidOrderId = Number(data.orderId);
      setBannerMessage("Thanh toán thành công. Đang chuyển đến đơn phụ tùng.");
      navigate(
        Number.isFinite(paidOrderId) && paidOrderId > 0
          ? `/my-retail-orders?orderId=${paidOrderId}`
          : "/my-retail-orders"
      );
    }

    window.addEventListener("message", handlePaymentMessage);
    return () => window.removeEventListener("message", handlePaymentMessage);
  }, [navigate]);

  async function handleCheckout() {
    setBannerMessage("");
    setError("");

    if (!branchId) {
      setError("Vui lòng chọn chi nhánh tiếp nhận đơn phụ tùng.");
      return;
    }

    if (!cart.length) {
      setError("Giỏ phụ tùng đang trống.");
      return;
    }

    setSubmitting(true);

    try {
      const payload = {
        branchId: Number(branchId),
        customerNotes: notes.trim() || null,
        items: cart.map((item) => ({
          productId: Number(item.id),
          quantity: toPositiveInt(item.qty, 1),
        })),
      };

      const createdOrder = await checkoutRetailOrder(payload);
      const retailOrderId = Number(createdOrder?.id);

      if (!Number.isFinite(retailOrderId) || retailOrderId <= 0) {
        throw new Error("Không tạo được đơn bán lẻ hợp lệ.");
      }

      const orderCode = createdOrder?.orderCode || `RO-${retailOrderId}`;
      const subTotalAmount =
        toSafeAmount(createdOrder?.subTotal) ||
        toSafeAmount(createdOrder?.subTotalAmount) ||
        toSafeAmount(subtotal);
      const taxAmount = toSafeAmount(createdOrder?.taxAmount);
      const finalAmount =
        toSafeAmount(createdOrder?.finalAmount) || toSafeAmount(subTotalAmount + taxAmount);
      const branchLabel = selectedBranch
        ? getBranchLabel(selectedBranch)
        : `Chi nhánh #${branchId}`;

      if (finalAmount <= 0) {
        throw new Error("Không xác định được tổng thanh toán cho đơn hàng.");
      }

      clearCart();
      setNotes("");
      setPendingOrder({
        id: retailOrderId,
        orderCode,
        branchLabel,
        customerNotes: createdOrder?.customerNotes || payload.customerNotes || "",
        subTotal: subTotalAmount,
        taxAmount,
        finalAmount,
      });
      setBannerMessage(`Đã tạo đơn ${orderCode}. Vui lòng kiểm tra popup chi tiết trước khi thanh toán.`);
    } catch (checkoutError) {
      setError(checkoutError?.message || "Không thể tạo đơn phụ tùng lúc này.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleConfirmPendingOrderPayment() {
    if (!pendingOrder) return;

    setError("");
    setPayingOrder(true);
    let popupWindow = null;

    try {
      if (pendingOrder.finalAmount < 1000) {
        throw new Error("Tổng thanh toán tối thiểu là 1,000đ để tạo liên kết PayOS.");
      }

      popupWindow = openPayosPopupWindow();
      if (!popupWindow) {
        throw new Error("Trình duyệt đang chặn popup. Hãy cho phép popup và thử lại.");
      }

      const payosDescription = `TT ${String(pendingOrder.orderCode).slice(0, 22)}`.slice(0, 25);
      const paymentResult = await createPayosOrder({
        totalAmount: pendingOrder.finalAmount,
        description: payosDescription,
        payerType: "CUSTOMER",
        payerId: user?.id || null,
        retailOrderId: pendingOrder.id,
        paymentType: "PAYMENT",
        returnUrl: buildFrontendUrl("/payment/success"),
        cancelUrl: buildFrontendUrl("/payment/cancel"),
        items: [
          {
            name: "Đơn phụ tùng MyGarage",
            quantity: 1,
            price: pendingOrder.finalAmount,
          },
        ],
      });

      if (!paymentResult?.ok) {
        throw new Error(paymentResult?.data?.message || "Không tạo được link thanh toán PayOS.");
      }

      const paymentOrder = paymentResult.data;
      if (!paymentOrder?.checkoutUrl) {
        throw new Error("Không nhận được checkoutUrl từ PayOS.");
      }

      const retryParams = new URLSearchParams({
        amount: String(pendingOrder.finalAmount),
        description: payosDescription,
        retailOrderId: String(pendingOrder.id),
      });

      localStorage.setItem("payos_last_payment_search", `?${retryParams.toString()}`);
      localStorage.setItem("payos_last_checkout_kind", "RETAIL");
      localStorage.setItem("payos_last_retail_order_id", String(pendingOrder.id));

      if (paymentOrder?.paymentRecordId) {
        localStorage.setItem("payos_last_payment_id", String(paymentOrder.paymentRecordId));
      } else {
        localStorage.removeItem("payos_last_payment_id");
      }

      if (paymentOrder?.id) {
        localStorage.setItem("payos_last_order_id", String(paymentOrder.id));
      }

      popupWindow.location.href = paymentOrder.checkoutUrl;
      popupWindow.focus();
      setBannerMessage(`Đã mở popup PayOS cho đơn ${pendingOrder.orderCode}.`);
      setPendingOrder(null);
    } catch (paymentError) {
      if (popupWindow && !popupWindow.closed) {
        popupWindow.close();
      }
      setError(paymentError?.message || "Không thể tạo liên kết thanh toán PayOS.");
    } finally {
      setPayingOrder(false);
    }
  }

  return (
    <div className="parts-shop-page">
      <section className="parts-shop-hero">
        <img
          className="parts-shop-hero-image"
          src="/assets/booking/hero.jpg"
          alt="Không gian trưng bày và chăm sóc xe cao cấp của MyGarage"
        />
        <div className="parts-shop-hero-overlay" />
        <Header overlay />
        <div className="parts-shop-hero-content">
          <p className="parts-shop-hero-kicker">MYGARAGE PART SHOP</p>
          <h1>Đặt phụ tùng đúng flow khách hàng của MyGarage</h1>
          <p className="parts-shop-hero-sub">
            Chọn phụ tùng, gom vào một đơn bán lẻ, gắn chi nhánh tiếp nhận và đi thẳng sang thanh toán PayOS.
          </p>
          <div className="parts-shop-hero-actions">
            <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-primary">
              Xem đơn của tôi
            </Link>
            <a href="#parts-shop-catalog" className="parts-shop-btn parts-shop-btn-ghost">
              Xem danh mục
            </a>
          </div>
          <div className="parts-shop-hero-stats">
            {SHOP_HIGHLIGHTS.map((item) => (
              <article key={item.value} className="parts-shop-hero-stat">
                <strong>{item.value}</strong>
                <span>{item.label}</span>
              </article>
            ))}
          </div>
        </div>
      </section>

      <div className="parts-shop">
        <section className="parts-shop-header">
          <h2>Danh mục phụ tùng bán lẻ</h2>
          <p>
            Xin chào {user?.fullName || "bạn"}, giỏ hàng bên phải sẽ giữ nguyên đúng flow bán lẻ:
            chọn phụ tùng, chọn chi nhánh, thêm ghi chú rồi checkout sang thanh toán.
          </p>
          <div className="parts-shop-inline-stats">
            {summaryStats.map((item) => (
              <div key={item.label} className="parts-shop-inline-stat">
                <strong>{item.value}</strong>
                <span>{item.label}</span>
              </div>
            ))}
          </div>
        </section>

        {error ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div>
        ) : null}
        {bannerMessage ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-success">
            {bannerMessage}
          </div>
        ) : null}

        <div className="parts-shop-layout" id="parts-shop-catalog">
          <section
            ref={catalogPanelRef}
            className="parts-shop-card parts-shop-product-panel"
          >
            <div className="parts-shop-panel-head">
              <div>
                <span className="parts-shop-section-kicker">Catalog tuyển chọn</span>
                <h3>Phụ tùng nổi bật cho khách hàng retail</h3>
                <p>
                  Tối ưu trải nghiệm tìm kiếm nhanh, chọn đúng danh mục và checkout ngay khi đã chốt giỏ hàng.
                </p>
              </div>
              <div className="parts-shop-results-meta">
                <strong>{filteredProducts.length}</strong>
                <span>sản phẩm đang hiển thị</span>
                {paginationSummary ? (
                  <small className="parts-shop-results-range">{paginationSummary}</small>
                ) : null}
              </div>
            </div>

            <div className="parts-shop-filter-shell">
              <div className="parts-shop-filter">
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Tìm theo tên, SKU hoặc mô tả phụ tùng"
                />
                <div className="parts-shop-filter-right">
                  <select value={sortBy} onChange={(event) => setSortBy(event.target.value)}>
                    <option value="name-asc">Sắp xếp: Tên A-Z</option>
                    <option value="name-desc">Sắp xếp: Tên Z-A</option>
                    <option value="price-asc">Giá: Thấp đến cao</option>
                    <option value="price-desc">Giá: Cao đến thấp</option>
                  </select>
                </div>
              </div>
            </div>

            <div className="parts-shop-pills">
              <button
                type="button"
                className={`parts-shop-pill ${catFilter === "all" ? "active" : ""}`}
                onClick={() => setCatFilter("all")}
              >
                Tất cả
              </button>
              {categories.map((category) => (
                <button
                  type="button"
                  key={category.id}
                  className={`parts-shop-pill ${
                    String(catFilter) === String(category.id) ? "active" : ""
                  }`}
                  onClick={() => setCatFilter(String(category.id))}
                >
                  {getCategoryLabel(category)}
                </button>
              ))}
            </div>

            {loading ? (
              <div className="parts-shop-empty">Đang tải danh mục phụ tùng...</div>
            ) : filteredProducts.length === 0 ? (
              <div className="parts-shop-empty">
                Không có phụ tùng nào khớp bộ lọc hiện tại. Hãy thử đổi từ khóa hoặc danh mục.
              </div>
            ) : (
              <>
                <div className="parts-shop-list">
                  {paginatedProducts.map((product) => {
                    const category = categoriesById.get(String(product?.categoryId));
                    const relatedImages = productImagesByProductId.get(String(product?.id)) || [];
                    const qty = draftQty[product.id] || 1;
                    const productDetailPath = `/parts-shop/products/${product.id}`;
                    const productDescription = getProductDescription(product, relatedImages);
                    const productName = getProductName(product);

                    return (
                      <article className="parts-shop-item" key={product.id}>
                        <Link
                          to={productDetailPath}
                          className="parts-shop-item-link"
                          aria-label={`Xem chi tiết phụ tùng ${productName}`}
                        >
                          <div className="parts-shop-item-media">
                            <RetailImage
                              src={resolveProductImage(product, relatedImages)}
                              alt={getProductImageAlt(product, relatedImages[0])}
                              className="parts-shop-item-image"
                              fallbackClassName="parts-shop-item-media-fallback"
                              fallbackLabel="Ảnh phụ tùng đang cập nhật"
                            />
                          </div>

                          <div className="parts-shop-item-copy">
                            <div className="parts-shop-item-head">
                              <div>
                                <span className="parts-shop-tag">{getCategoryLabel(category)}</span>
                                <h4 className="parts-shop-item-title">{productName}</h4>
                                <div className="parts-shop-meta">
                                  SKU: {product.sku || "Chưa cập nhật"} · ID: #{product.id}
                                </div>
                              </div>
                            </div>

                            <p className="parts-shop-item-desc">{productDescription}</p>

                            <div className="parts-shop-price">{formatVnd(product.retailPrice)}</div>
                          </div>
                        </Link>

                        <div className="parts-shop-actions">
                          <div className="parts-shop-qty">
                            <button
                              type="button"
                              className="parts-shop-btn parts-shop-btn-ghost"
                              onClick={() =>
                                setDraftQty((previous) => ({
                                  ...previous,
                                  [product.id]: Math.max(1, Number(previous[product.id] || 1) - 1),
                                }))
                              }
                            >
                              -
                            </button>
                            <input
                              type="number"
                              min="1"
                              value={qty}
                              onChange={(event) =>
                                setDraftQty((previous) => ({
                                  ...previous,
                                  [product.id]: toPositiveInt(event.target.value, 1),
                                }))
                              }
                            />
                            <button
                              type="button"
                              className="parts-shop-btn parts-shop-btn-ghost"
                              onClick={() =>
                                setDraftQty((previous) => ({
                                  ...previous,
                                  [product.id]: Number(previous[product.id] || 1) + 1,
                                }))
                              }
                            >
                              +
                            </button>
                          </div>

                          <button
                            type="button"
                            className="parts-shop-btn parts-shop-btn-primary parts-shop-action-button"
                            onClick={() => addToCart(product, qty)}
                          >
                            Thêm vào giỏ
                          </button>
                        </div>
                      </article>
                    );
                  })}
                </div>

                <CatalogPagination
                  currentPage={safeCurrentPage}
                  totalPages={totalPages}
                  onPageChange={handlePageChange}
                />
              </>
            )}
          </section>
          <aside className="parts-shop-card parts-shop-sidebar">
            <div className="parts-shop-cart-head">
              <div>
                <span className="parts-shop-section-kicker">Tóm tắt đơn hàng</span>
                <h3>Giỏ phụ tùng</h3>
                <p>{totalItems} sản phẩm trong đơn bán lẻ hiện tại</p>
              </div>
              <Link
                to="/my-retail-orders"
                className="parts-shop-btn parts-shop-btn-ghost parts-shop-btn-inline parts-shop-sidebar-link"
              >
                Đơn đã tạo
              </Link>
            </div>

            {cart.length === 0 ? (
              <div className="parts-shop-empty parts-shop-empty-panel">
                Hãy thêm ít nhất một phụ tùng để tạo đơn và chuyển sang thanh toán.
              </div>
            ) : (
              <div className="parts-shop-cart-list">
                {cart.map((item) => (
                  <div className="parts-shop-cart-row" key={item.id}>
                    <div>
                      <div className="parts-shop-cart-name">{getProductName(item)}</div>
                      <small>
                        {item.sku ? `SKU ${item.sku}` : "Phụ tùng bán lẻ"} · {formatVnd(item.retailPrice)}
                      </small>
                    </div>
                    <div className="parts-shop-cart-actions">
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-ghost"
                        onClick={() => updateCartQty(item.id, -1)}
                      >
                        -
                      </button>
                      <input
                        className="parts-shop-cart-qty-input"
                        type="number"
                        min="1"
                        value={item.qty}
                        onChange={(event) => setItemQuantity(item.id, event.target.value)}
                      />
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-ghost"
                        onClick={() => updateCartQty(item.id, 1)}
                      >
                        +
                      </button>
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-ghost"
                        onClick={() => removeFromCart(item.id)}
                      >
                        Xóa
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            <div className="parts-shop-order-form">
              <label className="parts-shop-field">
                <span>Chi nhánh tiếp nhận</span>
                <select value={branchId} onChange={(event) => setBranchId(event.target.value)}>
                  {branches.map((branch) => (
                    <option key={branch.id} value={branch.id}>
                      {getBranchLabel(branch)}
                    </option>
                  ))}
                </select>
              </label>

              <label className="parts-shop-field">
                <span>Ghi chú cho đơn bán lẻ</span>
                <textarea
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                  rows={4}
                  placeholder="Ví dụ: cần gọi trước khi xác nhận, nhận hàng tại quầy, ưu tiên đúng đời xe..."
                />
              </label>
            </div>

            <div className="parts-shop-summary">
              <div className="parts-shop-summary-row">
                <span>Khách hàng</span>
                <strong>{user?.fullName || "Khách hàng MyGarage"}</strong>
              </div>
              <div className="parts-shop-summary-row">
                <span>Chi nhánh</span>
                <strong>{selectedBranch ? getBranchLabel(selectedBranch) : "Chưa chọn"}</strong>
              </div>
              <div className="parts-shop-summary-row">
                <span>Tạm tính</span>
                <strong>{formatVnd(subtotal)}</strong>
              </div>
              <p className="parts-shop-summary-note">
                Frontend giữ nguyên contract checkout hiện có. Tổng tiền cuối cùng được xác nhận lại từ response của đơn bán lẻ trước khi chuyển sang PayOS.
              </p>
            </div>

            <button
              type="button"
              className="parts-shop-btn parts-shop-btn-primary parts-shop-checkout-btn"
              disabled={submitting || payingOrder || !cart.length || !branchId}
              onClick={handleCheckout}
            >
              {submitting ? "Đang tạo đơn..." : payingOrder ? "Đang mở PayOS..." : "Tạo đơn và thanh toán"}
            </button>
          </aside>
        </div>
      </div>

      {pendingOrder ? (
        <div
          className="parts-shop-modal-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target !== event.currentTarget || payingOrder) return;
            setPendingOrder(null);
          }}
        >
          <section
            className="parts-shop-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="parts-shop-modal-title"
          >
            <div className="parts-shop-modal-head">
              <span className="parts-shop-section-kicker">Chi tiết đơn vừa tạo</span>
              <h3 id="parts-shop-modal-title">Xác nhận trước khi mở PayOS</h3>
              <p>
                Đơn {pendingOrder.orderCode} đã tạo thành công. Kiểm tra tổng tiền và thuế trước
                khi chuyển đến cổng thanh toán.
              </p>
            </div>

            <div className="parts-shop-modal-summary">
              <div className="parts-shop-modal-row">
                <span>Mã đơn</span>
                <strong>{pendingOrder.orderCode}</strong>
              </div>
              <div className="parts-shop-modal-row">
                <span>Chi nhánh</span>
                <strong>{pendingOrder.branchLabel}</strong>
              </div>
              <div className="parts-shop-modal-row">
                <span>Tạm tính</span>
                <strong>{formatVnd(pendingOrder.subTotal)}</strong>
              </div>
              <div className="parts-shop-modal-row">
                <span>Thuế VAT</span>
                <strong>{formatVnd(pendingOrder.taxAmount)}</strong>
              </div>
              <div className="parts-shop-modal-row parts-shop-modal-row-total">
                <span>Tổng thanh toán</span>
                <strong>{formatVnd(pendingOrder.finalAmount)}</strong>
              </div>
            </div>

            {pendingOrder.customerNotes ? (
              <div className="parts-shop-modal-note">
                <span>Ghi chú đơn</span>
                <p>{pendingOrder.customerNotes}</p>
              </div>
            ) : null}

            <div className="parts-shop-modal-actions">
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-primary"
                onClick={handleConfirmPendingOrderPayment}
                disabled={payingOrder}
              >
                {payingOrder ? "Đang tạo link PayOS..." : "Xác nhận và thanh toán"}
              </button>
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-ghost"
                onClick={() => setPendingOrder(null)}
                disabled={payingOrder}
              >
                Đóng
              </button>
              <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-ghost">
                Xem đơn của tôi
              </Link>
            </div>
          </section>
        </div>
      ) : null}

      <Footer />
    </div>
  );
}
