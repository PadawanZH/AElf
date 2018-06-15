
using System.Collections.Generic;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.TxMemPool;
using Google.Protobuf;

namespace AElf.Benchmark
{
    public class TransactionDataGenerator
    {
        private int _totalNumber;
        private double _conflictRate;

        public TransactionDataGenerator(int totalNumber, double conflictRate)
        {
            _totalNumber = totalNumber;
            this._conflictRate = conflictRate;
        }

        public IEnumerable<KeyValuePair<Hash, Hash>> GenerateTransferAddressPair(out Dictionary<Hash, ECKeyPair> keyDict)
        {
            keyDict = new Dictionary<Hash, ECKeyPair>();
            var txAccountList = new List<KeyValuePair<Hash, Hash>>();
            var keyPairGenerator = new KeyPairGenerator();
            
            int conflictTxCount = (int) (_conflictRate * _totalNumber);
            var conflictKeyPair = keyPairGenerator.Generate();
            var conflictAddr = new Hash(conflictKeyPair.GetAddress());
            keyDict.Add(new Hash(conflictKeyPair.GetAddress()), conflictKeyPair);
            
            for (int i = 0; i < conflictTxCount; i++)
            {
                var senderKp = keyPairGenerator.Generate();
                var senderAddr = new Hash(senderKp.GetAddress());
                txAccountList.Add(new KeyValuePair<Hash, Hash>(senderAddr, conflictAddr));
                keyDict.Add(senderAddr, senderKp);
            }

            for (int i = 0; i < _totalNumber - conflictTxCount; i++)
            {
                var senderKp = keyPairGenerator.Generate();
                var senderAddr = new Hash(senderKp.GetAddress());
                var receiverKp = keyPairGenerator.Generate();
                var receiverAddr = new Hash(receiverKp.GetAddress());
                keyDict.Add(senderAddr, senderKp);
                keyDict.Add(receiverAddr, receiverKp);
                txAccountList.Add(new KeyValuePair<Hash, Hash>(senderAddr, receiverAddr));
            }

            return txAccountList;
        }
        
        

        public List<ITransaction> GenerateTransferTransactions(Hash tokenContractAddr, IEnumerable<KeyValuePair<Hash, Hash>> transferAddressPairs, Dictionary<Hash, ECKeyPair> keyDict)
        {
            var resList = new List<ITransaction>();
            foreach (var addressPair in transferAddressPairs)
            {

                var keyPair = keyDict[addressPair.Key];
                Transaction tx = new Transaction()
                {
                    From = addressPair.Key,
                    To = tokenContractAddr,
                    IncrementId = 0,
                    MethodName = "Transfer",
                    Params = ByteString.CopyFrom(
                        new AElf.Kernel.Parameters
                        {
                            Params =
                            {
                                new Param
                                {
                                    HashVal = addressPair.Key
                                },
                                new Param
                                {
                                    HashVal = addressPair.Value
                                },
                                new Param()
                                {
                                    UlongVal = 50
                                }
                            }
                        }.ToByteArray()
                    ),
                    P = ByteString.CopyFrom(keyPair.PublicKey.Q.GetEncoded())
                };

                Hash txHash = tx.GetHash();
                
                ECSigner signer = new ECSigner();
                ECSignature signature = signer.Sign(keyPair, txHash.GetHashBytes());

                tx.R = ByteString.CopyFrom(signature.R);
                tx.S = ByteString.CopyFrom(signature.S);
                
                resList.Add(tx);
            }

            return resList;
        }
    }
}