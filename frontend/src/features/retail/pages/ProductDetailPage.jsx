import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  getCategories,
  getProductById,
  getProductImages,
} from "@features/retail/api/retailApi";
import ProductGallery from "@features/retail/components/ProductGallery";
import ProductQuantitySelector from "@features/retail/components/ProductQuantitySelector";
import ProductSpecs from "@features/retail/components/ProductSpecs";
import {
  getPreferredScrollBehavior,
  markPartsShopCatalogReturn,
} from "@features/retail/hooks/usePartsShopScroll";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import {
  formatVnd,
  getCategoryLabel,
  resolveProductGallery,
} from "@features/retail/utils/retailFormat";
import "./PartsSalesPage.css";

function buildDescription(product, categoryLabel) {
  if (product?.description) {
    return product.description;
  }

  return `MyGarage hiện chưa đồng bộ mô tả đầy đủ cho ${
    product?.name || "phụ tùng này"
  }. Bạn vẫn có thể xem ảnh, giá bán lẻ, SKU và các thông tin cốt lõi để thêm vào giỏ hàng. Danh mục hiện tại: ${categoryLabel}.`;
}

function formatPercent(value) {
  const amount = Number(value);
  if (!Number.isFinite(amount)) return "0%";
  return `${amount}%`;
}

export default function ProductDetailPage() {
  const { productId } = useParams();
  const { user } = useAuth();
  const { addToCart } = useRetailCart();
  const [product, setProduct] = useState(null);
  const [categories, setCategories] = useState([]);
  const [productImages, setProductImages] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [activeImageIndex, setActiveImageIndex] = useState(0);
  const [bannerMessage, setBannerMessage] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadData() {
      setLoading(true);
      setError("");

      try {
        const [productData, categoryData, imageData] = await Promise.all([
          getProductById(productId),
          getCategories(),
          getProductImages(),
        ]);

        if (ignore) return;

        setProduct(productData || null);
        setCategories(Array.isArray(categoryData) ? categoryData : []);
        setProductImages(Array.isArray(imageData) ? imageData : []);
      } catch (loadError) {
        if (!ignore) {
          setProduct(null);
          setCategories([]);
          setProductImages([]);
          setError(loadError?.message || "Không thể tải chi tiết phụ tùng lúc này.");
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
  }, [productId]);

  const category = useMemo(
    () => categories.find((item) => String(item.id) === String(product?.categoryId)) || null,
    [categories, product?.categoryId]
  );

  const categoryLabel = useMemo(() => getCategoryLabel(category), [category]);

  const galleryImages = useMemo(
    () => resolveProductGallery(product, productImages),
    [product, productImages]
  );

  useEffect(() => {
    setActiveImageIndex(0);
  }, [galleryImages.length, product?.id]);

  useEffect(() => {
    if (typeof window === "undefined") return;

    window.scrollTo({
      top: 0,
      behavior: getPreferredScrollBehavior(),
    });
  }, [productId]);

  const productSpecs = useMemo(() => {
    if (!product) return [];

    return [
      { label: "SKU", value: product?.sku || "Chưa cập nhật" },
      { label: "Danh mục", value: categoryLabel },
      { label: "Đơn vị", value: product?.unit || "Tiêu chuẩn" },
      {
        label: "Bảo hành",
        value: product?.warrantyMonths ? `${product.warrantyMonths} tháng` : "Chưa cập nhật",
      },
      { label: "Thuế suất", value: formatPercent(product?.taxRate) },
      { label: "Tình trạng", value: product?.isActive === false ? "Ngừng bán" : "Đang mở bán" },
      { label: "Mã sản phẩm", value: `#${product.id}` },
    ];
  }, [categoryLabel, product]);

  const productHighlights = useMemo(() => {
    if (!product) return [];

    return [
      { label: "Danh mục", value: categoryLabel },
      { label: "Tình trạng", value: product?.isActive === false ? "Ngừng bán" : "Đang mở bán" },
      {
        label: "Bảo hành",
        value: product?.warrantyMonths ? `${product.warrantyMonths} tháng` : "Chưa cập nhật",
      },
      { label: "Thuế", value: formatPercent(product?.taxRate) },
    ];
  }, [categoryLabel, product]);

  function handleAddToCart() {
    if (!product) return;

    addToCart(product, quantity);
    setBannerMessage(`Đã thêm ${product.name} vào giỏ phụ tùng.`);
  }

  return (
    <div className="parts-shop-page">
      <section className="parts-shop-hero parts-shop-hero--detail">
        <img
          className="parts-shop-hero-image"
          src={galleryImages[0]?.src || "/assets/booking/showcase-1.jpg"}
          alt={product?.name || "Chi tiết phụ tùng MyGarage"}
        />
        <div className="parts-shop-hero-overlay" />
        <Header overlay />
        <div className="parts-shop-hero-content">
          <p className="parts-shop-hero-kicker">MYGARAGE PRODUCT DETAIL</p>
          <h1>{product?.name || "Chi tiết phụ tùng"}</h1>
          <p className="parts-shop-hero-sub">
            Không gian chi tiết theo tinh thần showroom cao cấp: ảnh lớn, thông tin cốt lõi rõ ràng
            và phần nội dung bên dưới được chia lớp mạch lạc, dễ đọc.
          </p>
          <div className="parts-shop-hero-actions">
            <Link
              to="/parts-shop"
              className="parts-shop-btn parts-shop-btn-ghost"
              onClick={markPartsShopCatalogReturn}
            >
              Quay lại Part Shop
            </Link>
            <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-primary">
              Xem đơn của tôi
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
          <section className="parts-shop-card parts-shop-product-detail-shell">
            <div className="parts-shop-empty">Đang tải chi tiết phụ tùng...</div>
          </section>
        ) : !product ? (
          <section className="parts-shop-card parts-shop-product-detail-shell">
            <div className="parts-shop-empty parts-shop-empty-panel">
              <p>Không tìm thấy phụ tùng bạn đang xem.</p>
              <Link
                to="/parts-shop"
                className="parts-shop-btn parts-shop-btn-primary"
                onClick={markPartsShopCatalogReturn}
              >
                Trở về danh mục
              </Link>
            </div>
          </section>
        ) : (
          <>
            <section className="parts-shop-card parts-shop-product-detail-shell">
              <div className="parts-shop-product-detail-layout">
                <ProductGallery
                  images={galleryImages}
                  activeIndex={activeImageIndex}
                  onSelect={setActiveImageIndex}
                  productName={product.name}
                />

                <div className="parts-shop-product-detail-summary">
                  <div className="parts-shop-product-detail-copy-group">
                    <span className="parts-shop-section-kicker">Showroom Part Detail</span>
                    <div className="parts-shop-product-detail-heading">
                      <h2>{product.name}</h2>
                      <div className="parts-shop-product-detail-meta-row">
                        <p className="parts-shop-product-detail-meta">
                          {categoryLabel}
                          {product?.sku ? ` • SKU ${product.sku}` : ""}
                        </p>
                        <span className="parts-shop-product-detail-stock-pill">
                          {product?.isActive === false ? "Ngừng bán" : "Sẵn sàng bán lẻ"}
                        </span>
                      </div>
                    </div>
                  </div>

                  <div className="parts-shop-product-detail-price-panel">
                    <div>
                      <span className="parts-shop-product-detail-price-label">Giá bán lẻ</span>
                      <div className="parts-shop-product-detail-price">
                        {formatVnd(product.retailPrice)}
                      </div>
                    </div>
                    <p className="parts-shop-product-detail-price-note">
                      Giá được đồng bộ từ dữ liệu sản phẩm hiện có của MyGarage và sẽ đi theo đúng flow
                      checkout hiện tại.
                    </p>
                  </div>

                  <p className="parts-shop-product-detail-intro">{buildDescription(product, categoryLabel)}</p>

                  <div className="parts-shop-product-detail-highlight-grid">
                    {productHighlights.map((item) => (
                      <article key={item.label} className="parts-shop-product-detail-highlight">
                        <span>{item.label}</span>
                        <strong>{item.value}</strong>
                      </article>
                    ))}
                  </div>

                  <div className="parts-shop-product-detail-actions-card">
                    <div className="parts-shop-product-detail-actions-copy">
                      <span className="parts-shop-product-detail-actions-kicker">Retail Checkout Ready</span>
                      <p>
                        Chọn số lượng phù hợp rồi thêm vào giỏ phụ tùng để tiếp tục checkout trong cùng
                        hành trình khách hàng.
                      </p>
                    </div>
                    <div className="parts-shop-product-detail-actions">
                      <ProductQuantitySelector
                        value={quantity}
                        onChange={setQuantity}
                        size="detail"
                        label={`Số lượng ${product.name}`}
                      />
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-primary parts-shop-product-detail-cta"
                        onClick={handleAddToCart}
                      >
                        Thêm vào giỏ
                      </button>
                    </div>
                  </div>

                  <div className="parts-shop-product-detail-notice">
                    <div className="parts-shop-product-detail-notice-icon" aria-hidden="true">
                      MG
                    </div>
                    <div>
                      <strong>{user?.fullName || "Khách hàng MyGarage"}</strong>
                      <span>
                        Bạn có thể thêm sản phẩm ở đây rồi quay lại Part Shop để tiếp tục checkout theo flow
                        hiện tại.
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <section className="parts-shop-product-detail-sections">
              <article className="parts-shop-card parts-shop-product-detail-section parts-shop-product-detail-section--story">
                <span className="parts-shop-section-kicker">Mô tả sản phẩm</span>
                <h3>Trình bày gọn, rõ và đúng trọng tâm</h3>
                <div className="parts-shop-product-detail-story">
                  <p>{buildDescription(product, categoryLabel)}</p>
                  <p>
                    Sản phẩm này được hiển thị theo tinh thần showroom premium: ảnh lớn để xem nhanh tổng
                    thể, thông tin cốt lõi được đẩy lên trước, và các chi tiết kỹ thuật nằm ngay bên dưới
                    để quyết định mua trở nên dễ dàng hơn.
                  </p>
                </div>
              </article>

              <article className="parts-shop-card parts-shop-product-detail-section parts-shop-product-detail-section--specs">
                <span className="parts-shop-section-kicker">Thông tin chính</span>
                <h3>Những dữ liệu hiện có từ hệ thống</h3>
                <ProductSpecs specs={productSpecs} />
              </article>

              <article className="parts-shop-card parts-shop-product-detail-section parts-shop-product-detail-section--detail">
                <span className="parts-shop-section-kicker">Chi tiết sản phẩm</span>
                <h3>Thông tin bổ sung cho quyết định mua</h3>
                <div className="parts-shop-product-detail-copy">
                  <div className="parts-shop-product-detail-note-block">
                    <span>Nguồn dữ liệu</span>
                    <p>
                      Giá, trạng thái bán lẻ và metadata đang lấy trực tiếp từ dữ liệu sản phẩm hiện có của
                      MyGarage. Nếu backend chưa có mô tả dài hoặc thông số sâu hơn, frontend sẽ hiển thị
                      fallback trung tính thay vì bịa thêm nội dung.
                    </p>
                  </div>
                  <div className="parts-shop-product-detail-note-block">
                    <span>Hiển thị hình ảnh</span>
                    <p>
                      Ảnh bên trên ưu tiên `product-images` nếu có nhiều ảnh. Khi backend chỉ có một ảnh hoặc
                      chưa có ảnh, trang sẽ tự động dùng ảnh mặc định để giữ bố cục showroom sáng, gọn và ổn
                      định.
                    </p>
                  </div>
                </div>
              </article>
            </section>
          </>
        )}
      </div>

      <Footer />
    </div>
  );
}
