export const VIETNAM_PHONE_PATTERN = "^(0\\d{9}|\\+84\\d{9})$";
export const VIETNAM_PHONE_REGEX = /^(0\d{9}|\+84\d{9})$/;
export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function normalizePhone(value) {
  return String(value || "").replace(/\s+/g, "").trim();
}

export function normalizeEmail(value) {
  return String(value || "").trim();
}

export function isValidVietnamesePhone(value) {
  return VIETNAM_PHONE_REGEX.test(normalizePhone(value));
}

export function isValidEmail(value) {
  return EMAIL_REGEX.test(normalizeEmail(value));
}

export function isOptionalEmailValid(value) {
  const email = normalizeEmail(value);
  if (!email) return true;
  return EMAIL_REGEX.test(email);
}
