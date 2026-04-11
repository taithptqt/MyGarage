import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";
import { extractField } from "@features/appointments/utils/bookingCommon";

export function buildSlotsBySession(availableSlots) {
  const base = {
    MORNING: [],
    AFTERNOON: [],
    EVENING: [],
  };

  for (const slot of availableSlots || []) {
    const key = String(extractField(slot, ["session", "Session"], "")).toUpperCase();
    if (base[key]) {
      base[key].push(slot);
    }
  }

  for (const key of Object.keys(base)) {
    base[key].sort((a, b) => {
      const at = new Date(extractField(a, ["startTime", "StartTime"])).getTime();
      const bt = new Date(extractField(b, ["startTime", "StartTime"])).getTime();
      return at - bt;
    });
  }

  return base;
}

export function buildDisplayedSlots({ slotsBySession, activeSession, bookingFlow, showOddSlots }) {
  const sessionSlots = slotsBySession?.[activeSession] || [];
  if (sessionSlots.length === 0) return [];
  const shouldExpand = Boolean(showOddSlots?.[activeSession]);
  if (shouldExpand) return sessionSlots;
  return buildCollapsedSlots(sessionSlots, bookingFlow);
}

export function resolveFirstActiveSession(sessionTabs, slotsBySession) {
  return sessionTabs.find((tab) => (slotsBySession?.[tab.key] || []).length > 0)?.key || "MORNING";
}

export function getAutoExpandOddStateForFixedFlow(sessionTabs, slotsBySession, previousState) {
  const next = { ...(previousState || {}) };
  for (const tab of sessionTabs) {
    const sessionSlots = slotsBySession?.[tab.key] || [];
    const roundedTop = sessionSlots.filter(
      (slot) =>
        extractField(slot, ["isRoundedHour", "IsRoundedHour"], false) &&
        extractField(slot, ["isTopPick", "IsTopPick"], false)
    );
    if (sessionSlots.length > 0 && roundedTop.length === 0) {
      next[tab.key] = true;
    }
  }
  return next;
}

export function buildCollapsedSlots(sessionSlots, bookingFlow = BOOKING_FLOW.FIXED) {
  if (!Array.isArray(sessionSlots) || sessionSlots.length === 0) return [];

  const roundedTopPicks = sessionSlots.filter(
    (slot) =>
      extractField(slot, ["isRoundedHour", "IsRoundedHour"], false) &&
      extractField(slot, ["isTopPick", "IsTopPick"], false)
  );
  const roundedAll = sessionSlots.filter((slot) => extractField(slot, ["isRoundedHour", "IsRoundedHour"], false));

  if (bookingFlow === BOOKING_FLOW.FLEXIBLE) {
    const preferredFlexible = roundedAll.length > 0 ? roundedAll : roundedTopPicks;
    return preferredFlexible.length > 0 ? preferredFlexible.slice(0, 5) : sessionSlots.slice(0, 5);
  }

  const preferredFixed =
    roundedTopPicks.length > 0
      ? roundedTopPicks
      : roundedAll.length > 0
        ? roundedAll
        : sessionSlots;
  return preferredFixed.slice(0, 5);
}

export function canToggleOddSlotsForSession({ slotsBySession, activeSession, bookingFlow = BOOKING_FLOW.FIXED }) {
  const sessionSlots = slotsBySession?.[activeSession] || [];
  if (!Array.isArray(sessionSlots) || sessionSlots.length === 0) return false;
  const collapsedSlots = buildCollapsedSlots(sessionSlots, bookingFlow);
  return sessionSlots.length > collapsedSlots.length;
}
