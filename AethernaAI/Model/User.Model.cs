using Newtonsoft.Json;

namespace AethernaAI.Model;

public class UserAvatar
{
  [JsonProperty("id")]
  public string? Id { get; set; } = null;
  
  [JsonProperty("name")]
  public string? Name { get; set; } = null;
}

public class User
{
  [JsonProperty("id")]
  public string? Id { get; set; } = null;

  [JsonProperty("joinedAt")]
  public long JoinedAt { get; set; } = 0;

  [JsonProperty("lastVisit")]
  public long LastVisit { get; set; } = 0;

  [JsonProperty("displayName")]
  public string? DisplayName { get; set; } = null;

  [JsonProperty("lastAvatars")]
  public List<UserAvatar>? LastAvatars { get; set; } = new(); // empty array = []
}