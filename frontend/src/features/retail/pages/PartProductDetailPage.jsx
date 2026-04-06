import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import RetailImage from "@features/retail/components/RetailImage";
import {
  getCategories,
  getProductById,
  getProductImages,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  formatVnd,
  getCategoryLabel,
  getProductDescription,
  getProductImageAlt,
  getProductName,
  getProductUnitLabel,
  resolveProductImage,
  sortProductImages,
  toPositiveInt,
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
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [bannerMessage, setBannerMessage] = useState("");
  const [draftQty, setDraftQty] = useState(1);
  const [selectedImageIndex, setSelectedImageIndex] = useState(0);
  const { addToCart, totalItems } = useRetailCart();

  useEffect(() => {
    let ignore = false;

    async function loadProductDetail() {
      setLoading(true);
      setError("");
      setBannerMessage("");

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
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải chi tiết phụ tùng lúc này.");
          setProduct(null);
          setCategories([]);
          setProductImages([]);
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

  const technicalDetails = useMemo(
    () => [
      { label: "SKU", value: product?.sku || "Chưa cập nhật" },
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

    const quantity = toPositiveInt(draftQty, 1);
    addToCart(product, quantity);
    setBannerMessage(`Đã thêm ${quantity} ${productName} vào giỏ phụ tùng.`);
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
            <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-ghost">
              Quay lại catalog
            </Link>
            <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-primary">
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

                  <div className="part-product-meta-grid">
                    <div>
                      <span>SKU</span>
                      <strong>{product?.sku || "Chưa cập nhật"}</strong>
                    </div>
                    <div>
                      <span>Mã sản phẩm</span>
                      <strong>#{product?.id}</strong>
                    </div>
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
                      <span>{totalItems} sản phẩm đang có trong giỏ</span>
                      <Link
                        to="/parts-shop"
                        className="parts-shop-btn parts-shop-btn-ghost parts-shop-btn-inline part-product-cart-link"
                      >
                        Mở giỏ hàng
                      </Link>
                    </div>

                    <div className="part-product-cart-actions">
                      <div className="parts-shop-qty">
                        <button
                          type="button"
                          className="parts-shop-btn parts-shop-btn-ghost"
                          onClick={() => setDraftQty((current) => Math.max(1, Number(current || 1) - 1))}
                        >
                          -
                        </button>
                        <input
                          type="number"
                          min="1"
                          value={draftQty}
                          onChange={(event) => setDraftQty(toPositiveInt(event.target.value, 1))}
                        />
                        <button
                          type="button"
                          className="parts-shop-btn parts-shop-btn-ghost"
                          onClick={() => setDraftQty((current) => Number(current || 1) + 1)}
                        >
                          +
                        </button>
                      </div>

                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-primary part-product-cart-button"
                        onClick={handleAddToCart}
                        disabled={!isAvailable}
                      >
                        Thêm vào giỏ
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
