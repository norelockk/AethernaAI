using AethernaAI.Enum;
using Newtonsoft.Json;

namespace AethernaAI.Model;

public class ConfigModel
{
  [JsonProperty("gpt.api")]
  public string? GptApi { get; set; } = "http://195.179.227.219:1337/v1";

  [JsonProperty("gpt.token")]
  public string? GptToken { get; set; } = null;

  [JsonProperty("vrchat.groupId")]
  public string? VrchatGroupId { get; set; } = "grp_f3518074-c206-4dc1-b17d-0864d46f8c98";

  [JsonProperty("vrchat.worldId")]
  public string? VrchatWorldId { get; set; } = "wrld_dec35e59-53f5-4def-b29c-2d7b649b8638";

  [JsonProperty("vrchat.username")]
  public string? VrchatUsername { get; set; } = "norelock";

  [JsonProperty("vrchat.password")]
  public string? VrchatPassword { get; set; } = "";

  [JsonProperty("speechRecognizer.token")]
  public string? SpeechRecognizerToken { get; set; } = "9019e90eebd249659d33a4999fbf33fb";

  [JsonProperty("speechRecognizer.region")]
  public string? SpeechRecognizerRegion { get; set; } = "northeurope";

  [JsonProperty("speechRecognizer.language")]
  public RecognizeLang SpeechRecognizerLanguage { get; set; } = RecognizeLang.Polish;
}