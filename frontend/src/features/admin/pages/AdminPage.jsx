import React, { useEffect, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
import "./AdminPage.css";

// Import Management Sections
import StaffManagement from "../components/StaffManagement";
import CustomerManagement from "../components/CustomerManagement";
import BrandManagement from "../components/BrandManagement";
import ModelManagement from "../components/ModelManagement";
import ServiceManagement from "../components/ServiceManagement";

const SECTION_META = {
  staff: { title: "Staff Management", subtitle: "Manage internal staff accounts and role assignment" },
  customers: { title: "Users (Customers)", subtitle: "Manage customer accounts only" },
  brands: { title: "Vehicle Brands", subtitle: "Manage car brands for service catalog" },
  models: { title: "Vehicle Models", subtitle: "Manage car models for each brand" },
  services: { title: "Service Management", subtitle: "Manage repair and maintenance service catalog" }
};

export default function AdminPage({ embedded = false, initialSection = "staff" }) {
  const { user, isAuthenticated, loading: authLoading, clearAuth } = useAuth();
  const navigate = useNavigate();
  const [section, setSection] = useState(initialSection);

  useEffect(() => {
    setSection(initialSection || "staff");
  }, [initialSection]);

  if (authLoading) {
    return (
      <div className="loading-state">
        <div className="spinner" />
        <p>Đang xác thực...</p>
      </div>
    );
  }

  if (!isAuthenticated) return <Navigate to="/login" replace />;

  const handleLogout = () => {
    clearAuth();
    navigate("/login", { replace: true });
  };

  const renderContent = () => {
    switch (section) {
      case "staff": return <StaffManagement />;
      case "customers": return <CustomerManagement />;
      case "brands": return <BrandManagement />;
      case "models": return <ModelManagement />;
      case "services": return <ServiceManagement />;
      default: return <StaffManagement />;
    }
  };

  const sectionMeta = SECTION_META[section] || SECTION_META.staff;

  return (
    <div className={`admin-page ${embedded ? "embedded" : ""}`}>
      {!embedded && (
        <aside className="admin-sidebar">
          <div className="sidebar-brand">
            <span className="brand-name">MyGarage</span>
          </div>

          <nav className="sidebar-nav">
            <button className={`nav-item ${section === "staff" ? "active" : ""}`} onClick={() => setSection("staff")}>Staff</button>
            <button className={`nav-item ${section === "customers" ? "active" : ""}`} onClick={() => setSection("customers")}>Users</button>
            <button className={`nav-item ${section === "brands" ? "active" : ""}`} onClick={() => setSection("brands")}>Vehicle Brands</button>
            <button className={`nav-item ${section === "models" ? "active" : ""}`} onClick={() => setSection("models")}>Vehicle Models</button>
            <button className={`nav-item ${section === "services" ? "active" : ""}`} onClick={() => setSection("services")}>Services</button>
            <button className="nav-item logout-btn" onClick={handleLogout}>Logout</button>
          </nav>
        </aside>
      )}

      <main className="admin-main">
        <header className="admin-header">
          <div>
            <h1>{sectionMeta.title}</h1>
            <p className="header-sub">{sectionMeta.subtitle}</p>
          </div>
        </header>

        <div className="admin-content-area">
          {renderContent()}
        </div>
      </main>
    </div>
  );
}
