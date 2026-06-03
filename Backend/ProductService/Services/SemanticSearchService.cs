using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProductService.Models;

namespace ProductService.Services
{
    public interface ISemanticSearchService
    {
        void ComputeAndStoreProductVectors(List<Product> products);
        List<(Product Product, double Score)> Search(List<Product> products, string query, int topK = 4);
    }

    public class SemanticSearchService : ISemanticSearchService
    {
        private List<string> _vocabulary = new();
        private Dictionary<string, double> _idf = new();
        private bool _isTrained = false;

        public void ComputeAndStoreProductVectors(List<Product> products)
        {
            if (products == null || !products.Any()) return;

            var docs = products.Select(p => Tokenize($"{p.Name} {p.Category} {p.Description}")).ToList();
            
            var allWords = docs.SelectMany(d => d).Distinct().ToList();
            _vocabulary = allWords;

            int N = docs.Count;
            _idf = new Dictionary<string, double>();
            foreach (var word in _vocabulary)
            {
                int docCount = docs.Count(d => d.Contains(word));
                _idf[word] = Math.Log((double)N / (docCount + 1)) + 1.0;
            }

            _isTrained = true;

            foreach (var product in products)
            {
                var docTokens = Tokenize($"{product.Name} {product.Category} {product.Description}");
                var vector = ComputeVector(docTokens);
                product.VectorJson = JsonSerializer.Serialize(vector);
            }
        }

        public List<(Product Product, double Score)> Search(List<Product> products, string query, int topK = 4)
        {
            if (string.IsNullOrWhiteSpace(query) || !products.Any())
            {
                return products.Select(p => (p, 0.0)).Take(topK).ToList();
            }

            if (!_isTrained)
            {
                ComputeAndStoreProductVectors(products);
            }

            var queryTokens = Tokenize(query);
            var queryVector = ComputeVector(queryTokens);

            var results = new List<(Product Product, double Score)>();

            foreach (var product in products)
            {
                double[]? productVector = null;
                if (!string.IsNullOrEmpty(product.VectorJson))
                {
                    try
                    {
                        productVector = JsonSerializer.Deserialize<double[]>(product.VectorJson);
                    }
                    catch
                    {
                        // Ignore and recompute
                    }
                }

                if (productVector == null)
                {
                    var docTokens = Tokenize($"{product.Name} {product.Category} {product.Description}");
                    productVector = ComputeVector(docTokens);
                }

                double similarity = CosineSimilarity(queryVector, productVector);

                double boost = 0.0;
                var queryLower = query.ToLowerInvariant().Trim();
                var nameLower = product.Name.ToLowerInvariant();
                var categoryLower = product.Category.ToLowerInvariant();

                if (nameLower.Contains(queryLower) || categoryLower.Contains(queryLower))
                {
                    boost += 0.5;
                }
                else
                {
                    var queryWords = queryLower.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var nameWords = nameLower.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var categoryWords = categoryLower.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var targetWords = nameWords.Concat(categoryWords).ToList();

                    foreach (var qw in queryWords)
                    {
                        if (qw.Length <= 2) continue;

                        if (targetWords.Any(w => w.StartsWith(qw) || qw.StartsWith(w)))
                        {
                            boost += 0.4;
                        }
                        else if (targetWords.Any(w => w.Contains(qw) || qw.Contains(w)))
                        {
                            boost += 0.2;
                        }
                    }
                }

                double finalScore = Math.Min(1.0, similarity + boost);
                results.Add((product, finalScore));
            }

            return results.OrderByDescending(r => r.Score).Take(topK).ToList();
        }

        private List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            var cleanText = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s]", "");
            return cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 2)
                            .ToList();
        }

        private double[] ComputeVector(List<string> tokens)
        {
            var vector = new double[_vocabulary.Count];
            if (tokens.Count == 0 || _vocabulary.Count == 0) return vector;

            var tf = new Dictionary<string, double>();
            foreach (var token in tokens)
            {
                if (tf.ContainsKey(token))
                    tf[token]++;
                else
                    tf[token] = 1;
            }

            for (int i = 0; i < _vocabulary.Count; i++)
            {
                var word = _vocabulary[i];
                if (tf.TryGetValue(word, out double count))
                {
                    vector[i] = (count / tokens.Count) * _idf[word];
                }
                else
                {
                    vector[i] = 0.0;
                }
            }

            return vector;
        }

        private double CosineSimilarity(double[] vecA, double[] vecB)
        {
            if (vecA.Length != vecB.Length || vecA.Length == 0) return 0.0;

            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }

            if (normA == 0.0 || normB == 0.0) return 0.0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
