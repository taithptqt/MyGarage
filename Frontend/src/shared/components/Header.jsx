import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import ProfileDialog from "@features/profile/components/ProfileDialog";
import { Bell, Menu, X } from "lucide-react";
import "./Header.css";
import { mediaUrl } from "@shared/brand/media";

function resolveAvatarUrl(avatarUrl) {
  if (!avatarUrl) return "";
  const raw = String(avatarUrl).trim();
  if (!raw) return "";
  if (
    raw.startsWith("http://") ||
    raw.startsWith("https://") ||
    raw.startsWith("data:") ||
    raw.startsWith("blob:")
  ) {
    return raw;
  }
  const base = getApiBaseUrl();
  if (!base) return raw;
  return `${base}${raw.startsWith("/") ? "" : "/"}${raw}`;
}

function Header({ overlay = false }) {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [showProfileDialog, setShowProfileDialog] = useState(false);
  const menuRef = useRef(null);

  const displayName = String(user?.fullName || user?.name || "Bạn").trim();
  const avatarUrl = resolveAvatarUrl(user?.avatarUrl || user?.AvatarUrl);
  const avatarInitial = displayName ? displayName.charAt(0).toUpperCase() : "B";
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isCustomer = roleCode === "CUSTOMER";
  const isStaffOrAdmin = ["ADMIN", "ACCOUNTANT", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER"].includes(roleCode);
  const customerPortalLinks = useMemo(
    () => [
      { label: "Garage", path: "/my-garage-dashboard" },
      { label: "Xe của tôi", path: "/my-vehicles" },
      { label: "Lịch hẹn", path: "/my-appointments" },
      { label: "Đơn phụ tùng", path: "/my-retail-orders" },
    ],
    []
  );

  const dashboardPath = useMemo(() => {
    switch (roleCode) {
      case "ADMIN":
        return "/admin/default";
      case "ACCOUNTANT":
        return "/accounting/dashboard";
      case "RECEPTIONIST":
        return "/workshop/reception";
      case "TECH":
        return "/workshop/technician";
      case "MANAGER":
        return "/workshop/manager";
      case "QUALITY_CONTROL":
        return "/workshop/qc";
      case "FOREMAN":
        return "/workshop/foreman";
      case "TEAM_LEADER":
        return "/workshop/team-leader";
      default:
        return "";
    }
  }, [roleCode]);

  useEffect(() => {
    setMenuOpen(false);
    setMobileOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    const close = event => { if (event.key === "Escape") { setMenuOpen(false); setMobileOpen(false); } };
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, []);

  useEffect(() => {
    if (!menuOpen) return undefined;
    const onDocClick = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, [menuOpen]);

  const handleNavigate = (path) => {
    setMenuOpen(false);
    setMobileOpen(false);
    if (location.pathname === path) {
      window.scrollTo({ top: 0, behavior: "smooth" });
      return;
    }
    navigate(path);
    window.setTimeout(() => {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }, 0);
  };

  const handleLogout = () => {
    setMenuOpen(false);
    setShowProfileDialog(false);
    logout();
    navigate("/home", { replace: true });
  };

  return (
    <nav className={`mg-header-wrap ${overlay ? "overlay" : "solid"} ${mobileOpen ? "menu-open" : ""}`}>
      <div className="mg-header-main">
        <button className="mg-header-menu-toggle" type="button" aria-label={mobileOpen ? "Đóng menu" : "Mở menu"} aria-expanded={mobileOpen} aria-controls="mg-primary-left mg-primary-right" onClick={() => setMobileOpen(value => !value)}>{mobileOpen ? <X size={22} /> : <Menu size={22} />}<span>MENU</span></button>
        <span className="mg-header-hotline">1900 0000</span>

        <div className="mg-header-side" id="mg-primary-left">
          <a href="/home" onClick={(event) => { event.preventDefault(); handleNavigate("/home"); }}>TRANG CHỦ</a>
          <a href="/booking" onClick={(event) => { event.preventDefault(); handleNavigate("/booking"); }}>ĐẶT LỊCH</a>
          <a href="/parts-shop" onClick={(event) => { event.preventDefault(); handleNavigate("/parts-shop"); }}>PART SHOP</a>
        </div>

        <button type="button"
          className="mg-header-logo cursor-pointer"
          aria-label="MY GARAGE"
          onClick={() => handleNavigate("/home")}
        >
          <img className="mg-header-brandmark" src={mediaUrl("/assets/mygarage-logo-original.svg")} alt="" aria-hidden="true" />
        </button>

        <div className="mg-header-side right" id="mg-primary-right">
          <a href="/storage" onClick={(event) => { event.preventDefault(); handleNavigate("/storage"); }}>STORAGE</a>
          <a href="/events" onClick={(event) => { event.preventDefault(); handleNavigate("/events"); }}>SỰ KIỆN</a>
          <a href="/partners" onClick={(event) => { event.preventDefault(); handleNavigate("/partners"); }}>ĐỐI TÁC</a>
          <a href="/news" onClick={(event) => { event.preventDefault(); handleNavigate("/news"); }}>TIN TỨC</a>
          <a href="/contact" onClick={(event) => { event.preventDefault(); handleNavigate("/contact"); }}>LIÊN HỆ</a>
        </div>

        {isAuthenticated ? (
          <div className="mg-header-user" ref={menuRef}>
            <button
              type="button"
              className="mg-header-notify-fixed-btn"
              onClick={() => handleNavigate("/my-notifications")}
              aria-label="Thông báo"
              title="Thông báo"
            >
              <Bell size={28} strokeWidth={2.2} />
            </button>
            <button
              type="button"
              className="mg-header-user-trigger"
              onClick={() => setMenuOpen((prev) => !prev)}
            >
              <span className="mg-header-user-name">Xin chào, {displayName}</span>
              <span className="mg-header-user-avatar" aria-hidden="true">
                {avatarUrl ? <img src={avatarUrl} alt="" /> : avatarInitial}
              </span>
            </button>
            {menuOpen && (
              <div className="mg-header-user-menu">
                <div className="mg-header-user-menu-head">Xin chào, {displayName}</div>
                {isCustomer
                  ? customerPortalLinks.map((item) => (
                    <button
                      key={item.path}
                      type="button"
                      className="mg-header-user-menu-item"
                      onClick={() => handleNavigate(item.path)}
                    >
                      {item.label}
                    </button>
                  ))
                  : null}
                <button
                  type="button"
                  className="mg-header-user-menu-item"
                  onClick={() => {
                    setMenuOpen(false);
                    setShowProfileDialog(true);
                  }}
                >
                  Hồ sơ của tôi
                </button>
                {isStaffOrAdmin && dashboardPath ? (
                  <button
                    type="button"
                    className="mg-header-user-menu-item"
                    onClick={() => handleNavigate(dashboardPath)}
                  >
                    Dashboard
                  </button>
                ) : null}
                <button
                  type="button"
                  className="mg-header-user-menu-item logout"
                  onClick={handleLogout}
                >
                  Đăng xuất
                </button>
              </div>
            )}
          </div>
        ) : (
          <a className="mg-header-login-btn" href="/login">ĐĂNG NHẬP</a>
        )}
      </div>

      {showProfileDialog ? <ProfileDialog onClose={() => setShowProfileDialog(false)} /> : null}
    </nav>
  );
}

export default Header;
