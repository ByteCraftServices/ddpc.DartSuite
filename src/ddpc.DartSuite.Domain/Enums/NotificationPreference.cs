namespace ddpc.DartSuite.Domain.Enums;

[Flags]
public enum NotificationPreference
{
    None = 0,
    OwnMatches = 1,
    FollowedMatches = 2,
    AllMatches = 4
}
