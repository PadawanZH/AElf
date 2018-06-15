using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using AElf.Kernel.Concurrency.Metadata;

namespace AElf.Kernel.Concurrency
{
    public interface IResourceUsageDetectionService
    {
        IEnumerable<string> GetResources(ITransaction transaction);
        
        IChainFunctionMetadata ChainFunctionMetadata { get; set; } 
    }
}
