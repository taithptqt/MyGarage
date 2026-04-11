const TOKEN_KEY = "mygarage_token";
const USER_KEY = "mygarage_user";
const LAST_ACTIVE_KEY = "mygarage_last_active_at";

function getStorage() {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

export function readAuthUser() {
  const storage = getStorage();
  const savedUser = storage?.getItem(USER_KEY);
  if (!savedUser) return null;
  try {
    return JSON.parse(savedUser);
  } catch {
    return null;
  }
}

export function readAuthToken() {
  return getStorage()?.getItem(TOKEN_KEY) || "";
}

export function persistAuthUser(userData) {
  const storage = getStorage();
  if (!storage) return;
  storage.setItem(TOKEN_KEY, userData?.token || "");
  storage.setItem(USER_KEY, JSON.stringify(userData || null));
  storage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
}

export function clearAuthStorage() {
  const storage = getStorage();
  if (!storage) return;
  storage.removeItem(TOKEN_KEY);
  storage.removeItem(USER_KEY);
  storage.removeItem(LAST_ACTIVE_KEY);
}

export function isAuthTokenAvailable() {
  return !!readAuthToken();
}

