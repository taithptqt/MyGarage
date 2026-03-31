import React, { createContext, useContext, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import * as authApi from "../features/auth/api/authApi";
import { ensureAuthorizedRole, mapAuthUserPayload } from "../features/auth/utils/authUserMapper";
import {
  clearAuthStorage,
  persistAuthUser,
  readAuthToken,
  readAuthUser,
} from "../features/auth/utils/authStorage";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(readAuthUser);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  const logout = () => {
    clearAuthStorage();
    setUser(null);
    navigate("/login");
  };

  useEffect(() => {
    const initAuth = async () => {
      const token = readAuthToken();
      if (!token) {
        setUser(null);
        setLoading(false);
        return;
      }

      try {
        const { ok, data } = await authApi.getProfile();
        if (ok && data) {
          const updatedUser = mapAuthUserPayload(data, token);
          ensureAuthorizedRole(updatedUser);
          setUser(updatedUser);
          persistAuthUser(updatedUser);
        } else if (!ok && data?.status === 401) {
          logout();
          return;
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
    return userData;
  };

  const register = async (payload) => {
    const { ok, data } = await authApi.register(payload);
    if (!ok) throw new Error(data?.message || "Đăng ký thất bại.");

    const userData = mapAuthUserPayload(data);
    ensureAuthorizedRole(userData);
    persistAuthUser(userData);
    setUser(userData);
    return userData;
  };

  const googleLogin = async (payload) => {
    const { ok, data } = await authApi.googleLogin(payload);
    if (!ok) throw new Error(data?.message || "Đăng nhập Google thất bại.");

    const userData = mapAuthUserPayload(data);
    ensureAuthorizedRole(userData);
    persistAuthUser(userData);
    setUser(userData);
    return userData;
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
