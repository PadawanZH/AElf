﻿using System;
using System.Threading.Tasks;
using AElf.Kernel.Extensions;
using AElf.Kernel.Storages;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Kernel.Managers
{
    public class BlockManager : IBlockManager
    {
        private readonly IBlockHeaderStore _blockHeaderStore;

        private readonly IBlockBodyStore _blockBodyStore;

        private readonly IWorldStateManager _worldStateManager;

        private IDataProvider _heightOfBlock;

        public BlockManager(IBlockHeaderStore blockHeaderStore, IBlockBodyStore blockBodyStore, IWorldStateManager worldStateManager)
        {
            _blockHeaderStore = blockHeaderStore;
            _blockBodyStore = blockBodyStore;
            _worldStateManager = worldStateManager;
        }

        public async Task<Block> AddBlockAsync(Block block)
        {
            if (!Validation(block))
            {
                throw new InvalidOperationException("Invalid block.");
            }

            await _blockHeaderStore.InsertAsync(block.Header);
            await _blockBodyStore.InsertAsync(block.Header.MerkleTreeRootOfTransactions, block.Body);

            return block;
        }


        public async Task<BlockHeader> GetBlockHeaderAsync(Hash blockHash)
        {
            return await _blockHeaderStore.GetAsync(blockHash);
        }

        public async Task<BlockHeader> AddBlockHeaderAsync(BlockHeader header)
        {
            return await _blockHeaderStore.InsertAsync(header);
        }

        public async Task<Block> GetBlockAsync(Hash blockHash)
        {
            var header = await _blockHeaderStore.GetAsync(blockHash);
            var body = await _blockBodyStore.GetAsync(header.MerkleTreeRootOfTransactions);
            return new Block
            {
                Header = header,
                Body = body
            };
        }
        
        public async Task<Block> GetNextBlockOf(Hash chainId, Hash blockHash)
        {
            await InitialHeightOfBlock(chainId);
            
            var nextBlockHeight = (await GetBlockAsync(blockHash)).Header.Index + 1;
            var nextBlockHash = Hash.Parser.ParseFrom(await _heightOfBlock.GetAsync(new UInt64Value {Value = nextBlockHeight}.CalculateHash()));
            return await GetBlockAsync(nextBlockHash);
        }
        
        public async Task<Block> GetBlockByHeight(Hash chainId, ulong height)
        {
            await InitialHeightOfBlock(chainId);
            
            var key = Hash.Parser.ParseFrom(await _heightOfBlock.GetAsync(new UInt64Value {Value = height}.CalculateHash()));

            var blockHeader = await _blockHeaderStore.GetAsync(key);
            var blockBody = await _blockBodyStore.GetAsync(blockHeader.MerkleTreeRootOfTransactions);
            return new Block
            {
                Header = blockHeader,
                Body = blockBody
            };
        }


        /// <summary>
        /// The validation should be done in manager instead of storage.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        private bool Validation(IBlock block)
        {
            // TODO:
            // Do some checks like duplication
            return true;
        }

        private async Task InitialHeightOfBlock(Hash chainId)
        {
            await _worldStateManager.OfChain(chainId);
            _heightOfBlock = _worldStateManager.GetAccountDataProvider(Path.CalculatePointerForAccountZero(chainId))
                .GetDataProvider().GetDataProvider("HeightOfBlock");
        }
    }
}