import React, { useCallback, useEffect, useRef, useState } from "react";
import { Navigate, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import "./Workshop.css";
import { MdNotificationsNone, MdInfoOutline } from 'react-icons/md';
import { IoMdMoon, IoMdSunny } from 'react-icons/io';
import { AiOutlineSearch } from 'react-icons/ai';
import { useSearch } from "@shared/core/context/SearchContext";
import { getApiBaseUrl } from "@features/auth/api/authApi";

// Role Components are now imported in App.jsx for routing
import WorkflowTimeline from "@features/workshop/components/WorkflowTimeline";
import ProfileDialog from "@features/profile/components/ProfileDialog";
import { ChakraProvider } from "@chakra-ui/react";
import initialTheme from "@horizon/theme/theme";

const ROLES_META = {
  RECEPTIONIST: { path: "reception", label: "Lễ tân", icon: "📝", description: "Tiếp nhận, Báo giá & Thanh toán" },
  MANAGER: { path: "manager", label: "Workshop Manager", icon: "👨‍🏫", description: "Quản lý chung" },
  FOREMAN: { path: "foreman", label: "Foreman", icon: "🛡️", description: "Điều phối & Phê duyệt kỹ thuật" },
  TECH: { path: "technician", label: "Technician", icon: "🔧", description: "Thực hiện & Báo cáo" },
  QUALITY_CONTROL: { path: "qc", label: "Quality Control", icon: "✅", description: "Kiểm tra chất lượng" }
};

const WORKSHOP_SIDEBAR_EXPANDED_WIDTH = 280;
const WORKSHOP_SIDEBAR_COLLAPSED_WIDTH = 5;
const WORKSHOP_SIDEBAR_COLLAPSE_STORAGE_KEY = "mygarage.workshop.sidebar.collapsed";

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

export default function WorkshopPage() {
  const { searchQuery, setSearchQuery } = useSearch();
  const { user, isAuthenticated, loading: authLoading, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const userRole = user?.roleCode?.toUpperCase();
  const [showProfileMenu, setShowProfileMenu] = useState(false);
  const [showNotifications, setShowNotifications] = useState(false);
  const [showInfo, setShowInfo] = useState(false);
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [showProfileModal, setShowProfileModal] = useState(false);
  const [isNarrowScreen, setIsNarrowScreen] = useState(() =>
    typeof window !== "undefined" ? window.innerWidth <= 900 : false
  );
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(() => {
    if (typeof window === "undefined") return false;
    return (
      window.localStorage.getItem(WORKSHOP_SIDEBAR_COLLAPSE_STORAGE_KEY) === "1"
    );
  });
  const [sidebarWidth, setSidebarWidth] = useState(() =>
    typeof window !== "undefined" &&
      window.localStorage.getItem(WORKSHOP_SIDEBAR_COLLAPSE_STORAGE_KEY) === "1"
      ? WORKSHOP_SIDEBAR_COLLAPSED_WIDTH
      : WORKSHOP_SIDEBAR_EXPANDED_WIDTH
  );
  const [isSidebarDragging, setIsSidebarDragging] = useState(false);
  const sidebarDragStartXRef = useRef(0);
  const sidebarDragStartWidthRef = useRef(sidebarWidth);
  const currentSidebarWidthRef = useRef(sidebarWidth);
  const sidebarMovedRef = useRef(false);
  const notificationsMenuRef = useRef(null);
  const infoMenuRef = useRef(null);
  const profileMenuRef = useRef(null);
  const resolvedAvatarUrl = resolveAvatarUrl(user?.avatarUrl || user?.AvatarUrl);

  const handleLogout = () => {
    logout();
  };

  const closeHeaderPopups = useCallback(() => {
    setShowNotifications(false);
    setShowInfo(false);
    setShowProfileMenu(false);
  }, []);

  useEffect(() => {
    currentSidebarWidthRef.current = sidebarWidth;
  }, [sidebarWidth]);

  useEffect(() => {
    if (typeof window === "undefined") return undefined;
    const onResize = () => setIsNarrowScreen(window.innerWidth <= 900);
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  useEffect(() => {
    return () => {
      if (typeof document !== "undefined") {
        document.body.style.userSelect = "";
        document.body.style.cursor = "";
      }
    };
  }, []);
  useEffect(() => {
    if (!showNotifications && !showInfo && !showProfileMenu) return undefined;

    const handleOutsideInteraction = (event) => {
      const target = event.target;
      const clickedInsidePopup = [notificationsMenuRef, infoMenuRef, profileMenuRef].some(
        (ref) => ref.current && ref.current.contains(target)
      );
      if (!clickedInsidePopup) {
        closeHeaderPopups();
      }
    };

    const handleEscape = (event) => {
      if (event.key === "Escape") {
        closeHeaderPopups();
      }
    };

    document.addEventListener("mousedown", handleOutsideInteraction);
    document.addEventListener("touchstart", handleOutsideInteraction);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("mousedown", handleOutsideInteraction);
      document.removeEventListener("touchstart", handleOutsideInteraction);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [showNotifications, showInfo, showProfileMenu, closeHeaderPopups]);

  const persistWorkshopSidebarState = (collapsed) => {
    if (typeof window === "undefined") return;
    window.localStorage.setItem(
      WORKSHOP_SIDEBAR_COLLAPSE_STORAGE_KEY,
      collapsed ? "1" : "0"
    );
  };

  const applyWorkshopSidebarState = (collapsed) => {
    setIsSidebarCollapsed(collapsed);
    setSidebarWidth(
      collapsed ? WORKSHOP_SIDEBAR_COLLAPSED_WIDTH : WORKSHOP_SIDEBAR_EXPANDED_WIDTH
    );
    persistWorkshopSidebarState(collapsed);
  };

  useEffect(() => {
    if (!isSidebarDragging || isNarrowScreen) return undefined;

    const finishSidebarDrag = () => {
      if (!isSidebarDragging) return;
      const threshold =
        (WORKSHOP_SIDEBAR_COLLAPSED_WIDTH + WORKSHOP_SIDEBAR_EXPANDED_WIDTH) / 2;
      const shouldExpand = currentSidebarWidthRef.current >= threshold;
      setIsSidebarDragging(false);

      if (typeof document !== "undefined") {
        document.body.style.userSelect = "";
        document.body.style.cursor = "";
      }

      if (!sidebarMovedRef.current) {
        applyWorkshopSidebarState(!isSidebarCollapsed);
        return;
      }

      applyWorkshopSidebarState(!shouldExpand);
    };

    const handleMouseMove = (event) => {
      if ((event.buttons & 1) !== 1) {
        finishSidebarDrag();
        return;
      }
      const deltaX = event.clientX - sidebarDragStartXRef.current;
      if (Math.abs(deltaX) > 2) sidebarMovedRef.current = true;
      const nextWidth = Math.min(
        WORKSHOP_SIDEBAR_EXPANDED_WIDTH,
        Math.max(
          WORKSHOP_SIDEBAR_COLLAPSED_WIDTH,
          sidebarDragStartWidthRef.current + deltaX
        )
      );
      setSidebarWidth(nextWidth);
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", finishSidebarDrag);
    window.addEventListener("blur", finishSidebarDrag);
    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", finishSidebarDrag);
      window.removeEventListener("blur", finishSidebarDrag);
    };
  }, [isSidebarDragging, isNarrowScreen, isSidebarCollapsed]);

  const handleSidebarResizeStart = (event) => {
    if (isNarrowScreen) return;
    if (event.button !== 0) return;
    event.preventDefault();
    sidebarDragStartXRef.current = event.clientX;
    sidebarDragStartWidthRef.current = currentSidebarWidthRef.current;
    sidebarMovedRef.current = false;
    setIsSidebarDragging(true);
    if (typeof document !== "undefined") {
      document.body.style.userSelect = "none";
      document.body.style.cursor = "ew-resize";
    }
  };

  // Auto-redirect to the correct sub-route if at /workshop root
  useEffect(() => {
    if (location.pathname === "/workshop" || location.pathname === "/workshop/") {
      const target = ROLES_META[userRole]?.path;
      if (target) {
        navigate(`/workshop/${target}`, { replace: true });
      }
    }
  }, [location, userRole, navigate]);

  if (authLoading) return <div className="loading-state"><div className="spinner" /></div>;
  if (!isAuthenticated) return <Navigate to="/login" replace />;

  // Only show the role that the user belongs to, or all if Admin (optional)
  const allowedRoles = Object.entries(ROLES_META).filter(([role]) => role === userRole);

  // Determine current context for Timeline
  const getTimelineStatus = () => {
    const path = location.pathname;
    if (path.includes("/diagnosis") || path.includes("/items")) return "DIAGNOSING";
    if (path.includes("/quote") || path.includes("/manager")) return "WAITING_APPROVAL";
    if (path.includes("/condition")) return "RECEPTION";
    if (path.includes("/technician")) return "REPAIRING";
    if (path.includes("/qc")) return "QC_PENDING";
    if (path.includes("/foreman")) return "DIAGNOSING";
    if (path.includes("/reception")) return "RECEPTION";
    return "RECEPTION";
  };
  const timelineStatus = getTimelineStatus();

  const currentRoleMeta = ROLES_META[userRole];
  const roleDashboardPath = currentRoleMeta?.path ? `/workshop/${currentRoleMeta.path}` : "/workshop";
  const normalizePath = (path) => String(path || "").replace(/\/+$/, "") || "/";
  const currentNormalizedPath = normalizePath(location.pathname);
  const isStaffRoleInWorkshop = Object.prototype.hasOwnProperty.call(ROLES_META, userRole || "");
  const isInsideWorkshop = currentNormalizedPath.startsWith("/workshop");
  const shouldShowHomeAction = isStaffRoleInWorkshop && isInsideWorkshop;
  const isReceptionDashboard = userRole === "RECEPTIONIST" && location.pathname.startsWith("/workshop/reception");
  const isReceptionAppointmentsRoute = location.pathname.startsWith("/workshop/reception/appointments");
  const receptionTab = (
    isReceptionAppointmentsRoute ||
    new URLSearchParams(location.search).get("tab") === "appointments"
  ) ? "appointments" : "orders";

  const goReceptionTab = (tab) => {
    const safeTab = tab === "appointments" ? "appointments" : "orders";
    navigate(
      {
        pathname: "/workshop/reception",
        search: `?tab=${safeTab}`,
      },
      { replace: location.pathname === "/workshop/reception" }
    );
  };

  return (
    <ChakraProvider theme={initialTheme}>
      <div className={`workshop-page ${isDarkMode ? "dark" : ""}`}>
        <aside
          className={`workshop-sidebar ${isSidebarCollapsed && !isNarrowScreen ? "is-collapsed" : ""
            } ${isSidebarDragging ? "is-dragging" : ""}`}
          style={{
            width: isNarrowScreen ? undefined : `${sidebarWidth}px`,
            transition: isSidebarDragging ? "none" : "width 0.2s linear",
          }}
        >
          <div className="workshop-sidebar-viewport">
            <div className="workshop-sidebar-body">
              <div className="sidebar-header">
            <h2>Workshop</h2>
            <p>GARAGE OPERATIONS</p>
          </div>
          <nav className="role-nav">
            {allowedRoles.map(([role, meta]) => (
              <div
                key={role}
                className="role-item active"
                style={{ cursor: 'default' }}
              >
                <span className="role-icon">{meta.icon}</span>
                <div className="role-info">
                  <span className="role-label">{meta.label}</span>
                  <span className="role-desc">{meta.description}</span>
                </div>
              </div>
            ))}
            {isReceptionDashboard && (
              <div className="role-sub-nav">
                <button
                  type="button"
                  className={`role-sub-item ${receptionTab === "orders" ? "active" : ""}`}
                  onClick={() => goReceptionTab("orders")}
                >
                  Lệnh sửa chữa
                </button>
                <button
                  type="button"
                  className={`role-sub-item ${receptionTab === "appointments" ? "active" : ""}`}
                  onClick={() => goReceptionTab("appointments")}
                >
                  Lịch đặt trước
                </button>
              </div>
            )}
              </nav>
            </div>
          </div>
          {!isNarrowScreen ? (
            <div
              className="workshop-sidebar-resize-handle"
              role="separator"
              aria-label="Workshop sidebar resize handle"
              aria-orientation="vertical"
              onMouseDown={handleSidebarResizeStart}
            >
              <div
                className={`workshop-sidebar-resize-grip ${isSidebarDragging ? "dragging" : ""
                  }`}
              />
            </div>
          ) : null}
        </aside>

        <main
          className="workshop-main"
          style={{
            marginLeft: isNarrowScreen ? 0 : `${sidebarWidth}px`,
            transition: isSidebarDragging ? "none" : "margin-left 0.2s linear",
          }}
        >
          <header className="workshop-header">
            <div className="header-left">
              <div className="breadcrumb">
                <span className="bc-link">Pages</span>
                <span className="bc-separator">/</span>
                <span className="bc-current">{currentRoleMeta?.label} Dashboard</span>
              </div>
              <h1 className="header-title">{currentRoleMeta?.label} Dashboard</h1>
            </div>
            <div className="header-right">
              <div className="header-actions-bar">
                <div className="search-bar-placeholder">
                  <span className="search-icon"><AiOutlineSearch /></span>
                  <input
                    type="text"
                    placeholder="Tìm kiếm..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                  />
                </div>

                <div className="header-menu-container" ref={notificationsMenuRef}>
                  <button className="header-icon-btn" title="Thông báo" onClick={() => { setShowNotifications(!showNotifications); setShowInfo(false); setShowProfileMenu(false); }}>
                    <MdNotificationsNone />
                  </button>
                  {showNotifications && (
                    <div className="header-dropdown notification-dropdown">
                      <div className="dropdown-header">Notifications</div>
                      <div className="dropdown-content">
                        <div className="dropdown-item-info">
                          <div className="item-title">Hệ thống MyGarage</div>
                          <div className="item-desc">Chào mừng bạn trở lại làm việc hôm nay!</div>
                        </div>
                        <div className="dropdown-item-info">
                          <div className="item-title">Lệnh sửa chữa mới</div>
                          <div className="item-desc">Có 3 xe mới đang chờ tiếp nhận.</div>
                        </div>
                      </div>
                    </div>
                  )}
                </div>

                <div className="header-menu-container" ref={infoMenuRef}>
                  <button className="header-icon-btn" title="Thông tin" onClick={() => { setShowInfo(!showInfo); setShowNotifications(false); setShowProfileMenu(false); }}>
                    <MdInfoOutline />
                  </button>
                  {showInfo && (
                    <div className="header-dropdown info-dropdown">
                      <div className="dropdown-content info-content">
                        <div className="info-image-placeholder"></div>
                        <p className="info-text">Hệ thống Quản lý Garage thông quản lý mới của công ty. Chúc bạn một ngày làm việc hiệu quả!</p>
                        <button className="btn-primary-sm">Xem hướng dẫn</button>
                      </div>
                    </div>
                  )}
                </div>

                <button
                  className="header-icon-btn"
                  title="Đổi giao diện"
                  onClick={() => setIsDarkMode(!isDarkMode)}
                >
                  {isDarkMode ? <IoMdSunny /> : <IoMdMoon />}
                </button>

                <div className="profile-container" ref={profileMenuRef}>
                  <div
                    className="user-avatar"
                    title={user?.fullName}
                    onClick={() => setShowProfileMenu(!showProfileMenu)}
                    style={{
                      cursor: 'pointer',
                      backgroundImage: resolvedAvatarUrl ? `url("${resolvedAvatarUrl}")` : 'none',
                      backgroundSize: 'cover',
                      backgroundPosition: 'center'
                    }}
                  >
                    {!resolvedAvatarUrl && (user?.fullName?.split(' ').pop().substring(0, 2).toUpperCase() || 'US')}
                  </div>

                  {showProfileMenu && (
                    <div className="profile-dropdown">
                      <div className="dropdown-header">
                        👋 Xin chào, {user?.fullName?.split(' ').pop()}
                      </div>
                      <div className="dropdown-content">
                        <button className="dropdown-item" onClick={() => { setShowProfileModal(true); setShowProfileMenu(false); }}>
                          <span className="item-icon">👤</span> Hồ sơ của tôi
                        </button>
                        <button
                          className="dropdown-item"
                          onClick={() => {
                            navigate(shouldShowHomeAction ? "/home" : roleDashboardPath);
                            setShowProfileMenu(false);
                          }}
                        >
                          <span className="item-icon">{shouldShowHomeAction ? "🏠" : "📊"}</span> {shouldShowHomeAction ? "Về trang chủ" : "Dashboard"}
                        </button>
                        <button className="dropdown-item logout" onClick={handleLogout}>
                          <span className="item-icon">🚪</span> Đăng xuất
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </header>

          <section className="workshop-content">
            <WorkflowTimeline currentStatus={timelineStatus} />
            <Outlet />
          </section>
        </main>

        {showProfileModal && <ProfileDialog onClose={() => setShowProfileModal(false)} />}
      </div>
    </ChakraProvider>
  );
}

