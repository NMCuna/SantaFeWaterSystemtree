using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SantaFeWaterSystem.Settings;

namespace SantaFeWaterSystem.Services
{
    public class SemaphoreSmsService : ISemaphoreSmsService
    {
        private readonly SemaphoreSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<SemaphoreSmsService> _logger;

        public SemaphoreSmsService(
            IOptions<SemaphoreSettings> options,
            HttpClient httpClient,
            ILogger<SemaphoreSmsService> logger)
        {
            _settings = options.Value;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<(bool success, string response)> SendSmsAsync(string number, string message)
        {
            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(message))
            {
                return (false, "Recipient number and message content are required.");
            }

            var data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("apikey", _settings.ApiKey),
                new KeyValuePair<string, string>("number", number),
                new KeyValuePair<string, string>("message", message),
                new KeyValuePair<string, string>("sendername", _settings.SenderName ?? "SEMAPHORE")
            });

            try
            {
                var response = await _httpClient.PostAsync("https://api.semaphore.co/api/v4/messages", data);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to send SMS to {Number}. Response: {Response}", number, responseBody);
                }

                return (response.IsSuccessStatusCode, responseBody);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error while sending SMS to {Number}", number);
                return (false, $"HTTP Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending SMS to {Number}", number);
                return (false, $"Unexpected Error: {ex.Message}");
            }
        }
    }
}
