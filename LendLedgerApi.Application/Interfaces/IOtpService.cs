using System.Threading.Tasks;

namespace LendLedgerApi.Application.Interfaces
{
    public interface IOtpService
    {
        Task GenerateAndSendOtpAsync(string email);
        Task<bool> VerifyOtpAsync(string email, string code);
    }
}
