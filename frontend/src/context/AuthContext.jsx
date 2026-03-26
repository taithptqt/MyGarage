import React, { createContext, useContext, useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import * as authApi from '../features/auth/api/authApi';

const AuthContext = createContext();
const LAST_ACTIVE_KEY = 'mygarage_last_active_at';

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(() => {
    // Optimistic initialization from localStorage
    const savedUser = localStorage.getItem('mygarage_user');
    try {
      return savedUser ? JSON.parse(savedUser) : null;
    } catch (e) {
      return null;
    }
  });
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const initAuth = async () => {
      const token = localStorage.getItem('mygarage_token');
      
      if (token) {
        try {
          const { ok, data } = await authApi.getProfile();
          if (ok && data) {
            const updatedUser = { 
              ...data, 
              id: data.id || data.Id,
              token, 
              roleCode: data.roleCode || data.RoleCode,
              branchId: data.branchId || data.BranchId,
              branchName: data.branchName || data.BranchName
            };
            if (!updatedUser.roleCode) {
              logout();
              return;
            }
            setUser(updatedUser);
            localStorage.setItem('mygarage_user', JSON.stringify(updatedUser));
            localStorage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
          } else if (!ok && data?.status === 401) {
            // Only clear on definitive unauthorized error
            logout();
          }
        } catch (error) {
          console.error("Auth initialization failed:", error);
          // Don't log out on network errors
        }
      } else {
        setUser(null);
      }
      setLoading(false);
    };

    initAuth();
  }, []);

  const login = async (credentials) => {
    const { ok, data } = await authApi.login(credentials);
    if (!ok) throw new Error(data?.message || "Login failed");

    const userData = {
      id: data.id || data.Id,
      fullName: data.fullName,
      email: data.email,
      phone: data.phone,
      role: data.role,
      roleCode: data.roleCode || data.RoleCode,
      avatarUrl: data.avatarUrl || data.AvatarUrl,
      branchId: data.branchId || data.BranchId,
      branchName: data.branchName || data.BranchName,
      token: data.token
    };
    if (!userData.roleCode) throw new Error("Tài khoản của bạn chưa được cấp quyền truy cập hệ thống.");
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    localStorage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
    setUser(userData);
    return userData;
  };

  const register = async (payload) => {
    const { ok, data } = await authApi.register(payload);
    if (!ok) throw new Error(data?.message || "Registration failed");

    const userData = {
      id: data.id || data.Id,
      fullName: data.fullName,
      email: data.email,
      phone: data.phone,
      role: data.role,
      roleCode: data.roleCode || data.RoleCode,
      avatarUrl: data.avatarUrl || data.AvatarUrl,
      branchId: data.branchId || data.BranchId,
      branchName: data.branchName || data.BranchName,
      token: data.token
    };
    if (!userData.roleCode) throw new Error("Tài khoản của bạn chưa được cấp quyền truy cập hệ thống.");
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    localStorage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
    setUser(userData);
    return userData;
  };

  const googleLogin = async (payload) => {
    const { ok, data } = await authApi.googleLogin(payload);
    if (!ok) throw new Error(data?.message || "Google login failed");

    const userData = {
      id: data.id || data.Id,
      fullName: data.fullName,
      email: data.email,
      phone: data.phone,
      role: data.role,
      roleCode: data.roleCode || data.RoleCode,
      avatarUrl: data.avatarUrl || data.AvatarUrl,
      branchId: data.branchId || data.BranchId,
      branchName: data.branchName || data.BranchName,
      token: data.token
    };
    if (!userData.roleCode) throw new Error("Tài khoản của bạn chưa được cấp quyền truy cập hệ thống.");
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    localStorage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
    setUser(userData);
    return userData;
  };

  const logout = () => {
    localStorage.removeItem('mygarage_token');
    localStorage.removeItem('mygarage_user');
    localStorage.removeItem(LAST_ACTIVE_KEY);
    setUser(null);
    navigate('/login');
  };

  return (
    <AuthContext.Provider value={{ user, setUser, isAuthenticated: !!user, login, register, googleLogin, logout, loading }}>
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
