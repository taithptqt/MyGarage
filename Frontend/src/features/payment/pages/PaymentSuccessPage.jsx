import { useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { syncOrder } from "@features/payment/api/payosApi";

export default function PaymentSuccessPage() {
  const navigate = useNavigate();
  const orderId = useMemo(() => localStorage.getItem("payos_last_order_id"), []);
  const retailOrderId = useMemo(() => localStorage.getItem("payos_last_retail_order_id"), []);
  const retrySearch = useMemo(() => localStorage.getItem("payos_last_payment_search") || "", []);

  const retryPath = retrySearch ? `/payment${retrySearch}` : "/payment";
  const isRetailCheckout = Boolean(retailOrderId);
  const retailOrdersPath = retailOrderId
    ? `/my-retail-orders?orderId=${encodeURIComponent(retailOrderId)}`
    : "/my-retail-orders";

  useEffect(() => {
    if (orderId) {
      // Trigger sync backend but do not block redirect UX.
      syncOrder(orderId).catch(() => {});
    }

    localStorage.removeItem("payos_last_payment_search");
    localStorage.removeItem("payos_last_checkout_kind");

    if (isRetailCheckout) {
      const hasOpener = typeof window !== "undefined" && window.opener && !window.opener.closed;
      if (hasOpener) {
        try {
          window.opener.postMessage(
            { type: "MYGARAGE_RETAIL_PAYMENT_COMPLETED", orderId: retailOrderId, skipWait: true },
            window.location.origin
          );
        } catch {
          // no-op
        }

        window.close();
        return;
      }

      navigate(retailOrdersPath, { replace: true });
      return;
    }

    navigate(retryPath, { replace: true });
  }, [isRetailCheckout, navigate, orderId, retailOrderId, retailOrdersPath, retryPath]);

  return null;
}
