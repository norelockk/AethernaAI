using AethernaAI.Model;

namespace AethernaAI.Event;

public class PlayerJoinedEvent : IEventListener
{
  public string Name { get; } = "PlayerJoined";

  public void Handle(params object?[] args)
  {
    Console.WriteLine($"Player ID {args[0]}");
  }
}
