import { isValidEmail, isValidVietnamesePhone } from "@shared/utils/contactValidation";

export function isRegisterFormValid(form) {
  const email = String(form.email || "").trim();
  const phone = String(form.phone || "").trim();

  return (
    form.fullName.trim().length > 0 &&
    email.length > 0 &&
    phone.length > 0 &&
    isValidEmail(email) &&
    isValidVietnamesePhone(phone) &&
    form.password.length >= 6
  );
}
