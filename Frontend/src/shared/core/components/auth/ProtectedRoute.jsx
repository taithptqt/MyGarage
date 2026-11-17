import React from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { getUnauthorizedRedirectPath, isRouteAllowed } from "@features/auth/utils/accessPolicy";

const ProtectedRoute = ({ children, allowedRoles }) => {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          minHeight: "100vh",
          width: "100%",
          background: "#f8fafc",
        }}
      >
        <div
          style={{
            width: "36px",
            height: "36px",
            border: "3px solid #e2e8f0",
            borderTopColor: "#2563eb",
            borderRadius: "50%",
            animation: "spin 0.8s linear infinite",
          }}
        />
        <style>{`@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }`}</style>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (!isRouteAllowed(user, allowedRoles)) {
    return <Navigate to={getUnauthorizedRedirectPath(user)} replace />;
  }

  return children;
};

export default ProtectedRoute;


