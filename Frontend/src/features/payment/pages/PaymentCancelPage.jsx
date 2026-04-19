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
            ? "B?n d� h?y thanh to�n cho don phụ tùng tr�n PayOS. Đơn b�n l? v?n c� th? du?c ki?m tra l?i t? m�n h�nh don h�ng c?a b?n."
            : "B?n d� h?y giao d?ch tr�n PayOS. N?u cần, b?n c� th? t?o l?i link thanh to�n."}
        </p>
        <div style={{ marginTop: 14, display: "flex", flexWrap: "wrap", gap: 10 }}>
          <Link
            to={retryPath}
            className="btn-book"
            style={{ textDecoration: "none", display: "inline-flex", alignItems: "center", marginTop: 0, width: "auto" }}
          >
            Th? l?i thanh to�n
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


