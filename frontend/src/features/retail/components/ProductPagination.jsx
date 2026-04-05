const WINDOW_SIZE = 5;

function buildPageList(currentPage, totalPages) {
  if (totalPages <= WINDOW_SIZE) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  const halfWindow = Math.floor(WINDOW_SIZE / 2);
  let start = Math.max(1, currentPage - halfWindow);
  let end = Math.min(totalPages, start + WINDOW_SIZE - 1);

  if (end - start + 1 < WINDOW_SIZE) {
    start = Math.max(1, end - WINDOW_SIZE + 1);
  }

  return Array.from({ length: end - start + 1 }, (_, index) => start + index);
}

export default function ProductPagination({
  currentPage,
  totalPages,
  onPageChange,
}) {
  if (totalPages <= 1) return null;

  const pages = buildPageList(currentPage, totalPages);

  return (
    <nav className="parts-shop-pagination" aria-label="Phân trang phụ tùng">
      <button
        type="button"
        className="parts-shop-pagination-btn"
        disabled={currentPage <= 1}
        onClick={() => onPageChange(currentPage - 1)}
      >
        Trang trước
      </button>

      <div className="parts-shop-pagination-pages">
        {pages.map((page) => (
          <button
            key={page}
            type="button"
            className={`parts-shop-pagination-page ${page === currentPage ? "is-active" : ""}`}
            aria-current={page === currentPage ? "page" : undefined}
            onClick={() => onPageChange(page)}
          >
            {page}
          </button>
        ))}
      </div>

      <button
        type="button"
        className="parts-shop-pagination-btn"
        disabled={currentPage >= totalPages}
        onClick={() => onPageChange(currentPage + 1)}
      >
        Trang sau
      </button>
    </nav>
  );
}
