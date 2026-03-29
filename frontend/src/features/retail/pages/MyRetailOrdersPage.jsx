import { useEffect, useState } from "react";
import { getMyRetailOrders } from "../api/retailApi";

function money(v) {
  return `${Number(v || 0).toLocaleString("vi-VN")}đ`;
}

export default function MyRetailOrdersPage() {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    async function load() {
      setLoading(true);
      setError("");
      try {
        const data = await getMyRetailOrders();
        setOrders(data || []);
      } catch (e) {
        setError(e?.message || "Khong tai duoc danh sach don phu tung.");
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  return (
    <div className="parts-shop">
      <header className="parts-shop-header">
        <h1>Don phu tung cua toi</h1>
        <p>Theo doi tat ca don mua le phu tung da dat.</p>
      </header>

      {error ? <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div> : null}

      <section className="parts-shop-card">
        {loading ? (
          <div className="parts-shop-empty">Dang tai don hang...</div>
        ) : orders.length === 0 ? (
          <div className="parts-shop-empty">Chua co don phu tung nao.</div>
        ) : (
          <div className="table-wrapper">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Ma don</th>
                  <th>Chi nhanh</th>
                  <th>Trang thai</th>
                  <th>Tong tien</th>
                  <th>Ngay tao</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.id}>
                    <td>{o.orderCode || `#${o.id}`}</td>
                    <td>{o.branchName || `CN ${o.branchId}`}</td>
                    <td><span className="badge badge-inactive">{o.status || "DRAFT"}</span></td>
                    <td>{money(o.finalAmount)}</td>
                    <td>{o.createdAt ? new Date(o.createdAt).toLocaleString() : "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

