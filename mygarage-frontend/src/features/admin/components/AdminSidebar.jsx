import React from "react";

export function AdminSidebar({ activeTab, onTabChange, onLogout }) {

  return (
    <aside className="admin-sidebar">
      <div className="sidebar-brand">
        <span className="brand-icon">🔧</span>
        <span className="brand-name">MyGarage</span>
      </div>
      <nav className="sidebar-nav">
        <button className={`nav-item ${activeTab === 'staff' ? 'active' : ''}`} onClick={() => onTabChange('staff')}>
          <span>👥</span> Nhân viên
        </button>
        <button className={`nav-item ${activeTab === 'services' ? 'active' : ''}`} onClick={() => onTabChange('services')}>
          <span>🛠️</span> Dịch vụ
        </button>
        <button className={`nav-item ${activeTab === 'service-categories' ? 'active' : ''}`} onClick={() => onTabChange('service-categories')}>
          <span>📁</span> Danh mục dịch vụ
        </button>
        <button className={`nav-item ${activeTab === 'parts' ? 'active' : ''}`} onClick={() => onTabChange('parts')}>
          <span>📦</span> Phụ tùng
        </button>
        <button className={`nav-item ${activeTab === 'part-categories' ? 'active' : ''}`} onClick={() => onTabChange('part-categories')}>
          <span>📁</span> Danh mục phụ tùng
        </button>
        
        <div className="sidebar-footer">
          <button className="nav-item btn-logout" onClick={onLogout}>
            <span>🚪</span> Đăng xuất
          </button>
        </div>
      </nav>
    </aside>
  );
}
