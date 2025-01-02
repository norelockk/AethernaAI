using AethernaAI;
using AethernaAI.Util;

internal class Program
{
  public static Core? Core;

  private static void CollectGarbage()
  {
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
  }

  static void Main(string[]? args)
  {
    Core = Core.I;
    Debugger.SetupExceptionHandling();

    while (true)
    {
      CollectGarbage();
      Thread.Sleep(500);
    }
  }
}