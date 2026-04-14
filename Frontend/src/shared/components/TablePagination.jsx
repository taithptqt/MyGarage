import React, { useState } from "react";

export default function TablePagination({
  totalItems = 0,
  currentPage = 1,
  pageSize = 10,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50]
}) {
  const [isPageSizeFocused, setIsPageSizeFocused] = useState(false);
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const safePage = Math.min(Math.max(1, currentPage), totalPages);

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
        gap: 10,
        marginTop: 12
      }}
    >
      <label htmlFor="page-size-select">{"M\u1ed7i trang"}</label>
      <select
        id="page-size-select"
        value={pageSize}
        onChange={(e) => onPageSizeChange?.(Number(e.target.value))}
        onFocus={() => setIsPageSizeFocused(true)}
        onBlur={() => setIsPageSizeFocused(false)}
        style={{
          minWidth: 74,
          height: 32,
          borderRadius: 8,
          border: isPageSizeFocused ? "3px solid #1f2a63" : "1px solid #c9d4e5",
          background: "#ffffff",
          color: "#2b3674",
          fontWeight: 600,
          padding: "0 8px",
          outline: "none",
          boxShadow: isPageSizeFocused ? "0 0 0 2px rgba(43, 54, 116, 0.22)" : "none"
        }}
      >
        {pageSizeOptions.map((size) => (
          <option key={size} value={size}>
            {size}
          </option>
        ))}
      </select>

      <div
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: 10,
          height: 30,
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
          aria-label="Trang trước"
          style={{
            width: 30,
            height: 30,
            fontSize: 0,
            fontWeight: 700,
            padding: 0,
            opacity: 1,
            color: safePage <= 1 ? "#8ea3bc" : "#2b3674",
            border: "1px solid #c9d4e5",
            backgroundColor: "#ffffff",
            borderRadius: 8,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: safePage <= 1 ? "not-allowed" : "pointer",
            transition: "none"
          }}
        >
          <span style={{ fontSize: 18, lineHeight: 1, transform: "translateY(-1px)" }}>{"\u2039"}</span>
        </button>

        <span
          style={{
            minWidth: 46,
            height: 30,
            textAlign: "center",
            fontSize: 17,
            lineHeight: 1,
            color: "#2b3674",
            fontWeight: 700,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center"
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
            width: 30,
            height: 30,
            fontSize: 0,
            fontWeight: 700,
            padding: 0,
            opacity: 1,
            color: safePage >= totalPages ? "#8ea3bc" : "#2b3674",
            border: "1px solid #c9d4e5",
            backgroundColor: "#ffffff",
            borderRadius: 8,
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: safePage >= totalPages ? "not-allowed" : "pointer",
            transition: "none"
          }}
        >
          <span style={{ fontSize: 18, lineHeight: 1, transform: "translateY(-1px)" }}>{"\u203A"}</span>
        </button>
      </div>
    </div>
  );
}