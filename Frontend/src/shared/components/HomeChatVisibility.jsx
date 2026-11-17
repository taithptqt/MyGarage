import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";

const SHOW_AFTER_SCROLL_PX = 100;

export default function HomeChatVisibility({ children }) {
  const { pathname } = useLocation();
  const isHome = pathname === "/home" || pathname === "/";
  const [hasScrolled, setHasScrolled] = useState(false);

  useEffect(() => {
    if (!isHome) return undefined;
    const update = () => setHasScrolled(window.scrollY > SHOW_AFTER_SCROLL_PX);
    update();
    window.addEventListener("scroll", update, { passive: true });
    return () => window.removeEventListener("scroll", update);
  }, [pathname, isHome]);

  return <div style={isHome && !hasScrolled ? { display: "none" } : undefined}>{children}</div>;
}
