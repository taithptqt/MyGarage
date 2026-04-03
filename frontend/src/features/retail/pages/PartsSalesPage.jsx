import { useEffect, useMemo, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { getUser, isAuthenticated } from "../../../app/authState";
import {
  checkoutRetailOrder,
  getBranches,
  getCategories,
  getProducts,
} from "../api/retailApi";
import "./PartsSalesPage.css";

function fmtMoney(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

function toPositiveInt(value, fallback = 1) {
  const n = Number(value);
  if (!Number.isFinite(n)) return fallback;
  const i = Math.floor(n);
  return i > 0 ? i : fallback;
}

export default function PartsSalesPage() {
  const navigate = useNavigate();
  const user = getUser();
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [branches, setBranches] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [search, setSearch] = useState("");
  const [catFilter, setCatFilter] = useState("all");
  const [sortBy, setSortBy] = useState("name-asc");
  const [draftQty, setDraftQty] = useState({});
const [cart, setCart] = useState(() => {
  try {
    const raw = localStorage.getItem("mygarage_retail_cart");
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
});

  const [branchId, setBranchId] = useState("");
  const [guestName, setGuestName] = useState(user?.fullName || "");
  const [guestPhone, setGuestPhone] = useState("");
  const [notes, setNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState("");
  const [cartOpen, setCartOpen] = useState(false);

  function categoryLabel(cat) {
    const raw = cat?.categoryName || cat?.name || "";
    const text = String(raw).trim();
    return text || `Danh muc ${cat?.id ?? ""}`;
  }

  useEffect(() => {
    async function loadData() {
      setLoading(true);
      setError("");
      try {
        const [productData, categoryData, branchData] = await Promise.all([
          getProducts(),
          getCategories(),
          getBranches(),
        ]);
        setProducts((productData || []).filter((p) => p.isActive !== false));
        setCategories(categoryData || []);
        setBranches(branchData || []);

        const firstBranch = (branchData || [])[0];
        if (firstBranch?.id) setBranchId(String(firstBranch.id));
      } catch (e) {
        setError("Khong the tai du lieu phu tung.");
      } finally {
        setLoading(false);
      }
    }

    loadData();
  }, []);

  const filteredProducts = useMemo(() => {
    const q = search.trim().toLowerCase();
    const result = (products || []).filter((p) => {
      const okCategory = catFilter === "all" || String(p.categoryId) === String(catFilter);
      if (!okCategory) return false;
      if (!q) return true;
      const name = String(p.name || "").toLowerCase();
      const sku = String(p.sku || "").toLowerCase();
      return name.includes(q) || sku.includes(q);
    });

    result.sort((a, b) => {
      if (sortBy === "price-asc") return Number(a.retailPrice || 0) - Number(b.retailPrice || 0);
      if (sortBy === "price-desc") return Number(b.retailPrice || 0) - Number(a.retailPrice || 0);
      return String(a.name || "").localeCompare(String(b.name || ""));
    });

    return result;
  }, [products, search, catFilter, sortBy]);

  const total = useMemo(
    () => cart.reduce((sum, item) => sum + Number(item.retailPrice || 0) * Number(item.qty || 0), 0),
    [cart]
  );
  const totalItems = useMemo(
    () => cart.reduce((sum, item) => sum + Number(item.qty || 0), 0),
    [cart]
  );

  useEffect(() => {
    localStorage.setItem("mygarage_retail_cart", JSON.stringify(cart || []));
  }, [cart]);

  function addToCart(product) {
    const qty = toPositiveInt(draftQty[product.id], 1);

    setCart((prev) => {
      const exists = prev.find((x) => x.id === product.id);
      if (exists) {
        return prev.map((x) => (x.id === product.id ? { ...x, qty: x.qty + qty } : x));
      }
      return [
        ...prev,
        {
          id: product.id,
          name: product.name,
          retailPrice: Number(product.retailPrice || 0),
          qty,
          taxRate: Number(product.taxRate || 0),
        },
      ];
    });

    setDraftQty((prev) => ({ ...prev, [product.id]: 1 }));
  }

  function removeFromCart(productId) {
    setCart((prev) => prev.filter((x) => x.id !== productId));
  }

  function updateCartQty(productId, delta) {
    setCart((prev) =>
      prev
        .map((x) => (x.id === productId ? { ...x, qty: Math.max(1, x.qty + delta) } : x))
    );
  }

  async function handleCreateOrder() {
    setSuccessMessage("");
    if (!branchId) {
      setError("Vui long chon chi nhanh.");
      return;
    }
    if (!cart.length) {
      setError("Gio hang dang trong.");
      return;
    }

    setSubmitting(true);
    setError("");
    try {
      const payload = {
        branchId: Number(branchId),
        customerNotes: notes || null,
        items: cart.map((item) => ({
          productId: item.id,
          quantity: toPositiveInt(item.qty, 1),
        })),
      };

      const createdOrder = await checkoutRetailOrder(payload);
      const orderId = createdOrder?.id;
      if (!orderId) throw new Error("Khong tao duoc don hang.");

      setCart([]);
      localStorage.removeItem("mygarage_retail_cart");
      setNotes("");
      const orderCode = createdOrder.orderCode || `RO-${orderId}`;
      const finalAmount = Number(createdOrder?.finalAmount || total || 0);
      setSuccessMessage(`Tao don ban phu tung thanh cong. Ma don: ${orderCode}`);
      const desc = encodeURIComponent(`TT ${orderCode}`.slice(0, 25));
      navigate(`/payment?amount=${finalAmount}&description=${desc}&retailOrderId=${orderId}&autoPay=1`);
    } catch (e) {
      setError(e?.message || "Tao don that bai.");
    } finally {
      setSubmitting(false);
    }
  }

  if (!isAuthenticated()) {
    return <Navigate to="/login" replace />;
  }

  return (
    <div className="parts-shop stitch-retail-page">
      <section className="parts-shop-header">
        <h2>Retail Parts Shop</h2>
        <p>Dat phu tung nhanh, xem gia ro rang, tao don trong 1 man hinh.</p>
      </section>

      {error ? <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div> : null}
      {successMessage ? <div className="parts-shop-card parts-shop-alert parts-shop-alert-success">{successMessage}</div> : null}

      <div className="parts-shop-layout">
        <section className="parts-shop-card parts-shop-product-panel">
          <div className="parts-shop-cart-toolbar">
            <div className="parts-shop-cart-dropdown-wrap">
              <button
                type="button"
                className="parts-shop-cart-icon-btn"
                onClick={() => setCartOpen((prev) => !prev)}
                aria-expanded={cartOpen}
                aria-label="Toggle cart"
              >
                <span className="parts-shop-cart-icon" aria-hidden="true">🛒</span>
                <span>Cart</span>
                <span className="parts-shop-cart-badge">{totalItems}</span>
              </button>

              {cartOpen ? (
                <div className="parts-shop-cart-dropdown">
                  <h4>Your cart</h4>
                  {cart.length === 0 ? (
                    <p className="parts-shop-empty">No products in cart.</p>
                  ) : (
                    <div className="parts-shop-cart-list">
                      {cart.map((item) => (
                        <div className="parts-shop-cart-row" key={item.id}>
                          <div>
                            <div className="parts-shop-cart-name">{item.name}</div>
                            <small>{fmtMoney(item.retailPrice)} x {item.qty}</small>
                          </div>
                          <div className="parts-shop-cart-actions">
                            <button className="parts-shop-btn parts-shop-btn-ghost" onClick={() => updateCartQty(item.id, -1)}>-</button>
                            <button className="parts-shop-btn parts-shop-btn-ghost" onClick={() => updateCartQty(item.id, 1)}>+</button>
                            <button className="parts-shop-btn parts-shop-btn-ghost" onClick={() => removeFromCart(item.id)}>X</button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}

                  <div className="parts-shop-cart-footer">
                    <div>
                      <span>Subtotal</span>
                      <strong>{fmtMoney(total)}</strong>
                    </div>
                    <button
                      className="parts-shop-btn parts-shop-btn-primary"
                      onClick={handleCreateOrder}
                      disabled={submitting || !cart.length}
                    >
                      {submitting ? "Creating..." : "Create order"}
                    </button>
                  </div>
                </div>
              ) : null}
            </div>
          </div>

          <div className="parts-shop-filter">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by name or SKU"
            />
            <div className="parts-shop-filter-right">
              <select value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
                <option value="name-asc">Sort: Name A-Z</option>
                <option value="price-asc">Price: Low to high</option>
                <option value="price-desc">Price: High to low</option>
              </select>
            </div>
          </div>

          <div className="parts-shop-pills">
            <button
              className={`parts-shop-pill ${catFilter === "all" ? "active" : ""}`}
              onClick={() => setCatFilter("all")}
            >
              ALL
            </button>
            {categories.map((cat) => (
              <button
                key={cat.id}
                className={`parts-shop-pill ${String(catFilter) === String(cat.id) ? "active" : ""}`}
                onClick={() => setCatFilter(String(cat.id))}
              >
                {categoryLabel(cat)}
              </button>
            ))}
          </div>

          {loading ? (
            <div className="parts-shop-empty">Loading products...</div>
          ) : (
            <div className="parts-shop-list">
              {filteredProducts.map((p) => (
                <article className="parts-shop-item" key={p.id}>
                  <div className="parts-shop-item-media">
                    <img
                      src={`https://picsum.photos/seed/parts-${p.id}/640/760`}
                      alt={p.name}
                    />
                  </div>
                  <div className="parts-shop-item-head">
                    <div>
                      <span className="parts-shop-tag">{categoryLabel(categories.find((c) => String(c.id) === String(p.categoryId)))}</span>
                      <h4>{p.name}</h4>
                      <div className="parts-shop-meta">SKU: {p.sku || "N/A"}</div>
                    </div>
                  </div>
                  <div className="parts-shop-price">{fmtMoney(p.retailPrice)}</div>
                  <div className="parts-shop-actions">
                    <div className="parts-shop-qty">
                      <button
                        className="parts-shop-btn parts-shop-btn-ghost"
                        onClick={() =>
                          setDraftQty((prev) => ({ ...prev, [p.id]: Math.max(1, Number(prev[p.id] || 1) - 1) }))
                        }
                      >
                        -
                      </button>
                      <input
                        type="number"
                        min="1"
                        value={draftQty[p.id] || 1}
                        onChange={(e) =>
                          setDraftQty((prev) => ({
                            ...prev,
                            [p.id]: toPositiveInt(e.target.value, 1),
                          }))
                        }
                      />
                      <button
                        className="parts-shop-btn parts-shop-btn-ghost"
                        onClick={() => setDraftQty((prev) => ({ ...prev, [p.id]: Number(prev[p.id] || 1) + 1 }))}
                      >
                        +
                      </button>
                    </div>
                    <button className="parts-shop-btn parts-shop-btn-primary" onClick={() => addToCart(p)}>
                      Add to cart
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

