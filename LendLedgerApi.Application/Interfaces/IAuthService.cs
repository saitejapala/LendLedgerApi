using System.Threading.Tasks;
using LendLedgerApi.Application.Dtos;

namespace LendLedgerApi.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto?> LoginAsync(LoginDto dto);
        Task<AuthResponseDto?> LoginWithOtpAsync(string email);
    }
}
