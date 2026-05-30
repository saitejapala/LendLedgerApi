using System;

namespace LendLedgerApi.Domain.Entities
{
    public class Note
    {
        public Guid Id { get; set; }
        public Guid BorrowerId { get; set; }
        public Guid LenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Borrower? Borrower { get; set; }
        public Lender? Lender { get; set; }
    }
}
