using Microsoft.EntityFrameworkCore;
using MyGarage.Data.Configurations;

namespace MyGarage.Data;

public partial class GarageContext
{
    private static void ConfigureChatbotModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ChatbotSessionConfiguration());
        modelBuilder.ApplyConfiguration(new ChatbotMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ChatbotActionLogConfiguration());
    }
}
