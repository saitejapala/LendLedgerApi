using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Domain.Interfaces;
using LendLedgerApi.Application.Dtos;
using LendLedgerApi.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LendLedgerApi.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly ILenderRepository _lenderRepository;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<Lender> _passwordHasher;

        public AuthService(
            ILenderRepository lenderRepository,
            IConfiguration configuration,
            IPasswordHasher<Lender> passwordHasher)
        {
            _lenderRepository = lenderRepository;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
        {
            var emailLower = dto.Email.ToLower();
            var existingLender = await _lenderRepository.GetByEmailAsync(emailLower);
            if (existingLender != null)
            {
                return null; // Email already exists
            }

            var lender = new Lender
            {
                Id = Guid.NewGuid(),
                FullName = dto.FullName,
                Email = emailLower,
                CreatedAt = DateTime.UtcNow
            };

            lender.PasswordHash = _passwordHasher.HashPassword(lender, dto.Password);

            await _lenderRepository.AddAsync(lender);
            await _lenderRepository.SaveChangesAsync();

            var token = GenerateJwtToken(lender);
            return new AuthResponseDto(token, lender.FullName, lender.Email);
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
        {
            var emailLower = dto.Email.ToLower();
            var lender = await _lenderRepository.GetByEmailAsync(emailLower);
            if (lender == null)
            {
                return null; // User not found
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(lender, lender.PasswordHash, dto.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return null; // Password mismatch
            }

            var token = GenerateJwtToken(lender);
            return new AuthResponseDto(token, lender.FullName, lender.Email);
        }

        public async Task<AuthResponseDto?> LoginWithOtpAsync(string email)
        {
            var emailLower = email.ToLower();
            var lender = await _lenderRepository.GetByEmailAsync(emailLower);
            
            // Auto-register if not exists (passwordless onboarding)
            if (lender == null)
            {
                lender = new Lender
                {
                    Id = Guid.NewGuid(),
                    FullName = emailLower.Split('@')[0], // default name
                    Email = emailLower,
                    PasswordHash = string.Empty, // passwordless
                    CreatedAt = DateTime.UtcNow
                };
                await _lenderRepository.AddAsync(lender);
                await _lenderRepository.SaveChangesAsync();
            }

            var token = GenerateJwtToken(lender);
            return new AuthResponseDto(token, lender.FullName, lender.Email);
        }

        private string GenerateJwtToken(Lender lender)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings.GetValue<string>("Secret") ?? "DefaultSuperSecretKey1234567890123456";
            var issuer = jwtSettings.GetValue<string>("Issuer") ?? "LendLedger.Api";
            var audience = jwtSettings.GetValue<string>("Audience") ?? "LendLedger.App";
            var expiryMinutes = jwtSettings.GetValue<int>("ExpiryMinutes", 1440); // default 24 hours

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, lender.Id.ToString()),
                new Claim(ClaimTypes.Name, lender.FullName),
                new Claim(ClaimTypes.Email, lender.Email)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
