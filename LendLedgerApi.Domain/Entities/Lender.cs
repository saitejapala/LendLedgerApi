using System;
using System.Collections.Generic;

namespace LendLedgerApi.Domain.Entities
{
    public class Lender
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Borrower> Borrowers { get; set; } = new List<Borrower>();
        public ICollection<Loan> Loans { get; set; } = new List<Loan>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
    }
}
