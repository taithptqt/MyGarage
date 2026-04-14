import { request } from "@app/apiClient";

async function toUploadFile(input, fallbackFileName) {
  if (input instanceof File) {
    return input;
  }

  if (input instanceof Blob) {
    return new File([input], fallbackFileName, { type: input.type || "application/octet-stream" });
  }

  if (typeof input === "string" && input.startsWith("data:")) {
    const response = await fetch(input);
    const blob = await response.blob();
    return new File([blob], fallbackFileName, { type: blob.type || "application/octet-stream" });
  }

  throw new Error("Dữ liệu tệp upload không hợp lệ.");
}

export const vehicleConditionLogApi = {
  getAll: () => request({ url: "/api/vehicle-condition-logs", method: "GET" }),
  getById: (id) => request({ url: `/api/vehicle-condition-logs/${id}`, method: "GET" }),
  create: (data) => request({ url: "/api/vehicle-condition-logs", method: "POST", data }),
  update: (id, data) => request({ url: `/api/vehicle-condition-logs/${id}`, method: "PUT", data }),
  remove: (id) => request({ url: `/api/vehicle-condition-logs/${id}`, method: "DELETE" }),
  getByServiceOrderId: (serviceOrderId) =>
    request({ url: "/api/vehicle-condition-logs", method: "GET", params: { serviceOrderId } }),
  uploadSignature: async (fileInput) => {
    const file = await toUploadFile(fileInput, "signature.png");
    const formData = new FormData();
    formData.append("file", file, file.name || "signature.png");
    return request({
      url: "/api/vehicle-condition-logs/signatures",
      method: "POST",
      data: formData,
      headers: { "Content-Type": "multipart/form-data" }
    });
  },
  uploadImage: async (fileInput) => {
    const file = await toUploadFile(fileInput, "upload-file");
    const formData = new FormData();
    formData.append("file", file, file.name || "upload-file");
    return request({
      url: "/api/vehicle-condition-logs/attachments",
      method: "POST",
      data: formData,
      headers: { "Content-Type": "multipart/form-data" }
    });
  },
};



