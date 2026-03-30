import React from "react";
import VoucherManagementPage from "./VoucherManagementPage";

class VoucherPageErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, message: "" };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, message: error?.message || "Lỗi render không xác định" };
  }

  componentDidCatch(error) {
    // Keep a breadcrumb in console for fast diagnosis when user opens devtools.
    // eslint-disable-next-line no-console
    console.error("[VoucherManagement] render error:", error);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="management-content voucher-admin-page">
          <header className="admin-header">
            <div>
              <h1>Quản lý voucher</h1>
              <p className="header-sub">Đã chặn lỗi runtime để tránh trắng màn hình.</p>
            </div>
          </header>
          <div className="alert alert-error">
            Voucher UI gặp lỗi runtime: {this.state.message}
          </div>
          <div className="table-toolbar">
            <button className="btn-primary" onClick={() => window.location.reload()} type="button">
              Tải lại trang
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

export default function SafeVoucherManagementPage({ section }) {
  return (
    <VoucherPageErrorBoundary>
      <VoucherManagementPage section={section} />
    </VoucherPageErrorBoundary>
  );
}

