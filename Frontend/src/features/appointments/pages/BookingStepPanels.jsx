import { Fragment } from "react";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import VehicleActionPanel from "@features/appointments/components/VehicleActionPanel";
import BookingInsurancePanel from "@features/appointments/components/BookingInsurancePanel";
import { BOOKING_FLOW, SESSION_TABS } from "@features/appointments/constants/bookingConstants";
import {
  extractField,
  formatVehicleTabLabel,
  formatVnd,
  iconByIndex,
  resolveBookingItemIcon,
  timeLabel,
} from "@features/appointments/utils/bookingCommon";
import { VIETNAM_PHONE_PATTERN } from "@shared/utils/contactValidation";
import { getInspectionTriadDisplayName } from "@shared/utils/serviceDurationUtils";
import { getBranchClosedMessage, isBranchClosedBySchedule } from "@shared/utils/branchOperatingSchedule";
import {
  FLEET_SERVICE_PRICE_LABEL,
  SERVICE_PRICE_DISCLAIMER,
  SERVICE_PRICE_LABEL,
} from "@shared/constants/servicePricing";
import { SERVICE_MODE, SLOT_MODE } from "@features/appointments/pages/bookingPageHelpers";

export function BookingStep1Customer({
  customerForm,
  setCustomerForm,
  linkedVehicles,
  multiVehicles,
  brands,
  models,
  activeSymptomVehicleKey,
  setActiveSymptomVehicleKey,
  customerHasNoSavedVehicles,
  draftVehicle,
  updateDraftVehicle,
  toggleExistingVehicle,
  addNewVehicle,
  handleRemoveVehicle,
  activeSymptomVehicle,
  vehicleSymptoms,
  onSymptomChange,
}) {
  return (
    <div className="booking-card">
      <header className="booking-card-head">
        <h2>Bước 1: Thông tin khách hàng & xe</h2>
      </header>

      {/* Cụm 1: Thông tin khách hàng */}
      <section className="booking-cluster customer-cluster">
        <div className="form-grid step4-form-grid">
          <label className="booking-field-group">
            <span className="booking-field-label">
              Họ và tên <span className="field-required">*</span>
            </span>
            <input
              type="text"
              placeholder="Nhập họ và tên..."
              value={customerForm.fullName}
              onChange={(e) => setCustomerForm((prev) => ({ ...prev, fullName: e.target.value }))}
            />
          </label>
          <label className="booking-field-group">
            <span className="booking-field-label">
              Số điện thoại <span className="field-required">*</span>
            </span>
            <input
              type="tel"
              inputMode="tel"
              pattern={VIETNAM_PHONE_PATTERN}
              title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
              placeholder="Nhập số điện thoại..."
              value={customerForm.phone}
              onChange={(e) => setCustomerForm((prev) => ({ ...prev, phone: e.target.value }))}
            />
          </label>
        </div>
      </section>

      {/* Cụm 2: Danh sách xe */}
      <section className="booking-cluster vehicle-cluster">
        <VehicleActionPanel
          linkedVehicles={linkedVehicles}
          selectedVehicles={multiVehicles}
          brands={brands}
          models={models}
          activeVehicleKey={activeSymptomVehicleKey}
          forceNewVehicleWhenNoSaved={customerHasNoSavedVehicles}
          onSelectVehicle={setActiveSymptomVehicleKey}
          draftVehicle={draftVehicle}
          onDraftChange={updateDraftVehicle}
          onToggleExistingVehicle={toggleExistingVehicle}
          onAddNewVehicle={addNewVehicle}
          onRemoveVehicle={handleRemoveVehicle}
        />
      </section>

      {/* Cụm 3: Ghi chú */}
      <section className="booking-cluster notes-cluster">
        <div className="notes-field-group">
          <label className="booking-field-label notes-field-label" htmlFor="booking-customer-notes">
            Ghi chú
          </label>
          {(multiVehicles || []).length > 0 ? (
            <div className="notes-box">
              <div className="notes-active-vehicle">
                <strong>{activeSymptomVehicle?.displayLabel || ""}</strong>
              </div>
              <textarea
                id="booking-customer-notes"
                rows={3}
                value={vehicleSymptoms[activeSymptomVehicle?.key] || ""}
                placeholder="Nhập ghi chú cho xe này..."
                onChange={(e) => onSymptomChange(activeSymptomVehicle?.key || "", e.target.value)}
              />
              <p className="vehicle-batch-note">
                Chọn xe ở danh sách phía trên để nhập ghi chú cho từng xe.
              </p>
            </div>
          ) : (
            <div className="notes-box">
              <textarea
                id="booking-customer-notes"
                rows={3}
                disabled
                placeholder="Vui lòng chọn hoặc thêm xe ở trên để nhập ghi chú..."
              />
            </div>
          )}
        </div>
      </section>
    </div>
  );
}

export function BookingStep2Categories({
  multiVehicles,
  step2VehicleKey,
  setStep2VehicleKey,
  dispatch,
  step2Vehicle,
  handleRemoveVehicle,
  categories,
  step2CompatibleCategoryIds,
  getEffectiveCategoryIds,
  sharedCategoryIds,
  toggleVehicleCategory,
  toggleSharedCategory,
}) {
  return (
    <div className="booking-card">
      <header className="booking-card-head">
        <h2>Bước 2: Phân loại dịch vụ</h2>
      </header>
      {multiVehicles.length > 1 ? (
        <div className="booking-scope-switch">
          <button
            type="button"
            className={!step2VehicleKey ? "active" : ""}
            onClick={() => {
              setStep2VehicleKey("");
              dispatch({ type: "SET_SERVICE_MODE", payload: SERVICE_MODE.SHARED });
            }}
          >
            Áp dụng tất cả xe
          </button>
          <button
            type="button"
            className={step2VehicleKey ? "active" : ""}
            onClick={() => {
              if (!step2VehicleKey && multiVehicles[0]) setStep2VehicleKey(multiVehicles[0].key);
              dispatch({ type: "SET_SERVICE_MODE", payload: SERVICE_MODE.SHARED });
            }}
          >
            Tùy chỉnh theo từng xe
          </button>
        </div>
      ) : null}
      {multiVehicles.length > 1 ? (
        <div className="selected-category-tabs vehicle-tabs step2-vehicle-tabs">
          {multiVehicles.map((vehicle) => (
            <div
              key={`step2-${vehicle.key}`}
              className={`selected-category-tab ${step2Vehicle?.key === vehicle.key ? "active" : ""}`}
            >
              <button
                type="button"
                className="selected-category-tab-main"
                onClick={() => setStep2VehicleKey(vehicle.key)}
              >
                {formatVehicleTabLabel(vehicle)}
              </button>
              <button
                type="button"
                className="selected-category-tab-remove"
                onClick={(e) => {
                  e.stopPropagation();
                  handleRemoveVehicle(vehicle.key);
                }}
              >
                x
              </button>
            </div>
          ))}
        </div>
      ) : null}
      <div className="category-grid">
        {categories
          .filter((item) => step2CompatibleCategoryIds.includes(Number(extractField(item, ["id", "Id"]))))
          .map((item) => {
            const id = Number(extractField(item, ["id", "Id"]));
            const selected = step2Vehicle
              ? getEffectiveCategoryIds(step2Vehicle).includes(id)
              : sharedCategoryIds.includes(id);
            return (
              <button
                key={id}
                type="button"
                className={`category-card ${selected ? "selected" : ""}`}
                onClick={() => {
                  if (step2Vehicle) toggleVehicleCategory(step2Vehicle, id);
                  else toggleSharedCategory(id);
                }}
              >
                <span className="category-icon">{iconByIndex(id || 0)}</span>
                <h3>{extractField(item, ["name", "Name"], "")}</h3>
              </button>
            );
          })}
      </div>
    </div>
  );
}

export function BookingStep3Services({
  multiVehicles,
  step3VehicleKey,
  setStep3VehicleKey,
  step3Vehicle,
  handleRemoveVehicle,
  step3CategoryIds,
  activeCategoryId,
  setActiveCategoryId,
  categories,
  toggleVehicleCategory,
  toggleSharedCategory,
  servicesByCategory,
  getEffectiveServiceIds,
  sharedServiceIds,
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
    <div className="booking-card">
      <header className="booking-card-head">
        <h2>Bước 3: Chi tiết dịch vụ</h2>
      </header>
      <>
        {multiVehicles.length > 1 ? (
          <div className="booking-scope-switch">
            <button
              type="button"
              className={!step3VehicleKey ? "active" : ""}
              onClick={() => setStep3VehicleKey("")}
            >
              Áp dụng tất cả xe
            </button>
            <button
              type="button"
              className={step3VehicleKey ? "active" : ""}
              onClick={() => {
                if (!step3VehicleKey && multiVehicles[0]) setStep3VehicleKey(multiVehicles[0].key);
              }}
            >
              Tùy chỉnh theo từng xe
            </button>
          </div>
        ) : null}
        {multiVehicles.length > 1 ? (
          <div className="selected-category-tabs vehicle-tabs step3-vehicle-tabs">
            {multiVehicles.map((vehicle) => (
              <div
                key={`step3-${vehicle.key}`}
                className={`selected-category-tab ${step3Vehicle?.key === vehicle.key ? "active" : ""}`}
              >
                <button
                  type="button"
                  className="selected-category-tab-main"
                  onClick={() => setStep3VehicleKey(vehicle.key)}
                >
                  {formatVehicleTabLabel(vehicle)}
                </button>
                <button
                  type="button"
                  className="selected-category-tab-remove"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleRemoveVehicle(vehicle.key);
                  }}
                >
                  x
                </button>
              </div>
            ))}
          </div>
        ) : null}
        <div className="selected-category-tabs">
          {step3CategoryIds.map((id) => (
            <div
              key={`cat-${id}`}
              className={`selected-category-tab ${String(activeCategoryId) === String(id) ? "active" : ""}`}
            >
              <button
                type="button"
                className="selected-category-tab-main"
                onClick={() => setActiveCategoryId(String(id))}
              >
                {extractField(
                  categories.find((item) => Number(extractField(item, ["id", "Id"])) === Number(id)),
                  ["name", "Name"],
                  ""
                ) || `Nhóm ${id}`}
              </button>
              <button
                type="button"
                className="selected-category-tab-remove"
                onClick={(e) => {
                  e.stopPropagation();
                  if (step3Vehicle) toggleVehicleCategory(step3Vehicle, id);
                  else toggleSharedCategory(id);
                }}
              >
                x
              </button>
            </div>
          ))}
        </div>
        <div className="service-list">
          {servicesByCategory.map((service) => {
            const id = Number(extractField(service, ["id", "Id"]));
            const selected = step3Vehicle
              ? getEffectiveServiceIds(step3Vehicle).includes(id)
              : sharedServiceIds.includes(id);
            const minutes = Number(extractField(service, ["standardMinutes", "StandardMinutes"], 0) || 0);
            return (
              <button
                key={id}
                type="button"
                className={`service-card ${selected ? "selected" : ""}`}
                onClick={() => toggleService(id)}
              >
                <span className="service-icon">{resolveBookingItemIcon(service)}</span>
                <div className="service-card-body">
                  <h3>{getInspectionTriadDisplayName(service, extractField(service, ["name", "Name"], ""))}</h3>
                  <small>{minutes > 0 ? `${minutes} phút` : "Chưa có thời lượng"}</small>
                </div>
                <aside className="service-card-side">
                  <strong>{formatVnd(extractField(service, ["basePrice", "BasePrice"], 0))}</strong>
                </aside>
              </button>
            );
          })}
        </div>
        {insuranceVehicleOptions.length > 0 ? (
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
            contextLabel="xe vào xưởng"
            mode="booking"
          />
        ) : null}
      </>
    </div>
  );
}

export function BookingStep4Branch({ branches, selectedBranchId, setSelectedBranchId, dispatch }) {
  return (
    <div className="booking-card">
      <header className="booking-card-head">
        <h2>Bước 4: Chọn chi nhánh</h2>
      </header>
      <div className="branch-list">
        {branches.map((branch) => {
          const id = extractField(branch, ["id", "Id"]);
          const selected = String(id) === String(selectedBranchId);
          return (
            <button
              key={id}
              type="button"
              className={`branch-card ${selected ? "selected" : ""}`}
              onClick={() => {
                setSelectedBranchId(String(id));
                dispatch({ type: "RESET_SLOTS" });
              }}
            >
              <h3>{extractField(branch, ["name", "Name"], "Chi nhánh")}</h3>
              <p>{extractField(branch, ["address", "Address"], "")}</p>
            </button>
          );
        })}
      </div>
    </div>
  );
}

export function BookingStep5Slots({
  multi,
  dispatch,
  activeVehicle,
  setActiveVehicleKey,
  effectiveSelectedDate,
  branchOperatingHours,
  selectedBranchId,
  showError,
  releaseHoldTokenIfAny,
  setSelectedDatesByVehicle,
  setSelectedDate,
  slotsLoading,
  effectiveSlotFlow,
  slotSelectionGuide,
  activeSession,
  setActiveSession,
  canToggleOddSlots,
  toggleOddSlotsForSession,
  showOddSlots,
  displayedSlots,
  handleSelectSlot,
}) {
  return (
    <div className="booking-card">
      <header className="booking-card-head">
        <h2>Bước 5: Chọn khung giờ</h2>
      </header>
      {multi.vehicles.length > 1 ? (
        <div className="booking-scope-switch">
          <button
            type="button"
            className={multi.slotMode === SLOT_MODE.PARALLEL ? "active" : ""}
            onClick={() => {
              dispatch({ type: "SET_SLOT_MODE", payload: SLOT_MODE.PARALLEL });
              dispatch({ type: "RESET_SLOTS" });
            }}
          >
            Song song
          </button>
          <button
            type="button"
            className={multi.slotMode === SLOT_MODE.INDIVIDUAL ? "active" : ""}
            onClick={() => {
              dispatch({ type: "SET_SLOT_MODE", payload: SLOT_MODE.INDIVIDUAL });
              dispatch({ type: "RESET_SLOTS" });
            }}
          >
            Riêng lẻ
          </button>
        </div>
      ) : null}
      {multi.slotMode === SLOT_MODE.INDIVIDUAL ? (
        <div className="selected-category-tabs vehicle-tabs">
          {multi.vehicles.map((vehicle) => (
            <div
              key={vehicle.key}
              className={`selected-category-tab ${activeVehicle?.key === vehicle.key ? "active" : ""}`}
            >
              <button
                type="button"
                className="selected-category-tab-main"
                onClick={() => setActiveVehicleKey(vehicle.key)}
              >
                {formatVehicleTabLabel(vehicle)}
              </button>
            </div>
          ))}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="booking-field-group">
          <span className="booking-field-label">Ngày</span>
          <DateTimePicker
            value={effectiveSelectedDate ? `${effectiveSelectedDate}T08:00:00` : ""}
            minDate={new Date()}
            showTime={false}
            onChange={(value) => {
              const nextDate = String(value || "").slice(0, 10);
              if (
                nextDate &&
                isBranchClosedBySchedule(branchOperatingHours, selectedBranchId, `${nextDate}T08:00:00`)
              ) {
                showError(getBranchClosedMessage(`${nextDate}T08:00:00`));
                return;
              }
              if (multi.slotMode === SLOT_MODE.INDIVIDUAL && activeVehicle?.key) {
                void releaseHoldTokenIfAny(activeVehicle.key);
                setSelectedDatesByVehicle((prev) => ({ ...prev, [activeVehicle.key]: nextDate }));
                dispatch({ type: "CLEAR_INDIVIDUAL_SLOT", payload: activeVehicle.key });
              } else {
                setSelectedDate(nextDate);
                dispatch({ type: "RESET_SLOTS" });
              }
            }}
          />
        </div>
      </div>
      <div className="slot-section">
        {!selectedBranchId ? (
          <div className="booking-empty">Vui lòng chọn chi nhánh trước khi chọn giờ.</div>
        ) : !effectiveSelectedDate ? (
          <div className="booking-empty">Vui lòng chọn ngày cho xe đang thao tác trước khi chọn giờ.</div>
        ) : slotsLoading ? (
          <div className="booking-empty slot-loading-empty">Đang tải khung giờ...</div>
        ) : (
          <>
            <p className={`slot-flow-note ${effectiveSlotFlow === BOOKING_FLOW.FLEXIBLE ? "checkin" : "repair"}`}>
              {slotSelectionGuide}
            </p>
            <div className="session-tabs">
              {SESSION_TABS.map((tab) => (
                <button
                  key={tab.key}
                  type="button"
                  className={`session-tab ${activeSession === tab.key ? "active" : ""}`}
                  onClick={() => setActiveSession(tab.key)}
                >
                  <strong>{tab.label}</strong>
                  <small>{tab.hint}</small>
                </button>
              ))}
            </div>
            {canToggleOddSlots ? (
              <div className="slot-actions">
                <button
                  type="button"
                  className="slot-expand-toggle"
                  onClick={() => toggleOddSlotsForSession(activeSession)}
                >
                  {showOddSlots[activeSession] ? "Ẩn" : "Xem thêm giờ"}
                </button>
              </div>
            ) : null}
            <div className="slot-grid">
              {displayedSlots.map((slot) => {
                const start = extractField(slot, ["startTime", "StartTime"]);
                const end = extractField(slot, ["endTime", "EndTime"]);
                const chosen =
                  multi.slotMode === SLOT_MODE.PARALLEL
                    ? multi.parallelSlot
                    : multi.selectedSlots[activeVehicle?.key || ""];
                const isSelected =
                  chosen &&
                  extractField(chosen, ["startTime", "StartTime"]) === start &&
                  extractField(chosen, ["endTime", "EndTime"]) === end;

                return (
                  <button
                    key={`${start}-${end}`}
                    type="button"
                    className={`slot-card ${isSelected ? "selected" : ""}`}
                    onClick={() => handleSelectSlot(slot)}
                  >
                    <strong className="slot-time">
                      {timeLabel(start)} - {timeLabel(end)}
                    </strong>
                    <div className="slot-meta">
                      <span
                        className={`slot-state-dot ${
                          Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)) > 0 ? "ok" : "full"
                        }`}
                      />
                      <span
                        className={`slot-state-text ${
                          Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)) > 0 ? "ok" : "full"
                        }`}
                      >
                        {Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0)) > 0
                          ? `Còn ${Number(extractField(slot, ["remainingCapacity", "RemainingCapacity"], 0))} chỗ`
                          : "Hết chỗ"}
                      </span>
                    </div>
                  </button>
                );
              })}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

export function BookingStep6Confirmation({
  success,
  navigate,
  successTargetTab,
  customerForm,
  selectedBranch,
  confirmationVehicleSummaries,
  flexibleCompletionNoteByVehicleKey,
  totalPrice,
  totalDurationLabel,
  handleConfirm,
  submitting,
}) {
  return (
    <div className="summary-card summary-card-large">
      {success ? (
        <div className="booking-success-screen">
          <div className="booking-success-icon">✓</div>
          <h3>Đặt lịch thành công</h3>
          <p>{success}</p>
          <div className="success-actions">
            <button type="button" onClick={() => navigate("/")}>
              Về trang chủ
            </button>
            <button type="button" onClick={() => navigate(`/my-appointments?tab=${successTargetTab}`)}>
              Xem lịch hẹn
            </button>
          </div>
        </div>
      ) : (
        <>
          <h3>Xác nhận đặt lịch</h3>
          <dl>
            <dt>Khách hàng</dt>
            <dd>{customerForm.fullName}</dd>
            <dt>SĐT</dt>
            <dd>{customerForm.phone}</dd>
            <dt>Chi nhánh</dt>
            <dd>{extractField(selectedBranch, ["name", "Name"], "Chưa chọn")}</dd>
          </dl>
          <div className="summary-vehicle-services">
            <h4>Tóm tắt theo từng xe</h4>
            <div className="summary-vehicle-list">
              {confirmationVehicleSummaries.map((vehicle) => (
                <article key={`confirm-vehicle-${vehicle.key}`} className="summary-vehicle-card">
                  <strong>{vehicle.label}</strong>
                  <div className="summary-vehicle-content">
                    <section className="summary-vehicle-section">
                      <p className="summary-vehicle-section-title">Dịch vụ đã chọn</p>
                      {vehicle.serviceHierarchy.length > 0 ? (
                        <ul className="summary-service-list">
                          {vehicle.serviceHierarchy.map((group) => (
                            <li key={`${vehicle.key}-${group.categoryName}`}>
                              <span>{group.categoryName}</span>
                              <ul className="summary-service-sublist">
                                {group.serviceNames.map((serviceName) => (
                                  <li key={`${vehicle.key}-${group.categoryName}-${serviceName}`}>{serviceName}</li>
                                ))}
                              </ul>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <p className="summary-vehicle-empty">Chưa chọn</p>
                      )}
                    </section>
                    {vehicle.insuranceSummary ? (
                      <section className="summary-vehicle-section">
                        <p className="summary-vehicle-section-title">Bảo hiểm</p>
                        <p>
                          <span>Công ty bảo hiểm:</span> {vehicle.insuranceSummary.companyName}
                        </p>
                        <p>
                          <span>Ảnh bảo hiểm:</span> {vehicle.insuranceSummary.assetSummaryLabel}
                        </p>
                        <p>
                          <span>Ghi chú bảo hiểm:</span> {vehicle.insuranceSummary.intakeNote}
                        </p>
                      </section>
                    ) : null}
                    <section className="summary-vehicle-section">
                      <p className="summary-vehicle-section-title">Khung giờ</p>
                      <p>
                        <span>Giờ bắt đầu:</span> {vehicle.slotStartLabel}
                      </p>
                      <p>
                        <span>Giờ kết thúc:</span> {vehicle.slotEndLabel}
                      </p>
                    </section>
                    <section className="summary-vehicle-section">
                      <p className="summary-vehicle-section-title">Thông tin thêm</p>
                      <p className="summary-note-line">
                        <span>Ghi chú:</span>
                        <span className="summary-note-text">{vehicle.noteValue || "Không có"}</span>
                      </p>
                      <p>
                        <span>{SERVICE_PRICE_LABEL}:</span> {formatVnd(vehicle.totalVehiclePrice)}
                      </p>
                      <p>
                        <span>Thời lượng xe:</span> {vehicle.durationLabel}
                      </p>
                    </section>
                    {vehicle.bookingFlow === BOOKING_FLOW.FLEXIBLE &&
                    flexibleCompletionNoteByVehicleKey.get(vehicle.key) ? (
                      <p className="summary-vehicle-flow-note">
                        * {flexibleCompletionNoteByVehicleKey.get(vehicle.key)}
                      </p>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>
          </div>
          <div className="summary-total">
            <div>
              <span>{FLEET_SERVICE_PRICE_LABEL}</span>
              <strong>{formatVnd(totalPrice)}</strong>
              <p className="summary-disclaimer">* {SERVICE_PRICE_DISCLAIMER}</p>
            </div>
            <div>
              <span>Thời lượng dự kiến xong tất cả xe</span>
              <strong>{totalDurationLabel}</strong>
            </div>
          </div>
          <button type="button" className="primary-submit" onClick={handleConfirm} disabled={submitting}>
            {submitting ? "Đang gửi lịch hẹn..." : "Xác nhận đặt lịch"}
          </button>
        </>
      )}
    </div>
  );
}

export function BookingSummarySidebar({
  summaryItems,
  summaryVehicleDetails,
  flexibleCompletionNoteByVehicleKey,
  totalPrice,
  fleetCompletionLabel,
}) {
  return (
    <aside className="booking-summary-panel">
      <div className="summary-card">
        <h3>Tóm tắt lịch hẹn</h3>
        <dl>
          {summaryItems.map((item) => (
            <Fragment key={item.key}>
              <dt>{item.label}</dt>
              <dd>
                {Array.isArray(item.values) ? (
                  <ul className="summary-bullet-list">
                    {item.values.map((entry, index) => (
                      <li key={`${item.key}-${index}`}>{entry}</li>
                    ))}
                  </ul>
                ) : (
                  item.value
                )}
              </dd>
            </Fragment>
          ))}
        </dl>
        <div className="summary-vehicle-services">
          <h4>Chi tiết theo từng xe</h4>
          <div className="summary-vehicle-list">
            {summaryVehicleDetails.map((vehicle, index) => (
              <article key={`sum-vehicle-${vehicle.key}`} className="summary-vehicle-card">
                <div className="summary-vehicle-header">
                  <strong>{vehicle.label}</strong>
                  <span>Xe {index + 1}</span>
                </div>
                <div className="summary-vehicle-content">
                  <section className="summary-vehicle-section">
                    <p className="summary-vehicle-section-title">Dịch vụ đã chọn</p>
                    {vehicle.serviceHierarchy.length > 0 ? (
                      <ul className="summary-service-list">
                        {vehicle.serviceHierarchy.map((group) => (
                          <li key={`${vehicle.key}-${group.categoryName}`}>
                            <span>{group.categoryName}</span>
                            <ul className="summary-service-sublist">
                              {group.serviceNames.map((serviceName) => (
                                <li key={`${vehicle.key}-${group.categoryName}-${serviceName}`}>{serviceName}</li>
                              ))}
                            </ul>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="summary-vehicle-empty">Chưa chọn</p>
                    )}
                  </section>
                  {vehicle.insuranceSummary ? (
                    <section className="summary-vehicle-section">
                      <p className="summary-vehicle-section-title">Bảo hiểm</p>
                      <p>
                        <span>Công ty bảo hiểm:</span> {vehicle.insuranceSummary.companyName}
                      </p>
                      <p>
                        <span>Ảnh bảo hiểm:</span> {vehicle.insuranceSummary.assetSummaryLabel}
                      </p>
                      <p>
                        <span>Ghi chú bảo hiểm:</span> {vehicle.insuranceSummary.intakeNote}
                      </p>
                    </section>
                  ) : null}
                  <section className="summary-vehicle-section">
                    <p className="summary-vehicle-section-title">Khung giờ</p>
                    <p>
                      <span>Giờ bắt đầu:</span> {vehicle.slotStartLabel}
                    </p>
                    <p>
                      <span>Giờ kết thúc:</span> {vehicle.slotEndLabel}
                    </p>
                  </section>
                  <section className="summary-vehicle-section">
                    <p className="summary-vehicle-section-title">Chi phí và thời lượng</p>
                    <p>
                      <span>{SERVICE_PRICE_LABEL}:</span> {formatVnd(vehicle.vehiclePrice)}
                    </p>
                    <p>
                      <span>Thời lượng xe:</span> {vehicle.durationLabel}
                    </p>
                  </section>
                  {vehicle.bookingFlow === BOOKING_FLOW.FLEXIBLE &&
                  flexibleCompletionNoteByVehicleKey.get(vehicle.key) ? (
                    <p className="summary-vehicle-flow-note">
                      * {flexibleCompletionNoteByVehicleKey.get(vehicle.key)}
                    </p>
                  ) : null}
                </div>
              </article>
            ))}
          </div>
        </div>
        <div className="summary-total">
          <div>
            <span>{FLEET_SERVICE_PRICE_LABEL}</span>
            <strong>{formatVnd(totalPrice)}</strong>
            <p className="summary-disclaimer">* {SERVICE_PRICE_DISCLAIMER}</p>
          </div>
          <div>
            <span>Thời gian dự kiến hoàn tất toàn bộ xe</span>
            <strong>{fleetCompletionLabel}</strong>
          </div>
        </div>
      </div>
    </aside>
  );
}
