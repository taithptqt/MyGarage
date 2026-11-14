using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    public virtual DbSet<SalaryCoefficient> SalaryCoefficients { get; set; }
}
