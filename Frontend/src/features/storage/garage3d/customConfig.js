export const CUSTOM_STORAGE_KEY = 'mygarage.custom-car.v1';
export const DEFAULT_CUSTOM = { paint: null, finish: 'gloss', rims: 'stock', calipers: 'stock', spoiler: 'stock',
  paintMethod: 'wrap', roof: 'stock', mirrors: 'stock', tint: 'stock', bodyKit: 'stock', wheelStyle: 'stock',
  stickers: [], accessories: [], vehicleId: '', vehicleDescription: '', notes: '', branchId: '' };
export const CUSTOM_TABS = [
  { id: 'paint', label: 'Sơn & wrap' }, { id: 'decal', label: 'Sticker / decal' },
  { id: 'exterior', label: 'Ngoại thất' }, { id: 'wheels', label: 'Mâm & phanh' }, { id: 'comfort', label: 'Tiện nghi' },
  { id: 'request', label: 'Gửi yêu cầu' },
];
export const CUSTOM_ACCESSORIES = [
  { id: 'camera360', label: 'Camera 360°' }, { id: 'dashcam', label: 'Camera hành trình' },
  { id: 'parkingSensors', label: 'Cảm biến đỗ xe' }, { id: 'audio', label: 'Nâng cấp âm thanh' },
  { id: 'ambient', label: 'Đèn viền nội thất' }, { id: 'seatLeather', label: 'Bọc ghế da' },
  { id: 'screen', label: 'Màn hình giải trí' },
];
export const STICKER_DESIGNS = [
  { id: 'stripes', label: 'Sọc racing', symbol: '▰ ▰' },
  { id: 'checker', label: 'Cờ caro', symbol: '▦' },
  { id: 'lightning', label: 'Tia chớp', symbol: 'ϟ' },
  { id: 'number', label: 'Số thi đấu', symbol: '07' },
  { id: 'text', label: 'Chữ cá nhân', symbol: 'Aa' },
];
export const STICKER_SURFACES = [
  { id: 'right', label: 'Hông phải', view: 'side' }, { id: 'left', label: 'Hông trái', view: 'left' },
  { id: 'hood', label: 'Nắp capo', view: 'front' }, { id: 'roof', label: 'Nóc xe', view: 'top' },
];
export const MAX_STICKERS = 6;
export const newSticker = (id = crypto.randomUUID()) => ({ id, design: 'stripes', surface: 'right', color: '#ffffff', text: 'MY GARAGE', x: 0, y: 0, size: 0.7, rotation: 0 });
export const CUSTOM_PRESETS = [{ id: 'stealth', label: 'Urban Stealth' }, { id: 'racing', label: 'Racing Club' }];
export function customPreset(id) {
  if (id === 'stealth') return { ...DEFAULT_CUSTOM, stickers: [], paint: '#171c24', finish: 'matte', roof: 'carbon', mirrors: 'carbon', tint: 'light', rims: 'graphite', wheelStyle: 'split', bodyKit: 'street' };
  return { ...DEFAULT_CUSTOM, paint: '#a51d32', rims: 'white', wheelStyle: 'five', calipers: 'red', spoiler: 'lip', bodyKit: 'sport',
    stickers: [{ ...newSticker(), surface: 'hood', size: 1 }, { ...newSticker(), design: 'number', text: '07', size: 0.65 }] };
}
export const CUSTOM_GROUPS = [
  { key: 'paintMethod', tab: 'paint', label: 'Phương án đổi màu', options: [
    { id: 'wrap', label: 'Dán wrap đổi màu' }, { id: 'paint', label: 'Sơn lại thân xe' },
  ] },
  { key: 'finish', tab: 'paint', label: 'Bề mặt sơn', options: [
    { id: 'gloss', label: 'Bóng' },
    { id: 'matte', label: 'Nhám satin' },
    { id: 'metallic', label: 'Ánh kim' },
  ] },
  { key: 'rims', tab: 'wheels', label: 'Màu mâm', options: [
    { id: 'stock', label: 'Nguyên bản' },
    { id: 'graphite', label: 'Đen graphite', color: '#242932' },
    { id: 'silver', label: 'Bạc sáng', color: '#c5cbd2' },
    { id: 'bronze', label: 'Đồng thể thao', color: '#ad8049' },
    { id: 'white', label: 'Trắng ceramic', color: '#f0f0ea' },
  ] },
  { key: 'calipers', tab: 'wheels', label: 'Màu cùm phanh', options: [
    { id: 'stock', label: 'Nguyên bản' },
    { id: 'red', label: 'Đỏ racing', color: '#dd243c' },
    { id: 'yellow', label: 'Vàng sport', color: '#f5c328' },
    { id: 'blue', label: 'Xanh performance', color: '#287bdc' },
  ] },
  { key: 'spoiler', tab: 'exterior', label: 'Cánh gió sau · hình học mô phỏng', options: [
    { id: 'stock', label: 'Không lắp' },
    { id: 'sport', label: 'Sport đen' },
    { id: 'lip', label: 'Lip thấp' },
  ] },
  { key: 'bodyKit', tab: 'exterior', label: 'Gói ốp ngoại thất · mô phỏng', options: [
    { id: 'stock', label: 'Nguyên bản' }, { id: 'street', label: 'Street · lip & ốp sườn' }, { id: 'sport', label: 'Sport · thêm khuếch tán sau' },
  ] },
  { key: 'roof', tab: 'paint', label: 'Phối màu nóc xe', options: [
    { id: 'stock', label: 'Theo thân xe' }, { id: 'black', label: 'Đen bóng' }, { id: 'carbon', label: 'Vân carbon mô phỏng' },
  ] },
  { key: 'mirrors', tab: 'exterior', label: 'Ốp gương', options: [
    { id: 'stock', label: 'Nguyên bản' }, { id: 'black', label: 'Đen bóng' }, { id: 'carbon', label: 'Vân carbon mô phỏng' },
  ] },
  { key: 'tint', tab: 'exterior', label: 'Phim kính · sắc độ xem trước', options: [
    { id: 'stock', label: 'Nguyên bản' }, { id: 'light', label: 'Khói nhẹ' }, { id: 'medium', label: 'Khói đậm' },
  ] },
  { key: 'wheelStyle', tab: 'wheels', label: 'Kiểu mâm · hình học mô phỏng', options: [
    { id: 'stock', label: 'Mâm theo xe' }, { id: 'five', label: '5 chấu thể thao' }, { id: 'split', label: '10 chấu thanh' },
  ] },
];
export const CUSTOM_COPY = {
  paintLabel: 'Màu thân xe',
  send: 'Gửi lựa chọn cho quản lý',
  sending: 'Đang gửi lựa chọn…',
  sent: 'Đã gửi lựa chọn cho bộ phận quản lý.',
  sendError: 'Chưa gửi được lựa chọn. Vui lòng thử lại.',
  login: 'Vui lòng đăng nhập bằng tài khoản khách hàng để gửi lựa chọn.',
  empty: 'Chọn ít nhất một thay đổi trước khi gửi.',
  requestHelp: 'Chọn xe đã lưu hoặc nhập hãng, dòng và đời xe. Garage sẽ kiểm tra vật tư, độ vừa và tính khả thi trước khi thực hiện.',
  decalHelp: 'Thêm tối đa 6 lớp. Chọn vị trí dán rồi chỉnh kích thước, xoay và dịch chuyển trên bề mặt xe.',
  accessoriesHelp: 'Các hạng mục tiện nghi được gửi để garage tư vấn độ tương thích; không thể hiện trên model ngoại thất.',
  missingVehicle: 'Vui lòng chọn xe hoặc nhập thông tin chiếc xe cần custom.',
  missingBranch: 'Vui lòng chọn chi nhánh tiếp nhận.',
  missingStickerText: 'Vui lòng nhập nội dung cho sticker chữ hoặc số thi đấu.',
  sendHelp: 'Các lựa chọn sẽ được gửi kèm tài khoản của bạn để quản lý tiếp nhận.',
  originalMode: 'ĐANG XEM XE NGUYÊN BẢN',
  customMode: 'CUSTOM PREVIEW · BẢN THỬ NGHIỆM',
  intro: 'Phối màu và phụ kiện cho chiếc Lux A2.0 của bạn.',
  disclaimer: 'Bản thử nghiệm trên một mẫu xe. Cánh gió là mô phỏng hình dáng; màu sắc và độ vừa cần kiểm tra thực tế.',
  storageError: 'Trình duyệt chưa cho phép lưu cấu hình. Bạn vẫn có thể tiếp tục thử.',
};
export function normalizeCustom(value, paints) {
  const result = { ...DEFAULT_CUSTOM, stickers: [], accessories: [] };
  if (!value || typeof value !== 'object') return result;
  if (paints.some(item => item.value === value.paint)) result.paint = value.paint;
  CUSTOM_GROUPS.forEach(group => {
    if (group.options.some(option => option.id === value[group.key])) result[group.key] = value[group.key];
  });
  result.stickers = Array.isArray(value.stickers) ? value.stickers.slice(0, MAX_STICKERS).map((item, index) => {
    const base = newSticker(`saved-${index}`);
    const number = (key, min, max) => Number.isFinite(item?.[key]) ? Math.min(max, Math.max(min, item[key])) : base[key];
    return { ...base, design: STICKER_DESIGNS.some(d => d.id === item?.design) ? item.design : base.design,
      surface: STICKER_SURFACES.some(s => s.id === item?.surface) ? item.surface : base.surface,
      color: /^#[0-9a-f]{6}$/i.test(item?.color) ? item.color : base.color,
      text: typeof item?.text === 'string' ? item.text.slice(0, 20) : base.text,
      x: number('x', -1, 1), y: number('y', -1, 1), size: number('size', 0.25, 1.2), rotation: number('rotation', -180, 180) };
  }) : [];
  for (const [key, limit] of [['vehicleDescription', 160], ['notes', 1000]]) result[key] = typeof value[key] === 'string' ? value[key].slice(0, limit) : '';
  for (const key of ['vehicleId', 'branchId']) result[key] = /^\d+$/.test(String(value[key])) ? String(value[key]) : '';
  result.accessories = CUSTOM_ACCESSORIES.filter(option => Array.isArray(value.accessories) && value.accessories.includes(option.id)).map(option => option.id);
  return result;
}
export function hasCustomChanges(custom) {
  return !!custom.paint || CUSTOM_GROUPS.some(g => g.key !== 'paintMethod' && custom[g.key] !== DEFAULT_CUSTOM[g.key]) || custom.stickers?.length > 0 || custom.accessories?.length > 0 || !!custom.notes?.trim();
}
export function customLines(custom, paints) {
  const paint = paints.find(item => item.value === custom.paint);
  return [
    ...(custom.paint ? [{ key: 'paint', label: CUSTOM_COPY.paintLabel, value: paint?.label || paints[0].label }] : []),
    ...CUSTOM_GROUPS.filter(group => group.key === 'paintMethod' ? custom.paint || custom.finish !== 'gloss' : custom[group.key] !== DEFAULT_CUSTOM[group.key]).map(group => ({
      key: group.key,
      label: group.label.split(' · ')[0],
      value: group.options.find(item => item.id === custom[group.key])?.label || group.options[0].label,
    })),
    ...(custom.stickers || []).map((sticker, index) => ({ key: sticker.id, label: `Decal ${index + 1}`,
      value: `${STICKER_DESIGNS.find(d => d.id === sticker.design)?.label} · ${STICKER_SURFACES.find(s => s.id === sticker.surface)?.label}` })),
    ...CUSTOM_ACCESSORIES.filter(option => custom.accessories?.includes(option.id)).map(option => ({ key: option.id, label: 'Tiện nghi', value: option.label })),
    ...(custom.notes?.trim() ? [{ key: 'notes', label: 'Ghi chú', value: custom.notes.trim() }] : []),
  ];
}
