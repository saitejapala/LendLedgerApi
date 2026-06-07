using System;

namespace LendLedgerApi.Domain.Entities
{
    public class Loan
    {
        public Guid Id { get; set; }
        public Guid BorrowerId { get; set; }
        public Guid LenderId { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal EmiAmount { get; set; }
        public decimal TotalPayment { get; set; }   // Principal + Total Interest (full repayable amount)
        public int Tenure { get; set; }              // Number of repayment periods
        public decimal InterestRate { get; set; }
        public string InterestType { get; set; } = string.Empty; // Fixed Monthly, Reducing Balance, Flat Rate, No Interest
        public string RepaymentCycle { get; set; } = string.Empty; // Monthly, Weekly, Quarterly, Lump Sum
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string Status { get; set; } = "active"; // overdue, active, paid

        // Navigation properties
        public Borrower? Borrower { get; set; }
        public Lender? Lender { get; set; }
    }
}
