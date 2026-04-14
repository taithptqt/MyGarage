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
  if (!notes) {
    return {
      isAppt: false,
      mainNotes: "KhÃ´ng cÃ³ ghi chÃº Ä‘áº·c biá»‡t.",
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
  const isAppt = /^\[(Lá»‹ch háº¹n|Lich hen)\]/i.test(notes);
  const isReception = /^\[(Tiáº¿p nháº­n|Tiep nhan)\]/i.test(notes);
  
  // If it doesn't have either prefix, it only has mainNotes
  if (!isAppt && !isReception) {
    // Try to extract anyway if it has "Dá»‹ch vá»¥ yÃªu cáº§u"
    const splitToken = /(?:\.\s*)?(Dá»‹ch vá»¥ yÃªu cáº§u|Dich vu yeu cau):\s*/i;
    const parts = notes.split(splitToken);
    if (parts.length >= 3) {
      const requested = parts[2]?.trim();
      return {
        isAppt: false,
        mainNotes: parts[0]?.trim() || "KhÃ´ng cÃ³ ghi chÃº",
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
  const withoutPrefix = notes.replace(/^\[(Lá»‹ch háº¹n|Lich hen|Tiáº¿p nháº­n|Tiep nhan)\]\s*/i, "");
  const splitToken = /(?:\.\s*)?(Dá»‹ch vá»¥ yÃªu cáº§u|Dich vu yeu cau):\s*/i;
  const parts = withoutPrefix.split(splitToken);

  if (parts.length >= 3) {
    const requested = parts[2]?.trim();
    return {
      isAppt: isAppt,
      mainNotes: parts[0]?.trim() || "KhÃ´ng cÃ³ ghi chÃº",
      requestedServices: requested && requested !== "N/A" ? requested : null,
    };
  }

  return {
    isAppt: isAppt,
    mainNotes: withoutPrefix || "KhÃ´ng cÃ³ ghi chÃº",
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

