import { request } from "../../../app/apiClient";

export const vehicleConditionLogApi = {
  getAll: () => request({ url: "/api/VehicleConditionLog", method: "GET" }),
  getById: (id) => request({ url: `/api/VehicleConditionLog/${id}`, method: "GET" }),
  create: (data) => request({ url: "/api/VehicleConditionLog", method: "POST", data }),
  update: (id, data) => request({ url: `/api/VehicleConditionLog/${id}`, method: "PUT", data }),
  remove: (id) => request({ url: `/api/VehicleConditionLog/${id}`, method: "DELETE" }),
  getByServiceOrderId: (serviceOrderId) => request({ url: `/api/VehicleConditionLog/by-order/${serviceOrderId}`, method: "GET" }),
  uploadSignature: (base64Image) => request({ url: "/api/VehicleConditionLog/upload-signature", method: "POST", data: { base64Image } }),
  uploadImage: (base64Image) => request({ url: "/api/VehicleConditionLog/upload-image", method: "POST", data: { base64Image } }),
};
