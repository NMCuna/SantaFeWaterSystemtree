using SantaFeWaterSystem.Services;

public class MockSmsService : ISemaphoreSmsService
{
    private readonly ILogger<MockSmsService> _logger;

    public MockSmsService(ILogger<MockSmsService> logger)
    {
        _logger = logger;
    }

    public Task<(bool success, string response)> SendSmsAsync(string number, string message)
    {
        _logger.LogInformation("=== MOCK SMS ===");
        _logger.LogInformation("Timestamp : {Timestamp}", DateTime.Now);
        _logger.LogInformation("To        : {Number}", number ?? "NULL");
        _logger.LogInformation("Message   : {Message}", message ?? "NO MESSAGE");
        _logger.LogInformation("================");

        // Also output to console
        Console.WriteLine("=== MOCK SMS ===");
        Console.WriteLine($"To      : {number}");
        Console.WriteLine($"Message : {message}");
        Console.WriteLine("================");

        return Task.FromResult((true, "Mock SMS sent successfully"));
    }
}
