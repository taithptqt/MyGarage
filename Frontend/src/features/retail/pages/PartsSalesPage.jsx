import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import RetailCartEntry from "@features/retail/components/RetailCartEntry";
import RetailImage from "@features/retail/components/RetailImage";
import {
  getBranches,
  getCategories,
  getRetailCatalog,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  clampQuantityToAvailable,
  formatVnd,
  getBranchLabel,
  getCategoryLabel,
  getProductDescription,
  getProductImageAlt,
  getProductName,
  normalizeAvailableQuantity,
  readStoredRetailBranchId,
  resolveProductImage,
  RETAIL_BRANCH_CHANGED_EVENT,
  RETAIL_SELECTED_BRANCH_STORAGE_KEY,
  sortProductImages,
  toPositiveInt,
  writeStoredRetailBranchId,
} from "@features/retail/utils/retailFormat";
import "./RetailShared.css";
import "./PartsSalesPage.css";

const SHOP_HIGHLIGHTS = [
  { value: "Chính hãng", label: "Ưu tiên danh mục phụ tùng phù hợp với workflow tồn kho" },
  { value: "1 lần", label: "Tạo đơn và chuyển sang thanh toán ngay trên cùng flow khách hàng" },
  { value: "Minh bạch", label: "Xem rõ SKU, chi nhánh nhận hàng và ghi chú đơn bán lẻ" },
];
const PRODUCTS_PER_PAGE = 12;

/** Trang hiển thị dạng gọn: 1 … 18 19 [20] 21 22 … 42 */
function buildCatalogPageItems(currentPage, totalPages, delta = 2) {
  if (totalPages <= 0) return [];
  const windowStart = Math.max(2, currentPage - delta);
  const windowEnd = Math.min(totalPages - 1, currentPage + delta);
  const pages = new Set([1, totalPages]);
  for (let p = windowStart; p <= windowEnd; p += 1) {
    if (p >= 1 && p <= totalPages) pages.add(p);
  }
  const sorted = [...pages].sort((a, b) => a - b);
  const out = [];
  let prev = 0;
  for (const p of sorted) {
    if (prev && p - prev > 1) {
      out.push("ellipsis");
    }
    out.push(p);
    prev = p;
  }
  return out;
}

function CatalogPagination({ currentPage, totalPages, onPageChange }) {
  if (totalPages <= 1) {
    return null;
  }

  const pageItems = buildCatalogPageItems(currentPage, totalPages, 2);

  return (
    <nav className="parts-shop-pagination" aria-label="Phân trang phụ tùng">
      <button
        type="button"
        className="parts-shop-page-button parts-shop-pagination__edge"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage <= 1}
      >
        Trước
      </button>

      <div className="parts-shop-page-numbers">
        {pageItems.map((item, index) =>
          item === "ellipsis" ? (
            <span
              key={`e-${index}`}
              className="parts-shop-page-ellipsis"
              aria-hidden="true"
            >
              …
            </span>
          ) : (
            <button
              type="button"
              key={item}
              className={`parts-shop-page-button ${item === currentPage ? "active" : ""}`}
              aria-current={item === currentPage ? "page" : undefined}
              onClick={() => onPageChange(item)}
            >
              {item}
            </button>
          )
        )}
      </div>

      <button
        type="button"
        className="parts-shop-page-button parts-shop-pagination__edge"
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
  const [branches, setBranches] = useState([]);
  const [branchesLoaded, setBranchesLoaded] = useState(false);
  const [stockByProductId, setStockByProductId] = useState({});
  const [selectedBranchId, setSelectedBranchId] = useState(() => readStoredRetailBranchId());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [referenceError, setReferenceError] = useState("");
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [catFilter, setCatFilter] = useState("all");
  const [sortBy, setSortBy] = useState("name-asc");
  const [currentPage, setCurrentPage] = useState(1);
  const [totalProducts, setTotalProducts] = useState(0);
  const [activeProductCount, setActiveProductCount] = useState(0);
  const [draftQty, setDraftQty] = useState({});
  const [bannerMessage, setBannerMessage] = useState("");
  const {
    totalLineItems,
    addToCart,
  } = useRetailCart();

  useEffect(() => {
    function syncBranchStockFromStorage() {
      setSelectedBranchId(readStoredRetailBranchId());
    }

    window.addEventListener(RETAIL_BRANCH_CHANGED_EVENT, syncBranchStockFromStorage);

    function handleStorage(event) {
      if (event.key === RETAIL_SELECTED_BRANCH_STORAGE_KEY) {
        syncBranchStockFromStorage();
      }
    }

    window.addEventListener("storage", handleStorage);

    return () => {
      window.removeEventListener(RETAIL_BRANCH_CHANGED_EVENT, syncBranchStockFromStorage);
      window.removeEventListener("storage", handleStorage);
    };
  }, []);

  useEffect(() => {
    let ignore = false;
    Promise.all([getCategories(), getBranches()]).then(([categoryData, branchData]) => {
      if (ignore) return;
      setCategories(Array.isArray(categoryData) ? categoryData.filter((item) => item?.isDeleted !== true) : []);
      setBranches(Array.isArray(branchData) ? branchData.filter((item) => item?.isDeleted !== true && item?.isActive !== false) : []);
      setBranchesLoaded(true);
    }).catch((loadError) => {
      if (!ignore) setReferenceError(loadError?.message || "Không thể tải danh sách phụ tùng lúc này.");
    });
    return () => { ignore = true; };
  }, []);

  useEffect(() => {
    if (!branchesLoaded || !selectedBranchId) return;
    if (branches.some((item) => String(item?.id) === String(selectedBranchId))) return;
    setSelectedBranchId("");
    writeStoredRetailBranchId("");
  }, [branches, branchesLoaded, selectedBranchId]);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let ignore = false;
    async function loadCatalog() {
      setLoading(true);
      setError("");
      try {
        const result = await getRetailCatalog({
          page: currentPage, pageSize: PRODUCTS_PER_PAGE,
          search: debouncedSearch || undefined,
          categoryId: catFilter === "all" ? undefined : catFilter,
          branchId: selectedBranchId || undefined,
          sort: sortBy,
        });
        if (ignore) return;
        setProducts(Array.isArray(result?.items) ? result.items : []);
        setProductImages(Array.isArray(result?.images) ? result.images : []);
        setStockByProductId(result?.availability || {});
        setTotalProducts(Number(result?.total) || 0);
        setActiveProductCount(Number(result?.activeCount) || 0);
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải danh sách phụ tùng lúc này.");
          setProducts([]);
          setProductImages([]);
          setStockByProductId({});
          setTotalProducts(0);
        }
      } finally {
        if (!ignore) setLoading(false);
      }
    }
    loadCatalog();
    return () => { ignore = true; };
  }, [currentPage, debouncedSearch, catFilter, sortBy, selectedBranchId]);

  useEffect(() => {
    setDraftQty((previous) => {
      const nextState = { ...previous };
      let changed = false;

      Object.entries(nextState).forEach(([productId, rawQty]) => {
        if (!Object.prototype.hasOwnProperty.call(stockByProductId, productId)) return;
        const availableQuantity = normalizeAvailableQuantity(stockByProductId[productId]);
        if (availableQuantity <= 0) {
          if (Number(rawQty) !== 0) {
            nextState[productId] = 0;
            changed = true;
          }
          return;
        }

        const clampedQty = clampQuantityToAvailable(rawQty, availableQuantity, 1);
        if (Number(clampedQty) !== Number(rawQty)) {
          nextState[productId] = clampedQty;
          changed = true;
        }
      });

      return changed ? nextState : previous;
    });
  }, [stockByProductId]);

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

  const sortedBranches = useMemo(
    () =>
      [...branches].sort((left, right) =>
        getBranchLabel(left).localeCompare(getBranchLabel(right), "vi")
      ),
    [branches]
  );

  const totalPages = useMemo(
    () => Math.ceil(totalProducts / PRODUCTS_PER_PAGE),
    [totalProducts]
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

  const paginationSummary = useMemo(() => {
    if (!totalProducts) {
      return "";
    }

    const start = (safeCurrentPage - 1) * PRODUCTS_PER_PAGE + 1;
    const end = Math.min(totalProducts, safeCurrentPage * PRODUCTS_PER_PAGE);
    return `Hiển thị ${start}-${end} trên ${totalProducts} phụ tùng`;
  }, [totalProducts, safeCurrentPage]);

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
      { value: `${activeProductCount}+`, label: "mã phụ tùng đang mở bán" },
      { value: `${categories.length}`, label: "nhóm danh mục đang hiển thị" },
      { value: `${totalLineItems}`, label: "mặt hàng đang nằm trong giỏ" },
    ],
    [categories.length, activeProductCount, totalLineItems]
  );

  function getAvailableQuantity(productId) {
    return normalizeAvailableQuantity(stockByProductId[String(productId)]);
  }

  function updateDraftQuantity(productId, nextValue) {
    const availableQuantity = getAvailableQuantity(productId);

    setDraftQty((previous) => ({
      ...previous,
      [productId]:
        availableQuantity > 0
          ? clampQuantityToAvailable(nextValue, availableQuantity, 1)
          : 0,
    }));
  }

  function handleAddProductToCart(product, quantity, availableQuantity) {
    const productName = getProductName(product);
    const mutation = addToCart(product, quantity, { maxQty: availableQuantity });

    if (!mutation?.ok) {
      if (availableQuantity <= 0) {
        setBannerMessage(`${productName} hiện đã hết hàng trong tồn kho khả dụng.`);
        return;
      }

      setBannerMessage(
        `${productName} chỉ còn tối đa ${availableQuantity} sản phẩm khả dụng trong tồn kho hiện tại.`
      );
      return;
    }

    if (mutation.adjusted) {
      setBannerMessage(
        `Đã thêm tối đa ${mutation.addedQuantity} ${productName} theo số lượng tồn kho khả dụng hiện tại.`
      );
      return;
    }

    setBannerMessage(
      `Đã thêm ${mutation.addedQuantity} ${productName} vào giỏ. Mở cart page để chọn sản phẩm cần checkout.`
    );
  }

  function handleBranchChange(nextBranchId) {
    const normalized = String(nextBranchId || "");
    setSelectedBranchId(normalized);
    writeStoredRetailBranchId(normalized);
    if (currentPage !== 1) {
      setCurrentPage(1);
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
            Chọn phụ tùng, gom vào giỏ, rà soát sản phẩm cần thanh toán rồi checkout ở cart page theo đúng flow retail hiện có của MyGarage.
          </p>
          <div className="parts-shop-hero-actions">
            <RetailCartEntry itemCount={totalLineItems} label="Giỏ hàng" />
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

        {(error || referenceError) ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error || referenceError}</div>
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
              <div className="parts-shop-branch-control">
                <label htmlFor="parts-shop-branch-select">Chi nhánh</label>
                <select
                  id="parts-shop-branch-select"
                  value={selectedBranchId}
                  onChange={(event) => handleBranchChange(event.target.value)}
                  aria-label="Chọn chi nhánh để ưu tiên tồn kho"
                >
                  <option value="">Tất cả chi nhánh</option>
                  {sortedBranches.map((branch) => (
                    <option key={branch.id} value={String(branch.id)}>
                      {getBranchLabel(branch)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="parts-shop-results-meta">
                <strong>{totalProducts}</strong>
                <span>sản phẩm đang hiển thị</span>
                {paginationSummary ? (
                  <small className="parts-shop-results-range">{paginationSummary}</small>
                ) : null}
              </div>
              <div className="parts-shop-panel-action">
                <RetailCartEntry
                  itemCount={totalLineItems}
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
              <select
                className="parts-shop-filter-select"
                value={catFilter}
                onChange={(event) => setCatFilter(event.target.value)}
                aria-label="Lọc theo danh mục phụ tùng"
              >
                <option value="all">Tất cả danh mục</option>
                {categories.map((category) => (
                  <option key={category.id} value={String(category.id)}>
                    {getCategoryLabel(category)}
                  </option>
                ))}
              </select>
              <select
                className="parts-shop-filter-select"
                value={sortBy}
                onChange={(event) => setSortBy(event.target.value)}
                aria-label="Sắp xếp danh sách phụ tùng"
              >
                <option value="name-asc">Sắp xếp: Tên A-Z</option>
                <option value="name-desc">Sắp xếp: Tên Z-A</option>
                <option value="price-asc">Giá: Thấp đến cao</option>
                <option value="price-desc">Giá: Cao đến thấp</option>
              </select>
            </div>
          </div>

          {loading ? (
            <div className="parts-shop-empty">Đang tải danh mục phụ tùng...</div>
          ) : totalProducts === 0 ? (
            <div className="parts-shop-empty">
              Không có phụ tùng nào khớp bộ lọc hiện tại. Hãy thử đổi từ khóa hoặc danh mục.
            </div>
          ) : (
            <>
              <div className="parts-shop-list">
                {products.map((product) => {
                  const category = categoriesById.get(String(product?.categoryId));
                  const relatedImages = productImagesByProductId.get(String(product?.id)) || [];
                  const availableQuantity = getAvailableQuantity(product.id);
                  const isOutOfStock = availableQuantity <= 0;
                  const savedDraft = draftQty[product.id];
                  const qty = isOutOfStock
                    ? 0
                    : clampQuantityToAvailable(
                        savedDraft != null ? Number(savedDraft) : 1,
                        availableQuantity,
                        1
                      );
                  const isAtStockLimit = qty >= availableQuantity && !isOutOfStock;
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

                          <div className={`parts-shop-stock-hint ${isOutOfStock ? "is-out" : ""}`}>
                            {isOutOfStock
                              ? "Tạm hết hàng trong tồn kho khả dụng"
                              : `Còn ${availableQuantity} sản phẩm khả dụng`}
                          </div>

                          <div className="parts-shop-price">{formatVnd(product.retailPrice)}</div>
                        </div>
                      </Link>

                      <div className="parts-shop-actions">
                        <div className="parts-shop-qty">
                          <button
                            type="button"
                            className="parts-shop-btn parts-shop-btn-ghost"
                            onClick={() =>
                              updateDraftQuantity(product.id, Math.max(1, Number(qty) - 1))
                            }
                            disabled={isOutOfStock}
                          >
                            -
                          </button>
                          <input
                            type="number"
                            min={isOutOfStock ? 0 : 1}
                            max={isOutOfStock ? 0 : Math.max(availableQuantity, 1)}
                            value={qty}
                            onChange={(event) =>
                              updateDraftQuantity(product.id, toPositiveInt(event.target.value, 1))
                            }
                            disabled={isOutOfStock}
                          />
                          <button
                            type="button"
                            className="parts-shop-btn parts-shop-btn-ghost"
                            onClick={() => updateDraftQuantity(product.id, Number(qty) + 1)}
                            disabled={isOutOfStock || isAtStockLimit}
                          >
                            +
                          </button>
                        </div>

                        <button
                          type="button"
                          className="parts-shop-btn parts-shop-btn-primary parts-shop-action-button"
                          onClick={() => handleAddProductToCart(product, qty, availableQuantity)}
                          disabled={isOutOfStock}
                        >
                          {isOutOfStock ? "Hết hàng" : "Thêm vào giỏ"}
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
