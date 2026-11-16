export const LOCATION_COPY = {
  title: 'Garage xe của tôi', loading: 'Đang tải vị trí xe…', error: 'Chưa tải được vị trí xe. Dữ liệu đang hiển thị là lần cập nhật gần nhất.',
  unknown: 'Chưa xác nhận vị trí', noBranch: 'Xe chưa được ghi nhận đang ở chi nhánh nào.',
  diagram: 'Sơ đồ vị trí theo mã khoang thực tế. Bố trí không gian và hình xe là minh họa, không phải mặt bằng đo đạc hay model chính xác của từng xe.',
  refresh: 'Vị trí tự cập nhật mỗi 15 giây khi trang đang mở.', noParked: 'Chưa có xe được xác nhận vị trí trong chi nhánh này.',
};

export function layoutGarageBays(bays) {
  const rows = Math.ceil(bays.length / 3);
  return bays.map((bay, index) => ({ ...bay, x: (index % 3 - 1) * 4.2, z: (Math.floor(index / 3) - (rows - 1) / 2) * 7.6 }));
}
