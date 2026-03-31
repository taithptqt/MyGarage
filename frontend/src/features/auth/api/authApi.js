import { getApiBaseUrl as getBaseUrl } from "../../../app/apiClient";

export * from "./authSessionApi";
export * from "./authProfileApi";

export function getApiBaseUrl() {
  return getBaseUrl();
}

