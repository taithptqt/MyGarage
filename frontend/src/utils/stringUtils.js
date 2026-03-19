/**
 * Chuyển đổi tiếng Việt có dấu thành không dấu
 */
export const removeAccents = (str) => {
  if (!str) return "";
  return str
    .normalize('NFD') // Tách dấu
    .replace(/[\u0300-\u036f]/g, '') // Xóa dấu
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D');
};

/**
 * Tạo mã viết tắt từ tên (Acronym)
 * Ví dụ: "Thay dầu nhớt" -> "TDN"
 */
export const generateAcronym = (name) => {
  if (!name) return "";
  const cleanName = removeAccents(name);
  return cleanName
    .split(/\s+/)
    .filter(word => word.length > 0)
    .map(word => word[0].toUpperCase())
    .join('');
};

/**
 * Tạo slug từ tên
 * Ví dụ: "Lốp xe Michelin" -> "LOP-XE-MICHELIN"
 */
export const generateSlug = (name) => {
  if (!name) return "";
  return removeAccents(name)
    .toUpperCase()
    .replace(/[^A-Z0-9\s-]/g, '') // Chỉ giữ lại chữ cái, số và khoảng trắng
    .trim()
    .replace(/\s+/g, '-'); // Thay khoảng trắng bằng dấu gạch ngang
};
