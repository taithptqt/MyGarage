import { useEffect, useRef } from "react";

const PARTS_SHOP_RETURN_KEY = "mygarage.partsShop.restoreCatalog";

export function getPreferredScrollBehavior() {
  if (typeof window === "undefined") return "auto";

  return window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ? "auto" : "smooth";
}

function scrollToCatalog(node, behavior = getPreferredScrollBehavior()) {
  if (!node) return;

  node.scrollIntoView({
    behavior,
    block: "start",
  });
}

export function markPartsShopCatalogReturn() {
  if (typeof window === "undefined") return;

  window.sessionStorage.setItem(PARTS_SHOP_RETURN_KEY, "1");
}

export default function usePartsShopScroll({ currentPage, setCurrentPage }) {
  const catalogRef = useRef(null);
  const hasMountedRef = useRef(false);
  const pendingPaginationScrollRef = useRef(false);

  useEffect(() => {
    if (typeof window === "undefined") return undefined;

    const shouldRestoreCatalog =
      window.sessionStorage.getItem(PARTS_SHOP_RETURN_KEY) === "1";

    if (!shouldRestoreCatalog) return undefined;

    window.sessionStorage.removeItem(PARTS_SHOP_RETURN_KEY);

    const frameId = window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        scrollToCatalog(catalogRef.current);
      });
    });

    return () => window.cancelAnimationFrame(frameId);
  }, []);

  useEffect(() => {
    if (!hasMountedRef.current) {
      hasMountedRef.current = true;
      return;
    }

    if (!pendingPaginationScrollRef.current) return;

    pendingPaginationScrollRef.current = false;

    if (typeof window === "undefined") return undefined;

    const frameId = window.requestAnimationFrame(() => {
      scrollToCatalog(catalogRef.current);
    });

    return () => window.cancelAnimationFrame(frameId);
  }, [currentPage]);

  function handlePageChange(nextPage) {
    if (Number(nextPage) === Number(currentPage)) return;

    pendingPaginationScrollRef.current = true;
    setCurrentPage(nextPage);
  }

  return {
    catalogRef,
    handlePageChange,
  };
}
