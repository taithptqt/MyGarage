const STATUS_LABELS_VI = Object.freeze({
  ALL: "T\u1ea5t c\u1ea3",
  PENDING: "Ch\u1edd x\u00e1c nh\u1eadn",
  PARTS_PENDING: "Ch\u1edd x\u1eed l\u00fd ph\u1ee5 t\u00f9ng",
  LATE: "Tr\u1ec5 h\u1eb9n",
  QUEUED: "X\u1ebfp h\u00e0ng",
  CONFIRMED: "\u0110\u00e3 x\u00e1c nh\u1eadn",
  PARTIAL_CONFIRMED: "X\u00e1c nh\u1eadn m\u1ed9t ph\u1ea7n",
  ARRIVED: "\u0110\u00e3 \u0111\u1ebfn",
  ARRIVED_LATE: "\u0110\u1ebfn tr\u1ec5",
  CHECKED_IN: "\u0110\u00e3 check-in",
  DIAGNOSING: "\u0110ang chu\u1ea9n \u0111o\u00e1n",
  IN_PROGRESS: "\u0110ang x\u1eed l\u00fd",
  WAITING_APPROVAL: "Ch\u1edd b\u00e1o gi\u00e1",
  READY: "S\u1eb5n s\u00e0ng b\u00e0n giao",
  COMPLETED: "Ho\u00e0n t\u1ea5t",
  CANCELED: "\u0110\u00e3 h\u1ee7y",
  CANCELED: "\u0110\u00e3 h\u1ee7y",
  NO_SHOW: "Kh\u00f4ng \u0111\u1ebfn",
  NEW: "M\u1edbi t\u1ea1o",
  RECEPTION: "Ti\u1ebfp nh\u1eadn",
  APPROVED: "\u0110\u00e3 duy\u1ec7t",
  ESCALATED: "\u0110\u00e3 chuy\u1ec3n c\u1ea5p qu\u1ea3n \u0111\u1ed1c",
  BYPASSED: "\u0110\u00e3 duy\u1ec7t v\u01b0\u1ee3t c\u1ea5p",
  SENT_TO_CUSTOMER: "\u0110\u00e3 g\u1eedi kh\u00e1ch h\u00e0ng",
  PENDING_INSURANCE: "Ch\u1edd b\u1ea3o hi\u1ec3m duy\u1ec7t",
  INSURANCE_APPROVED: "B\u1ea3o hi\u1ec3m \u0111\u00e3 duy\u1ec7t",
  PENDING_CUSTOMER: "Ch\u1edd kh\u00e1ch x\u00e1c nh\u1eadn",
  CUSTOMER_APPROVED: "Kh\u00e1ch \u0111\u1ed3ng \u00fd",
  CUSTOMER_REJECTED: "Kh\u00e1ch t\u1eeb ch\u1ed1i",
  SUPERSEDED: "\u0110\u00e3 b\u1ecb thay th\u1ebf",
  CANCELLED: "\u0110\u00e3 h\u1ee7y",
  QUOTING: "\u0110ang b\u00e1o gi\u00e1",
  REJECTED: "T\u1eeb ch\u1ed1i",
  DRAFT: "Nh\u00e1p",
  PAID: "\u0110\u00e3 thanh to\u00e1n",
  LOCKED: "\u0110\u00e3 kh\u00f3a",
});

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

export function getStatusLabelVi(status, fallback = "-") {
  const normalized = normalizeStatus(status);
  if (!normalized) return fallback;
  return STATUS_LABELS_VI[normalized] || String(status);
}

export function toStatusOptionVi(status) {
  const value = String(status || "").trim();
  return {
    value,
    label: getStatusLabelVi(value, value || "-"),
  };
}

