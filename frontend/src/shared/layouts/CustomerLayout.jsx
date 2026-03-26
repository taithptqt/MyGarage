import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import "./CustomerLayout.css";

export default function CustomerLayout({ children }) {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  function handleLogout() {
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="customer-layout">
      <header className="customer-navbar">
        <div className="customer-navbar-inner">
          <NavLink to="/home" className="customer-logo">
            MyGarage
          </NavLink>

          <nav className="customer-nav-links">
            <NavLink to="/booking" className={({ isActive }) => isActive ? "active" : ""}>
              Đặt lịch
            </NavLink>
            <NavLink to="/my-appointments" className={({ isActive }) => isActive ? "active" : ""}>
              Lịch của tôi
            </NavLink>
          </nav>

          <div className="customer-nav-user">
            <span className="customer-nav-name">{user?.fullName || "User"}</span>
            <button onClick={handleLogout} className="customer-nav-logout">
              Đăng xuất
            </button>
          </div>
        </div>
      </header>

      <main className="customer-main">{children}</main>
    </div>
  );
}
