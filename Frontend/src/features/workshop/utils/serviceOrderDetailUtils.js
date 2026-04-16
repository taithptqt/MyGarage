import { formatVietnameseLicensePlateInput } from "@shared/utils/licensePlateValidation";

export function formatOrderDateTime(isoString) {
  if (!isoString) return "-";
  try {
    const raw = String(isoString).trim();
    const hasTimezone = /(?:Z|[+\-]\d{2}:\d{2})$/i.test(raw);
    const normalized = hasTimezone ? raw : `${raw}Z`;
    return new Date(normalized).toLocaleString("vi-VN", {
      dateStyle: "short",
      timeStyle: "short",
      timeZone: "Asia/Ho_Chi_Minh",
    });
  } catch {
    return "-";
  }
}

export function parseCustomerNotes(notes) {
  const sanitizeText = (value) =>
    String(value || "")
      .replace(/\[[A-Z_]+:[^\]]*]/g, " ")
      .replace(/\s+/g, " ")
      .trim();

  const sanitizeRequestedServices = (value) => {
    const cleaned = sanitizeText(value);
    return cleaned && cleaned.toUpperCase() !== "N/A" ? cleaned : null;
  };

  if (!notes) {
    return {
      isAppt: false,
      mainNotes: "Không có ghi chú gì.",
      requestedServices: null,
    };
  }

  const rawNotes = String(notes).trim();

  try {
    const parsed = JSON.parse(rawNotes);
    const symptoms = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.symptoms)
        ? parsed.symptoms
        : [];
    const cleaned = symptoms.map((x) => sanitizeText(x)).filter(Boolean);
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

  const isAppt = /^\[(Lịch hẹn|Lich hen)\]/i.test(rawNotes);
  const isReception = /^\[(Tiếp nhận|Tiep nhan)\]/i.test(rawNotes);
  
  // If it doesn't have either prefix, it only has mainNotes
  if (!isAppt && !isReception) {
    // Try to extract anyway if it has "Dịch vụ yêu cầu"
    const splitToken = /(?:\.\s*)?(Dịch vụ yêu cầu|Dich vu yeu cau):\s*/i;
    const parts = rawNotes.split(splitToken);
    if (parts.length >= 3) {
      const requested = sanitizeRequestedServices(parts[2]);
      const mainNotes = sanitizeText(parts[0]);
      return {
        isAppt: false,
        mainNotes: mainNotes || "Không có ghi chú gì.",
        requestedServices: requested,
      };
    }

    const cleanedMain = sanitizeText(rawNotes);
    return {
      isAppt: false,
      mainNotes: cleanedMain || "Không có ghi chú gì.",
      requestedServices: null,
    };
  }

  // Handle prefixed notes
  const withoutPrefix = rawNotes.replace(/^\[(Lịch hẹn|Lich hen|Tiếp nhận|Tiep nhan)\]\s*/i, "");
  const splitToken = /(?:\.\s*)?(Dịch vụ yêu cầu|Dich vu yeu cau):\s*/i;
  const parts = withoutPrefix.split(splitToken);

  if (parts.length >= 3) {
    const requested = sanitizeRequestedServices(parts[2]);
    const mainNotes = sanitizeText(parts[0]);
    return {
      isAppt: isAppt,
      mainNotes: mainNotes || "Không có ghi chú gì.",
      requestedServices: requested,
    };
  }

  const cleanedWithoutPrefix = sanitizeText(withoutPrefix);
  return {
    isAppt: isAppt,
    mainNotes: cleanedWithoutPrefix || "Không có ghi chú gì.",
    requestedServices: null,
  };
}

export function calculateOrderTotal(details = []) {
  return details.reduce(
    (sum, item) => {
      const finalAmount = Number(item?.finalAmount ?? item?.FinalAmount);
      if (Number.isFinite(finalAmount) && finalAmount >= 0) {
        return sum + finalAmount;
      }

      const rawQuantity = item?.quantity ?? item?.Quantity;
      const rawUnitPrice =
        item?.unitPrice ??
        item?.UnitPrice ??
        item?.unitPriceAtTime ??
        item?.UnitPriceAtTime;
      const quantity = Number(rawQuantity ?? 1);
      const unitPrice = Number(rawUnitPrice ?? 0);
      return sum + (Number.isFinite(quantity) ? quantity : 1) * (Number.isFinite(unitPrice) ? unitPrice : 0);
    },
    0
  );
}

/**
 * Auto-format Vietnamese license plates
 * e.g., 56A56789   -> 56A-567.89   (5-digit á»‡' NNN.NN)
 *       43A1234     -> 43A-1234     (4-digit  á»‡' NNNN)
 *       30A156789   -> 30A1-567.89  (series + 5-digit)
 */
export const formatLicensePlate = (val) => {
  return formatVietnameseLicensePlateInput(val);
};

