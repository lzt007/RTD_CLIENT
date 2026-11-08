namespace RTD.ResourceUpdates {
 public sealed class TransferBudget {
  double advanced;readonly double timeout;float progress;
  public TransferBudget(double started,double timeout){advanced=started;this.timeout=timeout;}
  public void Observe(float value,double now){if(value>progress){progress=value;advanced=now;}}
  public bool Expired(double now){return now-advanced>=timeout;}
 }
}
