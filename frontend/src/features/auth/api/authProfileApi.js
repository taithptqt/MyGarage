import { requestSafe } from "../../../app/apiClient";
import { AUTH_ROUTES } from "./authRoutes";

export async function getProfile() {
  return requestSafe({
    url: AUTH_ROUTES.me,
    method: "GET",
  });
}
