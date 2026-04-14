const CANCEL_REASON_TEMPLATES = {
  AUTO_NO_SHOW_TIMEOUT: "Quá giờ hẹn, hệ thống tự động ghi nhận KHÔNG ĐẾN (NO_SHOW).",
  AUTO_NO_SHOW_LATE_ARRIVAL: "Khách đến quá ngưỡng cho phép nên hệ thống ghi nhận KHÔNG ĐẾN (NO_SHOW).",
};

function looksLikeTechnicalCode(value) {
  return /^[A-Z][A-Z0-9_]*$/.test(String(value || "").trim().toUpperCase());
}

function stripReasonMarkers(value) {
  return String(value || "")
    .replace(/\[[A-Z_]+:[^\]]*]/g, " ")
    .replace(/\[LATE_CANCEL]/gi, " ")
    .replace(/\s+/g, " ")
    .trim();
}

export function formatAppointmentCancelReason(rawReason) {
  const raw = String(rawReason || "").trim();
  if (!raw) return "";

  if (raw.toUpperCase() === "AUTO_LATE_CONFIRMATION_PENDING") {
    return "";
  }

  const normalizedRaw = raw.toUpperCase();
  if (CANCEL_REASON_TEMPLATES[normalizedRaw]) {
    return CANCEL_REASON_TEMPLATES[normalizedRaw];
  }

  if (normalizedRaw.startsWith("[LATE_CANCEL]")) {
    const detail = raw.replace(/\[LATE_CANCEL]/gi, "").trim();
    return detail ? `Hủy cận giờ hẹn: ${detail}` : "Hủy cận giờ hẹn.";
  }

  const stripped = stripReasonMarkers(raw);
  if (stripped) {
    const normalizedStripped = stripped.toUpperCase();
    if (CANCEL_REASON_TEMPLATES[normalizedStripped]) {
      return CANCEL_REASON_TEMPLATES[normalizedStripped];
    }
    if (looksLikeTechnicalCode(normalizedStripped)) {
      return "Hệ thống tự động hủy theo quy tắc vận hành.";
    }
    return stripped;
  }

  if (looksLikeTechnicalCode(normalizedRaw)) {
    return "Hệ thống tự động hủy theo quy tắc vận hành.";
  }

  return raw;
}

