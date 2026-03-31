const TOKEN_KEY = "mygarage_token";
const USER_KEY = "mygarage_user";
const LAST_ACTIVE_KEY = "mygarage_last_active_at";

export function readAuthUser() {
  const savedUser = localStorage.getItem(USER_KEY);
  if (!savedUser) return null;
  try {
    return JSON.parse(savedUser);
  } catch {
    return null;
  }
}

export function readAuthToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function persistAuthUser(userData) {
  localStorage.setItem(TOKEN_KEY, userData?.token || "");
  localStorage.setItem(USER_KEY, JSON.stringify(userData || null));
  localStorage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
}

export function clearAuthStorage() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  localStorage.removeItem(LAST_ACTIVE_KEY);
}

export function isAuthTokenAvailable() {
  return !!readAuthToken();
}

