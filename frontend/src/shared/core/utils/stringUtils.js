/**
 * Remove Vietnamese accents from a string.
 */
export const removeAccents = (str) => {
  if (!str) return "";
  return str
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\u0111/g, "d")
    .replace(/\u0110/g, "D");
};

/**
 * Generate acronym from full name.
 * Example: "Thay dau nhot" -> "TDN"
 */
export const generateAcronym = (name) => {
  if (!name) return "";
  const cleanName = removeAccents(name);
  return cleanName
    .split(/\s+/)
    .filter((word) => word.length > 0)
    .map((word) => word[0].toUpperCase())
    .join("");
};

/**
 * Generate slug from name.
 * Example: "Lop xe Michelin" -> "LOP-XE-MICHELIN"
 */
export const generateSlug = (name) => {
  if (!name) return "";
  return removeAccents(name)
    .toUpperCase()
    .replace(/[^A-Z0-9\s-]/g, "")
    .trim()
    .replace(/\s+/g, "-");
};