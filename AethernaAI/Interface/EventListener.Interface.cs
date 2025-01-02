// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

namespace AethernaAI.Interface;

public interface IEventListener
{
  string Name { get; }
  void Handle(params object?[] args);
}