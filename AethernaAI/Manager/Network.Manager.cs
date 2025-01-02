using AethernaAI.Enum;
using AethernaAI.Model;
using AethernaAI.Module;
using AethernaAI.Module.Internal;
using AethernaAI.Util;

namespace AethernaAI.Manager;

public class NetworkManager : IManager
{
  private readonly Core _core;
  private bool _isInitialized;
  private bool _isDisposed;

  public VRCOsc? OSC { get; private set; }
  public GPTModule? GPT { get; private set; }
  public SpeechModule? Speech { get; private set; }

  public bool IsInitialized => _isInitialized;

  public NetworkManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
  }

  public async void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    GPT = new GPTModule(_core);
    OSC = new VRCOsc(_core);
    Speech = new SpeechModule(_core);
    await Speech.StartListeningAsync();

    _isInitialized = true;
    Logger.Log(LogLevel.Info, "Network manager initialized");
  }

  public void Shutdown()
  {
    if (!_isInitialized) return;

    GPT?.Dispose();
    Speech?.Dispose();

    _isInitialized = false;
    Logger.Log(LogLevel.Info, "Network manager shutdown");
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

  ~NetworkManager()
  {
    Dispose(false);
  }
}