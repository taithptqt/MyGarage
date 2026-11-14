import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { getPaymentStatus, syncOrder } from "@features/payment/api/payosApi";

const PAYMENT_MESSAGES = {
  completedRetail: "Thanh toán đơn phụ tùng đã được xác nhận thành công.",
  completedDefault: "Thanh toán đã được xác nhận thành công.",
  canceledOrFailed: "Giao dịch chưa thành công. Bạn có thể thử lại.",
  unknown: "Không thể đối soát giao dịch lúc này.",
  pending: "Giao dịch đang chờ xác nhận. Hệ thống sẽ tự động cập nhật.",
  missingTxnCode: "Không tìm thấy mã giao dịch để đối soát.",
  failedFetchStatus: "Không lấy được trạng thái thanh toán.",
  verifyingInitial: "Đang xác nhận giao dịch với PayOS...",
  redirectingRetail: "Đang chuyển đến danh sách đơn phụ tùng...",
  pageTitle: "Kết quả thanh toán",
  btnRetryCheck: "Kiểm tra lại",
  btnViewRetailOrders: "Xem đơn phụ tùng",
  btnNewTransaction: "Tạo giao dịch mới",
  btnBackToPartShop: "Quay lại Part Shop"
};

function normalizeOrderStatus(order) {
  const rawStatus = String(order?.status || "").toUpperCase();
  const amountRemaining = Number(order?.amountRemaining);

  if (rawStatus.includes("CANCEL")) return "CANCELED";
  if (rawStatus.includes("FAIL")) return "FAILED";
  if (rawStatus === "PAID") return "COMPLETED";

  if (Number.isFinite(amountRemaining)) {
    return amountRemaining <= 0 ? "COMPLETED" : "PENDING";
  }

  return "PENDING";
}

function getMessageByStatus(status, isRetail) {
  if (status === "COMPLETED") {
    return isRetail
      ? PAYMENT_MESSAGES.completedRetail
      : PAYMENT_MESSAGES.completedDefault;
  }

  if (status === "CANCELED" || status === "FAILED") {
    return PAYMENT_MESSAGES.canceledOrFailed;
  }

  if (status === "UNKNOWN") {
    return PAYMENT_MESSAGES.unknown;
  }

  return PAYMENT_MESSAGES.pending;
}

function getStatusStyle(status) {
  if (status === "COMPLETED") {
    return { color: "#166534", background: "#dcfce7", border: "#86efac" };
  }

  if (status === "CANCELED" || status === "FAILED") {
    return { color: "#991b1b", background: "#fee2e2", border: "#fca5a5" };
  }

  if (status === "UNKNOWN") {
    return { color: "#1e3a8a", background: "#dbeafe", border: "#93c5fd" };
  }

  return { color: "#92400e", background: "#fef3c7", border: "#fcd34d" };
}

export default function PaymentSuccessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const paymentId = useMemo(() => localStorage.getItem("payos_last_payment_id"), []);
  const storedOrderId = useMemo(() => localStorage.getItem("payos_last_order_id"), []);
  const queryOrderCode = searchParams.get("orderCode") || searchParams.get("orderId") || "";
  const orderId = storedOrderId || queryOrderCode;
  const retailOrderId = useMemo(() => localStorage.getItem("payos_last_retail_order_id"), []);
  const retrySearch = useMemo(() => localStorage.getItem("payos_last_payment_search") || "", []);

  const [status, setStatus] = useState("PENDING");
  const [message, setMessage] = useState(PAYMENT_MESSAGES.verifyingInitial);
  const [loading, setLoading] = useState(true);
  const [redirecting, setRedirecting] = useState(false);

  const retryPath = retrySearch ? `/payment${retrySearch}` : "/payment";
  const isRetailCheckout = Boolean(retailOrderId);
  const retailOrdersPath = retailOrderId
    ? `/my-retail-orders?orderId=${encodeURIComponent(retailOrderId)}`
    : "/my-retail-orders";

  const checkStatus = useCallback(async () => {
    setLoading(true);
    let orderBasedStatus = null;

    // Bước 1: Sync order với PayOS → trigger ApplyOutcomeAsync bên backend → gửi email
    if (orderId) {
      try {
        const orderResult = await syncOrder(orderId);
        if (orderResult.ok) {
          orderBasedStatus = normalizeOrderStatus(orderResult.data);
        }
      } catch {
        // ignore, fallback to paymentId check
      }
    }

    // Nếu đã xác định được status từ order và không có paymentId (retail không có payment record)
    if (!paymentId) {
      const current = orderBasedStatus || "UNKNOWN";
      setStatus(current);
      setMessage(
        orderBasedStatus
          ? getMessageByStatus(current, isRetailCheckout)
          : PAYMENT_MESSAGES.missingTxnCode
      );
      setLoading(false);
      return current;
    }

    // Bước 2: Kiểm tra trạng thái payment record
    try {
      const result = await getPaymentStatus(paymentId);
      if (!result.ok) {
        const fallbackStatus = orderBasedStatus || "UNKNOWN";
        setStatus(fallbackStatus);
        setMessage(
          orderBasedStatus
            ? getMessageByStatus(fallbackStatus, isRetailCheckout)
            : (result.data?.message || PAYMENT_MESSAGES.failedFetchStatus)
        );
        setLoading(false);
        return fallbackStatus;
      }

      const current = String(result.data?.status || orderBasedStatus || "PENDING").toUpperCase();
      setStatus(current);
      setMessage(getMessageByStatus(current, isRetailCheckout));
      setLoading(false);
      return current;
    } catch {
      const fallbackStatus = orderBasedStatus || "UNKNOWN";
      setStatus(fallbackStatus);
      setMessage(getMessageByStatus(fallbackStatus, isRetailCheckout));
      setLoading(false);
      return fallbackStatus;
    }
  }, [isRetailCheckout, orderId, paymentId]);

  useEffect(() => {
    let timer;
    let disposed = false;
    let retries = 0;
    const maxRetries = 20;

    async function run() {
      if (disposed) return;
      const current = await checkStatus();
      retries += 1;

      if (
        !disposed &&
        current !== "COMPLETED" &&
        current !== "FAILED" &&
        current !== "CANCELED" &&
        retries < maxRetries
      ) {
        timer = setTimeout(run, 3000);
      }
    }

    run();

    return () => {
      disposed = true;
      if (timer) clearTimeout(timer);
    };
  }, [checkStatus]);

  useEffect(() => {
    if (!isRetailCheckout || status !== "COMPLETED") return;

    setRedirecting(true);
    const redirectTimer = setTimeout(() => {
      localStorage.removeItem("payos_last_payment_search");
      localStorage.removeItem("payos_last_checkout_kind");

      const hasOpener = typeof window !== "undefined" && window.opener && !window.opener.closed;
      if (hasOpener) {
        try {
          window.opener.postMessage(
            { type: "MYGARAGE_RETAIL_PAYMENT_COMPLETED", orderId: retailOrderId },
            window.location.origin
          );
        } catch {
          // no-op
        }

        window.close();
        return;
      }

      navigate(retailOrdersPath, { replace: true });
    }, 500);

    return () => clearTimeout(redirectTimer);
  }, [isRetailCheckout, navigate, retailOrderId, retailOrdersPath, status]);

  const statusStyle = getStatusStyle(status);

  return (
    <div>
      <h1 style={{ fontSize: 26, fontWeight: 700, color: "#111827", marginBottom: 14 }}>
        {PAYMENT_MESSAGES.pageTitle}
      </h1>

      <div className="booking-card" style={{ maxWidth: 760 }}>
        <div
          style={{
            display: "inline-flex",
            alignItems: "center",
            borderRadius: 999,
            border: `1px solid ${statusStyle.border}`,
            background: statusStyle.background,
            color: statusStyle.color,
            fontSize: 12,
            fontWeight: 700,
            letterSpacing: 0.3,
            padding: "6px 12px",
            marginBottom: 10,
          }}
        >
          {status}
        </div>

        <p style={{ margin: 0, color: "#2d3748", lineHeight: 1.6 }}>{message}</p>

        {redirecting ? (
          <p style={{ marginTop: 10, marginBottom: 0, color: "#1d4ed8", fontWeight: 600 }}>
            {PAYMENT_MESSAGES.redirectingRetail}
          </p>
        ) : null}

        <div style={{ marginTop: 16, display: "flex", flexWrap: "wrap", gap: 10 }}>
          <button
            className="btn-book"
            onClick={checkStatus}
            disabled={loading || redirecting}
            style={{ marginTop: 0, width: "auto" }}
          >
            {PAYMENT_MESSAGES.btnRetryCheck}
          </button>

          <Link
            to={isRetailCheckout ? retailOrdersPath : retryPath}
            className="btn-book"
            style={{ textDecoration: "none", display: "inline-flex", alignItems: "center", marginTop: 0, width: "auto" }}
          >
            {isRetailCheckout ? PAYMENT_MESSAGES.btnViewRetailOrders : PAYMENT_MESSAGES.btnNewTransaction}
          </Link>

          {isRetailCheckout ? (
            <Link
              to="/parts-shop"
              className="btn-popup-secondary"
              style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
            >
              {PAYMENT_MESSAGES.btnBackToPartShop}
            </Link>
          ) : null}
        </div>
      </div>
    </div>
  );
}
