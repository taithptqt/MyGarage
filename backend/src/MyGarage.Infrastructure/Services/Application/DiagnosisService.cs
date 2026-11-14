using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class DiagnosisService : GenericService<Diagnosis>, IDiagnosisService
    {
        private readonly IGarageContext _context;
        private readonly IConfiguration _configuration;

        public DiagnosisService(IDiagnosisRepository repository, IGarageContext context, IConfiguration configuration) : base(repository)
        {
            _context = context;
            _configuration = configuration;
        }

    }
}


