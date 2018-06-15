using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Concurrency;
using AElf.Kernel.Concurrency.Execution;
using AElf.Kernel.Concurrency.Execution.Messages;
using AElf.Kernel.Concurrency.Metadata;
using AElf.Kernel.Extensions;
using AElf.Kernel.KernelAccount;
using AElf.Kernel.Managers;
using AElf.Kernel.Modules.AutofacModule;
using AElf.Kernel.Services;
using AElf.Kernel.Storages;
using AElf.Runtime.CSharp;
using AElf.Sdk.CSharp;
using Akka.Actor;
using Autofac;
using Google.Protobuf;
using ServiceStack;

namespace AElf.Benchmark
{
    public class Benchmarks
    {
        private IWorldStateManager _worldStateManager;
        private IChainCreationService _chainCreationService;
        private IBlockManager _blockManager;
        private ISmartContractManager _smartContractManager;
        private ISmartContractService _smartContractService;
        private ITransactionContext _transactionContext;
        private ISmartContractContext _smartContractContext;
        private IChainContextService _chainContextService;
        private IResourceUsageDetectionService _resourceUsageDetectionService;
        private IChainFunctionMetadata _chainFunctionMetadata;
        private IChainFunctionMetadataTemplate _chainFunctionMetadataTemplate;
        private IDataStore _dataStore;

        private ServicePack _servicePack;
        
        private ISmartContractRunnerFactory _smartContractRunnerFactory = new SmartContractRunnerFactory();

        public Benchmarks(IWorldStateManager worldStateManager, IChainCreationService chainCreationService, IBlockManager blockManager, ISmartContractManager smartContractManager, IChainContextService chainContextService, IResourceUsageDetectionService resourceUsageDetectionService, IChainFunctionMetadata chainFunctionMetadata, IChainFunctionMetadataTemplate chainFunctionMetadataTemplate, IDataStore dataStore, ITransactionContext transactionContext, ISmartContractContext smartContractContext, Hash chainId = null)
        {
            ChainId = chainId;
            _worldStateManager = worldStateManager;
            _chainCreationService = chainCreationService;
            _blockManager = blockManager;
            _smartContractManager = smartContractManager;
            _chainContextService = chainContextService;
            _resourceUsageDetectionService = resourceUsageDetectionService;
            _chainFunctionMetadata = chainFunctionMetadata;
            _chainFunctionMetadataTemplate = chainFunctionMetadataTemplate;
            _dataStore = dataStore;
            _transactionContext = transactionContext;
            _smartContractContext = smartContractContext;


            var runner = new SmartContractRunner("./bin/Debug/netcoreapp2.0/");
            _smartContractRunnerFactory.AddRunner(0, runner);
            _smartContractService = new SmartContractService(_smartContractManager, _smartContractRunnerFactory, _worldStateManager);

            _servicePack = new ServicePack()
            {
                ChainContextService = _chainContextService,
                SmartContractService = _smartContractService,
                ResourceDetectionService = _resourceUsageDetectionService
            };
        }

        private Hash ChainId { get; }
        private int _incrementId = 0;
        
        public byte[] SmartContractZeroCode
        {
            get
            {
                byte[] code = null;
                using (FileStream file = File.OpenRead(System.IO.Path.GetFullPath("../AElf.Contracts.SmartContractZero/bin/Debug/netstandard2.0/AElf.Contracts.SmartContractZero.dll")))
                {
                    code = file.ReadFully();
                }
                return code;
            }
        }

        public async Task<Dictionary<string, double>> RunBenchmark(int txNumber, double conflictRate)
        {
            TransactionDataGenerator dataGenerator = new TransactionDataGenerator(txNumber, conflictRate);
            var addressPair = dataGenerator.GenerateTransferAddressPair(out var keyDict);
            Console.WriteLine(keyDict.Count + " pub-priv key pair generated");

            byte[] code = null;
            using (FileStream file = File.OpenRead(System.IO.Path.GetFullPath("./bin/Debug/netcoreapp2.0/AElf.Benchmark.dll")))
            {
                code = file.ReadFully();
            }

            var contractHash = await Prepare(code, keyDict.Keys);
            Console.WriteLine("Perparation done");
            
            var txList = dataGenerator.GenerateTransferTransactions(contractHash, addressPair, keyDict);
            Console.WriteLine(txList.Count + " tx generated");
            
            foreach (var tx in txList)
            {
                tx.To = contractHash;
            }
            
            Console.WriteLine("start to check signature");
            Stopwatch swVerifer = new Stopwatch();
            swVerifer.Start();

            var tasks = new List<Task>();
            
            foreach (var tx in txList)
            {
                var task = Task.Run(() =>
                {
                    ECKeyPair recipientKeyPair = ECKeyPair.FromPublicKey(tx.P.ToByteArray());
                    ECVerifier verifier = new ECVerifier(recipientKeyPair);
                    if(!verifier.Verify(tx.GetSignature(), tx.GetHash().GetHashBytes()))
                    {
                        throw new Exception("Signature failed");
                    }
                });
                tasks.Add(task);
            }
            Task.WaitAll(tasks.ToArray());
            
            swVerifer.Stop();
            
            Console.WriteLine("start execute");
            //Execution
            Stopwatch swExec = new Stopwatch();
            swExec.Start();

            var sysActor = ActorSystem.Create("benchmark");
            var _serviceRouter = sysActor.ActorOf(LocalServicesProvider.Props(_servicePack));
            var _generalExecutor = sysActor.ActorOf(GeneralExecutor.Props(sysActor, _serviceRouter), "exec");
            _generalExecutor.Tell(new RequestAddChainExecutor(ChainId));
            var executingService = new ParallelTransactionExecutingService(sysActor);
            var txResult = Task.Factory.StartNew(async () =>
            {
                return await executingService.ExecuteAsync(txList, ChainId);
            }).Unwrap().Result;
            
            swExec.Stop();
            
            
            
            Api.SetSmartContractContext(_smartContractContext);
            Api.SetTransactionContext(_transactionContext);
            
            TestTokenContract contract = new TestTokenContract();
            await contract.InitializeAsync("token1", Hash.Zero.ToAccount());

            foreach (var kv in keyDict)
            {
                await contract.InitBalance(kv.Key, Hash.Zero.ToAccount());
            }
            
            
            Stopwatch swNoReflaction = new Stopwatch();
            swNoReflaction.Start();
            
            foreach (var tx in txList)
            {
                contract.Transfer(tx.From, tx.To, 50);
            }
            
            
            swNoReflaction.Stop();
            
            Dictionary<string, double> res = new Dictionary<string, double>();

            var verifyPerSec = txNumber / (swVerifer.ElapsedMilliseconds / 1000.0);
            res.Add("verifyTPS", verifyPerSec);

            var executeTPS = txNumber / (swExec.ElapsedMilliseconds / 1000.0);
            res.Add("executeTPS", executeTPS);

            var executeTPSNoReflection = txNumber / (swNoReflaction.ElapsedMilliseconds / 1000.0);
            res.Add("executeTPSNoReflection", executeTPSNoReflection);
            return res;
        }
        
        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong)n;
        }

        public async Task<Hash> Prepare(byte[] contractCode, IEnumerable<Hash> addrBook)
        {
            //create smart contact zero
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.Zero
            };
            
            var chain = await _chainCreationService.CreateNewChainAsync(ChainId, reg);
            var genesis = await _blockManager.GetBlockAsync(chain.GenesisBlockHash);
            var contractAddressZero = new Hash(ChainId.CalculateHashWith("__SmartContractZero__")).ToAccount();
            
            
            
            //deploy token contract
            var code = contractCode;

            var regExample = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(code),
                ContractHash = code.CalculateHash()
            };
            
            var txnDep = new Transaction()
            {
                From = Hash.Zero.ToAccount(),
                To = contractAddressZero,
                IncrementId = NewIncrementId(),
                MethodName = "DeploySmartContract",
                Params = ByteString.CopyFrom(new AElf.Kernel.Parameters()
                {
                    Params = {
                        new Param
                        {
                            RegisterVal = regExample
                        }
                    }
                }.ToByteArray())
            };
            
            var txnCtxt = new TransactionContext()
            {
                Transaction = txnDep
            };
            
            var executive = await _smartContractService.GetExecutiveAsync(contractAddressZero, ChainId);
            await executive.SetTransactionContext(txnCtxt).Apply();
            
            
            var contractAddr = txnCtxt.Trace.RetVal.Unpack<Hash>();
            
            //set metadata template else where
            _chainFunctionMetadataTemplate.TryAddNewContract(typeof(TestTokenContract));
            _chainFunctionMetadata = new ChainFunctionMetadata(_chainFunctionMetadataTemplate, _dataStore);
            _chainFunctionMetadata.DeployNewContract("TestTokenContract", contractAddr, new Dictionary<string, Hash>());
            _servicePack.ResourceDetectionService.ChainFunctionMetadata = _chainFunctionMetadata;
            
            //init contract
            var txnInit = new Transaction
            {
                From = Hash.Zero.ToAccount(),
                To = contractAddr,
                IncrementId = NewIncrementId(),
                MethodName = "InitializeAsync",
                Params = ByteString.CopyFrom(new AElf.Kernel.Parameters()
                {
                    Params = {
                        new Param
                        {
                            StrVal = "TestToken" + _incrementId
                        },
                        new Param
                        {
                            HashVal = Hash.Zero.ToAccount()
                        }
                    }
                }.ToByteArray())
            };
            
            var txnInitCtxt = new TransactionContext()
            {
                Transaction = txnInit
            };
            var executiveUser = await _smartContractService.GetExecutiveAsync(contractAddr, ChainId);
            await executiveUser.SetTransactionContext(txnInitCtxt).Apply();
            
            //init contract
            var tasks = new List<Task>();
            foreach (var addr in addrBook)
            {
                var task = Task.Run(async () =>
                {
                    var txnBalInit = new Transaction
                    {
                        From = Hash.Zero.ToAccount(),
                        To = contractAddr,
                        IncrementId = NewIncrementId(),
                        MethodName = "InitBalance",
                        Params = ByteString.CopyFrom(new AElf.Kernel.Parameters()
                        {
                            Params = {
                                new Param
                                {
                                    HashVal = addr
                                },
                                new Param
                                {
                                    HashVal = Hash.Zero.ToAccount()
                                }
                            }
                        }.ToByteArray())
                    };
            
                    var txnBalInitCtx = new TransactionContext()
                    {
                        Transaction = txnBalInit
                    };
                    var executiveBalInitUser = await _smartContractService.GetExecutiveAsync(contractAddr, ChainId);
                    await executiveBalInitUser.SetTransactionContext(txnBalInitCtx).Apply();
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            
            return contractAddr;
        }
        
    }
}