namespace ddpc.DartSuite.Application.Contracts.Admins;

public sealed record UpdateAdminRequest(
    Guid Id,
    string AccountName,
    DateOnly ValidFromDate,
    DateOnly ValidToDate);
