using System;
using System.Text;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Domain.Interfaces;
using LendLedgerApi.Application.Interfaces;
using LendLedgerApi.Application.Services;
using LendLedgerApi.Infrastructure.Data;
using LendLedgerApi.Infrastructure.Repositories;
using LendLedgerApi.Infrastructure.Services;
using LendLedgerApi.Email;
using LendLedgerApi.CacheService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection
var connectionString = builder.Configuration.GetConnectionString("PgDefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("PostgreSQL Connection string 'PgDefaultConnection' is not configured.");
}

builder.Services.AddDbContext<LendLedgerDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Add Dependency Injections (Clean Architecture)
builder.Services.AddScoped<ILenderRepository, LenderRepository>();
builder.Services.AddScoped<IBorrowerRepository, BorrowerRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<INoteRepository, NoteRepository>();

builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
builder.Services.AddSingleton<ILookupService, LookupService>();
builder.Services.AddScoped<IEmailClient, EmailClient>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();

builder.Services.AddScoped<IPasswordHasher<Lender>, PasswordHasher<Lender>>();
builder.Services.AddHttpClient();

// 3. Add Controllers
builder.Services.AddControllers();

// 4. CORS Setup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 5. JWT Authentication Setup
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings.GetValue<string>("Secret");
var issuer = jwtSettings.GetValue<string>("Issuer");
var audience = jwtSettings.GetValue<string>("Audience");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddEndpointsApiExplorer();

// 6. Swagger configuration with JWT Auth Support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LendLedger Standalone API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 7. Initialize Lookup Cache at Startup and Drop Unique Constraint
using (var scope = app.Services.CreateScope())
{
    var lookupService = scope.ServiceProvider.GetRequiredService<ILookupService>();
    await lookupService.InitializeAsync();

    var dbContext = scope.ServiceProvider.GetRequiredService<LendLedgerDbContext>();
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE public.loans DROP CONSTRAINT IF EXISTS loans_borrower_id_key;");
        await dbContext.Database.ExecuteSqlRawAsync(@"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name   = 'payments'
                      AND column_name  = 'loan_id'
                ) THEN
                    ALTER TABLE public.payments ADD COLUMN loan_id uuid NULL;
                    ALTER TABLE public.payments
                        ADD CONSTRAINT fk_payments_loan_id
                        FOREIGN KEY (loan_id) REFERENCES public.loans(id) ON DELETE SET NULL;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name   = 'loans'
                      AND column_name  = 'tenure'
                ) THEN
                    ALTER TABLE public.loans ADD COLUMN tenure integer NOT NULL DEFAULT 0;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name   = 'loans'
                      AND column_name  = 'total_payment'
                ) THEN
                    ALTER TABLE public.loans ADD COLUMN total_payment numeric NOT NULL DEFAULT 0.0;
                END IF;
            END
            $$;
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Failed to run startup migrations: {ex.Message}");
    }
}

// 8. Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
