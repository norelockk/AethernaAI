// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

using AethernaAI.Model;

namespace AethernaAI.Event;

public class PlayerMutedEvent : IEventListener
{
  public string Name { get; } = "PlayerMuted";

  public void Handle(params object?[] args)
  {
    Console.WriteLine($"Player muted {args[0]}");
  }
}