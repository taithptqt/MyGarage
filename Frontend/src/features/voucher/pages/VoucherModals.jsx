import { Modal } from "@features/admin/components/AdminModals";
import TablePagination from "@shared/components/TablePagination";
import { budgetAlertText, dt, money, num, statusText } from "./voucherManagementHelpers";

export function VoucherFormModal({ mode, form, setForm, submit, saving, onClose }) {
  return (
    <Modal title={mode === "create" ? "Tạo voucher" : `Sửa voucher #${form.id}`} onClose={onClose}>
      <form className="admin-form" onSubmit={submit}>
        <div className="voucher-form-grid voucher-form-grid--2">
          <input value={form.code} onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))} placeholder="Mã voucher *" required />
          <input value={form.voucherName} onChange={(e) => setForm((p) => ({ ...p, voucherName: e.target.value }))} placeholder="Tên voucher *" required />
          <input value={form.type} onChange={(e) => setForm((p) => ({ ...p, type: e.target.value }))} placeholder="Loại voucher" />
          <input value={form.value} onChange={(e) => setForm((p) => ({ ...p, value: e.target.value }))} placeholder="Giá trị" />
          <input value={form.minOrderValue} onChange={(e) => setForm((p) => ({ ...p, minOrderValue: e.target.value }))} placeholder="Hóa đơn tđi thiểu" />
          <input value={form.maxDiscountAmount} onChange={(e) => setForm((p) => ({ ...p, maxDiscountAmount: e.target.value }))} placeholder="Giảm tđi đa" />
          <input type="datetime-local" value={form.startDate} onChange={(e) => setForm((p) => ({ ...p, startDate: e.target.value }))} />
          <input type="datetime-local" value={form.endDate} onChange={(e) => setForm((p) => ({ ...p, endDate: e.target.value }))} />
          <input value={form.usageLimit} onChange={(e) => setForm((p) => ({ ...p, usageLimit: e.target.value }))} placeholder="Giới hạn lượt dùng" />
          <input value={form.budgetLimit} onChange={(e) => setForm((p) => ({ ...p, budgetLimit: e.target.value }))} placeholder="Ngân sách tối đa" />
        </div>
        <textarea rows={3} value={form.description} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} placeholder="Mô tả" />
        <div className="voucher-toggle-grid">
          <label className="voucher-check-item"><input type="checkbox" checked={form.isStackable} onChange={(e) => setForm((p) => ({ ...p, isStackable: e.target.checked }))} /><span>Cho phép cộng dồn</span></label>
          <label className="voucher-check-item"><input type="checkbox" checked={form.approvalRequired} onChange={(e) => setForm((p) => ({ ...p, approvalRequired: e.target.checked }))} /><span>Yêu cầu phê duyệt</span></label>
          <label className="voucher-check-item"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))} /><span>Kích hoạt</span></label>
        </div>
        <div className="form-actions"><button type="button" className="btn-ghost" onClick={onClose}>Hủy</button><button type="submit" className="btn-primary" disabled={saving}>{saving ? "Đang lưu..." : "Lưu thay đổi"}</button></div>
      </form>
    </Modal>
  );
}

export function CampaignFormModal({
  campaignMode,
  campaignForm,
  setCampaignForm,
  submitCampaign,
  campaignVoucherQuery,
  setCampaignVoucherQuery,
  setCampaignVoucherPage,
  campaignVoucherError,
  campaignVoucherLoading,
  campaignVoucherRows,
  toggleCampaignVoucher,
  campaignVoucherTotal,
  campaignVoucherPage,
  campaignVoucherPageSize,
  setCampaignVoucherPageSize,
  campaignSaving,
  onClose
}) {
  return (
    <Modal title={campaignMode === "create" ? "Tạo chiến dịch" : `Sửa chiến dịch #${campaignForm.id}`} onClose={onClose}>
      <form className="admin-form" onSubmit={submitCampaign}>
        <div className="voucher-form-grid voucher-form-grid--2">
          <input value={campaignForm.campaignCode} onChange={(e) => setCampaignForm((p) => ({ ...p, campaignCode: e.target.value }))} placeholder="Mã chiến dịch (không bắt buộc)" />
          <input value={campaignForm.name} onChange={(e) => setCampaignForm((p) => ({ ...p, name: e.target.value }))} placeholder="Tên chiến dịch *" required />
          <input value={campaignForm.objective} onChange={(e) => setCampaignForm((p) => ({ ...p, objective: e.target.value }))} placeholder="Mục tiêu chiến dịch" />
          <input value={campaignForm.ownerStaffId} onChange={(e) => setCampaignForm((p) => ({ ...p, ownerStaffId: e.target.value }))} placeholder="ID quản lý chiến dịch" />
          <input type="datetime-local" value={campaignForm.startDate} onChange={(e) => setCampaignForm((p) => ({ ...p, startDate: e.target.value }))} />
          <input type="datetime-local" value={campaignForm.endDate} onChange={(e) => setCampaignForm((p) => ({ ...p, endDate: e.target.value }))} />
          <input value={campaignForm.budgetLimit} onChange={(e) => setCampaignForm((p) => ({ ...p, budgetLimit: e.target.value }))} placeholder="Ngân sách giới hạn (VND)" />
          <select value={campaignForm.status} onChange={(e) => setCampaignForm((p) => ({ ...p, status: e.target.value }))}>
            <option value="">Tự động theo thời gian</option>
            <option value="DRAFT">Nháp</option>
            <option value="PENDING_APPROVAL">Chờ duyệt</option>
            <option value="SCHEDULED">Đã lên lịch</option>
            <option value="ACTIVE">Đang hoạt động</option>
            <option value="PAUSED">Tạm dừng</option>
            <option value="ENDED">Đã kết thúc</option>
            <option value="ARCHIVED">Lưu trữ</option>
          </select>
        </div>

        <textarea rows={3} value={campaignForm.description} onChange={(e) => setCampaignForm((p) => ({ ...p, description: e.target.value }))} placeholder="Mô tả chiến dịch" />

        <div className="voucher-toggle-grid">
          <label className="voucher-check-item"><input type="checkbox" checked={campaignForm.isActive} onChange={(e) => setCampaignForm((p) => ({ ...p, isActive: e.target.checked }))} /><span>Chiến dịch đang hoạt động</span></label>
          <label className="voucher-check-item"><input type="checkbox" checked={campaignForm.replaceVoucherScope} onChange={(e) => setCampaignForm((p) => ({ ...p, replaceVoucherScope: e.target.checked }))} /><span>Thay toàn bộ danh sách voucher áp dụng</span></label>
        </div>

        <div className="voucher-campaign-voucher-box">
          <div className="voucher-campaign-voucher-head">
            <h3>Voucher được phép áp dụng trong chiến dịch</h3>
            <span>{campaignForm.voucherIds.length} voucher đã chọn</span>
          </div>
          <input className="search-input" value={campaignVoucherQuery} onChange={(e) => { setCampaignVoucherQuery(e.target.value); setCampaignVoucherPage(1); }} placeholder="Tìm voucher..." />
          {campaignVoucherError ? <div className="alert alert-error">{campaignVoucherError}</div> : null}
          <div className="voucher-campaign-voucher-list">
            {campaignVoucherLoading ? (
              <div className="voucher-subtext">Đang tải dữ liệu voucher...</div>
            ) : campaignVoucherRows.length === 0 ? (
              <div className="voucher-subtext">Chưa có voucher để gán.</div>
            ) : campaignVoucherRows.map((voucher) => (
              <label key={voucher.id} className="voucher-campaign-voucher-item">
                <input
                  type="checkbox"
                  checked={(campaignForm.voucherIds || []).includes(Number(voucher.id))}
                  onChange={() => toggleCampaignVoucher(voucher.id)}
                />
                <span className="voucher-campaign-voucher-code">{voucher.code || `#${voucher.id}`}</span>
                <span>{voucher.voucherName || "Chưa đặt tên"}</span>
              </label>
            ))}
          </div>
        </div>

        <TablePagination totalItems={campaignVoucherTotal} currentPage={campaignVoucherPage} pageSize={campaignVoucherPageSize} onPageChange={setCampaignVoucherPage} onPageSizeChange={(size) => { setCampaignVoucherPageSize(size); setCampaignVoucherPage(1); }} />
        <div className="form-actions">
          <button type="button" className="btn-ghost" onClick={onClose}>Hủy</button>
          <button type="submit" className="btn-primary" disabled={campaignSaving || campaignVoucherLoading || Boolean(campaignVoucherError)}>{campaignSaving ? "Đang lưu..." : "Lưu chiến dịch"}</button>
        </div>
      </form>
    </Modal>
  );
}

export function CampaignDetailModal({
  selectedCampaign,
  onClose,
  campaignDetailPageError,
  campaignDetailPageLoading,
  campaignDetailVouchers,
  campaignDetailTotal,
  campaignDetailPage,
  campaignDetailPageSize,
  setCampaignDetailPage,
  setCampaignDetailPageSize
}) {
  return (
    <Modal title={`Chi tiết chiến dịch #${selectedCampaign.id}`} onClose={onClose}>
      <div className="voucher-detail-grid">
        <div><strong>Mã chiến dịch:</strong> {selectedCampaign.campaignCode || "-"}</div>
        <div><strong>Tên:</strong> {selectedCampaign.name || "-"}</div>
        <div><strong>Mục tiêu:</strong> {selectedCampaign.objective || "-"}</div>
        <div><strong>Mô tả:</strong> {selectedCampaign.description || "-"}</div>
        <div><strong>Thời gian:</strong> {dt(selectedCampaign.startDate)} - {dt(selectedCampaign.endDate)}</div>
        <div><strong>Trạng thái:</strong> {statusText(selectedCampaign.status)}</div>
        <div><strong>Voucher áp dụng:</strong> {selectedCampaign.voucherCount || 0}</div>
        <div><strong>Ngân sách đã dùng:</strong> {money(selectedCampaign.budgetUsed)} / {money(selectedCampaign.budgetLimit)}</div>
        <div><strong>Tỷ lệ ngân sách:</strong> {num(selectedCampaign.budgetUsagePercent, 0)}%</div>
        <div><strong>Cảnh báo:</strong> {budgetAlertText(selectedCampaign)}</div>
      </div>
      <div className="voucher-campaign-detail-vouchers">
        <strong>Danh sách voucher:</strong>
        {campaignDetailPageError ? <div className="alert alert-error">{campaignDetailPageError}</div> : null}
        {campaignDetailPageLoading ? <p className="voucher-subtext">Đang tải dữ liệu voucher...</p> : campaignDetailPageError ? null : campaignDetailVouchers.length > 0 ? (
          <div className="voucher-campaign-tags">
            {campaignDetailVouchers.map((v) => (
              <span className="voucher-campaign-tag" key={v.id}>{v.code || `#${v.id}`} - {v.voucherName || "Chưa đặt tên"}</span>
            ))}
          </div>
        ) : (
          <p className="voucher-subtext">Chưa có voucher nào gắn vào chiến dịch này.</p>
        )}
        <TablePagination totalItems={campaignDetailTotal} currentPage={campaignDetailPage} pageSize={campaignDetailPageSize} onPageChange={setCampaignDetailPage} onPageSizeChange={(size) => { setCampaignDetailPageSize(size); setCampaignDetailPage(1); }} />
      </div>
    </Modal>
  );
}

export function VoucherDetailModal({ selected, onClose }) {
  return (
    <Modal title={`Chi tiết voucher #${selected.id}`} onClose={onClose}>
      <div className="voucher-detail-grid">
        <div><strong>M?:</strong> {selected.code || "-"}</div>
        <div><strong>Tên:</strong> {selected.voucherName || "-"}</div>
        <div><strong>Loại:</strong> {selected.type || "-"}</div>
        <div><strong>Giá trị:</strong> {selected.type === "PERCENT" ? `${selected.value || 0}%` : money(selected.value)}</div>
        <div><strong>Trạng thái:</strong> {statusText(selected.status)}</div>
        <div><strong>Thời gian:</strong> {dt(selected.startDate)} - {dt(selected.endDate)}</div>
        <div><strong>Hạn mức:</strong> {num(selected.redeemedCount, num(selected.usedCount, 0))}/{num(selected.issuedCount, num(selected.usageLimit, 0))}</div>
        <div><strong>Ngân sách:</strong> {money(selected.budgetUsed)} / {money(selected.budgetLimit)}</div>
      </div>
    </Modal>
  );
}
