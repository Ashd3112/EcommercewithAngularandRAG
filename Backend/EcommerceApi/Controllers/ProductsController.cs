using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApi.Data;
using EcommerceApi.Models;
using EcommerceApi.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly EcommerceDbContext _dbContext;
        private readonly ISemanticSearchService _searchService;

        public ProductsController(EcommerceDbContext dbContext, ISemanticSearchService searchService)
        {
            _dbContext = dbContext;
            _searchService = searchService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] string? category)
        {
            var query = _dbContext.Products.AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            return await query.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _dbContext.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> SearchProducts([FromQuery] string q)
        {
            if (string.IsNullOrEmpty(q))
            {
                return BadRequest("Search query cannot be empty");
            }

            var products = await _dbContext.Products.ToListAsync();
            var searchResults = _searchService.Search(products, q, topK: 6);

            var dtos = searchResults.Select(r => new ProductDto
            {
                Id = r.Product.Id,
                Name = r.Product.Name,
                Category = r.Product.Category,
                Price = r.Product.Price,
                ImageUrl = r.Product.ImageUrl,
                Rating = r.Product.Rating,
                SimilarityScore = Math.Round(r.Score, 4)
            }).ToList();

            return Ok(dtos);
        }

        // Endpoint to manually trigger vector re-calculation if needed
        [HttpPost("reindex")]
        public async Task<IActionResult> ReindexVectors()
        {
            var products = await _dbContext.Products.ToListAsync();
            _searchService.ComputeAndStoreProductVectors(products);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Successfully computed and stored vector embeddings for all products." });
        }
    }
}
