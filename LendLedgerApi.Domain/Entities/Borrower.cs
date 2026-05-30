using System;
using System.Collections.Generic;

namespace LendLedgerApi.Domain.Entities
{
    public class Borrower
    {
        public Guid Id { get; set; }
        public Guid LenderId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // e.g. Personal - Friend, Personal - Family, Professional - Business, Other
        public bool AutoReminders { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Lender? Lender { get; set; }
        public Loan? Loan { get; set; }
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
    }
}
