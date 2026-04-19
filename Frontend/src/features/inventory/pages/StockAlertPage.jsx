import React, { useState, useEffect, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./StockAlert.css";

function fmtNum(v) { return Number(v ?? 0).toLocaleString("vi-VN"); }
function fmtDate(v) {
  if (!v) return "--";
  return new Date(v).toLocaleDateString("vi-VN");
}

export default function StockAlertPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState("OUT_OF_STOCK"); // OUT_OF_STOCK, LOW_STOCK, ON_ORDER
  const [alerts, setAlerts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const fetchAlerts = useCallback(async () => {
    setLoading(true);
    try {
      // Mocking fetch or assuming API supports type filter or returns all alerts needing attention
      // Since it's stock-alerts, it should return array
      const res = await request({ 
        url: "/api/stock-alerts", 
        method: "GET", 
        params: { branchId: user?.branchId, resolved: false } 
      });
      setAlerts(res?.items ?? res ?? []);
    } catch (err) {
      setError("Kh?ng th? t?i th?ng b?o kho.");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    fetchAlerts();
  }, [fetchAlerts]);

  // Compute tabs data
  const processedAlerts = useMemo(() => {
    // We map raw data into categories
    let out = [];
    let low = [];
    let ord = [];

    alerts.forEach(a => {
      const type = (a.alertType || a.AlertType || "").toUpperCase();
      const stock = Number(a.currentStock ?? a.CurrentStock ?? 0);
      
      // If it's already on order
      if (a.isOnOrder || type === "ON_ORDER") {
        ord.push(a);
      } else if (stock <= 0 || type === "OUT_OF_STOCK") {
        out.push(a);
      } else {
        low.push({ ...a, type: "LOW_STOCK" });
      }
    });

    return { OUT_OF_STOCK: out, LOW_STOCK: low, ON_ORDER: ord };
  }, [alerts]);

  const currentList = processedAlerts[activeTab] || [];

  const handleCreatePo = (item) => {
    // Navigate to PO page and pre-fill productId
    navigate(`/inventory/purchase-orders/new?productId=${item.productId || item.ProductId}`);
  };

  return (
    <div className="stka-page">
      <div className="stka-header">
        <div>
          <h1 className="stka-title">?? Cầnh b?o T?n kho</h1>
          <p className="stka-subtitle">Tr? tu? th?ng minh cầnh b?o t? d?ng c?c m?t h?ng cần nh?p b? sung.</p>
        </div>
        <button className="stka-btn stka-btn-primary" onClick={() => navigate("/inventory/purchase-orders/new")}>
          + L?p Đơn Đặt H�ng (PO)
        </button>
      </div>

      {/* Global Banner if severe out of stock */}
      {processedAlerts.OUT_OF_STOCK.length > 0 && (
        <div className="stka-global-banner">
          <div className="stka-banner-icon">🚨</div>
          <div className="stka-banner-text">
            <strong>Cầnh b?o kh?n c?p:</strong> ?ang c? {processedAlerts.OUT_OF_STOCK.length} m?t h?ng d? h?t ho?n to?n trong kho. Cần l?n PO nh?p ngay l?p t?c!
          </div>
        </div>
      )}

      {error && <div className="stka-alert stka-alert-err">{error}</div>}

      <div className="stka-tabs">
        <button 
          className={`stka-tab ${activeTab === "OUT_OF_STOCK" ? "active" : ""}`}
          onClick={() => setActiveTab("OUT_OF_STOCK")}
        >
          ?? H?t h�ng ({processedAlerts.OUT_OF_STOCK.length})
        </button>
        <button 
          className={`stka-tab ${activeTab === "LOW_STOCK" ? "active" : ""}`}
          onClick={() => setActiveTab("LOW_STOCK")}
        >
          ?? S?p h?t ({processedAlerts.LOW_STOCK.length})
        </button>
        <button 
          className={`stka-tab ${activeTab === "ON_ORDER" ? "active" : ""}`}
          onClick={() => setActiveTab("ON_ORDER")}
        >
          ?? Đang d?t ({processedAlerts.ON_ORDER.length})
        </button>
      </div>

      <div className="stka-board">
        {loading ? (
          <div className="stka-loading">?ang t?i cầnh b?o...</div>
        ) : currentList.length === 0 ? (
          <div className="stka-empty">
            <span className="stka-empty-icon">?</span>
            <p>Kh?ng c? cầnh b?o n?o trong m?c n?y.</p>
          </div>
        ) : (
          <div className="stka-table-wrap">
            <table className="stka-table">
              <thead>
                <tr>
                  <th>M� phụ tùng</th>
                  <th>T�n phụ tùng</th>
                  <th className="t-right">T?n hi?n t?i</th>
                  <th className="t-right">M?c t?i thi?u</th>
                  <th>Ng?y cầnh b?o</th>
                  <th>H�nh d?ng</th>
                </tr>
              </thead>
              <tbody>
                {currentList.map(it => {
                  const stock = Number(it.currentStock ?? it.CurrentStock ?? 0);
                  const min = Number(it.minStockLevel ?? it.MinStockLevel ?? it.reorderPoint ?? 0);
                  const isOut = stock <= 0;
                  
                  return (
                    <tr key={it.id || it.Id}>
                      <td className="font-mono text-sm">{it.productCode ?? `--`}</td>
                      <td className="font-semibold">{it.productName ?? (it.product?.name ?? `SP-${it.productId}`)}</td>
                      <td className={`t-right font-bold ${isOut ? "text-red-600" : "text-amber-600"}`}>
                        {fmtNum(stock)} {it.unit ?? "cái"}
                      </td>
                      <td className="t-right text-slate-500">{fmtNum(min)}</td>
                      <td>{fmtDate(it.createdAt ?? it.CreatedAt)}</td>
                      <td>
                        {activeTab !== "ON_ORDER" ? (
                          <button className="stka-btn stka-btn-ghost btn-sm" onClick={() => handleCreatePo(it)}>
                            + T?o PO
                          </button>
                        ) : (
                          <span className="stka-status badge-blue">Đang v? kho</span>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}


