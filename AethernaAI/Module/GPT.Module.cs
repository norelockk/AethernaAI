using System.Text;
using Newtonsoft.Json;
using AethernaAI.Util;
using AethernaAI.Enum;
using AethernaAI.Model;
using AethernaAI.Interface;
using static AethernaAI.Addresses;

namespace AethernaAI.Module;

/// <summary>
/// GPT module for generating responses and other text-based tasks
/// </summary>
public class GPTModule : IGPTModule
{
  // <summary>
  // Construct the GPT module
  // </summary>
  public GPTModule(Core core)
  {
    _core = core;
    if (_core is null)
      throw new ArgumentNullException(nameof(core));

    _api = _core.Config.GetConfig<string?>(c => c.GptApi!) ?? throw new ArgumentNullException(nameof(_api));

    var _token = _core.Config.GetConfig<string?>(c => c.GptToken!);
    if (!string.IsNullOrEmpty(_token))
      _http.DefaultRequestHeaders.Add("g4f-api-key", _token);

    Logger.Log(LogLevel.Info, $"GPT initialized with their API: {_api}");
  }

  private protected object? GetBody(string prompt) => new
  {
    model = GetGptModel(model),
    messages = new[] { new { role = "user", content = prompt } }
  };

  public GPTModel model = GPTModel.UNCENSORED;
  private readonly string? _api = string.Empty;
  private readonly HttpClient _http = new();
  private readonly Core? _core = null;

  // <summary>
  // Set GPT model
  // </summary>
  public GPTModel SetModel(GPTModel model) => this.model = model;

  // <summary>
  // Generate a response from the GPT model
  // </summary>
  public async Task<string> GenerateResponse(string? prompt)
  {
    if (string.IsNullOrEmpty(prompt))
      throw new ArgumentNullException(nameof(prompt));

    var _body = GetBody(prompt);
    var _request = new HttpRequestMessage(HttpMethod.Post, $"{_api}/chat/completions")
    {
      Content = new StringContent(
        JsonConvert.SerializeObject(_body),
        Encoding.UTF8,
        "application/json"
      )
    };

    try
    {
      var _response = await _http.SendAsync(_request);
      _response.EnsureSuccessStatusCode();

      var _result = await _response.Content.ReadAsStringAsync();
      var _parsed = JsonConvert.DeserializeObject<GPTApiResponse>(_result);

      return _parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? throw new InvalidOperationException("No response content received");
    }
    catch (Exception ex) when (ex is HttpRequestException or JsonReaderException)
    {
      Logger.Log(LogLevel.Error, $"Response generation failed: {ex.Message}");
      throw new InvalidOperationException("Response generation failed", ex);
    }
  }

  // <summary>
  // Stream a response from model
  // Example using:
  // await foreach (var token in gpt.StreamResponse(prompt))
  // Console.Write(token)
  // )
  // </summary>
  public async IAsyncEnumerable<string> StreamResponse(string? prompt)
  {
    if (string.IsNullOrEmpty(prompt))
      throw new ArgumentNullException(nameof(prompt));

    var _body = GetBody(prompt);
    var _request = new HttpRequestMessage(HttpMethod.Post, $"{_api}/chat/completions")
    {
      Content = new StringContent(
        JsonConvert.SerializeObject(_body),
        Encoding.UTF8,
        "application/json"
      )
    };

    var _response = await _http.SendAsync(_request, HttpCompletionOption.ResponseHeadersRead);

    try
    {
      _response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      Logger.Log(LogLevel.Error, $"Response streaming failed: {ex.Message}");
      throw new InvalidOperationException("Response streaming failed", ex);
    }

    using var stream = await _response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);

    while (!reader.EndOfStream)
    {
      var line = await reader.ReadLineAsync();
      if (string.IsNullOrWhiteSpace(line))
        continue;

      string? content = null;
      try
      {
        var chunk = JsonConvert.DeserializeObject<GPTApiResponse>(line);
        content = chunk?.Choices?.FirstOrDefault()?.Message?.Content;
      }
      catch (JsonReaderException)
      {
        Logger.Log(LogLevel.Warn, $"Malformed JSON chunk: {line}");
      }

      if (!string.IsNullOrEmpty(content))
        yield return content;
    }
  }
}