﻿namespace AElf.Network.Data
{
    public enum MessageTypes : int
    {
        AskForTx = 0,
        RequestPeers = 1,
        ReturnPeers = 2,
        BroadcastTx = 3,
        Ok = 4
    }
}