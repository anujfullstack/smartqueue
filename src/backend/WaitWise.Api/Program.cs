using Microsoft.EntityFrameworkCore;
using WaitWise.Agent;
using WaitWise.Api.Endpoints;
using WaitWise.Api.Seed;
using WaitWise.Dal;
using WaitWise.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddDomainServices();
builder.Services.AddAgents();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Auto-migrate and seed on startup (dev/demo only — remove for production)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WaitWiseDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db);
}

app.UseCors();
app.MapPublicEndpoints();
app.MapStaffEndpoints();

app.Run();
