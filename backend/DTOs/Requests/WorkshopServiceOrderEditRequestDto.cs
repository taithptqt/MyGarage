using System;
using System.Collections.Generic;

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
        public List<long> ServiceIds { get; set; } = new();
        public List<string> SymptomNotes { get; set; } = new();
    }
}
