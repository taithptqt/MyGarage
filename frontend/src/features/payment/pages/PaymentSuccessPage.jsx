import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getPaymentStatus, syncOrder } from "@features/payment/api/payosApi";

export default function PaymentSuccessPage() {
  const paymentId = useMemo(() => localStorage.getItem("payos_last_payment_id"), []);
  const orderId = useMemo(() => localStorage.getItem("payos_last_order_id"), []);
  const retailOrderId = useMemo(() => localStorage.getItem("payos_last_retail_order_id"), []);
  const retrySearch = useMemo(() => localStorage.getItem("payos_last_payment_search") || "", []);
  const [status, setStatus] = useState("PENDING");
  const [message, setMessage] = useState("Đang xác nhận giao dịch với PayOS...");
  const [loading, setLoading] = useState(true);

  const retryPath = retrySearch ? `/payment${retrySearch}` : "/payment";
  const primaryLink = retailOrderId ? "/my-retail-orders" : retryPath;
  const primaryLabel = retailOrderId ? "Xem đơn phụ tùng" : "Tạo giao dịch mới";

  async function checkStatus() {
    if (!paymentId) {
      setStatus("UNKNOWN");
      setMessage("Không tìm thấy mã giao dịch để đối soát.");
      setLoading(false);
      return "UNKNOWN";
    }

    if (orderId) {
      await syncOrder(orderId);
    }

    const result = await getPaymentStatus(paymentId);
    if (!result.ok) {
      setStatus("UNKNOWN");
      setMessage(result.data?.message || "Không lấy được trạng thái thanh toán.");
      setLoading(false);
      return "UNKNOWN";
    }

    const current = (result.data?.status || "PENDING").toUpperCase();
    setStatus(current);

    if (current === "COMPLETED") {
      setMessage(
        retailOrderId
          ? "Thanh toán đơn phụ tùng đã được xác nhận thành công."
          : "Thanh toán đã được xác nhận thành công."
      );
      setLoading(false);
      return current;
    }

    if (current === "CANCELED" || current === "FAILED") {
      setMessage("Giao dịch chưa thành công. Bạn có thể thử lại.");
      setLoading(false);
      return current;
    }

    setMessage("Giao dịch đang chờ xác nhận. Hệ thống sẽ tự động cập nhật.");
    setLoading(false);
    return current;
  }

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
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 12 }}>
        Kết quả thanh toán
      </h1>
      <div className="booking-card" style={{ maxWidth: 720 }}>
        <p style={{ margin: 0, color: "#2d3748" }}>{message}</p>
        <div style={{ marginTop: 10, fontSize: 13, color: "#4a5568" }}>
          Trạng thái hiện tại: <strong>{status}</strong>
        </div>
        <div style={{ marginTop: 14, display: "flex", flexWrap: "wrap", gap: 10 }}>
          <button className="btn-book" onClick={checkStatus} disabled={loading} style={{ marginTop: 0, width: "auto" }}>
            Kiểm tra lại
          </button>
          <Link
            to={primaryLink}
            className="btn-book"
            style={{ textDecoration: "none", display: "inline-flex", alignItems: "center", marginTop: 0, width: "auto" }}
          >
            {primaryLabel}
          </Link>
          {retailOrderId ? (
            <Link
              to="/parts-shop"
              className="btn-popup-secondary"
              style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
            >
              Quay lại Part Shop
            </Link>
          ) : null}
        </div>
      </div>
    </div>
  );
}
