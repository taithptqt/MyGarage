<<<<<<< HEAD:frontend/src/shared/components/Header.jsx
import React, { useState, useEffect } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { Search, Phone, Globe } from "lucide-react";
=======
﻿import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import ProfileDialog from "@features/profile/components/ProfileDialog";
import { Bell } from "lucide-react";
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/shared/components/Header.jsx
import "./Header.css";

const Header = ({ overlay = false }) => {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const location = useLocation();
<<<<<<< HEAD:frontend/src/shared/components/Header.jsx
=======
  const [menuOpen, setMenuOpen] = useState(false);
  const [showProfileDialog, setShowProfileDialog] = useState(false);
  const menuRef = useRef(null);

  const displayName = String(user?.fullName || user?.name || "Bạn").trim();
  const avatarUrl = resolveAvatarUrl(user?.avatarUrl || user?.AvatarUrl);
  const avatarInitial = displayName ? displayName.charAt(0).toUpperCase() : "B";
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
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
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/shared/components/Header.jsx

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 50);
    };
    window.addEventListener("scroll", handleScroll);
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  const handleLinkClick = () => {
    window.scrollTo(0, 0);
    setIsMenuOpen(false);
  };

  const leftLinks = [
    { name: "OM MY GARAGE", path: "/about" },
    { name: "STORAGE", path: "/storage" },
    { name: "SALES", path: "/sales" },
  ];

  const rightLinks = [
    { name: "COLLECTIBLES", path: "/news" }, // Mapping collectibles to news/journal for now
    { name: "PARTNERS", path: "/partners" },
    { name: "CONTACT", path: "/contact" },
  ];

  const navBackground = isScrolled
    ? "bg-primary/95 backdrop-blur-md shadow-lg"
    : overlay
    ? "bg-black/30 backdrop-blur-md"
    : "bg-primary";

  return (
<<<<<<< HEAD:frontend/src/shared/components/Header.jsx
    <nav className={`fixed top-0 left-0 right-0 z-50 w-full transition-colors duration-500 ${navBackground}`}>
      {/* UTILITY TOP BAR */}
      <div className="max-w-screen-2xl mx-auto px-6 md:px-12 py-4 flex justify-between items-center border-b border-white/5 transition-opacity duration-300">
        <div className="flex items-center space-x-8">
          <div className="flex items-center text-secondary text-[10px] font-sans tracking-[0.3em] uppercase">
            <Phone size={14} className="mr-2" />
            <span>+45 69 133 911</span>
=======
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
          <a href="#">SHOWROOM</a>
          <a href="#">TIN TỨC</a>
          <a href="#">LIÊN HỆ</a>
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
                {customerPortalLinks.map((item) => (
                  <button
                    key={item.path}
                    type="button"
                    className="mg-header-user-menu-item"
                    onClick={() => handleNavigate(item.path)}
                  >
                    {item.label}
                  </button>
                ))}
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
>>>>>>> 4918de7b76e0dd1d9f1e491629638f4aa86bb4e5:Frontend/src/shared/components/Header.jsx
          </div>
          <Link className="text-surface/60 hover:text-secondary text-[10px] font-sans uppercase tracking-[0.3em] transition-colors" to="#">ENGLISH</Link>
        </div>
        <div className="flex items-center space-x-8">
          <Link className="text-surface/60 hover:text-secondary text-[10px] font-sans uppercase tracking-[0.3em] transition-colors" to="#">FAQ</Link>
          <Link className="text-surface/60 hover:text-secondary text-[10px] font-sans uppercase tracking-[0.3em] transition-colors" to="#">JOB</Link>
          <button className="text-secondary hover:text-surface transition-colors">
            <Search size={18} />
          </button>
        </div>
      </div>

      {/* MAIN NAVIGATION */}
      <div className="max-w-screen-2xl mx-auto px-6 md:px-12 py-6 transition-all duration-500">
        <div className="flex items-center justify-between">
          {/* Left Links */}
          <div className="hidden lg:flex items-center space-x-10 w-[40%]">
            {leftLinks.map((link) => (
              <NavLink
                key={link.name}
                to={link.path}
                onClick={handleLinkClick}
                className={({ isActive }) =>
                  `text-surface/80 text-[11px] font-sans uppercase tracking-[0.4em] hover:text-secondary transition-colors whitespace-nowrap ${isActive ? "text-secondary border-b border-secondary pb-1" : ""}`
                }
              >
                {link.name}
              </NavLink>
            ))}
          </div>

          {/* Center Logo */}
          <div className="flex flex-col items-center w-full lg:w-[20%]">
            <Link to="/home" onClick={handleLinkClick} className="flex flex-col items-center group">
              <span className="text-secondary text-4xl font-serif tracking-[0.15em] leading-none italic group-hover:scale-105 transition-transform">MY</span>
              <span className="text-secondary text-[9px] font-sans tracking-[0.8em] mt-1 uppercase">GARAGE</span>
            </Link>
          </div>

          {/* Right Links */}
          <div className="hidden lg:flex justify-end items-center space-x-10 w-[40%]">
            {rightLinks.map((link) => (
              <NavLink
                key={link.name}
                to={link.path}
                onClick={handleLinkClick}
                className={({ isActive }) =>
                  `text-surface/80 text-[11px] font-sans uppercase tracking-[0.4em] hover:text-secondary transition-colors whitespace-nowrap ${isActive ? "text-secondary border-b border-secondary pb-1" : ""}`
                }
              >
                {link.name}
              </NavLink>
            ))}
          </div>

          {/* Mobile Menu Toggle */}
          <button
            onClick={() => setIsMenuOpen(!isMenuOpen)}
            className="lg:hidden text-secondary focus:outline-none"
          >
            {isMenuOpen ? "CLOSE" : "MENU"}
          </button>
        </div>
      </div>

      {/* Mobile Menu Overlay */}
      <div className={`fixed inset-0 bg-primary z-[100] transition-transform duration-700 ${isMenuOpen ? "translate-x-0" : "translate-x-full"}`}>
        <div className="h-full flex flex-col items-center justify-center space-y-8 p-12">
          {[...leftLinks, ...rightLinks].map((link) => (
            <Link
              key={link.name}
              to={link.path}
              onClick={handleLinkClick}
              className="text-surface text-2xl font-serif uppercase tracking-[0.3em] hover:text-secondary transition-colors"
            >
              {link.name}
            </Link>
          ))}
          <button
            onClick={() => setIsMenuOpen(false)}
            className="text-secondary border border-secondary px-12 py-4 text-xs tracking-widest uppercase mt-12 hover:bg-secondary hover:text-primary transition-all"
          >
            [ CLOSE ]
          </button>
        </div>
      </div>
    </nav>
  );
};

export default Header;
