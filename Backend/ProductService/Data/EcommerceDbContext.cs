using Microsoft.EntityFrameworkCore;
using ProductService.Models;
using System;

namespace ProductService.Data
{
    public class EcommerceDbContext : DbContext
    {
        public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Vortex Noise Cancelling Headphones",
                    Description = "Premium over-ear wireless headphones with active noise cancellation, 40-hour battery life, spatial audio, and memory foam earcups. Perfect for study, travel, or work.",
                    Category = "Electronics",
                    Price = 199.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.8,
                    Stock = 25,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 2,
                    Name = "Aura Smart Ambient Light",
                    Description = "RGB LED smart lamp syncing with music and screen. Features 16 million colors, voice control compatibility, and a sleek minimalist design for modern gaming setup or bedroom.",
                    Category = "Smart Home",
                    Price = 49.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1507646227500-4d389b0012be?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.5,
                    Stock = 50,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 3,
                    Name = "Nebula Mechanical Keyboard",
                    Description = "Hot-swappable tenkeyless mechanical keyboard with linear yellow switches, double-shot PBT keycaps, per-key RGB backlighting, and a solid aluminum top case. Designed for developers and gamers.",
                    Category = "Electronics",
                    Price = 129.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.7,
                    Stock = 15,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 4,
                    Name = "ThermaSteel Travel Mug",
                    Description = "Double-walled vacuum insulated stainless steel flask. Keeps beverages hot for 12 hours or cold for 24. Spill-proof locking lid with a modern textured matte finish.",
                    Category = "Lifestyle",
                    Price = 29.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1514432324607-a09d9b4aefdd?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.6,
                    Stock = 120,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 5,
                    Name = "Solitude Ergonomic Desk Chair",
                    Description = "Fully adjustable office chair featuring adaptive lumbar support, 3D armrests, breathable mesh backrest, and smooth-glide caster wheels. Ergonomic seating for all-day comfort.",
                    Category = "Furniture",
                    Price = 249.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1505797149-43b0069ec26b?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.9,
                    Stock = 8,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 6,
                    Name = "Zen Bamboo Essential Oil Diffuser",
                    Description = "Ultrasonic aromatherapy diffuser crafted from natural sustainable bamboo. Runs silently up to 10 hours, featuring a soft warm light and auto shut-off function.",
                    Category = "Smart Home",
                    Price = 39.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1608571423902-eed4a5ad8108?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.4,
                    Stock = 45,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 7,
                    Name = "Nomad Leather Minimalist Wallet",
                    Description = "Ultra-slim front pocket cardholder built from premium full-grain vegetable-tanned leather. RFID blocking technology, holds up to 8 cards and cash notes.",
                    Category = "Lifestyle",
                    Price = 34.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1627123424574-724758594e93?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.6,
                    Stock = 60,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 8,
                    Name = "Apex Ultra Fitness Smartwatch",
                    Description = "Rugged outdoor smartwatch with built-in GPS, continuous heart rate sensor, blood oxygen tracker, and 20+ sports modes. 14-day battery life, 5ATM water resistance.",
                    Category = "Electronics",
                    Price = 179.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.3,
                    Stock = 30,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 9,
                    Name = "Pinnacle Drip Coffee Maker",
                    Description = "Precision barista coffee brewer featuring temperature control, gold-tone filter mesh, programmable timer, and a 12-cup glass carafe. Excellent drip extraction.",
                    Category = "Kitchen",
                    Price = 89.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1520970014086-2208d157c9e2?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.7,
                    Stock = 20,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 10,
                    Name = "Lunar Wireless Fast Charger Pad",
                    Description = "Sleek 15W Qi-certified wireless charging pad with a non-slip fabric surface and USB-C input. Charges iPhones, Samsung Galaxy phones, and AirPods fast and safely.",
                    Category = "Electronics",
                    Price = 24.99m,
                    ImageUrl = "https://images.unsplash.com/photo-1629367494173-c78a56567877?w=500&auto=format&fit=crop&q=60",
                    Rating = 4.5,
                    Stock = 80,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }
    }
}
