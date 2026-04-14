import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import "./CustomerLayout.css";

export default function CustomerLayout({ children }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const hideDefaultNavbar =
    location.pathname.startsWith("/booking") ||
    location.pathname.startsWith("/my-appointments") ||
    location.pathname.startsWith("/my-garage-dashboard") ||
    location.pathname.startsWith("/my-vehicles") ||
    location.pathname.startsWith("/my-notifications") ||
    location.pathname.startsWith("/parts-cart") ||
    location.pathname.startsWith("/parts-shop") ||
    location.pathname.startsWith("/my-retail-orders");

  function handleLogout() {
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="customer-layout">
      {!hideDefaultNavbar && (
        <header className="customer-navbar">
          <div className="customer-navbar-inner">
            <NavLink to="/home" className="customer-logo">
              MyGarage
            </NavLink>

            <nav className="customer-nav-links">
              <NavLink to="/booking" className={({ isActive }) => isActive ? "active" : ""}>
                Đặt lịch
              </NavLink>
              <NavLink to="/parts-shop" className={({ isActive }) => isActive ? "active" : ""}>
                Part Shop
              </NavLink>
              <NavLink to="/my-garage-dashboard" className={({ isActive }) => isActive ? "active" : ""}>
                Garage
              </NavLink>
              <NavLink to="/my-vehicles" className={({ isActive }) => isActive ? "active" : ""}>
                Xe của tôi
              </NavLink>
              <NavLink to="/my-notifications" className={({ isActive }) => isActive ? "active" : ""}>
                Thông báo
              </NavLink>
              <NavLink to="/my-appointments" className={({ isActive }) => isActive ? "active" : ""}>
                Lịch hẹn
              </NavLink>
              <NavLink to="/my-retail-orders" className={({ isActive }) => isActive ? "active" : ""}>
                Đơn phụ tùng
              </NavLink>
            </nav>

            <div className="customer-nav-user">
              <span className="customer-nav-name">{user?.fullName || "Khách hàng"}</span>
              <button onClick={handleLogout} className="customer-nav-logout">
                Đăng xuất
              </button>
            </div>
          </div>
        </header>
      )}

      <main className={`customer-main ${hideDefaultNavbar ? "customer-main--booking" : ""}`}>{children}</main>
    </div>
  );
}


