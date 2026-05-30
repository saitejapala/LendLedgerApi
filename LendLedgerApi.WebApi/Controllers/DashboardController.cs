using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LendLedgerApi.Infrastructure.Data;
using LendLedgerApi.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendLedgerApi.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ApiControllerBase
    {
        private readonly LendLedgerDbContext _dbContext;

        public DashboardController(LendLedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDataDto>> GetDashboardData()
        {
            var lenderId = LenderId;

            // 1. Fetch Lender details
            var lender = await _dbContext.Lenders
                .FirstOrDefaultAsync(l => l.Id == lenderId);
            if (lender == null)
            {
                return NotFound("Lender account not found.");
            }

            // 2. Fetch all loans and payments for calculation
            var loans = await _dbContext.Loans
                .Include(l => l.Borrower)
                .Where(l => l.LenderId == lenderId)
                .ToListAsync();

            var payments = await _dbContext.Payments
                .Where(p => p.LenderId == lenderId)
                .ToListAsync();

            // 3. Compute Metrics
            decimal totalRecovered = payments.Sum(p => p.Amount);
            decimal totalOutstanding = loans.Where(l => l.Status != "paid").Sum(l => l.RemainingBalance);
            decimal totalLent = loans.Sum(l => l.PrincipalAmount);
            int overdueAccountsCount = loans.Count(l => l.Status == "overdue");
            
            int recoveryRate = totalLent > 0 
                ? (int)Math.Round((totalRecovered / totalLent) * 100) 
                : 0;

            var metrics = new List<MetricCardDto>
            {
                new MetricCardDto(
                    Id: "recovered",
                    Label: "Total Recovered",
                    Value: $"${totalRecovered:N0}",
                    AccentColor: "bg-primary",
                    Icon: "payments",
                    IconBg: "bg-secondary-container",
                    IconColor: "text-primary"
                ),
                new MetricCardDto(
                    Id: "outstanding",
                    Label: "Outstanding",
                    Value: $"${totalOutstanding:N0}",
                    AccentColor: "bg-secondary",
                    Icon: "pending_actions",
                    IconBg: "bg-surface-container",
                    IconColor: "text-secondary"
                ),
                new MetricCardDto(
                    Id: "overdue",
                    Label: "Overdue",
                    Value: $"{overdueAccountsCount} Accounts",
                    AccentColor: "bg-error",
                    Icon: "error_outline",
                    IconBg: "bg-error-container",
                    IconColor: "text-error"
                ),
                new MetricCardDto(
                    Id: "recovery-rate",
                    Label: "Recovery Rate",
                    Value: $"{recoveryRate}%",
                    AccentColor: "bg-primary-container",
                    Icon: "donut_large",
                    IconBg: "bg-secondary-container",
                    IconColor: "text-primary-container",
                    Progress: recoveryRate
                )
            };

            // 4. Compute Status Distribution
            int totalLoansCount = loans.Count;
            int activeLoansCount = loans.Count(l => l.Status == "active");
            int overdueLoansCount = loans.Count(l => l.Status == "overdue");

            double activePercent = totalLoansCount > 0 ? Math.Round((double)activeLoansCount / totalLoansCount * 100, 1) : 0;
            double overduePercent = totalLoansCount > 0 ? Math.Round((double)overdueLoansCount / totalLoansCount * 100, 1) : 0;

            var statusDistribution = new List<StatusDistributionDto>
            {
                new StatusDistributionDto("Active Loans", activeLoansCount, activePercent, "bg-primary"),
                new StatusDistributionDto("Overdue", overdueLoansCount, overduePercent, "bg-secondary-container")
            };

            // 5. Compute Urgent Followups
            var urgentFollowups = new List<UrgentFollowupDto>();
            var nonPaidLoans = loans.Where(l => l.Status != "paid" && l.Borrower != null).ToList();

            foreach (var loan in nonPaidLoans)
            {
                var dueDate = loan.DueDate;
                var today = DateTime.UtcNow.Date;
                if (today > dueDate.Date)
                {
                    var daysOverdue = (int)(today - dueDate.Date).TotalDays;
                    string status = daysOverdue > 20 ? "High Risk" : "Overdue";
                    string statusVariant = daysOverdue > 20 ? "high-risk" : "overdue";
                    string daysVariant = "error";

                    var name = loan.Borrower!.FullName;
                    var nameParts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var initials = nameParts.Length > 0 
                        ? string.Concat(nameParts.Select(s => s[0])) 
                        : "B";

                    urgentFollowups.Add(new UrgentFollowupDto(
                        Id: loan.Borrower.Id.ToString(),
                        Name: name,
                        Initials: initials.Length > 2 ? initials.Substring(0, 2).ToUpper() : initials.ToUpper(),
                        LoanId: $"LD-{loan.Id.ToString().Substring(0, 4).ToUpper()}",
                        Amount: $"${loan.RemainingBalance:N2}",
                        Status: status,
                        StatusVariant: statusVariant,
                        DaysOverdue: daysOverdue,
                        DaysVariant: daysVariant
                    ));
                }
                else
                {
                    var daysRemaining = (int)(dueDate.Date - today).TotalDays;
                    if (daysRemaining <= 5) // Recent / approaching
                    {
                        var name = loan.Borrower!.FullName;
                        var nameParts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var initials = nameParts.Length > 0 
                            ? string.Concat(nameParts.Select(s => s[0])) 
                            : "B";

                        urgentFollowups.Add(new UrgentFollowupDto(
                            Id: loan.Borrower.Id.ToString(),
                            Name: name,
                            Initials: initials.Length > 2 ? initials.Substring(0, 2).ToUpper() : initials.ToUpper(),
                            LoanId: $"LD-{loan.Id.ToString().Substring(0, 4).ToUpper()}",
                            Amount: $"${loan.RemainingBalance:N2}",
                            Status: "Recent",
                            StatusVariant: "recent",
                            DaysOverdue: daysRemaining,
                            DaysVariant: "secondary"
                        ));
                    }
                }
            }

            // Sort by severity (overdue days descending) and take top 5
            var sortedFollowups = urgentFollowups
                .OrderByDescending(f => f.StatusVariant == "high-risk")
                .ThenByDescending(f => f.StatusVariant == "overdue")
                .ThenByDescending(f => f.DaysOverdue)
                .Take(5)
                .ToList();

            var dashboardData = new DashboardDataDto(
                LenderName: lender.FullName,
                Metrics: metrics,
                StatusDistribution: statusDistribution,
                StatusTotal: totalLoansCount,
                UrgentFollowups: sortedFollowups
            );

            return Ok(dashboardData);
        }
    }
}
