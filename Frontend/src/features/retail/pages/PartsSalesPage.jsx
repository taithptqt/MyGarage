import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import RetailImage from "@features/retail/components/RetailImage";
import {
  getBranches,
  getCategories,
  getRetailCatalog,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  formatVnd,
  getBranchLabel,
  getCategoryLabel,
  getProductImageAlt,
  getProductName,
  normalizeAvailableQuantity,
  readStoredRetailBranchId,
  resolveProductImage,
  RETAIL_BRANCH_CHANGED_EVENT,
  RETAIL_SELECTED_BRANCH_STORAGE_KEY,
  sortProductImages,
  writeStoredRetailBranchId,
} from "@features/retail/utils/retailFormat";
import {
  Search,
  ShoppingCart,
  ShoppingBag,
  CheckCircle2,
  AlertCircle,
  X,
  RotateCcw,
  ArrowDown,
  ArrowRight,
  Layers,
  Trash2,
  Package,
} from "lucide-react";
import "./RetailShared.css";
import "./PartsSalesPage.css";

const PRODUCTS_PER_PAGE = 12;
const ADDED_FEEDBACK_MS = 1000;

/** Phân trang dạng gọn: 1 … 18 19 [20] 21 22 … 42 */
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
  const addedFeedbackTimersRef = useRef({});
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
  const [toast, setToast] = useState(null);
  const [addedProductIds, setAddedProductIds] = useState({});
  const [cartDrawerOpen, setCartDrawerOpen] = useState(false);
  const {
    cart,
    subtotal,
    totalItems,
    totalLineItems,
    addToCart,
    removeFromCart,
    updateCartQty,
  } = useRetailCart();

  // Đóng Drawer giỏ hàng bằng phím Escape & khóa cuộn trang nền khi mở Drawer
  useEffect(() => {
    if (!cartDrawerOpen) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") setCartDrawerOpen(false);
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [cartDrawerOpen]);

  // Tự động tắt Toast thông báo sau 3.5 giây
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3500);
    return () => window.clearTimeout(timer);
  }, [toast]);

  useEffect(() => () => {
    Object.values(addedFeedbackTimersRef.current).forEach((timer) => window.clearTimeout(timer));
  }, []);

  // Vị trí và kéo thả nút giỏ hàng nổi (Draggable Floating 3D Bubble)
  const [cartBtnPos, setCartBtnPos] = useState({ x: null, y: null });
  const [isDraggingCart, setIsDraggingCart] = useState(false);
  const dragInfoRef = useRef({
    startX: 0,
    startY: 0,
    elemX: 0,
    elemY: 0,
    hasMoved: false,
  });

  const handleCartMouseDown = (e) => {
    if (e.button !== 0) return;
    const rect = e.currentTarget.getBoundingClientRect();
    dragInfoRef.current = {
      startX: e.clientX,
      startY: e.clientY,
      elemX: rect.left,
      elemY: rect.top,
      hasMoved: false,
    };

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - dragInfoRef.current.startX;
      const deltaY = moveEvent.clientY - dragInfoRef.current.startY;
      if (Math.abs(deltaX) > 4 || Math.abs(deltaY) > 4) {
        dragInfoRef.current.hasMoved = true;
        setIsDraggingCart(true);
      }
      if (dragInfoRef.current.hasMoved) {
        const nextX = dragInfoRef.current.elemX + deltaX;
        const nextY = dragInfoRef.current.elemY + deltaY;
        const btnSize = 64;
        const clampedX = Math.max(12, Math.min(window.innerWidth - btnSize - 12, nextX));
        const clampedY = Math.max(12, Math.min(window.innerHeight - btnSize - 12, nextY));
        setCartBtnPos({ x: clampedX, y: clampedY });
      }
    };

    const handleMouseUp = () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
      setTimeout(() => {
        setIsDraggingCart(false);
        dragInfoRef.current.hasMoved = false;
      }, 120);
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
  };

  const handleCartTouchStart = (e) => {
    if (e.touches.length !== 1) return;
    const touch = e.touches[0];
    const rect = e.currentTarget.getBoundingClientRect();
    dragInfoRef.current = {
      startX: touch.clientX,
      startY: touch.clientY,
      elemX: rect.left,
      elemY: rect.top,
      hasMoved: false,
    };

    const handleTouchMove = (moveEvent) => {
      if (moveEvent.touches.length !== 1) return;
      const moveTouch = moveEvent.touches[0];
      const deltaX = moveTouch.clientX - dragInfoRef.current.startX;
      const deltaY = moveTouch.clientY - dragInfoRef.current.startY;
      if (Math.abs(deltaX) > 4 || Math.abs(deltaY) > 4) {
        dragInfoRef.current.hasMoved = true;
        setIsDraggingCart(true);
      }
      if (dragInfoRef.current.hasMoved) {
        const nextX = dragInfoRef.current.elemX + deltaX;
        const nextY = dragInfoRef.current.elemY + deltaY;
        const btnSize = 64;
        const clampedX = Math.max(12, Math.min(window.innerWidth - btnSize - 12, nextX));
        const clampedY = Math.max(12, Math.min(window.innerHeight - btnSize - 12, nextY));
        setCartBtnPos({ x: clampedX, y: clampedY });
      }
    };

    const handleTouchEnd = () => {
      window.removeEventListener("touchmove", handleTouchMove);
      window.removeEventListener("touchend", handleTouchEnd);
      setTimeout(() => {
        setIsDraggingCart(false);
        dragInfoRef.current.hasMoved = false;
      }, 120);
    };

    window.addEventListener("touchmove", handleTouchMove, { passive: true });
    window.addEventListener("touchend", handleTouchEnd);
  };

  const handleCartClick = (e) => {
    if (dragInfoRef.current.hasMoved) {
      if (e) {
        e.preventDefault();
        e.stopPropagation();
      }
      dragInfoRef.current.hasMoved = false;
      return;
    }
    setCartDrawerOpen(true);
  };

  // Đồng bộ chi nhánh lưu trong localStorage
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

  // Tải danh mục và chi nhánh
  useEffect(() => {
    let ignore = false;
    Promise.all([getCategories(), getBranches()])
      .then(([categoryData, branchData]) => {
        if (ignore) return;
        setCategories(
          Array.isArray(categoryData)
            ? categoryData.filter((item) => item?.isDeleted !== true)
            : []
        );
        setBranches(
          Array.isArray(branchData)
            ? branchData.filter(
                (item) => item?.isDeleted !== true && item?.isActive !== false
              )
            : []
        );
        setBranchesLoaded(true);
      })
      .catch((loadError) => {
        if (!ignore) {
          setReferenceError(
            loadError?.message || "Không thể tải dữ liệu danh mục phụ tùng lúc này."
          );
        }
      });
    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!branchesLoaded || !selectedBranchId) return;
    if (branches.some((item) => String(item?.id) === String(selectedBranchId))) return;
    setSelectedBranchId("");
    writeStoredRetailBranchId("");
  }, [branches, branchesLoaded, selectedBranchId]);

  // Debounce tìm kiếm
  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  // Tải sản phẩm theo bộ lọc
  useEffect(() => {
    let ignore = false;
    async function loadCatalog() {
      setLoading(true);
      setError("");
      try {
        const result = await getRetailCatalog({
          page: currentPage,
          pageSize: PRODUCTS_PER_PAGE,
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
      } catch (loadError) {
        if (!ignore) {
          setError(
            loadError?.message || "Không thể tải danh sách phụ tùng lúc này."
          );
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
    return () => {
      ignore = true;
    };
  }, [currentPage, debouncedSearch, catFilter, sortBy, selectedBranchId]);

  // Reset trang về 1 khi thay đổi điều kiện tìm kiếm hoặc lọc
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
    if (top >= -40 && top <= 80) return;
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
      if (currentPage !== 1) setCurrentPage(1);
      return;
    }
    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  useEffect(() => {
    if (pendingScrollPageRef.current == null) return undefined;
    if (Number(currentPage) !== Number(pendingScrollPageRef.current)) return undefined;

    pendingScrollPageRef.current = null;
    const frameId = window.requestAnimationFrame(() => {
      scrollCatalogIntoView();
    });
    return () => window.cancelAnimationFrame(frameId);
  }, [currentPage]);

  const paginationSummary = useMemo(() => {
    if (!totalProducts) return "";
    const start = (safeCurrentPage - 1) * PRODUCTS_PER_PAGE + 1;
    const end = Math.min(totalProducts, safeCurrentPage * PRODUCTS_PER_PAGE);
    return `${start}-${end} trên ${totalProducts}`;
  }, [totalProducts, safeCurrentPage]);

  function handlePageChange(nextPage) {
    const normalizedPage = Math.min(
      Math.max(1, Number(nextPage) || 1),
      Math.max(totalPages, 1)
    );
    if (normalizedPage === safeCurrentPage) return;
    pendingScrollPageRef.current = normalizedPage;
    setCurrentPage(normalizedPage);
  }

  function getAvailableQuantity(productId) {
    return normalizeAvailableQuantity(stockByProductId[String(productId)]);
  }

  function handleAddProductToCart(product, quantity = 1, availableQuantity) {
    const productName = getProductName(product);
    const mutation = addToCart(product, quantity, { maxQty: availableQuantity });

    if (!mutation?.ok) {
      if (availableQuantity <= 0) {
        setToast({
          type: "error",
          message: `${productName} hiện đã tạm hết hàng.`,
        });
        return;
      }

      setToast({
        type: "error",
        message: `${productName} chỉ còn tối đa ${availableQuantity} sản phẩm tại kho.`,
      });
      return;
    }

    const productId = String(product.id);
    window.clearTimeout(addedFeedbackTimersRef.current[productId]);
    setAddedProductIds((current) => ({ ...current, [productId]: true }));
    addedFeedbackTimersRef.current[productId] = window.setTimeout(() => {
      setAddedProductIds((current) => {
        const next = { ...current };
        delete next[productId];
        return next;
      });
      delete addedFeedbackTimersRef.current[productId];
    }, ADDED_FEEDBACK_MS);

    if (mutation.adjusted) {
      setToast({
        type: "info",
        message: `Đã thêm tối đa ${mutation.addedQuantity} ${productName} theo tồn kho có sẵn.`,
      });
      return;
    }

    setToast(null);
  }

  function handleBranchChange(nextBranchId) {
    const normalized = String(nextBranchId || "");
    setSelectedBranchId(normalized);
    writeStoredRetailBranchId(normalized);
    if (currentPage !== 1) {
      setCurrentPage(1);
    }
  }

  function handleResetFilters() {
    setSearch("");
    setCatFilter("all");
    handleBranchChange("");
    setSortBy("name-asc");
  }

  const hasActiveFilters = Boolean(
    search.trim() || catFilter !== "all" || selectedBranchId
  );

  return (
    <div className="parts-shop-page">
      {/* Hero Section - Thiết kế thoáng đãng, sang trọng */}
      <section className="parts-shop-hero">
        <img
          className="parts-shop-hero-image"
          src="https://images.unsplash.com/photo-1778757949657-615fe09f063b?auto=format&fit=crop&w=2400&q=90"
          alt="Linh kiện và phụ tùng ô tô chính hãng của MyGarage"
          loading="eager"
        />
        <div className="parts-shop-hero-overlay" />
        <div className="parts-shop-hero-fade" />
        <Header overlay />
        <div className="parts-shop-hero-content">
          <div className="parts-shop-hero-text">
            <p className="parts-shop-hero-kicker mg-scene-fade-up mg-scene-delay-1">
              MYGARAGE GENUINE PARTS & ACCESSORIES
            </p>
            <h1 className="mg-scene-fade-up mg-scene-delay-2">
              Phụ Tùng & Linh Kiện Chính Hãng
            </h1>
            <p className="parts-shop-hero-sub mg-scene-fade-up mg-scene-delay-3">
              Hệ thống phân phối linh kiện, phụ tùng và phụ kiện ô tô tiêu chuẩn cao cấp. Được kiểm định nghiêm ngặt về chất lượng, cam kết nguồn gốc xuất xứ rõ ràng và tương thích tối ưu cho từng dòng xe.
            </p>
            <div className="parts-shop-hero-actions mg-scene-fade-up mg-scene-delay-4">
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-primary"
                onClick={(e) => {
                  e.preventDefault();
                  const el = document.getElementById("parts-shop-catalog");
                  if (el) {
                    el.scrollIntoView({ behavior: "smooth", block: "start" });
                  }
                }}
              >
                <ArrowDown size={17} />
                Khám phá danh mục
              </button>
              {String(user?.roleCode || user?.RoleCode || "").toUpperCase() === "CUSTOMER" ? (
                <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-ghost">
                  Đơn hàng của tôi
                </Link>
              ) : null}
            </div>
          </div>

          <div className="parts-shop-hero-side-cards mg-scene-fade-up mg-scene-delay-4">
            <div className="parts-shop-side-card">
              <div className="parts-shop-side-card-meta">
                <span className="parts-shop-side-card-idx">01</span>
                <span className="parts-shop-side-card-tag">AUTHENTICITY</span>
              </div>
              <h3 className="parts-shop-side-card-title">100% Chính Hãng</h3>
              <p className="parts-shop-side-card-desc">
                Linh kiện &amp; phụ tùng chuẩn xuất xứ, tuyển chọn trực tiếp từ các nhà sản xuất uy tín.
              </p>
            </div>
            <div className="parts-shop-side-card">
              <div className="parts-shop-side-card-meta">
                <span className="parts-shop-side-card-idx">02</span>
                <span className="parts-shop-side-card-tag">PRECISION FIT</span>
              </div>
              <h3 className="parts-shop-side-card-title">Tương Thích Chuẩn Xác</h3>
              <p className="parts-shop-side-card-desc">
                Tra cứu đúng thông số theo từng dòng xe, đội ngũ kỹ sư hỗ trợ tư vấn chuyên sâu.
              </p>
            </div>
            <div className="parts-shop-side-card">
              <div className="parts-shop-side-card-meta">
                <span className="parts-shop-side-card-idx">03</span>
                <span className="parts-shop-side-card-tag">GARAGE WARRANTY</span>
              </div>
              <h3 className="parts-shop-side-card-title">Bảo Hành Tiêu Chuẩn</h3>
              <p className="parts-shop-side-card-desc">
                Chính sách đổi trả minh bạch, nhận hàng tại chỗ hoặc hỗ trợ lắp đặt trực tiếp tại gara.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Catalog Container */}
      <div className="parts-shop" id="parts-shop-catalog" ref={catalogPanelRef}>
        <section className="parts-shop-catalog-section">
          {/* Header phần Catalog */}
          <div className="parts-shop-catalog-header">
            <div className="parts-shop-catalog-header-text">
              <span className="parts-shop-catalog-kicker">
                <Layers size={14} />
                BỘ SƯU TẬP PHỤ TÙNG
              </span>
              <h2 className="parts-shop-catalog-title">Danh Mục Linh Kiện Tuyển Chọn</h2>
              <p className="parts-shop-catalog-subtitle">
                Tìm kiếm phụ tùng chất lượng cao phù hợp cho việc bảo dưỡng, nâng cấp và sửa chữa xe của bạn.
              </p>
            </div>
          </div>

          {/* Toolbar tìm kiếm và bộ lọc đồng bộ */}
          <div className="parts-shop-toolbar">
            <div className="parts-shop-search-box">
              <Search size={18} className="parts-shop-search-icon" />
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Tìm theo tên phụ tùng, mã SKU..."
                aria-label="Tìm kiếm phụ tùng"
              />
              {search ? (
                <button
                  type="button"
                  className="parts-shop-search-clear"
                  onClick={() => setSearch("")}
                  aria-label="Xóa từ khóa tìm kiếm"
                >
                  <X size={15} />
                </button>
              ) : null}
            </div>

            <div className="parts-shop-filter-select-wrap">
              <select
                value={catFilter}
                onChange={(event) => setCatFilter(event.target.value)}
                aria-label="Lọc theo danh mục"
                className="parts-shop-select"
              >
                <option value="all">Tất cả danh mục ({categories.length})</option>
                {categories.map((category) => (
                  <option key={category.id} value={String(category.id)}>
                    {getCategoryLabel(category)}
                  </option>
                ))}
              </select>
            </div>

            <div className="parts-shop-filter-select-wrap">
              <select
                value={selectedBranchId}
                onChange={(event) => handleBranchChange(event.target.value)}
                aria-label="Chọn chi nhánh kho"
                className="parts-shop-select"
              >
                <option value="">Tất cả chi nhánh kho</option>
                {sortedBranches.map((branch) => (
                  <option key={branch.id} value={String(branch.id)}>
                    Kho: {getBranchLabel(branch)}
                  </option>
                ))}
              </select>
            </div>

            <div className="parts-shop-filter-select-wrap parts-shop-sort-wrap">
              <select
                value={sortBy}
                onChange={(event) => setSortBy(event.target.value)}
                aria-label="Sắp xếp sản phẩm"
                className="parts-shop-select"
              >
                <option value="name-asc">Sắp xếp: Tên A-Z</option>
                <option value="name-desc">Sắp xếp: Tên Z-A</option>
                <option value="price-asc">Giá: Thấp đến cao</option>
                <option value="price-desc">Giá: Cao đến thấp</option>
              </select>
            </div>
          </div>

          {/* Thanh trạng thái bộ lọc và số lượng kết quả */}
          <div className="parts-shop-meta-bar">
            <div className="parts-shop-meta-count">
              {loading ? (
                <span>Đang tải danh sách phụ tùng...</span>
              ) : (
                <span>
                  Tìm thấy <strong>{totalProducts}</strong> sản phẩm
                  {paginationSummary ? ` (Hiển thị ${paginationSummary})` : ""}
                </span>
              )}
            </div>

            {hasActiveFilters ? (
              <div className="parts-shop-active-filters">
                {search.trim() ? (
                  <span className="parts-shop-filter-chip">
                    "{search.trim()}"
                    <button
                      type="button"
                      onClick={() => setSearch("")}
                      aria-label="Bỏ lọc tìm kiếm"
                    >
                      <X size={12} />
                    </button>
                  </span>
                ) : null}
                {catFilter !== "all" ? (
                  <span className="parts-shop-filter-chip">
                    {getCategoryLabel(categoriesById.get(String(catFilter)))}
                    <button
                      type="button"
                      onClick={() => setCatFilter("all")}
                      aria-label="Bỏ lọc danh mục"
                    >
                      <X size={12} />
                    </button>
                  </span>
                ) : null}
                {selectedBranchId ? (
                  <span className="parts-shop-filter-chip">
                    Kho:{" "}
                    {getBranchLabel(
                      branches.find((b) => String(b.id) === String(selectedBranchId))
                    )}
                    <button
                      type="button"
                      onClick={() => handleBranchChange("")}
                      aria-label="Bỏ lọc chi nhánh"
                    >
                      <X size={12} />
                    </button>
                  </span>
                ) : null}
                <button
                  type="button"
                  className="parts-shop-reset-filters-btn"
                  onClick={handleResetFilters}
                  title="Đặt lại toàn bộ bộ lọc"
                >
                  <RotateCcw size={13} />
                  <span>Đặt lại</span>
                </button>
              </div>
            ) : null}
          </div>

          {/* Lỗi nạp dữ liệu nếu có */}
          {error || referenceError ? (
            <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">
              {error || referenceError}
            </div>
          ) : null}

          {/* Lưới sản phẩm */}
          {loading ? (
            <div className="parts-shop-empty">
              <div className="parts-shop-empty-spinner" />
              <p>Đang tải danh mục phụ tùng tuyển chọn...</p>
            </div>
          ) : totalProducts === 0 ? (
            <div className="parts-shop-empty">
              <p className="parts-shop-empty-title">Không tìm thấy phụ tùng phù hợp</p>
              <p className="parts-shop-empty-sub">
                Bạn hãy thử đổi từ khóa tìm kiếm hoặc chọn danh mục khác.
              </p>
              {hasActiveFilters ? (
                <button
                  type="button"
                  className="parts-shop-btn parts-shop-btn-primary parts-shop-empty-btn"
                  onClick={handleResetFilters}
                >
                  <RotateCcw size={15} />
                  Xóa bộ lọc
                </button>
              ) : null}
            </div>
          ) : (
            <>
              <div className="parts-shop-grid">
                {products.map((product) => {
                  const category = categoriesById.get(String(product?.categoryId));
                  const relatedImages =
                    productImagesByProductId.get(String(product?.id)) || [];
                  const availableQuantity = getAvailableQuantity(product.id);
                  const isOutOfStock = availableQuantity <= 0;
                  const isAdded = Boolean(addedProductIds[String(product.id)]);
                  const productDetailPath = `/parts-shop/products/${product.id}`;
                  const productName = getProductName(product);

                  return (
                    <article className="parts-shop-card-item" key={product.id}>
                      <div className="parts-shop-card-media-wrap">
                        <Link
                          to={productDetailPath}
                          className="parts-shop-card-media-link"
                          aria-label={`Xem chi tiết phụ tùng ${productName}`}
                        >
                          <RetailImage
                            src={resolveProductImage(product, relatedImages)}
                            alt={getProductImageAlt(product, relatedImages[0])}
                            className="parts-shop-card-image"
                            fallbackClassName="parts-shop-card-image-fallback"
                            fallbackLabel="Ảnh phụ tùng"
                          />
                        </Link>

                        {category ? (
                          <span className="parts-shop-card-category-badge">
                            {getCategoryLabel(category)}
                          </span>
                        ) : null}

                        {isOutOfStock ? (
                          <span className="parts-shop-card-status-badge is-out">
                            Tạm hết hàng
                          </span>
                        ) : availableQuantity <= 5 && availableQuantity > 0 ? (
                          <span className="parts-shop-card-status-badge is-low">
                            Chỉ còn {availableQuantity}
                          </span>
                        ) : (
                          <span className="parts-shop-card-status-badge is-available">
                            Sẵn hàng
                          </span>
                        )}
                      </div>

                      <div className="parts-shop-card-content">
                        <div className="parts-shop-card-sku">
                          {product.sku ? `SKU: ${product.sku}` : "Chính hãng"}
                        </div>

                        <h4 className="parts-shop-card-title">
                          <Link to={productDetailPath} title={productName}>
                            {productName}
                          </Link>
                        </h4>

                        <div className="parts-shop-card-footer">
                          <div className="parts-shop-card-price-wrap">
                            <span className="parts-shop-card-price-label">Giá niêm yết</span>
                            <span className="parts-shop-card-price">
                              {formatVnd(product.retailPrice)}
                            </span>
                          </div>

                          <button
                            type="button"
                            className={`parts-shop-card-add-btn ${
                              isOutOfStock ? "is-disabled" : ""
                            } ${isAdded ? "is-added" : ""}`}
                            onClick={() =>
                              handleAddProductToCart(product, 1, availableQuantity)
                            }
                            disabled={isOutOfStock}
                            title={
                              isOutOfStock
                                ? "Sản phẩm tạm hết hàng"
                                : "Thêm vào giỏ hàng"
                            }
                            aria-label={isAdded ? `Đã thêm ${productName} vào giỏ hàng` : `Thêm ${productName} vào giỏ hàng`}
                          >
                            <span className="parts-shop-card-icon-stack" aria-hidden="true">
                              <ShoppingBag size={16} className="parts-shop-card-btn-icon parts-shop-card-icon-cart" />
                              <CheckCircle2 size={16} className="parts-shop-card-btn-icon parts-shop-card-icon-check" />
                            </span>
                            <span aria-live="polite">{isOutOfStock ? "Hết hàng" : isAdded ? "Đã thêm" : "Thêm giỏ"}</span>
                          </button>
                        </div>
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

      {/* Nút Giỏ Hàng Nổi (Bong bóng 3D có thể kéo thả di chuyển trên màn hình) */}
      <button
        type="button"
        className={`parts-shop-cart-floating-3d ${isDraggingCart ? "is-dragging" : ""}`}
        style={
          cartBtnPos.x !== null && cartBtnPos.y !== null
            ? { left: `${cartBtnPos.x}px`, top: `${cartBtnPos.y}px`, right: "auto", bottom: "auto" }
            : undefined
        }
        onMouseDown={handleCartMouseDown}
        onTouchStart={handleCartTouchStart}
        onClick={handleCartClick}
        aria-label={`Giỏ hàng của bạn (${totalItems} sản phẩm)`}
        title="Xem giỏ hàng (kéo thả để di chuyển)"
      >
        <span className="parts-shop-cart-3d-glare" aria-hidden="true" />
        <ShoppingCart size={35} strokeWidth={2.3} className="parts-shop-cart-3d-icon" />
        {totalItems > 0 ? (
          <span className="parts-shop-cart-dot">
            {totalItems > 99 ? "99+" : totalItems}
          </span>
        ) : null}
      </button>

      {/* Toast thông báo nhẹ nhàng */}
      {toast ? (
        <aside
          className={`parts-shop-toast parts-shop-toast--${toast.type || "success"}`}
          role="status"
          aria-live="polite"
        >
          <div className="parts-shop-toast-icon">
            {toast.type === "error" ? (
              <AlertCircle size={20} />
            ) : (
              <CheckCircle2 size={20} />
            )}
          </div>
          <div className="parts-shop-toast-content">
            <p className="parts-shop-toast-msg">{toast.message}</p>
          </div>
          <button
            type="button"
            className="parts-shop-toast-link"
            onClick={() => {
              setToast(null);
              setCartDrawerOpen(true);
            }}
          >
            Xem giỏ hàng
          </button>
          <button
            type="button"
            className="parts-shop-toast-close"
            onClick={() => setToast(null)}
            aria-label="Đóng thông báo"
          >
            <X size={16} />
          </button>
        </aside>
      ) : null}

      {/* Drawer Giỏ Hàng Dạng Popup Trực Tiếp Tại Trang (Hiệu ứng trượt mở/đóng mượt mà) */}
      <div
        className={`parts-shop-drawer-overlay ${cartDrawerOpen ? "is-open" : ""}`}
        onClick={() => setCartDrawerOpen(false)}
        role="presentation"
        aria-hidden={!cartDrawerOpen}
      >
        <div
          className="parts-shop-drawer-panel"
          onClick={(e) => e.stopPropagation()}
          role="dialog"
          aria-modal="true"
          aria-label="Giỏ hàng phụ tùng"
        >
            <header className="parts-shop-drawer-header">
              <div className="parts-shop-drawer-title-group">
                <div className="parts-shop-drawer-icon-wrap">
                  <ShoppingCart size={20} />
                </div>
                <div>
                  <h2 className="parts-shop-drawer-title">Giỏ Hàng Phụ Tùng</h2>
                  <span className="parts-shop-drawer-count">{totalItems} món ({totalLineItems} mặt hàng)</span>
                </div>
              </div>
              <button
                type="button"
                className="parts-shop-drawer-close-btn"
                onClick={() => setCartDrawerOpen(false)}
                aria-label="Đóng giỏ hàng"
              >
                <X size={20} />
              </button>
            </header>

            <div className="parts-shop-drawer-body">
              {cart.length === 0 ? (
                <div className="parts-shop-drawer-empty">
                  <div className="parts-shop-drawer-empty-icon">
                    <ShoppingBag size={48} strokeWidth={1.5} />
                  </div>
                  <h3>Giỏ hàng đang trống</h3>
                  <p>Khám phá danh mục phụ tùng chính hãng và thêm vào giỏ hàng ngay hôm nay.</p>
                  <button
                    type="button"
                    className="parts-shop-btn parts-shop-btn-primary"
                    onClick={() => setCartDrawerOpen(false)}
                  >
                    Tiếp tục xem phụ tùng
                  </button>
                </div>
              ) : (
                <ul className="parts-shop-drawer-list">
                  {cart.map((item) => {
                    const itemSubtotal = (item.retailPrice || 0) * (item.qty || 1);
                    return (
                      <li key={item.id} className="parts-shop-drawer-item">
                        <div className="parts-shop-drawer-item-thumb">
                          {item.imageUrl ? (
                            <img src={item.imageUrl} alt={item.name} />
                          ) : (
                            <Package size={24} className="parts-shop-drawer-item-placeholder" />
                          )}
                        </div>
                        <div className="parts-shop-drawer-item-info">
                          <h4 className="parts-shop-drawer-item-name">{item.name}</h4>
                          <div className="parts-shop-drawer-item-meta">
                            {item.sku ? <span className="parts-shop-drawer-sku">Mã: {item.sku}</span> : null}
                            <span className="parts-shop-drawer-price">{formatVnd(item.retailPrice)}</span>
                          </div>
                          <div className="parts-shop-drawer-item-bottom">
                            <div className="parts-shop-qty-ctrl">
                              <button
                                type="button"
                                className="parts-shop-qty-btn"
                                disabled={item.qty <= 1}
                                onClick={() => updateCartQty(item.id, -1)}
                                aria-label="Giảm số lượng"
                              >
                                -
                              </button>
                              <span className="parts-shop-qty-num">{item.qty}</span>
                              <button
                                type="button"
                                className="parts-shop-qty-btn"
                                onClick={() => updateCartQty(item.id, 1)}
                                aria-label="Tăng số lượng"
                              >
                                +
                              </button>
                            </div>
                            <span className="parts-shop-drawer-line-total">{formatVnd(itemSubtotal)}</span>
                            <button
                              type="button"
                              className="parts-shop-drawer-remove-btn"
                              onClick={() => removeFromCart(item.id)}
                              aria-label="Xóa sản phẩm khỏi giỏ"
                              title="Xóa"
                            >
                              <Trash2 size={16} />
                            </button>
                          </div>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>

            {cart.length > 0 ? (
              <footer className="parts-shop-drawer-footer">
                <div className="parts-shop-drawer-summary-row">
                  <span>Tạm tính ({totalItems} món):</span>
                  <strong className="parts-shop-drawer-total-amount">{formatVnd(subtotal)}</strong>
                </div>
                <p className="parts-shop-drawer-vat-hint">
                  Giá đã bao gồm VAT. Hỗ trợ giao tận nơi hoặc nhận trực tiếp tại hệ thống gara.
                </p>
                <div className="parts-shop-drawer-actions">
                  <button
                    type="button"
                    className="parts-shop-btn parts-shop-btn-ghost parts-shop-drawer-btn-secondary"
                    onClick={() => setCartDrawerOpen(false)}
                  >
                    Tiếp tục mua hàng
                  </button>
                  <Link
                    to="/parts-cart"
                    className="parts-shop-btn parts-shop-btn-primary parts-shop-drawer-btn-primary"
                    onClick={() => setCartDrawerOpen(false)}
                  >
                    Tiến hành đặt hàng
                    <ArrowRight size={16} />
                  </Link>
                </div>
              </footer>
            ) : null}
          </div>
        </div>

      <Footer />
    </div>
  );
}
