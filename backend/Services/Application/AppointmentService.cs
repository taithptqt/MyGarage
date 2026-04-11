using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Constants;

namespace MyGarage.Services
{
    public partial class AppointmentService : GenericService<Appointment>, IAppointmentService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IGroupSessionService _groupSessionService;
        private readonly IAppointmentStatusPolicy _appointmentStatusPolicy;
        private readonly IAppointmentDetailAssembler _appointmentDetailAssembler;
        private readonly ISlotHoldService _slotHoldService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly ISystemClock _clock;
        private readonly IConfiguration _configuration;
        private readonly IGarageContext _context;
        private const int GraceArrivalMinutes = 15;
        private const int NoShowMinutes = 30;
        private const int StandardCheckInSetupMinutes = 15;
        private const int ExecutionBufferMinutes = 15;
        private const int FlexibleReceptionistCount = 2;
        private const int FlexibleMaxBayMoves = 1;
        private const int BookingLockHours = 4;
        private const string DefaultDiagnosticServiceCode = "CDTC-001";
        private const string FlexibleCheckInSlotMarker = WorkSlotNoteMarkers.FlexCheckIn;
        private const string FlexiblePreallocatedRepairSlotMarker = WorkSlotNoteMarkers.FlexPreallocatedRepair;
        private const string FlexiblePendingBayAssignmentMarker = WorkSlotNoteMarkers.FlexPendingBayAssignment;
        private const string FlexiblePendingDispatchAlertMarker = WorkSlotNoteMarkers.DispatchAlertManualAssignmentRequired;

        public AppointmentService(
            IAppointmentRepository repository,
            IGarageContext context,
            IGroupSessionService groupSessionService,
            IAppointmentStatusPolicy appointmentStatusPolicy,
            IAppointmentDetailAssembler appointmentDetailAssembler,
            ISlotHoldService slotHoldService,
            IServiceOrderService serviceOrderService,
            ISystemClock clock,
            IConfiguration configuration)
            : base(repository)
        {
            _appointmentRepository = repository;
            _context = context;
            _groupSessionService = groupSessionService;
            _appointmentStatusPolicy = appointmentStatusPolicy;
            _appointmentDetailAssembler = appointmentDetailAssembler;
            _slotHoldService = slotHoldService;
            _serviceOrderService = serviceOrderService;
            _clock = clock;
            _configuration = configuration;
        }

    }
}
