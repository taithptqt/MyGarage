export function formatOrderDateTime(isoString) {
  if (!isoString) return "-";
  try {
    return new Date(isoString).toLocaleString("vi-VN", {
      dateStyle: "short",
      timeStyle: "short",
    });
  } catch {
    return "-";
  }
}

export function parseCustomerNotes(notes) {
  if (!notes) {
    return {
      isAppt: false,
      mainNotes: "Không có ghi chú đặc biệt.",
      requestedServices: null,
    };
  }
  try {
    const parsed = JSON.parse(String(notes).trim());
    const symptoms = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.symptoms)
        ? parsed.symptoms
        : [];
    const cleaned = symptoms.map((x) => String(x || "").trim()).filter(Boolean);
    if (cleaned.length > 0) {
      return {
        isAppt: false,
        mainNotes: cleaned.join(" | "),
        requestedServices: null,
      };
    }
  } catch {
    // Keep backward-compat format parsing below
  }
  const isAppt = /^\[(Lịch hẹn|Lich hen)\]/i.test(notes);
  const isReception = /^\[(Tiếp nhận|Tiep nhan)\]/i.test(notes);
  
  // If it doesn't have either prefix, it only has mainNotes
  if (!isAppt && !isReception) {
    // Try to extract anyway if it has "Dịch vụ yêu cầu"
    const splitToken = /(?:\.\s*)?(Dịch vụ yêu cầu|Dich vu yeu cau):\s*/i;
    const parts = notes.split(splitToken);
    if (parts.length >= 3) {
      const requested = parts[2]?.trim();
      return {
        isAppt: false,
        mainNotes: parts[0]?.trim() || "Không có ghi chú",
        requestedServices: requested && requested !== "N/A" ? requested : null,
      };
    }

    return {
      isAppt: false,
      mainNotes: notes,
      requestedServices: null,
    };
  }

  // Handle prefixed notes
  const withoutPrefix = notes.replace(/^\[(Lịch hẹn|Lich hen|Tiếp nhận|Tiep nhan)\]\s*/i, "");
  const splitToken = /(?:\.\s*)?(Dịch vụ yêu cầu|Dich vu yeu cau):\s*/i;
  const parts = withoutPrefix.split(splitToken);

  if (parts.length >= 3) {
    const requested = parts[2]?.trim();
    return {
      isAppt: isAppt,
      mainNotes: parts[0]?.trim() || "Không có ghi chú",
      requestedServices: requested && requested !== "N/A" ? requested : null,
    };
  }

  return {
    isAppt: isAppt,
    mainNotes: withoutPrefix || "Không có ghi chú",
    requestedServices: null,
  };
}

export function calculateOrderTotal(details = []) {
  return details.reduce(
    (sum, item) => sum + (Number(item.quantity || 1) * Number(item.unitPrice || 0)),
    0
  );
}

/**
 * Auto-format Vietnamese license plates
 * e.g., 56A56789   -> 56A-567.89   (5-digit ệ' NNN.NN)
 *       43A1234     -> 43A-1234     (4-digit  ệ' NNNN)
 *       30A156789   -> 30A1-567.89  (series + 5-digit)
 */
export const formatLicensePlate = (val) => {
  if (!val) return "";
  const clean = val.replace(/[^a-zA-Z0-9]/g, "").toUpperCase();

  // Lazy series digit (\d??) so regex prefers more digits in the number group
  const match = clean.match(/^(\d{2}[A-Z]{1,2})(\d??)(\d{4,5})$/);
  if (match) {
    const fullPrefix = match[1] + match[2]; // e.g. "56A" or "30A1"
    const num = match[3];                    // 4 or 5 digit number
    if (num.length === 5) {
      return `${fullPrefix}-${num.slice(0, 3)}.${num.slice(3)}`;
    }
    return `${fullPrefix}-${num}`;
  }

  // Fallback: any prefix (letters/digits) + 4-5 trailing digits
  const fallback = clean.match(/^([A-Z0-9]+?)(\d{4,5})$/);
  if (fallback) {
    const num = fallback[2];
    if (num.length === 5) return `${fallback[1]}-${num.slice(0, 3)}.${num.slice(3)}`;
    return `${fallback[1]}-${num}`;
  }

  return clean;
};





