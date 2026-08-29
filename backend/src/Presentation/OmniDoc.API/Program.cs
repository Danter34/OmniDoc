using OmniDoc.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddPersistenceServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
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
}

app.MapControllers();

// Endpoint kiểm tra nhanh trạng thái server
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "OmniDoc API",
    timestamp = DateTime.UtcNow
}));

app.Run();