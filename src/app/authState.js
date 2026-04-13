import {
  clearAuthStorage,
  isAuthTokenAvailable,
  persistAuthUser,
  readAuthUser,
} from "@features/auth/utils/authStorage";

export function setAuth(user) {
  if (!user) return;
  persistAuthUser(user);
}

export function clearAuth() {
  clearAuthStorage();
}

export function isAuthenticated() {
  return isAuthTokenAvailable();
}

export function getUser() {
  return readAuthUser();
}

