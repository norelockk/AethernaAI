using AethernaAI.Enum;
using AethernaAI.Manager;
using AethernaAI.Model;
using AethernaAI.Util;
using static AethernaAI.Addresses;

namespace AethernaAI.Event;

public class SpeechToGPTEvent : IEventListener
{
    private readonly NetworkManager _networkManager;
    
    public string Name { get; } = "SpeechToGPT";

    public SpeechToGPTEvent(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public async void Handle(params object?[] args)
    {
        if (args.Length == 0 || args[0] == null)
            return;

        var recognizedText = args[0].ToString();
        if (string.IsNullOrEmpty(recognizedText))
            return;

        var activationWords = GetActivationPhrases(RecognizeLang.Polish)
            .Concat(GetActivationPhrases(RecognizeLang.English));
            
        if (activationWords.Any(word => 
            recognizedText.Trim().Equals(word, StringComparison.OrdinalIgnoreCase)))
        {
            Logger.Log(LogLevel.Debug, $"Activation word detected, skipping GPT: {recognizedText}");
            return;
        }

        try
        {
            var response = await _networkManager.GPT!.GenerateResponse(recognizedText);
            
            if (!string.IsNullOrEmpty(response))
            {
                await _networkManager.OSC!.Send(
                    GetOscAddress(VRCOscAddresses.SEND_CHATBOX_MESSAGE),
                    response,
                    true
                );
                Logger.Log(LogLevel.Info, $"Response sent to VRChat: {response}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, $"Failed to process speech: {ex.Message}");
        }
    }
}