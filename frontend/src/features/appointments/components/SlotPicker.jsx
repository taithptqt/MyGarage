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
        <div className="empty-icon">{isSunday ? "😴" : "📅"}</div>
        <p>
          {isSunday
            ? "Chu nhat la ngay nghi. Vui long chon ngay khac (Thu 2 - Thu 7)."
            : "Khong co khung gio nao kha dung cho ngay nay."}
        </p>
      </div>
    );
  }

  function formatTime(t) {
    if (!t) return "";
    const parts = t.split(":");
    return `${parts[0]}:${parts[1]}`;
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
                ? `Con ${slot.remainingCapacity} cho`
                : "Het cho"}
            </div>
          </div>
        );
      })}
    </div>
  );
}
