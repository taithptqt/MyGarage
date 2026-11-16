export const MODEL_URL = '/models/vinfast-lux-a20/model.glb';
export const BAYS = Array.from({ length: 6 }, (_, index) => ({
  id: `A${String(index + 1).padStart(2, '0')}`,
  x: (index % 3 - 1) * 4.2,
  z: index < 3 ? -3.8 : 3.8,
}));
export const PAINTS = [
  { label: 'Nguyên bản', value: null },
  { label: 'Trắng ngọc', value: '#e4e6e7' },
  { label: 'Đen huyền', value: '#171c24' },
  { label: 'Đỏ ruby', value: '#a51d32' },
  { label: 'Xanh đại dương', value: '#164d78' },
  { label: 'Bạc ánh kim', value: '#8f9aa8' },
  { label: 'Vàng đồng', value: '#b18a46' },
];
export const VIEWS = [
  { id: 'perspective', label: 'Phối cảnh', position: [5.5, 3.2, 6] },
  { id: 'front', label: 'Phía trước', position: [0, 2.2, 10] },
  { id: 'back', label: 'Phía sau', position: [0, 2.2, -10] },
  { id: 'side', label: 'Hông phải', position: [6, 1.6, 0] },
  { id: 'left', label: 'Hông trái', position: [-6, 1.6, 0] },
  { id: 'top', label: 'Từ trên', position: [0.01, 12, 0] },
  { id: 'garage', label: 'Toàn garage', position: [15, 17, 18] },
];
