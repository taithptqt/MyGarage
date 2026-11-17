import React from "react";
import BookingInsurancePanel from "@features/appointments/components/BookingInsurancePanel";
import {
  SERVICE_MODE,
  formatCurrency,
  getVehicleIdFromKey,
  toVehicleTabLabel,
} from "@features/workshop/components/receptionCreateAppointmentHelpers";

export default function ReceptionAppointmentServiceStep({
  showServiceModeSwitch,
  effectiveServiceMode,
  handleServiceModeChange,
  hasMultipleSelectedVehicles,
  selectedVehicleKeys,
  activePlanVehicleKey,
  setActivePlanVehicleKey,
  vehicleLabelByKey,
  toggleVehicleSelection,
  isServicePickerCollapsed,
  setIsServicePickerCollapsed,
  visibleCategoryOptions,
  activePlan,
  toggleCategory,
  categoryNameById,
  activeCategoryId,
  setActiveCategoryId,
  serviceSearch,
  setServiceSearch,
  filteredServices,
  toggleService,
  insuranceVehicleOptions,
  activeInsuranceVehicleKey,
  setActiveInsuranceVehicleKey,
  activeInsuranceSnapshot,
  handleInsuranceSnapshotChange,
  handleInsuranceAssetUpload,
  handleInsuranceAssetRemove,
  insuranceUploadStateByVehicleKey,
  insuranceCompanies,
  insuranceCompanyNameById,
}) {
  return (
    <section className="reception-appt-section">
      <div className="reception-appt-step-head reception-appt-step-head-service">
        <span className="reception-step-badge">2. HẠNG MỤC DỊCH VỤ</span>
      </div>

      {showServiceModeSwitch ? (
        <div className="reception-scope-switch">
          <button
            type="button"
            className={effectiveServiceMode === SERVICE_MODE.SHARED ? "active" : ""}
            onClick={() => handleServiceModeChange(SERVICE_MODE.SHARED)}
          >
            Áp dụng tất cả xe
          </button>
          <button
            type="button"
            className={effectiveServiceMode === SERVICE_MODE.CUSTOM ? "active" : ""}
            onClick={() => handleServiceModeChange(SERVICE_MODE.CUSTOM)}
            disabled={!hasMultipleSelectedVehicles}
            title={!hasMultipleSelectedVehicles ? "Cần chọn từ 2 xe trở lên để tùy chỉnh riêng" : undefined}
          >
            Tùy chỉnh theo từng xe
          </button>
        </div>
      ) : null}

      {selectedVehicleKeys.length > 1 ? (
        <div className="reception-selected-tabs reception-vehicle-tabs-layout">
          {selectedVehicleKeys.map((vehicleKey) => (
            <div
              key={vehicleKey}
              className={`reception-selected-tab ${
                effectiveServiceMode === SERVICE_MODE.CUSTOM && activePlanVehicleKey === vehicleKey
                  ? "active"
                  : ""
              }`}
            >
              <button
                type="button"
                className="reception-selected-tab-main"
                onClick={() => {
                  setActivePlanVehicleKey(vehicleKey);
                  if (effectiveServiceMode === SERVICE_MODE.SHARED) {
                    handleServiceModeChange(SERVICE_MODE.CUSTOM);
                  }
                }}
              >
                {toVehicleTabLabel(vehicleLabelByKey.get(vehicleKey), vehicleKey)}
              </button>
              <button
                type="button"
                className="reception-selected-tab-remove"
                aria-label={`Bỏ ${vehicleLabelByKey.get(vehicleKey) || vehicleKey}`}
                onClick={(event) => {
                  event.stopPropagation();
                  const vehicleId = getVehicleIdFromKey(vehicleKey);
                  if (vehicleId > 0) {
                    toggleVehicleSelection(vehicleId);
                  }
                }}
              >
                ×
              </button>
            </div>
          ))}
        </div>
      ) : null}

      <div className="reception-service-subsection reception-service-type-subsection">
        <h5 className="reception-service-subtitle">Loại dịch vụ</h5>
        <button
          type="button"
          className={`reception-section-collapse-btn reception-section-collapse-btn-corner ${
            isServicePickerCollapsed ? "is-collapsed" : "is-expanded"
          }`}
          aria-expanded={!isServicePickerCollapsed}
          aria-label={isServicePickerCollapsed ? "Mở phần loại dịch vụ" : "Thu gọn phần loại dịch vụ"}
          onClick={() => setIsServicePickerCollapsed((prev) => !prev)}
        >
          <span className="reception-section-collapse-glyph" aria-hidden="true" />
        </button>
        {!isServicePickerCollapsed ? (
          <div className="reception-category-grid">
            {visibleCategoryOptions.map((category) => {
              const selected = activePlan.categoryIds.includes(Number(category.id));
              return (
                <button
                  key={category.id}
                  type="button"
                  className={`reception-category-chip ${selected ? "selected" : ""}`}
                  onClick={() => toggleCategory(category.id)}
                >
                  {category.name}
                </button>
              );
            })}
          </div>
        ) : (
          <div className="reception-service-collapsed-card">
            <span className="reception-service-collapsed-label">Loại dịch vụ</span>
            <span className="reception-service-collapsed-value">
              {activePlan.categoryIds.length > 0
                ? activePlan.categoryIds.map((categoryId) => categoryNameById.get(categoryId) || `Nhóm ${categoryId}`).join(", ")
                : "Chưa chọn loại dịch vụ"}
            </span>
          </div>
        )}
      </div>

      <div className="reception-service-subsection reception-service-detail-subsection">
        <h5 className="reception-service-subtitle">Dịch vụ cụ thể</h5>
        {activePlan.categoryIds.length > 0 ? (
          <>
            <div className="reception-selected-tabs reception-category-tabs-layout">
              {activePlan.categoryIds.map((categoryId) => (
                <div
                  key={`cat-${categoryId}`}
                  className={`reception-selected-tab ${String(activeCategoryId) === String(categoryId) ? "active" : ""}`}
                >
                  <button
                    type="button"
                    className="reception-selected-tab-main"
                    onClick={() => setActiveCategoryId(String(categoryId))}
                  >
                    {categoryNameById.get(categoryId) || `Nhóm ${categoryId}`}
                  </button>
                  <button
                    type="button"
                    className="reception-selected-tab-remove"
                    aria-label={`Bỏ ${categoryNameById.get(categoryId) || `Nhóm ${categoryId}`}`}
                    onClick={(event) => {
                      event.stopPropagation();
                      toggleCategory(categoryId);
                    }}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>

            <div className="form-group">
              <label>Tìm dịch vụ</label>
              <input
                type="text"
                placeholder="Tìm theo tên dịch vụ..."
                value={serviceSearch}
                onChange={(event) => setServiceSearch(event.target.value)}
              />
            </div>

            <div className="reception-service-list">
              {filteredServices.length === 0 ? (
                <div className="reception-muted-note">
                  Không tìm thấy dịch vụ phù hợp trong nhóm đã chọn.
                </div>
              ) : (
                filteredServices.map((service) => {
                  const selected = activePlan.serviceIds.includes(service.id);
                  return (
                    <button
                      key={service.id}
                      type="button"
                      className={`reception-service-card ${selected ? "selected" : ""}`}
                      onClick={() => toggleService(service.id)}
                    >
                      <div className="reception-service-card-main">
                        <strong>{service.name}</strong>
                        <small>{service.minutes > 0 ? `${service.minutes} phút` : "Chưa có thời lượng"}</small>
                      </div>
                      <span>{formatCurrency(service.price)}</span>
                    </button>
                  );
                })
              )}
            </div>
          </>
        ) : (
          <div className="reception-muted-note">Hãy chọn ít nhất một loại dịch vụ trước khi chọn dịch vụ cụ thể.</div>
        )}
      </div>

      {insuranceVehicleOptions.length > 0 ? (
        <div className="reception-service-subsection">
          <BookingInsurancePanel
            vehicleOptions={insuranceVehicleOptions}
            activeVehicleKey={activeInsuranceVehicleKey}
            onActiveVehicleChange={setActiveInsuranceVehicleKey}
            value={activeInsuranceSnapshot}
            onChange={handleInsuranceSnapshotChange}
            onUploadAsset={handleInsuranceAssetUpload}
            onRemoveAsset={handleInsuranceAssetRemove}
            uploadingState={insuranceUploadStateByVehicleKey[activeInsuranceVehicleKey] || {}}
            companies={insuranceCompanies}
            companyNameById={insuranceCompanyNameById}
            contextLabel="tiếp nhận xe"
            mode="booking"
          />
        </div>
      ) : null}
    </section>
  );
}
