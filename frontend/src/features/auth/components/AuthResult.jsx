function AuthResult({ result, onLogout }) {
  if (!result) return null;

  return (
    <div className="success">
      <p>Thanh cong</p>
      <p>
        <strong>{result.fullName}</strong> ({result.role})
      </p>
      <p>{result.email}</p>
      <button type="button" className="ghost" onClick={onLogout}>
        Dang xuat (client)
      </button>
    </div>
  );
}

export default AuthResult;
