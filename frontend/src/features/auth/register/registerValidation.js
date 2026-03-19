export function isRegisterFormValid(form) {
  return (
    form.fullName.trim().length > 0 &&
    form.phone.trim().length > 0 &&
    form.email.trim().length > 0 &&
    form.password.length >= 6
  );
}
