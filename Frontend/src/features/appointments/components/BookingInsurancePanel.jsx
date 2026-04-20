import React, { useMemo } from "react";
import FilterDropdown from "@shared/components/FilterDropdown";
import {
  INSURANCE_ASSET_TYPE,
  INSURANCE_CARD_IMAGE_MAX,
  INSURANCE_CLAIM_MODE_OPTIONS,
  INSURANCE_DAMAGE_IMAGE_MAX,
  INSURANCE_DEDUCTIBLE_OPTIONS,
  buildInsuranceSummary,
} from "@features/appointments/utils/bookingInsurance";
import "./BookingInsurancePanel.css";

function toVehicleTabLabel(label, fallback = "") {
  const normalizedLabel = String(label || "").trim();
  if (!normalizedLabel) {
    return String(fallback || "").trim() || "Không biển số";
  }

  const [platePart] = normalizedLabel.split(" - ");
  const plate = String(platePart || "").trim();
  return plate || normalizedLabel;
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
    return <p className="booking-insurance-upload-empty">Chưa có ảnh.</p>;
  }

  return (
    <div className="booking-insurance-upload-preview-list">
      {urls.map((url, index) => (
        <div key={`${assetType}-${url}-${index}`} className="booking-insurance-upload-preview-item">
          <a href={url} target="_blank" rel="noreferrer" className="booking-insurance-upload-preview-link">
            <img src={url} alt="Tài liệu bảo hiểm" className="booking-insurance-upload-preview-image" />
          </a>
          <div className="booking-insurance-upload-preview-actions">
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
}) {
  const remainingCount = Math.max(0, maxCount - (Array.isArray(urls) ? urls.length : 0));
  const isDisabled = uploading || remainingCount <= 0;

  return (
    <section className="booking-insurance-upload-card">
      <div className="booking-insurance-upload-head">
        <div>
          <strong>{title}</strong>
          <p>{description}</p>
        </div>
        <span className="booking-insurance-upload-count">
          {Array.isArray(urls) ? urls.length : 0}/{maxCount}
        </span>
      </div>

      <label className={`booking-insurance-upload-trigger ${isDisabled ? "disabled" : ""}`}>
        <input
          className="booking-insurance-upload-input"
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

      <small className="booking-insurance-upload-hint">
        {remainingCount > 0 ? `Có thể thêm ${remainingCount} ảnh.` : "Đã đủ số ảnh."}
      </small>

      {feedbackMessage ? <p className="booking-insurance-upload-feedback">{feedbackMessage}</p> : null}

      {renderImagePreviewList(urls, assetType, onRemoveAsset, uploading)}
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

export default function BookingInsurancePanel({
  vehicleOptions = [],
  activeVehicleKey = "",
  onActiveVehicleChange,
  value,
  onChange,
  onUploadAsset,
  onRemoveAsset,
  uploadingState = {},
  companies = [],
  companyNameById = new Map(),
  contextLabel = "đặt lịch",
  mode = "internal",
  showUploads,
}) {
  const normalizedMode = String(mode || "").trim().toLowerCase() === "booking" ? "booking" : "internal";
  const shouldShowUploads = typeof showUploads === "boolean" ? showUploads : normalizedMode === "booking";
  const activeVehicle = vehicleOptions.find((item) => item.key === activeVehicleKey) || vehicleOptions[0] || null;
  const summary = buildInsuranceSummary(value, companyNameById, { mode: normalizedMode });
  const companyOptions = useMemo(() => buildCompanyOptions(companies), [companies]);
  const selectedInsuranceItems = Array.isArray(activeVehicle?.selectedInsuranceItemNames)
    ? activeVehicle.selectedInsuranceItemNames.filter(Boolean)
    : [];
  const cardUploadState = getUploadState(uploadingState, INSURANCE_ASSET_TYPE.CARD);
  const damageUploadState = getUploadState(uploadingState, INSURANCE_ASSET_TYPE.DAMAGE);

  return (
    <section className="booking-insurance-panel">
      <div className="booking-insurance-header">
        <p className="booking-insurance-eyebrow">Bảo hiểm</p>
        <h3 className="booking-insurance-title">Thông tin bảo hiểm</h3>
        <p className="booking-insurance-copy">
          {normalizedMode === "booking"
            ? `Chọn hãng bảo hiểm và tải ảnh nếu có để hoàn tất ${contextLabel}.`
            : `Snapshot ban đầu giúp lễ tân chuẩn bị hồ sơ khi ${contextLabel}.`}
        </p>
      </div>

      {vehicleOptions.length > 1 ? (
        <div className="booking-insurance-vehicle-tabs" role="tablist" aria-label="Xe cần khai báo bảo hiểm">
          {vehicleOptions.map((vehicle) => (
            <button
              key={vehicle.key}
              type="button"
              className={`booking-insurance-vehicle-tab ${vehicle.key === activeVehicle?.key ? "active" : ""}`}
              onClick={() => onActiveVehicleChange?.(vehicle.key)}
            >
              {toVehicleTabLabel(vehicle.label, vehicle.key)}
            </button>
          ))}
        </div>
      ) : null}

      <div className="booking-insurance-grid">
        <div className="booking-insurance-field">
          <label htmlFor={`insurance-company-${activeVehicle?.key || "default"}`}>Công ty bảo hiểm *</label>
          <div className="booking-insurance-company-dropdown">
            <FilterDropdown
              id={`insurance-company-${activeVehicle?.key || "default"}`}
              value={String(value?.insuranceCompanyId || "")}
              options={companyOptions}
              onChange={(nextValue) => onChange?.("insuranceCompanyId", nextValue)}
              minWidth={0}
              menuMinWidth={280}
              fullWidth
              ariaLabel="Chọn công ty bảo hiểm"
            />
          </div>
        </div>

        <div className="booking-insurance-summary">
          <div className="booking-insurance-summary-row">
            <span>Xe</span>
            <strong>{activeVehicle?.label || "Chưa xác định"}</strong>
          </div>
          <div className="booking-insurance-summary-row">
            <span>{normalizedMode === "booking" ? "Gói" : "Tạm tính"}</span>
            <strong>
              {normalizedMode === "booking"
                ? selectedInsuranceItems.length > 0
                  ? selectedInsuranceItems.join(", ")
                  : "Chưa chọn"
                : summary.estimatedCustomerPayLabel}
            </strong>
          </div>
          <div className="booking-insurance-summary-row">
            <span>{normalizedMode === "booking" ? "Hồ sơ" : "Xử lý"}</span>
            <strong>{normalizedMode === "booking" ? summary.intakeStatusLabel : summary.claimModeLabel}</strong>
          </div>
          <div className="booking-insurance-summary-row">
            <span>Ảnh</span>
            <strong>{summary.assetSummaryLabel}</strong>
          </div>
        </div>
      </div>

      {shouldShowUploads ? (
        <div className="booking-insurance-upload-grid">
          <InsuranceUploadField
            title="Ảnh thẻ bảo hiểm"
            description={normalizedMode === "booking" ? "Tùy chọn. Giúp lễ tân kiểm tra nhanh." : "Chụp nhanh để lưu dạng hồ sơ."}
            assetType={INSURANCE_ASSET_TYPE.CARD}
            urls={summary.insuranceCardImageUrls}
            maxCount={INSURANCE_CARD_IMAGE_MAX}
            uploading={cardUploadState.uploading}
            feedbackMessage={cardUploadState.successMessage}
            onUploadAsset={onUploadAsset}
            onRemoveAsset={onRemoveAsset}
          />
          <InsuranceUploadField
            title="Ảnh hiện trạng xe"
            description={normalizedMode === "booking" ? "Tùy chọn. Giúp gara chuẩn bị tiếp nhận." : "Giữ lại ảnh tham chiếu cho hồ sơ bảo hiểm."}
            assetType={INSURANCE_ASSET_TYPE.DAMAGE}
            urls={summary.insuranceDamageImageUrls}
            maxCount={INSURANCE_DAMAGE_IMAGE_MAX}
            uploading={damageUploadState.uploading}
            feedbackMessage={damageUploadState.successMessage}
            onUploadAsset={onUploadAsset}
            onRemoveAsset={onRemoveAsset}
          />
        </div>
      ) : null}

      {normalizedMode === "booking" ? (
        <>
          <p className="booking-insurance-note">
            Miễn thường và cách xử lý sẽ xác nhận khi tiếp nhận xe.
          </p>
        </>
      ) : (
        <>
          <div className="booking-insurance-choice-group">
            <span>Mức miễn thường *</span>
            <div className="booking-insurance-choice-list">
              {INSURANCE_DEDUCTIBLE_OPTIONS.map((option) => (
                <label key={option.value} className="booking-insurance-choice">
                  <input
                    type="radio"
                    name={`insurance-deductible-${activeVehicle?.key || "default"}`}
                    value={option.value}
                    checked={String(value?.insuranceDeductibleOption || "").toUpperCase() === option.value}
                    onChange={(event) => onChange?.("insuranceDeductibleOption", event.target.value)}
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
          </div>

          <div className="booking-insurance-choice-group">
            <span>Hình thức xử lý *</span>
            <div className="booking-insurance-choice-list">
              {INSURANCE_CLAIM_MODE_OPTIONS.map((option) => (
                <label key={option.value} className="booking-insurance-choice">
                  <input
                    type="radio"
                    name={`insurance-claim-mode-${activeVehicle?.key || "default"}`}
                    value={option.value}
                    checked={String(value?.insuranceClaimMode || "").toUpperCase() === option.value}
                    onChange={(event) => onChange?.("insuranceClaimMode", event.target.value)}
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
          </div>

          <p className="booking-insurance-note">
            Phần chênh lệch do khấu hao hoặc duyệt bảo hiểm sẽ chốt sau khi kiểm tra xe.
          </p>
        </>
      )}
    </section>
  );
}
