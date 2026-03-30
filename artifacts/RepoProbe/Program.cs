using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using MyGarage.Data;
using MyGarage.Repositories;

var connectionString = "Server=tcp:garage-server-2026.database.windows.net,1433;Initial Catalog=MyGarage;Persist Security Info=False;Uid=admingarage;Password=AutoGarage-2026;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

var options = new DbContextOptionsBuilder<GarageContext>()
    .UseSqlServer(connectionString)
    .Options;

var accessor = new HttpContextAccessor();
using var ctx = new GarageContext(options, accessor);
var repo = new AppointmentRepository(ctx);

try
{
    var data = await repo.GetByBranchIdAsync(1, null);
    Console.WriteLine($"OK branch=1 count={data.Count()}");
}
catch (Exception ex)
{
    Console.WriteLine("ERR: " + ex.ToString());
}
