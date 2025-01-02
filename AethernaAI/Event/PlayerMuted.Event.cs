// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

namespace AethernaAI.Event;

using AethernaAI.Interface;

public class PlayerMutedEvent : IEventListener
{
  public string Name { get; } = "PlayerMuted";

  public void Handle(params object?[] args)
  {
    Console.WriteLine($"Player muted {args[0]}");
  }
}