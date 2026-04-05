import { toPositiveInt } from "@features/retail/utils/retailFormat";

export default function ProductQuantitySelector({
  value,
  onChange,
  className = "",
  size = "default",
  label = "Số lượng",
  showLabel = true,
}) {
  const nextClassName = [
    "parts-shop-quantity-selector",
    `parts-shop-quantity-selector--${size}`,
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={nextClassName}>
      <button
        type="button"
        className="parts-shop-quantity-selector-btn"
        aria-label="Giảm số lượng"
        onClick={() => onChange(Math.max(1, Number(value || 1) - 1))}
      >
        -
      </button>

      <label className="parts-shop-quantity-selector-field">
        {showLabel ? (
          <span className="parts-shop-quantity-selector-label">{label}</span>
        ) : (
          <span className="parts-shop-quantity-selector-label parts-shop-quantity-selector-label--sr-only">
            {label}
          </span>
        )}
        <input
          type="number"
          min="1"
          inputMode="numeric"
          value={value}
          onChange={(event) => onChange(toPositiveInt(event.target.value, 1))}
        />
      </label>

      <button
        type="button"
        className="parts-shop-quantity-selector-btn"
        aria-label="Tăng số lượng"
        onClick={() => onChange(Number(value || 1) + 1)}
      >
        +
      </button>
    </div>
  );
}
