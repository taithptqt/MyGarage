function pickText(source, keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null) return String(value);
  }
  return "";
}

function pickNullable(source, keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null && value !== "") return value;
  }
  return null;
}

export function mapAuthUserPayload(data) {
  if (!data) return null;

  const role = pickText(data, ["role", "Role"]).trim();
  const roleCode = pickText(data, ["roleCode", "RoleCode"]).trim();
  const isStaff = roleCode !== "CUSTOMER" && role.toUpperCase() === "STAFF";

  return {
    id: pickNullable(data, ["id", "Id"]),
    fullName: pickText(data, ["fullName", "FullName"]),
    email: pickText(data, ["email", "Email"]),
    phone: pickText(data, ["phone", "Phone", "phoneNumber", "PhoneNumber", "mobile", "Mobile"]),
    role,
    roleCode,
    avatarUrl: pickText(data, ["avatarUrl", "AvatarUrl"]),
    branchId: pickNullable(data, ["branchId", "BranchId"]),
    branchName: pickText(data, ["branchName", "BranchName"]),
    citizenId: isStaff ? pickText(data, ["citizenId", "CitizenId"]) : "",
    dateOfBirth: isStaff ? pickText(data, ["dateOfBirth", "DateOfBirth"]) : "",
  };
}

export function ensureAuthorizedRole(userData) {
  if (!userData?.roleCode) {
    throw new Error("Tài khoản của bạn chưa được cấp quyền truy cập hệ thống.");
  }
}

