namespace ddpc.DartSuite.Application.Contracts.Admins;

public sealed record CreateAdminRequest(
    string AccountName,
    DateOnly ValidFromDate,
    DateOnly ValidToDate);
