using AethernaAI.Util;
using AethernaAI.Enum;
using AethernaAI.Model;
using AethernaAI.Module.Internal;
using static AethernaAI.Addresses;

namespace AethernaAI.Manager;

public class VRCManager : IAsyncManager
{
  private readonly Core _core;

  private List<string>? _oscMessage;
  private bool _isInitialized;
  private bool _isDisposed;
  private DateTime _lastUpdate = DateTime.MinValue;

  public VRCOsc? OSC { get; private set; }
  public VRCLogReader? LogReader { get; private set; }

  public bool IsInitialized => _isInitialized;

  public VRCManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
    _oscMessage = _core.Config.GetConfig<List<string>?>(c => c.VrchatOscMessage!);
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    OSC = new VRCOsc(_core);
    LogReader = new VRCLogReader(_core);

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

  #region OSC management
  private string GetOscMessage()
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
    var message = GetOscMessage();
    var address = GetOscAddress(VRCOscAddresses.SEND_CHATBOX_MESSAGE);

    await OSC!.Send(address, message, true);
  }
  #endregion

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
