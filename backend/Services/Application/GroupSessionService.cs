using MyGarage.Constants;
using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class GroupSessionService : IGroupSessionService
    {
        private readonly IGarageContext _context;
        private readonly IGroupSessionStatusPolicy _groupSessionStatusPolicy;
        private readonly ISystemClock _clock;

        public GroupSessionService(
            IGarageContext context,
            IGroupSessionStatusPolicy groupSessionStatusPolicy,
            ISystemClock clock)
        {
            _context = context;
            _groupSessionStatusPolicy = groupSessionStatusPolicy;
            _clock = clock;
        }
    }
}

