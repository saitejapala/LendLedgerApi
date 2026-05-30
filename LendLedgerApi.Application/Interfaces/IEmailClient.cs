using System.Threading.Tasks;

namespace LendLedgerApi.Application.Interfaces
{
    public interface IEmailClient
    {
        Task<bool> SendEmail(string toEmail, string emailSubject, string htmlEmailBody, string fallbackEmailBody, bool trackOpens = false, string emailCategory = "");
    }
}
