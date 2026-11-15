using Microsoft.Extensions.Configuration;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed partial class EfAppointmentStore : IAppointmentStore
{
    private readonly IGarageContext _context;
    private readonly IConfiguration _configuration;

    public EfAppointmentStore(IGarageContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
}
