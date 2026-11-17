import { requestWithDualRoute } from '../../chat/api/chatApi.js';

export function submitCustomRequest(custom, clientMessageId, requester) {
  return requestWithDualRoute('/api/v1/car-custom-requests', '/api/car-custom-requests', {
    method: 'POST', data: { ...custom, vehicleId: custom.vehicleId ? Number(custom.vehicleId) : null, branchId: Number(custom.branchId), clientMessageId },
  }, requester);
}

export function getCustomRequests(page = 1, requester) {
  const query = `?page=${page}&pageSize=20`;
  return requestWithDualRoute(`/api/v1/car-custom-requests${query}`, `/api/car-custom-requests${query}`, { method: 'GET' }, requester);
}
