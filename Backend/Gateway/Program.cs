var builder = WebApplication.CreateBuilder(args);

// Configure port to match the original API port
builder.WebHost.UseUrls("http://localhost:5194");

// Add Yarp Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configure CORS to match the original Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAngular");

app.MapReverseProxy();

app.Run();
