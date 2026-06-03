using System;

namespace LendLedgerApi.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid BorrowerId { get; set; }
        public Guid LenderId { get; set; }
        public Guid? LoanId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DateReceived { get; set; }
        public string Method { get; set; } = string.Empty; // Bank Transfer, Zelle / Venmo, Cash, Check
        public string ReferenceId { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Borrower? Borrower { get; set; }
        public Lender? Lender { get; set; }
        public Loan? Loan { get; set; }
    }
}
