﻿using System.Net;

namespace AethernaAI.OSC;

public interface IOscListener
{
  public Task<OscMessage> ReceiveMessageAsync();

  public Task<(OscMessage Message, IPEndPoint EndPoint)> ReceiveMessageExAsync();

  public Task<OscBundle> ReceiveBundleAsync();

  public Task<(OscBundle Bundle, IPEndPoint EndPoint)> ReceiveBundleExAsync();
}
