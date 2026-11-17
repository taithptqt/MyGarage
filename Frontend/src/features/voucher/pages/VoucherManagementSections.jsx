import TablePagination from "@shared/components/TablePagination";
import {
  STATUS_BADGE,
  budgetAlertClass,
  budgetAlertText,
  dt,
  money,
  num,
  pct,
  statusText,
  up
} from "./voucherManagementHelpers";

export function EmptyState({ message }) {
  return (
    <div className="empty-state">
      <span className="empty-icon">*</span>
      <span>{message}</span>
    </div>
  );
}

export function VoucherDashboardSection({
  activeCount,
  issued,
  redeemed,
  discount,
  pendingApprovals,
  refresh,
  busy,
  topVouchers,
  budgetUsed,
  budgetLimit,
  nearQuotaVouchers,
  dashboardApprovals
}) {
  return (
    <>
      <div className="stats-bar">
        <div className="stat-card"><div className="stat-value">{activeCount}</div><div className="stat-label">Voucher hoạt động</div></div>
        <div className="stat-card"><div className="stat-value">{issued}</div><div className="stat-label">Đã phát hành</div></div>
        <div className="stat-card"><div className="stat-value">{redeemed}</div><div className="stat-label">Đã sử dụng</div></div>
        <div className="stat-card"><div className="stat-value">{pct(redeemed, issued)}%</div><div className="stat-label">Tỷ lệ sử dụng</div></div>
        <div className="stat-card"><div className="stat-value">{money(discount)}</div><div className="stat-label">Tổng giảm giá</div></div>
        <div className="stat-card"><div className="stat-value">{pendingApprovals}</div><div className="stat-label">Chờ phê duyệt</div></div>
      </div>

      <div className="table-toolbar voucher-toolbar">
        <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới bảng điều khiển"}</button>
      </div>

      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>Top voucher</th><th>Mã</th><th>Đã sử dụng</th><th>Đã phát hành</th><th>Tỷ lệ</th></tr></thead>
          <tbody>
            {topVouchers.length === 0 ? <tr><td colSpan={5}><EmptyState message="Chưa có dữ liệu top voucher" /></td></tr> : topVouchers.map((v) => (
              <tr key={v.id}><td>{v.voucherName || "-"}</td><td>{v.code || "-"}</td><td>{v.redeemed}</td><td>{v.issued}</td><td>{pct(v.redeemed, v.issued)}%</td></tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="voucher-card-grid">
        <div className="voucher-panel-card">
          <h3>Ngân sách voucher</h3>
          <p>Đã dùng: <strong>{money(budgetUsed)}</strong></p>
          <p>Giới hạn: <strong>{money(budgetLimit)}</strong></p>
          <div className="voucher-progress"><div className="voucher-progress-fill" style={{ width: `${pct(budgetUsed, budgetLimit)}%` }} /></div>
        </div>
        <div className="voucher-panel-card">
          <h3>Voucher sắp hết quota</h3>
          {nearQuotaVouchers.length === 0 ? <p className="voucher-subtext">Không có voucher cảnh báo</p> : nearQuotaVouchers.map((v) => (
            <div key={v.id} className="voucher-subtext">{v.code || v.voucherName}: {Math.round(v.ratio * 100)}%</div>
          ))}
        </div>
        <div className="voucher-panel-card">
          <h3>Phê duyệt cần xử lý</h3>
          {dashboardApprovals.map((x) => (
            <div key={x.id} className="voucher-subtext">#{x.id} - {x.requestType || "N/A"} - {x.riskLevel || "-"}</div>
          ))}
          {pendingApprovals === 0 ? <p className="voucher-subtext">Không có yêu cầu chờ duyệt</p> : null}
        </div>
      </div>
    </>
  );
}

export function VoucherListSection({
  q,
  setQ,
  status,
  setStatus,
  statusOptions,
  openCreate,
  refresh,
  busy,
  voucherLoading,
  voucherError,
  voucherRows,
  setSelected,
  openEdit,
  changeStatus,
  voucherTotal,
  voucherPage,
  voucherPageSize,
  setVoucherPage,
  setVoucherPageSize
}) {
  return (
    <>
      <div className="table-toolbar voucher-toolbar">
        <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm voucher..." />
        <select className="voucher-select" value={status} onChange={(e) => setStatus(e.target.value)}>
          {statusOptions.map((x) => <option key={x} value={x}>{x === "ALL" ? "Tất cả" : statusText(x)}</option>)}
        </select>
        <button className="btn-primary" onClick={openCreate}>+ Tạo voucher</button>
        <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới danh sách"}</button>
      </div>

      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>ID</th><th>Mã</th><th>Tên voucher</th><th>Loại</th><th>Giá trị</th><th>Thời gian hiệu lực</th><th>Lượt dùng</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            {voucherLoading ? <tr><td colSpan={9}>Đang tải dữ liệu voucher...</td></tr> : voucherError ? null : voucherRows.length === 0 ? <tr><td colSpan={9}><EmptyState message="Không có voucher" /></td></tr> : voucherRows.map((v) => (
              <tr key={v.id}>
                <td className="td-id">#{v.id}</td><td>{v.code}</td><td>{v.voucherName}</td><td>{v.type}</td>
                <td>{v.type === "PERCENT" ? `${v.value || 0}%` : money(v.value)}</td>
                <td>{dt(v.startDate)} - {dt(v.endDate)}</td>
                <td>{num(v.redeemedCount, num(v.usedCount, 0))}/{num(v.issuedCount, num(v.usageLimit, 0))}</td>
                <td><span className={`badge ${STATUS_BADGE[up(v.status)] || "badge-inactive"}`}>{statusText(v.status)}</span></td>
                <td><div className="row-actions voucher-actions-col">
                  <button className="btn-icon" onClick={() => setSelected(v)}>Chi tiết</button>
                  <button className="btn-icon" onClick={() => openEdit(v)}>Sửa</button>
                  {up(v.status) === "ACTIVE" ? <button className="btn-icon" onClick={() => changeStatus(v.id, "pause")}>Tạm dừng</button> : <button className="btn-icon" onClick={() => changeStatus(v.id, "activate")}>Kích hoạt</button>}
                  <button className="btn-icon" onClick={() => changeStatus(v.id, "revoke")}>Thu hôi</button>
                </div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: "flex", justifyContent: "center" }}>
        {!voucherError && <TablePagination
          totalItems={voucherTotal}
          currentPage={voucherPage}
          pageSize={voucherPageSize}
          onPageChange={setVoucherPage}
          onPageSizeChange={(size) => {
            setVoucherPageSize(size);
            setVoucherPage(1);
          }}
        />}
      </div>
    </>
  );
}

export function VoucherCampaignsSection({
  campaignStats,
  q,
  setQ,
  campaignFilterStatus,
  setCampaignFilterStatus,
  campaignStatusOptions,
  openCreateCampaign,
  refresh,
  busy,
  campaignWarnings,
  campaignLoading,
  campaignPageRows,
  openCampaignDetail,
  campaignDetailLoading,
  openEditCampaign,
  removeCampaign,
  campaignTotal,
  campaignPage,
  campaignPageSize,
  setCampaignPage,
  setCampaignPageSize
}) {
  return (
    <>
      <div className="voucher-card-grid">
        <div className="voucher-panel-card"><h3>Tổng chiến dịch</h3><p><strong>{campaignStats.totalCampaigns}</strong></p></div>
        <div className="voucher-panel-card"><h3>Chiến dịch đang hoạt động</h3><p><strong>{campaignStats.activeCampaigns}</strong></p></div>
        <div className="voucher-panel-card"><h3>Tổng lượt sử dụng</h3><p><strong>{campaignStats.totalRedeemed}</strong></p></div>
        <div className="voucher-panel-card"><h3>Cảnh báo ngân sách</h3><p><strong>{campaignStats.budgetWarningCount}</strong></p></div>
      </div>
      <div className="table-toolbar voucher-toolbar">
        <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm chiến dịch..." />
        <select className="voucher-select" value={campaignFilterStatus} onChange={(e) => setCampaignFilterStatus(e.target.value)}>
          {campaignStatusOptions.map((x) => <option key={x} value={x}>{x === "ALL" ? "Tất cả trạng thái" : statusText(x)}</option>)}
        </select>
        <button className="btn-primary" onClick={openCreateCampaign}>+ Tạo chiến dịch</button>
        <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới chiến dịch"}</button>
      </div>
      {campaignStats.budgetWarningCount > 0 ? (
        <div className="voucher-budget-warning-box">
          <strong>{campaignStats.budgetWarningCount} chiến dịch đang sắp hết hoặc vượt ngân sách:</strong>
          <div className="voucher-budget-warning-list">
            {campaignWarnings.map((c) => (
              <span key={c.id} className="voucher-budget-warning-chip">
                {c.name || c.campaignCode || `#${c.id}`}: {num(c.budgetUsagePercent, 0)}%
              </span>
            ))}
          </div>
        </div>
      ) : null}
      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>ID</th><th>Mã chiến dịch</th><th>Tên chiến dịch</th><th>Thời gian</th><th>Voucher áp dụng</th><th>Đã sử dụng</th><th>Ngân sách đã dùng</th><th>Tỷ lệ ngân sách</th><th>Cảnh báo</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            {campaignLoading ? <tr><td colSpan={11}>Đang tải dữ liệu voucher...</td></tr> : campaignPageRows.length === 0 ? <tr><td colSpan={11}><EmptyState message="Không có chiến dịch phù hợp" /></td></tr> : campaignPageRows.map((r) => (
              <tr key={r.id}>
                <td className="td-id">#{r.id}</td>
                <td>{r.campaignCode || "-"}</td>
                <td>
                  <div className="voucher-cell-title">{r.name || "-"}</div>
                  <div className="voucher-subtext">{r.description || r.objective || "Chưa có mô tả"}</div>
                </td>
                <td>{dt(r.startDate)} - {dt(r.endDate)}</td>
                <td>{r.voucherCount}</td>
                <td>{r.redeemed}</td>
                <td>{money(r.budgetUsed)} / {money(r.budgetLimit)}</td>
                <td>{num(r.budgetUsagePercent, 0)}%</td>
                <td><span className={`badge ${budgetAlertClass(r)}`}>{budgetAlertText(r)}</span></td>
                <td><span className={`badge ${STATUS_BADGE[up(r.status)] || "badge-inactive"}`}>{statusText(r.status)}</span></td>
                <td>
                  <div className="row-actions voucher-actions-col">
                    <button className="btn-icon" onClick={() => openCampaignDetail(r)} disabled={campaignDetailLoading}>Chi tiết</button>
                    <button className="btn-icon" onClick={() => openEditCampaign(r)} disabled={campaignDetailLoading}>Sửa</button>
                    <button className="btn-icon" onClick={() => removeCampaign(r)}>Xóa</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: "flex", justifyContent: "center" }}>
        <TablePagination
          totalItems={campaignTotal}
          currentPage={campaignPage}
          pageSize={campaignPageSize}
          onPageChange={setCampaignPage}
          onPageSizeChange={(size) => {
            setCampaignPageSize(size);
            setCampaignPage(1);
          }}
        />
      </div>
    </>
  );
}

export function VoucherConflictsSection({
  confQuery,
  setConfQuery,
  confA,
  setConfA,
  confB,
  setConfB,
  selectConflict,
  confLoading,
  conflictOptions,
  confTotal,
  confPage,
  confPageSize,
  setConfPage,
  setConfPageSize,
  confResult
}) {
  return (
    <div className="voucher-card-grid">
      <div className="voucher-panel-card">
        <h3>So sánh voucher</h3>
        <p className="voucher-subtext">Chọn 2 voucher để kiểm tra xung đột cơ bản về mã, cộng dồn và thời gian.</p>
        <input className="search-input" value={confQuery} onChange={(e) => setConfQuery(e.target.value)} placeholder="Tìm voucher..." />
        <div className="voucher-form-grid voucher-form-grid--2">
          <select value={confA} onChange={(e) => selectConflict(e.target.value, setConfA)} disabled={confLoading}><option value="">Voucher A</option>{conflictOptions.map((v) => <option key={v.id} value={v.id}>#{v.id} - {v.code}</option>)}</select>
          <select value={confB} onChange={(e) => selectConflict(e.target.value, setConfB)} disabled={confLoading}><option value="">Voucher B</option>{conflictOptions.map((v) => <option key={v.id} value={v.id}>#{v.id} - {v.code}</option>)}</select>
        </div>
        <TablePagination totalItems={confTotal} currentPage={confPage} pageSize={confPageSize} onPageChange={setConfPage} onPageSizeChange={(size) => { setConfPageSize(size); setConfPage(1); }} />
        <div className="voucher-finding-list">
          {confResult.length === 0 ? <div className="voucher-finding">Chưa có kết quả. Vui lòng chọn 2 voucher.</div> : confResult.map((x, i) => <div key={`${x}-${i}`} className="voucher-finding voucher-finding-warning">{x}</div>)}
        </div>
      </div>
      <div className="voucher-panel-card">
        <h3>Hương dẫn xử lý</h3>
        <p className="voucher-subtext">1. Nếu trùng mã hoặc không cộng dồn được thì chặn ngay.</p>
        <p className="voucher-subtext">2. Nếu trùng thời gian thì cần thêm rule xung đột ở backend.</p>
        <p className="voucher-subtext">3. Nếu voucher % cộng với voucher % thì cần giới hạn trần tổng.</p>
      </div>
    </div>
  );
}

export function VoucherRedemptionsSection({
  q,
  setQ,
  refresh,
  busy,
  redemptionLoading,
  redemptionRows,
  redemptionTotal,
  redemptionPage,
  redemptionPageSize,
  setRedemptionPage,
  setRedemptionPageSize
}) {
  return (
    <>
      <div className="table-toolbar voucher-toolbar">
        <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm lịch sử sử dụng..." />
        <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới lịch sử"}</button>
      </div>
      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>ID</th><th>Voucher</th><th>Khách hàng</th><th>Giảm giá</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead>
          <tbody>
            {redemptionLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : redemptionRows.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có lịch sử sử dụng" /></td></tr> : redemptionRows.map((r) => (
              <tr key={r.id}><td>#{r.id}</td><td>{r.voucherCode || `#${r.voucherId}`}</td><td>{r.customerId || "-"}</td><td>{money(r.discountAmount)}</td><td><span className={`badge ${STATUS_BADGE[up(r.redemptionStatus)] || "badge-inactive"}`}>{statusText(r.redemptionStatus)}</span></td><td>{dt(r.createdAt || r.redeemedAt)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: "flex", justifyContent: "center" }}>
        <TablePagination
          totalItems={redemptionTotal}
          currentPage={redemptionPage}
          pageSize={redemptionPageSize}
          onPageChange={setRedemptionPage}
          onPageSizeChange={(size) => {
            setRedemptionPageSize(size);
            setRedemptionPage(1);
          }}
        />
      </div>
    </>
  );
}

export function VoucherApprovalsSection({
  approvalBy,
  setApprovalBy,
  approvalNote,
  setApprovalNote,
  q,
  setQ,
  refresh,
  busy,
  approvalLoading,
  approvalRows,
  decide,
  approvalTotal,
  approvalPage,
  approvalPageSize,
  setApprovalPage,
  setApprovalPageSize
}) {
  return (
    <>
      <div className="voucher-inline-form">
        <input value={approvalBy} onChange={(e) => setApprovalBy(e.target.value)} placeholder="ID người duyệt" />
        <input value={approvalNote} onChange={(e) => setApprovalNote(e.target.value)} placeholder="Ghi chú/Lý do" />
      </div>
      <div className="table-toolbar voucher-toolbar">
        <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm yêu cầu phê duyệt..." />
        <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới phê duyệt"}</button>
      </div>
      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>ID</th><th>Loại yêu cầu</th><th>Voucher</th><th>Mức rủi ro</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            {approvalLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : approvalRows.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có yêu cầu phê duyệt" /></td></tr> : approvalRows.map((a) => (
              <tr key={a.id}><td>#{a.id}</td><td>{a.requestType}</td><td>{a.voucherId ? `#${a.voucherId}` : "-"}</td><td>{a.riskLevel || "-"}</td><td><span className={`badge ${STATUS_BADGE[up(a.status)] || "badge-inactive"}`}>{statusText(a.status)}</span></td><td>{up(a.status) === "PENDING" ? <div className="row-actions"><button className="btn-icon" onClick={() => decide(a, "approve")}>Duyệt</button><button className="btn-icon" onClick={() => decide(a, "reject")}>Từ chối</button></div> : "Đã xử lý"}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: "flex", justifyContent: "center" }}>
        <TablePagination
          totalItems={approvalTotal}
          currentPage={approvalPage}
          pageSize={approvalPageSize}
          onPageChange={setApprovalPage}
          onPageSizeChange={(size) => {
            setApprovalPageSize(size);
            setApprovalPage(1);
          }}
        />
      </div>
    </>
  );
}

export function VoucherReportsSection({
  reportStats,
  refresh,
  busy,
  reportLoading,
  reportVouchers,
  reportVoucherTotal,
  reportVoucherPage,
  reportVoucherPageSize,
  setReportVoucherPage,
  setReportVoucherPageSize,
  reportRedemptions,
  reportRedemptionTotal,
  reportRedemptionPage,
  reportRedemptionPageSize,
  setReportRedemptionPage,
  setReportRedemptionPageSize
}) {
  return (
    <>
      <div className="voucher-card-grid">
        <div className="voucher-panel-card"><h3>Tỷ lệ sử dụng</h3><p><strong>{pct(reportStats.redeemed, reportStats.issued)}%</strong></p></div>
        <div className="voucher-panel-card"><h3>Tổng giá trị giảm</h3><p><strong>{money(reportStats.discount)}</strong></p></div>
        <div className="voucher-panel-card"><h3>Tỷ lệ dùng ngân sách</h3><p><strong>{pct(reportStats.budgetUsed, reportStats.budgetLimit)}%</strong></p></div>
      </div>
      <div className="table-toolbar voucher-toolbar">
        <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới báo cáo"}</button>
      </div>
      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>Voucher</th><th>Mã</th><th>Đã phát hành</th><th>Đã sử dụng</th><th>Tỷ lệ</th><th>Ngân sách đã dùng</th></tr></thead>
          <tbody>
            {reportLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : reportVouchers.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có dữ liệu báo cáo voucher" /></td></tr> : reportVouchers.map((v) => (
              <tr key={v.id}><td>{v.voucherName || "-"}</td><td>{v.code || "-"}</td><td>{v.issued}</td><td>{v.redeemed}</td><td>{pct(v.redeemed, v.issued)}%</td><td>{money(v.budgetUsed)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: "flex", justifyContent: "center" }}>
        <TablePagination
          totalItems={reportVoucherTotal}
          currentPage={reportVoucherPage}
          pageSize={reportVoucherPageSize}
          onPageChange={setReportVoucherPage}
          onPageSizeChange={(size) => {
            setReportVoucherPageSize(size);
            setReportVoucherPage(1);
          }}
        />
      </div>
      <div className="table-wrapper">
        <table className="admin-table">
          <thead><tr><th>ID sử dụng</th><th>Voucher</th><th>Khách hàng</th><th>Giảm giá</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead>
          <tbody>
            {reportLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : reportRedemptions.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có dữ liệu lịch sử sử dụng" /></td></tr> : reportRedemptions.map((r) => (
              <tr key={r.id}><td>#{r.id}</td><td>{r.voucherCode || `#${r.voucherId}`}</td><td>{r.customerId || "-"}</td><td>{money(r.discountAmount)}</td><td>{statusText(r.redemptionStatus)}</td><td>{dt(r.createdAt || r.redeemedAt)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: "flex", justifyContent: "center" }}>
        <TablePagination
          totalItems={reportRedemptionTotal}
          currentPage={reportRedemptionPage}
          pageSize={reportRedemptionPageSize}
          onPageChange={setReportRedemptionPage}
          onPageSizeChange={(size) => {
            setReportRedemptionPageSize(size);
            setReportRedemptionPage(1);
          }}
        />
      </div>
    </>
  );
}

export function VoucherSettingsSection({ policy, setPolicy }) {
  return (
    <div className="voucher-card-grid">
      <div className="voucher-panel-card">
        <h3>Chính sách</h3>
        <div className="voucher-settings-policy-grid">
          <label className="voucher-toggle-item voucher-settings-policy-item">
            <span>Bắt buộc trần giảm cho voucher phần trểm</span>
            <input
              type="checkbox"
              checked={policy.requireCapPercent}
              onChange={(e) => setPolicy((p) => ({ ...p, requireCapPercent: e.target.checked }))}
            />
          </label>
          <label className="voucher-toggle-item voucher-settings-policy-item">
            <span>Chặn cộng dồn theo mặc định</span>
            <input
              type="checkbox"
              checked={policy.blockStacking}
              onChange={(e) => setPolicy((p) => ({ ...p, blockStacking: e.target.checked }))}
            />
          </label>
        </div>
        <div className="form-actions voucher-form-actions-left">
          <button className="btn-primary" onClick={() => localStorage.setItem("voucher_admin_policy", JSON.stringify(policy))}>
            Lưu chính sách
          </button>
        </div>
      </div>
    </div>
  );
}
