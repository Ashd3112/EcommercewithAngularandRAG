using ChatService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure port
builder.WebHost.UseUrls("http://localhost:5002");

// Add Services
builder.Services.AddScoped<IRagsService, RagsService>();
builder.Services.AddHttpClient();

builder.Services.AddControllers();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.UseAuthorization();
app.MapControllers();

app.Run();
