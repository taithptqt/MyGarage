import { useEffect, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { createPayosOrder, getPayments } from "@features/payment/api/payosApi";
import { getRetailOrderById } from "@features/retail/api/retailApi";
import { useAuth } from "@shared/core/context/AuthContext";

function normalizeAmount(value) {
  if (typeof value === "number") {
    return Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0;
  }

  if (value == null) return 0;

  let raw = String(value).trim();
  if (!raw) return 0;

  raw = raw.replace(/[^\d,.-]/g, "");
  if (!raw) return 0;

  const hasDot = raw.includes(".");
  const hasComma = raw.includes(",");

  if (hasDot && hasComma) {
    const lastDot = raw.lastIndexOf(".");
    const lastComma = raw.lastIndexOf(",");
    const decimalIndex = Math.max(lastDot, lastComma);
    const decimalDigits = raw.length - decimalIndex - 1;

    if (decimalDigits > 0 && decimalDigits <= 2) {
      const integerPart = raw.slice(0, decimalIndex).replace(/[.,]/g, "");
      const fractionPart = raw.slice(decimalIndex + 1).replace(/[^\d]/g, "");
      raw = `${integerPart}.${fractionPart}`;
    } else {
      raw = raw.replace(/[.,]/g, "");
    }
  } else if (hasDot) {
    const parts = raw.split(".");
    if (parts.length > 2) {
      raw = parts.join("");
    } else if (parts.length === 2 && parts[1].length === 3) {
      raw = parts.join("");
    }
  } else if (hasComma) {
    const parts = raw.split(",");
    if (parts.length > 2) {
      raw = parts.join("");
    } else if (parts.length === 2 && parts[1].length === 3) {
      raw = parts.join("");
    } else if (parts.length === 2) {
      raw = `${parts[0]}.${parts[1]}`;
    }
  }

  const parsed = Number(raw);
  if (!Number.isFinite(parsed)) return 0;
  return Math.max(0, Math.floor(parsed));
}

function formatVnd(amount) {
  return `${new Intl.NumberFormat("vi-VN").format(Math.max(0, Math.floor(Number(amount || 0))))}d`;
}

function buildFrontendUrl(pathname) {
  if (typeof window === "undefined") return pathname;
  return `${window.location.origin}${pathname}`;
}

function buildRetrySearch({ amount, description, retailOrderId }) {
  const params = new URLSearchParams({
    amount: String(normalizeAmount(amount)),
    description: String(description || "Thanh toan MyGarage").slice(0, 25),
  });

  if (Number.isFinite(retailOrderId) && retailOrderId > 0) {
    params.set("retailOrderId", String(retailOrderId));
  }

  return `?${params.toString()}`;
}

export default function PayosCheckoutPage() {
  const location = useLocation();
  const { user, isAuthenticated } = useAuth();
  const [amount, setAmount] = useState(0);
  const [description, setDescription] = useState("Thanh toan MyGarage");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [preview, setPreview] = useState(null);
  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [autoPaid, setAutoPaid] = useState(false);
  const [retailAmountLoading, setRetailAmountLoading] = useState(false);
  const [retailAmountReady, setRetailAmountReady] = useState(false);
  const [retailSummary, setRetailSummary] = useState(null);

  const retailOrderId = useMemo(() => {
    const query = new URLSearchParams(location.search);
    const raw = normalizeAmount(query.get("retailOrderId"));
    return raw > 0 ? raw : null;
  }, [location.search]);

  const isRetailCheckout = Number.isFinite(retailOrderId);

  useEffect(() => {
    loadHistory();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const query = new URLSearchParams(location.search);
    const amountQuery = normalizeAmount(query.get("amount"));
    const descriptionQuery = query.get("description");

    if (amountQuery > 0) {
      setAmount(amountQuery);
    } else if (!isRetailCheckout) {
      setAmount(0);
    }

    if (descriptionQuery) {
      setDescription(String(descriptionQuery).slice(0, 25));
    } else if (isRetailCheckout && retailOrderId) {
      setDescription(`TT RO-${retailOrderId}`.slice(0, 25));
    }
  }, [isRetailCheckout, location.search, retailOrderId]);

  useEffect(() => {
    let ignore = false;

    async function resolveRetailAmount() {
      if (!isRetailCheckout || !retailOrderId) {
        setRetailSummary(null);
        return;
      }

      setRetailAmountReady(false);
      setRetailAmountLoading(true);

      try {
        const order = await getRetailOrderById(retailOrderId);
        if (ignore) return;

        const subTotal = normalizeAmount(order?.subTotal);
        const taxAmount = normalizeAmount(order?.taxAmount);
        const orderAmount = normalizeAmount(order?.finalAmount);

        setRetailSummary({
          orderCode: order?.orderCode || `RO-${retailOrderId}`,
          subTotal,
          taxAmount,
          finalAmount: orderAmount,
        });

        if (orderAmount <= 0) {
          setError("Khong lay duoc tong tien don hang. Vui long quay lai danh sach don de thanh toan lai.");
          return;
        }

        setAmount(orderAmount);
        setRetailAmountReady(true);
      } catch {
        if (!ignore) {
          setRetailSummary(null);
          setError("Khong lay duoc thong tin don retail. Vui long thu lai tu trang don hang.");
        }
      } finally {
        if (!ignore) {
          setRetailAmountLoading(false);
        }
      }
    }

    resolveRetailAmount();

    return () => {
      ignore = true;
    };
  }, [isRetailCheckout, retailOrderId]);

  const validAmount = useMemo(() => normalizeAmount(amount), [amount]);

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
      setError("Ban can dang nhap truoc khi thanh toan.");
      return;
    }

    if (isRetailCheckout && retailAmountLoading) {
      setError("Dang dong bo tong tien don retail, vui long cho trong giay lat.");
      return;
    }

    if (isRetailCheckout && !retailAmountReady) {
      setError("Chua xac nhan duoc tong tien don retail. Vui long thu lai tu trang don hang.");
      return;
    }

    if (validAmount < 1000) {
      setError(
        isRetailCheckout
          ? "Khong xac dinh duoc so tien don retail. Vui long thanh toan lai tu trang don hang."
          : "So tien toi thieu la 1,000d."
      );
      return;
    }

    setLoading(true);

    try {
      const finalDescription = (description || `TT ${isRetailCheckout ? `RO-${retailOrderId}` : "MyGarage"}`)
        .trim()
        .slice(0, 25);

      const retrySearch = buildRetrySearch({
        amount: validAmount,
        description: finalDescription,
        retailOrderId,
      });

      const payload = {
        totalAmount: validAmount,
        description: finalDescription,
        payerType: "CUSTOMER",
        payerId: user?.id || null,
        retailOrderId: isRetailCheckout ? retailOrderId : null,
        paymentType: "PAYMENT",
        returnUrl: buildFrontendUrl("/payment/success"),
        cancelUrl: buildFrontendUrl("/payment/cancel"),
        items: [
          {
            name: isRetailCheckout ? "Don phu tung MyGarage" : "Dich vu MyGarage",
            quantity: 1,
            price: validAmount,
          },
        ],
      };

      const result = await createPayosOrder(payload);
      if (!result.ok) {
        throw new Error(result.data?.message || "Khong tao duoc link thanh toan.");
      }

      const order = result.data;
      setPreview(order);
      await loadHistory();

      if (!order?.checkoutUrl) {
        throw new Error("Khong nhan duoc checkoutUrl tu PayOS.");
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
      setError(paymentError?.message || "Thanh toan that bai.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const query = new URLSearchParams(location.search);
    const autoPay = query.get("autoPay");

    if (
      autoPay === "1" &&
      !autoPaid &&
      !retailAmountLoading &&
      (!isRetailCheckout || retailAmountReady) &&
      validAmount >= 1000
    ) {
      setAutoPaid(true);
      handlePay();
    }
  }, [autoPaid, isRetailCheckout, location.search, retailAmountLoading, retailAmountReady, validAmount]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 16 }}>
        Thanh toan PayOS
      </h1>

      <div className="booking-card" style={{ maxWidth: 760 }}>
        <h3 style={{ marginBottom: 8 }}>
          {isRetailCheckout ? "Thanh toan don phu tung" : "Tao link thanh toan"}
        </h3>
        <p style={{ marginTop: 0, color: "#4a5568", lineHeight: 1.7 }}>
          {isRetailCheckout
            ? `Ban dang thanh toan cho don retail #${retailOrderId}. So tien duoc khoa theo tong tien don.`
            : "Ban co the tao link PayOS thu cong cho giao dich customer."}
        </p>

        <div style={{ display: "grid", gap: 12 }}>
          <div>
            <label
              style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}
            >
              So tien (VND)
            </label>
            <input
              type="number"
              min={1000}
              step={1000}
              value={amount}
              disabled={isRetailCheckout || retailAmountLoading}
              onChange={(event) => setAmount(event.target.value)}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
            {isRetailCheckout ? (
              <div style={{ marginTop: 6, fontSize: 12, color: "#718096" }}>
                So tien retail duoc dong bo tu backend de tranh sai lech.
              </div>
            ) : null}
          </div>

          <div>
            <label
              style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}
            >
              Noi dung
            </label>
            <input
              type="text"
              value={description}
              onChange={(event) => setDescription(event.target.value.slice(0, 25))}
              maxLength={25}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
            <div style={{ marginTop: 6, fontSize: 12, color: "#718096" }}>
              Toi da 25 ky tu theo gioi han PayOS ({description.length}/25)
            </div>
          </div>
        </div>

        <div style={{ marginTop: 14, color: "#4a5568", fontSize: 14 }}>
          Tong thanh toan: <strong style={{ color: "#422afb" }}>{formatVnd(validAmount)}</strong>
        </div>

        {isRetailCheckout && retailSummary ? (
          <div
            style={{
              marginTop: 12,
              border: "1px solid #e2e8f0",
              borderRadius: 10,
              padding: 12,
              background: "#f8fafc",
            }}
          >
            <div style={{ fontSize: 13, fontWeight: 600, color: "#334155", marginBottom: 8 }}>
              Chi tiet don retail: {retailSummary.orderCode}
            </div>
            <div style={{ display: "grid", gap: 6, fontSize: 13, color: "#475569" }}>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
                <span>Tam tinh</span>
                <strong>{formatVnd(retailSummary.subTotal)}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
                <span>Thue VAT</span>
                <strong>{formatVnd(retailSummary.taxAmount)}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 12, borderTop: "1px dashed #cbd5e1", paddingTop: 6 }}>
                <span>Tong thanh toan</span>
                <strong style={{ color: "#1d4ed8" }}>{formatVnd(retailSummary.finalAmount)}</strong>
              </div>
            </div>
          </div>
        ) : null}

        {error ? <div className="alert alert-error" style={{ marginTop: 12 }}>{error}</div> : null}

        <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginTop: 16 }}>
          <button
            className="btn-book"
            onClick={handlePay}
            disabled={loading || retailAmountLoading}
            style={{ marginTop: 0, width: "auto", minWidth: 220 }}
          >
            {loading ? "Dang tao link..." : "Thanh toan qua PayOS"}
          </button>
          {isRetailCheckout ? (
            <Link
              to="/my-retail-orders"
              className="btn-popup-secondary"
              style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
            >
              Xem don phu tung
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
          <h3 style={{ margin: 0 }}>Lich su giao dich PayOS</h3>
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
                                : "#fef3c7",
                        }}
                      >
                        {item.status || "PENDING"}
                      </span>
                    </td>
                    <td style={{ padding: "10px 8px" }}>{item.type || "-"}</td>
                    <td style={{ padding: "10px 8px" }}>
                      {item.createdAt
                        ? new Date(item.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })
                        : "-"}
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
