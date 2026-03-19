import React from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Package, LayoutDashboard, Wrench, LogOut, User } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

const Layout = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="layout">
      <div className="sidebar">
        <div className="logo" style={{ fontSize: '24px', fontWeight: 'bold', marginBottom: '32px', color: 'var(--primary)' }}>
          MyGarage
        </div>

        {user && (
          <div style={{ 
            padding: '16px', 
            background: 'rgba(255,255,255,0.05)', 
            borderRadius: '12px', 
            marginBottom: '32px',
            display: 'flex',
            alignItems: 'center',
            gap: '12px'
          }}>
            <div style={{ width: '40px', height: '40px', background: 'var(--primary)', borderRadius: '10px', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'white' }}>
              <User size={24} />
            </div>
            <div style={{ overflow: 'hidden' }}>
              <div style={{ fontWeight: 600, color: 'white', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{user.fullName}</div>
              <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{user.role}</div>
            </div>
          </div>
        )}

        <NavLink to="/" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
          <LayoutDashboard size={20} />
          Dashboard
        </NavLink>
        <NavLink to="/services" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
          <Wrench size={20} />
          Dịch vụ
        </NavLink>
        <NavLink to="/parts" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
          <Package size={20} />
          Vật tư
        </NavLink>
        <div style={{ marginTop: 'auto' }}>
          <div className="nav-item" onClick={handleLogout} style={{ cursor: 'pointer' }}>
            <LogOut size={20} />
            Đăng xuất
          </div>
        </div>
      </div>
      <div className="main-content">
        <Outlet />
      </div>
    </div>
  );
};

export default Layout;
