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

namespace AethernaAI.Manager;

public class VRCManager : ApiClient, IAsyncManager
{
  #region OSC
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

    return text;
  }

  private async Task UpdateOsc()
  {
    Console.WriteLine("update osc");
    var message = ProcessOscMessage();

    await OSC!.Send(GetOscAddress(VRCOscAddresses.SEND_CHATBOX_MESSAGE), message, true);
  }
  #endregion

  private readonly Core _core;
  private protected GroupsApi _vrcGroups;
  private protected Configuration _vrcConfig;
  private protected AuthenticationApi _vrcAuth;

  private bool RequiresEmail2FA(ApiResponse<CurrentUser> resp)
  {
    if (resp.RawContent.Contains("emailOtp"))
      return true;

    return false;
  }

  private List<string>? _oscMessage;
  private string? _groupId;
  private bool _isLogged;
  private bool _isDisposed;
  private bool _isInitialized;
  private DateTime _lastUpdate = DateTime.MinValue;

  public VRCOsc? OSC { get; private set; }
  public UsersApi? Users { get; private set; }
  public Group? Group { get; private set; }
  public CurrentUser? User { get; private set; }
  public VRCLogReader? LogReader { get; private set; }

  public bool IsLogged => _isLogged;
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
    _vrcGroups = new(this, this, _vrcConfig);

    Users = new(this, this, _vrcConfig);

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
          Group = _vrcGroups.GetGroup(_groupId, true);

          _isLogged = true;

          Logger.Log(LogLevel.Info, $"API is working");
        }
      }
    }
    catch (ApiException e)
    {
      throw e;
    }
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    OSC = new VRCOsc(_core);
    LogReader = new VRCLogReader(_core);

    Login();

    _isInitialized = true;
  }

  public async Task UpdateAsync()
  {
    if (!_isInitialized || _isDisposed)
      return;

    if ((DateTime.UtcNow - _lastUpdate).TotalSeconds >= 5)
    {
      try
      {
        await UpdateOsc();
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Error in VRCManager UpdateAsync: {ex.Message}");
      }

      _lastUpdate = DateTime.UtcNow;
    }
  }

  public void Shutdown()
  {
    if (!_isInitialized) return;

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
