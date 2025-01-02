namespace AethernaAI.Model;

public interface IManager : IDisposable
{
  bool IsInitialized { get; }
  void Initialize();
  void Shutdown();
}

public class ManagerNotFoundException : Exception
{
  public ManagerNotFoundException(Type type)
      : base($"Manager of type {type.Name} not found") { }
}

public class ManagerAlreadyInitializedException : Exception
{
  public ManagerAlreadyInitializedException(Type type)
      : base($"Manager of type {type.Name} is already initialized") { }
}