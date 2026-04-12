namespace ddpc.DartSuite.Application.Contracts.Admins;

public sealed record AdminDto(
    Guid Id,
    string AccountName,
    DateOnly ValidFromDate,
    DateOnly ValidToDate);
