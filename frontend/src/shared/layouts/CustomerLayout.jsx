import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import "./CustomerLayout.css";

export default function CustomerLayout({ children }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const hideDefaultNavbar =
    location.pathname.startsWith("/booking") ||
    location.pathname.startsWith("/my-appointments");

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
                Dat lich
              </NavLink>
              <NavLink to="/my-appointments" className={({ isActive }) => isActive ? "active" : ""}>
                Lich cua toi
              </NavLink>
            </nav>

            <div className="customer-nav-user">
              <span className="customer-nav-name">{user?.fullName || "User"}</span>
              <button onClick={handleLogout} className="customer-nav-logout">
                Dang xuat
              </button>
            </div>
          </div>
        </header>
      )}

      <main className={`customer-main ${hideDefaultNavbar ? "customer-main--booking" : ""}`}>{children}</main>
    </div>
  );
}


