using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Services
{
    public interface ISmsQueue
    {
        Task QueueMessageAsync(string number, string message, int? consumerId = null);
    }

    public class InMemorySmsQueue : BackgroundService, ISmsQueue
    {
        private readonly IServiceProvider _provider;
        private readonly ConcurrentQueue<(string number, string message, int? consumerId)> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public InMemorySmsQueue(IServiceProvider provider)
        {
            _provider = provider;
        }

        public Task QueueMessageAsync(string number, string message, int? consumerId = null)
        {
            _queue.Enqueue((number, message, consumerId));
            _signal.Release();
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(stoppingToken);

                if (_queue.TryDequeue(out var item))
                {
                    using var scope = _provider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var smsService = scope.ServiceProvider.GetRequiredService<SemaphoreSmsService>();

                    int attempt = 0;
                    bool success = false;
                    string response = "";

                    do
                    {
                        (success, response) = await smsService.SendSmsAsync(item.number, item.message);
                        attempt++;
                        if (!success)
                            await Task.Delay(2000, stoppingToken); // Retry delay
                    } while (!success && attempt < 3);

                    context.SmsLogs.Add(new SmsLog
                    {
                        ConsumerId = item.consumerId,
                        ContactNumber = item.number,
                        Message = item.message,
                        SentAt = DateTime.Now,
                        IsSuccess = success,
                        ResponseMessage = response,
                        RetryCount = attempt
                    });

                    await context.SaveChangesAsync();

                    // Throttle: 1 message per second (adjust if needed)
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}
