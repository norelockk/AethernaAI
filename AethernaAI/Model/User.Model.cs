using Newtonsoft.Json;

namespace AethernaAI.Model;

public class UserAvatar
{
  [JsonProperty("id")]
  public string? Id { get; set; } = null;
  
  [JsonProperty("name")]
  public string? Name { get; set; } = null;
}

public enum UserPenaltyType
{
  Ban,
  Warning,
  Blacklist
}

public enum UserStatus
{
  Online,
  Offline
}

public class UserPenalty
{
  [JsonProperty("id")]
  public string? Id { get; set; } = null;

  [JsonProperty("type")]
  public UserPenaltyType? Type { get; set; } = UserPenaltyType.Warning;

  [JsonProperty("reason")]
  public string? Reason { get; set; } = null;

  [JsonProperty("expires")]
  public long Expires { get; set; } = -1;

  [JsonProperty("invoker")]
  public string? Invoker { get; set; } = null;

  [JsonProperty("received")]
  public long Received { get; set; } = 0;
}

public class User
{
  [JsonProperty("id")]
  public string? Id { get; set; } = null;

  [JsonProperty("status")]
  public UserStatus Status { get; set; } = UserStatus.Offline;

  [JsonProperty("discordId")]
  public string? DiscordId { get; set; } = null;

  [JsonProperty("joinedAt")]
  public long JoinedAt { get; set; } = 0;

  [JsonProperty("lastVisit")]
  public long LastVisit { get; set; } = 0;

  [JsonProperty("displayName")]
  public string? DisplayName { get; set; } = null;

  [JsonProperty("lastAvatars")]
  public List<UserAvatar>? LastAvatars { get; set; } = new(); // empty array = []

  [JsonProperty("penalties")]
  public List<UserPenalty>? Penalties { get; set; } = new();
}