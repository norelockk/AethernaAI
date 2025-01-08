using VRChat.API.Api;
using VRChat.API.Model;
using VRChat.API.Client;
using AethernaAI.Util;
using AethernaAI.Enum;
using AethernaAI.Model;
using AethernaAI.Dialogs;
using AethernaAI.Module.Internal;
using static AethernaAI.Addresses;
using static AethernaAI.Constants;
using Logger = AethernaAI.Util.Logger;

namespace AethernaAI.Manager;

public class VRCManager : ApiClient, IAsyncManager
{
  #region OSC
  public static string ReplaceFirst<T>(string input, string search, T replacement)
  {
    int index = input.IndexOf(search);
    if (index < 0)
      return input;

    return input.Substring(0, index) + replacement + input.Substring(index + search.Length);
  }

  private string ProcessOscMessage()
  {
    string text = string.Empty;
    foreach (string line in _oscMessage!)
    {
      if (string.IsNullOrEmpty(line))
      {
        text += "\n";
        continue;
      }

      text += line;
    }

    // TODO: automatic placeholder replacer
    text = ReplaceFirst(text, "{online}", _groupUsers);
    text = ReplaceFirst(text, "{maxpi}", _groupMaxUsers);
    // text = ReplaceFirst()

    return text;
  }

  private async Task UpdateOsc()
  {
    var message = ProcessOscMessage();

    await OSC!.Send(GetOscAddress(VRCOscAddresses.SEND_CHATBOX_MESSAGE), message, true);
  }
  #endregion

  private readonly Core _core;
  private protected InstancesApi _vrcInstances;
  private protected Configuration _vrcConfig;
  private protected AuthenticationApi _vrcAuth;

  private bool RequiresEmail2FA(ApiResponse<CurrentUser> resp)
  {
    if (resp.RawContent.Contains("emailOtp"))
      return true;

    return false;
  }

  private List<string>? _oscMessage;
  private string? _worldId;
  private string? _groupId;
  private bool _isLogged;
  private bool _isDisposed;
  private int _groupUsers;
  private int _groupMaxUsers;

  private bool _isInitialized;
  private DateTime _lastOSCUpdate = DateTime.MinValue;
  private DateTime _lastInfoUpdate = DateTime.MinValue;

  public VRCOsc? OSC { get; private set; }
  public UsersApi? Users { get; private set; }
  public GroupsApi? Groups { get; private set; }
  public Group? Group { get; private set; }

  public CurrentUser? User { get; private set; }
  public VRCLogReader? LogReader { get; private set; }

  public bool IsLogged => _isLogged;
  public int GroupUsers => _groupUsers;
  public int GroupMaxUsers => _groupMaxUsers;
  public bool IsInitialized => _isInitialized;

  public VRCManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));

    var (username, password) = (
      _core.Config.GetConfig<string?>(c => c.VrchatUsername!) ?? throw new NullReferenceException("username null"),
      _core.Config.GetConfig<string?>(c => c.VrchatPassword!) ?? throw new NullReferenceException("password null")
    );

    _vrcConfig = new()
    {
      Username = username,
      Password = password,
      UserAgent = $"Eqipa/{VERSION} norelock"
    };
    _vrcAuth = new(this, this, _vrcConfig);
    _vrcInstances = new(this, this, _vrcConfig);

    Users = new(this, this, _vrcConfig);
    Groups = new(this, this, _vrcConfig);

    _worldId = _core.Config.GetConfig<string?>(c => c.VrchatWorldId!);
    _groupId = _core.Config.GetConfig<string?>(c => c.VrchatGroupId!);
    _oscMessage = _core.Config.GetConfig<List<string>?>(c => c.VrchatOscMessage!);
  }

  private void Login()
  {
    try
    {
      var u = _vrcAuth.GetCurrentUserWithHttpInfo();
      var d = new TwoStepDialog(RequiresEmail2FA(u));

      if (d.ShowDialog() == DialogResult.OK)
      {
        var code = d.Controls.OfType<TextBox>().First().Text.Trim();
        if (code == TwoStepDialog._ENTER_CODE || string.IsNullOrWhiteSpace(code))
        {
          Logger.Log(LogLevel.Warn, "Verification code not provided, disabling API..");
          return;
        }

        Logger.Log(LogLevel.Step, $"Verification code ({code}) has been provided, verifying and enabling API..");

        dynamic result = RequiresEmail2FA(u)
          ? _vrcAuth.Verify2FAEmailCode(new TwoFactorEmailCode(code))
          : _vrcAuth.Verify2FA(new TwoFactorAuthCode(code));

        if (result.Verified && !_isLogged)
        {
          User = _vrcAuth.GetCurrentUser();
          Group = Groups!.GetGroup(_groupId, true);
          UpdateInstanceUsers();

          _isLogged = true;

          Logger.Log(LogLevel.Info, $"API is working");
        }
      }
    }
    catch (ApiException e)
    {
      Logger.Log(LogLevel.Error, $"API login error: {e.Message}");
    }
  }

  private void UpdateInstanceUsers()
  {
    var now = DateUtil.ToUnixTime(DateTime.Now);
    var instances = Groups!.GetGroupInstances(_groupId);

    _groupMaxUsers = instances.Sum(i => i.World.Capacity);

    // TODO: optimized way
    _groupUsers = 0;

    foreach (var groupInstance in instances!)
    {
      try
      {
        var instance = _vrcInstances.GetInstance(_worldId, groupInstance.InstanceId);

        if (instance.Users is null)
        {
          // _groupUsers += instance.Platforms.Android + instance.Platforms.Ios + instance.Platforms.Standalonewindows;
          _groupUsers = _core.Registry.Users.GetCountByCondition(user => user.Status == Model.UserStatus.Online);
        }
        else
        {
          foreach (var user in instance.Users)
          {
            bool exists = _core.Registry.Users.Has(user.Id);

            if (!exists)
              _core.Registry.Users.Save(user.Id, new()
              {
                Id = user.Id,
                Status = Model.UserStatus.Online,
                JoinedAt = now,
                LastVisit = now,
                VisitCount = 1,
                DisplayName = user.DisplayName,
              });
            else
              _core.Registry.Users.Update(user.Id, user =>
              {
                if (user.Status is not Model.UserStatus.Online)
                  user.Status = Model.UserStatus.Online;
              });
          }
        }
      }
      catch (Exception e)
      {
        Logger.Log(LogLevel.Error, $"Problem with updating instance users: {e.Message}");
      }
    }

    Console.WriteLine($"{_groupUsers}/{_groupMaxUsers}");
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    // crash or smth = also status to offline
    _core.Registry.Users.UpdateAll(user =>
    {
      if (user.Status is Model.UserStatus.Online)
      {
        user.Status = Model.UserStatus.Offline;
        user.LastVisit = DateUtil.ToUnixTime(DateTime.Now);
      }
    });

    Login();

    OSC = new VRCOsc(_core);
    LogReader = new VRCLogReader(_core);

    _isInitialized = true;
  }

  public async Task UpdateAsync()
  {
    if (!_isInitialized || _isDisposed)
      return;

    if ((DateTime.UtcNow - _lastOSCUpdate).TotalSeconds >= 5)
    {
      try
      {
        await UpdateOsc();
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Error in VRCManager UpdateAsync: {ex.Message}");
      }

      _lastOSCUpdate = DateTime.UtcNow;
    }

    if ((DateTime.UtcNow - _lastInfoUpdate).TotalSeconds >= 180)
    {
      try
      {
        UpdateInstanceUsers();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.StackTrace);
        // throw;
      }

      _lastInfoUpdate = DateTime.UtcNow;
    }
  }

  public void Shutdown()
  {
    if (!_isInitialized) return;

    // make all users offline properly
    _core.Registry.Users.UpdateAll(user =>
    {
      if (user.Status is Model.UserStatus.Online)
      {
        user.Status = Model.UserStatus.Offline;
        user.LastVisit = DateUtil.ToUnixTime(DateTime.Now);
      }
    });

    _isInitialized = false;
    Logger.Log(LogLevel.Info, "VRCManager shutdown");
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

  ~VRCManager()
  {
    Dispose(false);
  }
}
