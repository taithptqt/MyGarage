using MyGarage.Application;
using MyGarage.Application.Bays;
using MyGarage.Application.Interfaces.Common;
using MyGarage.Application.Interfaces.Notifications;
using MyGarage.Application.Interfaces.Stores.Chat;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Extensions;
using MyGarage.Infrastructure;
using MyGarage.Middleware;
using MyGarage.Security;
using MyGarage.Services.Realtime;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // Keep local development stable on the requested ports.
    builder.WebHost.UseUrls("https://localhost:7009", "http://localhost:5062");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContextAccessor, HttpAuditContextAccessor>();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApplication(
    builder.Configuration["AutoMapper:LicenseKey"] ?? builder.Configuration["AUTOMAPPER_LICENSE_KEY"]);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddApiCors(builder.Configuration, builder.Environment)
    .AddApiForwardedHeaders(builder.Configuration)
    .AddApiAuthentication(builder.Configuration)
    .AddApiAuthorization()
    .AddApiRateLimiting()
    .AddApiControllersWithOData()
    .AddSignalR()
    .Services
    .AddApiSwagger();

builder.Services.AddScoped<IBayRealtimeNotifier, SignalRBayRealtimeNotifier>();
builder.Services.AddScoped<IChatRealtimeNotifier, SignalRChatRealtimeNotifier>();
builder.Services.AddScoped<IQuotationRealtimeNotifier, SignalRQuotationRealtimeNotifier>();
builder.Services.AddScoped<IDiagnosisRealtimeNotifier, SignalRDiagnosisRealtimeNotifier>();

var app = builder.Build();
app.UseApiPipeline(builder.Configuration);
app.Run();

public partial class Program;
