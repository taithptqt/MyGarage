export function mapAuthUserPayload(data, tokenOverride = null) {
  if (!data) return null;

  return {
    id: data.id || data.Id,
    fullName: data.fullName || data.FullName,
    email: data.email || data.Email,
    phone: data.phone || data.Phone,
    role: data.role || data.Role,
    roleCode: data.roleCode || data.RoleCode,
    avatarUrl: data.avatarUrl || data.AvatarUrl,
    branchId: data.branchId || data.BranchId,
    branchName: data.branchName || data.BranchName,
    token: tokenOverride || data.token || data.Token || "",
  };
}

export function ensureAuthorizedRole(userData) {
  if (!userData?.roleCode) {
    throw new Error("Tài khoản của bạn chưa được cấp quyền truy cập hệ thống.");
  }
}

