export default function SlotPicker({
  slots,
  selectedSlot,
  onSelect,
  loading,
  selectedDate,
  showEndTime = true,
}) {
  if (loading) {
    return (
      <div className="spinner-container">
        <div className="spinner" />
      </div>
    );
  }

  if (!slots || slots.length === 0) {
    const dateObj =
      selectedDate instanceof Date
        ? selectedDate
        : selectedDate
        ? new Date(selectedDate)
        : null;
    const isSunday = dateObj && dateObj.getDay() === 0;
    return (
      <div className="empty-state">
        <div className="empty-icon">{isSunday ? "😴" : "📅"}</div>
        <p>
          {isSunday
            ? "Chu nhat la ngay nghi. Vui long chon ngay khac (Thu 2 - Thu 7)."
            : "Khong co ca nao kha dung cho ngay nay."}
        </p>
      </div>
    );
  }

  function formatTime(t) {
    if (!t) return "";
    const parts = t.split(":");
    return `${parts[0]}:${parts[1]}`;
  }

  function shiftLabel(startTime) {
    if (!startTime) return "";
    const hour = parseInt(startTime.split(":")[0], 10);
    if (hour < 11) return "Ca sang";
    if (hour < 14) return "Ca trua";
    if (hour < 18) return "Ca chieu";
    return "Ca toi";
  }

  function shiftIcon(startTime) {
    if (!startTime) return "🕐";
    const hour = parseInt(startTime.split(":")[0], 10);
    if (hour < 11) return "🌅";
    if (hour < 14) return "☀️";
    if (hour < 18) return "🌇";
    return "🌙";
  }

  const dateObj =
    selectedDate instanceof Date
      ? selectedDate
      : selectedDate
      ? new Date(selectedDate)
      : null;

  return (
    <div className="shift-grid">
      {slots.map((slot) => {
        const key = `${slot.startTime}-${slot.endTime}`;
        const isSelected =
          selectedSlot &&
          selectedSlot.startTime === slot.startTime;

        const classes = [
          "shift-card",
          isSelected ? "shift-selected" : "",
          !slot.isAvailable ? "shift-disabled" : "",
        ]
          .filter(Boolean)
          .join(" ");

        return (
          <div
            key={key}
            className={classes}
            onClick={() => slot.isAvailable && onSelect(slot)}
          >
            <div className="shift-icon">{shiftIcon(slot.startTime)}</div>
            <div className="shift-label">{shiftLabel(slot.startTime)}</div>
            <div className="shift-time">
              {formatTime(slot.startTime)}
              {showEndTime ? ` - ${formatTime(slot.endTime)}` : ""}
            </div>
            <div className="shift-remaining">
              {slot.isAvailable
                ? `Con ${slot.remainingCapacity} cho`
                : "Het cho"}
            </div>
          </div>
        );
      })}
    </div>
  );
}
