import { NavLink, useNavigate } from "react-router-dom";
import { clearAuth, getUser } from "../../app/authState";
import "./CustomerLayout.css";

export default function CustomerLayout({ children }) {
  const navigate = useNavigate();
  const user = getUser();

  function handleLogout() {
    clearAuth();
    navigate("/", { replace: true });
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
              Dat lich
            </NavLink>
            <NavLink to="/my-appointments" className={({ isActive }) => isActive ? "active" : ""}>
              Lich cua toi
            </NavLink>
            <NavLink to="/payment" className={({ isActive }) => isActive ? "active" : ""}>
              Thanh toan
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

      <main className="customer-main">{children}</main>
    </div>
  );
}
