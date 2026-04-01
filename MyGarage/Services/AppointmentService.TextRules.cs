using System.Globalization;
using System.Linq;
using System.Text;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static bool IsHeavyHint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var text = NormalizeText(value);
            return text.Contains("sua chua")
                   || text.Contains("repair")
                   || text.Contains("bao hanh")
                   || text.Contains("bao hiem")
                   || text.Contains("rework")
                   || text.Contains("chan doan")
                   || text.Contains("chuan doan")
                   || text.Contains("kiem tra");
        }

        private static string NormalizeText(string value)
        {
            var lowered = value.Trim().ToLowerInvariant();
            return string.Concat(lowered.Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
                .Normalize(NormalizationForm.FormC);
        }
    }
}
