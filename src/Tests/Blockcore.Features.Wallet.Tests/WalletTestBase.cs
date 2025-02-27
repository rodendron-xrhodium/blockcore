﻿using System;
using System.Collections.Generic;
using Blockcore.Features.Wallet.Database;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Features.Wallet.Types;
using Blockcore.Tests.Common;
using NBitcoin;

namespace Blockcore.Features.Wallet.Tests
{
    public class WalletTestBase
    {
        public static AccountRoot CreateAccountRoot(int coinType)
        {
            return new AccountRoot()
            {
                Accounts = new List<IHdAccount>(),
                CoinType = coinType
            };
        }

        public static AccountRoot CreateAccountRootWithHdAccountHavingAddresses(string accountName, int coinType)
        {
            return new AccountRoot()
            {
                Accounts = new List<IHdAccount> {
                    new HdAccount {
                        Name = accountName,
                        InternalAddresses = new List<HdAddress>
                        {
                            CreateAddress(false),
                        },
                        ExternalAddresses = new List<HdAddress>
                        {
                            CreateAddress(false),
                        }
                    }
                },
                CoinType = coinType
            };
        }

        public static HdAccount CreateAccount(string name)
        {
            return new HdAccount
            {
                Name = name,
                HdPath = "1/2/3/4/5",
            };
        }

        public static TransactionOutputData CreateTransaction(uint256 id, Money amount, int? blockHeight, SpendingDetails spendingDetails = null, DateTimeOffset? creationTime = null)
        {
            if (creationTime == null)
            {
                creationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3));
            }

            return new TransactionOutputData
            {
                Amount = amount,
                Id = id,
                CreationTime = creationTime.Value,
                BlockHeight = blockHeight,
                SpendingDetails = spendingDetails
            };
        }

        public static HdAddress CreateAddress(bool changeAddress = false)
        {
            string hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress
            {
                Address = key.PubKey.GetAddress(KnownNetworks.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }
    }
}
