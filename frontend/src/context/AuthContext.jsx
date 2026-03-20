import React, { createContext, useContext, useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../features/auth/api/authApi';

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const initAuth = async () => {
      const token = localStorage.getItem('mygarage_token');
      const userStr = localStorage.getItem('mygarage_user');
      
      if (token && userStr) {
        try {
          const { ok, data } = await authApi.getProfile();
          if (ok && data) {
            const updatedUser = { ...data, token };
            setUser(updatedUser);
            localStorage.setItem('mygarage_user', JSON.stringify(updatedUser));
            if (data.branchId) {
              localStorage.setItem("mygarage_branch_id", String(data.branchId));
            }
          } else {
            throw new Error("Session expired or invalid profile data");
          }
        } catch (error) {
          localStorage.removeItem('mygarage_token');
          localStorage.removeItem('mygarage_user');
          setUser(null);
        }
      }
      setLoading(false);
    };

    initAuth();
  }, []);

  const login = async (credentials) => {
    const { ok, data } = await authApi.login(credentials);
    if (!ok) throw new Error(data?.message || "Login failed");

    const userData = {
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      branchId: data.branchId || null,
      branchName: data.branchName || "",
      token: data.token
    };
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    if (data.branchId) {
      localStorage.setItem("mygarage_branch_id", String(data.branchId));
    }
    setUser(userData);
    return userData;
  };

  const register = async (payload) => {
    const { ok, data } = await authApi.register(payload);
    if (!ok) throw new Error(data?.message || "Registration failed");

    const userData = {
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      branchId: data.branchId || null,
      branchName: data.branchName || "",
      token: data.token
    };
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    if (data.branchId) {
      localStorage.setItem("mygarage_branch_id", String(data.branchId));
    }
    setUser(userData);
    return userData;
  };

  const googleLogin = async (payload) => {
    const { ok, data } = await authApi.googleLogin(payload);
    if (!ok) throw new Error(data?.message || "Google login failed");

    const userData = {
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      branchId: data.branchId || null,
      branchName: data.branchName || "",
      token: data.token
    };
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    if (data.branchId) {
      localStorage.setItem("mygarage_branch_id", String(data.branchId));
    }
    setUser(userData);
    return userData;
  };

  const logout = () => {
    localStorage.removeItem('mygarage_token');
    localStorage.removeItem('mygarage_user');
    setUser(null);
    navigate('/login');
  };

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, login, register, googleLogin, logout, loading }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
