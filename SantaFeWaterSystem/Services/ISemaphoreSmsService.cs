using System.Threading.Tasks;

namespace SantaFeWaterSystem.Services
{
    public interface ISemaphoreSmsService
    {
        Task<(bool success, string response)> SendSmsAsync(string number, string message);
    }
}
