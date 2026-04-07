import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import RetailCartEntry from "@features/retail/components/RetailCartEntry";
import RetailImage from "@features/retail/components/RetailImage";
import {
  getCategories,
  getProductImages,
  getProducts,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  formatVnd,
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
const PRODUCTS_PER_PAGE = 12;

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

export default function PartsSalesPage() {
  const { user } = useAuth();
  const catalogPanelRef = useRef(null);
  const pendingScrollPageRef = useRef(null);
  const [products, setProducts] = useState([]);
  const [productImages, setProductImages] = useState([]);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [catFilter, setCatFilter] = useState("all");
  const [sortBy, setSortBy] = useState("name-asc");
  const [currentPage, setCurrentPage] = useState(1);
  const [draftQty, setDraftQty] = useState({});
  const [bannerMessage, setBannerMessage] = useState("");
  const {
    totalItems,
    addToCart,
  } = useRetailCart();

  useEffect(() => {
    let ignore = false;

    async function loadData() {
      setLoading(true);
      setError("");

      try {
        const [productData, productImageData, categoryData] = await Promise.all([
          getProducts(),
          getProductImages(),
          getCategories(),
        ]);

        if (ignore) return;

        const nextProducts = Array.isArray(productData)
          ? productData.filter((item) => item?.isActive !== false)
          : [];
        const nextProductImages = Array.isArray(productImageData) ? productImageData : [];
        const nextCategories = Array.isArray(categoryData)
          ? categoryData.filter((item) => item?.isDeleted !== true)
          : [];

        setProducts(nextProducts);
        setProductImages(nextProductImages);
        setCategories(nextCategories);
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải danh sách phụ tùng lúc này.");
          setProducts([]);
          setProductImages([]);
          setCategories([]);
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

  const summaryStats = useMemo(
    () => [
      { value: `${products.length}+`, label: "mã phụ tùng đang mở bán" },
      { value: `${categories.length}`, label: "nhóm danh mục đang hiển thị" },
      { value: `${totalItems}`, label: "sản phẩm đang nằm trong giỏ" },
    ],
    [categories.length, products.length, totalItems]
  );

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
            Chọn phụ tùng, gom vào giỏ, rà soát sản phẩm cần thanh toán rồi checkout ở cart page theo đúng flow retail hiện có của MyGarage.
          </p>
          <div className="parts-shop-hero-actions">
            <RetailCartEntry totalItems={totalItems} label="Giỏ hàng" />
            <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-ghost">
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
            Xin chào {user?.fullName || "bạn"}, hãy thêm phụ tùng từ catalog rồi mở giỏ hàng để
            chọn sản phẩm cần checkout, chốt chi nhánh và ghi chú trước khi chuyển sang PayOS.
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
            <div className="parts-shop-banner-copy">
              <span>{bannerMessage}</span>
              <Link to="/parts-cart" className="parts-shop-btn parts-shop-btn-inline parts-shop-btn-primary">
                Xem giỏ hàng
              </Link>
            </div>
          </div>
        ) : null}

        <section
          ref={catalogPanelRef}
          className="parts-shop-card parts-shop-product-panel"
          id="parts-shop-catalog"
        >
          <div className="parts-shop-panel-head">
            <div>
              <span className="parts-shop-section-kicker">Catalog tuyển chọn</span>
              <h3>Phụ tùng nổi bật cho khách hàng retail</h3>
              <p>
                Catalog này chỉ để tìm kiếm, lọc và thêm phụ tùng vào giỏ. Toàn bộ quản lý giỏ hàng và checkout hiện nằm ở cart page.
              </p>
            </div>
            <div className="parts-shop-panel-side">
              <div className="parts-shop-results-meta">
                <strong>{filteredProducts.length}</strong>
                <span>sản phẩm đang hiển thị</span>
                {paginationSummary ? (
                  <small className="parts-shop-results-range">{paginationSummary}</small>
                ) : null}
              </div>
              <div className="parts-shop-panel-action">
                <RetailCartEntry
                  totalItems={totalItems}
                  label="Xem giỏ hàng"
                  variant="ghost"
                  className="retail-cart-entry--panel"
                />
              </div>
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
                          onClick={() => {
                            addToCart(product, qty);
                            setBannerMessage(
                              `Đã thêm ${qty} ${productName} vào giỏ. Mở cart page để chọn sản phẩm cần checkout.`
                            );
                          }}
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
      </div>

      <Footer />
    </div>
  );
}
