using AethernaAI.Enum;
using static AethernaAI.Addresses;

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using AethernaAI.Util;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace AethernaAI.Module;

public class SpeechModule
{
  private List<WaveInCapabilities> GetAvailableMicrophones()
  {
    var deviceList = new List<WaveInCapabilities>();
    for (int deviceId = 0; deviceId < WaveIn.DeviceCount; deviceId++)
    {
      deviceList.Add(WaveIn.GetCapabilities(deviceId));
    }
    return deviceList;
  }

  private void Initialize()
  {
    _currentLanguage = _core.Config.GetConfig<RecognizeLang>(config => config.SpeechRecognizerLanguage!);
    _selectedLanguage = GetRecognizeLang(_currentLanguage);

    var _token = _core.Config.GetConfig<string?>(config => config.SpeechRecognizerToken!);
    var _region = _core.Config.GetConfig<string?>(config => config.SpeechRecognizerRegion!);
    if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_region))
      throw new ArgumentNullException(_token is null ? "SpeechRecognizerToken" : "SpeechRecognizerRegion");

    _config = SpeechConfig.FromSubscription(_token, _region);
    _config.SetProfanity(ProfanityOption.Raw);
    _config.SpeechRecognitionLanguage = _selectedLanguage;

    // var audioConfig = AudioConfig.FromDefaultMicrophoneInput();

    var enumerator = new MMDeviceEnumerator();
    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

    // int index = 0;
    // foreach (var device in devices)
    // {
    //   Console.WriteLine($"{index}: {device.FriendlyName}");
    //   index++;
    // }
    var targetDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("Virtual Cable", StringComparison.OrdinalIgnoreCase));
    var audioConfig = AudioConfig.FromMicrophoneInput(targetDevice!.ID);

    Console.WriteLine($"trg: {targetDevice.FriendlyName} | {targetDevice.ID}");

    _recognizer = new SpeechRecognizer(_config, audioConfig);
    _recognizer.Recognized += OnRecognized;

    Logger.Log(LogLevel.Info, $"Speech module initialized: l:{_currentLanguage}, r:{_region}");
  }

  public SpeechModule(Core core)
  {
    _core = core;
    if (_core is null)
      throw new ArgumentNullException(nameof(core));

    Initialize();
  }

  public bool Listening { get; private set; } = false;

  private readonly decimal _limitTalkTime = 20 * 1000;
  private readonly Core _core;

  private SpeechRecognizer? _recognizer;
  private DateTime _lastTalkTime = DateTime.Now;
  private SpeechConfig? _config;
  private RecognizeLang _currentLanguage = RecognizeLang.Polish;
  private string? _selectedLanguage = GetRecognizeLang(RecognizeLang.Polish);
  private bool _recognitionRunning = false;

  public async Task StartMyContinuousRecognitionAsync()
  {
    if (!_recognitionRunning)
    {
      _recognitionRunning = true;
      await _recognizer!.StartContinuousRecognitionAsync();
      Logger.Log(LogLevel.Info, "Started continuous recognition");
    }
  }

  public async Task StopMyContinuousRecognitionAsync()
  {
    if (_recognitionRunning)
    {
      _recognitionRunning = false;
      await _recognizer!.StopContinuousRecognitionAsync();
      Logger.Log(LogLevel.Info, "Stopped continuous recognition");
    }
  }

  private List<string> SplitIntoChunks(string text, int maxLength)
  {
    List<string> chunks = new List<string>();
    string currentChunk = "";

    foreach (var word in text.Split(' '))
    {
      if (currentChunk.Length + word.Length + 1 <= maxLength)
      {
        currentChunk += word + " ";
      }
      else
      {
        chunks.Add(currentChunk.Trim());
        currentChunk = word + " ";
      }
    }

    if (!string.IsNullOrEmpty(currentChunk))
    {
      chunks.Add(currentChunk.Trim());
    }

    return chunks;
  }

  private async Task SendMessageInChunks(string message, int delayBetweenChunks = 5000)
  {
    var chunks = SplitIntoChunks(message, 144); // VRChat's character limit is 144

    foreach (var chunk in chunks)
    {
      await _core.OSC!.Send(GetOscAddress(VRCOscAddresses.SET_CHATBOX_TYPING), true);
      await Task.Delay(500); // Brief typing indication

      await _core.OSC!.Send(GetOscAddress(VRCOscAddresses.SET_CHATBOX_TYPING), false);
      await _core.OSC!.Send(GetOscAddress(VRCOscAddresses.SEND_CHATBOX_MESSAGE), chunk, true);

      if (chunks.Count > 1)
        await Task.Delay(delayBetweenChunks); // Wait between chunks
    }
  }

  private async void OnRecognized(object? sender, SpeechRecognitionEventArgs e)
  {
    Console.WriteLine($"RECG={e.Result.Text}");

    var now = DateTime.Now;
    var elapsed = (now - _lastTalkTime).TotalMilliseconds;

    if (elapsed > (double)_limitTalkTime && Listening)
    {
      Logger.Log(LogLevel.Info, $"No speech detected for {Math.Floor(_limitTalkTime / 1000)} secs, stopping listening");
      Listening = false;
      return;
    }

    if (string.IsNullOrWhiteSpace(e.Result.Text) || !(e.Result.Reason == ResultReason.RecognizedSpeech))
      return;

    _lastTalkTime = DateTime.Now;

    var activatePhrase = GetActivationPhrases(_currentLanguage);
    if (activatePhrase.Any(phrase => e.Result.Text.Contains(phrase)) && !Listening)
    {
      Logger.Log(LogLevel.Info, "Activation phrase detected, starting listening");
      Listening = true;
      return;
    }

    if (Listening)
    {
      Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
      await _core.OSC!.Send(GetOscAddress(VRCOscAddresses.SET_CHATBOX_TYPING), true);

      var talk = await _core.GPT!.GenerateResponse($"{e.Result.Text} <note>Limit it to 100 characters</note>");
      Console.WriteLine($"RESPONSE: {talk}");

      await SendMessageInChunks(talk);
      Listening = false;
    }
  }
}
