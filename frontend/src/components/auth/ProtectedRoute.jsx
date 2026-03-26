import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

const ProtectedRoute = ({ children, allowedRoles }) => {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <div className="flex items-center justify-center h-screen">Loading...</div>;
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  const userRoleCode = String(user.roleCode || "").toUpperCase();
  const userRoleName = String(user.role || "").toLowerCase();

  if (allowedRoles && !allowedRoles.includes(userRoleCode)) {
    // Special fallback for ADMIN if roleCode is missing but name or email is correct
    if (allowedRoles.includes("ADMIN") && (userRoleName === "admin" || user.email === "hieuvo.23102004@gmail.com")) {
       return children;
    }

    // Customer trying to access Admin/Staff area -> send home
    if (userRoleCode === "CUSTOMER") {
        return <Navigate to="/home" replace />;
    }

    // Redirect to login if they don't have permission (force re-login for updated roles)
    return <Navigate to="/login" replace />;
  }

  return children;
};

export default ProtectedRoute;
