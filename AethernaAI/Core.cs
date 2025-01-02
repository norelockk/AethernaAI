using AethernaAI.Enum;
using AethernaAI.Manager;
using AethernaAI.Model;
using AethernaAI.Service;
using AethernaAI.Util;

namespace AethernaAI;

public class Core : Singleton<Core>, IDisposable
{
  public Core()
  {
    _managers = new Dictionary<Type, IManager>();
    _isInitialized = false;
    _isDisposed = false;

    Initialize();
  }

  public readonly EventEmitter Bus = new();
  public readonly ConfigService Config = new();

  private readonly Dictionary<Type, IManager> _managers;
  private List<IManager>? _preinitManagers;
  private bool _isInitialized;
  private bool _isDisposed;

  public bool IsInitialized => _isInitialized;

  private void Initialize()
  {
    if (_isInitialized)
      return;

    _preinitManagers = new()
    {
      new NetworkManager(this)
    };

    foreach (var preinitManager in _preinitManagers)
      RegisterManager(preinitManager);

    _isInitialized = true;
    InitializeManagers();
    Logger.Log(LogLevel.Info, "Initialized");
  }

  public void RegisterManager<T>(T manager) where T : class, IManager
  {
    ThrowIfDisposed();
    var type = typeof(T);

    if (_managers.ContainsKey(type)) return;
    if (manager.IsInitialized) return;

    _managers[type] = manager;
  }

  public T GetManager<T>() where T : class, IManager
  {
    ThrowIfDisposed();
    var type = typeof(T);

    if (_managers.TryGetValue(type, out var manager))
      return (T)manager;

    throw new ManagerNotFoundException(type);
  }

  public T? GetManagerOrDefault<T>() where T : class, IManager
  {
    try
    {
      return GetManager<T>();
    }
    catch (ManagerNotFoundException)
    {
      return null;
    }
  }

  private void InitializeManagers()
  {
    ThrowIfDisposed();

    foreach (var manager in _managers.Values)
    {
      try
      {
        if (manager.IsInitialized)
          throw new ManagerAlreadyInitializedException(manager.GetType());

        manager.Initialize();
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Failed to initialize manager {manager.GetType().Name}: {ex}");
      }
    }
  }

  private void ShutdownManagers()
  {
    if (!_isInitialized || _isDisposed)
      return;

    foreach (var manager in _managers.Values.Reverse())
    {
      try
      {
        if (manager.IsInitialized)
        {
          manager.Shutdown();
        }
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Failed to shutdown manager {manager.GetType().Name}: {ex}");
      }
    }
  }

  public bool HasManager<T>() where T : class, IManager
  {
    ThrowIfDisposed();
    return _managers.ContainsKey(typeof(T));
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_isDisposed)
      return;

    if (disposing)
    {
      ShutdownManagers();

      foreach (var manager in _managers.Values)
      {
        try
        {
          manager.Dispose();
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Failed to dispose manager {manager.GetType().Name}: {ex}");
        }
      }

      _managers.Clear();
    }

    _isDisposed = true;
    _isInitialized = false;
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  private void ThrowIfDisposed()
  {
    if (_isDisposed)
    {
      throw new ObjectDisposedException(nameof(Core));
    }
  }

  ~Core()
  {
    Dispose(false);
  }
}