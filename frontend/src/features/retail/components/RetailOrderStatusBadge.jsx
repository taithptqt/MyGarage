import { getRetailOrderStatusMeta } from "@features/retail/utils/retailFormat";

export default function RetailOrderStatusBadge({ status }) {
  const meta = getRetailOrderStatusMeta(status);

  return (
    <span className={`retail-order-status retail-order-status--${meta.tone}`}>
      {meta.label}
    </span>
  );
}
