import { useEffect, useRef, useCallback } from 'react';

const DEFAULT_STORAGE_KEY = 'mygarage_last_active_at';

const useIdleTimeout = ({
  timeoutMinutes = 60,
  onIdle,
  enabled = true,
  storageKey = DEFAULT_STORAGE_KEY
}) => {
  const timeoutId = useRef(null);
  const isIdle = useRef(false);
  const timeoutMs = timeoutMinutes * 60 * 1000;

  const markActive = useCallback((timestamp = Date.now()) => {
    localStorage.setItem(storageKey, String(timestamp));
  }, [storageKey]);

  const triggerIdle = useCallback(() => {
    if (!isIdle.current) {
      isIdle.current = true;
      if (onIdle) onIdle();
    }
  }, [onIdle]);

  const resetTimer = useCallback(() => {
    if (!enabled) return;
    isIdle.current = false;
    if (timeoutId.current) {
      clearTimeout(timeoutId.current);
    }
    timeoutId.current = setTimeout(triggerIdle, timeoutMs);
  }, [enabled, timeoutMs, triggerIdle]);

  const checkExpired = useCallback(() => {
    if (!enabled) return false;
    const lastActiveRaw = localStorage.getItem(storageKey);
    const lastActive = Number(lastActiveRaw);

    if (!Number.isFinite(lastActive) || lastActive <= 0) {
      markActive();
      return false;
    }

    if (Date.now() - lastActive >= timeoutMs) {
      triggerIdle();
      return true;
    }
    return false;
  }, [enabled, storageKey, markActive, timeoutMs, triggerIdle]);

  useEffect(() => {
    if (!enabled) {
      if (timeoutId.current) clearTimeout(timeoutId.current);
      return undefined;
    }

    if (checkExpired()) {
      return undefined;
    }

    const activityEvents = ['mousemove', 'keydown', 'wheel', 'touchstart', 'touchmove', 'click', 'scroll', 'mousedown'];

    const handleActivity = () => {
      if (document.visibilityState === 'hidden') return;
      markActive();
      resetTimer();
    };

    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        if (checkExpired()) return;
        markActive();
        resetTimer();
      }
    };

    const handleStorageSync = (event) => {
      if (event.key !== storageKey) return;
      if (checkExpired()) return;
      resetTimer();
    };

    const handlePageLeave = () => {
      markActive();
    };

    activityEvents.forEach((event) => window.addEventListener(event, handleActivity, { passive: true }));
    window.addEventListener('focus', handleVisibility);
    window.addEventListener('storage', handleStorageSync);
    window.addEventListener('beforeunload', handlePageLeave);
    window.addEventListener('pagehide', handlePageLeave);
    document.addEventListener('visibilitychange', handleVisibility);

    markActive();
    resetTimer();

    return () => {
      if (timeoutId.current) clearTimeout(timeoutId.current);
      activityEvents.forEach((event) => window.removeEventListener(event, handleActivity));
      window.removeEventListener('focus', handleVisibility);
      window.removeEventListener('storage', handleStorageSync);
      window.removeEventListener('beforeunload', handlePageLeave);
      window.removeEventListener('pagehide', handlePageLeave);
      document.removeEventListener('visibilitychange', handleVisibility);
    };
  }, [enabled, checkExpired, markActive, resetTimer, storageKey]);
};

export default useIdleTimeout;
