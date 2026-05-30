using System.ComponentModel.DataAnnotations;

namespace LendLedgerApi.Application.Dtos
{
    public record RegisterDto(
        [Required, MaxLength(100)] string FullName,
        [Required, EmailAddress] string Email,
        [Required, MinLength(8)] string Password
    );

    public record LoginDto(
        [Required, EmailAddress] string Email,
        [Required] string Password
    );

    public record RequestOtpDto(
        [Required, EmailAddress] string Email
    );

    public record VerifyOtpDto(
        [Required, EmailAddress] string Email,
        [Required, StringLength(6, MinimumLength = 6)] string Code
    );

    public record AuthResponseDto(
        string Token,
        string FullName,
        string Email
    );
}
