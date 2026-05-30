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
                    var remaining = b.Loan?.RemainingBalance ?? 0;
                    var principal = b.Loan?.PrincipalAmount ?? 0;
                    
                    string balanceVariant = "default";
                    if (b.Loan?.Status == "overdue") balanceVariant = "error";
                    else if (b.Loan?.Status == "paid") balanceVariant = "primary";

                    string? nextEmiNote = null;
                    string? nextEmiNoteVariant = null;
                    if (b.Loan?.Status == "overdue")
                    {
                        var days = (int)(DateTime.UtcNow.Date - b.Loan.DueDate.Date).TotalDays;
                        nextEmiNote = days > 0 ? $"{days} Days Overdue" : "Due Today";
                        nextEmiNoteVariant = "error";
                    }

                    return new BorrowerListItemDto(
                        Id: b.Id.ToString(),
                        Name: b.FullName,
                        LoanId: b.Loan != null ? $"LID-{b.Loan.Id.ToString().Substring(0, 5).ToUpper()}" : string.Empty,
                        Contact: b.Phone,
                        TotalLent: $"${principal:N2}",
                        RemainingBalance: $"${remaining:N2}",
                        BalanceVariant: balanceVariant,
                        NextEmiDate: b.Loan?.DueDate.ToString("MMM dd, yyyy"),
                        NextEmiNote: nextEmiNote,
                        NextEmiNoteVariant: nextEmiNoteVariant,
                        Status: b.Loan?.Status ?? "active",
                        StatusLabel: (b.Loan?.Status ?? "active").ToUpper()
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

            var principal = borrower.Loan?.PrincipalAmount ?? 0;
            var remaining = borrower.Loan?.RemainingBalance ?? 0;
            var paid = principal - remaining;

            // Stats
            var stats = new List<BorrowerProfileStatDto>
            {
                new BorrowerProfileStatDto("lent", "Total Lent", $"${principal:N0}", "Principal", "outbound", "bg-primary-container/10", "text-primary-container", null),
                new BorrowerProfileStatDto("paid", "Total Paid", $"${paid:N0}", "Repaid", "move_to_inbox", "bg-secondary-container/50", "text-secondary", null),
                new BorrowerProfileStatDto("balance", "Balance Due", $"${remaining:N0}", "Outstanding", "account_balance_wallet", "bg-error-container", "text-error", borrower.Loan?.Status == "overdue" ? "error" : null),
                new BorrowerProfileStatDto("interest", "Interest Earned", $"$0", "Earnings", "trending_up", "bg-tertiary-container/30", "text-tertiary", null)
            };

            // Repayments
            var repayments = borrower.Payments
                .OrderByDescending(p => p.DateReceived)
                .Select(p => new RepaymentRecordDto(
                    Id: p.Id.ToString(),
                    Date: p.DateReceived.ToString("dd MMM, yyyy"),
                    Time: p.DateReceived.ToString("hh:mm tt"),
                    Amount: $"${p.Amount:N2}",
                    Method: p.Method,
                    MethodDot: p.Method == "Cash" ? "bg-tertiary" : "bg-primary-container",
                    Evidence: string.IsNullOrEmpty(p.ReferenceId) ? "none" : "receipt"
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
            if (borrower.Loan?.Status == "overdue")
            {
                trustScore = 550;
                trustDesc = $"{borrower.FullName} is currently overdue. High risk warning.";
            }

            var profileDto = new BorrowerProfileDto(
                Id: borrower.Id.ToString(),
                Name: borrower.FullName,
                Status: borrower.Loan?.Status ?? "active",
                StatusLabel: borrower.Loan?.Status == "overdue" ? "Overdue" : borrower.Loan?.Status == "paid" ? "Paid" : "Active",
                Email: borrower.Email,
                Phone: borrower.Phone,
                Location: "Local Directory",
                Stats: stats,
                Repayments: repayments,
                RepaymentProgress: progress,
                RepaymentStart: $"Started: {borrower.Loan?.StartDate.ToString("MMM yyyy")}",
                RepaymentTarget: $"Target: {borrower.Loan?.DueDate.ToString("MMM yyyy")}",
                LoanTerms: new LoanTermsDto(
                    EmiAmount: $"${borrower.Loan?.EmiAmount:N2} / mo",
                    StartDate: borrower.Loan?.StartDate.ToString("dd MMM, yyyy") ?? string.Empty,
                    InterestRate: $"{borrower.Loan?.InterestRate:G}% P.A.",
                    NextDue: borrower.Loan?.DueDate.ToString("dd MMM, yyyy") ?? string.Empty,
                    Collateral: "Personal Trust"
                ),
                Notes: notes,
                TrustScore: trustScore,
                TrustDescription: trustDesc
            );

            return Ok(profileDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBorrower([FromBody] CreateBorrowerDto dto)
        {
            if (!_lookupService.IsValid("Category", dto.Category))
            {
                ModelState.AddModelError("Category", $"Invalid borrower category '{dto.Category}'.");
            }
            if (!_lookupService.IsValid("InterestType", dto.InterestType))
            {
                ModelState.AddModelError("InterestType", $"Invalid interest type '{dto.InterestType}'.");
            }
            if (!_lookupService.IsValid("RepaymentCycle", dto.RepaymentCycle))
            {
                ModelState.AddModelError("RepaymentCycle", $"Invalid repayment cycle '{dto.RepaymentCycle}'.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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

            if (borrower == null || borrower.Loan == null)
            {
                return NotFound(new { message = "Borrower or active loan not found." });
            }

            var loan = borrower.Loan;
            
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BorrowerId = borrower.Id,
                LenderId = lenderId,
                Amount = dto.Amount,
                DateReceived = dto.DateReceived,
                Method = dto.Method,
                ReferenceId = dto.ReferenceId ?? string.Empty,
                Notes = dto.Notes ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepository.AddAsync(payment);

            // Update Loan Balance
            loan.RemainingBalance = Math.Max(0, loan.RemainingBalance - dto.Amount);
            if (loan.RemainingBalance <= 0)
            {
                loan.Status = "paid";
            }
            else
            {
                if (loan.DueDate.Date >= DateTime.UtcNow.Date)
                {
                    loan.Status = "active";
                }
            }

            await _paymentRepository.SaveChangesAsync();
            return Created("", new { id = payment.Id, remainingBalance = loan.RemainingBalance });
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
    }
}
