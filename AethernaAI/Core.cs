using AethernaAI.Util;
using AethernaAI.Enum;
using AethernaAI.Event;
using AethernaAI.Module;
using AethernaAI.Service;
using AethernaAI.Interface;
using AethernaAI.Module.Internal;

namespace AethernaAI;

public class Core : Singleton<Core>
{

  public Core()
  {
    Logger.Log(LogLevel.Info, "Core constructed");
    Initialize();
  }

  public bool             Initialized { get; private set; } = false;

  public VRCOsc?          OSC { get; private set; }
  public GPTModule?       GPT { get; private set; }
  public SpeechModule?    Speech { get; private set; }
  public AnticrashModule? AC { get; private set; }

  // Non-essential modules
  public EventEmitter     Bus { get; private set; } = new();
  public ConfigService    Config { get; private set; } = new();
  public RegistryService  Registry { get; private set; } = new();

  private VRCLogReader? LogReader;
  private List<IEventListener> _eventListeners = new()
  {
    new PlayerMutedEvent(),
  };
  
  private async void Initialize()
  {
    if (Initialized)
      return;

    foreach (var listener in _eventListeners)
    {
      Bus.On(listener.Name, listener);
      Logger.Log(LogLevel.Info, $"Event listener '{listener.Name}' initialized");
    }

    // More-important modules
    // AC = new(this);
    // GPT = new(this);
    // Speech = new(this);
    // await Speech.StartMyContinuousRecognitionAsync();

    // var dev = new HttpService("https://avtr.just-h.party");
    // var r = await dev.Exec(HttpMethod.Get, "vrcx_search.php?search=ukon&n=100");
    // Console.WriteLine(r.ToString());

    // Less-important modules
    OSC = new(this);
    new AFKModule(this);
    LogReader = new(this);
    
    Initialized = true;
  }
}
