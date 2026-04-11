const DEFAULT_OPEN_HOUR = 8;
const DEFAULT_CLOSE_HOUR = 20;
const DEFAULT_SLOT_DURATION_MINUTES = 15;
const DEFAULT_CHECK_IN_MINUTES = 15;
const DEFAULT_BUFFER_MINUTES = 15;

function toValidDate(value) {
  if (value instanceof Date) {
    if (Number.isNaN(value.getTime())) return null;
    return new Date(value.getTime());
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function resolveWorkingWindow(options = {}) {
  const openHour = Number(options.openHour);
  const closeHour = Number(options.closeHour);

  const resolvedOpenHour = Number.isFinite(openHour) ? openHour : DEFAULT_OPEN_HOUR;
  const resolvedCloseHour = Number.isFinite(closeHour) ? closeHour : DEFAULT_CLOSE_HOUR;

  if (resolvedCloseHour <= resolvedOpenHour) {
    return { openHour: DEFAULT_OPEN_HOUR, closeHour: DEFAULT_CLOSE_HOUR };
  }

  return { openHour: resolvedOpenHour, closeHour: resolvedCloseHour };
}

export function calculateRoundedOccupancyMinutes(
  serviceMinutes,
  {
    slotDurationMinutes = DEFAULT_SLOT_DURATION_MINUTES,
    checkInMinutes = DEFAULT_CHECK_IN_MINUTES,
    bufferMinutes = DEFAULT_BUFFER_MINUTES,
  } = {}
) {
  const normalizedServiceMinutes = Number(serviceMinutes || 0);
  const safeServiceMinutes = Number.isFinite(normalizedServiceMinutes) && normalizedServiceMinutes > 0
    ? normalizedServiceMinutes
    : 0;

  const normalizedSlotDuration = Number(slotDurationMinutes || DEFAULT_SLOT_DURATION_MINUTES);
  const safeSlotDuration = Number.isFinite(normalizedSlotDuration) && normalizedSlotDuration > 0
    ? normalizedSlotDuration
    : DEFAULT_SLOT_DURATION_MINUTES;

  const safeCheckInMinutes = Number.isFinite(Number(checkInMinutes))
    ? Math.max(0, Number(checkInMinutes))
    : DEFAULT_CHECK_IN_MINUTES;
  const safeBufferMinutes = Number.isFinite(Number(bufferMinutes))
    ? Math.max(0, Number(bufferMinutes))
    : DEFAULT_BUFFER_MINUTES;

  const occupancyMinutes = safeCheckInMinutes + safeServiceMinutes + safeBufferMinutes;
  const slotCount = Math.max(1, Math.ceil(occupancyMinutes / safeSlotDuration));
  return slotCount * safeSlotDuration;
}

export function alignToWorkingWindow(startValue, options = {}) {
  const startDate = toValidDate(startValue);
  if (!startDate) return null;

  const { openHour, closeHour } = resolveWorkingWindow(options);
  const openMinutes = openHour * 60;
  const closeMinutes = closeHour * 60;
  const currentMinutes = startDate.getHours() * 60 + startDate.getMinutes();

  if (currentMinutes < openMinutes) {
    startDate.setHours(openHour, 0, 0, 0);
    return startDate;
  }

  if (currentMinutes >= closeMinutes) {
    startDate.setDate(startDate.getDate() + 1);
    startDate.setHours(openHour, 0, 0, 0);
    return startDate;
  }

  return startDate;
}

export function addMinutesWithinWorkingHours(startValue, minutesToAdd, options = {}) {
  const { openHour, closeHour } = resolveWorkingWindow(options);
  const initial = alignToWorkingWindow(startValue, { openHour, closeHour });
  if (!initial) return null;

  const normalizedMinutes = Number(minutesToAdd || 0);
  let remaining = Number.isFinite(normalizedMinutes) ? Math.max(0, Math.ceil(normalizedMinutes)) : 0;
  if (remaining <= 0) return initial;

  const cursor = new Date(initial.getTime());
  while (remaining > 0) {
    const closeToday = new Date(cursor);
    closeToday.setHours(closeHour, 0, 0, 0);

    const availableMinutes = Math.max(0, Math.floor((closeToday.getTime() - cursor.getTime()) / 60000));
    if (availableMinutes <= 0) {
      cursor.setDate(cursor.getDate() + 1);
      cursor.setHours(openHour, 0, 0, 0);
      continue;
    }

    const consume = Math.min(remaining, availableMinutes);
    cursor.setMinutes(cursor.getMinutes() + consume);
    remaining -= consume;

    if (remaining > 0) {
      cursor.setDate(cursor.getDate() + 1);
      cursor.setHours(openHour, 0, 0, 0);
    }
  }

  return cursor;
}
