namespace MyGarage.Common;

public static class ValidationPatterns
{
    // Chấp nhận số nội địa 10 số hoặc chuẩn quốc tế tương đương +84xxxxxxxxx.
    public const string VietnamPhone10 = @"^\s*(0\d{9}|\+84\d{9})\s*$";
    public const string VietnamLicensePlate = @"^\s*(?:$|\d{2}[A-Za-z0-9]{1,2}-?\d{3}(?:\.\d{1,2})?|\d{2}[A-Za-z0-9]{1,2}-?\d{4,5})\s*$";
    public const string Vin17 = @"^\s*(?:$|[A-HJ-NPR-Z0-9]{17})\s*$";
}

public static class ValidationMessages
{
    public const string VietnamPhone10 = "Số điện thoại phải đúng định dạng Việt Nam 10 số (0xxxxxxxxx hoặc +84xxxxxxxxx).";
    public const string Email = "Email không đúng định dạng.";
    public const string VietnamLicensePlate = "Biển số xe phải đúng định dạng Việt Nam (ví dụ: 30A-12345 hoặc 30A-123.45).";
    public const string Vin17 = "Số VIN phải gồm 17 ký tự chữ in hoa và số, không chứa I, O, Q.";
}
