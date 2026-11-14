namespace MyGarage.DTOs.Responses
{
    public class DiagnosisIntakePackageOptionDto
    {
        public long StandardServiceId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int NominalMinutes { get; set; }
        public int EffectiveMinutes { get; set; }
        public bool RequiresCustomerConsent { get; set; }
        public bool RequiresBayBlock { get; set; }
        public int Rank { get; set; }
    }
}
