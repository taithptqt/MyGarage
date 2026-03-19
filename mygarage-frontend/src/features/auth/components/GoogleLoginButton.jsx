import { useCallback, useEffect, useRef, useState } from "react";

const SCRIPT_ID = "google-identity-services-script";

function GoogleLoginButton({ onCredential, onError, mode = "signin" }) {
  const buttonRef = useRef(null);
  const callbackRef = useRef(onCredential);
  const errorRef = useRef(onError);
  const [localError, setLocalError] = useState("");

  // Keep refs current without re-triggering useEffect
  useEffect(() => {
    callbackRef.current = onCredential;
  }, [onCredential]);

  useEffect(() => {
    errorRef.current = onError;
  }, [onError]);

  const handleCredentialResponse = useCallback(async (response) => {
    if (!response?.credential) {
      errorRef.current?.("Khong lay duoc Google credential.");
      return;
    }
    try {
      await callbackRef.current?.(response.credential);
    } catch (err) {
      errorRef.current?.(`Google login error: ${err?.message || "Unknown"}`);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID || "";
    if (!clientId) {
      setLocalError("Thieu VITE_GOOGLE_CLIENT_ID trong frontend/.env");
      return;
    }

    const initGoogleButton = () => {
      if (cancelled || !buttonRef.current || !window.google?.accounts?.id) return;

      try {
        window.google.accounts.id.initialize({
          client_id: clientId,
          ux_mode: "popup",
          callback: handleCredentialResponse
        });

        buttonRef.current.innerHTML = "";
        window.google.accounts.id.renderButton(buttonRef.current, {
          type: "standard",
          theme: "outline",
          size: "large",
          text: mode === "signup" ? "signup_with" : "signin_with",
          shape: "rectangular",
          logo_alignment: "left",
          width: 340
        });

      } catch (err) {
        console.error("Google button init error:", err);
        errorRef.current?.("Khong khoi tao duoc Google Sign-In.");
      }
    };

    if (window.google?.accounts?.id) {
      initGoogleButton();
      return () => { cancelled = true; };
    }

    const existing = document.getElementById(SCRIPT_ID);
    if (existing) {
      existing.addEventListener("load", initGoogleButton);
      return () => {
        cancelled = true;
        existing.removeEventListener("load", initGoogleButton);
      };
    }

    const script = document.createElement("script");
    script.id = SCRIPT_ID;
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = initGoogleButton;
    script.onerror = () => errorRef.current?.("Khong tai duoc Google script.");
    document.head.appendChild(script);

    return () => {
      cancelled = true;
      script.onload = null;
    };
  }, [mode, handleCredentialResponse]);

  if (localError) {
    return <div className="error">{localError}</div>;
  }

  return <div ref={buttonRef} className="google-button" />;
}

export default GoogleLoginButton;
