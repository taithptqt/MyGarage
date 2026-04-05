import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  checkoutRetailOrder,
  getBranches,
  getCategories,
  getProducts,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  formatVnd,
  getBranchLabel,
  getCategoryLabel,
  normalizeText,
  resolveProductImage,
  toPositiveInt,
} from "@features/retail/utils/retailFormat";
import "./PartsSalesPage.css";

const SHOP_HIGHLIGHTS = [
  { value: "Chính hãng", label: "Ưu tiên danh mục phụ tùng phù hợp với workflow tồn kho" },
  { value: "1 lần", label: "Tạo đơn và chuyển sang thanh toán ngay trên cùng flow khách hàng" },
  { value: "Minh bạch", label: "Xem rõ SKU, chi nhánh nhận hàng và ghi chú đơn bán lẻ" },
];

function buildPaymentQuery({ amount, orderCode, retailOrderId }) {
  const params = new URLSearchParams({
    amount: String(Math.max(0, Math.floor(Number(amount || 0)))),
    description: `TT ${String(orderCode || `RO-${retailOrderId}`).slice(0, 22)}`,
    retailOrderId: String(retailOrderId),
    autoPay: "1",
  });

  return `/payment?${params.toString()}`;
}

export default function PartsSalesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [branches, setBranches] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [catFilter, setCatFilter] = useState("all");
  const [sortBy, setSortBy] = useState("name-asc");
  const [draftQty, setDraftQty] = useState({});
  const [branchId, setBranchId] = useState("");
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);
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
        const [productData, categoryData, branchData] = await Promise.all([
          getProducts(),
          getCategories(),
          getBranches(),
        ]);

        if (ignore) return;

        const nextProducts = Array.isArray(productData)
          ? productData.filter((item) => item?.isActive !== false)
          : [];
        const nextCategories = Array.isArray(categoryData)
          ? categoryData.filter((item) => item?.isDeleted !== true)
          : [];
        const nextBranches = Array.isArray(branchData)
          ? branchData.filter((item) => item?.isDeleted !== true)
          : [];

        setProducts(nextProducts);
        setCategories(nextCategories);
        setBranches(nextBranches);

        if (nextBranches[0]?.id) {
          setBranchId((current) => current || String(nextBranches[0].id));
        }
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải danh sách phụ tùng lúc này.");
          setProducts([]);
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

  const categoriesById = useMemo(
    () => new Map(categories.map((item) => [String(item.id), item])),
    [categories]
  );

  const filteredProducts = useMemo(() => {
    const normalizedQuery = normalizeText(search);
    const rows = products.filter((product) => {
      const matchesCategory =
        catFilter === "all" || String(product?.categoryId) === String(catFilter);

      if (!matchesCategory) return false;
      if (!normalizedQuery) return true;

      const haystack = [
        product?.name,
        product?.sku,
        product?.description,
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
        return String(right?.name || "").localeCompare(String(left?.name || ""), "vi");
      }
      return String(left?.name || "").localeCompare(String(right?.name || ""), "vi");
    });

    return rows;
  }, [catFilter, categoriesById, products, search, sortBy]);

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
      const finalAmount = Number(createdOrder?.finalAmount || subtotal || 0);

      clearCart();
      setNotes("");
      setBannerMessage(`Đã tạo đơn phụ tùng ${orderCode}. Đang chuyển sang bước thanh toán.`);
      navigate(
        buildPaymentQuery({
          amount: finalAmount,
          orderCode,
          retailOrderId,
        })
      );
    } catch (checkoutError) {
      setError(checkoutError?.message || "Không thể tạo đơn phụ tùng lúc này.");
    } finally {
      setSubmitting(false);
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
          <section className="parts-shop-card parts-shop-product-panel">
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
              <div className="parts-shop-list">
                {filteredProducts.map((product) => {
                  const category = categoriesById.get(String(product?.categoryId));
                  const qty = draftQty[product.id] || 1;

                  return (
                    <article className="parts-shop-item" key={product.id}>
                      <div className="parts-shop-item-media">
                        <img src={resolveProductImage(product)} alt={product.name} />
                      </div>

                      <div className="parts-shop-item-head">
                        <div>
                          <span className="parts-shop-tag">{getCategoryLabel(category)}</span>
                          <h4>{product.name}</h4>
                          <div className="parts-shop-meta">
                            SKU: {product.sku || "Chưa cập nhật"} · ID: #{product.id}
                          </div>
                        </div>
                      </div>

                      <p className="parts-shop-item-desc">
                        {product.description ||
                          "Phụ tùng đang được đồng bộ theo danh mục sản phẩm hiện có của hệ thống."}
                      </p>

                      <div className="parts-shop-price">{formatVnd(product.retailPrice)}</div>

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
                          className="parts-shop-btn parts-shop-btn-primary"
                          onClick={() => addToCart(product, qty)}
                        >
                          Thêm vào giỏ
                        </button>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </section>

          <aside className="parts-shop-card parts-shop-sidebar">
            <div className="parts-shop-cart-head">
              <div>
                <span className="parts-shop-section-kicker">Tóm tắt đơn hàng</span>
                <h3>Giỏ phụ tùng</h3>
                <p>{totalItems} sản phẩm trong đơn bán lẻ hiện tại</p>
              </div>
              <Link to="/my-retail-orders" className="parts-shop-link-button">
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
                      <div className="parts-shop-cart-name">{item.name}</div>
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
              disabled={submitting || !cart.length || !branchId}
              onClick={handleCheckout}
            >
              {submitting ? "Đang tạo đơn..." : "Tạo đơn và thanh toán"}
            </button>
          </aside>
        </div>
      </div>

      <Footer />
    </div>
  );
}
