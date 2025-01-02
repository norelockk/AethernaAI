namespace AethernaAI.Model;

public interface IEventListener
{
  string Name { get; }
  void Handle(params object?[] args);
}