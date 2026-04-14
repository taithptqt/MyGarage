using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VehicleConditionLogService : GenericService<VehicleConditionLog>, IVehicleConditionLogService
    {
        private readonly IVehicleConditionLogRepository _vehicleConditionLogRepository;
        private readonly IGarageContext _context;

        public VehicleConditionLogService(IVehicleConditionLogRepository repository, IGarageContext context)
            : base(repository)
        {
            _vehicleConditionLogRepository = repository;
            _context = context;
        }

        public async Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId)
        {
            return await _vehicleConditionLogRepository.GetByServiceOrderIdAsync(serviceOrderId);
        }

        public async Task<bool> ServiceOrderExistsAsync(long serviceOrderId)
        {
            return await _context.ServiceOrders.AnyAsync(x => x.Id == serviceOrderId);
        }

        public async Task<bool> TechnicianExistsAsync(long technicianId)
        {
            return await _context.Staffs.AnyAsync(x => x.Id == technicianId);
        }
    }
}
