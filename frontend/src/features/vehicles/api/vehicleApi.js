import { api } from "../../../utils/apiClient";

// ── VehicleBrand ────────────────────────────────────────────────────────────
export const brandApi = {
  getAll: ()          => api.get("/api/VehicleBrand"),
  getById: (id)       => api.get(`/api/VehicleBrand/${id}`),
  create: (payload)   => api.post("/api/VehicleBrand", payload),
  update: (id, payload) => api.put(`/api/VehicleBrand/${id}`, payload),
  remove: (id)        => api.delete(`/api/VehicleBrand/${id}`),
};

// ── VehicleModel ─────────────────────────────────────────────────────────────
export const modelApi = {
  getAll: ()          => api.get("/api/VehicleModel"),
  getById: (id)       => api.get(`/api/VehicleModel/${id}`),
  create: (payload)   => api.post("/api/VehicleModel", payload),
  update: (id, payload) => api.put(`/api/VehicleModel/${id}`, payload),
  remove: (id)        => api.delete(`/api/VehicleModel/${id}`),
};

export const FUEL_TYPES = ["GASOLINE", "DIESEL", "ELECTRIC", "HYBRID"];
export const FUEL_LABELS = { GASOLINE: "Xăng", DIESEL: "Dầu", ELECTRIC: "Điện", HYBRID: "Hybrid" };

export const SEGMENTS = ["A", "B", "C", "D", "SUV", "MPV", "PICKUP"];
