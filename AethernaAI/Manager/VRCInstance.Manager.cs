using AethernaAI.Util;
using AethernaAI.Model;
using AethernaAI.Module.Internal;

namespace AethernaAI.Manager;

public class VRCInstanceManager : IManager
{
  private readonly Core _core;
  private bool _isDisposed = false;
  private bool _isInitialized = false;

  private UserManager? _userManager;
  private VRCLogReader? _logReader;
  private DiscordManager? _discordManager;
  private readonly Dictionary<int, VRCInstance> _processInstances = new();

  public bool IsInitialized => _isInitialized;

  public VRCInstanceManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    _isInitialized = true;

    if (_core.HasManager<VRCManager>())
      _logReader = _core.GetManagerOrDefault<VRCManager>()!.LogReader;

    if (_core.HasManager<UserManager>())
      _userManager = _core.GetManagerOrDefault<UserManager>();

    if (_core.HasManager<DiscordManager>())
      _discordManager = _core.GetManagerOrDefault<DiscordManager>();

    _logReader!.OnProcessed += OnLogProcessed;
    Logger.Log(LogLevel.Info, "VRCInstanceManager initialized.");
  }

  public void Shutdown()
  {
    if (!_isInitialized) return;

    _isInitialized = false;

    if (_logReader != null)
      _logReader.OnProcessed -= OnLogProcessed;

    Logger.Log(LogLevel.Info, "VRCInstanceManager shutdown.");
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

  ~VRCInstanceManager()
  {
    Dispose(false);
  }

  private void OnLogProcessed(object? sender, ProcessedLogEventArgs e)
  {
    switch (e.Action)
    {
      case "setInstance":
        HandleInstanceChange(e.Data);
        break;
      case "resetInstance":
        HandleResetInstance(e.Data);
        break;
      case "left":
      case "joined":
        HandlePlayerJoinLeave(e.Data);
        break;
      default:
        Logger.Log(LogLevel.Debug, $"Unhandled action: {e.Action}");
        break;
    }
  }

  private void HandleInstanceChange(Dictionary<string, object> eventData)
  {
    string worldId = eventData["WorldId"]?.ToString() ?? "";
    string worldName = eventData["WorldName"]?.ToString() ?? "";
    string worldAccessType = eventData["WorldAccessType"]?.ToString() ?? "";
    string groupId = eventData["GroupId"]?.ToString() ?? "";
    string region = eventData["Region"]?.ToString() ?? "";

    int processId = eventData.ContainsKey("ProcessId") ? Convert.ToInt32(eventData["ProcessId"]) : -1;

    if (processId == -1) return;

    if (!_processInstances.ContainsKey(processId))
    {
      _processInstances[processId] = new VRCInstance
      {
        GroupId = groupId,
        WorldId = worldId,
        ProcessId = processId,
        WorldName = worldName,
        WorldType = worldAccessType,
        WorldRegion = region,
      };

      var info = _processInstances[processId];

      Logger.Log(LogLevel.Info, $"Process {processId} entered {worldAccessType} world {worldName} ({worldId}) in group {groupId}, region {region}.");
      _discordManager!.SendEmbed($"Uruchomiono monitorowanie instancji {info.GetInstanceId()}", $"VRChat ID: {processId}", "#32a852");
    }
    else
    {
      var instance = _processInstances[processId];
      instance.GroupId = groupId;
      instance.WorldId = worldId;
      instance.WorldName = worldName;
      instance.WorldType = worldAccessType;
      instance.WorldRegion = region;

      Logger.Log(LogLevel.Info, $"Process {processId} changed to world {worldName} ({worldId}) in group {groupId}, region {region}.");
      _discordManager!.SendEmbed($"Zmiana instancji do monitorowania na {instance.GetInstanceId()}", $"VRChat ID: {processId}", "#32a852");
    }
  }

  private void HandleResetInstance(Dictionary<string, object> eventData)
  {
    int processId = eventData.ContainsKey("ProcessId") ? Convert.ToInt32(eventData["ProcessId"]) : -1;

    if (processId == -1) return;

    if (_processInstances.ContainsKey(processId))
    {
      var instance = _processInstances[processId];

      instance.GroupId = string.Empty;
      instance.WorldId = string.Empty;
      instance.WorldName = string.Empty;
      instance.WorldType = string.Empty;
      instance.WorldRegion = string.Empty;

      Logger.Log(LogLevel.Info, $"Process {processId} instance reset.");
      _discordManager!.SendEmbed("Resetowanie monitoringu", "", "#32a852");
    }
    else
    {
      Logger.Log(LogLevel.Warn, $"Process {processId} not found for reset.");
    }
  }

  private void HandlePlayerJoinLeave(Dictionary<string, object> eventData)
  {
    string action = eventData["Action"]?.ToString() ?? "";
    string userId = eventData["UserId"]?.ToString() ?? "";
    int processId = eventData.ContainsKey("ProcessId") ? Convert.ToInt32(eventData["ProcessId"]) : -1;

    var processor = GetWorldInstance(processId);
    if (processor is null)
      return;

    if (_userManager!.Has(userId)) _userManager.Update(userId, u => u.CurrentInstanceId = action == "joined" ? processor.GetInstanceId() : null);
  }

  public VRCInstance? GetWorldInstance(int processId)
  {
    return _processInstances.ContainsKey(processId) ? _processInstances[processId] : null;
  }

  public IEnumerable<VRCInstance> GetAllInstances()
  {
    return _processInstances.Values;
  }

  public void RemoveProcessInstance(int processId)
  {
    var processor = GetWorldInstance(processId);
    if (processor is null)
      return;

    _processInstances.Remove(processId);
    Logger.Log(LogLevel.Info, $"Removed process {processId} from world tracking");
    _discordManager!.SendEmbed($"Wyłączanie monitoringu instancji {processor.GetInstanceId()}", $"ID: {processId}", "#32a852");
  }
}