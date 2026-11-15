using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyGarage.Interfaces;

public sealed class ChatbotBookingState
{
    public ChatbotBookingStep Step { get; set; } = ChatbotBookingStep.IdentifyOrCreateVehicle;
    public long? VehicleId { get; set; }
    public long? ServiceCategoryId { get; set; }
    public List<long> ServiceIds { get; set; } = [];
    public long? BranchId { get; set; }
    public string? BookingDate { get; set; }
    public string? SelectedSlotStartUtc { get; set; }
    public string? SelectedSlotEndUtc { get; set; }
    public string? SlotHoldToken { get; set; }
    public string? SlotHoldExpiresAtUtc { get; set; }
    public string? PrefillInput { get; set; }
    public int TurnCount { get; set; }
    public bool AutoConfirmRequested { get; set; }

    public ChatbotBookingState Reset() => new();

    public void ClearSlotSelection()
    {
        SelectedSlotStartUtc = null;
        SelectedSlotEndUtc = null;
        SlotHoldToken = null;
        SlotHoldExpiresAtUtc = null;
    }

    public JsonNode ToJson() => JsonSerializer.SerializeToNode(this) ?? new JsonObject();

    public static ChatbotBookingState From(string? intentState)
    {
        if (string.IsNullOrWhiteSpace(intentState))
        {
            return new ChatbotBookingState();
        }

        try
        {
            using var doc = JsonDocument.Parse(intentState);
            if (!doc.RootElement.TryGetProperty("booking", out var b))
            {
                return new ChatbotBookingState();
            }

            var s = new ChatbotBookingState();
            if (b.TryGetProperty("Step", out var st) || b.TryGetProperty("step", out st))
            {
                if (st.ValueKind == JsonValueKind.String && Enum.TryParse<ChatbotBookingStep>(st.GetString(), true, out var parsed))
                {
                    s.Step = parsed;
                }
                else if (st.ValueKind == JsonValueKind.Number && st.TryGetInt32(out var i) && i >= 0 && i <= 5)
                {
                    s.Step = (ChatbotBookingStep)i;
                }
            }

            if ((b.TryGetProperty("VehicleId", out var v) || b.TryGetProperty("vehicleId", out v)) && v.ValueKind == JsonValueKind.Number) s.VehicleId = v.GetInt64();
            if ((b.TryGetProperty("ServiceCategoryId", out var cat) || b.TryGetProperty("serviceCategoryId", out cat)) && cat.ValueKind == JsonValueKind.Number) s.ServiceCategoryId = cat.GetInt64();
            if ((b.TryGetProperty("BranchId", out var br) || b.TryGetProperty("branchId", out br)) && br.ValueKind == JsonValueKind.Number) s.BranchId = br.GetInt64();
            if ((b.TryGetProperty("BookingDate", out var d) || b.TryGetProperty("bookingDate", out d)) && d.ValueKind == JsonValueKind.String) s.BookingDate = d.GetString();
            if ((b.TryGetProperty("SelectedSlotStartUtc", out var ss) || b.TryGetProperty("selectedSlotStartUtc", out ss)) && ss.ValueKind == JsonValueKind.String) s.SelectedSlotStartUtc = ss.GetString();
            if ((b.TryGetProperty("SelectedSlotEndUtc", out var se) || b.TryGetProperty("selectedSlotEndUtc", out se)) && se.ValueKind == JsonValueKind.String) s.SelectedSlotEndUtc = se.GetString();
            if ((b.TryGetProperty("SlotHoldToken", out var sht) || b.TryGetProperty("slotHoldToken", out sht)) && sht.ValueKind == JsonValueKind.String) s.SlotHoldToken = sht.GetString();
            if ((b.TryGetProperty("SlotHoldExpiresAtUtc", out var she) || b.TryGetProperty("slotHoldExpiresAtUtc", out she)) && she.ValueKind == JsonValueKind.String) s.SlotHoldExpiresAtUtc = she.GetString();
            if ((b.TryGetProperty("PrefillInput", out var pi) || b.TryGetProperty("prefillInput", out pi)) && pi.ValueKind == JsonValueKind.String) s.PrefillInput = pi.GetString();
            if ((b.TryGetProperty("TurnCount", out var tc) || b.TryGetProperty("turnCount", out tc)) && tc.ValueKind == JsonValueKind.Number && tc.TryGetInt32(out var turnCount)) s.TurnCount = Math.Max(0, turnCount);
            if ((b.TryGetProperty("AutoConfirmRequested", out var ac) || b.TryGetProperty("autoConfirmRequested", out ac))
                && (ac.ValueKind == JsonValueKind.True || ac.ValueKind == JsonValueKind.False))
            {
                s.AutoConfirmRequested = ac.GetBoolean();
            }

            if ((b.TryGetProperty("ServiceIds", out var arr) || b.TryGetProperty("serviceIds", out arr)) && arr.ValueKind == JsonValueKind.Array)
            {
                s.ServiceIds = arr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt64()).ToList();
            }

            return s;
        }
        catch
        {
            return new ChatbotBookingState();
        }
    }
}

public enum ChatbotBookingStep
{
    IdentifyOrCreateVehicle = 0,
    SelectServices = 1,
    SelectBranch = 2,
    ComputeAvailableSlots = 3,
    FinalizeBooking = 4,
    Completed = 5
}
