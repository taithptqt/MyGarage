export function setAuth(user) {
  if (!user) return;
  localStorage.setItem("mygarage_user", JSON.stringify(user));
  if (user.token) {
    localStorage.setItem("mygarage_token", user.token);
  }
}

export function clearAuth() {
  localStorage.removeItem("mygarage_token");
  localStorage.removeItem("mygarage_user");
}

export function isAuthenticated() {
  return !!localStorage.getItem("mygarage_token");
}

export function getUser() {
  const userStr = localStorage.getItem("mygarage_user");
  try {
    return userStr ? JSON.parse(userStr) : null;
  } catch (e) {
    return null;
  }
}
