// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

using System.Reflection;
using System.Text.RegularExpressions;

namespace AethernaAI;

/// <summary>
/// Constants used throughout everywhere in project
/// </summary>
internal static class Constants
{
  // VRChat Logs Regex Regonition (ex. PlayerJoined or else)
  public static readonly Regex PLAYER_JOIN = new(@"\[Behaviour\] OnPlayerJoined ([^\(]+) \((usr_[a-f0-9-]+)\)", RegexOptions.Compiled);
  public static readonly Regex PLAYER_LEFT = new(@"\[Behaviour\] OnPlayerLeft ([^\(]+) \((usr_[a-f0-9-]+)\)", RegexOptions.Compiled);
  public static readonly Regex STICKER_SPAWN = new(@"\[Always\] \[StickersManager\] User (usr_[a-f0-9-]+) \(([^)]+)\) spawned sticker (file_[a-f0-9-]+)", RegexOptions.Compiled);

  // App stuff
  public static readonly string VERSION = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
}