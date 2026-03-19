/**
 * Chuyển đổi tiếng Việt có dấu thành không dấu
 */
export const removeAccents = (str) => {
  return str
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
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
    .replace(/[^A-Z0-9\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-');
};
