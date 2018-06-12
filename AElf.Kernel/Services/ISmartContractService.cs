﻿using System.Threading.Tasks;

namespace AElf.Kernel.Services
{
    public interface ISmartContractService
    {
        Task<IExecutive> GetExecutiveAsync(Hash account, Hash chainId);
        Task PutExecutiveAsync(Hash account, IExecutive executive);
        Task DeployContractAsync(Hash account, SmartContractRegistration registration);
    }
}