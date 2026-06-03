using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Domain.Interfaces;
using LendLedgerApi.Infrastructure.Data;
using LendLedgerApi.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LendLedgerApi.Application.Interfaces;

namespace LendLedgerApi.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BorrowersController : ApiControllerBase
    {
        private readonly IBorrowerRepository _borrowerRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly INoteRepository _noteRepository;
        private readonly LendLedgerDbContext _dbContext;
        private readonly ILookupService _lookupService;

        public BorrowersController(
            IBorrowerRepository borrowerRepository,
            IPaymentRepository paymentRepository,
            INoteRepository noteRepository,
            LendLedgerDbContext dbContext,
            ILookupService lookupService)
        {
            _borrowerRepository = borrowerRepository;
            _paymentRepository = paymentRepository;
            _noteRepository = noteRepository;
            _dbContext = dbContext;
            _lookupService = lookupService;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedBorrowersDto>> GetBorrowers(
            [FromQuery] string? status,
            [FromQuery] string? sort,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var lenderId = LenderId;
            var borrowers = await _borrowerRepository.GetAllAsync(lenderId, status, sort, search);
            var borrowerList = borrowers.ToList();

            var totalCount = borrowerList.Count;

            var items = borrowerList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b =>
                {
                    var remaining = b.Loans.Sum(l => l.RemainingBalance);
                    var principal = b.Loans.Sum(l => l.PrincipalAmount);
                    
                    var aggregatedStatus = "active";
                    if (b.Loans.Any(l => l.Status == "overdue")) aggregatedStatus = "overdue";
                    else if (b.Loans.Any() && b.Loans.All(l => l.Status == "paid")) aggregatedStatus = "paid";

                    string balanceVariant = "default";
                    if (aggregatedStatus == "overdue") balanceVariant = "error";
                    else if (aggregatedStatus == "paid") balanceVariant = "primary";

                    var nextLoan = b.Loans.Where(l => l.Status != "paid").OrderBy(l => l.DueDate).FirstOrDefault() 
                                   ?? b.Loans.OrderByDescending(l => l.DueDate).FirstOrDefault();

                    string? nextEmiNote = null;
                    string? nextEmiNoteVariant = null;
                    if (aggregatedStatus == "overdue")
                    {
                        var overdueLoan = b.Loans.Where(l => l.Status == "overdue").OrderBy(l => l.DueDate).FirstOrDefault();
                        if (overdueLoan != null)
                        {
                            var days = (int)(DateTime.UtcNow.Date - overdueLoan.DueDate.Date).TotalDays;
                            nextEmiNote = days > 0 ? $"{days} Days Overdue" : "Due Today";
                            nextEmiNoteVariant = "error";
                        }
                    }

                    var recentLoanForId = b.Loans.Where(l => l.Status != "paid").OrderByDescending(l => l.StartDate).FirstOrDefault() 
                                           ?? b.Loans.OrderByDescending(l => l.StartDate).FirstOrDefault();

                    return new BorrowerListItemDto(
                        Id: b.Id.ToString(),
                        Name: b.FullName,
                        LoanId: recentLoanForId != null ? $"LID-{recentLoanForId.Id.ToString().Substring(0, 5).ToUpper()}" : string.Empty,
                        Contact: b.Phone,
                        TotalLent: $"${principal:N2}",
                        RemainingBalance: $"${remaining:N2}",
                        BalanceVariant: balanceVariant,
                        NextEmiDate: nextLoan?.DueDate.ToString("MMM dd, yyyy"),
                        NextEmiNote: nextEmiNote,
                        NextEmiNoteVariant: nextEmiNoteVariant,
                        Status: aggregatedStatus,
                        StatusLabel: aggregatedStatus.ToUpper()
                    );
                })
                .ToList();

            return Ok(new PaginatedBorrowersDto(items, totalCount, page, pageSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BorrowerProfileDto>> GetBorrowerById(Guid id)
        {
            var lenderId = LenderId;
            var borrower = await _borrowerRepository.GetByIdAsync(id, lenderId);
            
            if (borrower == null)
            {
                return NotFound(new { message = "Borrower not found." });
            }

            var principal = borrower.Loans.Sum(l => l.PrincipalAmount);
            var remaining = borrower.Loans.Sum(l => l.RemainingBalance);
            var paid = principal - remaining;

            var aggregatedStatus = "active";
            if (borrower.Loans.Any(l => l.Status == "overdue")) aggregatedStatus = "overdue";
            else if (borrower.Loans.Any() && borrower.Loans.All(l => l.Status == "paid")) aggregatedStatus = "paid";

            // Stats
            var stats = new List<BorrowerProfileStatDto>
            {
                new BorrowerProfileStatDto("lent", "Total Lent", $"${principal:N0}", "Principal", "outbound", "bg-primary-container/10", "text-primary-container", null),
                new BorrowerProfileStatDto("paid", "Total Paid", $"${paid:N0}", "Repaid", "move_to_inbox", "bg-secondary-container/50", "text-secondary", null),
                new BorrowerProfileStatDto("balance", "Balance Due", $"${remaining:N0}", "Outstanding", "account_balance_wallet", "bg-error-container", "text-error", aggregatedStatus == "overdue" ? "error" : null),
                new BorrowerProfileStatDto("interest", "Interest Earned", $"$0", "Earnings", "trending_up", "bg-tertiary-container/30", "text-tertiary", null)
            };

            // Repayments
            var loanLookup = borrower.Loans.ToDictionary(l => l.Id, l => $"LID-{l.Id.ToString().Substring(0, 5).ToUpper()}");
            var repayments = borrower.Payments
                .OrderByDescending(p => p.DateReceived)
                .Select(p => new RepaymentRecordDto(
                    Id: p.Id.ToString(),
                    Date: p.DateReceived.ToString("dd MMM, yyyy"),
                    Time: p.DateReceived.ToString("hh:mm tt"),
                    Amount: $"${p.Amount:N2}",
                    Method: p.Method,
                    MethodDot: p.Method == "Cash" ? "bg-tertiary" : "bg-primary-container",
                    Evidence: string.IsNullOrEmpty(p.ReferenceId) ? "none" : "receipt",
                    LoanDisplayId: p.LoanId.HasValue && loanLookup.ContainsKey(p.LoanId.Value) ? loanLookup[p.LoanId.Value] : null
                ))
                .ToList();

            // Notes
            var notes = borrower.Notes
                .OrderByDescending(n => n.DateAdded)
                .Select(n => new BorrowerNoteDto(
                    Id: n.Id.ToString(),
                    Date: n.DateAdded.ToString("dd MMM"),
                    Content: n.Content,
                    ShowMenu: true
                ))
                .ToList();

            // Calculate progress
            int progress = principal > 0 ? (int)Math.Round((paid / principal) * 100) : 0;

            // Trust score mock rules
            int trustScore = 750;
            string trustDesc = $"{borrower.FullName} maintains a consistent repayment record.";
            if (aggregatedStatus == "overdue")
            {
                trustScore = 550;
                trustDesc = $"{borrower.FullName} is currently overdue. High risk warning.";
            }

            var activeOrRecentLoan = borrower.Loans.Where(l => l.Status != "paid").OrderByDescending(l => l.StartDate).FirstOrDefault() 
                                     ?? borrower.Loans.OrderByDescending(l => l.StartDate).FirstOrDefault();

            var loans = borrower.Loans
                .OrderBy(l => l.StartDate)
                .Select(l => new LoanDetailDto(
                    Id: l.Id.ToString(),
                    DisplayId: $"LID-{l.Id.ToString().Substring(0, 5).ToUpper()}",
                    PrincipalAmount: l.PrincipalAmount,
                    RemainingBalance: l.RemainingBalance,
                    EmiAmount: l.EmiAmount,
                    InterestRate: l.InterestRate,
                    InterestType: l.InterestType,
                    RepaymentCycle: l.RepaymentCycle,
                    StartDate: l.StartDate.ToString("dd MMM, yyyy"),
                    DueDate: l.DueDate.ToString("dd MMM, yyyy"),
                    Status: l.Status,
                    StatusLabel: l.Status == "overdue" ? "Overdue" : l.Status == "paid" ? "Paid" : "Active",
                    Notes: l.Notes
                ))
                .ToList();

            var profileDto = new BorrowerProfileDto(
                Id: borrower.Id.ToString(),
                Name: borrower.FullName,
                Status: aggregatedStatus,
                StatusLabel: aggregatedStatus == "overdue" ? "Overdue" : aggregatedStatus == "paid" ? "Paid" : "Active",
                Email: borrower.Email,
                Phone: borrower.Phone,
                Location: "Local Directory",
                Stats: stats,
                Repayments: repayments,
                RepaymentProgress: progress,
                RepaymentStart: activeOrRecentLoan != null ? $"Started: {activeOrRecentLoan.StartDate.ToString("MMM yyyy")}" : string.Empty,
                RepaymentTarget: activeOrRecentLoan != null ? $"Target: {activeOrRecentLoan.DueDate.ToString("MMM yyyy")}" : string.Empty,
                LoanTerms: new LoanTermsDto(
                    EmiAmount: activeOrRecentLoan != null ? $"${activeOrRecentLoan.EmiAmount:N2} / mo" : "$0.00 / mo",
                    StartDate: activeOrRecentLoan?.StartDate.ToString("dd MMM, yyyy") ?? string.Empty,
                    InterestRate: activeOrRecentLoan != null ? $"{activeOrRecentLoan.InterestRate:G}% P.A." : "0% P.A.",
                    NextDue: activeOrRecentLoan?.DueDate.ToString("dd MMM, yyyy") ?? string.Empty,
                    Collateral: activeOrRecentLoan != null && !string.IsNullOrEmpty(activeOrRecentLoan.Notes) ? activeOrRecentLoan.Notes : "Personal Trust"
                ),
                Notes: notes,
                TrustScore: trustScore,
                TrustDescription: trustDesc,
                Loans: loans
            );

            return Ok(profileDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBorrower([FromBody] CreateBorrowerDto dto)
        {
            //if (!_lookupService.IsValid("Category", dto.Category))
            //{
            //    ModelState.AddModelError("Category", $"Invalid borrower category '{dto.Category}'.");
            //}
            //if (!_lookupService.IsValid("InterestType", dto.InterestType))
            //{
            //    ModelState.AddModelError("InterestType", $"Invalid interest type '{dto.InterestType}'.");
            //}
            //if (!_lookupService.IsValid("RepaymentCycle", dto.RepaymentCycle))
            //{
            //    ModelState.AddModelError("RepaymentCycle", $"Invalid repayment cycle '{dto.RepaymentCycle}'.");
            //}

            //if (!ModelState.IsValid)
            //{
            //    return BadRequest(ModelState);
            //}

            var lenderId = LenderId;

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var borrower = new Borrower
                {
                    Id = Guid.NewGuid(),
                    LenderId = lenderId,
                    FullName = dto.FullName,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    Category = dto.Category,
                    AutoReminders = dto.AutoReminders,
                    CreatedAt = DateTime.UtcNow
                };

                await _borrowerRepository.AddAsync(borrower);

                var loan = new Loan
                {
                    Id = Guid.NewGuid(),
                    BorrowerId = borrower.Id,
                    LenderId = lenderId,
                    PrincipalAmount = dto.LoanAmount,
                    RemainingBalance = dto.LoanAmount,
                    EmiAmount = dto.EmiAmount,
                    InterestRate = dto.InterestRate,
                    InterestType = dto.InterestType,
                    RepaymentCycle = dto.RepaymentCycle,
                    StartDate = dto.StartDate,
                    DueDate = dto.DueDate,
                    Notes = dto.Notes ?? string.Empty,
                    Status = dto.DueDate.Date < DateTime.UtcNow.Date ? "overdue" : "active"
                };

                await _dbContext.Loans.AddAsync(loan);
                await _borrowerRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetBorrowerById), new { id = borrower.Id }, new { id = borrower.Id });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPost("{id}/payments")]
        public async Task<IActionResult> AddPayment(Guid id, [FromBody] CreatePaymentDto dto)
        {
            if (!_lookupService.IsValid("PaymentMethod", dto.Method))
            {
                ModelState.AddModelError("Method", $"Invalid payment method '{dto.Method}'.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lenderId = LenderId;
            var borrower = await _borrowerRepository.GetByIdAsync(id, lenderId);

            if (borrower == null || !borrower.Loans.Any())
            {
                return NotFound(new { message = "Borrower or active loans not found." });
            }

            var activeLoans = borrower.Loans
                .Where(l => l.Status != "paid")
                .OrderBy(l => l.StartDate)
                .ToList();

            if (!activeLoans.Any())
            {
                return BadRequest(new { message = "No active loans to apply payment to." });
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BorrowerId = borrower.Id,
                LenderId = lenderId,
                LoanId = dto.LoanId,
                Amount = dto.Amount,
                DateReceived = dto.DateReceived,
                Method = dto.Method,
                ReferenceId = dto.ReferenceId ?? string.Empty,
                Notes = dto.Notes ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepository.AddAsync(payment);

            var loansToProcess = new List<Loan>();

            if (dto.LoanId.HasValue)
            {
                // Targeted repayment: apply payment to the specified loan only
                var targetLoan = borrower.Loans.FirstOrDefault(l => l.Id == dto.LoanId.Value);
                if (targetLoan == null)
                {
                    return NotFound(new { message = "Specified loan not found for this borrower." });
                }
                if (targetLoan.Status == "paid")
                {
                    return BadRequest(new { message = "The selected loan is already fully paid." });
                }
                loansToProcess.Add(targetLoan);
            }
            else
            {
                // FIFO: apply payment sequentially to oldest unpaid loans first
                loansToProcess = activeLoans;
            }

            decimal remainingPayment = dto.Amount;
            foreach (var loan in loansToProcess)
            {
                if (remainingPayment <= 0) break;

                var allocation = Math.Min(loan.RemainingBalance, remainingPayment);
                loan.RemainingBalance -= allocation;
                remainingPayment -= allocation;

                if (loan.RemainingBalance <= 0)
                {
                    loan.Status = "paid";
                }
                else
                {
                    loan.Status = loan.DueDate.Date >= DateTime.UtcNow.Date ? "active" : "overdue";
                }
            }

            await _paymentRepository.SaveChangesAsync();
            
            var totalRemaining = borrower.Loans.Sum(l => l.RemainingBalance);
            return Created("", new { id = payment.Id, remainingBalance = totalRemaining });
        }

        [HttpPost("{id}/notes")]
        public async Task<IActionResult> AddNote(Guid id, [FromBody] CreateNoteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lenderId = LenderId;
            var borrower = await _borrowerRepository.GetByIdAsync(id, lenderId);

            if (borrower == null)
            {
                return NotFound(new { message = "Borrower not found." });
            }

            var note = new Note
            {
                Id = Guid.NewGuid(),
                BorrowerId = borrower.Id,
                LenderId = lenderId,
                Content = dto.Content,
                DateAdded = DateTime.UtcNow
            };

            await _noteRepository.AddAsync(note);
            await _noteRepository.SaveChangesAsync();

            return Created("", new { id = note.Id, date = note.DateAdded.ToString("dd MMM") });
        }

        [HttpPut("{id}/notes/{noteId}")]
        public async Task<IActionResult> EditNote(Guid id, Guid noteId, [FromBody] UpdateNoteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lenderId = LenderId;
            var note = await _noteRepository.GetByIdAsync(noteId, lenderId);

            if (note == null || note.BorrowerId != id)
            {
                return NotFound(new { message = "Note not found or does not belong to this borrower." });
            }

            note.Content = dto.Content;
            await _noteRepository.SaveChangesAsync();

            return Ok(new { message = "Note updated successfully." });
        }

        [HttpDelete("{id}/notes/{noteId}")]
        public async Task<IActionResult> DeleteNote(Guid id, Guid noteId)
        {
            var lenderId = LenderId;
            var note = await _noteRepository.GetByIdAsync(noteId, lenderId);

            if (note == null || note.BorrowerId != id)
            {
                return NotFound(new { message = "Note not found or does not belong to this borrower." });
            }

            await _noteRepository.DeleteAsync(noteId, lenderId);
            await _noteRepository.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/loans")]
        public async Task<IActionResult> AddLoan(Guid id, [FromBody] CreateLoanDto dto)
        {
            var lenderId = LenderId;
            var borrower = await _borrowerRepository.GetByIdAsync(id, lenderId);

            if (borrower == null)
            {
                return NotFound(new { message = "Borrower not found." });
            }

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                BorrowerId = borrower.Id,
                LenderId = lenderId,
                PrincipalAmount = dto.LoanAmount,
                RemainingBalance = dto.LoanAmount,
                EmiAmount = dto.EmiAmount,
                InterestRate = dto.InterestRate,
                InterestType = dto.InterestType,
                RepaymentCycle = dto.RepaymentCycle,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                Notes = dto.Notes ?? string.Empty,
                Status = dto.DueDate.Date < DateTime.UtcNow.Date ? "overdue" : "active"
            };

            await _dbContext.Loans.AddAsync(loan);
            await _borrowerRepository.SaveChangesAsync();

            return Created("", new { id = loan.Id });
        }
    }
}

namespace LendLedgerApi.Application.Dtos
{
    public record CreateLoanDto(
        decimal LoanAmount,
        decimal EmiAmount,
        decimal InterestRate,
        string InterestType,
        string RepaymentCycle,
        DateTime StartDate,
        DateTime DueDate,
        string? Notes
    );
}
