﻿using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Kernel
{
    public static class ParamsHandler
    {
        public static object Value(this Param param)
        {
            object val = null;
            switch (param.DataCase)
            {
                case Param.DataOneofCase.IntVal:
                    val = param.IntVal;
                    break;
                case Param.DataOneofCase.UintVal:
                    val = param.UintVal;
                    break;
                case Param.DataOneofCase.LongVal:
                    val = param.LongVal;
                    break;
                case Param.DataOneofCase.UlongVal:
                    val = param.UlongVal;
                    break;
                case Param.DataOneofCase.BoolVal:
                    val = param.BoolVal;
                    break;
                case Param.DataOneofCase.BytesVal:
                    val = param.BytesVal.ToByteArray();
                    break;
                case Param.DataOneofCase.StrVal:
                    val = param.StrVal;
                    break;
                case Param.DataOneofCase.DVal:
                    val = param.DVal;
                    break;
                case Param.DataOneofCase.HashVal:
                    val = param.HashVal;
                    break;
                case Param.DataOneofCase.RegisterVal:
                    val = param.RegisterVal;
                    break;
                case Param.DataOneofCase.DeploymentVal:
                    val = param.DeploymentVal;
                    break;
                case Param.DataOneofCase.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return val;
        }

        public static Param ToParam(this object param)
        {
            var type = param.GetType();
            if (type == typeof(int))
            {
                return new Param
                {
                    IntVal = (int)param
                };
            }
            if (type == typeof(uint))
            {
                return new Param
                {
                    UintVal = (uint)param
                };
            }
            if (type == typeof(long))
            {
                return new Param
                {
                    LongVal = (long)param
                };
            }
            if (type == typeof(ulong))
            {
                return new Param
                {
                    UlongVal = (ulong)param
                };
            }
            if (type == typeof(bool))
            {
                return new Param
                {
                    BoolVal = (bool)param
                };
            }
            if (type == typeof(byte[]))
            {
                return new Param
                {
                    BytesVal = ByteString.CopyFrom((byte[])param)
                };
            }
            if (type == typeof(string))
            {
                return new Param
                {
                    StrVal = (string)param
                };
            }
            if (type == typeof(double))
            {
                return new Param
                {
                    DVal = (double)param
                };
            }
            if (type == typeof(Hash))
            {
                return new Param
                {
                    HashVal = (Hash)param
                };
            }
            if (type == typeof(SmartContractRegistration))
            {
                return new Param
                {
                    RegisterVal = (SmartContractRegistration)param
                };
            }
            if (type == typeof(SmartContractDeployment))
            {
                return new Param
                {
                    DeploymentVal = (SmartContractDeployment)param
                };
            }

            return null;
        }
    }
}