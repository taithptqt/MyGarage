import { useCallback, useEffect, useRef, useState } from "react";

const SCRIPT_ID = "google-identity-services-script";

function GoogleLoginButton({ onCredential, onError }) {
  const buttonRef = useRef(null);
  const callbackRef = useRef(onCredential);
  const errorRef = useRef(onError);
  const [localError, setLocalError] = useState("");
  const [initialized, setInitialized] = useState(false);

  // Keep refs current without re-triggering useEffect
  useEffect(() => {
    callbackRef.current = onCredential;
  }, [onCredential]);

  useEffect(() => {
    errorRef.current = onError;
  }, [onError]);

  const handleCredentialResponse = useCallback(async (response) => {
    if (!response?.credential) {
      errorRef.current?.("Không lấy được thông tin đăng nhập từ Google.");
      return;
    }
    try {
      await callbackRef.current?.(response.credential);
    } catch (err) {
      errorRef.current?.(`Lỗi đăng nhập Google: ${err?.message || "Không xác định"}`);
    }
  }, []);

  useEffect(() => {
    if (initialized) return;
    let cancelled = false;

    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID || "";
    if (!clientId) {
      setLocalError("Thiếu VITE_GOOGLE_CLIENT_ID trong tệp frontend/.env");
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
          text: "signin_with",
          shape: "rectangular",
          logo_alignment: "left",
          width: 340
        });

        setInitialized(true);
      } catch (err) {
        console.error("Google button init error:", err);
        errorRef.current?.("Không khởi tạo được nút đăng nhập Google.");
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
    script.onerror = () => errorRef.current?.("Không tải được script hỗ trợ đăng nhập Google.");
    document.head.appendChild(script);

    return () => {
      cancelled = true;
      script.onload = null;
    };
  }, [initialized, handleCredentialResponse]);

  if (localError) {
    return <div className="error">{localError}</div>;
  }

  return <div ref={buttonRef} className="google-button" />;
}

export default GoogleLoginButton;
