// AethernaAI 2.0
// Original author: '`Deto' (deto_deto)
// Refactor version: 'Norelock' (norelock)

namespace AethernaAI.Util;

public class Interval
{
  private System.Threading.Timer? timer;
  private Action repeatedAction;
  private int interval;

  public Interval(Action action, int interval)
  {
    this.interval = interval;
    repeatedAction = action;
  }

  public void Start()
  {
    timer = new System.Threading.Timer(InvokeAction!, null, 0, interval);
  }

  public void Stop()
  {
    timer?.Change(Timeout.Infinite, Timeout.Infinite);
  }

  private void InvokeAction(object state)
  {
    repeatedAction?.Invoke();
  }
}