namespace AethernaAI.Interface;

public interface IGPTModule
{
  Task<string> GenerateResponse(string prompt);
}