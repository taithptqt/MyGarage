import React, { useCallback, useEffect, useMemo, useState } from "react";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import { Modal } from "@features/admin/components/AdminModals";

const MONTH_NAVIGATION_ACTIONS = ["prev", "prev2", "next", "next2", "drillUp", "drillDown"];

export function toDateText(dateObj) {
  if (!(dateObj instanceof Date) || Number.isNaN(dateObj.getTime())) return "";
  const y = dateObj.getFullYear();
  const m = String(dateObj.getMonth() + 1).padStart(2, "0");
  const d = String(dateObj.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export function normalizeDateText(value) {
  const text = String(value || "").trim();
  if (!text) return "";
  const date = new Date(`${text}T00:00:00`);
  return Number.isNaN(date.getTime()) ? "" : toDateText(date);
}

export function formatDateInputValue(dateText) {
  const normalized = normalizeDateText(dateText);
  if (!normalized) return "";
  const [year, month, day] = normalized.split("-");
  return `${day}/${month}/${year}`;
}

function parseDateInputValue(value) {
  const raw = String(value || "").trim();
  if (!raw) return "";

  if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) return normalizeDateText(raw);

  const match = raw.match(/^(\d{1,2})[\/.-](\d{1,2})[\/.-](\d{4})$/);
  if (!match) return "";
  const day = String(Number(match[1])).padStart(2, "0");
  const month = String(Number(match[2])).padStart(2, "0");
  const year = match[3];
  return normalizeDateText(`${year}-${month}-${day}`);
}

function toMonthStartDate(dateText) {
  const normalized = normalizeDateText(dateText);
  const base = normalized ? new Date(`${normalized}T00:00:00`) : new Date();
  return new Date(base.getFullYear(), base.getMonth(), 1);
}

export function getDateRangeButtonLabel(dateFrom, dateTo, options = {}) {
  const from = normalizeDateText(dateFrom);
  const to = normalizeDateText(dateTo);
  const {
    todayDateText = "",
    emptyLabel = "Chọn khoảng ngày",
    todayLabel = "Hôm nay",
  } = options;

  if (!from && !to) return emptyLabel;
  if (from && to) {
    if (from === to) {
      if (todayDateText && from === todayDateText) return todayLabel;
      return formatDateInputValue(from);
    }
    return `${formatDateInputValue(from)} - ${formatDateInputValue(to)}`;
  }
  if (from) return `Từ ${formatDateInputValue(from)}`;
  return `Đến ${formatDateInputValue(to)}`;
}

export default function DateRangePickerModal({
  isOpen,
  onClose,
  onApply,
  initialDateFrom = "",
  initialDateTo = "",
  title = "Lọc lịch theo khoảng ngày",
  closeLabel = "Đóng",
  applyLabel = "Áp dụng",
  className = "date-range-modal",
  requireFullRange = true,
}) {
  const [draftDateFrom, setDraftDateFrom] = useState("");
  const [draftDateTo, setDraftDateTo] = useState("");
  const [draftDateFromInput, setDraftDateFromInput] = useState("");
  const [draftDateToInput, setDraftDateToInput] = useState("");
  const [calendarActiveStartDate, setCalendarActiveStartDate] = useState(() => toMonthStartDate(toDateText(new Date())));

  const syncFromProps = useCallback(() => {
    const nextFrom = normalizeDateText(initialDateFrom);
    const nextTo = normalizeDateText(initialDateTo);
    setDraftDateFrom(nextFrom);
    setDraftDateTo(nextTo);
    setDraftDateFromInput(formatDateInputValue(nextFrom));
    setDraftDateToInput(formatDateInputValue(nextTo));
    setCalendarActiveStartDate(toMonthStartDate(nextFrom || nextTo || toDateText(new Date())));
  }, [initialDateFrom, initialDateTo]);

  useEffect(() => {
    if (!isOpen) return;
    syncFromProps();
  }, [isOpen, syncFromProps]);

  const handleRangeDayClick = useCallback((dateObj) => {
    const picked = toDateText(dateObj);
    if (!picked) return;

    if (!draftDateFrom || (draftDateFrom && draftDateTo)) {
      setDraftDateFrom(picked);
      setDraftDateTo("");
      setDraftDateFromInput(formatDateInputValue(picked));
      setDraftDateToInput("");
      return;
    }

    if (picked < draftDateFrom) {
      setDraftDateTo(draftDateFrom);
      setDraftDateFrom(picked);
      setDraftDateFromInput(formatDateInputValue(picked));
      setDraftDateToInput(formatDateInputValue(draftDateFrom));
      return;
    }

    setDraftDateTo(picked);
    setDraftDateToInput(formatDateInputValue(picked));
  }, [draftDateFrom, draftDateTo]);

  const handleDraftDateFromInput = useCallback((event) => {
    const raw = event.target.value;
    setDraftDateFromInput(raw);
    const next = parseDateInputValue(raw);
    if (!next) return;
    setDraftDateFrom(next);
    if (draftDateTo && draftDateTo < next) {
      setDraftDateTo(next);
      setDraftDateToInput(formatDateInputValue(next));
    }
  }, [draftDateTo]);

  const handleDraftDateToInput = useCallback((event) => {
    const raw = event.target.value;
    setDraftDateToInput(raw);
    const next = parseDateInputValue(raw);
    if (!next) return;
    if (!draftDateFrom) {
      setDraftDateFrom(next);
      setDraftDateTo(next);
      setDraftDateFromInput(formatDateInputValue(next));
      setDraftDateToInput(formatDateInputValue(next));
      return;
    }
    if (next < draftDateFrom) {
      setDraftDateTo(draftDateFrom);
      setDraftDateFrom(next);
      setDraftDateFromInput(formatDateInputValue(next));
      setDraftDateToInput(formatDateInputValue(draftDateFrom));
      return;
    }
    setDraftDateTo(next);
    setDraftDateToInput(formatDateInputValue(next));
  }, [draftDateFrom]);

  const handleDraftDateFromBlur = useCallback(() => {
    const raw = String(draftDateFromInput || "").trim();
    if (!raw) {
      setDraftDateFrom("");
      setDraftDateTo("");
      setDraftDateFromInput("");
      setDraftDateToInput("");
      return;
    }
    const parsed = parseDateInputValue(raw);
    if (!parsed) {
      setDraftDateFromInput(formatDateInputValue(draftDateFrom));
      return;
    }
    setDraftDateFrom(parsed);
    setDraftDateFromInput(formatDateInputValue(parsed));
    if (draftDateTo && draftDateTo < parsed) {
      setDraftDateTo(parsed);
      setDraftDateToInput(formatDateInputValue(parsed));
    }
  }, [draftDateFromInput, draftDateFrom, draftDateTo]);

  const handleDraftDateToBlur = useCallback(() => {
    const raw = String(draftDateToInput || "").trim();
    if (!raw) {
      setDraftDateTo("");
      setDraftDateToInput("");
      return;
    }
    const parsed = parseDateInputValue(raw);
    if (!parsed) {
      setDraftDateToInput(formatDateInputValue(draftDateTo));
      return;
    }
    if (!draftDateFrom || parsed >= draftDateFrom) {
      setDraftDateTo(parsed);
      setDraftDateToInput(formatDateInputValue(parsed));
      return;
    }
    setDraftDateFrom(parsed);
    setDraftDateTo(draftDateFrom);
    setDraftDateFromInput(formatDateInputValue(parsed));
    setDraftDateToInput(formatDateInputValue(draftDateFrom));
  }, [draftDateToInput, draftDateTo, draftDateFrom]);

  const tileClassName = useCallback(({ date, view }) => {
    if (view !== "month") return "";
    const text = toDateText(date);
    if (!text || !draftDateFrom) return "";
    if (text === draftDateFrom) return "range-start";
    if (draftDateTo && text === draftDateTo) return "range-end";
    if (draftDateTo && text > draftDateFrom && text < draftDateTo) return "range-middle";
    return "";
  }, [draftDateFrom, draftDateTo]);

  const handleCalendarActiveStartDateChange = useCallback(({ activeStartDate, view, action }) => {
    if (view !== "month" || !activeStartDate) return;
    if (!MONTH_NAVIGATION_ACTIONS.includes(action)) return;
    setCalendarActiveStartDate(new Date(activeStartDate.getFullYear(), activeStartDate.getMonth(), 1));
  }, []);

  const handleQuickSetDateRange = useCallback((days) => {
    const now = new Date();
    const to = toDateText(now);
    const fromDate = new Date(now);
    fromDate.setDate(now.getDate() - (days - 1));
    const from = toDateText(fromDate);
    setDraftDateFrom(from);
    setDraftDateTo(to);
    setDraftDateFromInput(formatDateInputValue(from));
    setDraftDateToInput(formatDateInputValue(to));
    setCalendarActiveStartDate(toMonthStartDate(from));
  }, []);

  const handleQuickSetCurrentMonth = useCallback(() => {
    const now = new Date();
    const from = toDateText(new Date(now.getFullYear(), now.getMonth(), 1));
    const to = toDateText(new Date(now.getFullYear(), now.getMonth() + 1, 0));
    setDraftDateFrom(from);
    setDraftDateTo(to);
    setDraftDateFromInput(formatDateInputValue(from));
    setDraftDateToInput(formatDateInputValue(to));
    setCalendarActiveStartDate(toMonthStartDate(from));
  }, []);

  const handleQuickSetToday = useCallback(() => {
    const today = toDateText(new Date());
    setDraftDateFrom(today);
    setDraftDateTo(today);
    setDraftDateFromInput(formatDateInputValue(today));
    setDraftDateToInput(formatDateInputValue(today));
    setCalendarActiveStartDate(toMonthStartDate(today));
  }, []);

  const disableApplyButton = requireFullRange
    ? !draftDateFrom || !draftDateTo
    : !draftDateFrom && !draftDateTo;

  const calendarValue = useMemo(() => {
    if (draftDateFrom && draftDateTo) {
      return [new Date(`${draftDateFrom}T00:00:00`), new Date(`${draftDateTo}T00:00:00`)];
    }
    if (draftDateFrom) {
      return new Date(`${draftDateFrom}T00:00:00`);
    }
    return new Date();
  }, [draftDateFrom, draftDateTo]);

  const hintText = useMemo(() => {
    if (!draftDateFrom) return "Chọn ngày bắt đầu";
    if (draftDateFrom && !draftDateTo) {
      return "Chọn ngày kết thúc (không nhỏ hơn ngày bắt đầu)";
    }
    return "Đã chọn xong khoảng ngày";
  }, [draftDateFrom, draftDateTo]);

  const handleApply = useCallback(() => {
    const nextFrom = normalizeDateText(draftDateFrom);
    const nextTo = normalizeDateText(draftDateTo);
    if (requireFullRange && (!nextFrom || !nextTo)) return;

    if (onApply) {
      onApply({
        dateFrom: nextFrom || "",
        dateTo: nextTo || "",
      });
    }

    if (onClose) onClose();
  }, [draftDateFrom, draftDateTo, onApply, onClose, requireFullRange]);

  if (!isOpen) return null;

  return (
    <Modal title={title} onClose={onClose} className={className}>
      <div className="date-range-picker-shell">
        <div className="date-range-summary">
          <div className="date-pill">
            <span className="date-pill-label">Bắt đầu</span>
            <input
              type="text"
              className="date-pill-input"
              inputMode="numeric"
              placeholder="dd/mm/yyyy"
              value={draftDateFromInput || ""}
              onChange={handleDraftDateFromInput}
              onBlur={handleDraftDateFromBlur}
            />
          </div>
          <div className="date-pill-arrow">→</div>
          <div className="date-pill">
            <span className="date-pill-label">Kết thúc</span>
            <input
              type="text"
              className="date-pill-input"
              inputMode="numeric"
              placeholder="dd/mm/yyyy"
              value={draftDateToInput || ""}
              onChange={handleDraftDateToInput}
              onBlur={handleDraftDateToBlur}
            />
          </div>
        </div>

        <div className="date-range-hint">{hintText}</div>

        <div className="date-range-calendar-wrap">
          <Calendar
            locale="vi-VN"
            className="date-range-calendar"
            activeStartDate={calendarActiveStartDate}
            onActiveStartDateChange={handleCalendarActiveStartDateChange}
            onClickDay={handleRangeDayClick}
            tileClassName={tileClassName}
            value={calendarValue}
          />
        </div>

        <div className="date-range-quick-actions">
          <button type="button" className="date-quick-btn" onClick={handleQuickSetToday}>
            Hôm nay
          </button>
          <button type="button" className="date-quick-btn" onClick={() => handleQuickSetDateRange(7)}>
            7 ngày gần nhất
          </button>
          <button type="button" className="date-quick-btn" onClick={() => handleQuickSetDateRange(30)}>
            30 ngày gần nhất
          </button>
          <button type="button" className="date-quick-btn" onClick={handleQuickSetCurrentMonth}>
            Th�ng n�y
          </button>
        </div>

        <div className="date-range-footer">
          <button
            type="button"
            className="btn-secondary-outline"
            style={{ height: "42px", minWidth: "90px", borderRadius: "10px", padding: "0 16px" }}
            onClick={onClose}
          >
            {closeLabel}
          </button>
          <button
            type="button"
            className="btn-primary"
            style={{ height: "42px", minWidth: "110px", borderRadius: "10px", padding: "0 18px" }}
            onClick={handleApply}
            disabled={disableApplyButton}
          >
            {applyLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
}

