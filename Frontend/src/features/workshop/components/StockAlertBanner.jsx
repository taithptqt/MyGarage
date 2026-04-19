import { useEffect, useMemo, useState } from "react";
import { executionApi } from "@features/workshop/api/executionApi";

function formatQty(value) {
  return Number(value || 0).toLocaleString("vi-VN");
}

export default function StockAlertBanner({ branchId = null, maxItems = 5 }) {
  const [loading, setLoading] = useState(false);
  const [alerts, setAlerts] = useState([]);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setError("");
      try {
        const rows = await executionApi.getStockAlerts({
          resolved: false,
          take: Math.max(1, Number(maxItems) || 5),
        });
        if (!cancelled) {
          setAlerts(Array.isArray(rows) ? rows : []);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err?.message || "Không thể tải cảnh báo tồn kho.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    load();
    const timer = setInterval(load, 60000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [branchId, maxItems]);

  const visibleRows = useMemo(() => {
    if (!branchId) return alerts;
    const target = Number(branchId);
    return alerts.filter((item) => Number(item.branchId || item.warehouseBranchId || 0) === target);
  }, [alerts, branchId]);

  if (loading && visibleRows.length === 0) {
    return <div className="inventory-stock-alert-banner">Đang tải cảnh báo tồn kho...</div>;
  }

  if (error) {
    return <div className="inventory-stock-alert-banner is-error">{error}</div>;
  }

  if (visibleRows.length === 0) {
    return (
      <div className="inventory-stock-alert-banner is-ok">
        Không có cảnh báo tồn kho nghiêm trọng.
      </div>
    );
  }

  return (
    <div className="inventory-stock-alert-banner is-warning">
      <div className="inventory-stock-alert-title">Cảnh báo tồn kho cần xử lý</div>
      <div className="inventory-stock-alert-list">
        {visibleRows.slice(0, maxItems).map((item, index) => (
          <div
            key={`${item.id || index}-${item.productId || index}`}
            className="inventory-stock-alert-item"
            title={`Kho: ${item.warehouseName || "-"} | Loại: ${item.alertType || "-"}`}
          >
            <strong>{item.productName || item.sku || "Sản phẩm"}</strong>
            <span>
              Tồn hiện tại {formatQty(item.currentStock)} / Min {formatQty(item.minStock)}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}



