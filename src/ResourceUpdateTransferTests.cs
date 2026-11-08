using System;
using RTD.ResourceUpdates;
class ResourceUpdateTransferTests {
 static void Main(){
  TransferBudget budget=new TransferBudget(10,15);
  if(budget.Expired(24.9))throw new Exception("premature timeout");
  budget.Observe(0.5f,24);
  if(budget.Expired(38.9))throw new Exception("progress must extend idle budget");
  budget.Observe(0.5f,38);
  if(!budget.Expired(39))throw new Exception("same progress must not reset timeout");
  budget.Observe(0.4f,40);
  if(!budget.Expired(41))throw new Exception("regression must not reset timeout");
  Console.WriteLine("PASS 4 transfer deadline checks");
 }
}
