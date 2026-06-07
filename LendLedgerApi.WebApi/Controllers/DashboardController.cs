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
        public async Task<ActionResult<DashboardDataDto>> GetDashboardData([FromQuery] string range = "all")
        {
            var lenderId = LenderId;

            DateTime? startDate = null;
            var today = DateTime.UtcNow.Date;

            if (range == "30d")
            {
                startDate = today.AddDays(-30);
            }
            else if (range == "90d")
            {
                startDate = today.AddDays(-90);
            }
            else if (range == "ytd")
            {
                startDate = new DateTime(today.Year, 1, 1);
            }

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

            try
            {
                var debugData = new
                {
                    loans = loans.Select(l => new { l.Id, l.Status, l.DueDate, l.RemainingBalance, BorrowerNull = l.Borrower == null, BorrowerName = l.Borrower?.FullName }),
                    today = DateTime.UtcNow.Date
                };
                System.IO.File.WriteAllText(@"c:\Users\Satya\React Projects\lender-management\debug_loans.json", System.Text.Json.JsonSerializer.Serialize(debugData));
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(@"c:\Users\Satya\React Projects\lender-management\debug_loans_error.txt", ex.ToString());
            }

            var payments = await _dbContext.Payments
                .Where(p => p.LenderId == lenderId)
                .ToListAsync();

            // Filter lists based on the resolved date range filter
            var filteredLoans = startDate.HasValue
                ? loans.Where(l => l.StartDate >= startDate.Value).ToList()
                : loans;

            var filteredPayments = startDate.HasValue
                ? payments.Where(p => p.DateReceived >= startDate.Value).ToList()
                : payments;

            // 3. Compute Metrics
            decimal totalRecovered = filteredPayments.Sum(p => p.Amount);
            decimal totalOutstanding = filteredLoans.Where(l => l.Status != "paid").Sum(l => l.RemainingBalance);
            decimal totalLent = filteredLoans.Sum(l => l.PrincipalAmount);
            int overdueAccountsCount = filteredLoans.Count(l => l.Status == "overdue");
            
            int recoveryRate = totalLent > 0 
                ? (int)Math.Round((totalRecovered / totalLent) * 100) 
                : 0;

            var metrics = new List<MetricCardDto>
            {
                new MetricCardDto(
                    Id: "recovered",
                    Label: "Total Recovered",
                    Value: $"₹{totalRecovered:N0}",
                    AccentColor: "bg-primary",
                    Icon: "payments",
                    IconBg: "bg-secondary-container",
                    IconColor: "text-primary"
                ),
                new MetricCardDto(
                    Id: "outstanding",
                    Label: "Outstanding",
                    Value: $"₹{totalOutstanding:N0}",
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
            int totalLoansCount = filteredLoans.Count;
            int activeLoansCount = filteredLoans.Count(l => l.Status == "active");
            int overdueLoansCount = filteredLoans.Count(l => l.Status == "overdue");

            double activePercent = totalLoansCount > 0 ? Math.Round((double)activeLoansCount / totalLoansCount * 100, 1) : 0;
            double overduePercent = totalLoansCount > 0 ? Math.Round((double)overdueLoansCount / totalLoansCount * 100, 1) : 0;

            var statusDistribution = new List<StatusDistributionDto>
            {
                new StatusDistributionDto("Active Loans", activeLoansCount, activePercent, "bg-primary"),
                new StatusDistributionDto("Overdue", overdueLoansCount, overduePercent, "bg-secondary-container")
            };

            // 5. Compute Urgent Followups
            var urgentFollowups = new List<UrgentFollowupDto>();
            var nonPaidLoans = filteredLoans.Where(l => l.Status != "paid" && l.Borrower != null).ToList();

            foreach (var loan in nonPaidLoans)
            {
                var dueDate = loan.DueDate;
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
                        Amount: $"₹{loan.RemainingBalance:N2}",
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
                            Amount: $"₹{loan.RemainingBalance:N2}",
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
