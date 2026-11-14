const LEGACY_TOKEN_KEYS = ["mygarage_token", "token"];
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

function purgeLegacyTokens(storage) {
  if (!storage) return;
  for (const key of LEGACY_TOKEN_KEYS) {
    storage.removeItem(key);
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

export function persistAuthUser(userData) {
  const storage = getStorage();
  if (!storage) return;
  purgeLegacyTokens(storage);
  if (!userData || typeof userData !== "object") {
    storage.setItem(USER_KEY, JSON.stringify(null));
  } else {
    const { token: _token, Token: _upperToken, ...safeUser } = userData;
    storage.setItem(USER_KEY, JSON.stringify(safeUser));
  }
  storage.setItem(LAST_ACTIVE_KEY, String(Date.now()));
}

export function clearAuthStorage() {
  const storage = getStorage();
  if (!storage) return;
  purgeLegacyTokens(storage);
  storage.removeItem(USER_KEY);
  storage.removeItem(LAST_ACTIVE_KEY);
}

export function isAuthTokenAvailable() {
  return !!readAuthUser()?.roleCode;
}

