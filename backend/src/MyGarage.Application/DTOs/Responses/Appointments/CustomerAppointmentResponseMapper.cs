using System.Collections.Generic;
using System.Linq;

namespace MyGarage.DTOs.Responses
{
    public static class CustomerAppointmentResponseMapper
    {
        public static AppointmentCustomerResponseDto ToCustomerResponse(this AppointmentDetailResponseDto source)
        {
            return new AppointmentCustomerResponseDto
            {
                Id = source.Id,
                BranchId = source.BranchId,
                BranchName = source.BranchName,
                CustomerId = source.CustomerId,
                CustomerName = source.CustomerName,
                CustomerPhone = source.CustomerPhone,
                VehicleId = source.VehicleId,
                VehicleLicensePlate = source.VehicleLicensePlate,
                VehicleModelName = source.VehicleModelName,
                GroupSessionId = source.GroupSessionId,
                GroupSessionCode = source.GroupSessionCode,
                GroupSessionStatus = source.GroupSessionStatus,
                CreatedBy = source.CreatedBy,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                CreatedAt = source.CreatedAt,
                RequestedServiceIds = source.RequestedServiceIds,
                RequestedPackageIds = source.RequestedPackageIds,
                InsuranceCompanyId = source.InsuranceCompanyId,
                InsuranceCompanyName = source.InsuranceCompanyName,
                InsuranceDeductibleAmount = source.InsuranceDeductibleAmount,
                InsuranceDeductibleOption = source.InsuranceDeductibleOption,
                InsuranceClaimMode = source.InsuranceClaimMode,
                InsuranceQuoteState = source.InsuranceQuoteState,
                InsuranceCardImageUrls = source.InsuranceCardImageUrls?.ToList() ?? new List<string>(),
                InsuranceDamageImageUrls = source.InsuranceDamageImageUrls?.ToList() ?? new List<string>(),
                ServiceNames = source.ServiceNames?.ToList() ?? new List<string>(),
                PackageNames = source.PackageNames?.ToList() ?? new List<string>(),
                CustomerNotes = source.CustomerNotes,
                RequiresDispatchReview = source.RequiresDispatchReview,
                DispatchReviewNote = source.DispatchReviewNote,
                IsReminderSent = source.IsReminderSent,
                Status = source.Status,
                CancelReason = source.CancelReason,
                UpdatedAt = source.UpdatedAt
            };
        }

        public static GroupSessionCustomerResponseDto ToCustomerResponse(this GroupSessionResponseDto source)
        {
            return new GroupSessionCustomerResponseDto
            {
                Id = source.Id,
                GroupCode = source.GroupCode,
                CustomerId = source.CustomerId,
                BranchId = source.BranchId,
                Status = source.Status,
                TotalAppointments = source.TotalAppointments,
                ConfirmedAppointments = source.ConfirmedAppointments,
                CanceledAppointments = source.CanceledAppointments,
                ConfirmedAt = source.ConfirmedAt,
                CanceledAt = source.CanceledAt,
                CancelReason = source.CancelReason,
                CustomerNote = source.CustomerNote,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt,
                Appointments = source.Appointments?.Select(ToCustomerResponse).ToList() ?? new List<AppointmentCustomerResponseDto>()
            };
        }
    }
}
