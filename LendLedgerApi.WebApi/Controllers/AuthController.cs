using System.Threading.Tasks;
using LendLedgerApi.Application.Dtos;
using LendLedgerApi.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LendLedgerApi.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;

        public AuthController(IAuthService authService, IOtpService otpService)
        {
            _authService = authService;
            _otpService = otpService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(dto);
            if (result == null)
            {
                return BadRequest(new { message = "Email is already registered." });
            }

            return Created("", result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(dto);
            if (result == null)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            return Ok(result);
        }

        [HttpPost("otp/request")]
        public async Task<IActionResult> RequestOtp([FromBody] RequestOtpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _otpService.GenerateAndSendOtpAsync(dto.Email);
            return Ok(new { message = "OTP code has been generated and dispatched." });
        }

        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var isValid = await _otpService.VerifyOtpAsync(dto.Email, dto.Code);
            if (!isValid)
            {
                return BadRequest(new { message = "Invalid or expired verification code." });
            }

            var result = await _authService.LoginWithOtpAsync(dto.Email);
            if (result == null)
            {
                return BadRequest(new { message = "Failed to authenticate user." });
            }

            return Ok(result);
        }
    }
}
