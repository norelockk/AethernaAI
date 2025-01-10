﻿namespace AethernaAI.OSC;

public struct TimeTag
{
  public static TimeTag Immediate => new(1);

  public ulong Tag;

  /// <summary>
  /// Gets or sets the timestamp from a DateTime. DateTime has an accuracy down to 100 nanoseconds (100'000
  /// picoseconds)
  /// </summary>
  public DateTime Timestamp
  {
    get => Utils.TimeTagToDateTime(Tag);
    set => Tag = Utils.DateTimeToTimeTag(value);
  }

  /// <summary>
  /// Gets or sets the total seconds in the timestamp. the double precision number is multiplied by 2^32
  /// giving an accuracy down to about 230 picoseconds ( 1/(2^32) of a second)
  /// </summary>
  public double Seconds
  {
    get => Utils.TimeTagToSeconds(Tag);
    set => Tag = Utils.SecondsToTimeTag(value);
  }

  /// <summary>
  /// Gets or sets the fraction of a second in the timestamp. the double precision number is multiplied by 2^32
  /// giving an accuracy down to about 230 picoseconds ( 1/(2^32) of a second)
  /// </summary>
  public double Fraction
  {
    get => Utils.TimeTagToFraction(Tag);
    set => Tag = Utils.SecondsToTimeTag(value);
  }

  public TimeTag(ulong value)
  {
    Tag = value;
  }

  public TimeTag(DateTime value)
  {
    Tag = 0;
    Timestamp = value;
  }

  public override bool Equals(object? obj) => obj switch
  {
    TimeTag timeTag => Tag == timeTag.Tag,
    ulong ulongValue => Tag == ulongValue,
    _ => false
  };

  public static bool operator ==(TimeTag a, TimeTag b) => a.Equals(b);
  public static bool operator !=(TimeTag a, TimeTag b) => a.Equals(b);
  public override int GetHashCode() => (int)(((uint)(Tag >> 32) + (uint)(Tag & 0x00000000FFFFFFFF)) / 2);
}