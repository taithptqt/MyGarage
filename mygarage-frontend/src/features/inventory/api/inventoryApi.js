import api from '../../../api/api';

export const inventoryApi = {
  getPartCategories: () => api.get('/Category'),
  createPartCategory: (data) => api.post('/Category', data),
  updatePartCategory: (id, data) => api.put(`/Category/${id}`, data),
  deletePartCategory: (id) => api.delete(`/Category/${id}`),
  
  getProducts: () => api.get('/Product'),
  createProduct: (data) => api.post('/Product', data),
  updateProduct: (id, data) => api.put(`/Product/${id}`, data),
  deleteProduct: (id) => api.delete(`/Product/${id}`),
};
