import { requestWithDualRoute } from '../../chat/api/chatApi.js';

export const getMyGarageLocations = requester => requestWithDualRoute('/api/v1/customers/me/garage-locations', '/api/customers/me/garage-locations', { method: 'GET' }, requester);
export const getTechnicianLocationWorkspace = requester => requestWithDualRoute('/api/v1/technician/vehicle-locations', '/api/technician/vehicle-locations', { method: 'GET' }, requester);
export const reportVehicleLocation = (orderId, data, requester) => requestWithDualRoute(`/api/v1/technician/vehicle-locations/${orderId}`, `/api/technician/vehicle-locations/${orderId}`, { method: 'PUT', data }, requester);
export const reportVehicleRepair = (orderId, data, requester) => requestWithDualRoute(`/api/v1/technician/vehicle-repairs/${orderId}`, `/api/technician/vehicle-repairs/${orderId}`, { method: 'PUT', data }, requester);
