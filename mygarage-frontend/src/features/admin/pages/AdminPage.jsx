import { useState, useEffect } from "react";
import { useAuth } from "../../../context/AuthContext";
import { AdminSidebar } from "../components/AdminSidebar";
import StaffManagement from "../../staff/pages/StaffManagement";
import ServicesPage from "../../services/pages/ServicesPage";
import ServiceCategoriesPage from "../../services/pages/ServiceCategoriesPage";
import PartsPage from "../../inventory/pages/PartsPage";
import PartCategoriesPage from "../../inventory/pages/PartCategoriesPage";
import "./AdminPage.css";

export default function AdminPage() {
  const { logout } = useAuth();
  const [activeTab, setActiveTab] = useState("staff");

  useEffect(() => {
    const titles = {
      staff: "Quản lý nhân viên | MyGarage",
      services: "Quản lý dịch vụ | MyGarage",
      "service-categories": "Danh mục dịch vụ | MyGarage",
      parts: "Quản lý phụ tùng | MyGarage",
      "part-categories": "Danh mục phụ tùng | MyGarage",
    };
    document.title = titles[activeTab] || "Quản lý | MyGarage";
  }, [activeTab]);

  function handleLogout() {
    logout();
  }

  return (
    <div className="admin-page">
      <AdminSidebar 
        activeTab={activeTab} 
        onTabChange={setActiveTab} 
        onLogout={handleLogout} 
      />

      <main className="admin-main">
        {activeTab === "staff" && <StaffManagement />}
        {activeTab === "services" && <ServicesPage />}
        {activeTab === "service-categories" && <ServiceCategoriesPage />}
        {activeTab === "parts" && <PartsPage />}
        {activeTab === "part-categories" && <PartCategoriesPage />}
      </main>
    </div>
  );
}
