using MyGarage.Constants;
using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class BookingGroupService : IBookingGroupService
    {
        private readonly IGarageContext _context;
        private readonly IBookingGroupStatusPolicy _bookingGroupStatusPolicy;
        private readonly ISystemClock _clock;

        public BookingGroupService(
            IGarageContext context,
            IBookingGroupStatusPolicy bookingGroupStatusPolicy,
            ISystemClock clock)
        {
            _context = context;
            _bookingGroupStatusPolicy = bookingGroupStatusPolicy;
            _clock = clock;
        }
    }
}

