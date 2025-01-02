using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using AethernaAI.Enum;
using AethernaAI.Util;
using NAudio.CoreAudioApi;
using System.Collections.Concurrent;

namespace AethernaAI.Module;

public class SpeechModule : IDisposable
{
  private readonly Core _core;
  private SpeechRecognizer? _recognizer;
  private SpeechConfig? _config;
  private AudioConfig? _audioConfig;
  public RecognizeLang _currentLanguage;
  private readonly ConcurrentQueue<string> _recognitionQueue = new();
  private readonly CancellationTokenSource _cancellationTokenSource = new();
  private readonly System.Timers.Timer _healthCheckTimer;
  private readonly object _reconnectLock = new();
  private bool _isDisposed;
  private bool _needsReconnect;
  private DateTime _lastRecognitionTime = DateTime.Now;
  private int _reconnectAttempts;
  private const int MAX_RECONNECT_ATTEMPTS = 5;
  private const int RECONNECT_DELAY_MS = 5000;
  private const int HEALTH_CHECK_INTERVAL_MS = 30000; // 30 sekund
  private const int RECOGNITION_TIMEOUT_MS = 60000;   // 1 minuta

  public bool IsListening { get; private set; }
  public event EventHandler<string>? OnSpeechRecognized;
  public event EventHandler<ConnectionStatus>? OnConnectionStatusChanged;

  public enum ConnectionStatus
  {
    Connected,
    Disconnected,
    Reconnecting,
    Failed
  }

  public SpeechModule(Core core)
  {
    _core = core ?? throw new ArgumentNullException(nameof(core));
    _healthCheckTimer = new System.Timers.Timer(HEALTH_CHECK_INTERVAL_MS);
    _healthCheckTimer.Elapsed += async (s, e) => await CheckConnectionHealthAsync();
    Initialize();
    StartHealthCheck();
    Logger.Log(LogLevel.Info, "Speech module initialized");
  }

  private void Initialize()
  {
    try
    {
      _currentLanguage = _core.Config.GetConfig<RecognizeLang>(config => config.SpeechRecognizerLanguage);
      var token = _core.Config.GetConfig<string?>(config => config.SpeechRecognizerToken!);
      var region = _core.Config.GetConfig<string?>(config => config.SpeechRecognizerRegion!);

      if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(region))
        throw new ArgumentNullException("Speech configuration is missing");

      InitializeSpeechComponents(token, region);
      SetupRecognizer();
    }
    catch (Exception ex)
    {
      Logger.Log(LogLevel.Error, $"Failed to initialize Robust Speech module: {ex.Message}");
      throw;
    }
  }

  private void InitializeSpeechComponents(string token, string region)
  {
    _config = SpeechConfig.FromSubscription(token, region);
    _config.SetProfanity(ProfanityOption.Raw);
    _config.SpeechRecognitionLanguage = GetLanguageCode(_currentLanguage);

    // Ustawienia dla lepszej stabilności
    _config.SetProperty("SpeechServiceConnection_KeepAlive", "true");
    _config.SetProperty("SpeechServiceConnection_InitialSilenceTimeoutMs", "5000");
    _config.SetProperty("SpeechServiceConnection_EndSilenceTimeoutMs", "5000");

    _audioConfig = GetAudioConfig();
  }

  private void SetupRecognizer()
  {
    _recognizer?.Dispose();
    _recognizer = new SpeechRecognizer(_config, _audioConfig);

    _recognizer.Recognized += OnRecognized;
    _recognizer.Canceled += OnCanceled;
    _recognizer.SessionStarted += (s, e) =>
    {
      _reconnectAttempts = 0;
      OnConnectionStatusChanged?.Invoke(this, ConnectionStatus.Connected);
      Logger.Log(LogLevel.Info, "Speech recognition session started");
    };
    _recognizer.SessionStopped += (s, e) =>
    {
      if (!_isDisposed && IsListening)
      {
        _needsReconnect = true;
      }
    };
  }

  private AudioConfig GetAudioConfig()
  {
    var enumerator = new MMDeviceEnumerator();
    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
    var targetDevice = devices.FirstOrDefault(d =>
        d.FriendlyName.Contains("Virtual Cable", StringComparison.OrdinalIgnoreCase));

    // return targetDevice != null
    //     ? AudioConfig.FromMicrophoneInput(targetDevice.ID)
    //     : AudioConfig.FromDefaultMicrophoneInput();

    return AudioConfig.FromDefaultMicrophoneInput();
  }

  private async Task CheckConnectionHealthAsync()
  {
    if (!IsListening || _isDisposed) return;

    var timeSinceLastRecognition = DateTime.Now - _lastRecognitionTime;
    if (timeSinceLastRecognition.TotalMilliseconds > RECOGNITION_TIMEOUT_MS)
    {
      Logger.Log(LogLevel.Warn, "No recognition events detected for a while, attempting reconnection");
      _needsReconnect = true;
      await ReconnectAsync();
    }
  }

  private void StartHealthCheck()
  {
    _healthCheckTimer.Start();
  }

  private async Task ReconnectAsync()
  {
    lock (_reconnectLock)
    {
      if (!_needsReconnect || _isDisposed) return;
      if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
      {
        OnConnectionStatusChanged?.Invoke(this, ConnectionStatus.Failed);
        Logger.Log(LogLevel.Error, "Max reconnection attempts reached");
        return;
      }
    }

    try
    {
      _reconnectAttempts++;
      OnConnectionStatusChanged?.Invoke(this, ConnectionStatus.Reconnecting);
      Logger.Log(LogLevel.Info, $"Attempting reconnection (attempt {_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})");

      await StopListeningAsync();
      await Task.Delay(RECONNECT_DELAY_MS);
      Initialize();
      await StartListeningAsync();

      _needsReconnect = false;
      OnConnectionStatusChanged?.Invoke(this, ConnectionStatus.Connected);
    }
    catch (Exception ex)
    {
      Logger.Log(LogLevel.Error, $"Reconnection attempt failed: {ex.Message}");
      if (_reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
      {
        await Task.Delay(RECONNECT_DELAY_MS);
        await ReconnectAsync();
      }
    }
  }

  private void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
  {
    _lastRecognitionTime = DateTime.Now;

    if (e.Result.Reason == ResultReason.RecognizedSpeech)
    {
      var recognizedText = e.Result.Text;
      _recognitionQueue.Enqueue(recognizedText);

      Logger.Log(LogLevel.Debug, $"Speech recognized: {recognizedText}");
      OnSpeechRecognized?.Invoke(this, recognizedText);
      _core.Bus.Emit("SpeechRecognized", recognizedText);
    }
  }

  private async void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
  {
    if (e.Reason == CancellationReason.Error)
    {
      Logger.Log(LogLevel.Error, $"Speech recognition canceled: {e.ErrorCode} - {e.ErrorDetails}");
      _needsReconnect = true;
      await ReconnectAsync();
    }
  }

  private string GetLanguageCode(RecognizeLang lang) => lang switch
  {
    RecognizeLang.Polish => "pl-PL",
    RecognizeLang.English => "en-US",
    _ => "en-US"
  };

  public async Task StartListeningAsync()
  {
    if (!IsListening && !_isDisposed && _recognizer != null)
    {
      IsListening = true;
      await _recognizer.StartContinuousRecognitionAsync();
      _lastRecognitionTime = DateTime.Now;
      Logger.Log(LogLevel.Info, "Speech recognition started");
    }
  }

  public async Task StopListeningAsync()
  {
    if (IsListening && !_isDisposed && _recognizer != null)
    {
      IsListening = false;
      await _recognizer.StopContinuousRecognitionAsync();
      Logger.Log(LogLevel.Info, "Speech recognition stopped");
    }
  }

  public async Task SetLanguageAsync(RecognizeLang language)
  {
    if (_currentLanguage != language)
    {
      _currentLanguage = language;
      await StopListeningAsync();
      Initialize();
      if (IsListening)
      {
        await StartListeningAsync();
      }
      Logger.Log(LogLevel.Info, $"Speech recognition language changed to {language}");
    }
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_isDisposed)
    {
      if (disposing)
      {
        _healthCheckTimer.Stop();
        _healthCheckTimer.Dispose();
        _cancellationTokenSource.Cancel();
        _recognizer?.Dispose();
        _audioConfig?.Dispose();
      }
      _isDisposed = true;
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  ~SpeechModule()
  {
    Dispose(false);
  }
}