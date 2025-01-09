using AethernaAI.Util;
using AethernaAI.Model;
using AethernaAI.Module.Internal;
using VRChat.API.Model;
using VRChat.API.Client;
using User = AethernaAI.Model.User;
using UserStatus = AethernaAI.Model.UserStatus;

namespace AethernaAI.Manager;

public class UserManager : Registry<User>, IManager
{
  private readonly Core _core;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private string? _groupId;

  private bool IsUserGroupInvited(string userId)
  {
    if (!_vrcManager!.IsLogged)
      return true;

    var invites = _vrcManager!.Groups!.GetGroupInvites(_groupId);
    foreach (var invite in invites)
    {
      if (invite.UserId == userId)
        return true;
    }

    return false;
  }

  public bool IsUserInGroup(string userId)
  {
    if (!_vrcManager!.IsLogged)
      return true;

    var userGroups = _vrcManager!.Users!.GetUserGroups(userId);
    foreach (var group in userGroups)
    {
      if (group.GroupId == _groupId)
        return true;
    }

    return false;
  }

  private void UserJoined(string userId)
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
        $"Użytkownik **{data.DisplayName}** (`{userId}`) dołączył na instancje (ID: *{data.CurrentInstanceId}*)",
        $"Pierwszy raz dołączył w <t:{data.JoinedAt}:F>",
        "",
        $"To jest jego {data.VisitCount} wejście, ostatnio był widziany w <t:{data.LastVisit}:F> (<t:{data.LastVisit}:R>)",
      };

      _ = _discordManager!.SendEmbed("Użytkownicy", String.Join("\n", msg));
    }
  }

  private void UserLeft(string userId)
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
        $"Użytkownik **{data.DisplayName}** (`{userId}`) opuścił instancje (ID: *{data.CurrentInstanceId}*)"
      };

      _ = _discordManager!.SendEmbed("Użytkownicy", String.Join("\n", msg));
    }
  }

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
      }
    });

    if (_core.HasManager<VRCManager>()) _vrcManager = _core.GetManagerOrDefault<VRCManager>();
    if (_core.HasManager<DiscordManager>()) _discordManager = _core.GetManagerOrDefault<DiscordManager>();
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    _vrcManager!.LogReader!.OnProcessed += Process;

    _isInitialized = true;
  }

  private protected void Process(object? sender, ProcessedEventArgs data)
  {
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

  public void Shutdown()
  {
    if (!_isInitialized) return;

    UpdateAll(user =>
    {
      if (user.Status is UserStatus.Online)
      {
        user.Status = UserStatus.Offline;
        user.LastVisit = DateUtil.ToUnixTime(DateTime.Now);
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
}