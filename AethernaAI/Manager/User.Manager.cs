using Swan;
using AethernaAI.Util;
using AethernaAI.Enum;
using AethernaAI.Model;
using AethernaAI.Module.Internal;
using VRChat.API.Model;
using VRChat.API.Client;
using AethernaAI.Service;
using User = AethernaAI.Model.User;
using UserStatus = AethernaAI.Model.UserStatus;

namespace AethernaAI.Manager;

public class UserManager : IManager
{
  private readonly Core _core;
  private readonly Registry<User> _registry;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private string? _groupId;

  private bool IsUserInGroup(string userId)
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
    var now = DateUtil.ToUnixTime(DateTime.Now);
    var user = _vrcManager!.Users!.GetUser(userId);
    if (user is null)
    {
      Logger.Log(LogLevel.Warn, $"API cannot get user {userId} data");
      return;
    }

    if (!IsUserInGroup(userId))
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

    if (!_registry.Has(userId))
    {
      _registry.Save(userId, new()
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
      _registry.Update(userId, data =>
      {
        if (data.Status is not UserStatus.Online)
          data.Status = UserStatus.Online;

        data.VisitCount++;
      });
    }
  }

  private void UserLeft(string userId)
  {
    var now = DateUtil.ToUnixTime(DateTime.Now);

    if (!_registry.Has(userId))
    {
      var user = _vrcManager!.Users!.GetUser(userId);
      if (user is null)
      {
        Logger.Log(LogLevel.Warn, $"API cannot get user {userId} data");
        return;
      }

      _registry.Save(userId, new()
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
      _registry.Update(userId, data =>
      {
        if (data.Status is not UserStatus.Offline)
          data.Status = UserStatus.Offline;

        data.LastVisit = now;
      });
    }
  }

  private void Process(object? sender, ProcessedEventArgs data)
  {
    switch (data.Action)
    {
      case "joined":
        {
          UserJoined(data.UserId);
          break;
        }

      case "left":
        {
          UserLeft(data.UserId);
          break;
        }
    }
  }

  private VRCManager? _vrcManager;

  public bool IsInitialized => _isInitialized;

  public UserManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
    _registry = _core.Registry.Users;

    _groupId = _core.Config.GetConfig<string?>(c => c.VrchatGroupId!);

    if (_core.HasManager<VRCManager>())
      _vrcManager = _core.GetManagerOrDefault<VRCManager>();
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