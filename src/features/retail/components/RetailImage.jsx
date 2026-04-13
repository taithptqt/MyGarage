import { useEffect, useMemo, useState } from "react";
import { resolveMediaUrl } from "@features/retail/utils/retailFormat";

const DEFAULT_RETAIL_IMAGE = "/assets/booking/showcase-2.jpg";

export default function RetailImage({
  src,
  alt,
  className = "",
  fallbackClassName = "",
  fallbackLabel = "Hình ảnh phụ tùng đang cập nhật",
  ...rest
}) {
  const normalizedSrc = useMemo(
    () => resolveMediaUrl(src) || DEFAULT_RETAIL_IMAGE,
    [src]
  );
  const [currentSrc, setCurrentSrc] = useState(normalizedSrc);
  const [showFallbackSurface, setShowFallbackSurface] = useState(!normalizedSrc);

  useEffect(() => {
    setCurrentSrc(normalizedSrc);
    setShowFallbackSurface(!normalizedSrc);
  }, [normalizedSrc]);

  function handleError() {
    if (showFallbackSurface) {
      return;
    }

    if (currentSrc !== DEFAULT_RETAIL_IMAGE) {
      setCurrentSrc(DEFAULT_RETAIL_IMAGE);
      return;
    }

    setShowFallbackSurface(true);
  }

  if (showFallbackSurface) {
    return (
      <div
        className={`retail-media-fallback ${className} ${fallbackClassName}`.trim()}
        role="img"
        aria-label={alt}
      >
        <span>{fallbackLabel}</span>
      </div>
    );
  }

  return (
    <img
      {...rest}
      src={currentSrc}
      alt={alt}
      className={className}
      onError={handleError}
    />
  );
}
