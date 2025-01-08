using AethernaAI.Util;
using AethernaAI.Model;
using System.Net.Http.Json;

namespace AethernaAI.Manager;

public class DiscordManager : IManager
{
  private readonly Core _core;

  private bool _isInitialized = false;
  private bool _isDisposed = false;

  private HttpClient? _httpClient = null;
  private string? _webhookUrl = null;

  public bool IsInitialized => _isInitialized;

  public DiscordManager(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
    _webhookUrl = _core.Config.GetConfig<string?>(c => c.DiscordWebhookUrl!);
  }

  public void Initialize()
  {
    if (_isInitialized)
      throw new ManagerAlreadyInitializedException(GetType());

    if (string.IsNullOrEmpty(_webhookUrl))
      throw new InvalidOperationException("Webhook URL must be set before initialization.");

    _httpClient = new HttpClient();
    _isInitialized = true;
  }

  public async Task SendEmbed(string title, string description, string? colorHex = "#5865F2")
  {
    if (!_isInitialized)
      throw new InvalidOperationException("DiscordManager is not initialized.");

    if (string.IsNullOrEmpty(_webhookUrl))
      throw new InvalidOperationException("Webhook URL is not set.");

    var embed = new
    {
      embeds = new[]
        {
          new
          {
            title = title,
            color = ConvertHexToDecimalColor(colorHex),
            timestamp = DateTime.UtcNow.ToString("o"),
            description = description,
          }
        }
    };

    var response = await _httpClient!.PostAsJsonAsync(_webhookUrl, embed);
    if (!response.IsSuccessStatusCode)
    {
      Logger.Log(LogLevel.Error, $"Failed to send embed to Discord: {response.StatusCode}");
    }
  }

  private int ConvertHexToDecimalColor(string? colorHex)
  {
    if (string.IsNullOrEmpty(colorHex) || !colorHex.StartsWith("#")) return 0x5865F2; // Default Discord blue
    return int.Parse(colorHex.Substring(1), System.Globalization.NumberStyles.HexNumber);
  }

  public void Shutdown()
  {
    if (!_isInitialized) return;

    _httpClient?.Dispose();
    _httpClient = null;

    _isInitialized = false;
    Logger.Log(LogLevel.Info, "DiscordManager shutdown");
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_isDisposed) return;

    if (disposing)
    {
      Shutdown();
    }

    _isDisposed = true;
  }

  ~DiscordManager()
  {
    Dispose(false);
  }
}
