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
  public string? VrchatGroupId { get; set; } = "grp_2e1917ed-0f8d-4075-8098-5919a37c8f43";

  [JsonProperty("vrchat.worldId")]
  public string? VrchatWorldId { get; set; } = "wrld_dec35e59-53f5-4def-b29c-2d7b649b8638";

  [JsonProperty("vrchat.username")]
  public string? VrchatUsername { get; set; } = "eqipleeburton@gmail.com";

  [JsonProperty("vrchat.password")]
  public string? VrchatPassword { get; set; } = "eqipleeburton@gmail.com";

  [JsonProperty("vrchat.oscMessage")]
  public List<string> VrchatOscMessage { get; set; } = new() {};

  [JsonProperty("discord.webhookUrl")]
  public string? DiscordWebhookUrl { get; set; } = "https://discord.com/api/webhooks/1325915482477563944/lHEGyvoyv0Zsa42Yay70VvttlKeiL7npzVHGwm4jYkcA8J2Lcub0GSggiGVWCn8y_jEs";

  [JsonProperty("speechRecognizer.token")]
  public string? SpeechRecognizerToken { get; set; } = "9019e90eebd249659d33a4999fbf33fb";

  [JsonProperty("speechRecognizer.region")]
  public string? SpeechRecognizerRegion { get; set; } = "northeurope";

  [JsonProperty("speechRecognizer.language")]
  public RecognizeLang SpeechRecognizerLanguage { get; set; } = RecognizeLang.Polish;
}