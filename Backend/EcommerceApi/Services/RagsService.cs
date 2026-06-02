using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using EcommerceApi.Data;
using EcommerceApi.Models;

namespace EcommerceApi.Services
{
    public interface IRagsService
    {
        Task<ChatResponse> ProcessChatQueryAsync(string userQuery);
    }

    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
        public List<ProductDto> RetrievedProducts { get; set; } = new();
    }

    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public double Rating { get; set; }
        public double SimilarityScore { get; set; }
    }

    public class RagsService : IRagsService
    {
        private readonly EcommerceDbContext _dbContext;
        private readonly ISemanticSearchService _searchService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public RagsService(
            EcommerceDbContext dbContext, 
            ISemanticSearchService searchService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _searchService = searchService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<ChatResponse> ProcessChatQueryAsync(string userQuery)
        {
            // 1. Get all products from database
            var products = _dbContext.Products.ToList();

            // 2. Perform semantic search using our local Vector engine
            var searchResults = _searchService.Search(products, userQuery, topK: 3);

            // Filter out low-relevance description matches if we have highly relevant name/category matches
            var bestScore = searchResults.Any() ? searchResults.Max(r => r.Score) : 0.0;
            var filteredResults = searchResults
                .Where(r => r.Score > 0.05 && (bestScore < 0.5 || r.Score >= 0.3))
                .ToList();

            // 3. Prepare retrieved products DTOs
            var retrieved = filteredResults
                .Select(r => new ProductDto
                {
                    Id = r.Product.Id,
                    Name = r.Product.Name,
                    Category = r.Product.Category,
                    Price = r.Product.Price,
                    ImageUrl = r.Product.ImageUrl,
                    Rating = r.Product.Rating,
                    SimilarityScore = Math.Round(r.Score, 4)
                }).ToList();

            // 4. Generate the RAG Response
            string systemAnswer = await GenerateRagResponseAsync(userQuery, filteredResults);

            return new ChatResponse
            {
                Response = systemAnswer,
                RetrievedProducts = retrieved
            };
        }

        private async Task<string> GenerateRagResponseAsync(string query, List<(Product Product, double Score)> matches)
        {
            var ollamaBaseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var ollamaModel = _configuration["Ollama:Model"] ?? "llama3";

            // If we have no matches, we can still ask Ollama directly as a general assistant or use rule-based fallback
            if (!matches.Any())
            {
                return await CallOllamaAsync(ollamaBaseUrl, ollamaModel, 
                    "You are a helpful and friendly shopping assistant for our e-commerce store. A user asked: \"" + query + 
                    "\". We do not have matching products in stock right now. Respond politely and offer assistance.",
                    () => "I couldn't find any products in our store that match your query. Could you try asking for something else, like headphones, lighting, or desk chairs?");
            }

            // Create context
            var contextString = string.Join("\n", matches.Select(m => 
                $"- Name: {m.Product.Name}, Category: {m.Product.Category}, Price: ${m.Product.Price}, Rating: {m.Product.Rating}⭐, Stock: {m.Product.Stock}. Description: {m.Product.Description}"));

            var prompt = $"You are a helpful e-commerce shopping assistant. Below are products matching the user's search query:\n\n" +
                         $"{contextString}\n\n" +
                         $"User query: \"{query}\"\n\n" +
                         $"Answer the user's query naturally, informatively, and concisely based *only* on the products listed above. If they ask about a specific product, highlight its features, price, and rating. Keep your answer engaging and brief (max 3-4 sentences). Do not mention that you got this data from a list or SQL database; answer as if you are the store assistant.";

            return await CallOllamaAsync(ollamaBaseUrl, ollamaModel, prompt, () => GenerateFallbackResponse(query, matches));
        }

        private async Task<string> CallOllamaAsync(string baseUrl, string model, string prompt, Func<string> fallbackGenerator)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var requestPayload = new OllamaGenerateRequest
                {
                    Model = model,
                    Prompt = prompt,
                    Stream = false
                };

                var response = await client.PostAsJsonAsync($"{baseUrl}/api/generate", requestPayload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
                    if (result != null && !string.IsNullOrWhiteSpace(result.Response))
                    {
                        return result.Response.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ollama Error] Could not generate response via Ollama: {ex.Message}. Falling back to rule-based generation.");
            }

            return fallbackGenerator();
        }

        private string GenerateFallbackResponse(string query, List<(Product Product, double Score)> matches)
        {
            query = query.ToLowerInvariant();
            var allProducts = matches.Select(m => m.Product).ToList();

            // 1. Check for specific global database query intents first
            if (query.Contains("lowest") || query.Contains("cheapest") || query.Contains("least expensive"))
            {
                var cheapestProduct = allProducts.OrderBy(p => p.Price).First();
                return $"The lowest price product in our store is the **{cheapestProduct.Name}** in the *{cheapestProduct.Category}* category, priced at only **${cheapestProduct.Price}** (Customer Rating: {cheapestProduct.Rating}⭐).\n\n*Details:* {cheapestProduct.Description}\n\nWould you like me to add this budget option to your cart?";
            }

            if (query.Contains("highest") || query.Contains("most expensive") || query.Contains("premium") || query.Contains("costliest"))
            {
                var expensiveProduct = allProducts.OrderByDescending(p => p.Price).First();
                return $"The most premium option in our store is the **{expensiveProduct.Name}** in the *{expensiveProduct.Category}* category, priced at **${expensiveProduct.Price}** (Customer Rating: {expensiveProduct.Rating}⭐).\n\n*Details:* {expensiveProduct.Description}\n\nWould you like me to add this premium option to your cart?";
            }

            if (query.Contains("selling") || query.Contains("popular") || query.Contains("bestseller") || query.Contains("best seller") || query.Contains("demand"))
            {
                var bestSeller = allProducts.OrderByDescending(p => p.Rating).First();
                return $"Our top selling and most popular item is the **{bestSeller.Name}** (${bestSeller.Price}). Customers have rated it highly at **{bestSeller.Rating}⭐**!\n\nWould you like to add it to your shopping cart?";
            }

            if (query.Contains("cheap") || query.Contains("under") || query.Contains("budget") || query.Contains("price"))
            {
                var numberMatch = Regex.Match(query, @"\d+");
                decimal budget = 1000m;
                if (numberMatch.Success && decimal.TryParse(numberMatch.Value, out decimal parsedBudget))
                {
                    budget = parsedBudget;
                }

                var budgetMatches = allProducts.Where(p => p.Price <= budget).ToList();
                if (budgetMatches.Any())
                {
                    var productList = string.Join("\n", budgetMatches.Take(3).Select(p => $"- **{p.Name}** ({p.Category}) - **${p.Price}** with a rating of {p.Rating}⭐"));
                    return $"Here are some great products within your budget:\n\n{productList}\n\nWould you like me to add any of these to your shopping cart?";
                }
            }

            if (query.Contains("recommend") || query.Contains("best") || query.Contains("top") || query.Contains("suggest"))
            {
                var bestRated = allProducts.OrderByDescending(p => p.Rating).First();
                return $"Based on customer reviews, I highly recommend the **{bestRated.Name}** in the *{bestRated.Category}* category. It has a stellar rating of **{bestRated.Rating}⭐** and costs **${bestRated.Price}**. \n\n*Description:* {bestRated.Description}\n\nWould you like more details or to add this product to your cart?";
            }

            var primaryMatch = matches.First().Product;
            var intro = "Based on our product catalog, here are the best matches for your search:\n\n";
            var body = string.Join("\n\n", matches.Select(m => 
                $"### **{m.Product.Name}** (${m.Product.Price})\n" +
                $"* **Category:** {m.Product.Category}\n" +
                $"* **Rating:** {m.Product.Rating}⭐\n" +
                $"* **Details:** {m.Product.Description}"));

            var conclusion = "\n\nFeel free to ask for specific comparisons, budget-friendly items, or click directly on the product cards to view them!";

            return intro + body + conclusion;
        }

        private class OllamaGenerateRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [JsonPropertyName("stream")]
            public bool Stream { get; set; } = false;
        }

        private class OllamaGenerateResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;
        }
    }
}
