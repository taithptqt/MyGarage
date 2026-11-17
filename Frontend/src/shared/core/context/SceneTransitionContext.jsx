import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import "@shared/components/SceneTransitionCurtain.css";

const ENTER_DURATION_MS = 380;
const MIN_COVER_MS = 120;
const LEAVE_DURATION_MS = 440;
const MAX_COVER_WAIT_MS = 650;

const SceneTransitionContext = createContext(null);

function prefersReducedMotion() {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return false;
  }
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export function SceneTransitionProvider({ children }) {
  const navigate = useNavigate();
  const location = useLocation();
  const [phase, setPhase] = useState("idle");
  const [label, setLabel] = useState("");
  const timersRef = useRef([]);
  const phaseRef = useRef("idle");
  const pendingPathRef = useRef("");

  const clearTimers = useCallback(() => {
    timersRef.current.forEach((id) => window.clearTimeout(id));
    timersRef.current = [];
  }, []);

  const updatePhase = useCallback((nextPhase) => {
    phaseRef.current = nextPhase;
    setPhase(nextPhase);
    if (nextPhase === "entering" || nextPhase === "covered") {
      document.documentElement.dataset.sceneCovering = "true";
    } else {
      delete document.documentElement.dataset.sceneCovering;
    }
  }, []);

  const scheduleReveal = useCallback(() => {
    clearTimers();
    const coverHold = window.setTimeout(() => {
      updatePhase("leaving");
      const doneTimer = window.setTimeout(() => {
        pendingPathRef.current = "";
        setLabel("");
        updatePhase("idle");
      }, LEAVE_DURATION_MS);
      timersRef.current.push(doneTimer);
    }, MIN_COVER_MS);
    timersRef.current.push(coverHold);
  }, [clearTimers, updatePhase]);

  useEffect(() => {
    if (phaseRef.current === "covered" && pendingPathRef.current) {
      if (location.pathname === pendingPathRef.current) {
        scheduleReveal();
      }
    }
  }, [location.pathname, scheduleReveal]);

  useEffect(() => {
    return () => {
      clearTimers();
      delete document.documentElement.dataset.sceneCovering;
    };
  }, [clearTimers]);

  const navigateWithTransition = useCallback(
    (targetPath, targetLabel = "") => {
      if (!targetPath) return;

      if (location.pathname === targetPath) {
        window.scrollTo({ top: 0, behavior: "smooth" });
        return;
      }

      if (prefersReducedMotion()) {
        navigate(targetPath);
        window.scrollTo({ top: 0, behavior: "instant" });
        return;
      }

      if (phaseRef.current === "entering" || phaseRef.current === "covered") {
        return;
      }

      clearTimers();
      pendingPathRef.current = targetPath;
      setLabel(String(targetLabel || "").trim());
      updatePhase("entering");

      const enterTimer = window.setTimeout(() => {
        updatePhase("covered");
        navigate(targetPath);
        window.scrollTo({ top: 0, behavior: "instant" });

        const fallbackReveal = window.setTimeout(() => {
          if (phaseRef.current === "covered") {
            scheduleReveal();
          }
        }, MAX_COVER_WAIT_MS);
        timersRef.current.push(fallbackReveal);
      }, ENTER_DURATION_MS);

      timersRef.current.push(enterTimer);
    },
    [clearTimers, location.pathname, navigate, scheduleReveal, updatePhase]
  );

  const value = useMemo(
    () => ({
      navigateWithTransition,
      transitionPhase: phase,
    }),
    [navigateWithTransition, phase]
  );

  return (
    <SceneTransitionContext.Provider value={value}>
      {children}
      <div className={`mg-curtain mg-curtain--${phase}`} aria-hidden="true">
        <div className="mg-curtain__inner">
          <span className="mg-curtain__brand">MY GARAGE</span>
          <span className="mg-curtain__line" />
          {label ? <span className="mg-curtain__label">{label}</span> : null}
        </div>
      </div>
    </SceneTransitionContext.Provider>
  );
}

export function useSceneTransition() {
  return useContext(SceneTransitionContext);
}
