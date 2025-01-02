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

/// <summary>
/// Class responsible for reading and processing VRChat log files.
/// </summary>
public class VRCLogReader
{
  private Core? _core;
  private long _lastReadOffset = 0;

  /// <summary>
  /// A list of patterns to ignore when processing log lines.
  /// </summary>
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

  /// <summary>
  /// Initializes a new instance of the <see cref="VRCLogReader"/> class.
  /// </summary>
  public VRCLogReader(Core core)
  {
    _core = core;
    Logger.Log(LogLevel.Info, "VRCLogReader constructed");
    MonitorVRChatProcess();
  }

  /// <summary>
  /// Reads new lines from the log file starting from the last read offset.
  /// </summary>
  /// <param name="filePath">The path to the log file.</param>
  /// <returns>A list of new lines read from the log file.</returns>
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

  /// <summary>
  /// Processes a single line from the log file.
  /// </summary>
  /// <param name="line">The line to process.</param>
  /// <returns>True if the line was processed, otherwise false.</returns>

  private bool ProcessLine(string line)
  {
    Dictionary<string, Match> _matched = new Dictionary<string, Match>
    {
      { "left",             PLAYER_LEFT.Match(line) },
      { "joined",           PLAYER_JOIN.Match(line) },
      { "sticker_spawned",  STICKER_SPAWN.Match(line) }
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
          string username = _matched[action].Groups[1].Value;

          if (action == "left")
            _core!.Bus.Emit("PlayerLeft", userId, username);
          else
            _core!.Bus.Emit("PlayerJoined", userId, username);

          Logger.Log(LogLevel.Debug, $"User {username} ({userId}) {action}");
          break;
        }
        case "sticker_spawned":
        {
          string userId = _matched[action].Groups[1].Value;
          string username = _matched[action].Groups[2].Value;
          string stickerId = _matched[action].Groups[3].Value;

          _core!.Bus.Emit("PlayerSpawnedSticker", userId, stickerId, username);
          // PlayerSpawnedSticker?.Invoke(userId, stickerId, username);
          Logger.Log(LogLevel.Debug, $"User {username} ({userId}) spawned sticker {stickerId}");
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

  /// <summary>
  /// Reads the log file and processes each line.
  /// </summary>
  /// <param name="path">The path to the log file.</param>
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

  /// <summary>
  /// Monitors the VRChat process and reads the log file when the process is running.
  /// </summary>
  
  // public Action<bool>? Ready;

  private void MonitorVRChatProcess()
  {
    while (true)
    {
      var process = GetProcessByName("VRChat");
      if (process == null || process.HasExited)
      {
        Logger.Log(LogLevel.Info, "Waiting for VRChat process to start...");
        Thread.Sleep(1000);
        continue;
      }

      Logger.Log(LogLevel.Info, "VRChat process detected.");
      InitializeLogOffset();
      ProcessLogForProcessLifecycle(process);
    }
  }

  /// <summary>
  /// Processes the log file for the lifecycle of the VRChat process.
  /// </summary>
  /// <param name="vrcProcess">The VRChat process.</param>
  private void ProcessLogForProcessLifecycle(Process vrcProcess)
  {
    var logFile = GetLatestLogFile();
    if (logFile is null)
    {
      Logger.Log(LogLevel.Warn, "No VRChat log file found.");
      return;
    }

    Logger.Log(LogLevel.Info, $"Monitoring VRChat log: {logFile.Name}");

    while (!vrcProcess.HasExited)
    {
      ReadLog(logFile.FullName);
      Thread.Sleep(100);
    }

    Logger.Log(LogLevel.Warn, "VRChat process exited.");
  }

  /// <summary>
  /// Initializes the log offset to the end of the latest log file.
  /// </summary>
  private void InitializeLogOffset()
  {
    var logFile = GetLatestLogFile();
    if (logFile == null) return;

    using var stream = new FileStream(logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    _lastReadOffset = stream.Length;
  }

  /// <summary>
  /// Gets the latest VRChat log file.
  /// </summary>
  /// <returns>The latest log file, or null if no log file is found.</returns>
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
}