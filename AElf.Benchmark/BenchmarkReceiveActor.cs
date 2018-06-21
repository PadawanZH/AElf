using System;
using AElf.Kernel.Concurrency.Execution.Messages;
using Akka.Actor;

namespace AElf.Benchmark
{
    public class BenchmarkReceiveActor : UntypedActor
    {
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case RespondAddChainExecutor req:
                    Console.WriteLine("Add chain response, chainId: " + req.ChainId);
                    break;
            }
        }
    }
}