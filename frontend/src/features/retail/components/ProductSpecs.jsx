export default function ProductSpecs({ specs }) {
  return (
    <dl className="parts-shop-product-specs">
      {specs.map((spec) => (
        <div key={spec.label} className="parts-shop-product-spec">
          <dt className="parts-shop-product-spec-label">{spec.label}</dt>
          <dd className="parts-shop-product-spec-value">{spec.value}</dd>
        </div>
      ))}
    </dl>
  );
}
