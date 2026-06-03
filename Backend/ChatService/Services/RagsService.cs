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
using ChatService.Models;

namespace ChatService.Services
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

    public class RagsService : IRagsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public RagsService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<ChatResponse> ProcessChatQueryAsync(string userQuery)
        {
            var productServiceUrl = _configuration["Services:ProductServiceUrl"] ?? "http://localhost:5001";
            var client = _httpClientFactory.CreateClient();
            
            List<ProductDto> retrieved = new();
            List<ProductDto> filteredResults = new();

            try
            {
                // Query ProductService for semantic search results with topK=3
                var response = await client.GetAsync($"{productServiceUrl}/api/products/search?q={Uri.EscapeDataString(userQuery)}&topK=3");
                if (response.IsSuccessStatusCode)
                {
                    retrieved = await response.Content.ReadFromJsonAsync<List<ProductDto>>() ?? new();
                    
                    // Filter out low-relevance matches
                    var bestScore = retrieved.Any() ? retrieved.Max(r => r.SimilarityScore) : 0.0;
                    filteredResults = retrieved
                        .Where(r => r.SimilarityScore > 0.05 && (bestScore < 0.5 || r.SimilarityScore >= 0.3))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductService Error] Could not fetch products: {ex.Message}");
            }

            // Generate the RAG Response
            string systemAnswer = await GenerateRagResponseAsync(userQuery, filteredResults);

            return new ChatResponse
            {
                Response = systemAnswer,
                RetrievedProducts = filteredResults
            };
        }

        private async Task<string> GenerateRagResponseAsync(string query, List<ProductDto> matches)
        {
            var ollamaBaseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var ollamaModel = _configuration["Ollama:Model"] ?? "llama3";

            if (!matches.Any())
            {
                return await CallOllamaAsync(ollamaBaseUrl, ollamaModel, 
                    "You are a helpful and friendly shopping assistant for our e-commerce store. A user asked: \"" + query + 
                    "\". We do not have matching products in stock right now. Respond politely and offer assistance.",
                    () => "I couldn't find any products in our store that match your query. Could you try asking for something else, like headphones, lighting, or desk chairs?");
            }

            // Create context
            var contextString = string.Join("\n", matches.Select(m => 
                $"- Name: {m.Name}, Category: {m.Category}, Price: ${m.Price}, Rating: {m.Rating}⭐, Stock: {m.Stock}. Description: {m.Description}"));

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

        private string GenerateFallbackResponse(string query, List<ProductDto> matches)
        {
            query = query.ToLowerInvariant();

            if (query.Contains("lowest") || query.Contains("cheapest") || query.Contains("least expensive"))
            {
                var cheapestProduct = matches.OrderBy(p => p.Price).First();
                return $"The lowest price product in our store is the **{cheapestProduct.Name}** in the *{cheapestProduct.Category}* category, priced at only **${cheapestProduct.Price}** (Customer Rating: {cheapestProduct.Rating}⭐).\n\n*Details:* {cheapestProduct.Description}\n\nWould you like me to add this budget option to your cart?";
            }

            if (query.Contains("highest") || query.Contains("most expensive") || query.Contains("premium") || query.Contains("costliest"))
            {
                var expensiveProduct = matches.OrderByDescending(p => p.Price).First();
                return $"The most premium option in our store is the **{expensiveProduct.Name}** in the *{expensiveProduct.Category}* category, priced at **${expensiveProduct.Price}** (Customer Rating: {expensiveProduct.Rating}⭐).\n\n*Details:* {expensiveProduct.Description}\n\nWould you like me to add this premium option to your cart?";
            }

            if (query.Contains("selling") || query.Contains("popular") || query.Contains("bestseller") || query.Contains("best seller") || query.Contains("demand"))
            {
                var bestSeller = matches.OrderByDescending(p => p.Rating).First();
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

                var budgetMatches = matches.Where(p => p.Price <= budget).ToList();
                if (budgetMatches.Any())
                {
                    var productList = string.Join("\n", budgetMatches.Take(3).Select(p => $"- **{p.Name}** ({p.Category}) - **${p.Price}** with a rating of {p.Rating}⭐"));
                    return $"Here are some great products within your budget:\n\n{productList}\n\nWould you like me to add any of these to your shopping cart?";
                }
            }

            if (query.Contains("recommend") || query.Contains("best") || query.Contains("top") || query.Contains("suggest"))
            {
                var bestRated = matches.OrderByDescending(p => p.Rating).First();
                return $"Based on customer reviews, I highly recommend the **{bestRated.Name}** in the *{bestRated.Category}* category. It has a stellar rating of **{bestRated.Rating}⭐** and costs **${bestRated.Price}**. \n\n*Description:* {bestRated.Description}\n\nWould you like more details or to add this product to your cart?";
            }

            var intro = "Based on our product catalog, here are the best matches for your search:\n\n";
            var body = string.Join("\n\n", matches.Select(m => 
                $"### **{m.Name}** (${m.Price})\n" +
                $"* **Category:** {m.Category}\n" +
                $"* **Rating:** {m.Rating}⭐\n" +
                $"* **Details:** {m.Description}"));

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
