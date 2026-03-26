const PHONE_REGEX = /^(0\d{9,10}|\+84\d{9,10})$/;

export function isRegisterFormValid(form) {
  const phone = String(form.phone || "").trim();
  return (
    form.fullName.trim().length > 0 &&
    phone.length > 0 &&
    PHONE_REGEX.test(phone) &&
    form.password.length >= 6
  );
}
