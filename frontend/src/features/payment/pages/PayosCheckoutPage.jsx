import { useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { createPayosOrder, getPayments } from "@features/payment/api/payosApi";
import { useAuth } from "@shared/core/context/AuthContext";

function formatVnd(amount) {
  return `${new Intl.NumberFormat("vi-VN").format(Math.max(0, Math.floor(Number(amount || 0))))}đ`;
}

function buildFrontendUrl(pathname) {
  if (typeof window === "undefined") return pathname;
  return `${window.location.origin}${pathname}`;
}

function buildRetrySearch({ amount, description, retailOrderId }) {
  const params = new URLSearchParams({
    amount: String(Math.max(0, Math.floor(Number(amount || 0)))),
    description: String(description || "Thanh toán MyGarage").slice(0, 25),
  });

  if (Number.isFinite(retailOrderId)) {
    params.set("retailOrderId", String(retailOrderId));
  }

  return `?${params.toString()}`;
}

export default function PayosCheckoutPage() {
  const location = useLocation();
  const { user, isAuthenticated } = useAuth();
  const [amount, setAmount] = useState(10000);
  const [description, setDescription] = useState("Thanh toán MyGarage");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [preview, setPreview] = useState(null);
  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [autoPaid, setAutoPaid] = useState(false);

  const retailOrderId = useMemo(() => {
    const query = new URLSearchParams(location.search);
    const raw = Number(query.get("retailOrderId"));
    return Number.isFinite(raw) ? raw : null;
  }, [location.search]);

  const isRetailCheckout = Number.isFinite(retailOrderId);

  useEffect(() => {
    loadHistory();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const query = new URLSearchParams(location.search);
    const amountQuery = Number(query.get("amount"));
    const descriptionQuery = query.get("description");

    if (Number.isFinite(amountQuery) && amountQuery > 0) {
      setAmount(Math.floor(amountQuery));
    }

    if (descriptionQuery) {
      setDescription(descriptionQuery.slice(0, 25));
    }
  }, [location.search]);

  const validAmount = useMemo(() => {
    const parsed = Number(amount);
    if (!Number.isFinite(parsed)) return 0;
    return Math.max(0, Math.floor(parsed));
  }, [amount]);

  async function loadHistory() {
    setHistoryLoading(true);

    try {
      const result = await getPayments();
      if (!result.ok) {
        setHistory([]);
        return;
      }

      const userId = user?.id;
      const rows = Array.isArray(result.data) ? result.data : [];
      const filtered = rows
        .filter((payment) => payment?.payerType === "CUSTOMER")
        .filter((payment) => (userId ? Number(payment?.payerId) === Number(userId) : true))
        .sort((left, right) => new Date(right?.createdAt || 0) - new Date(left?.createdAt || 0));

      setHistory(filtered);
    } catch {
      setHistory([]);
    } finally {
      setHistoryLoading(false);
    }
  }

  async function handlePay() {
    setError("");

    if (!isAuthenticated) {
      setError("Bạn cần đăng nhập trước khi thanh toán.");
      return;
    }

    if (validAmount < 1000) {
      setError("Số tiền tối thiểu là 1.000đ.");
      return;
    }

    setLoading(true);

    try {
      const retrySearch = buildRetrySearch({
        amount: validAmount,
        description,
        retailOrderId,
      });

      const payload = {
        totalAmount: validAmount,
        description: (description || "Thanh toán MyGarage").trim().slice(0, 25),
        payerType: "CUSTOMER",
        payerId: user?.id || null,
        retailOrderId: isRetailCheckout ? retailOrderId : null,
        paymentType: "PAYMENT",
        returnUrl: buildFrontendUrl("/payment/success"),
        cancelUrl: buildFrontendUrl("/payment/cancel"),
        items: [
          {
            name: isRetailCheckout ? "Đơn phụ tùng MyGarage" : "Dịch vụ MyGarage",
            quantity: 1,
            price: validAmount,
          },
        ],
      };

      const result = await createPayosOrder(payload);
      if (!result.ok) {
        throw new Error(result.data?.message || "Không tạo được link thanh toán.");
      }

      const order = result.data;
      setPreview(order);
      await loadHistory();

      if (!order?.checkoutUrl) {
        throw new Error("Không nhận được checkoutUrl từ PayOS.");
      }

      localStorage.setItem("payos_last_payment_search", retrySearch);
      localStorage.setItem("payos_last_checkout_kind", isRetailCheckout ? "RETAIL" : "SERVICE");

      if (order?.paymentRecordId) {
        localStorage.setItem("payos_last_payment_id", String(order.paymentRecordId));
      }

      if (order?.id) {
        localStorage.setItem("payos_last_order_id", String(order.id));
      }

      if (isRetailCheckout) {
        localStorage.setItem("payos_last_retail_order_id", String(retailOrderId));
      } else {
        localStorage.removeItem("payos_last_retail_order_id");
      }

      window.location.href = order.checkoutUrl;
    } catch (paymentError) {
      setError(paymentError?.message || "Thanh toán thất bại.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const query = new URLSearchParams(location.search);
    const autoPay = query.get("autoPay");

    if (autoPay === "1" && !autoPaid && validAmount >= 1000) {
      setAutoPaid(true);
      handlePay();
    }
  }, [autoPaid, location.search, validAmount]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 16 }}>
        Thanh toán PayOS
      </h1>

      <div className="booking-card" style={{ maxWidth: 760 }}>
        <h3 style={{ marginBottom: 8 }}>
          {isRetailCheckout ? "Thanh toán đơn phụ tùng" : "Tạo link thanh toán"}
        </h3>
        <p style={{ marginTop: 0, color: "#4a5568", lineHeight: 1.7 }}>
          {isRetailCheckout
            ? `Bạn đang thanh toán cho đơn bán lẻ #${retailOrderId}. Frontend vẫn giữ nguyên contract tạo payment hiện có của repo.`
            : "Bạn có thể tạo link PayOS thủ công cho giao dịch customer trong phạm vi frontend hiện tại."}
        </p>

        <div style={{ display: "grid", gap: 12 }}>
          <div>
            <label
              style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}
            >
              Số tiền (VND)
            </label>
            <input
              type="number"
              min={1000}
              step={1000}
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
          </div>

          <div>
            <label
              style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}
            >
              Nội dung
            </label>
            <input
              type="text"
              value={description}
              onChange={(event) => setDescription(event.target.value.slice(0, 25))}
              maxLength={25}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
            <div style={{ marginTop: 6, fontSize: 12, color: "#718096" }}>
              Tối đa 25 ký tự theo giới hạn PayOS ({description.length}/25)
            </div>
          </div>
        </div>

        <div style={{ marginTop: 14, color: "#4a5568", fontSize: 14 }}>
          Tổng thanh toán: <strong style={{ color: "#422afb" }}>{formatVnd(validAmount)}</strong>
        </div>

        {error ? <div className="alert alert-error" style={{ marginTop: 12 }}>{error}</div> : null}

        <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginTop: 16 }}>
          <button className="btn-book" onClick={handlePay} disabled={loading} style={{ marginTop: 0, width: "auto", minWidth: 220 }}>
            {loading ? "Đang tạo link..." : "Thanh toán qua PayOS"}
          </button>
          {isRetailCheckout ? (
            <Link
              to="/my-retail-orders"
              className="btn-popup-secondary"
              style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
            >
              Xem đơn phụ tùng
            </Link>
          ) : null}
        </div>

        {preview?.paymentLinkId ? (
          <p style={{ marginTop: 12, fontSize: 13, color: "#718096" }}>
            PaymentLinkId: {preview.paymentLinkId}
          </p>
        ) : null}
      </div>

      <div className="booking-card" style={{ maxWidth: 980, marginTop: 20 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <h3 style={{ margin: 0 }}>Lịch sử giao dịch PayOS</h3>
          <button
            type="button"
            onClick={loadHistory}
            disabled={historyLoading}
            style={{ border: "1px solid #d7def0", background: "#fff", borderRadius: 8, padding: "8px 12px", cursor: "pointer" }}
          >
            {historyLoading ? "Đang tải..." : "Tải lại"}
          </button>
        </div>

        {history.length === 0 ? (
          <p style={{ marginTop: 12, color: "#718096" }}>Chưa có giao dịch nào.</p>
        ) : (
          <div style={{ marginTop: 12, overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 14 }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "1px solid #edf2f7" }}>
                  <th style={{ padding: "10px 8px" }}>Mã GD</th>
                  <th style={{ padding: "10px 8px" }}>Số tiền</th>
                  <th style={{ padding: "10px 8px" }}>Trạng thái</th>
                  <th style={{ padding: "10px 8px" }}>Loại</th>
                  <th style={{ padding: "10px 8px" }}>Thời gian</th>
                </tr>
              </thead>
              <tbody>
                {history.map((item) => (
                  <tr key={item.id} style={{ borderBottom: "1px solid #f7fafc" }}>
                    <td style={{ padding: "10px 8px", fontFamily: "monospace" }}>
                      {item.transactionCode || "-"}
                    </td>
                    <td style={{ padding: "10px 8px" }}>{formatVnd(Number(item.amount || 0))}</td>
                    <td style={{ padding: "10px 8px" }}>
                      <span
                        style={{
                          display: "inline-block",
                          padding: "4px 10px",
                          borderRadius: 999,
                          fontSize: 12,
                          color:
                            item.status === "COMPLETED"
                              ? "#166534"
                              : item.status === "CANCELED"
                                ? "#991b1b"
                                : "#92400e",
                          background:
                            item.status === "COMPLETED"
                              ? "#dcfce7"
                              : item.status === "CANCELED"
                                ? "#fee2e2"
                                : "#fef3c7",
                        }}
                      >
                        {item.status || "PENDING"}
                      </span>
                    </td>
                    <td style={{ padding: "10px 8px" }}>{item.type || "-"}</td>
                    <td style={{ padding: "10px 8px" }}>
                      {item.createdAt ? new Date(item.createdAt).toLocaleString("vi-VN") : "-"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
