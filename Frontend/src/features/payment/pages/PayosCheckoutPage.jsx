import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { createPayosOrder, getPayments } from "@features/payment/api/payosApi";
import { parsePaymentHistoryPage } from "@features/payment/api/paymentHistoryPage";
import { getRetailOrderById } from "@features/retail/api/retailApi";
import { useAuth } from "@shared/core/context/AuthContext";

const DEFAULT_DESCRIPTION = "Thanh toán MyGarage";

const UI_TEXT = {
  pageTitle: "Thanh toán PayOS",
  retailHeader: "Thanh toán đơn phụ tùng",
  manualHeader: "Tạo link thanh toán",
  retailGuide: (orderId) => `Bạn đang thanh toán cho đơn phụ tùng #${orderId}. Số tiền được khóa theo tổng tiền đơn.`,
  manualGuide: "Bạn có thể tạo link PayOS thủ công cho giao dịch khách hàng.",
  amountLabel: "Số tiền (VNĐ)",
  amountSyncNote: "Số tiền phụ tùng được đồng bộ từ backend để tránh sai lệch.",
  descriptionLabel: "Nội dung",
  descriptionLimitNote: (len) => `Tối đa 25 ký tự theo giới hạn PayOS (${len}/25)`,
  totalPaymentLabel: "Tổng thanh toán:",
  retailDetailHeader: (code) => `Chi tiết đơn phụ tùng: ${code}`,
  subTotalLabel: "Tạm tính",
  vatLabel: "Thuế VAT",
  creatingLink: "Đang tạo link...",
  payViaPayos: "Thanh toán qua PayOS",
  viewRetailOrder: "Xem đơn phụ tùng",
  historyTitle: "Lịch sử giao dịch PayOS",
  historyReload: "Tải lại",
  historyLoading: "Đang tải...",
  noTransactions: "Chưa có giao dịch nào.",
  colTxnCode: "Mã GD",
  colAmount: "Số tiền",
  colStatus: "Trạng thái",
  colType: "Loại",
  colTime: "Thời gian",
  prevPage: "Trang trước",
  nextPage: "Trang sau",
  pageIndicator: (page, total) => `Trang ${page} / ${total}`
};

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
  return `${new Intl.NumberFormat("vi-VN").format(Math.max(0, Math.floor(Number(amount || 0))))}đ`;
}

function buildFrontendUrl(pathname) {
  if (typeof window === "undefined") return pathname;
  return `${window.location.origin}${pathname}`;
}

function buildRetrySearch({ amount, description, retailOrderId }) {
  const params = new URLSearchParams({
    amount: String(normalizeAmount(amount)),
    description: String(description || DEFAULT_DESCRIPTION).slice(0, 25),
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
  const [description, setDescription] = useState(DEFAULT_DESCRIPTION);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [preview, setPreview] = useState(null);
  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);
  const [historyTotal, setHistoryTotal] = useState(0);
  const [historyError, setHistoryError] = useState("");
  const historySequence = useRef(0);
  const historyPageSize = 20;
  const [autoPaid, setAutoPaid] = useState(false);
  const [retailAmountLoading, setRetailAmountLoading] = useState(false);
  const [retailAmountReady, setRetailAmountReady] = useState(false);
  const [retailSummary, setRetailSummary] = useState(null);

  const queryParams = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const retailOrderId = useMemo(() => {
    const raw = queryParams.get("retailOrderId");
    const parsed = Number(raw);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  }, [queryParams]);
  const isRetailCheckout = Boolean(retailOrderId);

  useEffect(() => {
    loadHistory();
    return () => { historySequence.current += 1; };
  }, [historyPage, user?.id]); // eslint-disable-line react-hooks/exhaustive-deps

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
        setRetailAmountReady(false);
        setRetailAmountLoading(false);
        return;
      }

      setRetailAmountLoading(true);
      setRetailAmountReady(false);

      try {
        const order = await getRetailOrderById(retailOrderId);
        if (ignore) return;

        const candidate = [
          order?.finalAmount,
          order?.totalAmount,
          order?.grandTotal,
          order?.amount,
        ].find((val) => val !== undefined && val !== null);

        const parsedAmount = normalizeAmount(candidate);
        setRetailSummary(order);
        setAmount(parsedAmount);
        setRetailAmountReady(parsedAmount > 0);
      } catch (loadError) {
        if (ignore) return;
        setRetailSummary(null);
        setRetailAmountReady(false);
        setError(loadError?.message || "Không thể đồng bộ số tiền đơn retail từ backend.");
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
    const sequence = ++historySequence.current;
    setHistoryLoading(true);
    setHistoryError("");
    setHistory([]);

    try {
      const result = await getPayments({ page: historyPage, pageSize: historyPageSize });
      if (sequence !== historySequence.current) return;
      const { items: rows, total } = parsePaymentHistoryPage(result);
      const lastPage = Math.max(1, Math.ceil(total / historyPageSize));
      if (historyPage > lastPage) { setHistoryPage(lastPage); return; }
      setHistory(rows);
      setHistoryTotal(total);
    } catch (error) {
      if (sequence === historySequence.current) {
        setHistory([]);
        setHistoryTotal(0);
        setHistoryError(error.message || "Không tải được lịch sử giao dịch.");
      }
    } finally {
      if (sequence === historySequence.current) setHistoryLoading(false);
    }
  }

  async function handlePay() {
    setError("");

    if (!isAuthenticated) {
      setError("Bạn cần đăng nhập trước khi thanh toán.");
      return;
    }

    if (isRetailCheckout && retailAmountLoading) {
      setError("Đang đồng bộ tổng tiền đơn phụ tùng, vui lòng chờ trong giây lát.");
      return;
    }

    if (isRetailCheckout && !retailAmountReady) {
      setError("Chưa xác nhận được tổng tiền đơn phụ tùng. Vui lòng thử lại từ trang đơn hàng.");
      return;
    }

    if (validAmount < 1000) {
      setError(
        isRetailCheckout
          ? "Không xác định được số tiền đơn phụ tùng. Vui lòng thanh toán lại từ trang đơn hàng."
          : "Số tiền tối thiểu là 1,000đ."
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
        amount: validAmount,
        description: finalDescription,
        returnUrl: buildFrontendUrl("/payment/success"),
        cancelUrl: buildFrontendUrl(`/payment/cancel${retrySearch}`),
      };

      if (isRetailCheckout) {
        payload.retailOrderId = retailOrderId;
      }

      const result = await createPayosOrder(payload);

      if (!result.ok) {
        setError(result.data?.message || "Không thể tạo link thanh toán PayOS.");
        return;
      }

      const order = result.data;
      setPreview(order);

      if (!order?.checkoutUrl) {
        setError("Không nhận được checkoutUrl từ PayOS.");
        return;
      }

      if (order.id) {
        localStorage.setItem("payos_last_payment_id", String(order.id));
      } else {
        localStorage.removeItem("payos_last_payment_id");
      }

      if (order.orderCode || order.orderId) {
        localStorage.setItem("payos_last_order_id", String(order.orderCode || order.orderId));
      } else {
        localStorage.removeItem("payos_last_order_id");
      }

      localStorage.setItem("payos_last_payment_search", retrySearch);
      localStorage.setItem("payos_last_checkout_kind", isRetailCheckout ? "retail" : "manual");

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
        {UI_TEXT.pageTitle}
      </h1>

      <div className="booking-card" style={{ maxWidth: 760 }}>
        <h3 style={{ marginBottom: 8 }}>
          {isRetailCheckout ? UI_TEXT.retailHeader : UI_TEXT.manualHeader}
        </h3>
        <p style={{ marginTop: 0, color: "#4a5568", lineHeight: 1.7 }}>
          {isRetailCheckout
            ? UI_TEXT.retailGuide(retailOrderId)
            : UI_TEXT.manualGuide}
        </p>

        <div style={{ display: "grid", gap: 12 }}>
          <div>
            <label
              style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}
            >
              {UI_TEXT.amountLabel}
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
                {UI_TEXT.amountSyncNote}
              </div>
            ) : null}
          </div>

          <div>
            <label
              style={{ display: "block", fontSize: 13, fontWeight: 500, marginBottom: 6, color: "#4a5568" }}
            >
              {UI_TEXT.descriptionLabel}
            </label>
            <input
              type="text"
              value={description}
              onChange={(event) => setDescription(event.target.value.slice(0, 25))}
              maxLength={25}
              style={{ width: "100%", padding: "10px 14px", border: "1px solid #e2e8f0", borderRadius: 8, fontSize: 14 }}
            />
            <div style={{ marginTop: 6, fontSize: 12, color: "#718096" }}>
              {UI_TEXT.descriptionLimitNote(description.length)}
            </div>
          </div>
        </div>

        <div style={{ marginTop: 14, color: "#4a5568", fontSize: 14 }}>
          {UI_TEXT.totalPaymentLabel} <strong style={{ color: "#422afb" }}>{formatVnd(validAmount)}</strong>
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
              {UI_TEXT.retailDetailHeader(retailSummary.orderCode)}
            </div>
            <div style={{ display: "grid", gap: 6, fontSize: 13, color: "#475569" }}>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
                <span>{UI_TEXT.subTotalLabel}</span>
                <strong>{formatVnd(retailSummary.subTotal)}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
                <span>{UI_TEXT.vatLabel}</span>
                <strong>{formatVnd(retailSummary.taxAmount)}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 12, borderTop: "1px dashed #cbd5e1", paddingTop: 6 }}>
                <span>{UI_TEXT.totalPaymentLabel}</span>
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
            {loading ? UI_TEXT.creatingLink : UI_TEXT.payViaPayos}
          </button>
          {isRetailCheckout ? (
            <Link
              to="/my-retail-orders"
              className="btn-popup-secondary"
              style={{ textDecoration: "none", display: "inline-flex", alignItems: "center" }}
            >
              {UI_TEXT.viewRetailOrder}
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
          <h3 style={{ margin: 0 }}>{UI_TEXT.historyTitle}</h3>
          <button
            type="button"
            onClick={loadHistory}
            disabled={historyLoading}
            style={{ border: "1px solid #d7def0", background: "#fff", borderRadius: 8, padding: "8px 12px", cursor: "pointer" }}
          >
            {historyLoading ? UI_TEXT.historyLoading : UI_TEXT.historyReload}
          </button>
        </div>

        {historyError ? <p role="alert" style={{ marginTop: 12, color: "#991b1b" }}>{historyError}</p> : null}
        {!historyError && history.length === 0 ? (
          <p style={{ marginTop: 12, color: "#718096" }}>{UI_TEXT.noTransactions}</p>
        ) : !historyError ? (
          <div style={{ marginTop: 12, overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 14 }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "1px solid #edf2f7" }}>
                  <th style={{ padding: "10px 8px" }}>{UI_TEXT.colTxnCode}</th>
                  <th style={{ padding: "10px 8px" }}>{UI_TEXT.colAmount}</th>
                  <th style={{ padding: "10px 8px" }}>{UI_TEXT.colStatus}</th>
                  <th style={{ padding: "10px 8px" }}>{UI_TEXT.colType}</th>
                  <th style={{ padding: "10px 8px" }}>{UI_TEXT.colTime}</th>
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
        ) : null}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 12, marginTop: 12 }}>
          <button type="button" disabled={historyLoading || historyPage <= 1} onClick={() => setHistoryPage((page) => page - 1)}>{UI_TEXT.prevPage}</button>
          <span>{UI_TEXT.pageIndicator(historyPage, Math.max(1, Math.ceil(historyTotal / historyPageSize)))}</span>
          <button type="button" disabled={historyLoading || historyPage * historyPageSize >= historyTotal} onClick={() => setHistoryPage((page) => page + 1)}>{UI_TEXT.nextPage}</button>
        </div>
      </div>
    </div>
  );
}
