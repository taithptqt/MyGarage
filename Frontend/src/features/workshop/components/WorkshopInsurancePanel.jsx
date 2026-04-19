import React, { useMemo } from "react";
import FilterDropdown from "@shared/components/FilterDropdown";
import {
  INSURANCE_ASSET_TYPE,
  INSURANCE_CARD_IMAGE_MAX,
  INSURANCE_CLAIM_MODE_OPTIONS,
  INSURANCE_DAMAGE_IMAGE_MAX,
  INSURANCE_DEDUCTIBLE_OPTIONS,
  formatInsuranceCurrency,
  getInsuranceClaimModeLabel,
  getInsuranceDeductibleLabel,
} from "@features/appointments/utils/bookingInsurance";

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

function buildCompanyOptions(companies = []) {
  const normalized = (companies || [])
    .map((company) => {
      const companyId = Number(company?.id || company?.Id || 0);
      const companyName = String(company?.name || company?.Name || "").trim();
      if (companyId <= 0 || !companyName) {
        return null;
      }

      return {
        value: String(companyId),
        label: companyName,
      };
    })
    .filter(Boolean);

  return [{ value: "", label: "-- Chọn công ty bảo hiểm --" }, ...normalized];
}

function renderImagePreviewList(urls = [], assetType, onRemoveAsset, disabled) {
  if (!Array.isArray(urls) || urls.length === 0) {
    return <p className="workshop-insurance-upload-empty">Chưa có ảnh.</p>;
  }

  return (
    <div className="workshop-insurance-upload-preview-list">
      {urls.map((url, index) => (
        <div key={`${assetType}-${url}-${index}`} className="workshop-insurance-upload-preview-item">
          <a href={url} target="_blank" rel="noreferrer" className="workshop-insurance-upload-preview-link">
            <img src={url} alt="Hồ sơ bảo hiểm" className="workshop-insurance-upload-preview-image" />
          </a>
          <div className="workshop-insurance-upload-preview-actions">
            <span>Ảnh {index + 1}</span>
            <button type="button" onClick={() => onRemoveAsset?.(assetType, index)} disabled={disabled}>
              Xóa
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

function InsuranceUploadField({
  title,
  description,
  assetType,
  urls,
  maxCount,
  uploading,
  feedbackMessage,
  onUploadAsset,
  onRemoveAsset,
  disabled = false,
}) {
  const remainingCount = Math.max(0, maxCount - (Array.isArray(urls) ? urls.length : 0));
  const isDisabled = disabled || uploading || remainingCount <= 0;

  return (
    <section className="workshop-insurance-upload-card">
      <div className="workshop-insurance-upload-head">
        <div>
          <strong>{title}</strong>
          <p>{description}</p>
        </div>
        <span className="workshop-insurance-upload-count">
          {Array.isArray(urls) ? urls.length : 0}/{maxCount}
        </span>
      </div>

      <label className={`workshop-insurance-upload-trigger ${isDisabled ? "disabled" : ""}`}>
        <input
          className="workshop-insurance-upload-input"
          type="file"
          accept="image/*"
          multiple
          disabled={isDisabled}
          onChange={(event) => {
            const files = Array.from(event.target.files || []);
            event.target.value = "";
            if (files.length === 0) return;
            onUploadAsset?.(assetType, files.slice(0, remainingCount));
          }}
        />
        {uploading ? "Đang tải ảnh..." : remainingCount > 0 ? "Tải ảnh" : "Đã đủ ảnh"}
      </label>

      <small className="workshop-insurance-upload-hint">
        {remainingCount > 0 ? `Có thể thêm ${remainingCount} ảnh.` : "Đã đủ số ảnh."}
      </small>

      {feedbackMessage ? <p className="workshop-insurance-upload-feedback">{feedbackMessage}</p> : null}

      {renderImagePreviewList(urls, assetType, onRemoveAsset, isDisabled)}
    </section>
  );
}

function getUploadState(uploadingState, assetType) {
  const assetState = uploadingState?.[assetType];
  if (assetState && typeof assetState === "object") {
    return {
      uploading: Boolean(assetState.uploading),
      successMessage: String(assetState.successMessage || "").trim(),
    };
  }

  return {
    uploading: Boolean(assetState),
    successMessage: "",
  };
}

function getQuoteStateLabel(insuranceQuoteState, insuranceDeductibleOption, insuranceClaimMode) {
  const normalizedQuoteState = String(insuranceQuoteState || "").trim().toUpperCase();
  if (normalizedQuoteState === "NEEDS_ADVISOR") return "Chờ tư vấn";
  if (normalizedQuoteState === "PENDING_REVIEW") return "Chờ rà soát";

  if (String(insuranceDeductibleOption || "").trim() || String(insuranceClaimMode || "").trim()) {
    return "Đã có snapshot";
  }

  return "Chưa chốt";
}

export default function WorkshopInsurancePanel({
  enabled = false,
  locked = false,
  insuranceVehicleOptions = [],
  activeInsuranceVehicleKey = "",
  onInsuranceVehicleChange,
  companies = [],
  selectedCompanyId = "",
  onToggle,
  onCompanyChange,
  companyError = "",
  insuranceDeductibleOption = "",
  insuranceClaimMode = "",
  insuranceQuoteState = "",
  onDeductibleOptionChange,
  onClaimModeChange,
  deductibleError = "",
  claimModeError = "",
  draftBreakdown = null,
  insuranceCardImageUrls = [],
  insuranceDamageImageUrls = [],
  uploadingState = {},
  onUploadAsset,
  onRemoveAsset,
}) {
  const hasCompanies = Array.isArray(companies) && companies.length > 0;
  const companyOptions = useMemo(() => buildCompanyOptions(companies), [companies]);
  const cardUploadState = getUploadState(uploadingState, INSURANCE_ASSET_TYPE.CARD);
  const damageUploadState = getUploadState(uploadingState, INSURANCE_ASSET_TYPE.DAMAGE);
  const deductibleLabel = getInsuranceDeductibleLabel(insuranceDeductibleOption);
  const claimModeLabel = getInsuranceClaimModeLabel(insuranceClaimMode);
  const quoteStateLabel = getQuoteStateLabel(
    insuranceQuoteState,
    insuranceDeductibleOption,
    insuranceClaimMode
  );

  return (
    <div className="workshop-insurance-panel">
      <div className="workshop-insurance-panel-head">
        <div className="workshop-insurance-panel-copy">
          <p className="workshop-insurance-panel-kicker">Bảo hiểm</p>
          <h5 className="workshop-insurance-panel-title">Tách bill bảo hiểm ngay từ lúc tiếp nhận</h5>
          <p className="workshop-insurance-panel-note">
            {locked
              ? "Đã có dịch vụ hoặc gói bảo hiểm trong danh sách, hệ thống giữ luồng bảo hiểm cho lệnh này."
              : "Bật khi lệnh sửa chữa cần gửi hồ sơ cho công ty bảo hiểm và theo dõi phần khách cùng chi trả."}
          </p>
        </div>

        <label className="reception-appt-quick-toggle workshop-insurance-toggle" htmlFor="workshop-insurance-toggle">
          <input
            id="workshop-insurance-toggle"
            type="checkbox"
            checked={enabled}
            onChange={(event) => onToggle?.(Boolean(event.target.checked))}
            disabled={locked}
          />
          <span>Xử lý theo bảo hiểm</span>
        </label>
      </div>

      {enabled ? (
        <>
          {Array.isArray(insuranceVehicleOptions) && insuranceVehicleOptions.length > 1 ? (
            <div className="form-group">
              <label>Bảo hiểm theo từng xe</label>
              <div className="reception-selected-tabs reception-vehicle-tabs-layout">
                {insuranceVehicleOptions.map((vehicle) => (
                  <div
                    key={`insurance-vehicle-${vehicle.key}`}
                    className={`reception-selected-tab ${String(activeInsuranceVehicleKey) === String(vehicle.key) ? "active" : ""}`}
                  >
                    <button
                      type="button"
                      className="reception-selected-tab-main"
                      onClick={() => onInsuranceVehicleChange?.(vehicle.key)}
                    >
                      {vehicle.label}
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          <div className="workshop-insurance-core-grid">
            <div className="form-group workshop-insurance-company-field">
              <label>Công ty bảo hiểm *</label>
              <div className="workshop-insurance-company-dropdown">
                <FilterDropdown
                  value={String(selectedCompanyId || "")}
                  options={companyOptions}
                  onChange={(nextValue) => onCompanyChange?.(nextValue)}
                  minWidth={0}
                  menuMinWidth={280}
                  fullWidth
                  ariaLabel="Chọn công ty bảo hiểm"
                />
              </div>
              {companyError ? <div className="field-error">{companyError}</div> : null}
              {!hasCompanies ? (
                <div className="reception-muted-note">Chưa tải được danh sách công ty bảo hiểm.</div>
              ) : null}
            </div>

            <div className="workshop-insurance-snapshot">
              <div className="workshop-insurance-summary-row">
                <span>Miễn thường</span>
                <strong>{deductibleLabel}</strong>
              </div>
              <div className="workshop-insurance-summary-row">
                <span>Xử lý</span>
                <strong>{claimModeLabel}</strong>
              </div>
              <div className="workshop-insurance-summary-row">
                <span>Hồ sơ</span>
                <strong>{quoteStateLabel}</strong>
              </div>
              <div className="workshop-insurance-summary-row">
                <span>Khách dự kiến trả</span>
                <strong>
                  {String(insuranceDeductibleOption || "").trim().toUpperCase() === "CONSULT"
                    ? "Chờ tư vấn"
                    : formatInsuranceCurrency(
                        String(insuranceDeductibleOption || "").trim().toUpperCase() === "1000K"
                          ? 1000000
                          : String(insuranceDeductibleOption || "").trim().toUpperCase() === "500K"
                            ? 500000
                            : null
                      )}
                </strong>
              </div>
            </div>
          </div>

          <div className="workshop-insurance-choice-group">
            <span>Mức miễn thường *</span>
            <div className="workshop-insurance-choice-list">
              {INSURANCE_DEDUCTIBLE_OPTIONS.map((option) => (
                <label key={option.value} className="workshop-insurance-choice">
                  <input
                    type="radio"
                    name="workshop-insurance-deductible"
                    value={option.value}
                    checked={String(insuranceDeductibleOption || "").toUpperCase() === option.value}
                    onChange={(event) => onDeductibleOptionChange?.(event.target.value)}
                  />
                  <span>
                    <strong>{option.label}</strong>
                    <small>
                      {option.value === "CONSULT"
                        ? "Chọn khi khách chưa nhớ hợp đồng."
                        : "Dùng để ước tính phần khách cùng chi trả."}
                    </small>
                  </span>
                </label>
              ))}
            </div>
            {deductibleError ? <div className="field-error">{deductibleError}</div> : null}
          </div>

          <div className="workshop-insurance-choice-group">
            <span>Hình thức xử lý *</span>
            <div className="workshop-insurance-choice-list">
              {INSURANCE_CLAIM_MODE_OPTIONS.map((option) => (
                <label key={option.value} className="workshop-insurance-choice">
                  <input
                    type="radio"
                    name="workshop-insurance-claim-mode"
                    value={option.value}
                    checked={String(insuranceClaimMode || "").toUpperCase() === option.value}
                    onChange={(event) => onClaimModeChange?.(event.target.value)}
                  />
                  <span>
                    <strong>{option.label}</strong>
                    <small>
                      {option.value === "LINKED"
                        ? "Gara bảo lãnh hoặc liên kết trực tiếp."
                        : option.value === "SELF_PAY"
                          ? "Khách thanh toán trước, gara hỗ trợ hồ sơ."
                          : "Dùng khi khách chưa rõ cách xử lý."}
                    </small>
                  </span>
                </label>
              ))}
            </div>
            {claimModeError ? <div className="field-error">{claimModeError}</div> : null}
          </div>

          <div className="workshop-insurance-upload-grid">
            <InsuranceUploadField
              title="Ảnh thẻ bảo hiểm"
              description="Nên có đủ hồ sơ và kế toán đối soát nhanh."
              assetType={INSURANCE_ASSET_TYPE.CARD}
              urls={insuranceCardImageUrls}
              maxCount={INSURANCE_CARD_IMAGE_MAX}
              uploading={cardUploadState.uploading}
              feedbackMessage={cardUploadState.successMessage}
              onUploadAsset={onUploadAsset}
              onRemoveAsset={onRemoveAsset}
            />
            <InsuranceUploadField
              title="Ảnh hiện trạng xe"
              description="Lưu tham chiếu cho hồ sơ claim và tư vấn."
              assetType={INSURANCE_ASSET_TYPE.DAMAGE}
              urls={insuranceDamageImageUrls}
              maxCount={INSURANCE_DAMAGE_IMAGE_MAX}
              uploading={damageUploadState.uploading}
              feedbackMessage={damageUploadState.successMessage}
              onUploadAsset={onUploadAsset}
              onRemoveAsset={onRemoveAsset}
            />
          </div>

          {draftBreakdown ? (
            <div className="workshop-insurance-summary-grid">
              <article className="workshop-insurance-summary-card">
                <span>Tổng giá trị dịch vụ</span>
                <strong>{formatCurrency(draftBreakdown.totalAmount)}</strong>
              </article>
              <article className="workshop-insurance-summary-card">
                <span>Bảo hiểm dự kiến trả</span>
                <strong>{formatCurrency(draftBreakdown.insuranceRequestedAmount)}</strong>
              </article>
              <article className="workshop-insurance-summary-card">
                <span>Khách dự kiến trả</span>
                <strong>{formatCurrency(draftBreakdown.customerPayAmount)}</strong>
              </article>
            </div>
          ) : null}

          <p className="workshop-insurance-helper">
            Ảnh check-in dùng cho hiện trạng xe, còn ảnh hồ sơ bảo hiểm giúp lễ tân và kế toán làm claim đúng bộ chứng từ.
          </p>
        </>
      ) : null}
    </div>
  );
}


