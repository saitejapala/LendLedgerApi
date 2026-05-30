namespace LendLedgerApi.Domain.Entities
{
    public class LookupValue
    {
        public string Id { get; set; } = string.Empty;       // Unique ID, e.g., "category:personal"
        public string Type { get; set; } = string.Empty;     // e.g., "Category", "InterestType", "PaymentMethod", "LoanStatus", "RepaymentCycle"
        public string Code { get; set; } = string.Empty;     // e.g., "personal"
        public string Value { get; set; } = string.Empty;    // UI Label, e.g., "Personal"
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
