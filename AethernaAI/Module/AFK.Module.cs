using AethernaAI.Enum;
using static AethernaAI.Addresses;

namespace AethernaAI.Module;

public class AFKModule : IDisposable
{
  public AFKModule(Core core)
  {
    if (core is null)
      throw new ArgumentNullException(nameof(core));

    _core = core;
    _startTime = DateTime.Now;
    _currentTime = _startTime;
    StartTimeTracking();
  }

  private readonly Core _core;
  private readonly DateTime _startTime;
  private DateTime _currentTime;
  private System.Threading.Timer? _timer;
  private bool _isDisposed;
  private bool _isMessageSent = false;

  public TimeSpan Uptime => _currentTime - _startTime;

  private void StartTimeTracking()
  {
    Console.WriteLine("start track");
    _timer = new System.Threading.Timer(UpdateTime, null, 0, 5000); // Update every second
  }

  private void UpdateTime(object? state)
  {
    Console.WriteLine("updatwe");


    _currentTime = DateTime.Now;


    var oscAddress = GetOscAddress(VRCOscAddresses.SEND_CHATBOX_MESSAGE);
    Console.WriteLine($"Sending message to OSC address: {oscAddress}");

    try
    {
      _core.OSC!.Send(oscAddress, $"I went eepy (eepy since {FormatUptime(Uptime)})\nbrb.", true);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Failed to send OSC message: {ex.Message}");
    }
  }

  private string FormatUptime(TimeSpan uptime)
  {
    return $"{uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_isDisposed)
    {
      if (disposing)
      {
        _timer?.Dispose();
      }
      _isDisposed = true;
    }
  }

  ~AFKModule()
  {
    Dispose(false);
  }
}