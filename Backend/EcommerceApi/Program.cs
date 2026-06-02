using Microsoft.EntityFrameworkCore;
using EcommerceApi.Data;
using EcommerceApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EcommerceDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add Custom Services for Semantic Search and RAG
builder.Services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IRagsService, RagsService>();
builder.Services.AddHttpClient();

builder.Services.AddControllers();

// Configure CORS
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

// Enable CORS
app.UseCors("AllowAngular");

// Automatically apply migrations and build vectors on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<EcommerceDbContext>();
        
        // Ensure Database is Created
        context.Database.EnsureCreated();

        // Fix broken Unsplash images in the database if they exist
        var nomadWallet = await context.Products.FirstOrDefaultAsync(p => p.Id == 7);
        if (nomadWallet != null && nomadWallet.ImageUrl.Contains("photo-1627124718185-60f1b4daebd6"))
        {
            nomadWallet.ImageUrl = "https://images.unsplash.com/photo-1627123424574-724758594e93?w=500&auto=format&fit=crop&q=60";
        }

        var coffeeMaker = await context.Products.FirstOrDefaultAsync(p => p.Id == 9);
        if (coffeeMaker != null && coffeeMaker.ImageUrl.Contains("photo-1517256064527-09c53b2d0bc6"))
        {
            coffeeMaker.ImageUrl = "https://images.unsplash.com/photo-1520970014086-2208d157c9e2?w=500&auto=format&fit=crop&q=60";
        }

        var wirelessCharger = await context.Products.FirstOrDefaultAsync(p => p.Id == 10);
        if (wirelessCharger != null && wirelessCharger.ImageUrl.Contains("photo-1622445262465-2481c4574875"))
        {
            wirelessCharger.ImageUrl = "https://images.unsplash.com/photo-1629367494173-c78a56567877?w=500&auto=format&fit=crop&q=60";
        }
        await context.SaveChangesAsync();

        // Seed calculations for semantic search vectors
        var searchService = services.GetRequiredService<ISemanticSearchService>();
        var products = await context.Products.ToListAsync();
        if (products.Any())
        {
            searchService.ComputeAndStoreProductVectors(products);
            await context.SaveChangesAsync();
        }
        
        Console.WriteLine("Database initialized and vectors computed successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding or initializing the database.");
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
