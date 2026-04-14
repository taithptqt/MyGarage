using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests
{
    public class WorkshopServiceOrderEditRequestDto
    {
        public string? OrderCategory { get; set; }
        public long? ServiceAdvisorId { get; set; }
        public string? OldPartsReturnOption { get; set; }
        public decimal? RequiredDepositAmount { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? EstimatedHandover { get; set; }
        public DateTime? PromisedHandoverTime { get; set; }
        public string? Status { get; set; }
        public long? InsuranceCompanyId { get; set; }
        [Range(0, 1000000000, ErrorMessage = "Mức miễn thường không hợp lệ.")]
        public decimal? InsuranceDeductibleAmount { get; set; }
        public string? InsuranceDeductibleOption { get; set; }
        public string? InsuranceClaimMode { get; set; }
        public string? InsuranceQuoteState { get; set; }
        public List<string> InsuranceCardImageUrls { get; set; } = new();
        public List<string> InsuranceDamageImageUrls { get; set; } = new();
        public List<long> ServiceIds { get; set; } = new();
        public List<long> PackageIds { get; set; } = new();
        public List<string> SymptomNotes { get; set; } = new();
    }
}
