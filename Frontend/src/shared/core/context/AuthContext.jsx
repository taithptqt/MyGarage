import React, { createContext, useContext, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import * as authApi from "@features/auth/api/authApi";
import { ensureAuthorizedRole, mapAuthUserPayload } from "@features/auth/utils/authUserMapper";
import {
  clearAuthStorage,
  persistAuthUser,
  readAuthUser,
} from "@features/auth/utils/authStorage";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(readAuthUser);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  const hydrateProfileAfterAuth = async (baseUser) => {
    try {
      const { ok, data } = await authApi.getProfile();
      if (ok && data) {
        const updatedUser = mapAuthUserPayload(data);
        ensureAuthorizedRole(updatedUser);
        setUser(updatedUser);
        persistAuthUser(updatedUser);
        return updatedUser;
      }
    } catch (error) {
      console.error("Profile hydration after auth failed:", error);
    }
    return baseUser;
  };

  const logout = async () => {
    await authApi.logout().catch(() => {});
    clearAuthStorage();
    setUser(null);
    navigate("/login");
  };

  useEffect(() => {
    const initAuth = async () => {
      try {
        const { ok, status, data } = await authApi.getSession();
        if (ok && status === 200 && data && (data.roleCode || data.RoleCode)) {
          const updatedUser = mapAuthUserPayload(data);
          ensureAuthorizedRole(updatedUser);
          setUser(updatedUser);
          persistAuthUser(updatedUser);
        } else if (status === 204 || status === 401 || (!ok && status)) {
          clearAuthStorage();
          setUser(null);
        }
      } catch (error) {
        console.error("Auth initialization failed:", error);
      }

      setLoading(false);
    };

    initAuth();
  }, []);

  const login = async (credentials) => {
    const { ok, data } = await authApi.login(credentials);
    if (!ok) throw new Error(data?.message || "Đăng nhập thất bại.");

    const userData = mapAuthUserPayload(data);
    ensureAuthorizedRole(userData);
    persistAuthUser(userData);
    setUser(userData);
    return await hydrateProfileAfterAuth(userData);
  };

  const register = async (payload) => {
    const { ok, data } = await authApi.register(payload);
    if (!ok) throw new Error(data?.message || "Đăng ký thất bại.");

    const userData = mapAuthUserPayload(data);
    ensureAuthorizedRole(userData);
    persistAuthUser(userData);
    setUser(userData);
    return await hydrateProfileAfterAuth(userData);
  };

  const googleLogin = async (payload) => {
    const { ok, data } = await authApi.googleLogin(payload);
    if (!ok) throw new Error(data?.message || "Đăng nhập Google thất bại.");

    const userData = mapAuthUserPayload(data);
    ensureAuthorizedRole(userData);
    persistAuthUser(userData);
    setUser(userData);
    return await hydrateProfileAfterAuth(userData);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        setUser,
        isAuthenticated: !!user,
        login,
        register,
        googleLogin,
        logout,
        loading,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
};



