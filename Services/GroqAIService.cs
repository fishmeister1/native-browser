using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NativeBrowser.Services
{
    public class GroqAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string API_BASE_URL = "https://api.groq.com/openai/v1/chat/completions";
        private const string MODEL = "openai/gpt-oss-120b";

        public GroqAIService()
        {
            _httpClient = new HttpClient();
            _apiKey = LoadApiKey();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        private string LoadApiKey()
        {
            try
            {
                var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                if (File.Exists(envPath))
                {
                    var lines = File.ReadAllLines(envPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("groq_api_key="))
                        {
                            return line.Split('=')[1].Trim('"');
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load API key: {ex.Message}");
            }
            throw new Exception("API key not found in .env file");
        }

        public async Task<string> GetBrowserActionResponse(string userMessage)
        {
            var systemPrompt = "RESPOND ONLY WITH JSON. NO OTHER TEXT.\n\n" +
                "{\n" +
                "  \"thinking\": \"describe what user wants\",\n" +
                "  \"action\": \"what you will do\",\n" +
                "  \"url\": \"target URL\",\n" +
                "  \"javascript\": \"\",\n" +
                "  \"explanation\": \"why this URL\"\n" +
                "}\n\n" +
                "URL patterns:\n" +
                "Videos: https://www.youtube.com/results?search_query=TERM\n" +
                "Research: https://en.wikipedia.org/wiki/TERM\n" +
                "Search: https://www.google.com/search?q=TERM";

            var request = new
            {
                model = MODEL,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.7,
                max_tokens = 1000
            };

            string responseContent = "";
            try
            {
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(API_BASE_URL, content);
                responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    return $"API Error ({response.StatusCode}): {responseContent}";
                }
                
                // Debug: Log the full API response
                System.IO.File.WriteAllText("api_response.json", responseContent);
                
                var result = JsonConvert.DeserializeObject<GroqResponse>(responseContent);
                var aiContent = result?.Choices?[0]?.Message?.Content;
                
                if (string.IsNullOrEmpty(aiContent))
                {
                    return $"No content received from AI. Full response: {responseContent}";
                }
                
                return aiContent;
            }
            catch (HttpRequestException ex)
            {
                return $"Network Error: {ex.Message}";
            }
            catch (JsonException ex)
            {
                return $"API Response Parse Error: {ex.Message}. Response was: {responseContent ?? "null"}";
            }
            catch (Exception ex)
            {
                return $"Unexpected Error: {ex.Message}. Response was: {responseContent ?? "null"}";
            }
        }

        public async Task<string> GetQAResponse(string userMessage)
        {
            var systemPrompt = @"You are a helpful AI assistant. The user is asking you a general question that doesn't require browser control. Provide a helpful, informative response.";

            var request = new
            {
                model = MODEL,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.7,
                max_tokens = 1000
            };

            try
            {
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(API_BASE_URL, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<GroqResponse>(responseContent);
                
                return result?.Choices?[0]?.Message?.Content ?? "No response received";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class GroqResponse
    {
        public GroqChoice[] Choices { get; set; }
    }

    public class GroqChoice
    {
        public GroqMessage Message { get; set; }
    }

    public class GroqMessage
    {
        public string Content { get; set; }
    }

    public class BrowserActionResponse
    {
        public string Thinking { get; set; }
        public string Action { get; set; }
        public string Url { get; set; }
        public string Javascript { get; set; }
        public string Explanation { get; set; }
    }
}