import React from "react";
import { Box, Text } from "@chakra-ui/react";
import FilterDropdown from "@shared/components/FilterDropdown";

export default function DiagnosisDropdown({
  id,
  label,
  value,
  onChange,
  placeholder = "",
  isDisabled = false,
  minW,
  maxW,
  flex,
  labelColor = "#2b3674",
  labelFontSize = "sm",
  children,
  menuMinWidth,
  optionNoWrap = false,
  fullWidth = true,
}) {
  const options = React.Children.toArray(children)
    .filter((child) => React.isValidElement(child))
    .map((child) => ({
      value: child.props?.value ?? "",
      label: typeof child.props?.children === "string" ? child.props?.children : String(child.props?.children ?? ""),
      disabled: Boolean(child.props?.disabled),
    }));

  const normalizedValue = value ?? "";

  const selectedValueExists = options.some((option) => String(option.value) === String(normalizedValue));
  const fallbackOption = options.find((option) => String(option.value) === "") || options[0];
  const effectiveValue = selectedValueExists ? normalizedValue : (fallbackOption?.value ?? "");

  const normalizedMinWidth = minW === undefined ? 170 : (typeof minW === "number" ? minW : String(minW));

  return (
    <Box minW={minW} maxW={maxW} flex={flex}>
      {label ? (
        <Text fontSize={labelFontSize} fontWeight="600" color={labelColor} mb={2}>
          {label}
        </Text>
      ) : null}
      <FilterDropdown
        id={id}
        value={effectiveValue}
        options={options}
        onChange={onChange}
        minWidth={normalizedMinWidth}
        menuMinWidth={menuMinWidth}
        ariaLabel={label || placeholder || "dropdown"}
        disabled={isDisabled}
        optionNoWrap={optionNoWrap}
        fullWidth={fullWidth}
      />
    </Box>
  );
}
