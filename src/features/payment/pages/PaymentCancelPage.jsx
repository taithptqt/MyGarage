import { useMemo } from "react";
import { Link } from "react-router-dom";

export default function PaymentCancelPage() {
  const retailOrderId = useMemo(() => localStorage.getItem("payos_last_retail_order_id"), []);
  const retrySearch = useMemo(() => localStorage.getItem("payos_last_payment_search") || "", []);
  const retryPath = retrySearch ? `/payment${retrySearch}` : "/payment";

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 12 }}>
        Giao dịch đã hủy
      </h1>
      <div className="booking-card" style={{ maxWidth: 720 }}>
        <p style={{ margin: 0, color: "#2d3748", lineHeight: 1.7 }}>
          {retailOrderId
            ? "Bạn đã hủy thanh toán cho đơn phụ tùng trên PayOS. Đơn bán lẻ vẫn có thể được kiểm tra lại từ màn hình đơn hàng của bạn."
            : "Bạn đã hủy giao dịch trên PayOS. Nếu cần, bạn có thể tạo lại link thanh toán."}
        </p>
        <div style={{ marginTop: 14, display: "flex", flexWrap: "wrap", gap: 10 }}>
          <Link
            to={retryPath}
            className="btn-book"
            style={{ textDecoration: "none", display: "inline-flex", alignItems: "center", marginTop: 0, width: "auto" }}
          >
            Thử lại thanh toán
          </Link>
          {retailOrderId ? (
            <>
              <Link
                to="/my-retail-orders"
                className="btn-popup-secondary"
                style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
              >
                Xem đơn phụ tùng
              </Link>
              <Link
                to="/parts-shop"
                className="btn-popup-secondary"
                style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
              >
              Quay lại
              </Link>
            </>
          ) : null}
        </div>
      </div>
    </div>
  );
}
