import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  Box,
  IconButton,
  Input,
  InputGroup,
  InputLeftElement,
  Spinner,
  Text,
  useColorModeValue,
} from "@chakra-ui/react";
import { SearchIcon } from "@chakra-ui/icons";
import { useNavigate } from "react-router-dom";
import { useSearch } from "@shared/core/context/SearchContext";
import { getAllStaff } from "@features/admin/api/staffApi";
import { getAllCustomers } from "@features/admin/api/customerApi";
import {
  getAllVehicleBrands,
  getAllVehicleModels,
} from "@features/admin/api/vehicleCatalogApi";
import {
  getAllServiceCategories,
  getAllStandardServices,
} from "@features/admin/api/serviceApi";
import { inventoryApi } from "@features/inventory/api/inventoryApi";

const INDEX_CACHE_MS = 3 * 60 * 1000;
const MAX_RESULTS = 10;

function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .trim();
}

function createEntry({ id, type, title, subtitle, route, keywords }) {
  const cleanTitle = String(title || "").trim();
  const cleanSubtitle = String(subtitle || "").trim();
  const cleanKeywords = (keywords || []).map((k) => String(k || "").trim()).filter(Boolean);
  return {
    key: `${type}-${id ?? cleanTitle}`,
    id,
    type,
    title: cleanTitle || "Untitled",
    subtitle: cleanSubtitle,
    route,
    searchText: normalizeText([cleanTitle, cleanSubtitle, ...cleanKeywords].join(" ")),
  };
}

function scoreEntry(entry, query) {
  const title = normalizeText(entry.title);
  if (title.startsWith(query)) return 100;
  if (title.includes(query)) return 70;
  if (entry.searchText.includes(query)) return 40;
  return 0;
}

async function buildGlobalIndex() {
  const [
    staffRes,
    customersRes,
    brandsRes,
    modelsRes,
    servicesRes,
    serviceCategoriesRes,
    productsRes,
    partCategoriesRes,
  ] = await Promise.allSettled([
    getAllStaff(),
    getAllCustomers(),
    getAllVehicleBrands(),
    getAllVehicleModels(),
    getAllStandardServices(),
    getAllServiceCategories(),
    inventoryApi.getProducts(),
    inventoryApi.getPartCategories(),
  ]);

  const safe = (result) => (result.status === "fulfilled" && Array.isArray(result.value) ? result.value : []);

  const staff = safe(staffRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Staff",
      title: item.fullName || item.email || item.phone || `Staff #${item.id}`,
      subtitle: [item.phone, item.email].filter(Boolean).join(" - "),
      route: "/admin/staff",
      keywords: [item.roleName, item.status],
    }),
  );

  const customers = safe(customersRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Customer",
      title: item.fullName || item.email || item.phone || `Customer #${item.id}`,
      subtitle: [item.phone, item.email, item.licensePlate].filter(Boolean).join(" - "),
      route: "/admin/customers",
      keywords: [item.tierName, item.brandName, item.modelName, item.status],
    }),
  );

  const brands = safe(brandsRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Vehicle Brand",
      title: item.name || item.code || `Brand #${item.id}`,
      subtitle: item.countryOfOrigin || "",
      route: "/admin/brands",
      keywords: [item.code, item.countryOfOrigin],
    }),
  );

  const models = safe(modelsRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Vehicle Model",
      title: item.modelName || `Model #${item.id}`,
      subtitle: [item.segment, item.fuelType].filter(Boolean).join(" - "),
      route: "/admin/models",
      keywords: [item.segment, item.fuelType, item.cylinderCapacity],
    }),
  );

  const services = safe(servicesRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Service",
      title: item.name || item.code || `Service #${item.id}`,
      subtitle: [item.code, item.standardMinutes ? `${item.standardMinutes} min` : ""]
        .filter(Boolean)
        .join(" - "),
      route: "/admin/services",
      keywords: [item.code, item.description],
    }),
  );

  const serviceCategories = safe(serviceCategoriesRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Service Category",
      title: item.name || item.code || `Category #${item.id}`,
      subtitle: item.code || "",
      route: "/admin/service-categories",
      keywords: [item.description, item.code],
    }),
  );

  const products = safe(productsRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Part",
      title: item.name || item.sku || `Part #${item.id}`,
      subtitle: item.sku || "",
      route: "/admin/parts",
      keywords: [item.sku, item.unit],
    }),
  );

  const partCategories = safe(partCategoriesRes).map((item) =>
    createEntry({
      id: item.id,
      type: "Part Category",
      title: item.categoryName || `Category #${item.id}`,
      subtitle: item.description || "",
      route: "/admin/part-categories",
      keywords: [item.categoryName, item.description],
    }),
  );

  return [
    ...staff,
    ...customers,
    ...brands,
    ...models,
    ...services,
    ...serviceCategories,
    ...products,
    ...partCategories,
  ];
}

export function SearchBar(props) {
  const { searchQuery, setSearchQuery } = useSearch();
  const navigate = useNavigate();
  const containerRef = useRef(null);
  const indexCacheRef = useRef({ loadedAt: 0, entries: [] });
  const [loading, setLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [entries, setEntries] = useState([]);

  const { background, placeholder, borderRadius, ...rest } = props;

  const searchIconColor = useColorModeValue("gray.700", "white");
  const inputBg = useColorModeValue("secondaryGray.300", "navy.900");
  const inputText = useColorModeValue("gray.700", "gray.100");
  const menuBg = useColorModeValue("white", "navy.800");
  const menuBorder = useColorModeValue("#dbe3f3", "whiteAlpha.200");
  const menuShadow = useColorModeValue(
    "0 14px 30px rgba(17, 38, 146, 0.14)",
    "0 14px 30px rgba(0, 0, 0, 0.45)",
  );
  const typeColor = useColorModeValue("gray.500", "gray.300");
  const titleColor = useColorModeValue("gray.800", "white");
  const subtitleColor = useColorModeValue("gray.500", "gray.300");
  const hoverBg = useColorModeValue("secondaryGray.300", "whiteAlpha.100");

  useEffect(() => {
    const onMouseDown = (event) => {
      if (!containerRef.current?.contains(event.target)) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", onMouseDown);
    return () => document.removeEventListener("mousedown", onMouseDown);
  }, []);

  useEffect(() => {
    let cancelled = false;
    const query = normalizeText(searchQuery);

    if (query.length < 2) {
      setEntries([]);
      return;
    }

    const load = async () => {
      setLoading(true);
      try {
        const isStale = Date.now() - indexCacheRef.current.loadedAt > INDEX_CACHE_MS;
        if (isStale || indexCacheRef.current.entries.length === 0) {
          const freshEntries = await buildGlobalIndex();
          if (cancelled) return;
          indexCacheRef.current = { loadedAt: Date.now(), entries: freshEntries };
        }

        const matches = indexCacheRef.current.entries
          .map((entry) => ({ entry, score: scoreEntry(entry, query) }))
          .filter((item) => item.score > 0)
          .sort((a, b) => b.score - a.score || a.entry.title.localeCompare(b.entry.title))
          .slice(0, MAX_RESULTS)
          .map((item) => item.entry);

        if (!cancelled) {
          setEntries(matches);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    const timer = setTimeout(load, 180);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [searchQuery]);

  const shouldShowMenu = useMemo(
    () => isOpen && normalizeText(searchQuery).length >= 2,
    [isOpen, searchQuery],
  );

  const handleSelect = (item) => {
    setIsOpen(false);
    navigate(item.route);
  };

  return (
    <InputGroup w={{ base: "100%", md: "240px" }} {...rest} position="relative" ref={containerRef}>
      <InputLeftElement
        children={
          <IconButton
            aria-label="search"
            bg="inherit"
            borderRadius="inherit"
            _hover={{ bg: "inherit" }}
            _active={{
              bg: "inherit",
              transform: "none",
              borderColor: "transparent",
            }}
            _focus={{
              boxShadow: "none",
            }}
            icon={<SearchIcon color={searchIconColor} w="15px" h="15px" />}
          ></IconButton>
        }
      />
      <Input
        variant="search"
        fontSize="sm"
        bg={background || inputBg}
        color={inputText}
        fontWeight="500"
        _placeholder={{ color: "gray.400", fontSize: "14px" }}
        borderRadius={borderRadius || "30px"}
        placeholder={placeholder || "Tim kiem..."}
        value={searchQuery}
        onFocus={() => setIsOpen(true)}
        onChange={(e) => setSearchQuery(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && entries[0]) {
            e.preventDefault();
            handleSelect(entries[0]);
          }
        }}
      />

      {shouldShowMenu && (
        <Box
          position="absolute"
          left="0"
          right="0"
          top="calc(100% + 8px)"
          bg={menuBg}
          border="1px solid"
          borderColor={menuBorder}
          borderRadius="14px"
          boxShadow={menuShadow}
          zIndex="1500"
          maxH="360px"
          overflowY="auto"
          p="8px"
        >
          {loading ? (
            <Box py="16px" textAlign="center">
              <Spinner size="sm" />
            </Box>
          ) : entries.length === 0 ? (
            <Text px="8px" py="10px" fontSize="sm" color={subtitleColor}>
                  Không tìm thấy kết quả phù hợp.
            </Text>
          ) : (
            entries.map((item) => (
              <Box
                key={item.key}
                p="10px"
                borderRadius="10px"
                cursor="pointer"
                _hover={{ bg: hoverBg }}
                onClick={() => handleSelect(item)}
              >
                <Text fontSize="xs" color={typeColor} fontWeight="700" mb="2px">
                  {item.type}
                </Text>
                <Text fontSize="sm" color={titleColor} fontWeight="700" noOfLines={1}>
                  {item.title}
                </Text>
                {item.subtitle ? (
                  <Text fontSize="xs" color={subtitleColor} noOfLines={1}>
                    {item.subtitle}
                  </Text>
                ) : null}
              </Box>
            ))
          )}
        </Box>
      )}
    </InputGroup>
  );
}
