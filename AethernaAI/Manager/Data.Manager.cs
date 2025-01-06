using AethernaAI.Util;
using AethernaAI.Enum;
using AethernaAI.Model;
using AethernaAI.Module.Internal;
using VRChat.API.Model;
using UserStatus = AethernaAI.Model.UserStatus;
using VRChat.API.Client;
using Swan;

namespace AethernaAI.Manager;

public class DataManager : IManager
{
  private readonly Core _core;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private string? _groupId;

  private bool IsUserInGroup(string userId)
  {
    if (!_vrcManager!.IsLogged) // prevent spamming when not enabled api
      return true;

    var userGroups = _vrcManager!.Users!.GetUserGroups(userId);
    Console.WriteLine(userGroups.ToString());
    foreach (var group in userGroups)
    {
      if (group.Id == _groupId)
        return true;
    }

    return false;
  }

  private void Process(object? sender, ProcessedEventArgs data)
  {
    var now = DateUtil.ToUnixTime(DateTime.Now);
    bool exists = _core.Registry.Users.Has(data.UserId);

    switch (data.Action)
    {
      case "joined":
        {
          var user = _vrcManager!.Users!.GetUser(data.UserId);
          if (user is null)
            return;

          if (!IsUserInGroup(data.UserId))
          {
            var groupInvite = new CreateGroupInviteRequest(data.UserId);

            try
            {
              _vrcManager!.Groups!.CreateGroupInvite(_groupId, groupInvite);
            }
            catch (ApiException e)
            {
              Logger.Log(LogLevel.Error, $"Cannot invite user: {e.Message}");
            }
          }

          if (!exists)
            _core.Registry.Users.Save(data.UserId, new()
            {
              Id = data.UserId,
              Status = UserStatus.Online,
              JoinedAt = now,
              LastVisit = now,
              DisplayName = user.DisplayName
            });
          else
            _core.Registry.Users.Update(data.UserId, user =>
            {
              if (user.Status is not UserStatus.Online)
                user.Status = UserStatus.Online;

              user.LastVisit = now;
            });

          break;
        }

      case "left":
        {
          if (exists)
            _core.Registry.Users.Update(data.UserId, user =>
            {
              if (user.Status is not UserStatus.Offline)
                user.Status = UserStatus.Offline;

              user.LastVisit = now;
            });
          else
          {
            var user = _vrcManager!.Users!.GetUser(data.UserId);
            if (user is null)
              return;

            _core.Registry.Users.Save(data.UserId, new()
            {
              Id = data.UserId,
              JoinedAt = now,
              LastVisit = now,
              DisplayName = user.DisplayName
            });
          }

          break;
        }
    }
  }

  private VRCManager? _vrcManager;

  public bool IsInitialized => _isInitialized;

  public DataManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));

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
    Logger.Log(LogLevel.Info, "DataManager shutdown");
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

  ~DataManager()
  {
    Dispose(false);
  }
}