export default function ProductGallery({
  images,
  activeIndex,
  onSelect,
  productName,
}) {
  const activeImage = images[activeIndex] || images[0];

  return (
    <div className="parts-shop-product-detail-gallery">
      <div className="parts-shop-product-detail-main-image-shell">
        <div className="parts-shop-product-detail-main-image">
          <img src={activeImage?.src} alt={activeImage?.alt || productName || "Ảnh sản phẩm"} />
        </div>
        <div className="parts-shop-product-detail-gallery-badge">
          <span>Gallery</span>
          <strong>
            {Math.min(activeIndex + 1, images.length)} / {images.length}
          </strong>
        </div>
      </div>

      {images.length > 1 ? (
        <div className="parts-shop-product-detail-thumbnails" aria-label="Bộ ảnh sản phẩm">
          {images.map((image, index) => (
            <button
              key={image.id}
              type="button"
              className={`parts-shop-product-thumb ${index === activeIndex ? "is-active" : ""}`}
              onClick={() => onSelect(index)}
              aria-label={`Xem ảnh ${index + 1} của ${productName || "sản phẩm"}`}
              aria-pressed={index === activeIndex}
            >
              <img src={image.src} alt={image.alt || `Ảnh sản phẩm ${index + 1}`} />
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
