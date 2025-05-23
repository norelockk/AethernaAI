using System.Text;
using Newtonsoft.Json;
using AethernaAI.Util;
using AethernaAI.Model;
using VRChat.API.Model;
using VRChat.API.Client;
using User = AethernaAI.Model.User;
using UserStatus = AethernaAI.Model.UserStatus;
using static AethernaAI.Constants;

namespace AethernaAI.Manager;

public class UserManager : Registry<User>, IManager
{
  private readonly Core _core;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private string? _groupId = string.Empty;

  private VRCManager? _vrcManager;
  private DiscordManager? _discordManager;

  public bool IsInitialized => _isInitialized;

  public UserManager(Core core) : base("users")
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
    _groupId = _core.Config.GetConfig<string?>(c => c.VrchatGroupId!);

    UpdateAll(user =>
    {
      if (user.Status is UserStatus.Online)
      {
        user.Status = UserStatus.Offline;
        user.LastVisit = DateUtil.ToUnixTime(DateTime.Now);

        if (user.CurrentInstanceId is not null)
          user.CurrentInstanceId = null;
      }
    });

    if (_core.HasManager<VRCManager>())
      _vrcManager = _core.GetManagerOrDefault<VRCManager>();

    if (_core.HasManager<DiscordManager>())
      _discordManager = _core.GetManagerOrDefault<DiscordManager>();
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    _vrcManager!.LogReader!.OnProcessed += Process;

    _isInitialized = true;
  }

  public void Shutdown()
  {
    if (!_isInitialized) return;

    UpdateAll(user =>
    {
      if (user.Status is UserStatus.Online)
      {
        user.Status = UserStatus.Offline;
        user.LastVisit = DateUtil.ToUnixTime(DateTime.Now);

        if (user.CurrentInstanceId is not null)
          user.CurrentInstanceId = null;
      }
    });

    _isInitialized = false;
    Logger.Log(LogLevel.Info, "UserManager shutdown");
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_isDisposed) return;

    if (disposing)
    {
      Shutdown();
    }

    _isDisposed = true;
  }

  ~UserManager()
  {
    Dispose(false);
  }

  private protected void Process(object? sender, ProcessedLogEventArgs data)
  {
    if (!_vrcManager!.IsLogged)
      return;

    switch (data.Action)
    {
      case "joined":
        {
          UserJoined(data.Data["UserId"].ToString()!);
          break;
        }

      case "left":
        {
          UserLeft(data.Data["UserId"].ToString()!);
          break;
        }
    }
  }

  private protected void UserJoined(string userId)
  {
    if (!_vrcManager!.IsLogged)
      return;

    var now = DateUtil.ToUnixTime(DateTime.Now);
    var user = _vrcManager!.Users!.GetUser(userId);
    if (user is null)
    {
      Logger.Log(LogLevel.Warn, $"API cannot get user {userId} data");
      return;
    }

    var inGroup = IsUserInGroup(userId);
    var invited = IsUserGroupInvited(userId);
    if (!invited && !inGroup)
    {
      var groupInvite = new CreateGroupInviteRequest(userId);

      try
      {
        _vrcManager.Groups!.CreateGroupInvite(_groupId, groupInvite);
      }
      catch (ApiException ex)
      {
        Logger.Log(LogLevel.Error, $"Cannot invite user to group: {ex.Message}");
      }
    }

    if (!Has(userId))
    {
      Save(userId, new()
      {
        Id = userId,
        Status = UserStatus.Online,
        JoinedAt = now,
        LastVisit = now,
        VisitCount = 1,
        DisplayName = user.DisplayName
      });

      Logger.Log(LogLevel.Info, $"Registered {user.DisplayName} ({userId}) in registry");
    }
    else
    {
      Update(userId, data =>
      {
        if (data.Status is not UserStatus.Online)
        {
          if (data.DisplayName is null || data.DisplayName != user.DisplayName)
            data.DisplayName = user.DisplayName;

          data.Status = UserStatus.Online;
          data.VisitCount++;
        }
      });
    }

    var data = Get(userId);
    if (data is not null)
    {
      List<string> msg = new()
      {
        $"Użytkownik ``{data.DisplayName}`` (``{userId}``) dołączył na instancje",
        $"Pierwszy raz dołączył w <t:{data.JoinedAt}:F>",
        "",
        $"To jest jego {data.VisitCount} wejście, ostatnio był widziany w <t:{data.LastVisit}:F> (<t:{data.LastVisit}:R>)",
      };

      _ = _discordManager!.SendEmbed("Użytkownicy", String.Join("\n", msg));
    }
  }

  private protected void UserLeft(string userId)
  {
    if (!_vrcManager!.IsLogged)
      return;

    var now = DateUtil.ToUnixTime(DateTime.Now);

    if (!Has(userId))
    {
      var user = _vrcManager!.Users!.GetUser(userId);
      if (user is null)
      {
        Logger.Log(LogLevel.Warn, $"API cannot get user {userId} data");
        return;
      }

      Save(userId, new()
      {
        Id = userId,
        Status = UserStatus.Offline,
        JoinedAt = now,
        LastVisit = now,
        VisitCount = 1,
        DisplayName = user.DisplayName
      });

      Logger.Log(LogLevel.Info, $"Registered {user.DisplayName} ({userId}) in registry");
    }
    else
    {
      Update(userId, data =>
      {
        if (data.Status is not UserStatus.Offline)
        {
          data.Status = UserStatus.Offline;
          data.LastVisit = now;
        }
      });
    }

    var data = Get(userId);
    if (data is not null)
    {
      List<string> msg = new()
      {
        $"Użytkownik ``{data.DisplayName}`` (``{userId}``) opuścił instancje"
      };

      _ = _discordManager!.SendEmbed("Użytkownicy", String.Join("\n", msg));
    }
  }

  private bool IsUserInGroup(string userId)
  {
    if (!_vrcManager!.IsLogged)
      return true;

    var groups = _vrcManager!.Users!.GetUserGroups(userId);
    if (groups.Any(group => group.GroupId == _groupId))
      return true;

    return false;
  }

  private bool IsUserGroupInvited(string userId)
  {
    if (!_vrcManager!.IsLogged)
      return true;

    var invites = _vrcManager!.Groups!.GetGroupInvites(_groupId, 100);
    if (invites.Any(invite => invite.UserId == userId || invite.User.Id == userId))
      return true;

    return false;
  }

  #region prototype
  public async Task SendModeration(VRCModeration data)
  {
    using (var client = new HttpClient())
    {
      client.BaseAddress = new Uri($"{_vrcManager!._vrcConfig.BasePath}/user/{data.TargetUserId}/moderations");
      string cookieStr = string.Empty;

      var cookies = _vrcManager.CookieContainer!.GetAllCookies().ToList();
      foreach (var cookie in cookies)
        cookieStr += $"{cookie.Name}={cookie.Value};";

      if (!string.IsNullOrEmpty(cookieStr))
        client.DefaultRequestHeaders.Add("Cookie", cookieStr);

      client.DefaultRequestHeaders.Add("User-Agent", $"Eqipa/{VERSION} norelock");

      var json = JsonConvert.SerializeObject(data);
      var content = new StringContent(json, Encoding.UTF8, "application/json");
      var response = await client.PostAsync(client.BaseAddress, content);

      var raw = await response.Content.ReadAsStringAsync();

      if (response.IsSuccessStatusCode)
      {
        Logger.Log(LogLevel.Debug, $"Moderation response: {raw}");
      }
      else
      {
        Logger.Log(LogLevel.Debug, $"Error sending moderation: {response.StatusCode}, {raw}");
      }
    }
  }
  
  // await SendModeration(new() {
  //   Type = "warn",
  //   Reason = "test",
  //   WorldId = _worldId,
  //   InstanceId = "EqipaPoland~group(grp_2e1917ed-0f8d-4075-8098-5919a37c8f43)~groupAccessType(public)~region(eu)",
  //   IsPermanent = false,
  //   TargetUserId = "usr_56f0cd95-e51a-40c7-8746-dcd75baf8497"
  // });
  #endregion

}