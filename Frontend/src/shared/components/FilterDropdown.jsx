import React, { useMemo, useRef, useState } from "react";
import "./FilterDropdown.css";

const labelStyle = {
  fontSize: 13,
  fontWeight: 600,
  color: "#55637a",
  whiteSpace: "nowrap",
};

function normalizeOptions(options) {
  return (options || []).map((option) => {
    if (option && typeof option === "object") {
      return {
        value: option.value,
        label: option.label ?? String(option.value ?? ""),
        disabled: Boolean(option.disabled),
      };
    }

    const value = String(option ?? "");
    return { value, label: value, disabled: false };
  });
}

export default function FilterDropdown({
  id,
  label,
  value,
  options,
  onChange,
  minWidth = 170,
  menuMinWidth,
  ariaLabel,
  disabled = false,
  optionNoWrap = false,
  fullWidth = false,
}) {
  const [open, setOpen] = useState(false);
  const [openUpward, setOpenUpward] = useState(false);
  const [hoveredValue, setHoveredValue] = useState(null);
  const containerRef = useRef(null);

  const normalizedOptions = useMemo(() => normalizeOptions(options), [options]);
  const selected = useMemo(
    () => normalizedOptions.find((option) => String(option.value) === String(value)) || normalizedOptions[0],
    [normalizedOptions, value]
  );

  const handleToggle = () => {
    if (disabled) return;
    if (!open && containerRef.current) {
      const rect = containerRef.current.getBoundingClientRect();
      const spaceBelow = window.innerHeight - rect.bottom;
      const spaceAbove = rect.top;
      if (spaceBelow < 220 && spaceAbove > spaceBelow) {
        setOpenUpward(true);
      } else {
        setOpenUpward(false);
      }
    }
    setOpen((prev) => !prev);
  };

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, width: fullWidth ? "100%" : undefined }}>
      {label ? (
        <label htmlFor={id} style={labelStyle}>{label}:</label>
      ) : null}

      <div
        ref={containerRef}
        tabIndex={-1}
        onBlur={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget)) {
            setOpen(false);
          }
        }}
        style={{
          position: "relative",
          minWidth,
          width: fullWidth ? "100%" : undefined,
          zIndex: open ? 50 : 1,
        }}
      >
        <button
          id={id}
          type="button"
          disabled={disabled}
          aria-label={ariaLabel || label || "dropdown"}
          aria-haspopup="listbox"
          aria-expanded={open}
          onClick={handleToggle}
          style={{
            width: "100%",
            height: 36,
            margin: 0,
            borderRadius: 12,
            border: open ? "1px solid #95b7f6" : "1px solid #d8e1ef",
            background: disabled ? "#f3f4f6" : "linear-gradient(180deg, #ffffff 0%, #f7faff 100%)",
            color: disabled ? "#94a3b8" : "#334155",
            padding: "0 12px",
            fontSize: 14,
            fontWeight: 600,
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            boxShadow: disabled ? "none" : (open ? "0 6px 18px rgba(58, 99, 168, 0.16)" : "0 2px 8px rgba(58, 99, 168, 0.08)"),
            cursor: disabled ? "not-allowed" : "pointer",
            transition: "all .15s ease",
          }}
        >
          <span>{selected?.label || "-"}</span>
          <span
            style={{
              width: 8,
              height: 8,
              borderRight: "2px solid #6b7e9c",
              borderBottom: "2px solid #6b7e9c",
              transform: open ? (openUpward ? "rotate(45deg)" : "rotate(-135deg)") : (openUpward ? "rotate(-135deg)" : "rotate(45deg)"),
              marginBottom: open ? -3 : 3,
              transition: "all .15s ease",
            }}
          />
        </button>

        {open && (
          <div
            role="listbox"
            className="filter-dropdown-menu"
            style={{
              position: "absolute",
              top: openUpward ? "auto" : "calc(100% + 6px)",
              bottom: openUpward ? "calc(100% + 6px)" : "auto",
              left: 0,
              width: menuMinWidth ? Math.max(minWidth || 0, menuMinWidth) : "100%",
              minWidth: menuMinWidth || "100%",
              maxWidth: "min(92vw, 760px)",
              zIndex: 1000,
              background: "#ffffff",
              border: "1px solid #dbe4f2",
              borderRadius: 12,
              padding: 6,
              maxHeight: 280,
              overflowY: "auto",
              boxShadow: openUpward
                ? "0 -10px 24px rgba(58, 99, 168, 0.18)"
                : "0 10px 24px rgba(58, 99, 168, 0.18)",
            }}
          >
            {normalizedOptions.map((option) => {
              const isActive = String(option.value) === String(value);
              const isHovered = String(hoveredValue) === String(option.value);
              const isOptionDisabled = Boolean(option.disabled);
              return (
                <button
                  key={String(option.value)}
                  type="button"
                  onMouseEnter={() => {
                    if (isOptionDisabled) return;
                    setHoveredValue(option.value);
                  }}
                  onMouseLeave={() => setHoveredValue(null)}
                  onClick={() => {
                    if (isOptionDisabled) return;
                    onChange?.(option.value);
                    setOpen(false);
                  }}
                  disabled={isOptionDisabled}
                  style={{
                    width: "100%",
                    margin: 0,
                    textAlign: "left",
                    border: "none",
                    borderRadius: 9,
                    background: isOptionDisabled ? "#f8fafc" : (isActive ? "#e9f1ff" : (isHovered ? "#f3f7ff" : "transparent")),
                    color: isOptionDisabled ? "#94a3b8" : (isActive ? "#244f9a" : "#2f3d55"),
                    padding: "9px 10px",
                    fontSize: 14,
                    fontWeight: isActive ? 700 : 500,
                    whiteSpace: optionNoWrap ? "nowrap" : "normal",
                    cursor: isOptionDisabled ? "not-allowed" : "pointer",
                  }}
                >
                  {option.label}
                </button>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
