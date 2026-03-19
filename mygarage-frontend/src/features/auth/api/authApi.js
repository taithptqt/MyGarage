import apiClient from "../../../api/api";

export function getApiBaseUrl() {
  return apiClient.defaults.baseURL || window.location.origin;
}

export const authApi = {
  login: async (credentials) => {
    const response = await apiClient.post('Auth/login', credentials);
    return response.data;
  },
  register: async (data) => {
    const response = await apiClient.post('Auth/register', data);
    return response.data;
  },
  googleLogin: async (idToken) => {
    const response = await apiClient.post('Auth/google', { idToken });
    return response.data;
  },
  getProfile: async () => {
    const response = await apiClient.get('Auth/me');
    return response.data;
  }
};
