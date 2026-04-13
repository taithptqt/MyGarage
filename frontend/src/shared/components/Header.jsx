import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import ProfileDialog from "@features/profile/components/ProfileDialog";
import "./Header.css";

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
  const [showProfileDialog, setShowProfileDialog] = useState(false);
  const menuRef = useRef(null);

  const displayName = String(user?.fullName || user?.name || "Bạn").trim();
  const avatarUrl = resolveAvatarUrl(user?.avatarUrl || user?.AvatarUrl);
  const avatarInitial = displayName ? displayName.charAt(0).toUpperCase() : "B";
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isStaffOrAdmin = ["ADMIN", "ACCOUNTANT", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER"].includes(roleCode);

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
  }, [location.pathname]);

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
    <nav className={`mg-header-wrap ${overlay ? "overlay" : "solid"}`}>
      <div className="mg-header-main">
        <span className="mg-header-hotline">+45 69 133 911</span>

        <div className="mg-header-side">
          <a href="/home" onClick={(event) => { event.preventDefault(); handleNavigate("/home"); }}>TRANG CHỦ</a>
          <a href="/booking" onClick={(event) => { event.preventDefault(); handleNavigate("/booking"); }}>ĐẶT LỊCH</a>
          <a href="/parts-shop" onClick={(event) => { event.preventDefault(); handleNavigate("/parts-shop"); }}>PART SHOP</a>
        </div>

        <div className="mg-header-logo" role="img" aria-label="MY GARAGE">
          <span>MY</span>
          <small>GARAGE</small>
        </div>

        <div className="mg-header-side right">
          <a href="/my-garage-dashboard" onClick={(event) => { event.preventDefault(); handleNavigate("/my-garage-dashboard"); }}>GARAGE</a>
          <a href="/my-vehicles" onClick={(event) => { event.preventDefault(); handleNavigate("/my-vehicles"); }}>XE CỦA TÔI</a>
          <a href="/my-notifications" onClick={(event) => { event.preventDefault(); handleNavigate("/my-notifications"); }}>THÔNG BÁO</a>
          <a href="/my-appointments" onClick={(event) => { event.preventDefault(); handleNavigate("/my-appointments"); }}>LỊCH HẸN</a>
          <a href="/my-retail-orders" onClick={(event) => { event.preventDefault(); handleNavigate("/my-retail-orders"); }}>ĐƠN PHỤ TÙNG</a>
          <a href="#">TIN TỨC</a>
          <a href="#">LIÊN HỆ</a>
        </div>

        {isAuthenticated ? (
          <div className="mg-header-user" ref={menuRef}>
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







