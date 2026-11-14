export default function SlotPicker({ slots, selectedSlot, onSelect, loading, selectedDate }) {
  if (loading) {
    return (
      <div className="spinner-container">
        <div className="spinner" />
      </div>
    );
  }

  if (!slots || slots.length === 0) {
    const isSunday = selectedDate && selectedDate.getDay() === 0;
    return (
      <div className="empty-state">
        <div className="empty-icon">{isSunday ? "\u{1F634}" : "\u{1F4C5}"}</div>
        <p>
          {isSunday
            ? "Chủ nhật là ngày nghỉ. Vui lòng chọn ngày khác (Thứ 2 - Thứ 7)."
            : "Không có khung giờ nào khả dụng cho ngày này."}
        </p>
      </div>
    );
  }

  function formatTime(t) {
    if (!t) return "";
    const d = new Date(t);
    if (!Number.isNaN(d.getTime())) {
      return d.toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
        timeZone: "Asia/Ho_Chi_Minh",
      });
    }
    const parts = String(t).split(":");
    return `${parts[0] || ""}:${parts[1] || ""}`;
  }

  return (
    <div className="slot-grid">
      {slots.map((slot) => {
        const key = `${slot.startTime}-${slot.endTime}`;
        const isSelected =
          selectedSlot &&
          selectedSlot.startTime === slot.startTime &&
          selectedSlot.endTime === slot.endTime;

        const classes = [
          "slot-item",
          isSelected ? "slot-selected" : "",
          !slot.isAvailable ? "slot-disabled" : "",
        ]
          .filter(Boolean)
          .join(" ");

        return (
          <div
            key={key}
            className={classes}
            onClick={() => slot.isAvailable && onSelect(slot)}
          >
            <div className="slot-time">
              {formatTime(slot.startTime)} - {formatTime(slot.endTime)}
            </div>
            <div className="slot-remaining">
              {slot.isAvailable
                ? `Còn ${slot.remainingCapacity} chđ-`
                : "Hết chđ-"}
            </div>
          </div>
        );
      })}
    </div>
  );
}




