using Hangfire;
using OmniDoc.API;
using OmniDoc.API.Hubs;
using OmniDoc.Application;
using OmniDoc.Infrastructure;
using OmniDoc.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddPresentationServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("OmniDoc API Documentation")
            .WithTheme(ScalarTheme.Mars);
    });

    // The dashboard is unauthenticated, so it stays out of non-development environments
    // until an authorization filter is wired up.
    app.UseHangfireDashboard("/hangfire");
}

app.MapControllers();
app.MapHub<DocumentProgressHub>("/hubs/document-progress");
app.MapHub<NotificationHub>("/hubs/notifications");

var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<OmniDoc.Application.Common.Interfaces.IEmailOutboxDispatcher>(
    "email-outbox-dispatcher",
    dispatcher => dispatcher.DispatchPendingAsync(CancellationToken.None),
    Cron.Minutely,
    new RecurringJobOptions());

// Endpoint kiểm tra nhanh trạng thái server
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "OmniDoc API",
    timestamp = DateTime.UtcNow
}));

app.Run();
