import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import RetailOrderStatusBadge from "@features/retail/components/RetailOrderStatusBadge";
import { getBranches, getMyRetailOrders } from "@features/retail/api/retailApi";
import {
  formatVnd,
  getBranchLabel,
  getRetailOrderStatusMeta,
} from "@features/retail/utils/retailFormat";
import "./PartsSalesPage.css";

function formatDateTime(value) {
  if (!value) return "Chưa có";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa có";

  return date.toLocaleString("vi-VN");
}

function buildRepayLink(order) {
  const params = new URLSearchParams({
    amount: String(Math.max(0, Math.floor(Number(order?.finalAmount || 0)))),
    description: `TT ${String(order?.orderCode || `RO-${order?.id || ""}`).slice(0, 22)}`,
    retailOrderId: String(order?.id || ""),
  });

  return `/payment?${params.toString()}`;
}

export default function MyRetailOrdersPage() {
  const [orders, setOrders] = useState([]);
  const [branches, setBranches] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function load() {
      setLoading(true);
      setError("");

      try {
        const [orderData, branchData] = await Promise.all([
          getMyRetailOrders(),
          getBranches(),
        ]);

        if (ignore) return;

        const nextOrders = Array.isArray(orderData)
          ? [...orderData].sort(
              (left, right) =>
                new Date(right?.createdAt || 0).getTime() -
                new Date(left?.createdAt || 0).getTime()
            )
          : [];
        const nextBranches = Array.isArray(branchData) ? branchData : [];

        setOrders(nextOrders);
        setBranches(nextBranches);
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không tải được danh sách đơn phụ tùng.");
          setOrders([]);
          setBranches([]);
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, []);

  const branchById = useMemo(
    () => new Map(branches.map((branch) => [String(branch.id), branch])),
    [branches]
  );

  const orderStats = useMemo(() => {
    const draftCount = orders.filter(
      (order) => String(order?.status || "DRAFT").toUpperCase() === "DRAFT"
    ).length;
    const confirmedCount = orders.filter(
      (order) => String(order?.status || "").toUpperCase() === "CONFIRMED"
    ).length;
    const totalValue = orders.reduce(
      (sum, order) => sum + Number(order?.finalAmount || 0),
      0
    );

    return [
      { value: `${orders.length}`, label: "đơn phụ tùng đã tạo" },
      { value: `${draftCount}`, label: "đơn đang chờ thanh toán" },
      { value: `${confirmedCount}`, label: "đơn đã xác nhận" },
      { value: formatVnd(totalValue), label: "tổng giá trị hiển thị" },
    ];
  }, [orders]);

  return (
    <div className="parts-shop-page">
      <section className="parts-shop-hero parts-shop-hero--orders">
        <img
          className="parts-shop-hero-image"
          src="/assets/booking/showcase-1.jpg"
          alt="Không gian premium dành cho trải nghiệm hậu mãi và phụ tùng của MyGarage"
        />
        <div className="parts-shop-hero-overlay" />
        <Header overlay />
        <div className="parts-shop-hero-content">
          <p className="parts-shop-hero-kicker">MYGARAGE RETAIL ORDERS</p>
          <h1>Theo dõi toàn bộ đơn phụ tùng của bạn</h1>
          <p className="parts-shop-hero-sub">
            Trang này bám đúng workflow bán lẻ hiện có của repo: đơn được tạo từ Part Shop,
            status hiện tại đang theo backend DRAFT hoặc CONFIRMED, và thanh toán vẫn đi qua PayOS.
          </p>
          <div className="parts-shop-hero-stats">
            {orderStats.map((item) => (
              <article key={item.label} className="parts-shop-hero-stat">
                <strong>{item.value}</strong>
                <span>{item.label}</span>
              </article>
            ))}
          </div>
        </div>
      </section>

      <div className="parts-shop">
        <section className="parts-shop-header">
          <h2>Đơn phụ tùng của tôi</h2>
          <p>
            Mỗi đơn hiển thị theo dữ liệu backend hiện có: mã đơn, chi nhánh, trạng thái,
            số tiền cuối cùng, ghi chú và thời gian tạo/cập nhật.
          </p>
        </section>

        {error ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div>
        ) : null}

        <section className="parts-shop-card retail-orders-panel">
          {loading ? (
            <div className="parts-shop-empty">Đang tải đơn phụ tùng...</div>
          ) : orders.length === 0 ? (
            <div className="parts-shop-empty parts-shop-empty-panel">
              <p>Bạn chưa có đơn phụ tùng nào.</p>
              <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-primary">
                Mua phụ tùng ngay
              </Link>
            </div>
          ) : (
            <div className="retail-order-list">
              {orders.map((order) => {
                const normalizedStatus = String(order?.status || "DRAFT").toUpperCase();
                const statusMeta = getRetailOrderStatusMeta(normalizedStatus);
                const branch = branchById.get(String(order?.branchId));
                const canRetryPayment =
                  normalizedStatus === "DRAFT" && Number(order?.finalAmount || 0) >= 1000;

                return (
                  <article key={order.id} className="retail-order-card">
                    <div className="retail-order-card-top">
                      <div>
                        <div className="retail-order-card-code">
                          {order.orderCode || `RO-${order.id}`}
                        </div>
                        <p className="retail-order-card-desc">{statusMeta.description}</p>
                      </div>
                      <RetailOrderStatusBadge status={normalizedStatus} />
                    </div>

                    <dl className="retail-order-grid">
                      <div>
                        <dt>Chi nhánh</dt>
                        <dd>{branch ? getBranchLabel(branch) : `Chi nhánh #${order.branchId}`}</dd>
                      </div>
                      <div>
                        <dt>Tổng thanh toán</dt>
                        <dd>{formatVnd(order.finalAmount)}</dd>
                      </div>
                      <div>
                        <dt>Tạo lúc</dt>
                        <dd>{formatDateTime(order.createdAt)}</dd>
                      </div>
                      <div>
                        <dt>Cập nhật</dt>
                        <dd>{formatDateTime(order.updatedAt)}</dd>
                      </div>
                    </dl>

                    {order.customerNotes ? (
                      <div className="retail-order-notes">
                        <span>Ghi chú khách hàng</span>
                        <p>{order.customerNotes}</p>
                      </div>
                    ) : null}

                    <div className="retail-order-actions">
                      <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-ghost">
                        Mua thêm phụ tùng
                      </Link>
                      {canRetryPayment ? (
                        <Link
                          to={buildRepayLink(order)}
                          className="parts-shop-btn parts-shop-btn-primary"
                        >
                          Thanh toán đơn này
                        </Link>
                      ) : null}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>

      <Footer />
    </div>
  );
}
