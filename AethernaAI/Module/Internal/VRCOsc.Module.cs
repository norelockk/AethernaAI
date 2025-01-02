// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

using System.Net;
using OscQueryLibrary;
using LucHeart.CoreOSC;
using OscQueryLibrary.Utils;
using AethernaAI.Util;
using AethernaAI.Enum;

namespace AethernaAI.Module.Internal;

public class VRCOsc
{
  private Core? _core;
  private OscDuplex? _gameConnection = null;
  private List<OscQueryServer> _oscQueryServers = new();
  private OscQueryServer? _currentOscQueryServer = null;
  private CancellationTokenSource _loopCancellationToken = new();

  public VRCOsc(Core core)
  {
    _core = core;

    var server = new OscQueryServer("Aetherna", IPAddress.Loopback);
    server.FoundVrcClient += FindVrcClient;

    _oscQueryServers.Add(server);
    server.Start();
  }

  private Task FindVrcClient(OscQueryServer oscQueryServer, IPEndPoint ipEndPoint)
  {
    _loopCancellationToken.Cancel();
    _loopCancellationToken = new CancellationTokenSource();
    _gameConnection?.Dispose();
    _gameConnection = null;

    Logger.Log(LogLevel.Debug, $"Connected to VRC Client at {ipEndPoint}");

    _gameConnection = new OscDuplex(new IPEndPoint(ipEndPoint.Address, oscQueryServer.OscReceivePort), ipEndPoint);
    _currentOscQueryServer = oscQueryServer;

    ErrorHandledTask.Run(ReceiverLoopAsync);
    return Task.CompletedTask;
  }

  private async Task ReceiverLoopAsync()
  {
    var currentCancellationToken = _loopCancellationToken.Token;

    while (!currentCancellationToken.IsCancellationRequested)
    {
      try
      {
        await ReceiveLogic();
      }
      catch (Exception e)
      {
        Logger.Log(LogLevel.Error, $"Loop receiver error: {e.Message}");
      }
    }
  }

  // public event Action<bool>? GotMuted;

  private async Task ReceiveLogic()
  {
    if (_gameConnection is null) return;

    OscMessage received;

    try
    {
      received = await _gameConnection.ReceiveMessageAsync();
    }
    catch (Exception e)
    {
      Logger.Log(LogLevel.Error, $"Error receiving message: {e.Message}");
      return;
    }

    // Console.WriteLine($"Received message: {received.Address}, {string.Join(", ", received.Arguments)}");
    switch (received.Address)
    {
      case "/avatar/parameters/MuteSelf":
        _core!.Bus.Emit("PlayerMuted", (bool)received.Arguments[0]!);
        break;
      default:
        // Logger.Log(LogLevel.Debug, $"Received unknown message: {received.Address}");
        break;
    }
  }
  public async Task Send(string address, params object[] arguments)
  {
    if (_gameConnection is null)
      return;

    arguments ??= Array.Empty<object>();

    await _gameConnection.SendAsync(new OscMessage(address, arguments));
  }
}