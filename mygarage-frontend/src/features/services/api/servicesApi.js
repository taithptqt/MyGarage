import api from '../../../api/api';

export const servicesApi = {
  getServiceCategories: () => api.get('/ServiceCategory'),
  createServiceCategory: (data) => api.post('/ServiceCategory', data),
  updateServiceCategory: (id, data) => api.put(`/ServiceCategory/${id}`, data),
  deleteServiceCategory: (id) => api.delete(`/ServiceCategory/${id}`),
  
  getStandardServices: () => api.get('/StandardService'),
  createStandardService: (data) => api.post('/StandardService', data),
  updateStandardService: (id, data) => api.put(`/StandardService/${id}`, data),
  deleteStandardService: (id) => api.delete(`/StandardService/${id}`),
};
