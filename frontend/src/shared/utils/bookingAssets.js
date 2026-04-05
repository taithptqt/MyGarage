/**
 * Ảnh marketing đặt lịch: mặc định dùng file trong `public/assets/booking/`.
 * Khi deploy, upload cùng bộ file lên Azure Blob (thư mục `booking/`) và set:
 *   VITE_BOOKING_ASSETS_BASE=https://<account>.blob.core.windows.net/<container>/booking
 * (không có dấu / ở cuối). Build lại frontend sau khi đổi .env
 */
export function bookingAssetUrl(path) {
  const base = (import.meta.env.VITE_BOOKING_ASSETS_BASE || "").trim().replace(/\/$/, "");
  const p = String(path || "").trim();
  if (!p) return "";
  if (/^https?:\/\//i.test(p)) return p;
  if (!base) return p;
  const relative = p
    .replace(/^\/+assets\/booking\/?/i, "")
    .replace(/^\/+/, "");
  return `${base}/${relative}`;
}
