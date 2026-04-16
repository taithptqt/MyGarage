import React, { useEffect, useRef, useState } from "react";

export default function TablePagination({
  totalItems = 0,
  currentPage = 1,
  pageSize = 10,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50]
}) {
  const [isPageSizeFocused, setIsPageSizeFocused] = useState(false);
  const [isPageSizeOpen, setIsPageSizeOpen] = useState(false);
  const pageSizeRef = useRef(null);

  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const safePage = Math.min(Math.max(1, currentPage), totalPages);

  useEffect(() => {
    function handleClickOutside(event) {
      if (!pageSizeRef.current?.contains(event.target)) {
        setIsPageSizeOpen(false);
        setIsPageSizeFocused(false);
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  if (totalItems <= 0) return null;

  return (
    <div
      className="table-pagination"
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        flexWrap: "wrap",
        overflow: "visible",
        gap: 12,
        marginTop: 12
      }}
    >
      <label htmlFor="page-size-select" style={{ fontWeight: 600, color: "#2b3674" }}>
        {"M\u1ed7i trang"}
      </label>

      <div ref={pageSizeRef} style={{ position: "relative" }}>
        <button
          id="page-size-select"
          type="button"
          onClick={() => {
            setIsPageSizeOpen((prev) => !prev);
            setIsPageSizeFocused(true);
          }}
          style={{
            minWidth: 84,
            height: 36,
            marginTop: 0,
            borderRadius: 12,
            border: isPageSizeFocused || isPageSizeOpen ? "2px solid #5872b0" : "1px solid #c9d4e5",
            background: "#ffffff",
            color: "#2b3674",
            fontWeight: 600,
            padding: "0 30px 0 12px",
            outline: "none",
            boxShadow: isPageSizeFocused || isPageSizeOpen ? "0 0 0 1px rgba(88, 114, 176, 0.22)" : "none",
            cursor: "pointer",
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "flex-start",
            position: "relative"
          }}
          aria-haspopup="listbox"
          aria-expanded={isPageSizeOpen}
        >
          <span>{pageSize}</span>
          <span
            style={{
              position: "absolute",
              right: 10,
              top: "50%",
              transform: `translateY(-50%) rotate(${isPageSizeOpen ? 180 : 0}deg)`,
              width: 0,
              height: 0,
              borderLeft: "5px solid transparent",
              borderRight: "5px solid transparent",
              borderTop: "6px solid #5e72a8",
              transition: "transform 0.15s ease"
            }}
          />
        </button>

        {isPageSizeOpen ? (
          <div
            role="listbox"
            aria-label="Chon so luong moi trang"
            style={{
              position: "absolute",
              top: 40,
              left: 0,
              minWidth: 84,
              background: "#ffffff",
              border: "1px solid #d7e0ef",
              borderRadius: 12,
              boxShadow: "0 10px 24px rgba(28, 44, 82, 0.16)",
              overflow: "hidden",
              zIndex: 20
            }}
          >
            {pageSizeOptions.map((size, idx) => {
              const active = Number(size) === Number(pageSize);
              const isLast = idx === pageSizeOptions.length - 1;

              return (
                <button
                  key={size}
                  type="button"
                  onClick={() => {
                    onPageSizeChange?.(Number(size));
                    setIsPageSizeOpen(false);
                    setIsPageSizeFocused(false);
                  }}
                  style={{
                    width: "100%",
                    height: 36,
                    marginTop: 0,
                    border: "none",
                    borderBottom: isLast ? "none" : "1px solid #eef2f9",
                    background: active ? "#eef3ff" : "#ffffff",
                    color: active ? "#2b3674" : "#32466f",
                    fontWeight: active ? 700 : 600,
                    textAlign: "left",
                    padding: "0 12px",
                    cursor: "pointer"
                  }}
                >
                  {size}
                </button>
              );
            })}
          </div>
        ) : null}
      </div>

      <div
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: 12,
          height: 36,
          visibility: "visible",
          opacity: 1,
          pointerEvents: "auto",
          flexShrink: 0
        }}
      >
        <button
          type="button"
          disabled={safePage <= 1}
          onClick={() => onPageChange?.(safePage - 1)}
          aria-label="Trang truoc"
          style={{
            width: 36,
            height: 36,
            marginTop: 0,
            fontSize: 0,
            fontWeight: 700,
            padding: 0,
            opacity: 1,
            color: safePage <= 1 ? "#8ea3bc" : "#2b3674",
            border: "1px solid #c9d4e5",
            backgroundColor: safePage <= 1 ? "#f6f8fc" : "#ffffff",
            borderRadius: 10,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: safePage <= 1 ? "not-allowed" : "pointer",
            transition: "none"
          }}
        >
          <span style={{ fontSize: 18, lineHeight: "18px", display: "block" }}>{"\u2039"}</span>
        </button>

        <span
          style={{
            minWidth: 52,
            height: 36,
            textAlign: "center",
            fontSize: 17,
            lineHeight: 1,
            color: "#2b3674",
            fontWeight: 700,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            transform: "translateY(1px)"
          }}
        >
          {safePage}/{totalPages}
        </span>

        <button
          type="button"
          disabled={safePage >= totalPages}
          onClick={() => onPageChange?.(safePage + 1)}
          aria-label="Trang sau"
          style={{
            width: 36,
            height: 36,
            marginTop: 0,
            fontSize: 0,
            fontWeight: 700,
            padding: 0,
            opacity: 1,
            color: safePage >= totalPages ? "#8ea3bc" : "#2b3674",
            border: "1px solid #c9d4e5",
            backgroundColor: safePage >= totalPages ? "#f6f8fc" : "#ffffff",
            borderRadius: 10,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: safePage >= totalPages ? "not-allowed" : "pointer",
            transition: "none"
          }}
        >
          <span style={{ fontSize: 18, lineHeight: "18px", display: "block" }}>{"\u203A"}</span>
        </button>
      </div>
    </div>
  );
}

