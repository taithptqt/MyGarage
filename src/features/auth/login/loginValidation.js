export function isLoginFormValid(form) {
  return form.identifier.trim().length > 0 && form.password.length >= 6;
}
