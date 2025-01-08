namespace AethernaAI.Model;

public enum VRCModerationType
{
  Ban = 3,
  None = 0,
  Warn = 1,
  Kick = 2,
}

public enum VRCModerationTimeRange
{
  None = 0,
  OneDay = 2,
  OneHour = 1,
}

public static class VRCModerationHelpers
{
  public static string ModerationTimeRangeToString(VRCModerationTimeRange timeRange)
  {
    switch (timeRange)
    {
      case VRCModerationTimeRange.None:
        return "";
      case VRCModerationTimeRange.OneDay:
        return "1_day_ahead";
      case VRCModerationTimeRange.OneHour:
        return "1_hour_ahead";
      default:
        return "<unknown_ModerationTimeRange>";
    }
  }
}