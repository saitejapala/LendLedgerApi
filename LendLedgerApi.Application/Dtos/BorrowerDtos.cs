using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LendLedgerApi.Application.Dtos
{
    public record CreateBorrowerDto(
        [Required, MaxLength(100)] string FullName,
        [Required, Phone] string Phone,
        [Required, EmailAddress] string Email,
        [Required] string Category,
        [Required, Range(0.01, double.MaxValue)] decimal LoanAmount,
        [Required, Range(0.01, double.MaxValue)] decimal EmiAmount,
        [Required, Range(0.0, 100.0)] decimal InterestRate,
        [Required] string InterestType,
        [Required] DateTime StartDate,
        [Required] DateTime DueDate,
        [Required] string RepaymentCycle,
        string? Notes,
        bool AutoReminders
    );

    public record BorrowerListItemDto(
        string Id,
        string Name,
        string LoanId,
        string Contact,
        string TotalLent,
        string RemainingBalance,
        string BalanceVariant,
        string? NextEmiDate,
        string? NextEmiNote,
        string? NextEmiNoteVariant,
        string Status,
        string StatusLabel
    );

    public record PaginatedBorrowersDto(
        List<BorrowerListItemDto> Items,
        int TotalCount,
        int Page,
        int PageSize
    );

    public record CreatePaymentDto(
        [Required, Range(0.01, double.MaxValue)] decimal Amount,
        [Required] DateTime DateReceived,
        [Required] string Method,
        string? ReferenceId,
        string? Notes,
        Guid? LoanId
    );

    public record CreateNoteDto(
        [Required, MinLength(1), MaxLength(1000)] string Content
    );

    public record UpdateNoteDto(
        [Required, MinLength(1), MaxLength(1000)] string Content
    );

    public record BorrowerProfileStatDto(
        string Id,
        string Label,
        string Value,
        string Tag,
        string Icon,
        string IconBg,
        string IconColor,
        string? Variant
    );

    public record RepaymentRecordDto(
        string Id,
        string Date,
        string Time,
        string Amount,
        string Method,
        string MethodDot,
        string Evidence,
        string? LoanDisplayId
    );

    public record BorrowerNoteDto(
        string Id,
        string Date,
        string Content,
        bool ShowMenu
    );

    public record LoanTermsDto(
        string EmiAmount,
        string StartDate,
        string InterestRate,
        string NextDue,
        string Collateral
    );

    public record LoanDetailDto(
        string Id,
        string DisplayId,
        decimal PrincipalAmount,
        decimal RemainingBalance,
        decimal EmiAmount,
        decimal InterestRate,
        string InterestType,
        string RepaymentCycle,
        string StartDate,
        string DueDate,
        string Status,
        string StatusLabel,
        string? Notes
    );

    public record BorrowerProfileDto(
        string Id,
        string Name,
        string Status,
        string StatusLabel,
        string Email,
        string Phone,
        string Location,
        List<BorrowerProfileStatDto> Stats,
        List<RepaymentRecordDto> Repayments,
        int RepaymentProgress,
        string RepaymentStart,
        string RepaymentTarget,
        LoanTermsDto LoanTerms,
        List<BorrowerNoteDto> Notes,
        int TrustScore,
        string TrustDescription,
        List<LoanDetailDto> Loans
    );
}
