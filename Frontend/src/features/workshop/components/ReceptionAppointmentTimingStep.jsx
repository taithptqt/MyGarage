import React from "react";
import { BOOKING_FLOW, SESSION_TABS } from "@features/appointments/constants/bookingConstants";
import { timeLabel } from "@features/appointments/utils/bookingCommon";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import {
  SLOT_MODE,
  formatCurrency,
  getVehicleIdFromKey,
  readField,
  toVehicleTabLabel,
} from "@features/workshop/components/receptionCreateAppointmentHelpers";

export default function ReceptionAppointmentTimingStep({
  hasMultipleSelectedVehicles,
  slotMode,
  handleSlotModeChange,
  selectedVehicleKeys,
  activeSlotVehicleKey,
  setActiveSlotVehicleKey,
  vehicleLabelByKey,
  effectiveSelectedDate,
  handleDateChange,
  dateSelectionGuardMessage,
  setErrorText,
  appointmentSummary,
  branchId,
  slotsLoading,
  effectiveSlotFlow,
  slotSelectionGuide,
  activeSession,
  setActiveSession,
  slotsBySession,
  showOddSlots,
  toggleOddSlotsForSession,
  displayedSlots,
  chosenSlot,
  handleSelectSlot,
  activeNoteKey,
  setActiveNoteVehicleKey,
  toggleVehicleSelection,
  activeNoteValue,
  onVehicleNoteChange,
  fleetCompletionDateTimeLabel,
  totalPrice,
  selectedVehicleCount,
  planSummaries,
  insuranceSummaryByVehicleKey,
}) {
  return (
    <section className="reception-appt-section reception-appt-timing-section">
      <div className="reception-appt-step-head">
        <span className="reception-step-badge">3. THỜI GIAN & ĐIỀU PHỐI</span>
      </div>

      {hasMultipleSelectedVehicles ? (
        <div className="reception-scope-switch reception-slot-mode-switch">
          <button
            type="button"
            className={slotMode === SLOT_MODE.PARALLEL ? "active" : ""}
            onClick={() => {
              void handleSlotModeChange(SLOT_MODE.PARALLEL);
            }}
          >
            Song song
          </button>
          <button
            type="button"
            className={slotMode === SLOT_MODE.INDIVIDUAL ? "active" : ""}
            onClick={() => {
              void handleSlotModeChange(SLOT_MODE.INDIVIDUAL);
            }}
          >
            Riêng lẻ
          </button>
        </div>
      ) : null}

      {slotMode === SLOT_MODE.INDIVIDUAL && selectedVehicleKeys.length > 1 ? (
        <div className="reception-selected-tabs reception-vehicle-tabs-layout">
          {selectedVehicleKeys.map((vehicleKey) => (
            <div
              key={`slot-mode-${vehicleKey}`}
              className={`reception-selected-tab ${activeSlotVehicleKey === vehicleKey ? "active" : ""}`}
            >
              <button
                type="button"
                className="reception-selected-tab-main"
                onClick={() => setActiveSlotVehicleKey(vehicleKey)}
              >
                {toVehicleTabLabel(vehicleLabelByKey.get(vehicleKey), vehicleKey)}
              </button>
            </div>
          ))}
        </div>
      ) : null}

      <div className="form-group">
        <label>Ngày đặt lịch *</label>
        <DateTimePicker
          value={effectiveSelectedDate ? `${effectiveSelectedDate}T08:00:00` : ""}
          onChange={(value) => {
            void handleDateChange(value);
          }}
          minDate={new Date()}
          showTime={false}
          iconColor="#2f6bd8"
          onOpenRequest={() => {
            if (dateSelectionGuardMessage) {
              setErrorText(dateSelectionGuardMessage);
              return false;
            }
            return true;
          }}
          placeholder="Chọn ngày..."
        />
        {appointmentSummary ? <div className="reception-muted-note">{appointmentSummary}</div> : null}
      </div>

      <div className="slot-section reception-slot-section">
        {!Number(branchId) ? (
          <div className="booking-empty">Vui lòng chọn đúng chi nhánh làm việc trước khi chọn giờ.</div>
        ) : !effectiveSelectedDate ? (
          <div className="booking-empty">
            {slotMode === SLOT_MODE.INDIVIDUAL
              ? "Vui lòng chọn ngày cho xe đang thao tác trước khi chọn giờ."
              : "Vui lòng chọn ngày trước khi chọn giờ."}
          </div>
        ) : slotsLoading ? (
          <div className="booking-empty">Đang tải khung giờ...</div>
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

            {(slotsBySession[activeSession] || []).length > 0 ? (
              <div className="slot-actions">
                <button
                  type="button"
                  className="slot-expand-toggle"
                  onClick={() => toggleOddSlotsForSession(activeSession)}
                >
                  {showOddSlots[activeSession] ? "Ẩn" : "Xem thêm"}
                </button>
              </div>
            ) : null}

            <div className="slot-grid">
              {displayedSlots.map((slot) => {
                const start = readField(slot, ["startTime", "StartTime"], "");
                const end = readField(slot, ["endTime", "EndTime"], "");
                const selectedStart = readField(chosenSlot, ["startTime", "StartTime"], "");
                const selectedEnd = readField(chosenSlot, ["endTime", "EndTime"], "");
                const isSelected = selectedStart === start && selectedEnd === end;
                const remaining = Number(readField(slot, ["remainingCapacity", "RemainingCapacity"], 0) || 0);
                const hasCapacity = remaining > 0;

                return (
                  <button
                    key={`${start}-${end}`}
                    type="button"
                    className={`slot-card ${isSelected ? "selected" : ""}`}
                    onClick={() => {
                      void handleSelectSlot(slot);
                    }}
                  >
                    <strong className="slot-time">
                      {timeLabel(start)} - {timeLabel(end)}
                    </strong>
                    <div className="slot-meta">
                      <span className={`slot-state-dot ${hasCapacity ? "ok" : "full"}`} />
                      <span className={`slot-state-text ${hasCapacity ? "ok" : "full"}`}>
                        {hasCapacity ? `Còn ${remaining} chỗ` : "Hết chỗ"}
                      </span>
                    </div>
                  </button>
                );
              })}
            </div>

            {displayedSlots.length === 0 ? (
              <div className="booking-empty">Không có khung giờ phù hợp cho ngày đã chọn.</div>
            ) : null}
          </>
        )}
      </div>

      <div className="form-group">
        <label>Ghi chú theo từng xe</label>
        {selectedVehicleKeys.length > 0 ? (
          <div className="reception-note-editor">
            {selectedVehicleKeys.length > 1 ? (
              <div className="reception-selected-tabs reception-vehicle-tabs-layout reception-note-vehicle-tabs">
                {selectedVehicleKeys.map((vehicleKey) => (
                  <div
                    key={`note-tab-${vehicleKey}`}
                    className={`reception-selected-tab ${activeNoteKey === vehicleKey ? "active" : ""}`}
                  >
                    <button
                      type="button"
                      className="reception-selected-tab-main"
                      onClick={() => setActiveNoteVehicleKey(vehicleKey)}
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

            <div className="reception-note-list">
              <textarea
                key={`note-${activeNoteKey || "none"}`}
                className="reception-note-input"
                rows={3}
                value={activeNoteValue}
                placeholder="Nhập ghi chú cho xe này..."
                onChange={(event) => onVehicleNoteChange(activeNoteKey, event.target.value)}
              />
            </div>
            {selectedVehicleKeys.length > 1 ? (
              <p className="reception-muted-note">Chọn xe ở tab bên trên để nhập ghi chú theo từng xe.</p>
            ) : null}
          </div>
        ) : (
          <div className="reception-muted-note">Hãy chọn xe trước khi nhập ghi chú.</div>
        )}
      </div>

      <div className="reception-summary-inline" role="status" aria-live="polite">
        <div className="reception-summary-inline-row">
          <span>Thời gian dự kiến hoàn tất toàn bộ xe</span>
          <strong>{fleetCompletionDateTimeLabel}</strong>
        </div>
        <div className="reception-summary-inline-row">
          <span>Tổng giá dịch vụ tất cả xe</span>
          <strong>{formatCurrency(totalPrice)}</strong>
        </div>
        <div className="reception-summary-inline-row">
          <span>Số xe</span>
          <strong>
            {selectedVehicleCount} {selectedVehicleCount > 1 ? "(lịch nhóm)" : "(lịch thường)"}
          </strong>
        </div>
      </div>

      {planSummaries.length > 0 ? (
        <div className="reception-plan-summary-grid">
          {planSummaries.map((plan) => {
            const insuranceDetails = insuranceSummaryByVehicleKey.get(plan.vehicleKey) || null;
            const insuranceSummary = insuranceDetails?.summary || null;

            return (
              <article key={`plan-summary-${plan.vehicleKey}`} className="reception-plan-summary-card">
                <div className="reception-plan-summary-head">
                  <h5 className="reception-plan-summary-title">{plan.vehicleLabel}</h5>
                  <span className="reception-plan-summary-badge">
                    {`${plan.serviceIds.length} dịch vụ`}
                  </span>
                </div>

                <div className="reception-plan-summary-block">
                  <p className="reception-plan-summary-block-label">DỊCH VỤ ĐÃ CHỌN</p>
                  {Array.isArray(plan.serviceGroupsByCategory) && plan.serviceGroupsByCategory.length > 0 ? (
                    <ul className="reception-plan-summary-list">
                      {plan.serviceGroupsByCategory.map((group) => (
                        <li key={`${plan.vehicleKey}-${group.categoryName}`} className="reception-plan-summary-list-item">
                          <span className="reception-plan-summary-list-group">{group.categoryName}</span>
                          <ul className="reception-plan-summary-sublist">
                            {(group.serviceNames || []).map((serviceName) => (
                              <li
                                key={`${plan.vehicleKey}-${group.categoryName}-${serviceName}`}
                                className="reception-plan-summary-sublist-item"
                              >
                                {serviceName}
                              </li>
                            ))}
                          </ul>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="reception-plan-summary-text">Chưa có dịch vụ</p>
                  )}
                </div>

                {insuranceSummary ? (
                  <div className="reception-plan-summary-block">
                    <p className="reception-plan-summary-block-label">BẢO HIỂM</p>
                    <div className="reception-plan-summary-time-row">
                      <span>Công ty bảo hiểm:</span>
                      <strong>{insuranceSummary.companyName}</strong>
                    </div>
                    <div className="reception-plan-summary-time-row">
                      <span>Hồ sơ bảo hiểm:</span>
                      <strong>{insuranceSummary.assetSummaryLabel}</strong>
                    </div>
                    <p className="reception-plan-summary-text">
                      {`Ghi chú bảo hiểm: ${insuranceSummary.intakeNote}`}
                    </p>
                  </div>
                ) : null}

                <div className="reception-plan-summary-block">
                  <p className="reception-plan-summary-block-label">KHUNG GIỜ</p>
                  <div className="reception-plan-summary-time-row">
                    <span>Giờ bắt đầu:</span>
                    <strong>{plan.startTimeLabel}</strong>
                  </div>
                  <div className="reception-plan-summary-time-row">
                    <span>Giờ kết thúc:</span>
                    <strong>{plan.endTimeLabel}</strong>
                  </div>
                  <div className="reception-plan-summary-time-row">
                    <span>Tổng thời lượng:</span>
                    <strong>{`${plan.totalMinutes} phút`}</strong>
                  </div>
                </div>

                {plan.bookingFlow === BOOKING_FLOW.FLEXIBLE && plan.flowWarningLabel ? (
                  <p className="reception-plan-summary-warning">
                    {`* ${plan.flowWarningLabel}`}
                  </p>
                ) : null}

                <div className="reception-plan-summary-footer">
                  <span>Tổng giá dịch vụ</span>
                  <strong>{formatCurrency(plan.totalPrice)}</strong>
                </div>
              </article>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}
