import { useEffect, useMemo, useState } from "react";
import { getUser, isAuthenticated } from "../../../app/authState";
import { createPayosOrder, getPayments } from "../api/payosApi";

function formatVnd(amount) {
  return new Intl.NumberFormat("vi-VN").format(amount) + " d";
}

export default function PayosCheckoutPage() {
  const user = getUser();
  const [amount, setAmount] = useState(10000);
  const [description, setDescription] = useState("Thanh toan MyGarage");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [preview, setPreview] = useState(null);
  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  useEffect(() => {
    loadHistory();
  }, []);

  const validAmount = useMemo(() => {
    const n = Number(amount);
    if (!Number.isFinite(n)) return 0;
    return Math.max(0, Math.floor(n));
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
        .filter((p) => p?.payerType === "CUSTOMER")
        .filter((p) => (userId ? Number(p?.payerId) === Number(userId) : true))
        .sort((a, b) => new Date(b?.createdAt || 0) - new Date(a?.createdAt || 0));
      setHistory(filtered);
    } catch {
      setHistory([]);
    } finally {
      setHistoryLoading(false);
    }
  }

  async function handlePay() {
    setError("");
    if (!isAuthenticated()) {
      setError("Ban can dang nhap truoc khi thanh toan.");
      return;
    }
    if (validAmount < 1000) {
      setError("So tien toi thieu la 1,000 VND.");
      return;
    }

    setLoading(true);
    try {
      const payload = {
        totalAmount: validAmount,
        description: (description || "Thanh toan MyGarage").trim(),
        payerType: "CUSTOMER",
        payerId: user?.id || null,
        paymentType: "PAYMENT",
        returnUrl: "https://localhost:7009/payment/success",
        cancelUrl: "https://localhost:7009/payment/cancel",
        items: [
          {
            name: "Dich vu MyGarage",
            quantity: 1,
            price: validAmount
          }
        ]
      };

      const result = await createPayosOrder(payload);
      if (!result.ok) {
        throw new Error(result.data?.message || "Khong tao duoc link thanh toan.");
      }

      const order = result.data;
      setPreview(order);
      await loadHistory();
      if (order?.checkoutUrl) {
        if (order?.paymentRecordId) {
          localStorage.setItem("payos_last_payment_id", String(order.paymentRecordId));
        }
        if (order?.id) {
          localStorage.setItem("payos_last_order_id", String(order.id));
        }
        window.location.href = order.checkoutUrl;
        return;
      }
      throw new Error("Khong nhan duoc checkoutUrl tu PAYOS.");
    } catch (e) {
      setError(e.message || "Thanh toan that bai.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 16 }}>
        Thanh toan PAYOS
      </h1>

      <div className="booking-card" style={{ maxWidth: 720 }}>
        <h3>Tao link thanh toan</h3>
        <div style={{ display: "grid", gap: 12 }}>
          <div>
            <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}>
              So tien (VND)
            </label>
            <input
              type="number"
              min={1000}
              step={1000}
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}>
              Noi dung
            </label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value.slice(0, 25))}
              maxLength={25}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
            <div style={{ marginTop: 6, fontSize: 12, color: "#718096" }}>
              Toi da 25 ky tu theo yeu cau PAYOS ({description.length}/25)
            </div>
          </div>
        </div>

        <div style={{ marginTop: 14, color: "#4a5568", fontSize: 14 }}>
          Tong thanh toan: <strong style={{ color: "#422afb" }}>{formatVnd(validAmount)}</strong>
        </div>

        {error && <div className="alert alert-error" style={{ marginTop: 12 }}>{error}</div>}

        <button className="btn-book" onClick={handlePay} disabled={loading} style={{ marginTop: 16 }}>
          {loading ? "Dang tao link..." : "Thanh toan qua PAYOS"}
        </button>

        {preview?.paymentLinkId && (
          <p style={{ marginTop: 12, fontSize: 13, color: "#718096" }}>
            PaymentLinkId: {preview.paymentLinkId}
          </p>
        )}
      </div>

      <div className="booking-card" style={{ maxWidth: 980, marginTop: 20 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <h3 style={{ margin: 0 }}>Lich su giao dich PAYOS</h3>
          <button
            type="button"
            onClick={loadHistory}
            disabled={historyLoading}
            style={{ border: "1px solid #d7def0", background: "#fff", borderRadius: 8, padding: "8px 12px", cursor: "pointer" }}
          >
            {historyLoading ? "Dang tai..." : "Tai lai"}
          </button>
        </div>

        {history.length === 0 ? (
          <p style={{ marginTop: 12, color: "#718096" }}>Chua co giao dich nao.</p>
        ) : (
          <div style={{ marginTop: 12, overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 14 }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "1px solid #edf2f7" }}>
                  <th style={{ padding: "10px 8px" }}>Ma GD</th>
                  <th style={{ padding: "10px 8px" }}>So tien</th>
                  <th style={{ padding: "10px 8px" }}>Trang thai</th>
                  <th style={{ padding: "10px 8px" }}>Loai</th>
                  <th style={{ padding: "10px 8px" }}>Thoi gian</th>
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
                                : "#fef3c7"
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
