import { getApiBaseUrl as getBaseUrl } from "@app/apiClient";

export * from "@features/auth/api/authSessionApi";
export * from "@features/auth/api/authProfileApi";

export function getApiBaseUrl() {
  return getBaseUrl();
}

