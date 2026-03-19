import React, { createContext, useContext, useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../features/auth/api/authApi';

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
          const userData = await authApi.getProfile();
          const updatedUser = { ...userData, token };
          setUser(updatedUser);
          localStorage.setItem('mygarage_user', JSON.stringify(updatedUser));
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
    const data = await authApi.login(credentials);
    const userData = {
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      token: data.token
    };
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    setUser(userData);
    return userData;
  };

  const register = async (data) => {
    const res = await authApi.register(data);
    const userData = {
      fullName: res.fullName,
      email: res.email,
      role: res.role,
      token: res.token
    };
    localStorage.setItem('mygarage_token', res.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
    setUser(userData);
    return userData;
  };

  const googleLogin = async (idToken) => {
    const data = await authApi.googleLogin(idToken);
    const userData = {
      fullName: data.fullName,
      email: data.email,
      role: data.role,
      token: data.token
    };
    localStorage.setItem('mygarage_token', data.token);
    localStorage.setItem('mygarage_user', JSON.stringify(userData));
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
