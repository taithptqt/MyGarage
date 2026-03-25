import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getPaymentStatus, syncOrder } from "../api/payosApi";

export default function PaymentSuccessPage() {
  const paymentId = useMemo(() => localStorage.getItem("payos_last_payment_id"), []);
  const orderId = useMemo(() => localStorage.getItem("payos_last_order_id"), []);
  const [status, setStatus] = useState("PENDING");
  const [message, setMessage] = useState("Dang xac nhan giao dich voi PAYOS...");
  const [loading, setLoading] = useState(true);

  async function checkStatus() {
    if (!paymentId) {
      setStatus("UNKNOWN");
      setMessage("Khong tim thay ma giao dich de doi soat.");
      setLoading(false);
      return "UNKNOWN";
    }

    if (orderId) {
      await syncOrder(orderId);
    }

    const result = await getPaymentStatus(paymentId);
    if (!result.ok) {
      setStatus("UNKNOWN");
      setMessage(result.data?.message || "Khong lay duoc trang thai thanh toan.");
      setLoading(false);
      return "UNKNOWN";
    }

    const current = (result.data?.status || "PENDING").toUpperCase();
    setStatus(current);
    if (current === "COMPLETED") {
      setMessage("Thanh toan da duoc xac nhan thanh cong.");
      setLoading(false);
      return current;
    }
    if (current === "CANCELED" || current === "FAILED") {
      setMessage("Giao dich chua thanh cong. Vui long thu lai.");
      setLoading(false);
      return current;
    }
    setMessage("Giao dich dang cho xac nhan. He thong se tu dong cap nhat.");
    setLoading(false);
    return current;
  }

  useEffect(() => {
    let timer;
    let isDisposed = false;
    let retries = 0;
    const maxRetries = 20;

    async function run() {
      if (isDisposed) return;
      const current = await checkStatus();
      retries += 1;
      if (!isDisposed && current !== "COMPLETED" && current !== "FAILED" && current !== "CANCELED" && retries < maxRetries) {
        timer = setTimeout(run, 3000);
      }
    }

    run();
    return () => {
      isDisposed = true;
      if (timer) clearTimeout(timer);
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 12 }}>
        Ket qua thanh toan
      </h1>
      <div className="booking-card" style={{ maxWidth: 720 }}>
        <p style={{ margin: 0, color: "#2d3748" }}>{message}</p>
        <div style={{ marginTop: 10, fontSize: 13, color: "#4a5568" }}>
          Trang thai hien tai: <strong>{status}</strong>
        </div>
        <div style={{ marginTop: 14, display: "flex", gap: 10 }}>
          <button className="btn-book" onClick={checkStatus} disabled={loading} style={{ marginTop: 0 }}>
            Kiem tra lai
          </button>
          <Link to="/payment" className="btn-book" style={{ textDecoration: "none", display: "inline-block", marginTop: 0 }}>
            Tao giao dich moi
          </Link>
        </div>
      </div>
    </div>
  );
}
