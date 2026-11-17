import TablePagination from "@shared/components/TablePagination";
import { ActionPanel, NoDataRow, StatCards } from "./InventoryAdminSharedWidgets";
import {
  ALERT_SEVERITY_META,
  APPROVAL_STATUS_META,
  AUDIT_STATUS_META,
  RESERVE_RULE_OPTIONS,
  RETURN_RULE_OPTIONS,
  TRANSFER_RULE_OPTIONS,
  friendlyAlertType,
  friendlyApprovalType,
  friendlySource,
  friendlyTransactionType,
  normalizeBranchStatus,
  normalizeBranchStatusClass,
  normalizeKey,
  resolveStatusMeta,
  toNumber
} from "./inventoryAdminHelpers";

export function PolicySection({
  settings,
  updatePolicyField,
  policyOverview,
  branchSummary,
  updateSafeStockBranch,
  runAction,
  saveSettings
}) {
  return (
    <>
      <div className="policy-card">
        <div className="policy-card-head">
          <h3>Thiết lập chính sách chung</h3>
          <p>Áp dụng cho toàn hệ thống kho. Cấu hình này ảnh hưởng đến cảnh báo và phê duyệt.</p>
        </div>
        <div className="policy-grid">
          <label className="compact-field policy-toggle">
            <span>Cho phép âm kho</span>
            <input
              type="checkbox"
              checked={!!settings.policyRules?.allowNegativeStock}
              onChange={(e) => updatePolicyField("allowNegativeStock", e.target.checked)}
            />
          </label>
          <label className="compact-field">
            <span>Ngưỡng cảnh báo tồn thấp (%)</span>
            <input
              className="search-input"
              type="number"
              min={0}
              max={100}
              value={settings.policyRules?.lowStockThresholdPercent ?? 0}
              onChange={(e) => updatePolicyField("lowStockThresholdPercent", Math.min(100, Math.max(0, toNumber(e.target.value, 0))))}
            />
          </label>
          <label className="compact-field">
            <span>Quy tắc giữ hàng (Reserve)</span>
            <select
              className="search-input"
              value={settings.policyRules?.reserveRule || "STRICT"}
              onChange={(e) => updatePolicyField("reserveRule", e.target.value)}
            >
              {RESERVE_RULE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </label>
          <label className="compact-field policy-toggle">
            <span>Cho phép xuất vượt định mức</span>
            <input
              type="checkbox"
              checked={!!settings.policyRules?.allowOverIssue}
              onChange={(e) => updatePolicyField("allowOverIssue", e.target.checked)}
            />
          </label>
          <label className="compact-field">
            <span>Quy tắc hoàn trả vật tư</span>
            <select
              className="search-input"
              value={settings.policyRules?.materialReturnRule || "ALLOW_WITH_REASON"}
              onChange={(e) => updatePolicyField("materialReturnRule", e.target.value)}
            >
              {RETURN_RULE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </label>
          <label className="compact-field">
            <span>Quy tắc chuyển kho</span>
            <select
              className="search-input"
              value={settings.policyRules?.transferRule || "REQUIRE_APPROVAL"}
              onChange={(e) => updatePolicyField("transferRule", e.target.value)}
            >
              {TRANSFER_RULE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </label>
        </div>
      </div>

      <div className="policy-overview">
        <div className="policy-overview-item"><span>Tổng chi nhánh</span><strong>{policyOverview.branchCount}</strong></div>
        <div className="policy-overview-item"><span>Chi nhánh hoạt động</span><strong>{policyOverview.activeBranchCount}</strong></div>
        <div className="policy-overview-item"><span>Tồn hiện tại</span><strong>{policyOverview.totalCurrent.toLocaleString()}</strong></div>
        <div className="policy-overview-item"><span>Đã giữ chỗ</span><strong>{policyOverview.totalReserved.toLocaleString()}</strong></div>
        <div className="policy-overview-item"><span>Tổng tồn an toàn</span><strong>{policyOverview.totalSafeStock.toLocaleString()}</strong></div>
      </div>

      <div className="policy-card">
        <div className="policy-card-head">
          <h3>Cấu hình tồn an toàn theo chi nhánh</h3>
          <p>Đặt mức tồn tối thiểu riêng cho từng chi nhánh để hệ thống cảnh báo sát thực tế vận hành.</p>
        </div>
        <div className="table-wrapper policy-table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Chi nhánh</th>
                <th>Trạng thái</th>
                <th>Số kho</th>
                <th>Tồn hiện tại</th>
                <th>Đã giữ chỗ</th>
                <th>Tồn an toàn</th>
              </tr>
            </thead>
            <tbody>
              {branchSummary.length === 0 ? (
                <NoDataRow colSpan={6} message="Chưa có dữ liệu chi nhánh để cấu hình tồn an toàn." />
              ) : (
                branchSummary.map((b) => (
                  <tr key={b.branchId}>
                    <td>{b.branchName}</td>
                    <td>
                      <span className={`policy-status ${normalizeBranchStatusClass(b.branchStatus)}`}>
                        {normalizeBranchStatus(b.branchStatus)}
                      </span>
                    </td>
                    <td>{toNumber(b.warehouseCount).toLocaleString()}</td>
                    <td>{toNumber(b.totalQuantity).toLocaleString()}</td>
                    <td>{toNumber(b.reservedQuantity).toLocaleString()}</td>
                    <td>
                      <input
                        className="search-input policy-stock-input"
                        type="number"
                        min={0}
                        value={settings.policyRules?.safeStockByBranch?.[b.branchId] ?? 0}
                        onChange={(e) => updateSafeStockBranch(b.branchId, e.target.value)}
                      />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="table-toolbar policy-savebar">
        <button className="btn-primary" onClick={() => runAction(() => saveSettings(settings))}>
          Lưu chính sách
        </button>
      </div>
    </>
  );
}

export function MonitoringSection({ overview, branchSummary }) {
  return (
    <>
      <StatCards
        items={[
          { label: "Tổng tồn", value: toNumber(overview.totalQuantity).toLocaleString() },
          { label: "Đã giữ chỗ", value: toNumber(overview.reservedQuantity).toLocaleString() },
          { label: "Sắp hết", value: toNumber(overview.lowStockCount).toLocaleString() },
          { label: "Âm kho", value: toNumber(overview.negativeStockCount).toLocaleString() },
          { label: "Kho lỗi", value: toNumber(overview.defectiveStockCount).toLocaleString() },
          { label: "Giá trị tồn", value: toNumber(overview.totalInventoryValue).toLocaleString() }
        ]}
      />
      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Chi nhánh</th>
              <th>Tồn hiện tại</th>
              <th>Đã giữ chỗ</th>
              <th>Sắp hết</th>
              <th>Âm kho</th>
              <th>Giá trị tồn</th>
            </tr>
          </thead>
          <tbody>
            {branchSummary.length === 0 ? (
              <NoDataRow colSpan={6} message="Chưa có dữ liệu giám sát tồn kho." />
            ) : (
              branchSummary.map((b) => (
                <tr key={b.branchId}>
                  <td>{b.branchName}</td>
                  <td>{toNumber(b.totalQuantity).toLocaleString()}</td>
                  <td>{toNumber(b.reservedQuantity).toLocaleString()}</td>
                  <td>{toNumber(b.lowStockCount).toLocaleString()}</td>
                  <td>{toNumber(b.negativeStockCount).toLocaleString()}</td>
                  <td>{toNumber(b.totalValue).toLocaleString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function AuditsSection({ audits, runAction, handleApproveAudit, handleRejectAudit }) {
  return (
    <div className="table-wrapper">
      <table className="admin-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Ngày kiểm kê</th>
            <th>Chi nhánh</th>
            <th>Kho</th>
            <th>Trạng thái</th>
            <th>Tổng chênh lệch</th>
            <th>Thao tác</th>
          </tr>
        </thead>
        <tbody>
          {audits.length === 0 ? (
            <NoDataRow colSpan={7} message="Chưa có kỳ kiểm kê nào." />
          ) : (
            audits.map((a) => {
              const status = resolveStatusMeta(a.status, AUDIT_STATUS_META);
              const canAction = ["PENDING", "DRAFT"].includes(normalizeKey(a.status));
              return (
                <tr key={a.id}>
                  <td className="td-id">#{a.id}</td>
                  <td>{a.auditDate ? new Date(a.auditDate).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                  <td>{a.branchName}</td>
                  <td>{a.warehouseName}</td>
                  <td><span className={`badge ${status.className}`}>{status.label}</span></td>
                  <td>{toNumber(a.differenceTotal).toLocaleString()}</td>
                  <td>
                    {canAction ? (
                      <div className="row-actions">
                        <button className="btn-icon" onClick={() => runAction(() => handleApproveAudit(a.id))}>Duyệt</button>
                        <button className="btn-icon" onClick={() => runAction(() => handleRejectAudit(a.id))}>Từ chối</button>
                      </div>
                    ) : (
                      <span className="result-count">Đã xử lý</span>
                    )}
                  </td>
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}

export function TransactionsSection({
  txParams,
  handleTxFilterChange,
  warehouseStructure,
  warehouseOptionsByBranch,
  fetchData,
  resetTxFilters,
  transactions,
  changePage,
  txTotalPages
}) {
  return (
    <>
      <ActionPanel
        title="Bộ lọc truy vết giao dịch"
        description="Lọc theo chi nhánh, kho, loại giao dịch và khoảng thời gian để truy xuất nhanh."
      >
        <div className="table-toolbar inventory-form-grid inventory-form-grid--tx">
          <label className="compact-field">
            <span>Chi nhánh</span>
            <select
              className="search-input"
              value={txParams.branchId}
              onChange={(e) => {
                handleTxFilterChange("branchId", e.target.value);
                handleTxFilterChange("warehouseId", "");
              }}
            >
              <option value="">Tất cả chi nhánh</option>
              {(warehouseStructure.branches || []).map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </label>
          <label className="compact-field">
            <span>Kho</span>
            <select
              className="search-input"
              value={txParams.warehouseId}
              onChange={(e) => handleTxFilterChange("warehouseId", e.target.value)}
            >
              <option value="">Tất cả kho</option>
              {warehouseOptionsByBranch.map((w) => (
                <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
              ))}
            </select>
          </label>
          <label className="compact-field">
            <span>Loại giao dịch</span>
            <input
              className="search-input"
              placeholder="Ví dụ: OUT, SERVICE_ISSUE, RETURN"
              value={txParams.type}
              onChange={(e) => handleTxFilterChange("type", e.target.value)}
            />
          </label>
          <label className="compact-field">
            <span>Từ ngày</span>
            <input
              className="search-input"
              type="date"
              value={txParams.from}
              onChange={(e) => handleTxFilterChange("from", e.target.value)}
            />
          </label>
          <label className="compact-field">
            <span>Độn ngày</span>
            <input
              className="search-input"
              type="date"
              value={txParams.to}
              onChange={(e) => handleTxFilterChange("to", e.target.value)}
            />
          </label>
        </div>
        <div className="table-toolbar inventory-form-actions">
          <button className="btn-primary" onClick={fetchData}>Lọc dữ liệu</button>
          <button className="btn-ghost" onClick={resetTxFilters}>Xóa bộ lọc</button>
        </div>
      </ActionPanel>

      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Thời gian</th>
              <th>Loại giao dịch</th>
              <th>Chi nhánh</th>
              <th>Kho</th>
              <th>SKU</th>
              <th>Số lượng</th>
              <th>Nguồn phát sinh</th>
              <th>Người thao tác</th>
            </tr>
          </thead>
          <tbody>
            {(transactions.items || []).length === 0 ? (
              <NoDataRow colSpan={9} message="Không có giao dịch nào khớp với bộ lọc hiện tại." />
            ) : (
              (transactions.items || []).map((t) => (
                <tr key={t.id}>
                  <td className="td-id">#{t.id}</td>
                  <td>{t.createdAt ? new Date(t.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                  <td><span className="badge badge-inactive">{friendlyTransactionType(t.type)}</span></td>
                  <td>{t.branchName}</td>
                  <td>{t.warehouseName}</td>
                  <td>{t.productSku}</td>
                  <td>{toNumber(t.quantity).toLocaleString()}</td>
                  <td>{friendlySource(t.source)}</td>
                  <td>{t.createdByName || "-"}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="table-toolbar">
        <button className="btn-ghost" onClick={() => changePage(-1)} disabled={txParams.page <= 1}>
          Trang trước
        </button>
        <span className="result-count">
          Trang {txParams.page} / {txTotalPages} - Tổng {toNumber(transactions.total).toLocaleString()} giao dịch
        </span>
        <button className="btn-ghost" onClick={() => changePage(1)} disabled={txParams.page >= txTotalPages}>
          Trang sau
        </button>
      </div>
    </>
  );
}

export function ApprovalsSection({ approvals, runAction, handleApproveRequest, handleRejectRequest }) {
  return (
    <div className="table-wrapper">
      <table className="admin-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Loại yêu cầu</th>
            <th>Trạng thái</th>
            <th>Người gửi</th>
            <th>Lý do</th>
            <th>Thao tác</th>
          </tr>
        </thead>
        <tbody>
          {approvals.length === 0 ? (
            <NoDataRow colSpan={6} message="Không có yêu cầu ngoại lệ nào." />
          ) : (
            approvals.map((a) => {
              const status = resolveStatusMeta(a.status, APPROVAL_STATUS_META);
              const canAction = normalizeKey(a.status) === "PENDING";
              return (
                <tr key={a.id}>
                  <td className="td-id">#{a.id}</td>
                  <td>{friendlyApprovalType(a.requestType)}</td>
                  <td><span className={`badge ${status.className}`}>{status.label}</span></td>
                  <td>{a.requestedByName}</td>
                  <td>{a.reason || "-"}</td>
                  <td>
                    {canAction ? (
                      <div className="row-actions">
                        <button className="btn-icon" onClick={() => runAction(() => handleApproveRequest(a.id))}>Duyệt</button>
                        <button className="btn-icon" onClick={() => runAction(() => handleRejectRequest(a.id))}>Từ chối</button>
                      </div>
                    ) : (
                      <span className="result-count">Đã xử lý</span>
                    )}
                  </td>
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}

export function AlertsSection({ alerts }) {
  return (
    <div className="table-wrapper">
      <table className="admin-table">
        <thead>
          <tr>
            <th>Mức độ</th>
            <th>Loại cảnh báo</th>
            <th>Nội dung</th>
            <th>Chi nhánh</th>
            <th>Kho</th>
            <th>Thời gian</th>
          </tr>
        </thead>
        <tbody>
          {alerts.length === 0 ? (
            <NoDataRow colSpan={6} message="Hiện chưa có cảnh báo tồn kho." />
          ) : (
            alerts.map((a, idx) => {
              const severity = resolveStatusMeta(a.severity, ALERT_SEVERITY_META);
              return (
                <tr key={`${a.referenceType}-${a.referenceId}-${idx}`}>
                  <td><span className={`badge ${severity.className}`}>{severity.label}</span></td>
                  <td>{friendlyAlertType(a.type)}</td>
                  <td>{a.message}</td>
                  <td>{a.branchName || "-"}</td>
                  <td>{a.warehouseName || "-"}</td>
                  <td>{a.createdAt ? new Date(a.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}

export function ReportsSection({
  reports,
  pagedReportStockByBranch,
  reportStockByBranchRows,
  reportBranchPage,
  reportBranchPageSize,
  setReportBranchPage,
  setReportBranchPageSize,
  pagedReportTopOutbound,
  reportTopOutboundRows,
  reportTopOutboundPage,
  reportTopOutboundPageSize,
  setReportTopOutboundPage,
  setReportTopOutboundPageSize,
  pagedReportAuditLogs,
  reportAuditLogRows,
  reportAuditLogPage,
  reportAuditLogPageSize,
  setReportAuditLogPage,
  setReportAuditLogPageSize
}) {
  return (
    <>
      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Chi nhánh</th>
              <th>Tổng tồn</th>
              <th>Giá trị tồn</th>
            </tr>
          </thead>
          <tbody>
            {(reports.stockByBranch || []).length === 0 ? (
              <NoDataRow colSpan={3} message="Chưa có dữ liệu tồn kho theo chi nhánh." />
            ) : (
              pagedReportStockByBranch.map((r) => (
                <tr key={r.branchId}>
                  <td>{r.branchName}</td>
                  <td>{toNumber(r.totalQuantity).toLocaleString()}</td>
                  <td>{toNumber(r.totalValue).toLocaleString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <TablePagination
        totalItems={reportStockByBranchRows.length}
        currentPage={reportBranchPage}
        pageSize={reportBranchPageSize}
        onPageChange={setReportBranchPage}
        onPageSizeChange={(size) => {
          setReportBranchPageSize(size);
          setReportBranchPage(1);
        }}
      />

      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Top sản phẩm xuất nhiều</th>
              <th>Số lượng xuất</th>
            </tr>
          </thead>
          <tbody>
            {(reports.topOutbound || []).length === 0 ? (
              <NoDataRow colSpan={2} message="Chưa có dữ liệu top xuất kho." />
            ) : (
              pagedReportTopOutbound.map((r) => (
                <tr key={r.productId}>
                  <td>{r.sku} - {r.productName}</td>
                  <td>{toNumber(r.totalOutboundQty).toLocaleString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <TablePagination
        totalItems={reportTopOutboundRows.length}
        currentPage={reportTopOutboundPage}
        pageSize={reportTopOutboundPageSize}
        onPageChange={setReportTopOutboundPage}
        onPageSizeChange={(size) => {
          setReportTopOutboundPageSize(size);
          setReportTopOutboundPage(1);
        }}
      />

      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Hành động</th>
              <th>Bảng dữ liệu</th>
              <th>Bản ghi</th>
              <th>Thời gian</th>
            </tr>
          </thead>
          <tbody>
            {(reports.auditLogs || []).length === 0 ? (
              <NoDataRow colSpan={5} message="Chưa có audit log liên quan đến kho." />
            ) : (
              pagedReportAuditLogs.map((log) => (
                <tr key={log.id}>
                  <td className="td-id">#{log.id}</td>
                  <td>{log.action}</td>
                  <td>{log.tableName}</td>
                  <td>{log.recordId}</td>
                  <td>{log.timestamp ? new Date(log.timestamp).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <TablePagination
        totalItems={reportAuditLogRows.length}
        currentPage={reportAuditLogPage}
        pageSize={reportAuditLogPageSize}
        onPageChange={setReportAuditLogPage}
        onPageSizeChange={(size) => {
          setReportAuditLogPageSize(size);
          setReportAuditLogPage(1);
        }}
      />
    </>
  );
}
