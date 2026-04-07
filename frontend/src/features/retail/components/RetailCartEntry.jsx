import { Link } from "react-router-dom";

function CartIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
      className="retail-cart-entry-icon"
    >
      <path
        d="M3.5 5.25h2.02a1 1 0 0 1 .97.77l.34 1.48h11.33a1 1 0 0 1 .98 1.2l-1.21 5.8a1 1 0 0 1-.98.8H9.43a1 1 0 0 1-.97-.76L6.15 6.75H3.5a.75.75 0 0 1 0-1.5Zm5.15 11.5a1.9 1.9 0 1 1 0 3.8 1.9 1.9 0 0 1 0-3.8Zm8.2 0a1.9 1.9 0 1 1 0 3.8 1.9 1.9 0 0 1 0-3.8Z"
        fill="currentColor"
      />
    </svg>
  );
}

export default function RetailCartEntry({
  to = "/parts-cart",
  totalItems = 0,
  label = "Giỏ hàng",
  variant = "primary",
  compact = false,
  className = "",
}) {
  const variantClass =
    variant === "ghost" ? "parts-shop-btn-ghost" : "parts-shop-btn-primary";

  return (
    <Link
      to={to}
      className={[
        "retail-cart-entry",
        variantClass,
        compact ? "retail-cart-entry--compact" : "",
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      aria-label={`${label}. Hiện có ${totalItems} sản phẩm trong giỏ`}
      title={label}
    >
      <span className="retail-cart-entry-icon-wrap" aria-hidden="true">
        <CartIcon />
        {totalItems > 0 ? (
          <span className="retail-cart-entry-badge">{totalItems}</span>
        ) : null}
      </span>
    </Link>
  );
}
