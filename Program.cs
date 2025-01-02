using AethernaAI.Enum;
using AethernaAI.Util;

namespace AethernaAI;

internal class Program
{
  private static Core? _core;
  private static bool _isShuttingDown;

  static async Task Main(string[] args)
  {
    AppDomain.CurrentDomain.ProcessExit += OnProcessExit!;
    Console.CancelKeyPress += OnCancelKeyPress!;

    try
    {
      await InitializeAsync();
      await RunMainLoopAsync();
    }
    catch (Exception ex)
    {
      Logger.Log(LogLevel.Error, $"Unhandled exception in main loop: {ex}");
      Environment.ExitCode = 1;
    }
    finally
    {
      await ShutdownAsync();
    }
  }

  private static async Task InitializeAsync()
  {
    try
    {
      _core = Core.Instance;
    }
    catch (Exception ex)
    {
      Logger.Log(LogLevel.Error, $"Failed to initialize application: {ex}");
      throw;
    }
  }

  private static async Task RunMainLoopAsync()
  {
    Logger.Log(LogLevel.Step, "Application started");

    while (!_isShuttingDown)
    {
      try
      {

        await Task.Delay(100);
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Error in main loop: {ex}");
        if (ex is ObjectDisposedException)
        {
          _isShuttingDown = true;
        }
      }
    }
  }

  private static async Task ShutdownAsync()
  {
    if (_isShuttingDown)
    {
      return;
    }

    _isShuttingDown = true;

    try
    {
      // Dispose Core and its managers
      if (_core != null)
      {
        _core.Dispose();
        _core = null;
      }
    }
    catch (Exception ex)
    {
      Logger.Log(LogLevel.Error, $"Error during shutdown: {ex}");
      Environment.ExitCode = 1;
    }
  }

  private static void OnProcessExit(object sender, EventArgs e)
  {
    ShutdownAsync().Wait();
  }

  private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
  {
    e.Cancel = true;
    _isShuttingDown = true;
  }
}