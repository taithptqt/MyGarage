const VIETNAM_TIMEZONE = "Asia/Ho_Chi_Minh";

function withVietnamTimezone(options) {
  if (options && typeof options === "object" && options.timeZone) {
    return options;
  }
  return { ...(options || {}), timeZone: VIETNAM_TIMEZONE };
}

export function forceVietnamTimezone() {
  if (typeof Intl === "undefined" || typeof Date === "undefined") {
    return;
  }

  if (globalThis.__MYGARAGE_VN_TIMEZONE_PATCHED__) {
    return;
  }

  const OriginalDateTimeFormat = Intl.DateTimeFormat;
  const PatchedDateTimeFormat = function patchedDateTimeFormat(locales, options) {
    return new OriginalDateTimeFormat(locales, withVietnamTimezone(options));
  };

  PatchedDateTimeFormat.prototype = OriginalDateTimeFormat.prototype;
  Object.setPrototypeOf(PatchedDateTimeFormat, OriginalDateTimeFormat);
  PatchedDateTimeFormat.supportedLocalesOf = OriginalDateTimeFormat.supportedLocalesOf.bind(OriginalDateTimeFormat);

  Intl.DateTimeFormat = PatchedDateTimeFormat;

  const localeMethods = ["toLocaleString", "toLocaleTimeString", "toLocaleDateString"];
  localeMethods.forEach((methodName) => {
    const originalMethod = Date.prototype[methodName];
    if (typeof originalMethod !== "function") {
      return;
    }

    Date.prototype[methodName] = function patchedLocaleMethod(locales, options) {
      return originalMethod.call(this, locales, withVietnamTimezone(options));
    };
  });

  globalThis.__MYGARAGE_VN_TIMEZONE_PATCHED__ = true;
}

