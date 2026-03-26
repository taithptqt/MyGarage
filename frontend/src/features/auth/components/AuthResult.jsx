function AuthResult({ result, onLogout }) {
  if (!result) return null;

  return (
    <div className="success">
      <p>Đăng nhập thành công</p>
      <p>
        <strong>{result.fullName}</strong> ({result.role})
      </p>
      <p>{result.email}</p>
      <button type="button" className="ghost" onClick={onLogout}>
        Đăng xuất (client)
      </button>
    </div>
  );
}

export default AuthResult;
