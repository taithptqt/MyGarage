import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import AuthPage from './features/auth/pages/AuthPage';
import AdminPage from './features/admin/pages/AdminPage';
import HomePage from './features/home/pages/HomePage';

const ProtectedRoute = ({ children, role }) => {
  const { user, isAuthenticated, loading } = useAuth();
  
  if (loading) return <div style={{ display: 'flex', height: '100vh', alignItems: 'center', justifyContent: 'center' }}>Đang tải...</div>;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  
  if (role && user?.role !== role) {
    return <Navigate to={user?.role === "Staff" ? "/admin" : "/home"} replace />;
  }
  
  return <>{children}</>;
};

const RootRedirect = () => {
  const { user, isAuthenticated, loading } = useAuth();
  
  if (loading) return null;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  
  return <Navigate to={user?.role === 'Staff' ? '/admin' : '/home'} replace />;
};

function App() {
  return (
    <Router>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<AuthPage />} />
          <Route path="/register" element={<AuthPage />} />
          
          {/* Admin / Staff Routes */}
          <Route path="/admin" element={<ProtectedRoute role="Staff"><AdminPage /></ProtectedRoute>} />
          
          {/* Customer Routes */}
          <Route path="/home" element={<ProtectedRoute role="Customer"><HomePage /></ProtectedRoute>} />

          <Route path="/" element={<RootRedirect />} />
          
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </AuthProvider>
    </Router>
  );
}

export default App;
