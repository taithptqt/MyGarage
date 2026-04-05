import { Link } from "react-router-dom";
import ProductQuantitySelector from "@features/retail/components/ProductQuantitySelector";
import { formatVnd, resolveProductImage } from "@features/retail/utils/retailFormat";
import { markPartsShopCatalogReturn } from "@features/retail/hooks/usePartsShopScroll";

function getProductSummary(product) {
  return product?.description || "Phụ tùng chính hãng được đồng bộ trực tiếp từ danh mục sản phẩm hiện tại.";
}

export default function ProductCard({
  product,
  categoryLabel,
  quantity,
  onQuantityChange,
  onAddToCart,
}) {
  return (
    <article className="parts-shop-gallery-card">
      <Link
        to={`/parts-shop/products/${product.id}`}
        className="parts-shop-gallery-link"
        aria-label={`Xem chi tiết ${product.name}`}
        onClick={markPartsShopCatalogReturn}
      >
        <div className="parts-shop-gallery-media">
          <img src={resolveProductImage(product)} alt={product.name} />
        </div>

        <div className="parts-shop-gallery-body">
          <span className="parts-shop-gallery-tag">{categoryLabel}</span>
          <h4>{product.name}</h4>
          <p className="parts-shop-gallery-meta">
            {product?.sku ? `SKU ${product.sku}` : `Mã sản phẩm #${product.id}`}
            {product?.unit ? ` • ${product.unit}` : ""}
          </p>
          <p className="parts-shop-gallery-desc">{getProductSummary(product)}</p>
        </div>
      </Link>

      <div className="parts-shop-gallery-footer">
        <div className="parts-shop-gallery-price">{formatVnd(product.retailPrice)}</div>

        <div className="parts-shop-gallery-actions">
          <ProductQuantitySelector
            value={quantity}
            onChange={onQuantityChange}
            size="compact"
            label={`Số lượng ${product.name}`}
            showLabel={false}
          />

          <button
            type="button"
            className="parts-shop-btn parts-shop-btn-primary"
            onClick={() => onAddToCart(product, quantity)}
          >
            Thêm giỏ
          </button>
        </div>
      </div>
    </article>
  );
}
