// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

using System.Diagnostics;

namespace AethernaAI.Util;

/// <summary>
/// Provides utility methods for working with processes.
/// </summary>
internal static class ProcessUtil
{
  /// <summary>
  /// Gets the first process with the specified name.
  /// </summary>
  /// <param name="name">The name of the process to find.</param>
  /// <returns>The first <see cref="Process"/> with the specified name, or <c>null</c> if no such process is found.</returns>
  public static Process? GetProcessByName(string name)
  {
    return Process.GetProcessesByName(name).FirstOrDefault();
  }
}