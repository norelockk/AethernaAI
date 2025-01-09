using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AethernaAI.Util;
using static AethernaAI.Constants;
using static AethernaAI.Util.ProcessUtil;

namespace AethernaAI.Module.Internal;

public class ProcessedEventArgs : EventArgs
{
  public ProcessedEventArgs(string action, Dictionary<string, object> data)
  {
    Action = action;
    Data = data ?? new Dictionary<string, object>();
  }

  public string Action { get; }
  public Dictionary<string, object> Data { get; }

  public void AddData(string key, object value)
  {
    Data[key] = value;
  }
}

public class VRCLogReader
{
  private Core? _core;
  private readonly Dictionary<int, long> _processOffsets = new(); // Track offsets by process ID
  private readonly Dictionary<int, FileInfo?> _processLogFiles = new(); // Track log files by process ID
  private readonly List<string> _ignorePatterns = new()
    {
        "SRV", "gesture", "Exception", "Found SDK", "is missing", "EOSManager",
        "Removed player", "Restored player", "CacheComponents", "OnPlayerLeftRoom",
        "OnPlayerEnteredRoom", "OnPlayerJoinComplete", "Measure Human Avatar",
        "Using custom fx mask", "Initialized PlayerAPI", "Using default fx mask",
        "Network IDs from Avatar", "is controlled by a curve",
        "Buffer already contains chain", "doesn't have a texture property",
        "Releasing render texture that is set", "Can not play a disabled audio source",
        "Look rotation viewing vector is zero", "Collision force is restricted on avatars"
    };

  private readonly HashSet<int> _monitoredProcesses = new(); // Track monitored processes
  private bool _isEOSLauncherRunning = false;

  public VRCLogReader(Core core)
  {
    _core = core;
    Logger.Log(LogLevel.Info, "VRCLogReader constructed");
    _ = DetectVRChatProcess();
  }

  public event EventHandler<int>? ProcessQuit;
  public event EventHandler<int>? ProcessLaunched;

  private async Task DetectVRChatProcess()
  {
    while (true)
    {
      try
      {
        var eosProcess = GetEOSLauncherProcess();
        bool eosLauncherExited = !_isEOSLauncherRunning && eosProcess == null;

        if (eosProcess != null)
        {
          if (!_isEOSLauncherRunning)
          {
            Logger.Log(LogLevel.Info, $"EOS Launcher detected with Process ID {eosProcess.Id}. Waiting for it to exit...");
            _isEOSLauncherRunning = true;
            await WaitForEOSLauncherExitAsync(eosProcess); // Wait for the EOS Launcher to exit
            eosLauncherExited = true; // Indicate that EOS Launcher exited
          }
        }
        else
        {
          if (_isEOSLauncherRunning)
          {
            _isEOSLauncherRunning = false; // EOS Launcher exited
            eosLauncherExited = true;
          }
        }

        // If EOS Launcher has exited (or was never running), proceed with the VRChat process check after a delay
        if (eosLauncherExited)
          await Task.Delay(new Random().Next(1000, 5000));

        // Now monitor VRChat processes after EOS Launcher exit or if EOS was never running
        var processes = GetProcessesByName("VRChat");

        // Detect new processes
        foreach (var process in processes)
        {
          if (!_monitoredProcesses.Contains(process.Id))
          {
            Logger.Log(LogLevel.Info, $"New VRChat process detected: {process.Id}");
            ProcessLaunched?.Invoke(this, process.Id);
            _monitoredProcesses.Add(process.Id);  // Track new process
            _ = ProcessLogForProcessLifecycleAsync(process);  // Start monitoring its log
          }
        }

        // Detect processes that have exited
        var exitedProcesses = _monitoredProcesses.Where(id => !processes.Any(p => p.Id == id)).ToList();
        foreach (var processId in exitedProcesses)
        {
          Logger.Log(LogLevel.Info, $"VRChat process {processId} has exited. Stopping log monitoring.");
          _monitoredProcesses.Remove(processId);
          _processOffsets.Remove(processId); // Cleanup offsets
          _processLogFiles.Remove(processId); // Cleanup log file tracking
          ProcessQuit?.Invoke(this, processId);
        }

        await Task.Delay(1000); // Delay to prevent tight looping
      }
      catch (Exception ex)
      {
        Logger.Log(LogLevel.Error, $"Error in MonitorVRChatProcessesAsync: {ex}");
        await Task.Delay(1000); // Retry after error
      }
    }
  }

  private async Task WaitForEOSLauncherExitAsync(Process eosProcess)
  {
    // Wait for the EOS Launcher to exit
    while (!eosProcess.HasExited)
    {
      await Task.Delay(500); // Check every 500ms
    }

    Logger.Log(LogLevel.Info, $"EOS Launcher (Process {eosProcess.Id}) exited.");
  }

  private Process? GetEOSLauncherProcess()
  {
    // Search for the start_protected_game.exe (EOS Launcher) process
    var eosProcesses = Process.GetProcessesByName("start_protected_game");
    return eosProcesses.FirstOrDefault();
  }

  private async Task ProcessLogForProcessLifecycleAsync(Process vrcProcess)
  {
    try
    {
      // Get or Assign log file for this process
      FileInfo? logFile = _processLogFiles.GetValueOrDefault(vrcProcess.Id);

      if (logFile == null)
      {
        int attempts = 0;
        while (logFile == null && attempts < 5)
        {
          logFile = GetLogFileForProcess(vrcProcess); // Try to get the correct log file for the new process
          if (logFile == null)
          {
            Logger.Log(LogLevel.Warn, $"No VRChat log file found for Process {vrcProcess.Id}. Retrying...");
            await Task.Delay(2000); // Delay for a few seconds before retrying
          }
          attempts++;
        }

        if (logFile == null)
        {
          Logger.Log(LogLevel.Error, $"Failed to find VRChat log file for Process {vrcProcess.Id} after {attempts} attempts.");
          return;
        }

        // Assign the found log file to the process
        Logger.Log(LogLevel.Info, $"Monitoring VRChat log: {logFile.Name} for Process {vrcProcess.Id}");
        _processLogFiles[vrcProcess.Id] = logFile;
        InitializeLogOffsetForProcess(vrcProcess.Id, logFile);
      }

      // Now that we have the log file, continue processing it until the process exits
      while (!vrcProcess.HasExited)
      {
        try
        {
          ReadLogForProcess(vrcProcess.Id, logFile.FullName); // Process log lines per ProcessId
          await Task.Delay(100); // Non-blocking delay for smoother polling
        }
        catch (Exception ex)
        {
          Logger.Log(LogLevel.Error, $"Error processing log for Process {vrcProcess.Id}: {ex}");
        }
      }

      Logger.Log(LogLevel.Warn, $"VRChat process {vrcProcess.Id} exited.");
      _monitoredProcesses.Remove(vrcProcess.Id); // Remove from monitored processes list
      _processOffsets.Remove(vrcProcess.Id); // Remove offsets for the process
      _processLogFiles.Remove(vrcProcess.Id); // Remove the log file tracking
    }
    catch (Exception ex)
    {
      Logger.Log(LogLevel.Error, $"Error in ProcessLogForProcessLifecycleAsync for process {vrcProcess.Id}: {ex}");
    }
  }

  private FileInfo? GetLogFileForProcess(Process process)
  {
    Logger.Log(LogLevel.Info, $"Checking log files for VRChat process {process.Id}");

    var directory = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"Low\VRChat\VRChat");
    if (!directory.Exists)
    {
      Logger.Log(LogLevel.Error, "VRChat log directory does not exist.");
      return null;
    }

    var logFiles = directory.GetFiles("output_log_*.txt");
    if (logFiles.Length == 0)
    {
      Logger.Log(LogLevel.Warn, "No log files found for VRChat process.");
      return null;
    }

    // Return the most recent log file
    var latestLogFile = logFiles.OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
    return latestLogFile;
  }

  private void InitializeLogOffsetForProcess(int processId, FileInfo logFile)
  {
    // Initialize offset based on the last write time of the log file
    _processOffsets[processId] = 0;
  }

  private void ReadLogForProcess(int processId, string path)
  {
    var lines = ReadNewLinesForProcess(processId, path);
    foreach (var line in lines)
    {
      if (!ProcessLine(processId, line)) // Process line per Process ID
      {
        // Console.WriteLine(line); // Default output for unprocessed lines
      }
    }
  }

  private List<string> ReadNewLinesForProcess(int processId, string filePath)
  {
    List<string> lines = new();
    StringBuilder currentLine = new();

    try
    {
      // Open the file for reading and allow sharing for writes by other processes.
      using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var reader = new StreamReader(stream);

      // Seek to the last read position in the file (if available).
      long lastReadPosition = _processOffsets.GetValueOrDefault(processId, 0);
      reader.BaseStream.Seek(lastReadPosition, SeekOrigin.Begin);

      string line;
      while ((line = reader.ReadLine()!) != null)
      {
        // If the line starts with a timestamp (this is an example of the format you provided),
        // we treat it as a new log entry.
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

      // If there is any content left in currentLine after the loop, add it to the list.
      if (currentLine.Length > 0)
      {
        lines.Add(currentLine.ToString().Trim());
      }

      // Update the file position for this process so we can continue from here next time.
      _processOffsets[processId] = reader.BaseStream.Position;
    }
    catch (IOException ex)
    {
      Logger.Log(LogLevel.Error, ex.Message);
    }

    return lines;
  }

  public event EventHandler<ProcessedEventArgs>? OnProcessed;

  private bool ProcessLine(int processId, string line) // Now processing per ProcessId
  {
    // Dictionary to store matched data
    Dictionary<string, Match> _matched = new Dictionary<string, Match>
        {
            { "left", PLAYER_LEFT.Match(line) },
            { "joined", PLAYER_JOIN.Match(line) },
            { "newSticker", STICKER_SPAWN.Match(line) },
            { "setInstance", WORLD_JOINED_OR_DESTINATION.Match(line) },
            { "resetInstance", RESETTING_GAME_FLOW.Match(line) }
        };

    var eventData = new Dictionary<string, object>();
    eventData.Add("ProcessId", processId);

    if (_matched.Any(x => x.Value.Success))
    {
      string action = _matched.First(x => x.Value.Success).Key;
      eventData.Add("Action", action);

      switch (action)
      {
        case "left":
        case "joined":
          {
            string userId = _matched[action].Groups[2].Value;
            string displayName = _matched[action].Groups[1].Value;

            Logger.Log(LogLevel.Debug, $"User {displayName} ({userId}) {action}");

            eventData.Add("UserId", userId);
            eventData.Add("DisplayName", displayName);

            OnProcessed?.Invoke(this, new ProcessedEventArgs(action, eventData));
            break;
          }
        case "newSticker":
          {
            string userId = _matched[action].Groups[1].Value;
            string username = _matched[action].Groups[2].Value;
            string stickerId = _matched[action].Groups[3].Value;

            Logger.Log(LogLevel.Debug, $"User {username} ({userId}) spawned sticker {stickerId}");

            eventData.Add("UserId", userId);
            eventData.Add("Username", username);
            eventData.Add("StickerId", stickerId);

            OnProcessed?.Invoke(this, new ProcessedEventArgs(action, eventData));
            break;
          }
        case "setInstance":
          {
            // string act = _matched[action].Groups[1].Value;  // "Joining" or "Destination requested"
            string worldId = _matched[action].Groups[1].Value;
            string worldName = _matched[action].Groups[2].Value;
            string groupId = _matched[action].Groups[3].Value; // Can be empty if not found
            string worldAccessType = _matched[action].Groups[4].Value; // Can be empty if not found
            string region = _matched[action].Groups[5].Value; // Can be empty if not found

            Logger.Log(LogLevel.Debug, $"Join: WorldId={worldId}, WorldName={worldName}, GroupId={groupId}, worldAccessType={worldAccessType}, Region={region}");

            eventData.Add("WorldId", worldId);
            eventData.Add("WorldName", worldName);
            eventData.Add("WorldAccessType", worldAccessType);
            eventData.Add("GroupId", groupId);
            eventData.Add("Region", region);

            OnProcessed?.Invoke(this, new ProcessedEventArgs(action, eventData));
            break;
          }

        default:
          {
            Logger.Log(LogLevel.Warn, $"Unknown action: {action}");
            OnProcessed?.Invoke(this, new ProcessedEventArgs(action, eventData));
            break;
          }
      }

      return true;
    }

    if (_ignorePatterns.Any(pattern => line.Contains(pattern)))
      return true;

    return false;
  }
}
