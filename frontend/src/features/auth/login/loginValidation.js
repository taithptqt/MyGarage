export function isLoginFormValid(form) {
  return form.email.trim().length > 0 && form.password.length >= 6;
}
