import { requestSafe } from "@app/apiClient";
import { AUTH_ROUTES } from "@features/auth/api/authRoutes";

export async function login(payload) {
  return requestSafe({
    url: AUTH_ROUTES.login,
    method: "POST",
    data: payload,
  });
}

export async function register(payload) {
  return requestSafe({
    url: AUTH_ROUTES.register,
    method: "POST",
    data: payload,
  });
}

export async function googleLogin(payload) {
  return requestSafe({
    url: AUTH_ROUTES.google,
    method: "POST",
    data: typeof payload === "string" ? { idToken: payload } : payload,
  });
}

export async function getSession() {
  return requestSafe({
    url: AUTH_ROUTES.session,
    method: "GET",
  });
}

export async function logout() {
  return requestSafe({
    url: AUTH_ROUTES.logout,
    method: "POST",
  });
}

export async function forgotPassword(payload) {
  return requestSafe({
    url: AUTH_ROUTES.forgotPassword,
    method: "POST",
    data: payload,
  });
}

export async function resetPassword(payload) {
  return requestSafe({
    url: AUTH_ROUTES.resetPassword,
    method: "POST",
    data: payload,
  });
}

export async function changePassword(payload) {
  return requestSafe({
    url: AUTH_ROUTES.changePassword,
    method: "PATCH",
    data: payload,
  });
}
