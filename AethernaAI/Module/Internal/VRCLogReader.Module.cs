// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AethernaAI.Enum;
using AethernaAI.Util;
using static AethernaAI.Constants;
using static AethernaAI.Util.ProcessUtil;

namespace AethernaAI.Module.Internal;

public class ProcessedEventArgs : EventArgs
{
  public string Action { get; }
  public string UserId { get; } 
  public string? SourceId { get; } = null;

  public ProcessedEventArgs(string action, string userId, string? sourceId)
  {
    Action = action;
    UserId = userId;
    SourceId = sourceId;
  }
}

/// <summary>
/// Class responsible for reading and processing VRChat log files.
/// </summary>
public class VRCLogReader
{
  private Core? _core;
  private long _lastReadOffset = 0;

  private readonly List<string> _ignorePatterns = new List<string>
    {
        "SRV",
        "gesture",
        "Exception",
        "Found SDK",
        "is missing",
        "EOSManager",
        "Removed player",
        "Restored player",
        "CacheComponents",
        "OnPlayerLeftRoom",
        "Voice DeliveryMode",
        "OnPlayerEnteredRoom",
        "OnPlayerJoinComplete",
        "Measure Human Avatar",
        "Using custom fx mask",
        "Initialize ThreePoint",
        "Initialized PlayerAPI",
        "Using default fx mask",
        "Network IDs from Avatar",
        "is controlled by a curve",
        "Buffer already contains chain",
        "doesn't have a texture property",
        "Releasing render texture that is set",
        "Can not play a disabled audio source",
        "Look rotation viewing vector is zero",
        "Collision force is restricted on avatars",
    };

  public VRCLogReader(Core core)
  {
    _core = core;
    Logger.Log(LogLevel.Info, "VRCLogReader constructed");
    _ = MonitorVRChatProcessAsync(); // Start monitoring as a background task
  }

  private async Task MonitorVRChatProcessAsync()
  {
    while (true)
    {
      try
      {
        var process = GetProcessByName("VRChat");
        if (process == null || process.HasExited)
        {
          Logger.Log(LogLevel.Info, "Waiting for VRChat process to start...");
          await Task.Delay(1000); // Non-blocking delay
          continue;
        }

        Logger.Log(LogLevel.Info, "VRChat process detected.");
        InitializeLogOffset();

        // Process logs in the background
        await ProcessLogForProcessLifecycleAsync(process);
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Error in MonitorVRChatProcessAsync: {ex}");
      }
    }
  }

  private async Task ProcessLogForProcessLifecycleAsync(Process vrcProcess)
  {
    var logFile = GetLatestLogFile();
    if (logFile == null)
    {
      Logger.Log(LogLevel.Warn, "No VRChat log file found.");
      return;
    }

    Logger.Log(LogLevel.Info, $"Monitoring VRChat log: {logFile.Name}");

    while (!vrcProcess.HasExited)
    {
      try
      {
        ReadLog(logFile.FullName);
        await Task.Delay(100); // Non-blocking delay for smoother polling
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Error processing log lifecycle: {ex}");
      }
    }

    Logger.Log(LogLevel.Warn, "VRChat process exited.");
  }

  private void InitializeLogOffset()
  {
    var logFile = GetLatestLogFile();
    if (logFile == null) return;

    using var stream = new FileStream(logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    _lastReadOffset = stream.Length;
  }

  private FileInfo? GetLatestLogFile()
  {
    var directory = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"Low\VRChat\VRChat");
    if (!directory.Exists)
    {
      Logger.Log(LogLevel.Error, "VRChat log directory does not exist.");
      return null;
    }

    return directory.GetFiles("output_log_*.txt", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(file => file.LastWriteTime)
                    .FirstOrDefault();
  }

  private void ReadLog(string path)
  {
    var lines = ReadNewLines(path);
    foreach (var line in lines)
    {
      if (!ProcessLine(line))
      {
        Console.WriteLine(line);
      }
    }
  }

  private List<string> ReadNewLines(string filePath)
  {
    List<string> lines = new();
    StringBuilder currentLine = new();

    try
    {
      using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var reader = new StreamReader(stream);

      reader.BaseStream.Seek(_lastReadOffset, SeekOrigin.Begin);

      string line;
      while ((line = reader.ReadLine()!) != null)
      {
        if (Regex.IsMatch(line, @"^\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2}"))
        {
          if (currentLine.Length > 0)
          {
            lines.Add(currentLine.ToString().Trim());
            currentLine.Clear();
          }
        }

        currentLine.AppendLine(line);
      }

      if (currentLine.Length > 0)
      {
        lines.Add(currentLine.ToString().Trim());
      }

      _lastReadOffset = reader.BaseStream.Position;
    }
    catch (IOException ex)
    {
      Logger.Log(LogLevel.Error, ex.Message);
    }

    return lines;
  }

  public event EventHandler<ProcessedEventArgs>? OnProcessed;
  private bool ProcessLine(string line)
  {
    Dictionary<string, Match> _matched = new Dictionary<string, Match>
        {
            { "left",             PLAYER_LEFT.Match(line) },
            { "joined",           PLAYER_JOIN.Match(line) },
            { "newSticker",  STICKER_SPAWN.Match(line) }
        };

    if (_matched.Any(x => x.Value.Success))
    {
      string action = _matched.First(x => x.Value.Success).Key;

      switch (action)
      {
        case "left":
        case "joined":
          {
            string userId = _matched[action].Groups[2].Value;
            string displayName = _matched[action].Groups[1].Value;

            Logger.Log(LogLevel.Debug, $"User {displayName} ({userId}) {action}");
            OnProcessed?.Invoke(this, new ProcessedEventArgs(action, userId, null));
            break;
          }
        case "newSticker":
          {
            string userId = _matched[action].Groups[1].Value;
            string username = _matched[action].Groups[2].Value;
            string stickerId = _matched[action].Groups[3].Value;

            Logger.Log(LogLevel.Debug, $"User {username} ({userId}) spawned sticker {stickerId}");
            OnProcessed?.Invoke(this, new ProcessedEventArgs(action, userId, stickerId));
            break;
          }
        default:
          Logger.Log(LogLevel.Warn, $"Unknown action: {action}");
          break;
      }

      return true;
    }

    if (_ignorePatterns.Any(pattern => line.Contains(pattern)))
      return true;

    return false;
  }
}
