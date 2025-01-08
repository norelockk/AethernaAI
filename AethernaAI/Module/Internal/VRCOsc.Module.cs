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
  private readonly List<OscConnection> _connections = new();
  private readonly List<OscQueryServer> _oscQueryServers = new();

  public VRCOsc(Core core)
  {
    _core = core;

    var server = new OscQueryServer("Aetherna", IPAddress.Loopback);
    server.FoundVrcClient += HandleNewVrcClient;

    _oscQueryServers.Add(server);
    server.Start();
  }

  private Task HandleNewVrcClient(OscQueryServer oscQueryServer, IPEndPoint ipEndPoint)
  {
    var existingConnection = _connections.FirstOrDefault(conn => conn.Endpoint.Equals(ipEndPoint));

    if (existingConnection != null)
    {
      Logger.Log(LogLevel.Debug, $"VRC Client at {ipEndPoint} already connected.");
      return Task.CompletedTask;
    }

    Logger.Log(LogLevel.Debug, $"Connecting to VRC Client at {ipEndPoint}");

    var newConnection = new OscConnection(_core!, oscQueryServer, ipEndPoint);
    _connections.Add(newConnection);
    newConnection.StartReceiverLoop();

    return Task.CompletedTask;
  }

  public async Task Send(string address, params object[] arguments)
  {
    foreach (var connection in _connections)
    {
      await connection.Send(address, arguments);
    }
  }

  private class OscConnection
  {
    private readonly Core _core;
    private readonly OscDuplex _gameConnection;
    private readonly CancellationTokenSource _loopCancellationToken = new();
    public IPEndPoint Endpoint { get; }

    public OscConnection(Core core, OscQueryServer oscQueryServer, IPEndPoint ipEndPoint)
    {
      _core = core;
      _gameConnection = new OscDuplex(new IPEndPoint(ipEndPoint.Address, oscQueryServer.OscReceivePort), ipEndPoint);
      Endpoint = ipEndPoint;
    }

    public void StartReceiverLoop()
    {
      ErrorHandledTask.Run(ReceiverLoopAsync);
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
          Logger.Log(LogLevel.Error, $"Loop receiver error for {Endpoint}: {e.Message}");
        }
      }
    }

    private async Task ReceiveLogic()
    {
      OscMessage received;

      try
      {
        received = await _gameConnection.ReceiveMessageAsync();
      }
      catch (Exception e)
      {
        Logger.Log(LogLevel.Error, $"Error receiving message from {Endpoint}: {e.Message}");
        return;
      }

      switch (received.Address)
      {
        case "/avatar/parameters/MuteSelf":
          _core.Bus.Emit("PlayerMuted", (bool)received.Arguments[0]!);
          break;
        default:
          // Logger.Log(LogLevel.Debug, $"Received unknown message from {Endpoint}: {received.Address}");
          break;
      }
    }

    public async Task Send(string address, params object[] arguments)
    {
      arguments ??= Array.Empty<object>();
      await _gameConnection.SendAsync(new OscMessage(address, arguments));
    }

    public void Dispose()
    {
      _loopCancellationToken.Cancel();
      _gameConnection.Dispose();
    }
  }
}
