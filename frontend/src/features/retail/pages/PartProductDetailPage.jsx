import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import RetailCartEntry from "@features/retail/components/RetailCartEntry";
import RetailImage from "@features/retail/components/RetailImage";
import {
  getCategories,
  getProductById,
  getProductImages,
  getProductRetailStockByBranches,
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
  getProductUnitLabel,
  normalizeAvailableQuantity,
  readStoredRetailBranchId,
  resolveProductImage,
  RETAIL_BRANCH_CHANGED_EVENT,
  sortProductImages,
  toPositiveInt,
  writeStoredRetailBranchId,
} from "@features/retail/utils/retailFormat";
import "./RetailShared.css";
import "./PartProductDetailPage.css";

function formatRate(value) {
  const rate = Number(value);
  if (!Number.isFinite(rate)) return "Chưa cập nhật";
  return `${rate.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%`;
}

function formatStatus(value) {
  if (value == null) return "Chưa cập nhật";
  return value ? "Đang bán" : "Tạm ẩn";
}

function formatFlag(value, trueLabel = "Có", falseLabel = "Không") {
  if (value == null) return "Chưa cập nhật";
  return value ? trueLabel : falseLabel;
}

export default function PartProductDetailPage() {
  const { productId } = useParams();
  const [product, setProduct] = useState(null);
  const [categories, setCategories] = useState([]);
  const [productImages, setProductImages] = useState([]);
  const [stockBreakdown, setStockBreakdown] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState("");
  const [stockContextError, setStockContextError] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [bannerMessage, setBannerMessage] = useState("");
  const [draftQty, setDraftQty] = useState(1);
  const [selectedImageIndex, setSelectedImageIndex] = useState(0);
  const { addToCart, totalLineItems } = useRetailCart();

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [productId]);

  useEffect(() => {
    let ignore = false;

    async function loadProductDetail() {
      setLoading(true);
      setError("");
      setBannerMessage("");
      setStockContextError("");
      setStockBreakdown([]);

      try {
        const [productData, categoryData, imageData] = await Promise.all([
          getProductById(productId),
          getCategories(),
          getProductImages({ productId }),
        ]);

        if (ignore) return;

        setProduct(productData || null);
        setCategories(
          Array.isArray(categoryData)
            ? categoryData.filter((item) => item?.isDeleted !== true)
            : []
        );
        setProductImages(sortProductImages(Array.isArray(imageData) ? imageData : []));
        setSelectedImageIndex(0);
        setDraftQty(1);

        let nextBreakdown = [];
        try {
          const stockPayload = await getProductRetailStockByBranches(productId);
          nextBreakdown = Array.isArray(stockPayload?.branches) ? stockPayload.branches : [];
        } catch (stockError) {
          if (!ignore) {
            setStockContextError(
              stockError?.message || "Không thể tải tồn kho theo chi nhánh lúc này."
            );
          }
        }

        if (!ignore) {
          setStockBreakdown(nextBreakdown);
        }
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải chi tiết phụ tùng lúc này.");
          setProduct(null);
          setCategories([]);
          setProductImages([]);
          setStockBreakdown([]);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    if (!productId) {
      setLoading(false);
      setError("Không xác định được phụ tùng cần xem.");
      return undefined;
    }

    loadProductDetail();

    return () => {
      ignore = true;
    };
  }, [productId]);

  useEffect(() => {
    if (!stockBreakdown.length) {
      setSelectedBranchId("");
      return;
    }

    const idSet = new Set(stockBreakdown.map((row) => String(row.branchId)));

    setSelectedBranchId((current) => {
      if (current && idSet.has(String(current))) {
        return String(current);
      }

      const stored = readStoredRetailBranchId();
      if (stored && idSet.has(stored)) {
        return stored;
      }

      return String(stockBreakdown[0].branchId);
    });
  }, [stockBreakdown]);

  useEffect(() => {
    function handleExternalBranch(event) {
      const nextId = event?.detail?.branchId;
      if (nextId == null || nextId === "") {
        return;
      }

      setSelectedBranchId((current) => {
        if (!stockBreakdown.some((row) => String(row.branchId) === String(nextId))) {
          return current;
        }

        return String(nextId);
      });
    }

    window.addEventListener(RETAIL_BRANCH_CHANGED_EVENT, handleExternalBranch);
    return () => window.removeEventListener(RETAIL_BRANCH_CHANGED_EVENT, handleExternalBranch);
  }, [stockBreakdown]);

  const categoriesById = useMemo(
    () => new Map(categories.map((item) => [String(item.id), item])),
    [categories]
  );

  const category = useMemo(
    () => categoriesById.get(String(product?.categoryId)) || null,
    [categoriesById, product?.categoryId]
  );

  const images = useMemo(() => sortProductImages(productImages), [productImages]);

  useEffect(() => {
    if (!images.length) {
      setSelectedImageIndex(0);
      return;
    }

    if (selectedImageIndex > images.length - 1) {
      setSelectedImageIndex(0);
    }
  }, [images, selectedImageIndex]);

  const selectedImage = images[selectedImageIndex] || images[0] || null;
  const productImageSrc = selectedImage?.imageUrl || resolveProductImage(product, images);
  const productDescription = getProductDescription(product, images);
  const productName = getProductName(product, "Chi tiết phụ tùng");
  const productUnitLabel = getProductUnitLabel(product);
  const isAvailable = product?.isActive !== false && product?.isDeleted !== true;
  const retailPriceLabel =
    product?.retailPrice == null ? "Chưa cập nhật" : formatVnd(product.retailPrice);

  const selectedBranchRow = useMemo(
    () => stockBreakdown.find((row) => String(row.branchId) === String(selectedBranchId)) || null,
    [selectedBranchId, stockBreakdown]
  );

  const selectedBranchDisplayName = useMemo(() => {
    if (!selectedBranchRow) {
      return "";
    }

    return getBranchLabel({
      id: selectedBranchRow.branchId,
      branchName: selectedBranchRow.branchName,
      name: selectedBranchRow.branchName,
    });
  }, [selectedBranchRow]);

  const availableQuantity = useMemo(() => {
    if (stockContextError) {
      return null;
    }

    if (!stockBreakdown.length || !selectedBranchRow) {
      return null;
    }

    return normalizeAvailableQuantity(selectedBranchRow.availableQuantity);
  }, [selectedBranchRow, stockBreakdown.length, stockContextError]);

  const systemWideOutOfStock = useMemo(
    () =>
      !stockContextError &&
      stockBreakdown.length > 0 &&
      stockBreakdown.every((row) => normalizeAvailableQuantity(row.availableQuantity) <= 0),
    [stockBreakdown, stockContextError]
  );

  const alternativeStockBranches = useMemo(() => {
    if (!selectedBranchId || !stockBreakdown.length) {
      return [];
    }

    return stockBreakdown
      .filter(
        (row) =>
          String(row.branchId) !== String(selectedBranchId) &&
          normalizeAvailableQuantity(row.availableQuantity) > 0
      )
      .sort(
        (left, right) =>
          normalizeAvailableQuantity(right.availableQuantity) -
          normalizeAvailableQuantity(left.availableQuantity)
      )
      .slice(0, 3);
  }, [selectedBranchId, stockBreakdown]);

  const hasStockData =
    !stockContextError && stockBreakdown.length > 0 && selectedBranchRow != null;
  const isOutOfStock = hasStockData && availableQuantity != null && availableQuantity <= 0;
  const isStockUnavailable = !hasStockData;

  const stockPresentation = useMemo(() => {
    if (stockContextError) {
      return { text: stockContextError, toneClass: "is-unknown" };
    }

    if (!stockBreakdown.length) {
      return {
        text: "Chưa có dữ liệu tồn kho theo chi nhánh",
        toneClass: "is-unknown",
      };
    }

    if (!selectedBranchDisplayName) {
      return {
        text: "Chưa chọn được chi nhánh hiển thị",
        toneClass: "is-unknown",
      };
    }

    if (systemWideOutOfStock) {
      return {
        text: "Tạm hết hàng toàn hệ thống",
        toneClass: "is-out",
      };
    }

    if (availableQuantity != null && availableQuantity <= 0) {
      return {
        text: `Hiện không còn tồn kho tại ${selectedBranchDisplayName}`,
        toneClass: "is-out",
      };
    }

    if (availableQuantity != null && availableQuantity > 0) {
      return {
        text: `${selectedBranchDisplayName}: còn ${availableQuantity} sản phẩm`,
        toneClass: "is-in",
      };
    }

    return {
      text: "Chưa có dữ liệu tồn kho",
      toneClass: "is-unknown",
    };
  }, [
    availableQuantity,
    selectedBranchDisplayName,
    stockBreakdown.length,
    stockContextError,
    systemWideOutOfStock,
  ]);

  useEffect(() => {
    if (availableQuantity == null) {
      return;
    }

    if (availableQuantity <= 0) {
      setDraftQty(1);
      return;
    }

    setDraftQty((current) => clampQuantityToAvailable(current, availableQuantity, 1));
  }, [availableQuantity]);

  function handleBranchChange(event) {
    const nextValue = event.target.value;
    setSelectedBranchId(nextValue);
    writeStoredRetailBranchId(nextValue);
  }

  const technicalDetails = useMemo(
    () => [
      { label: "Đơn vị", value: productUnitLabel },
      { label: "Danh mục", value: getCategoryLabel(category) },
      { label: "Giá retail", value: retailPriceLabel },
      {
        label: "Giá dịch vụ",
        value:
          product?.servicePrice == null ? "Chưa cập nhật" : formatVnd(product.servicePrice),
      },
      { label: "Thuế suất", value: formatRate(product?.taxRate) },
      {
        label: "Bảo hành",
        value:
          product?.warrantyMonths == null
            ? "Chưa cập nhật"
            : `${product.warrantyMonths} tháng`,
      },
      {
        label: "Kiểm soát dung lượng nghiêm ngặt",
        value: formatFlag(product?.isStrictCapacityControl),
      },
      {
        label: "Yêu cầu hoàn vỏ rỗng",
        value: formatFlag(product?.requireEmptyReturn),
      },
      {
        label: "Trạng thái bán",
        value: formatStatus(product?.isActive),
      },
    ],
    [category, product, productUnitLabel, retailPriceLabel]
  );

  function handleAddToCart() {
    if (!product) return;

    if (!isAvailable) {
      setError("Phụ tùng này hiện không mở bán trong flow khách hàng.");
      return;
    }

    if (!hasStockData) {
      setError("Chưa xác định được tồn kho theo chi nhánh cho phụ tùng này.");
      return;
    }

    if (availableQuantity <= 0) {
      setError(
        `Phụ tùng này hiện không còn tồn kho tại ${selectedBranchDisplayName || "chi nhánh đang chọn"}.`
      );
      return;
    }

    const quantity = clampQuantityToAvailable(draftQty, availableQuantity, 1);
    const mutation = addToCart(product, quantity, { maxQty: availableQuantity });

    if (!mutation?.ok) {
      setError(
        `${productName} chỉ còn tối đa ${availableQuantity} sản phẩm tại ${selectedBranchDisplayName || "chi nhánh đang chọn"}.`
      );
      return;
    }

    if (mutation.adjusted) {
      setBannerMessage(
        `Đã thêm tối đa ${mutation.addedQuantity} ${productName} theo tồn kho tại ${selectedBranchDisplayName || "chi nhánh đang chọn"}.`
      );
      return;
    }

    setBannerMessage(`Đã thêm ${mutation.addedQuantity} ${productName} vào giỏ phụ tùng.`);
  }

  return (
    <div className="parts-shop-page part-product-page">
      <section className="parts-shop-hero part-product-hero">
        <RetailImage
          className="parts-shop-hero-image"
          src={productImageSrc}
          alt={getProductImageAlt(product, selectedImage)}
          fallbackClassName="part-product-hero-image-fallback"
          fallbackLabel="Ảnh phụ tùng đang cập nhật"
        />
        <div className="parts-shop-hero-overlay part-product-hero-overlay" />
        <Header overlay />
        <div className="parts-shop-hero-content part-product-hero-content">
          <div className="part-product-breadcrumbs">
            <Link to="/parts-shop">Part Shop</Link>
            <span>/</span>
            <span>Chi tiết phụ tùng</span>
          </div>
          <p className="parts-shop-hero-kicker">MYGARAGE PART DETAIL</p>
          <h1>{productName}</h1>
          <p className="parts-shop-hero-sub">
            Xem ảnh, thông tin kỹ thuật và thêm phụ tùng vào đúng giỏ bán lẻ hiện tại của bạn.
          </p>
          <div className="parts-shop-hero-actions">
            <RetailCartEntry itemCount={totalLineItems} label="Giỏ hàng" />
            <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-ghost">
              Quay lại catalog
            </Link>
            <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-ghost">
              Xem đơn phụ tùng
            </Link>
          </div>
        </div>
      </section>

      <div className="parts-shop">
        {error ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div>
        ) : null}
        {bannerMessage ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-success">
            {bannerMessage}
          </div>
        ) : null}

        {loading ? (
          <section className="parts-shop-card part-product-loading">
            Đang tải chi tiết phụ tùng...
          </section>
        ) : !product ? (
          <section className="parts-shop-card part-product-loading">
            Không tìm thấy phụ tùng phù hợp. Bạn có thể quay lại catalog để tiếp tục mua hàng.
          </section>
        ) : (
          <>
            <section className="parts-shop-card part-product-card">
              <div className="part-product-layout">
                <div className="part-product-gallery">
                  <div className="part-product-main-image">
                    <RetailImage
                      src={productImageSrc}
                      alt={getProductImageAlt(product, selectedImage)}
                      className="part-product-main-image-media"
                      fallbackClassName="part-product-main-image-fallback"
                      fallbackLabel="Ảnh phụ tùng đang cập nhật"
                    />
                  </div>

                  {images.length > 1 ? (
                    <div className="part-product-thumbnails" aria-label="Danh sách ảnh phụ tùng">
                      {images.map((image, index) => (
                        <button
                          type="button"
                          key={image.id || `${image.imageUrl}-${index}`}
                          className={`part-product-thumbnail ${
                            index === selectedImageIndex ? "active" : ""
                          }`}
                          aria-label={getProductImageAlt(product, image, index)}
                          onClick={() => setSelectedImageIndex(index)}
                        >
                          <RetailImage
                            src={image.imageUrl}
                            alt={getProductImageAlt(product, image, index)}
                            className="part-product-thumbnail-image"
                            fallbackClassName="part-product-thumbnail-fallback"
                            fallbackLabel="Ảnh"
                          />
                        </button>
                      ))}
                    </div>
                  ) : null}
                </div>

                <div className="part-product-summary">
                  <span className="parts-shop-tag">{getCategoryLabel(category)}</span>
                  <h2>{productName}</h2>
                  <div className="part-product-price-row">
                    <strong>{retailPriceLabel}</strong>
                    <span>{formatStatus(product.isActive)}</span>
                  </div>
                  <div className={`part-product-stock-hint ${stockPresentation.toneClass}`}>
                    <span className="part-product-stock-label">Tồn kho theo chi nhánh</span>
                    <strong>{stockPresentation.text}</strong>
                  </div>

                  {stockBreakdown.length > 0 ? (
                    <div className="part-product-branch-row">
                      <label className="part-product-branch-label" htmlFor="part-product-branch-select">
                        Chi nhánh xem tồn và mua
                      </label>
                      <select
                        id="part-product-branch-select"
                        className="part-product-branch-select"
                        value={selectedBranchId}
                        onChange={handleBranchChange}
                      >
                        {stockBreakdown.map((row) => {
                          const qty = normalizeAvailableQuantity(row.availableQuantity);
                          const label = getBranchLabel({
                            id: row.branchId,
                            branchName: row.branchName,
                            name: row.branchName,
                          });
                          return (
                            <option key={row.branchId} value={String(row.branchId)}>
                              {qty > 0 ? `${label} — còn ${qty}` : `${label} — hết hàng`}
                            </option>
                          );
                        })}
                      </select>
                    </div>
                  ) : null}

                  {alternativeStockBranches.length > 0 ? (
                    <ul className="part-product-stock-alternatives" aria-label="Chi nhánh khác còn hàng">
                      {alternativeStockBranches.map((row) => {
                        const qty = normalizeAvailableQuantity(row.availableQuantity);
                        const label = getBranchLabel({
                          id: row.branchId,
                          branchName: row.branchName,
                          name: row.branchName,
                        });
                        return (
                          <li key={row.branchId} className="part-product-stock-alt-item">
                            <span>
                              {label} còn {qty} sản phẩm
                            </span>
                            <button
                              type="button"
                              className="parts-shop-btn parts-shop-btn-ghost parts-shop-btn-inline part-product-branch-swap"
                              onClick={() => {
                                const id = String(row.branchId);
                                setSelectedBranchId(id);
                                writeStoredRetailBranchId(id);
                              }}
                            >
                              Đổi sang chi nhánh này
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  ) : null}

                  <p className="part-product-stock-footnote">
                    Chọn chi nhánh nhận hàng trong{" "}
                    <Link to="/parts-cart" className="part-product-inline-link">
                      giỏ hàng
                    </Link>{" "}
                    để đồng bộ thanh toán và tồn kho.
                  </p>

                  <div className="part-product-meta-grid">
                    <div>
                      <span>Đơn vị</span>
                      <strong>{productUnitLabel}</strong>
                    </div>
                    <div>
                      <span>Danh mục</span>
                      <strong>{getCategoryLabel(category)}</strong>
                    </div>
                  </div>

                  <p className="part-product-summary-text">{productDescription}</p>

                  <div className="part-product-cart-box">
                    <div className="part-product-cart-head">
                      <span>{totalLineItems} mặt hàng đang có trong giỏ</span>
                      <Link
                        to="/parts-cart"
                        className="parts-shop-btn parts-shop-btn-ghost parts-shop-btn-inline part-product-cart-link"
                      >
                        Mở cart
                      </Link>
                    </div>

                    <div className="part-product-cart-actions">
                      <div className="parts-shop-qty">
                        <button
                          type="button"
                          className="parts-shop-btn parts-shop-btn-ghost"
                          onClick={() =>
                            setDraftQty((current) =>
                              availableQuantity > 0
                                ? clampQuantityToAvailable(
                                    Math.max(1, Number(current || 1) - 1),
                                    availableQuantity,
                                    1
                                  )
                                : 1
                            )
                          }
                          disabled={isOutOfStock || isStockUnavailable}
                        >
                          -
                        </button>
                        <input
                          type="number"
                          min="1"
                          max={Math.max(availableQuantity, 1)}
                          value={draftQty}
                          onChange={(event) =>
                            setDraftQty(
                              availableQuantity > 0
                                ? clampQuantityToAvailable(
                                    toPositiveInt(event.target.value, 1),
                                    availableQuantity,
                                    1
                                  )
                                : 1
                            )
                          }
                          disabled={isOutOfStock || isStockUnavailable}
                        />
                        <button
                          type="button"
                          className="parts-shop-btn parts-shop-btn-ghost"
                          onClick={() =>
                            setDraftQty((current) =>
                              availableQuantity > 0
                                ? clampQuantityToAvailable(Number(current || 1) + 1, availableQuantity, 1)
                                : 1
                            )
                          }
                          disabled={isOutOfStock || isStockUnavailable || draftQty >= availableQuantity}
                        >
                          +
                        </button>
                      </div>

                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-primary part-product-cart-button"
                        onClick={handleAddToCart}
                        disabled={!isAvailable || isOutOfStock || isStockUnavailable}
                      >
                        {isStockUnavailable ? "Chưa rõ tồn kho" : isOutOfStock ? "Hết hàng" : "Thêm vào giỏ"}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <section className="part-product-sections">
              <article className="parts-shop-card part-product-section-card">
                <div className="part-product-section-head">
                  <span className="parts-shop-section-kicker">Mô tả</span>
                  <h3>Nội dung sản phẩm</h3>
                </div>
                <p className="part-product-section-text">{productDescription}</p>
              </article>

              <article className="parts-shop-card part-product-section-card">
                <div className="part-product-section-head">
                  <span className="parts-shop-section-kicker">Thông số</span>
                  <h3>Chi tiết kỹ thuật</h3>
                </div>
                <dl className="part-product-spec-grid">
                  {technicalDetails.map((item) => (
                    <div key={item.label}>
                      <dt>{item.label}</dt>
                      <dd>{item.value}</dd>
                    </div>
                  ))}
                </dl>
              </article>
            </section>
          </>
        )}
      </div>

      <Footer />
    </div>
  );
}
