import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { cancelRetailOrder, getRetailOrderById } from "@features/retail/api/retailApi";
import { RETAIL_CART_STORAGE_KEY } from "@features/retail/utils/retailFormat";
import { RotateCcw, AlertTriangle, ArrowLeft, PackageCheck } from "lucide-react";

export default function PaymentCancelPage() {
  const navigate = useNavigate();
  const retailOrderId = useMemo(() => localStorage.getItem("payos_last_retail_order_id"), []);
  const [cancelledOrder, setCancelledOrder] = useState(null);
  const [restoring, setRestoring] = useState(false);

  useEffect(() => {
    if (!retailOrderId) return;

    let ignore = false;
    async function performCancel() {
      try {
        const [cancelRes, orderRes] = await Promise.allSettled([
          cancelRetailOrder(retailOrderId, "Khách hủy thanh toán trên cổng PayOS"),
          getRetailOrderById(retailOrderId),
        ]);

        if (ignore) return;

        if (orderRes.status === "fulfilled" && orderRes.value) {
          setCancelledOrder(orderRes.value);
        } else if (cancelRes.status === "fulfilled" && cancelRes.value) {
          setCancelledOrder(cancelRes.value);
        }
      } catch (err) {
        console.warn("Could not cancel retail order automatically:", err);
      }
    }

    void performCancel();

    return () => {
      ignore = true;
    };
  }, [retailOrderId]);

  const handleRestoreCart = () => {
    if (!cancelledOrder) {
      navigate("/parts-cart");
      return;
    }

    setRestoring(true);
    try {
      const items = cancelledOrder.items || cancelledOrder.retailOrderItems || [];
      const raw = localStorage.getItem(RETAIL_CART_STORAGE_KEY);
      let existingCart = [];
      try {
        existingCart = raw ? JSON.parse(raw) : [];
        if (!Array.isArray(existingCart)) existingCart = [];
      } catch {
        existingCart = [];
      }

      const existingMap = new Map(existingCart.map((it) => [Number(it.id), it]));

      items.forEach((item) => {
        const id = Number(item.productId || item.id);
        if (!Number.isFinite(id) || id <= 0) return;
        const qty = Math.max(1, Number(item.quantity || item.qty || 1));

        if (existingMap.has(id)) {
          const current = existingMap.get(id);
          existingMap.set(id, {
            ...current,
            qty: (current.qty || 1) + qty,
            selected: true,
          });
        } else {
          existingMap.set(id, {
            id,
            name: item.productName || item.name || `Phụ tùng #${id}`,
            sku: item.productSku || item.sku || null,
            unit: item.unit || null,
            retailPrice: Number(item.unitPriceAtTime || item.retailPrice || 0),
            qty,
            selected: true,
            categoryId: item.categoryId || null,
            imageUrl: item.imageUrl || null,
          });
        }
      });

      localStorage.setItem(RETAIL_CART_STORAGE_KEY, JSON.stringify(Array.from(existingMap.values())));
      localStorage.removeItem("payos_last_retail_order_id");
      localStorage.removeItem("payos_last_payment_search");

      navigate("/parts-cart");
    } catch (err) {
      console.error("Error restoring items to cart", err);
      navigate("/parts-cart");
    } finally {
      setRestoring(false);
    }
  };

  return (
    <div style={{ maxWidth: 760, margin: "40px auto", padding: "0 20px" }}>
      <div
        style={{
          background: "#ffffff",
          borderRadius: 16,
          padding: "32px 28px",
          boxShadow: "0 10px 25px -5px rgba(0, 0, 0, 0.05), 0 8px 10px -6px rgba(0, 0, 0, 0.01)",
          border: "1px solid #f1f5f9",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 14, marginBottom: 16 }}>
          <div
            style={{
              width: 48,
              height: 48,
              borderRadius: "50%",
              background: "#fff1f2",
              color: "#e11d48",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              flexShrink: 0,
            }}
          >
            <AlertTriangle size={26} />
          </div>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 700, color: "#0f172a", margin: 0 }}>
              Giao dịch đã hủy
            </h1>
            <p style={{ margin: "4px 0 0", color: "#64748b", fontSize: 14 }}>
              {retailOrderId
                ? `Đơn hàng #${cancelledOrder?.orderCode || `RO-${retailOrderId}`} đã được chuyển sang trạng thái ĐÃ HỦY.`
                : "Phiên giao dịch thanh toán PayOS đã kết thúc."}
            </p>
          </div>
        </div>

        <div
          style={{
            background: "#f8fafc",
            borderRadius: 12,
            padding: "16px 18px",
            border: "1px solid #e2e8f0",
            marginBottom: 24,
            fontSize: 14,
            color: "#334155",
            lineHeight: 1.6,
          }}
        >
          {retailOrderId ? (
            <>
              <p style={{ margin: 0, fontWeight: 500 }}>
                💡 Đơn hàng cũ đã được đóng lại để đảm bảo tính chính xác về tồn kho và giá bán. Nếu có sử dụng Voucher, lượt ưu đãi đã được tự động hoàn lại cho bạn.
              </p>
              <p style={{ margin: "8px 0 0", color: "#64748b" }}>
                Bạn có thể bấm <strong>"Khôi phục vào giỏ hàng"</strong> bên dưới để mang toàn bộ phụ tùng của đơn này trở lại giỏ hàng và tiến hành đặt đơn mới.
              </p>
            </>
          ) : (
            <p style={{ margin: 0 }}>
              Bạn đã hủy giao dịch trên PayOS. Bạn có thể quay lại để tiếp tục thao tác.
            </p>
          )}
        </div>

        <div style={{ display: "flex", flexWrap: "wrap", gap: 12 }}>
          {retailOrderId ? (
            <>
              <button
                type="button"
                onClick={handleRestoreCart}
                disabled={restoring}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 8,
                  padding: "11px 20px",
                  borderRadius: 10,
                  background: "#b45309",
                  color: "#ffffff",
                  border: "none",
                  fontWeight: 600,
                  fontSize: 14,
                  cursor: "pointer",
                  transition: "all 0.2s ease",
                }}
              >
                <RotateCcw size={16} />
                <span>{restoring ? "Đang khôi phục..." : "Khôi phục vào giỏ hàng"}</span>
              </button>

              <Link
                to="/my-retail-orders"
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 8,
                  padding: "11px 18px",
                  borderRadius: 10,
                  background: "#f1f5f9",
                  color: "#334155",
                  textDecoration: "none",
                  fontWeight: 600,
                  fontSize: 14,
                  border: "1px solid #cbd5e1",
                }}
              >
                <PackageCheck size={16} />
                <span>Xem đơn phụ tùng</span>
              </Link>
            </>
          ) : null}

          <Link
            to="/parts-shop"
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: 8,
              padding: "11px 18px",
              borderRadius: 10,
              background: "#ffffff",
              color: "#64748b",
              textDecoration: "none",
              fontWeight: 500,
              fontSize: 14,
              border: "1px solid #e2e8f0",
            }}
          >
            <ArrowLeft size={16} />
            <span>Tiếp tục mua phụ tùng</span>
          </Link>
        </div>
      </div>
    </div>
  );
}
