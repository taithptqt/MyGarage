// Positions are in the normalized vehicle frame: +X right, +Z front.
export const REPAIR_PARTS = [
  { id: 'door-right-front', label: 'Cửa trước bên phải', position: [0.94, 0.85, 0.45] },
  { id: 'door-left-front', label: 'Cửa trước bên trái', position: [-0.94, 0.85, 0.45] },
  { id: 'door-right-rear', label: 'Cửa sau bên phải', position: [0.94, 0.85, -0.7] },
  { id: 'door-left-rear', label: 'Cửa sau bên trái', position: [-0.94, 0.85, -0.7] },
  { id: 'tyre-right-front', label: 'Lốp trước bên phải', position: [0.91, 0.33, 1.6] },
  { id: 'tyre-left-front', label: 'Lốp trước bên trái', position: [-0.91, 0.33, 1.6] },
  { id: 'tyre-right-rear', label: 'Lốp sau bên phải', position: [0.91, 0.33, -1.306] },
  { id: 'tyre-left-rear', label: 'Lốp sau bên trái', position: [-0.91, 0.33, -1.306] },
  { id: 'windshield', label: 'Kính chắn gió trước', position: [0, 1.24, 0.65] },
  { id: 'rear-glass', label: 'Kính sau', position: [0, 1.2, -1.1] },
  { id: 'hood', label: 'Nắp capo', position: [0, 0.95, 1.65] },
  { id: 'trunk', label: 'Cốp sau', position: [0, 0.95, -2] },
  { id: 'bumper-front', label: 'Cản trước', position: [0, 0.5, 2.4] },
  { id: 'bumper-rear', label: 'Cản sau', position: [0, 0.5, -2.4] },
  { id: 'fender-right-front', label: 'Vè trước bên phải', position: [0.9, 0.85, 1.5] },
  { id: 'fender-left-front', label: 'Vè trước bên trái', position: [-0.9, 0.85, 1.5] },
];

export const REPAIR_COPY = {
  title: 'Báo bộ phận đang sửa trên xe 3D',
  help: 'Chọn bộ phận thực tế đang xử lý. Khách hàng sẽ thấy thông báo nối tới bộ phận này trong Garage của tôi.',
  select: 'Chọn bộ phận', note: 'Nội dung sửa chữa', placeholder: 'Ví dụ: Đang thay lốp bị thủng và cân bằng bánh xe',
  save: 'Báo đang sửa', complete: 'Đánh dấu hoàn tất', active: 'Bộ phận đang sửa',
  success: 'Đã cập nhật thông báo sửa chữa trên xe 3D.', error: 'Chưa cập nhật được thông báo sửa chữa.',
  status: 'ĐANG SỬA CHỮA', message: 'Thợ đang sửa tại bộ phận này', empty: 'Chưa có bộ phận đang sửa được KTV báo cáo.',
};
