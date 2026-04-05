import { useEffect, useMemo, useState } from "react";

export const DEFAULT_PARTS_PAGE_SIZE = 9;

export default function useProductPagination(
  items,
  {
    pageSize = DEFAULT_PARTS_PAGE_SIZE,
    resetKeys = [],
  } = {}
) {
  const [currentPage, setCurrentPage] = useState(1);

  const totalItems = items.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

  useEffect(() => {
    setCurrentPage(1);
  }, resetKeys);

  useEffect(() => {
    setCurrentPage((previous) => Math.min(previous, totalPages));
  }, [totalPages]);

  const paginatedItems = useMemo(() => {
    const startIndex = (currentPage - 1) * pageSize;
    return items.slice(startIndex, startIndex + pageSize);
  }, [currentPage, items, pageSize]);

  return {
    currentPage,
    pageSize,
    paginatedItems,
    totalItems,
    totalPages,
    setCurrentPage,
    paginationMode: "client",
  };
}
