using System.Collections.Generic;

namespace LendLedgerApi.Application.Dtos
{
    public record MetricCardDto(
        string Id,
        string Label,
        string Value,
        string AccentColor,
        string Icon,
        string IconBg,
        string IconColor,
        string? BadgeText = null,
        string? BadgeClassName = null,
        int? Progress = null
    );

    public record StatusDistributionDto(
        string Label,
        int Count,
        double Percentage,
        string ColorClass
    );

    public record UrgentFollowupDto(
        string Id,
        string Name,
        string Initials,
        string LoanId,
        string Amount,
        string Status,
        string StatusVariant,
        int DaysOverdue,
        string DaysVariant
    );

    public record DashboardDataDto(
        string LenderName,
        List<MetricCardDto> Metrics,
        List<StatusDistributionDto> StatusDistribution,
        int StatusTotal,
        List<UrgentFollowupDto> UrgentFollowups
    );
}
