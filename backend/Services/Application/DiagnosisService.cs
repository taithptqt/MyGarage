using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class DiagnosisService : GenericService<Diagnosis>, IDiagnosisService
    {
        private readonly IGarageContext _context;

        public DiagnosisService(IDiagnosisRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

    }
}


