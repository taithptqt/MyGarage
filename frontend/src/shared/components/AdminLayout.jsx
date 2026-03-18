import { useMemo } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import "../../styles/admin.css";

const NAV = [
  {
    group: "Danh mục xe",
    items: [
      { to: "/admin/vehicle-brands", label: "Hãng xe", icon: "🏭" },
      { to: "/admin/vehicle-models", label: "Dòng xe", icon: "🚗" },
    ],
  },
];

export default function AdminLayout({ title, children }) {
  const navigate = useNavigate();
  const user = useMemo(() => {
    try { return JSON.parse(localStorage.getItem("mygarage_user") || "{}"); }
    catch { return {}; }
  }, []);

  function handleLogout() {
    localStorage.removeItem("mygarage_token");
    localStorage.removeItem("mygarage_user");
    navigate("/", { replace: true });
  }

  return (
    <div className="admin-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <h2>🔧 MyGarage</h2>
          <span>Admin Panel</span>
        </div>

        <ul className="sidebar-nav">
          {NAV.map((section) => (
            <li key={section.group}>
              <div className="nav-group-label">{section.group}</div>
              {section.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) => (isActive ? "active" : "")}
                >
                  <span>{item.icon}</span>
                  {item.label}
                </NavLink>
              ))}
            </li>
          ))}
        </ul>

        <div className="sidebar-footer">
          <div className="sidebar-user">{user.fullName || "Admin"}</div>
          <div>{user.role || ""}</div>
          <button
            onClick={handleLogout}
            style={{ marginTop: 10, width: "100%", background: "#1b3a57", fontSize: 13 }}
          >
            Đăng xuất
          </button>
        </div>
      </aside>

      <div className="admin-main">
        <header className="admin-topbar">
          <h1>{title}</h1>
        </header>
        <main className="admin-content">{children}</main>
      </div>
    </div>
  );
}
