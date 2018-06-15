using System;
using AElf.Database;
using AElf.Database.Config;
using AElf.Kernel;
using AElf.Kernel.Modules.AutofacModule;
using AElf.Kernel.Node;
using Autofac;
using Autofac.Core;

namespace AElf.Benchmark
{
    public class Program
    {
        
        public static void Main()
        {
            Hash chainId = Hash.Generate();
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            builder.RegisterModule(new MetadataModule(chainId));
            builder.RegisterModule(new DatabaseModule(new DatabaseConfig()));
            builder.RegisterType<Benchmarks>().WithParameter("chainId", chainId);
            var container = builder.Build();
            
            if (container == null)
            {
                Console.WriteLine("IoC setup failed");
                return;
            }

            if (!CheckDBConnect(container))
            {
                Console.WriteLine("Database connection failed");
                return;
            }
            
            using(var scope = container.BeginLifetimeScope())
            {
                var benchmarkTps = scope.Resolve<Benchmarks>();
                var resDict = benchmarkTps.RunBenchmark(1, 0).Result;

                foreach (var kv in resDict)
                {
                    Console.WriteLine(kv.Key + ": " + kv.Value);
                }
            }
        }
        
        private static bool CheckDBConnect(IContainer container)
        {
            var db = container.Resolve<IKeyValueDatabase>();
            return db.IsConnected();
        }
    }
}