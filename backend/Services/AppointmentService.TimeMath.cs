using System;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static int RoundUpToStep(int minutes, int stepMinutes)
        {
            var step = stepMinutes > 0 ? stepMinutes : 15;
            if (minutes <= 0) return step;
            var mod = minutes % step;
            return mod == 0 ? minutes : minutes + (step - mod);
        }

        private static int ResolveSlotStepMinutes(int? slotDurationMinutes)
        {
            var step = slotDurationMinutes.GetValueOrDefault(15);
            return step > 0 ? step : 15;
        }

        private static void ValidateTimeWindow(DateTime startAt, DateTime endAt, TimeOnly openTime, TimeOnly closeTime)
        {
            if (endAt <= startAt) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
            if (startAt.Date != endAt.Date) throw new InvalidOperationException("Lịch hẹn phải nằm trong cùng một ngày.");
            var start = TimeOnly.FromDateTime(startAt);
            var end = TimeOnly.FromDateTime(endAt);
            if (start < openTime || end > closeTime)
                throw new InvalidOperationException($"Lịch hẹn phải trong giờ làm việc {openTime:HH\\:mm}-{closeTime:HH\\:mm}.");
        }
    }
}



