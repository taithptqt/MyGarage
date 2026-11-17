import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  Box,
  Button,
  Grid,
  HStack,
  Input,
  NumberInput,
  NumberInputField,
  Stack,
  Text,
  Textarea,
  Tooltip,
} from "@chakra-ui/react";
import DiagnosisDropdown from "@features/workshop/pages/DiagnosisDropdown";
import {
  extractCategoryId,
  extractCategoryName,
  extractCategoryParentId,
  extractProductCategoryId,
  extractProductCategoryName,
  extractProductName,
  extractServiceId,
  extractServiceName,
  normalizeImageUrls,
  normalizeLookupText,
  toPositiveNumber,
} from "@features/workshop/pages/serviceOrderDiagnosisHelpers";

export default function RecommendationRow({
  item,
  mode = "service",
  displayNumber,
  canEdit,
  services,
  products,
  partCategories = [],
  onChange,
  onRemove,
  onAddMaterial,
  onAddEvidenceImages,
  onRemoveEvidenceImage,
  parentServiceName,
  materialItems = [],
  materialCount = 0,
  isMaterialExpanded = false,
  onToggleMaterials = null,
}) {
  const isServiceMode = mode === "service";
  const canAddMaterial = Boolean(item.standardServiceId);

  const selectedService = isServiceMode
    ? (services || []).find((service) => String(extractServiceId(service) || "") === String(item.standardServiceId || ""))
    : null;

  const selectedServiceName = isServiceMode
    ? (extractServiceName(selectedService) || (item.standardServiceId ? `Dịch vụ #${item.standardServiceId}` : "Chưa chọn dịch vụ"))
    : "";

  const selectedProduct = !isServiceMode
    ? (products || []).find((product) => String(product?.id ?? product?.Id ?? "") === String(item.inventoryItemId || ""))
    : null;

  const selectedProductName = !isServiceMode
    ? String(selectedProduct?.name ?? selectedProduct?.Name ?? "").trim() || "Chưa chọn vật tư"
    : "";

  const isQuantityInputEmpty = item.quantity === "";
  const parsedQuantity = Number(item.quantity);
  const itemQuantity = Number.isFinite(parsedQuantity) && parsedQuantity > 0 ? parsedQuantity : 1;
  const quantityInputValue = isQuantityInputEmpty ? "" : itemQuantity;

  const quantityWarningMessage = isServiceMode
    ? (itemQuantity > 5 ? "Số lượng dịch vụ vượt 5. Vui lòng kiểm tra lại." : "")
    : "";

  const serviceEvidenceUrls = isServiceMode ? normalizeImageUrls(item.evidenceMediaUrls) : [];
  const evidenceInputId = `diagnose-evidence-${item.id}`;

  const canToggleMaterials = isServiceMode && materialCount > 0 && typeof onToggleMaterials === "function";
  const toggleMaterialsTooltip = isMaterialExpanded
    ? "Bấm vào dòng dịch vụ để ẩn danh sách phụ tùng."
    : "Bấm vào dòng dịch vụ để xem danh sách phụ tùng.";

  const [materialPickerVisible, setMaterialPickerVisible] = useState(false);
  const [materialPickerCategoryId, setMaterialPickerCategoryId] = useState("");
  const [materialPickerKeyword, setMaterialPickerKeyword] = useState("");
  const [materialPickerOpen, setMaterialPickerOpen] = useState(false);
  const materialPickerRef = useRef(null);

  const safeMaterialItems = Array.isArray(materialItems) ? materialItems : [];
  const safeProducts = Array.isArray(products) ? products : [];
  const safePartCategories = Array.isArray(partCategories) ? partCategories : [];

  const partCategoryById = useMemo(() => {
    const map = new Map();
    safePartCategories.forEach((category) => {
      const id = extractCategoryId(category);
      if (!id) return;
      const name = extractCategoryName(category) || `Danh mục ${id}`;
      map.set(String(id), name);
    });
    return map;
  }, [safePartCategories]);

  const existingMaterialInventoryIdSet = useMemo(
    () =>
      new Set(
        safeMaterialItems
          .map((material) => String(toPositiveNumber(material?.inventoryItemId ?? material?.InventoryItemId) || ""))
          .filter(Boolean)
      ),
    [safeMaterialItems]
  );

  const materialCategoryOptions = useMemo(() => {
    const map = new Map();
    const allowedCategoryIdSet = new Set();

    safePartCategories.forEach((category) => {
      const categoryId = extractCategoryId(category);
      const parentCategoryId = extractCategoryParentId(category);
      if (!categoryId || !parentCategoryId) return;
      if (map.has(String(categoryId))) return;
      const categoryName = extractCategoryName(category) || `Danh mục ${categoryId}`;
      allowedCategoryIdSet.add(String(categoryId));
      map.set(String(categoryId), { value: String(categoryId), label: categoryName });
    });

    safeProducts.forEach((product) => {
      const categoryId = extractProductCategoryId(product);
      if (!categoryId) return;
      if (!allowedCategoryIdSet.has(String(categoryId))) return;
      if (map.has(String(categoryId))) return;
      const categoryName =
        partCategoryById.get(String(categoryId)) ||
        extractProductCategoryName(product) ||
        `Danh mục ${categoryId}`;
      map.set(String(categoryId), { value: String(categoryId), label: categoryName });
    });

    return Array.from(map.values()).sort((a, b) => a.label.localeCompare(b.label, "vi"));
  }, [safePartCategories, safeProducts, partCategoryById]);

  const filteredMaterialProducts = useMemo(() => {
    const keyword = normalizeLookupText(materialPickerKeyword);
    return safeProducts
      .filter((product) => {
        const productId = String(toPositiveNumber(product?.id ?? product?.Id) || "");
        if (!productId) return false;

        const categoryId = String(extractProductCategoryId(product) || "");
        if (materialPickerCategoryId && categoryId !== String(materialPickerCategoryId)) return false;

        if (!keyword) return true;

        const productName = normalizeLookupText(extractProductName(product));
        const productCode = normalizeLookupText(String(product?.sku ?? product?.Sku ?? product?.code ?? product?.Code ?? ""));
        const categoryName = normalizeLookupText(partCategoryById.get(categoryId) || extractProductCategoryName(product));
        return productName.includes(keyword) || productCode.includes(keyword) || categoryName.includes(keyword);
      })
      .sort((a, b) => extractProductName(a).localeCompare(extractProductName(b), "vi"));
  }, [safeProducts, materialPickerCategoryId, materialPickerKeyword, partCategoryById]);

  useEffect(() => {
    if (!materialPickerOpen) return undefined;
    const handleOutsideClick = (event) => {
      if (!materialPickerRef.current) return;
      if (materialPickerRef.current.contains(event.target)) return;
      setMaterialPickerOpen(false);
    };
    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, [materialPickerOpen]);

  const handleServiceRowToggle = (event) => {
    if (!canToggleMaterials) return;
    const target = event?.target;
    if (target instanceof Element) {
      const interactiveParent = target.closest(
        "button, input, textarea, select, a, label, [role='button'], [data-no-row-toggle='true']"
      );
      if (interactiveParent && interactiveParent !== event.currentTarget) return;
    }
    onToggleMaterials?.();
  };

  const handleServiceRowKeyDown = (event) => {
    if (!canToggleMaterials) return;
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onToggleMaterials?.();
    }
  };

  const openMaterialPicker = () => {
    if (!canEdit || !isServiceMode || !canAddMaterial) return;
    if (materialPickerVisible) {
      setMaterialPickerVisible(false);
      setMaterialPickerOpen(false);
      setMaterialPickerKeyword("");
      return;
    }
    setMaterialPickerVisible(true);
    setMaterialPickerOpen(false);
    if (!isMaterialExpanded) onToggleMaterials?.();
  };

  const selectMaterialFromPicker = (product) => {
    const productId = toPositiveNumber(product?.id ?? product?.Id);
    if (!productId) return;
    onAddMaterial?.(item.id, productId);
    if (!isMaterialExpanded) onToggleMaterials?.();
    setMaterialPickerOpen(true);
    setMaterialPickerKeyword("");
  };

  const toggleMaterialFromPicker = (product) => {
    const productId = toPositiveNumber(product?.id ?? product?.Id);
    if (!productId) return;

    const existedItem = safeMaterialItems.find(
      (material) => toPositiveNumber(material?.inventoryItemId ?? material?.InventoryItemId) === productId
    );

    if (existedItem) {
      onRemove?.(existedItem);
      setMaterialPickerOpen(true);
      return;
    }

    selectMaterialFromPicker(product);
  };

  return (
    <Stack
      spacing={isServiceMode ? 1 : 1}
      p={{ base: 2.5, md: 2 }}
      position="relative"
      overflow="visible"
      zIndex={isServiceMode && materialPickerOpen ? 60 : "auto"}
      border={isServiceMode ? "1px solid #c9daf8" : "1px solid #eadfcf"}
      borderRadius="12px"
      bg={isServiceMode ? "#f8fbff" : "#fffaf5"}
      boxShadow="0 1px 4px rgba(42, 67, 122, 0.08)"
      cursor={canToggleMaterials ? "pointer" : "default"}
      transition="all .18s ease"
      _hover={
        canToggleMaterials
          ? {
              borderColor: "#93b6ff",
              boxShadow: "0 6px 14px rgba(42, 67, 122, 0.12)",
            }
          : undefined
      }
      onClick={handleServiceRowToggle}
      onKeyDown={handleServiceRowKeyDown}
      role={canToggleMaterials ? "button" : undefined}
      tabIndex={canToggleMaterials ? 0 : undefined}
      aria-expanded={canToggleMaterials ? isMaterialExpanded : undefined}
      title={canToggleMaterials ? toggleMaterialsTooltip : undefined}
    >
      <HStack justify="space-between" align="flex-start" flexWrap="wrap" gap={1} position="relative" zIndex={2}>
        <HStack spacing={1.5} align="center" minH={{ base: "30px", md: "26px" }}>
          {canToggleMaterials ? (
            <Tooltip hasArrow label={toggleMaterialsTooltip} placement="top-start">
              <Text fontWeight="800" fontSize={{ base: "lg", md: "md" }} color={isServiceMode ? "#2b3674" : "#8a5a1f"}>
                {`#${displayNumber ?? item.id}`}
              </Text>
            </Tooltip>
          ) : (
            <Text fontWeight="800" fontSize={{ base: "lg", md: "md" }} color={isServiceMode ? "#2b3674" : "#8a5a1f"}>
              {`#${displayNumber ?? item.id}`}
            </Text>
          )}
        </HStack>

        <HStack spacing={2} align="center" position="relative" zIndex={3} ml="auto">
          {canEdit && (
            <Button
              size={{ base: "md", md: "sm" }}
              minH={{ base: "34px", md: "30px" }}
              px={{ base: 3, md: 2 }}
              colorScheme="red"
              variant="ghost"
              position="relative"
              zIndex={3}
              onClick={() => onRemove(item)}
            >
              Xóa
            </Button>
          )}
        </HStack>
      </HStack>

      {isServiceMode && (
        <Grid templateColumns={{ base: "1fr", xl: "minmax(0, 1fr) 520px" }} gap={{ base: 2, md: 2 }} alignItems="center">
          <Box>
            <Text fontSize={{ base: "lg", md: "md" }} fontWeight="800" color="#2b3674" lineHeight="1.2" mt={1}>
              {selectedServiceName}
            </Text>
          </Box>

          <HStack align="center" spacing={1} maxW={{ base: "100%", xl: "520px" }} justify="flex-start" ml={0}>
            <Box p={1.5} borderRadius="8px" border="1px solid #9fc0ff" bg="#f8fbff" w={{ base: "100%", xl: "126px" }} minW={{ base: "100%", xl: "126px" }}>
              <Input
                id={evidenceInputId}
                type="file"
                accept="image/*"
                capture="environment"
                multiple
                display="none"
                isDisabled={!canEdit}
                onChange={(event) => {
                  onAddEvidenceImages?.(item.id, event.target.files);
                  event.target.value = "";
                }}
              />

              <Box
                as="label"
                htmlFor={evidenceInputId}
                display="inline-flex"
                alignItems="center"
                justifyContent="center"
                px={2}
                h="44px"
                w="100%"
                minW="100%"
                borderRadius="8px"
                bg="linear-gradient(180deg, #ffffff 0%, #edf4ff 100%)"
                color="#2563eb"
                border="1px solid #7fa7ff"
                boxShadow="0 1px 4px rgba(37, 99, 235, 0.12)"
                fontSize="13px"
                fontWeight="700"
                whiteSpace="nowrap"
                letterSpacing="0.01em"
                cursor={canEdit ? "pointer" : "not-allowed"}
                _hover={canEdit ? { borderColor: "#5b8fff", bg: "linear-gradient(180deg, #ffffff 0%, #e5efff 100%)" } : undefined}
                data-no-row-toggle="true"
              >
                + Tải ảnh
              </Box>
              <Text mt={0.5} fontSize="11px" fontWeight="700" color="#e45b5b" lineHeight="1.2" textAlign="center">
                * Ảnh bằng chứng
              </Text>
            </Box>

            <HStack spacing={1} overflowX="visible" maxW="100%" flex="1" justify="flex-start" pt={0} flexWrap="wrap" rowGap={1}>
              {serviceEvidenceUrls.map((url, index) => (
                <Box key={`${item.id}-evidence-${index}`} position="relative" flex="0 0 auto">
                  <Box
                    as="img"
                    src={url}
                    alt={`evidence-${index + 1}`}
                    w="82px"
                    h="82px"
                    objectFit="cover"
                    borderRadius="10px"
                    border="1px solid #9fc0ff"
                    bg="#f5f8ff"
                    cursor="zoom-in"
                    data-no-row-toggle="true"
                    onClick={(event) => event.stopPropagation()}
                  />
                  {canEdit && (
                    <Button
                      size="2xs"
                      colorScheme="red"
                      position="absolute"
                      top="-4px"
                      right="-4px"
                      minW="14px"
                      h="14px"
                      p={0}
                      fontSize="10px"
                      data-no-row-toggle="true"
                      onClick={() => onRemoveEvidenceImage?.(item.id, index)}
                    >
                      x
                    </Button>
                  )}
                </Box>
              ))}
            </HStack>
          </HStack>
        </Grid>
      )}

      {isServiceMode && (
        <Box mt={{ base: 1, md: 0.5 }} data-no-row-toggle="true">
          <Grid
            templateColumns={{ base: "1fr", lg: "minmax(220px, 1fr) minmax(220px, 1fr) auto" }}
            gap={{ base: 1.5, md: 3 }}
            alignItems="end"
            justifyContent="stretch"
          >
            <Box position="relative" w="100%" maxW="100%" ml={0}>
              <Text fontSize={{ base: "md", md: "sm" }} fontWeight="700" color="#7f8fb2" mb={1}>
                Số lượng
              </Text>
              <NumberInput
                size="sm"
                min={1}
                value={quantityInputValue}
                isDisabled={!canEdit}
                onChange={(valueString, valueNumber) => {
                  if (valueString === "") {
                    onChange(item.id, { quantity: "" });
                    return;
                  }
                  onChange(item.id, { quantity: Number.isFinite(valueNumber) && valueNumber > 0 ? valueNumber : 1 });
                }}
                keepWithinRange
                clampValueOnBlur
                w="100%"
              >
                <HStack spacing={0.75} w="100%">
                  <Button
                    size="sm"
                    minW={{ base: "34px", md: "30px" }}
                    h={{ base: "34px", md: "30px" }}
                    px={0}
                    borderRadius="10px"
                    variant="outline"
                    borderColor="#9fc0ff"
                    color="#2b3674"
                    fontSize={{ base: "md", md: "sm" }}
                    isDisabled={!canEdit || isQuantityInputEmpty || itemQuantity <= 1}
                    onClick={() => onChange(item.id, { quantity: Math.max(1, itemQuantity - 1) })}
                  >
                    -
                  </Button>
                  <NumberInputField
                    bg="rgba(255,255,255,0.92)"
                    borderColor={quantityWarningMessage ? "#f4b400" : "#9fc0ff"}
                    borderRadius="10px"
                    textAlign="center"
                    h={{ base: "34px", md: "30px" }}
                    fontSize={{ base: "md", md: "sm" }}
                    fontWeight="700"
                    _focus={{ borderColor: "#7aa6ff", boxShadow: "0 0 0 1px #7aa6ff" }}
                    onBlur={() => {
                      if (item.quantity === "" || !Number.isFinite(Number(item.quantity)) || Number(item.quantity) <= 0) {
                        onChange(item.id, { quantity: 1 });
                      }
                    }}
                  />
                  <Button
                    size="sm"
                    minW={{ base: "34px", md: "30px" }}
                    h={{ base: "34px", md: "30px" }}
                    px={0}
                    borderRadius="10px"
                    variant="outline"
                    borderColor="#9fc0ff"
                    color="#2b3674"
                    fontSize={{ base: "md", md: "sm" }}
                    isDisabled={!canEdit}
                    onClick={() => onChange(item.id, { quantity: (isQuantityInputEmpty ? 0 : itemQuantity) + 1 })}
                  >
                    +
                  </Button>
                </HStack>
              </NumberInput>
              {quantityWarningMessage ? (
                <Text
                  position="absolute"
                  top="calc(100% + 4px)"
                  left="0"
                  fontSize="12px"
                  fontWeight="600"
                  color="#dc2626"
                  lineHeight="1.3"
                  zIndex={2}
                >
                  {quantityWarningMessage}
                </Text>
              ) : null}
            </Box>

            <Box w="100%" maxW="100%">
              <Text fontSize={{ base: "md", md: "sm" }} fontWeight="700" color="#7f8fb2" mb={1}>
                Độ ưu tiên
              </Text>
              <Box display="flex" alignItems="center">
                <DiagnosisDropdown
                  minW="100%"
                  value={item.urgency || "RECOMMENDED"}
                  isDisabled={!canEdit}
                  onChange={(value) => onChange(item.id, { urgency: value })}
                  fullWidth
                >
                  <option value="CRITICAL">Khẩn cấp</option>
                  <option value="RECOMMENDED">Khuyến nghị</option>
                  <option value="OPTIONAL">Tùy chọn</option>
                </DiagnosisDropdown>
              </Box>
            </Box>

            {canEdit && (
              <Box w={{ base: "100%", lg: "auto" }}>
                <Box
                  as="button"
                  type="button"
                  appearance="none"
                  WebkitAppearance="none"
                  h={{ base: "34px", md: "32px" }}
                  minH={{ base: "34px", md: "32px" }}
                  maxH={{ base: "34px", md: "32px" }}
                  px={{ base: 3, md: 3 }}
                  py="0"
                  m="0"
                  borderRadius="12px"
                  border="1px solid"
                  borderColor={canAddMaterial ? "#2b6fe8" : "#c9d8ef"}
                  bg={canAddMaterial ? "linear-gradient(180deg, #ffffff 0%, #f7faff 100%)" : "#f3f4f6"}
                  color={canAddMaterial ? "#2b6fe8" : "#94a3b8"}
                  fontSize={{ base: "sm", md: "sm" }}
                  fontWeight="700"
                  lineHeight={{ base: "34px", md: "32px" }}
                  display="inline-flex"
                  alignItems="center"
                  justifyContent="center"
                  verticalAlign="middle"
                  boxShadow={canAddMaterial ? "0 2px 8px rgba(58, 99, 168, 0.08)" : "none"}
                  cursor={canAddMaterial ? "pointer" : "not-allowed"}
                  transition="all .15s ease"
                  _hover={canAddMaterial ? { bg: "#f4f8ff" } : undefined}
                  w={{ base: "100%", lg: "auto" }}
                  onClick={(event) => {
                    event.stopPropagation();
                    openMaterialPicker();
                  }}
                  disabled={!canAddMaterial}
                  data-no-row-toggle="true"
                >
                  + Thêm phụ tùng
                </Box>
              </Box>
            )}
          </Grid>

          <Box pt={0.75} mt={0.75} borderTop="1px dashed #9fc0ff">
            <Text fontSize={{ base: "xs", md: "11px" }} fontWeight="700" textTransform="uppercase" letterSpacing="0.02em" color="#6f83b3" mb={1.5}>
              Ghi chú kỹ thuật
            </Text>
            <Textarea
              size="sm"
              minH={{ base: "48px", md: "42px" }}
              placeholder="Nhập ghi chú kỹ thuật..."
              value={item.technicianNotes || ""}
              isDisabled={!canEdit}
              onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
              bg="rgba(255,255,255,0.92)"
              borderColor="#9fc0ff"
              fontSize={{ base: "sm", md: "sm" }}
              _focus={{ borderColor: "#7aa6ff", boxShadow: "0 0 0 1px #7aa6ff" }}
            />
          </Box>

          {materialPickerVisible ? (
            <Box
              ref={materialPickerRef}
              mt={2}
              p={2}
              position="relative"
              zIndex={30}
              border="1px solid #9fc0ff"
              borderRadius="12px"
              bg="#f8fbff"
              data-no-row-toggle="true"
            >
              <HStack spacing={1.5} align="end" flexWrap="nowrap" w="100%">
                <DiagnosisDropdown
                  label="Loại phụ tùng"
                  labelFontSize="xs"
                  labelColor="#5c6b8a"
                  minW="250px"
                  maxW="250px"
                  flex="0 0 250px"
                  value={materialPickerCategoryId}
                  onChange={(value) => setMaterialPickerCategoryId(value || "")}
                  isDisabled={!canEdit}
                >
                  <option value="">-- Tất cả loại phụ tùng --</option>
                  {materialCategoryOptions.map((category) => (
                    <option key={category.value} value={category.value}>
                      {category.label}
                    </option>
                  ))}
                </DiagnosisDropdown>

                <Box flex="1 1 auto" minW="0">
                  <Text fontSize="xs" fontWeight="600" color="#5c6b8a" mb={2}>
                    Tìm phụ tùng
                  </Text>
                  <Box position="relative">
                    <Input
                      size="sm"
                      value={materialPickerKeyword}
                      onChange={(event) => {
                        setMaterialPickerKeyword(event.target.value);
                        setMaterialPickerOpen(true);
                      }}
                      onFocus={() => setMaterialPickerOpen(true)}
                      placeholder={
                        materialPickerCategoryId
                          ? "Nhập tên phụ tùng trong loại đã chọn..."
                          : "Nhập tên phụ tùng để tìm trên toàn bộ danh sách..."
                      }
                      isDisabled={!canEdit}
                      bg="white"
                      borderColor="#d7e3f5"
                      data-no-row-toggle="true"
                    />
                    {materialPickerOpen && canEdit ? (
                      <Box
                        position="absolute"
                        top="calc(100% + 6px)"
                        left={0}
                        right={0}
                        zIndex={40}
                        bg="white"
                        border="1px solid #d7e3f5"
                        borderRadius="md"
                        boxShadow="0 10px 24px rgba(27, 37, 89, 0.12)"
                        maxH="240px"
                        overflowY="auto"
                      >
                        {filteredMaterialProducts.length === 0 ? (
                          <Text px={3} py={2} fontSize="sm" color="#7f8fb2">
                            Không có phụ tùng phù hợp.
                          </Text>
                        ) : (
                          filteredMaterialProducts.map((product) => {
                            const productId = String(toPositiveNumber(product?.id ?? product?.Id) || "");
                            const existed = existingMaterialInventoryIdSet.has(productId);
                            const productName = extractProductName(product) || `Phụ tùng #${productId}`;
                            const categoryName = extractProductCategoryName(product);
                            return (
                              <Box
                                as="button"
                                type="button"
                                key={productId}
                                w="100%"
                                textAlign="left"
                                px={3}
                                py={2}
                                bg={existed ? "#edf4ff" : "transparent"}
                                _hover={{ bg: existed ? "#e4efff" : "#f4f8ff" }}
                                borderLeft={existed ? "2px solid #3b82f6" : "2px solid transparent"}
                                border="none"
                                data-no-row-toggle="true"
                                onClick={() => toggleMaterialFromPicker(product)}
                              >
                                <Text fontSize="sm" fontWeight={existed ? "700" : "600"} color={existed ? "#274690" : "#2b3674"}>
                                  {productName}
                                </Text>
                                {categoryName ? (
                                  <Text fontSize="xs" color="#7f8fb2">
                                    {categoryName}
                                  </Text>
                                ) : null}
                              </Box>
                            );
                          })
                        )}
                      </Box>
                    ) : null}
                  </Box>
                </Box>
              </HStack>
            </Box>
          ) : null}
        </Box>
      )}

      {!isServiceMode && (
        <Stack spacing={0.5}>
          <Grid
            templateColumns={{ base: "1fr", lg: "minmax(260px, 1fr) 220px" }}
            gap={2}
            alignItems="start"
          >
            <Box>
              <Text fontSize={{ base: "xl", md: "lg" }} fontWeight="800" color="#7f4f1f" lineHeight="1.35">
                {selectedProductName}
              </Text>
              <Box mt={2}>
                <Text fontSize="11px" fontWeight="700" textTransform="uppercase" letterSpacing="0.02em" color="#b07a2f" mb={1}>Ghi chú kỹ thuật</Text>
                <Textarea
                  size="sm"
                  maxW={{ base: "100%", lg: "420px" }}
                  minH="68px"
                  placeholder="Ghi chú cho phụ tùng này..."
                  value={item.technicianNotes || ""}
                  isDisabled={!canEdit}
                  onChange={(e) => onChange(item.id, { technicianNotes: e.target.value })}
                  bg="rgba(255,255,255,0.92)"
                  borderColor="#e7d5b6"
                  _focus={{ borderColor: "#d4a15a", boxShadow: "0 0 0 1px #d4a15a" }}
                />
              </Box>
            </Box>
            <Box p={2} bg="rgba(255,255,255,0.55)" border="1px solid #f1dec1" borderRadius="10px" w={{ base: "100%", lg: "220px" }} maxW={{ base: "100%", lg: "220px" }} justifySelf={{ base: "stretch", lg: "end" }}>
              <Text fontSize="11px" fontWeight="700" textTransform="uppercase" letterSpacing="0.02em" color="#b07a2f" mb={1.5}>Độ ưu tiên</Text>
              <DiagnosisDropdown
                minW="100%"
                value={item.urgency || "RECOMMENDED"}
                isDisabled={!canEdit}
                onChange={(value) => onChange(item.id, { urgency: value })}
                fullWidth
              >
                <option value="CRITICAL">Khẩn cấp</option>
                <option value="RECOMMENDED">Khuyến nghị</option>
                <option value="OPTIONAL">Tùy chọn</option>
              </DiagnosisDropdown>
            </Box>
          </Grid>
        </Stack>
      )}
    </Stack>
  );
}
