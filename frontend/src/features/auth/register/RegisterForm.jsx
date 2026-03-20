const VIETNAM_PROVINCES = [
  "Ha Noi",
  "TP HCM",
  "Hai Phong",
  "Da Nang",
  "Can Tho",
  "An Giang",
  "Ba Ria - Vung Tau",
  "Bac Giang",
  "Bac Kan",
  "Bac Lieu",
  "Bac Ninh",
  "Ben Tre",
  "Binh Dinh",
  "Binh Duong",
  "Binh Phuoc",
  "Binh Thuan",
  "Ca Mau",
  "Cao Bang",
  "Dak Lak",
  "Dak Nong",
  "Dien Bien",
  "Dong Nai",
  "Dong Thap",
  "Gia Lai",
  "Ha Giang",
  "Ha Nam",
  "Ha Tinh",
  "Hai Duong",
  "Hau Giang",
  "Hoa Binh",
  "Hung Yen",
  "Khanh Hoa",
  "Kien Giang",
  "Kon Tum",
  "Lai Chau",
  "Lam Dong",
  "Lang Son",
  "Lao Cai",
  "Long An",
  "Nam Dinh",
  "Nghe An",
  "Ninh Binh",
  "Ninh Thuan",
  "Phu Tho",
  "Phu Yen",
  "Quang Binh",
  "Quang Nam",
  "Quang Ngai",
  "Quang Ninh",
  "Quang Tri",
  "Soc Trang",
  "Son La",
  "Tay Ninh",
  "Thai Binh",
  "Thai Nguyen",
  "Thanh Hoa",
  "Thua Thien Hue",
  "Tien Giang",
  "Tra Vinh",
  "Tuyen Quang",
  "Vinh Long",
  "Vinh Phuc",
  "Yen Bai"
];

function RegisterForm({ form, onChange, onSubmit, isValid, loading }) {
  return (
    <form onSubmit={onSubmit} className="form">
      <label htmlFor="register-name">Ho ten</label>
      <input
        id="register-name"
        type="text"
        value={form.fullName}
        onChange={(e) => onChange("fullName", e.target.value)}
        placeholder="Nguyen Van A"
        required
      />

      <label htmlFor="register-phone">So dien thoai</label>
      <input
        id="register-phone"
        type="tel"
        value={form.phone}
        onChange={(e) => onChange("phone", e.target.value)}
        placeholder="0900000000"
        required
      />

      <div className="city-select-field">
        <label htmlFor="register-city">Tinh/Thanh pho</label>
        <div className="city-select-wrap">
          <span className="city-select-icon" aria-hidden="true">📍</span>
          <select
            id="register-city"
            value={form.city}
            onChange={(e) => onChange("city", e.target.value)}
            required
          >
            <option value="">Chon tinh/thanh pho</option>
            {VIETNAM_PROVINCES.map((province) => (
              <option key={province} value={province}>
                {province}
              </option>
            ))}
          </select>
        </div>
        <p className="city-select-hint">He thong se tu dong gan chi nhanh theo khu vuc mien.</p>
      </div>

      <label htmlFor="register-email">Email</label>
      <input
        id="register-email"
        type="email"
        value={form.email}
        onChange={(e) => onChange("email", e.target.value)}
        placeholder="user@example.com"
        autoComplete="email"
        required
      />

      <label htmlFor="register-password">Mat khau</label>
      <input
        id="register-password"
        type="password"
        value={form.password}
        onChange={(e) => onChange("password", e.target.value)}
        placeholder="Toi thieu 6 ky tu"
        autoComplete="new-password"
        required
        minLength={6}
      />

      <button type="submit" disabled={loading || !isValid}>
        {loading ? "Dang xu ly..." : "Dang ky"}
      </button>
    </form>
  );
}

export default RegisterForm;
