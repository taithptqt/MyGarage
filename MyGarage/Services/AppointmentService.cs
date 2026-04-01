using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Constants;

namespace MyGarage.Services
{
    public partial class AppointmentService : GenericService<Appointment>, IAppointmentService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IBookingGroupService _bookingGroupService;
        private readonly IAppointmentStatusPolicy _appointmentStatusPolicy;
        private readonly IAppointmentDetailAssembler _appointmentDetailAssembler;
        private readonly ISystemClock _clock;
        private readonly GarageContext _context;
        private const int GraceArrivalMinutes = 15;
        private const int NoShowMinutes = 30;
        private const int BookingLockHours = 4;
        private const int QuickOnlineCapDefault = 7;
        private const int RepairOnlineCapDefault = 10;
        private const int ReservedRepairForQuickOverflow = 3;
        private const string DefaultDiagnosticServiceCode = "CDTC-001";

        public AppointmentService(
            IAppointmentRepository repository,
            GarageContext context,
            IBookingGroupService bookingGroupService,
            IAppointmentStatusPolicy appointmentStatusPolicy,
            IAppointmentDetailAssembler appointmentDetailAssembler,
            ISystemClock clock)
            : base(repository)
        {
            _appointmentRepository = repository;
            _context = context;
            _bookingGroupService = bookingGroupService;
            _appointmentStatusPolicy = appointmentStatusPolicy;
            _appointmentDetailAssembler = appointmentDetailAssembler;
            _clock = clock;
        }

    }
}

