using Microsoft.EntityFrameworkCore;
using MyGarage.Data.Configurations;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureAuthModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());
    }
}
