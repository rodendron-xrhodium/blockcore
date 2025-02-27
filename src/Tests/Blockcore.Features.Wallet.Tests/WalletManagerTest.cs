﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.AsyncWork;
using Blockcore.Configuration;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.Wallet.Database;
using Blockcore.Features.Wallet.Exceptions;
using Blockcore.Features.Wallet.Helpers;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Features.Wallet.Types;
using Blockcore.Interfaces;
using Blockcore.Networks;
using Blockcore.Networks.XRC;
using Blockcore.Tests.Common;
using Blockcore.Tests.Common.Logging;
using Blockcore.Tests.Wallet.Common;
using Blockcore.Utilities;
using Blockcore.Utilities.JsonConverters;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NLog.Time;
using Xunit;

namespace Blockcore.Features.Wallet.Tests
{
    public class WalletManagerTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        private readonly IBlockStore blockStore;
        private readonly WalletFixture walletFixture;

        public WalletManagerTest(WalletFixture walletFixture)
        {
            this.blockStore = new Mock<IBlockStore>().Object;
            this.walletFixture = walletFixture;
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateMultisigXRCWalletWithSeed()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            this.Network = new XRCMain();
            var chain = new ChainIndexer(this.Network);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            var xPubs = new List<string>()
            {
                "xpub6BjqcxqZxmEMszvyvzhwDZbEcLpLsQCyWrCQjmnLJ3BvXHwwiPNH1W3jmF882W9jsJM5vGspDTzetnXwSy4P4qDgvYR46Adn11PUBvQAW1T", 
                "xpub6Cnh2EhE9yAggVwQA7mAy2Reh7hP9VsqVMa7FTBckKUYKwHvi1HPJcqwkuunA5V3LRhCvx8EeKdKoxSRFD2GJQeAd7wawJC1jqJvD2HeP8W"
            };

            var recieveRedeemScripts = new string[]
            {
                "522102af76842fc756db497b984f14ccfa6f0d16c86cf5f3f8a4d2711ffb1d4a1eb4fe210300037e7d78e4f52d4431a586335c16eae6ce0f1afebe841231d913b6fe45a2af21034ec89eb5faa3638ec6b93c24ab75e9f02d11289ee519809cb187bb71cee4e02d53ae",
                "522102613dc4f647b8feb0907fa6ba9f8dcb0a9319c93d80653a7858f156a6dc3501b221031384c54a42164e0c70a40f9b97b1a4922ca778fd6d43fac669c9b84d7edc4c152103c7d4e479dcb45a26f7f6911e2f66fc3d11ef6170d4f763fab6c4520921f700e853ae",
                "5221024a7088f372ca162025f3ddbad6c99ed4d828d9c819c368caa3bc42450f44fb092102784c59f90d06fef61d4fb04a9275ca29219821938240f104bddd6dca2ec48de5210280d767323327d01aa05458ea166c0d74431c778505a2ea7d870b07167c8a4dfc53ae",
                "52210278f2fcb9987d3f8d6a1ab5fe5696379b7cff8c0f1e5184bc4861a7f89e22241b2102f7d134704d07d2cc6f9d50afbba71e44ea71999f705656908b0e4e16615cd6f221036e26d114735ac210e698fe525ddfd47df3fd4ba005c6174395f4e5ea5e98249353ae",
                "5221020c82ebeaf0420684d377899968715dc10308b011748d8c160c242d485cd90f36210375095b989a1d0100962d38b8f6ebbefff726350e1a0dc7602fb7afb119df45fd2103ca9111db12a21bf89106339a99a69b68047e421f3a42b943f9b19bf52520fe4b53ae",
            };

            var changeRedeemScripts = new string[]
            {
                "5221023bc1b977890204ab69eced05b5c6259db7555ab95116b838f950e350806c15d92102910c7e86a0cc485dc992caef88a78f919b8c74b281c5f5a4431bbc541361c51f21029fe588173670c6c28d49a7cc6c1a0222a8dcc0609a0cdd98b0ab5492cc5ba78f53ae",
                "5221024026c42573ae2cfda5f22aa76bb774696707d3f5d58368578b5ee06e6c97bba9210265f1f62947b908156e9b5b8ee64dd9ce92ee1e53467dac154d819ca82f2407a921027d59cfb9b1f7d61eeeb1442003d110a0ec6de021099a494b235b20ad746301c753ae",
                "5221024c573939f84a4f25c38931be3b549ddcb8aeab5a8c3495c8b3ffc26974c752a8210308c910a1363afdd5b26f2584512cd1c7eb368caecb403ec64e97726e4aafac0b2103471d2ca002d7c3d677f5df243f37cecb0590c75cec09a6e6f60c5981a894f75a53ae",
                "522102b5473119cc06852b76ae0b7df36b1c1d862254b6ddde2b9d3b200925aa895c1b2102e113754108029c015c8b9cb5c24a78904ca050c54f2a08d8f80c85d593b7cb662103db1afa7afac4020291b726fc7435e1bdd1e0084050e896c948a17672872a883153ae",
                "522102148f8f6cd405defd54c7d397a98bb6a064c76daec9ef6e0eeac8b2137782b30b21034d797d8bfdb35d5634d6a7251a829835d155e822b3e2bed9932afd3f2e981012210361277992e5ba401846d53a02f9d4ca8cb94f01bd7f4ddf4a0ce0378af09c6e3b53ae",
            };

            string password = "bdemq1XLhLYbiGHD";
            string passphrase = "bdemq1XLhLYbiGHD";

            string seed = "chalk call anger chase endless level slow sleep coast left sand enter save bind curious puzzle stadium volume mixture shuffle hurry gas borrow believe";

            // create the wallet
            WalletMultisig expectedWallet = walletManager.CreateMutisigWallet("mywallet", 2, xPubs, 10291, seed, password, passphrase);

            // assert it has saved it to disk and has been created correctly.
            var actualWallet = walletManager.MultisigFileStorage.LoadByFileName("mywallet.multisigwallet.json");

            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(this.Network, expectedWallet.Network);

            Assert.Equal(expectedWallet.Name, actualWallet.Name);
            Assert.Equal(expectedWallet.Network, actualWallet.Network);
            Assert.True(actualWallet.IsMultisig);
            Assert.Equal(expectedWallet.EncryptedSeed, actualWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, actualWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (var i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(10291, expectedWallet.AccountsRoot.FirstOrDefault().CoinType);
                Assert.Equal(1, expectedWallet.AccountsRoot.FirstOrDefault().LastBlockSyncedHeight);
                Assert.Equal(block.GetHash(), expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, actualWallet.AccountsRoot.ElementAt(i).CoinType);

                // blockcore wallet does not serialize these, because this has explicit json ignore messages.
                Assert.Null(actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);
                Assert.Null(actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);

                var accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Equal(1, accountRoot.Accounts.Count);

                for (var j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    var actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/10291'/{j}'", actualAccount.HdPath);


                    Assert.Equal("N/A", actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (var k = 0; k < 5; k++)
                    {
                        var actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        Script expectedAddressPubKey = Script.FromBytesUnsafe(Encoders.Hex.DecodeData(changeRedeemScripts[k]));
                        var expectedAddress = expectedAddressPubKey.Hash.GetAddress(expectedWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/10291'/{j}'/1/{k}", actualAddress.HdPath);
                        //Assert.Equal(0, actualAddress.Transactions.Count);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (var l = 0; l < 5; l++)
                    {
                        var actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        Script expectedAddressPubKey = Script.FromBytesUnsafe(Encoders.Hex.DecodeData(recieveRedeemScripts[l]));
                        var expectedAddress = expectedAddressPubKey.Hash.GetAddress(expectedWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/10291'/{j}'/0/{l}", actualAddress.HdPath);
                        //Assert.Equal(0, actualAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            //blockore wallets have explicit jsonignore directives at time of writing this
            Assert.Null(actualWallet.BlockLocator);
        }
        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithoutPassphraseOrMnemonicCreatesWalletUsingPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            string password = "test";
            string passphrase = "test";

            // create the wallet
            Mnemonic mnemonic = walletManager.CreateWallet(password, "mywallet", passphrase);

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(dataFolder.WalletPath + "/mywallet.wallet.json"));

            Types.Wallet actualWallet = walletManager.Wallets.ElementAt(0);

            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(KnownNetworks.StratisMain, expectedWallet.Network);

            Assert.Equal(expectedWallet.Name, actualWallet.Name);
            Assert.Equal(expectedWallet.Network, actualWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, actualWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, actualWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (int i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(KnownCoinTypes.Stratis, expectedWallet.AccountsRoot.ElementAt(i).CoinType);

                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, actualWallet.AccountsRoot.ElementAt(i).CoinType);

                IAccountRoot accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Equal(1, accountRoot.Accounts.Count);

                for (int j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    IHdAccount actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/105'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(expectedWallet.EncryptedSeed, "test", expectedWallet.Network), expectedWallet.ChainCode);
                    string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/105'/{j}'")).Neuter().ToString(expectedWallet.Network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (int k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/1/{k}", actualAddress.HdPath);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (int l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/0/{l}", actualAddress.HdPath);
                    }
                }
            }

            Assert.Equal(actualWallet.EncryptedSeed, mnemonic.DeriveExtKey(password).PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.StratisMain).ToWif());
            Assert.Equal(expectedWallet.EncryptedSeed, mnemonic.DeriveExtKey(password).PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.StratisMain).ToWif());
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithPasswordAndPassphraseCreatesWalletUsingPasswordAndPassphrase()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            string password = "test";
            string passphrase = "this is my magic passphrase";

            // create the wallet
            Mnemonic mnemonic = walletManager.CreateWallet(password, "mywallet", passphrase);
            Types.Wallet actualWallet = walletManager.Wallets.ElementAt(0);

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(dataFolder.WalletPath + "/mywallet.wallet.json"));

            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(KnownNetworks.StratisMain, expectedWallet.Network);

            Assert.Equal(expectedWallet.Name, actualWallet.Name);
            Assert.Equal(expectedWallet.Network, actualWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, actualWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, actualWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (int i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(KnownCoinTypes.Stratis, expectedWallet.AccountsRoot.ElementAt(i).CoinType);

                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, actualWallet.AccountsRoot.ElementAt(i).CoinType);

                IAccountRoot accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Equal(1, accountRoot.Accounts.Count);

                for (int j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    IHdAccount actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/105'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(expectedWallet.EncryptedSeed, "test", expectedWallet.Network), expectedWallet.ChainCode);
                    string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/105'/{j}'")).Neuter().ToString(expectedWallet.Network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (int k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/1/{k}", actualAddress.HdPath);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (int l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/0/{l}", actualAddress.HdPath);
                    }
                }
            }

            Assert.Equal(actualWallet.EncryptedSeed, mnemonic.DeriveExtKey(passphrase).PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.StratisMain).ToWif());
            Assert.Equal(expectedWallet.EncryptedSeed, mnemonic.DeriveExtKey(passphrase).PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.StratisMain).ToWif());
        }

        [Fact]
        public void CreateWalletWithMnemonicListCreatesWalletUsingMnemonicList()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                                                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            string password = "test";
            string passphrase = "this is my magic passphrase";

            var mnemonic = new Mnemonic(Wordlist.French, WordCount.Eighteen);

            Mnemonic returnedMnemonic = walletManager.CreateWallet(password, "mywallet", passphrase, mnemonic);

            Assert.Equal(mnemonic.DeriveSeed(), returnedMnemonic.DeriveSeed());
        }

        [Fact]
        public void CreateWalletWithWalletSetting100UnusedAddressBufferCreates100AddressesToMonitor()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = this.CreateWalletManager(dataFolder, this.Network, "-walletaddressbuffer=100");

            walletManager.CreateWallet("test", "mywallet", "this is my magic passphrase", new Mnemonic(Wordlist.English, WordCount.Eighteen));

            IHdAccount hdAccount = walletManager.Wallets.Single().AccountsRoot.Single().Accounts.Single();

            Assert.Equal(100, hdAccount.ExternalAddresses.Count);
            Assert.Equal(100, hdAccount.InternalAddresses.Count);
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                                                  dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            var concurrentChain = new ChainIndexer(this.Network);
            ChainedHeader tip = WalletTestsHelpers.AppendBlock(this.Network, null, concurrentChain).ChainedHeader;

            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet2"));

            Parallel.For(0, 500, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.UpdateLastBlockSyncedHeight(tip);
                walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet"));
                walletManager.UpdateLastBlockSyncedHeight(tip);
            });

            Assert.Equal(502, walletManager.Wallets.Count);
            Assert.True(walletManager.Wallets.All(w => w.BlockLocator != null));
        }

        [Fact]
        public void LoadWalletWithExistingWalletLoadsWalletOntoManager()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");

            File.WriteAllText(Path.Combine(dataFolder.WalletPath, "testWallet.wallet.json"), JsonConvert.SerializeObject(wallet, Formatting.Indented, new ByteArrayConverter()));

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            Types.Wallet result = walletManager.LoadWallet("password", "testWallet");

            Assert.Equal("testWallet", result.Name);
            Assert.Equal(this.Network, result.Network);

            Assert.Single(walletManager.Wallets);
            Assert.Equal("testWallet", walletManager.Wallets.ElementAt(0).Name);
            Assert.Equal(this.Network, walletManager.Wallets.ElementAt(0).Network);
        }

        [Fact]
        public void LoadWalletWithNonExistingWalletThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);

                var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                                                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

                walletManager.LoadWallet("password", "testWallet");
            });
        }

        [Fact]
        public void RecoverWalletWithEqualInputAsExistingWalletRecoversWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            string password = "test";
            string passphrase = "this is my magic passphrase";
            string walletName = "mywallet";

            ChainIndexer chainIndexer = WalletTestsHelpers.PrepareChainWithBlock();
            // Prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            (Mnemonic mnemonic, Types.Wallet wallet) deletedWallet = this.CreateWalletOnDiskAndDeleteWallet(dataFolder, password, passphrase, walletName, chainIndexer);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            // create a fresh manager.
            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // Try to recover it.
            Types.Wallet recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), passphrase);

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            Types.Wallet expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (int i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                IAccountRoot recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                IAccountRoot expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);

                Assert.Equal(1, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(1, expectedAccountRoot.Accounts.Count);

                for (int j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    IHdAccount expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    IHdAccount recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (int k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        HdAddress recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (int l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        HdAddress recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                    }
                }
            }

            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
        }

        [Fact]
        public void RecoverWalletOnlyWithPasswordWalletRecoversWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            string password = "test";
            string walletName = "mywallet";

            ChainIndexer chainIndexer = WalletTestsHelpers.PrepareChainWithBlock();
            // prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            (Mnemonic mnemonic, Types.Wallet wallet) deletedWallet = this.CreateWalletOnDiskAndDeleteWallet(dataFolder, password, password, walletName, chainIndexer);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            // create a fresh manager.
            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // try to recover it.
            Types.Wallet recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), password);

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            Types.Wallet expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (int i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                IAccountRoot recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                IAccountRoot expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);

                Assert.Equal(1, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(1, expectedAccountRoot.Accounts.Count);

                for (int j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    IHdAccount expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    IHdAccount recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (int k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        HdAddress recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (int l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        HdAddress recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                    }
                }
            }

            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
        }

        [Fact]
        public void LoadKeysLookupInParallelDoesNotThrowInvalidOperationException()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet2"));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet3"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            Parallel.For(0, 5000, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.LoadKeysLookup();
                walletManager.LoadKeysLookup();
                walletManager.LoadKeysLookup();
            });

            Assert.Equal(240, walletManager.walletIndex.Values.SelectMany(s => s.ScriptToAddressLookup.Values).Count());
        }

        [Fact]
        public void GetUnusedAccountUsingNameForNonExistinAccountThrowsWalletException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

                walletManager.GetUnusedAccount("nonexisting", "password");
            });
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });
            walletManager.Wallets.Add(wallet);

            IHdAccount result = walletManager.GetUnusedAccount("testWallet", "password");

            Assert.Equal("unused", result.Name);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            walletManager.Wallets.Add(wallet);

            IHdAccount result = walletManager.GetUnusedAccount("testWallet", "password");

            Assert.Equal("account 0", result.Name);

            int addressBuffer = new WalletSettings(NodeSettings.Default(this.Network)).UnusedAddressesBuffer;
            Assert.Equal(addressBuffer, result.ExternalAddresses.Count);
            Assert.Equal(addressBuffer, result.InternalAddresses.Count);
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });
            walletManager.Wallets.Add(wallet);

            IHdAccount result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("unused", result.Name);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            walletManager.Wallets.Add(wallet);

            IHdAccount result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("account 0", result.Name);
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void CreateNewAccountGivenNoAccountsExistingInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();

            IHdAccount result = wallet.AddNewAccount("password", DateTimeOffset.UtcNow);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 0", result.Name);
            Assert.Equal(0, result.Index);
            Assert.Equal($"m/44'/0'/0'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(0, result.InternalAddresses.Count);
            Assert.Equal(0, result.ExternalAddresses.Count);
        }

        [Fact]
        public void CreateNewAccountGivenExistingAccountInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            Network network = this.Network;
            var walletManager = new WalletManager(this.LoggerFactory.Object, network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });

            IHdAccount result = wallet.AddNewAccount("password", DateTimeOffset.UtcNow);

            Assert.Equal(2, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/1'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 1", result.Name);
            Assert.Equal(1, result.Index);
            Assert.Equal($"m/44'/0'/1'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(0, result.InternalAddresses.Count);
            Assert.Equal(0, result.ExternalAddresses.Count);
        }

        [Fact]
        public void GetUnusedAddressUsingNameWithWalletWithoutAccountOfGivenNameThrowsException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
                walletManager.Wallets.Add(wallet);

                HdAddress result = walletManager.GetUnusedAddress(new WalletAccountReference("testWallet", "unexistingAccount"));
            });
        }

        [Fact]
        public void GetUnusedAddressUsingNameForNonExistinAccountThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

                walletManager.GetUnusedAddress(new WalletAccountReference("nonexisting", "account"));
            });
        }

        [Fact]
        public void GetUnusedAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                ExternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = "myUsedAddress",
                        //Transactions = new List<TransactionData>
                        //{
                        //    new TransactionData()
                        //}
                    },
                     new HdAddress {
                        Index = 1,
                        Address = "myUnusedAddress",
                        //Transactions = new List<TransactionData>()
                    }
                },
                InternalAddresses = null
            });
            walletManager.Wallets.Add(wallet);
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Address = "myUsedAddress" });

            HdAddress result = walletManager.GetUnusedAddress(new WalletAccountReference("myWallet", "myAccount"));

            Assert.Equal("myUnusedAddress", result.Address);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            var bob = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            var alice = new BitcoinSecret(new Key(), KnownNetworks.RegTest);
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                InternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = bob.GetAddress().ToString(),
                        ScriptPubKey = bob.ScriptPubKey,
                        //Transactions = new List<TransactionData>
                        //{
                        //    new TransactionData()
                        //}
                    },
                    new HdAddress {
                        Index = 1,
                        Address = alice.GetAddress().ToString(),
                        ScriptPubKey = alice.ScriptPubKey,
                       //Transactions = new List<TransactionData>()
                    }
                },
                ExternalAddresses = new List<HdAddress>()
            });
            walletManager.Wallets.Add(wallet);
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Address = bob.GetAddress().ToString() });

            HdAddress result = walletManager.GetUnusedChangeAddress(new WalletAccountReference(wallet.Name, wallet.AccountsRoot.Single().Accounts.First().Name));

            Assert.Equal(alice.GetAddress().ToString(), result.Address);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletNotHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            string accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountExtendedPubKey,
                InternalAddresses = new List<HdAddress>(),
                ExternalAddresses = new List<HdAddress>()
            });
            walletManager.Wallets.Add(wallet);

            HdAddress result = walletManager.GetUnusedChangeAddress(new WalletAccountReference(wallet.Name, wallet.AccountsRoot.Single().Accounts.First().Name));

            Assert.NotNull(result.Address);
        }

        [Fact]
        public void GetUnusedAddressWithoutWalletHavingUnusedAddressCreatesAddressAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            string accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = "myUsedAddress",
                        ScriptPubKey = new Script(),
                        //Transactions = new List<TransactionData>
                        //{
                        //    new TransactionData()
                        //},
                    }
                },
                InternalAddresses = new List<HdAddress>(),
                ExtendedPubKey = accountExtendedPubKey
            });
            walletManager.Wallets.Add(wallet);

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Address = "myUsedAddress" });

            HdAddress result = walletManager.GetUnusedAddress(new WalletAccountReference("myWallet", "myAccount"));

            var keyPath = new KeyPath($"0/1");
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtendedPubKey).Derive(keyPath);
            PubKey pubKey = extPubKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(wallet.Network);
            Assert.Equal(1, result.Index);
            Assert.Equal("m/44'/0'/0'/0/1", result.HdPath);
            Assert.Equal(address.ToString(), result.Address);
            Assert.Equal(pubKey.ScriptPubKey, result.Pubkey);
            Assert.Equal(address.ScriptPubKey, result.ScriptPubKey);
            Assert.Empty(wallet.walletStore.GetForAddress(result.Address));
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/myWallet.wallet.json")));
        }

        [Fact]
        public void GetHistoryByNameWithExistingWalletReturnsAllAddressesWithTransactions()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedExternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedExternalAddress"),
                },
                InternalAddresses = new List<HdAddress> {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedInternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedInternalAddress"),
                },
                ExtendedPubKey = "blabla"
            });
            walletManager.Wallets.Add(wallet);

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Address = "myUsedExternalAddress" });
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), 1), Address = "myUsedInternalAddress" });

            List<AccountHistory> result = walletManager.GetHistory("myWallet").ToList();

            Assert.NotEmpty(result);
            Assert.Single(result);
            AccountHistory accountHistory = result.ElementAt(0);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            FlatHistory historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal("myUsedExternalAddress", historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal("myUsedInternalAddress", historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistorySlimByNameWithExistingWalletReturnsAllAddressesWithTransactions()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedExternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedExternalAddress"),
                },
                InternalAddresses = new List<HdAddress> {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedInternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedInternalAddress"),
                },
                ExtendedPubKey = "blabla"
            });
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Id = new uint256(1), Amount = 2, AccountIndex = 0, Address = "myUsedExternalAddress" });
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), 1), Id = new uint256(1), Amount = 2, AccountIndex = 0, Address = "myUsedInternalAddress" });
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(3), 1), Id = new uint256(2), Amount = 2, AccountIndex = 0, Address = "myUsedExternalAddress" });
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(4), 1), Id = new uint256(3), Amount = 2, AccountIndex = 0, Address = "myUsedInternalAddress" });

            List<AccountHistorySlim> result = walletManager.GetHistorySlim("myWallet").ToList();

            Assert.NotEmpty(result);
            Assert.Single(result);
            AccountHistorySlim accountHistory = result.ElementAt(0);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(3, accountHistory.History.Count());
        }

        [Fact]
        public void GetHistoryByAccountWithExistingAccountReturnsAllAddressesWithTransactions()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            var account = new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedExternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedExternalAddress"),
                },
                InternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedInternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedInternalAddress"),
                },
                ExtendedPubKey = "blabla"
            };

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(account);
            walletManager.Wallets.Add(wallet);

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Address = "myUsedExternalAddress" });
            wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), 1), Address = "myUsedInternalAddress" });

            AccountHistory accountHistory = walletManager.GetHistory(wallet, account);

            Assert.NotNull(accountHistory);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            FlatHistory historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal("myUsedExternalAddress", historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal("myUsedInternalAddress", historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistoryByAccountWithoutHavingAddressesWithTransactionsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            var account = new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                ExtendedPubKey = "blabla"
            };
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(account);
            walletManager.Wallets.Add(wallet);

            AccountHistory result = walletManager.GetHistory(wallet, account);

            Assert.NotNull(result.Account);
            Assert.Equal("myAccount", result.Account.Name);
            Assert.Empty(result.History);
        }

        [Fact]
        public void GetHistoryByWalletNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                walletManager.GetHistory("noname");
            });
        }

        [Fact]
        public void GetWalletByNameWithExistingWalletReturnsWallet()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            walletManager.Wallets.Add(wallet);

            Types.Wallet result = walletManager.GetWallet("myWallet");

            Assert.Equal(wallet.EncryptedSeed, result.EncryptedSeed);
        }

        [Fact]
        public void GetWalletByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                walletManager.GetWallet("noname");
            });
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletReturnsAccountsFromWallet()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "Account 0" });
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "Account 1" });

            walletManager.Wallets.Add(wallet);

            IEnumerable<IHdAccount> result = walletManager.GetAccounts("myWallet");

            Assert.Equal(2, result.Count());
            Assert.Equal("Account 0", result.ElementAt(0).Name);
            Assert.Equal("Account 1", result.ElementAt(1).Name);
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletMissingAccountsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.Clear();
            walletManager.Wallets.Add(wallet);

            IEnumerable<IHdAccount> result = walletManager.GetAccounts("myWallet");

            Assert.Empty(result);
        }

        [Fact]
        public void GetAccountsByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

                walletManager.GetAccounts("myWallet");
            });
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsReturnsChainTipHeight()
        {
            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(this.Network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            int result = walletManager.LastBlockHeight();

            Assert.Equal(chain.Tip.Height, result);
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsOfCoinTypeReturnsZero()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = KnownCoinTypes.Stratis;
            walletManager.Wallets.Add(wallet);

            int result = walletManager.LastBlockHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void LastReceivedBlockHashWithoutWalletsReturnsChainTipHashBlock()
        {
            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(this.Network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            uint256 result = walletManager.LastReceivedBlockInfo().Hash;

            Assert.Equal(chain.Tip.HashBlock, result);
        }

        [Fact]
        public void GetSpendableTransactionsWithChainOfHeightZeroReturnsNoTransactions()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(0, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 2, 9, 10)
            });

            walletManager.Wallets.Add(wallet);

            IEnumerable<UnspentOutputReference> result = walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);

            Assert.Empty(result);
        }

        /// <summary>
        /// If the block height of the transaction is x+ away from the current chain top transactions must be returned where x is higher or equal to the specified amount of confirmations.
        /// </summary>
        [Fact]
        public void GetSpendableTransactionsReturnsTransactionsGivenBlockHeight()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First expectation",
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 2, 9, 10)
            });

            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = KnownCoinTypes.Stratis,
                Accounts = new List<IHdAccount>
                {
                    new HdAccount {
                        ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, KnownNetworks.StratisMain, 8,9,10),
                        InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, KnownNetworks.StratisMain, 8,9,10)
                    }
                }
            });

            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");
            wallet2.AccountsRoot.ElementAt(0).CoinType = KnownCoinTypes.Stratis;
            wallet2.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet2.walletStore as WalletMemoryStore, KnownNetworks.StratisMain, 1, 3, 5, 7, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet2.walletStore as WalletMemoryStore, KnownNetworks.StratisMain, 2, 4, 6, 8, 9, 10)
            });

            Types.Wallet wallet3 = this.walletFixture.GenerateBlankWallet("myWallet3", "password");
            wallet3.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "Second expectation",
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet3.walletStore as WalletMemoryStore, this.Network, 5, 9, 11),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet3.walletStore as WalletMemoryStore, this.Network, 6, 9, 11)
            });

            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.Wallets.Add(wallet3);

            UnspentOutputReference[] result = walletManager.GetSpendableTransactionsInWallet("myWallet3", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            UnspentOutputReference info = result[0];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(5, info.Transaction.BlockHeight);
            info = result[1];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            info = result[2];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(6, info.Transaction.BlockHeight);
            info = result[3];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
        }

        [Fact]
        public void GetSpendableTransactionsWithSpentTransactionsReturnsSpendableTransactionsGivenBlockHeight()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First expectation",
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 9, 11).Concat(WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 9, 11)).ToList(),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 2, 9, 11).Concat(WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 2, 9, 11)).ToList()
            });

            walletManager.Wallets.Add(wallet);

            UnspentOutputReference[] result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            UnspentOutputReference info = result[0];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(1, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[1];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[2];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(2, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[3];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
        }

        [Fact]
        public void GetSpendableTransactionsWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

                walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);
            });
        }

        [Fact]
        public void GetSpendableTransactionsWithOnlySpentTransactionsReturnsEmptyList()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First expectation",
                ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 2, 9, 10)
            });

            walletManager.Wallets.Add(wallet);

            IEnumerable<UnspentOutputReference> result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1);

            Assert.Empty(result);
        }

        [Fact]
        public void GetKeyForAddressWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

                Types.Wallet wallet = walletManager.GetWalletByName("mywallet");
                Key key = wallet.GetExtendedPrivateKeyForAddress("password", new HdAddress()).PrivateKey;
            });
        }

        [Fact]
        public void GetKeyForAddressWithWalletReturnsAddressExtPrivateKey()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            (Types.Wallet wallet, ExtKey key) data = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = "m/44'/0'/0'/0/0",
            };

            data.wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                ExternalAddresses = new List<HdAddress> {
                    address
                },
                InternalAddresses = new List<HdAddress>(),
                Name = "savings account"
            });
            walletManager.Wallets.Add(data.wallet);

            ISecret result = data.wallet.GetExtendedPrivateKeyForAddress("password", address);

            Assert.Equal(data.key.Derive(new KeyPath("m/44'/0'/0'/0/0")).GetWif(data.wallet.Network), result);
        }

        [Fact]
        public void GetKeyForAddressWitoutAddressOnWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                (Types.Wallet wallet, ExtKey key) data = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password");

                var address = new HdAddress
                {
                    Index = 0,
                    HdPath = "m/44'/0'/0'/0/0",
                };

                data.wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
                {
                    Index = 0,
                    ExternalAddresses = new List<HdAddress>(),
                    InternalAddresses = new List<HdAddress>(),
                    Name = "savings account"
                });
                walletManager.Wallets.Add(data.wallet);

                data.wallet.GetExtendedPrivateKeyForAddress("password", address);
            });
        }

        [Fact]
        public void ProcessTransactionWithValidTransactionLoadsTransactionsIntoWalletIfMatching()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                //    Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(wallet.walletStore.GetForAddress(spendingAddress.Address));
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).DestinationScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).Count());
            TransactionOutputData destinationAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithEmptyScriptInTransactionDoesNotAddTransactionToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                //  Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                //  Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            transaction.Outputs.ElementAt(1).Value = Money.Zero;
            transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spendingAddress.Address).Count());
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.Count);

            Assert.Equal(0, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).Count());

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithDestinationToChangeAddressDoesNotAddTransactionAsPayment()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/1");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var destinationChangeAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/1/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress },
                InternalAddresses = new List<HdAddress> { changeAddress, destinationChangeAddress }
            });

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();

            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spendingAddress.Address).Count());
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(2, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.Count);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).BlockHeight);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData destinationAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address).ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithDestinationAsMultisigAddTransactionAsPayment()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            Script scriptToHash = new PayToScriptHashTemplate().GenerateScriptPubKey(new Key().PubKey.ScriptPubKey);
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, scriptToHash, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spendingAddress.Address).Count());
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).DestinationScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockHeightSetsBlockHeightOnTransactionData()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                //  Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();

            Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            int blockHeight = chainInfo.chain.GetHeader(block.GetHash()).Height;
            walletManager.ProcessTransaction(transaction, blockHeight);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spendingAddress.Address).Count());
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).DestinationScriptPubKey);
            Assert.Equal(blockHeight - 1, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).BlockHeight);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).Count());
            TransactionOutputData destinationAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockSetsBlockHash()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                //  Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();

            Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            walletManager.ProcessTransaction(transaction, block: block);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spendingAddress.Address).Count());
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).DestinationScriptPubKey);
            Assert.Equal(chainInfo.block.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).BlockHash);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).Count());
            TransactionOutputData destinationAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithoutMempoolValidatorProcessesTransactionAndBroadcastsTransactionToConnectionManagerNodes()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.True(result);
        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
        //        Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

        //        Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
        //        Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
        //        Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
        //        Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

        //        Assert.Equal(1, payloads.Count);
        //        Assert.Equal(typeof(TxPayload), payloads[0].GetType());

        //        var payload = payloads[0] as TxPayload;
        //        var payloadTransaction = payload.Object;
        //        Assert.Equal(transaction.ToHex(), payloadTransaction.ToHex());
        //}
        //}
        //}

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithMempoolValidatorWithAcceptToMemoryPoolSuccessProcessesTransaction()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.True(result);
        //        // verify AcceptToMemoryPool has been called.
        //        mempoolValidator.Verify();

        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
        //        Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

        //        Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
        //        Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
        //        Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
        //        Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

        //        Assert.Equal(1, payloads.Count);
        //        Assert.Equal(typeof(TxPayload), payloads[0].GetType());

        //        var payload = payloads[0] as TxPayload;
        //        var payloadTransaction = payload.Object;
        //        Assert.Equal(transaction.ToHex(), payloadTransaction.ToHex());
        //    }
        //}
        //}

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithMempoolValidatorWithAcceptToMemoryPoolFailedDoesNotProcessesTransaction()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.False(result);
        //        // verify AcceptToMemoryPool has been called.
        //        mempoolValidator.Verify();

        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Null(spentAddressResult.Transactions.ElementAt(0).SpendingDetails);
        //        Assert.Null(spentAddressResult.Transactions.ElementAt(0).SpendingDetails);
        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        Assert.Equal(0, payloads.Count);
        //    }
        //}
        //}

        [Fact]
        public void RemoveBlocksRemovesTransactionsWithHigherBlockHeightAndUpdatesLastSyncedBlockHeight()
        {
            uint256 trxId = uint256.Parse("21e74d1daed6dec93d58396a3406803c5fc8d220b59f4b4dd185cab5f7a9a22e");
            int trxCount = 0;
            var concurrentchain = new ChainIndexer(this.Network);
            ChainedHeader chainedHeader = WalletTestsHelpers.AppendBlock(this.Network, null, concurrentchain).ChainedHeader;
            chainedHeader = WalletTestsHelpers.AppendBlock(this.Network, chainedHeader, concurrentchain).ChainedHeader;
            chainedHeader = WalletTestsHelpers.AppendBlock(this.Network, chainedHeader, concurrentchain).ChainedHeader;

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First account",
                ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights((WalletMemoryStore)wallet.walletStore, this.Network, 1, 2, 3, 4, 5).ToList(),
                InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights((WalletMemoryStore)wallet.walletStore, this.Network, 1, 2, 3, 4, 5).ToList()
            });

            IHdAccount account = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);

            // reorg at block 3
            TransactionOutputData data = null;

            // Trx at block 0 is not spent
            data = wallet.walletStore.GetForAddress(account.ExternalAddresses.ElementAt(0).Address).First(); data.Id = trxId >> trxCount++; data.SpendingDetails = null; wallet.walletStore.InsertOrUpdate(data);
            data = wallet.walletStore.GetForAddress(account.InternalAddresses.ElementAt(0).Address).First(); data.Id = trxId >> trxCount++; data.SpendingDetails = null; wallet.walletStore.InsertOrUpdate(data);

            // Trx at block 2 is spent in block 3, after reorg it will not be spendable.
            data = wallet.walletStore.GetForAddress(account.ExternalAddresses.ElementAt(1).Address).First(); data.SpendingDetails.TransactionId = trxId >> trxCount++; data.SpendingDetails.BlockHeight = 3; wallet.walletStore.InsertOrUpdate(data);
            data = wallet.walletStore.GetForAddress(account.InternalAddresses.ElementAt(1).Address).First(); data.SpendingDetails.TransactionId = trxId >> trxCount++; data.SpendingDetails.BlockHeight = 3; wallet.walletStore.InsertOrUpdate(data);

            // Trx at block 3 is spent at block 5, after reorg it will be spendable.
            data = wallet.walletStore.GetForAddress(account.ExternalAddresses.ElementAt(2).Address).First(); data.SpendingDetails.TransactionId = trxId >> trxCount++; data.SpendingDetails.BlockHeight = 5; wallet.walletStore.InsertOrUpdate(data);
            data = wallet.walletStore.GetForAddress(account.InternalAddresses.ElementAt(2).Address).First(); data.SpendingDetails.TransactionId = trxId >> trxCount++; data.SpendingDetails.BlockHeight = 5; wallet.walletStore.InsertOrUpdate(data);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();
            walletManager.RemoveBlocks(chainedHeader);

            Assert.Equal(chainedHeader.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedHeader.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedHeader.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedHeader.HashBlock, walletManager.WalletTipHash);

            Assert.Equal(6, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => wallet.walletStore.GetForAddress(r.Address)).Count());
            Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => wallet.walletStore.GetForAddress(r.Address)).All(r => r.BlockHeight <= chainedHeader.Height));
            Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => wallet.walletStore.GetForAddress(r.Address)).All(r => r.SpendingDetails == null || r.SpendingDetails.BlockHeight <= chainedHeader.Height));
            Assert.Equal(4, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => wallet.walletStore.GetForAddress(r.Address)).Count(t => t.SpendingDetails == null));
        }

        [Fact]
        public void ProcessBlockWithoutWalletsSetsWalletTipToBlockHash()
        {
            var concurrentchain = new ChainIndexer(this.Network);
            (ChainedHeader ChainedHeader, Block Block) blockResult = WalletTestsHelpers.AppendBlock(this.Network, null, concurrentchain);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            walletManager.ProcessBlock(blockResult.Block, blockResult.ChainedHeader);

            Assert.Equal(blockResult.ChainedHeader.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletsProcessesTransactionsOfBlockToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);

            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            // setup a payment to yourself in a new block.
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();

            ChainedHeader chainedBlock = chainInfo.chain.GetHeader(block.GetHash());
            walletManager.WalletTipHash = block.Header.GetHash();
            walletManager.WalletTipHeight = chainedBlock.Height;

            walletManager.ProcessBlock(block, chainedBlock);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, wallet.walletStore.GetForAddress(spendingAddress.Address).Count());
            Assert.Equal(transaction.GetHash(), wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, wallet.walletStore.GetForAddress(spentAddressResult.Address).ElementAt(0).SpendingDetails.Payments.ElementAt(1).DestinationScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).Count());
            TransactionOutputData destinationAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address).ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).Count());
            TransactionOutputData changeAddressResult = wallet.walletStore.GetForAddress(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address).ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletTipBlockNotOnChainYetThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

                var chain = new ChainIndexer(wallet.Network);
                (ChainedHeader ChainedHeader, Block Block) chainResult = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                walletManager.Wallets.Add(wallet);

                walletManager.WalletTipHash = new uint256(15012522521);

                walletManager.ProcessBlock(chainResult.Block, chainResult.ChainedHeader);
            });
        }

        [Fact]
        public void ProcessBlockWithBlockAheadOfWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

                var chain = new ChainIndexer(wallet.Network);
                (ChainedHeader ChainedHeader, Block Block) chainResult = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain);
                (ChainedHeader ChainedHeader, Block Block) chainResult2 = WalletTestsHelpers.AppendBlock(this.Network, chainResult.ChainedHeader, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                walletManager.Wallets.Add(wallet);

                walletManager.WalletTipHash = wallet.Network.GetGenesis().Header.GetHash();

                walletManager.ProcessBlock(chainResult2.Block, chainResult2.ChainedHeader);
            });
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            IHdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();
            WalletMemoryStore store = new WalletMemoryStore();

            // add two unconfirmed transactions
            for (int i = 1; i < 3; i++)
            {
                store.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                store.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            Assert.Equal(0, firstAccount.GetBalances(store, firstAccount.IsNormalAccount()).ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetBalances(store, firstAccount.IsNormalAccount()).UnConfirmedAmount);
        }

        [Fact]
        public void GetAccountBalancesReturnsCorrectAccountBalances()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            // Initialize chain object.
            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            WalletMemoryStore store = new WalletMemoryStore();

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1", 1);
            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, accountIndex: account.Index, address: accountAddress1.Address));
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1, accountIndex: account.Index, address: accountAddress1.Address));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null, accountIndex: account.Index, address: accountAddress2.Address));
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 2, accountIndex: account.Index, address: accountAddress2.Address));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            HdAccount account2 = WalletTestsHelpers.CreateAccount("account 2", 2);
            HdAddress account2Address1 = WalletTestsHelpers.CreateAddress();
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(5), new Money(74000), null, accountIndex: account2.Index, address: account2Address1.Address));
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(6), new Money(18700), 3, accountIndex: account2.Index, address: account2Address1.Address));

            HdAddress account2Address2 = WalletTestsHelpers.CreateAddress();
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(7), new Money(65000), null, accountIndex: account2.Index, address: account2Address2.Address));
            store.InsertOrUpdate(WalletTestsHelpers.CreateTransaction(new uint256(8), new Money(89300), 4, accountIndex: account2.Index, address: account2Address2.Address));

            account2.ExternalAddresses.Add(account2Address1);
            account2.InternalAddresses.Add(account2Address2);

            var accounts = new List<IHdAccount> { account, account2 };

            Types.Wallet wallet = WalletTestsHelpers.CreateWallet("myWallet");
            wallet.walletStore = store;
            wallet.AccountsRoot.Add(new AccountRoot());
            wallet.AccountsRoot.Single().Accounts = accounts;

            walletManager.Wallets.Add(wallet);

            // Act.
            IEnumerable<AccountBalance> balances = walletManager.GetBalances("myWallet");

            // Assert.
            AccountBalance resultingBalance = balances.First();
            Assert.Equal(account.Name, resultingBalance.Account.Name);
            Assert.Equal(account.HdPath, resultingBalance.Account.HdPath);
            Assert.Equal(new Money(130000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(35000), resultingBalance.AmountUnconfirmed);

            resultingBalance = balances.ElementAt(1);
            Assert.Equal(account2.Name, resultingBalance.Account.Name);
            Assert.Equal(account2.HdPath, resultingBalance.Account.HdPath);
            Assert.Equal(new Money(108000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(139000), resultingBalance.AmountUnconfirmed);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithUnConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.walletStore = new WalletMemoryStore();
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            IHdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two confirmed transactions
            for (int i = 1; i < 3; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, BlockHeight = 10, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, BlockHeight = 10, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            Assert.Equal(40, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.walletStore = new WalletMemoryStore();
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            IHdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails { TransactionId = new uint256(1) }, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails { TransactionId = new uint256(1) }, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            Assert.Equal(0, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.walletStore = new WalletMemoryStore();
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            IHdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails { TransactionId = new uint256(1) }, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails { TransactionId = new uint256(1) }, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            for (int i = 3; i < 5; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, BlockHeight = 10, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, BlockHeight = 10, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            Assert.Equal(40, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndUnConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.walletStore = new WalletMemoryStore();
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            IHdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails { TransactionId = new uint256(1) }, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails { TransactionId = new uint256(1) }, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            for (int i = 3; i < 5; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            Assert.Equal(0, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetBalances(wallet.walletStore, firstAccount.IsNormalAccount()).UnConfirmedAmount);
        }

        [Fact]
        public void SaveToFileWithoutWalletParameterSavesAllWalletsOnManagerToDisk()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            walletManager.SaveWallets();

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            var resultWallet = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);

            var resultWallet2 = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));
            Assert.Equal(wallet2.Name, resultWallet2.Name);
            Assert.Equal(wallet2.EncryptedSeed, resultWallet2.EncryptedSeed);
            Assert.Equal(wallet2.ChainCode, resultWallet2.ChainCode);
            Assert.Equal(wallet2.Network, resultWallet2.Network);
            Assert.Equal(wallet2.AccountsRoot.Count, resultWallet2.AccountsRoot.Count);
        }

        [Fact]
        public void SaveToFileWithWalletParameterSavesGivenWalletToDisk()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            walletManager.SaveWallet(wallet);

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            var resultWallet = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);
        }

        [Fact]
        public void GetWalletFileExtensionReturnsWalletExtension()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            string result = walletManager.GetWalletFileExtension();

            Assert.Equal("wallet.json", result);
        }

        [Fact]
        public void GetWalletsReturnsLoadedWalletNames()
        {
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            string[] result = walletManager.GetWalletsNames().OrderBy(w => w).ToArray();

            Assert.Equal(2, result.Count());
            Assert.Equal("wallet1", result[0]);
            Assert.Equal("wallet2", result[1]);
        }

        [Fact]
        public void GetWalletsWithoutLoadedWalletsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            IOrderedEnumerable<string> result = walletManager.GetWalletsNames().OrderBy(w => w);

            Assert.Empty(result);
        }

        [Fact]
        public void LoadKeysLookupWithKeysLoadsKeyLookup()
        {
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First account",
                ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 2, 3).ToList(),
                InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(wallet.walletStore as WalletMemoryStore, this.Network, 1, 2, 3).ToList()
            });

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);

            walletManager.LoadKeysLookup();

            Assert.NotNull(walletManager.walletIndex);
            Assert.Equal(6, walletManager.walletIndex.Values.Sum(s => s.ScriptToAddressLookup.Count));

            ICollection<HdAddress> externalAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses;
            Assert.Equal(externalAddresses.ElementAt(0).Address, walletManager.walletIndex[wallet.Name].ScriptToAddressLookup[externalAddresses.ElementAt(0).ScriptPubKey].Address);
            Assert.Equal(externalAddresses.ElementAt(1).Address, walletManager.walletIndex[wallet.Name].ScriptToAddressLookup[externalAddresses.ElementAt(1).ScriptPubKey].Address);
            Assert.Equal(externalAddresses.ElementAt(2).Address, walletManager.walletIndex[wallet.Name].ScriptToAddressLookup[externalAddresses.ElementAt(2).ScriptPubKey].Address);

            ICollection<HdAddress> internalAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses;
            Assert.Equal(internalAddresses.ElementAt(0).Address, walletManager.walletIndex[wallet.Name].ScriptToAddressLookup[internalAddresses.ElementAt(0).ScriptPubKey].Address);
            Assert.Equal(internalAddresses.ElementAt(1).Address, walletManager.walletIndex[wallet.Name].ScriptToAddressLookup[internalAddresses.ElementAt(1).ScriptPubKey].Address);
            Assert.Equal(internalAddresses.ElementAt(2).Address, walletManager.walletIndex[wallet.Name].ScriptToAddressLookup[internalAddresses.ElementAt(2).ScriptPubKey].Address);
        }

        [Fact]
        public void LoadKeysLookupWithoutWalletsInitializesEmptyDictionary()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            walletManager.LoadKeysLookup();

            Assert.NotNull(walletManager.walletIndex);
            Assert.Empty(walletManager.walletIndex.Values.SelectMany(s => s.ScriptToAddressLookup.Values));
        }

        [Fact]
        public void CreateBip44PathWithChangeAddressReturnsPath()
        {
            string result = HdOperations.CreateHdPath((int)KnownCoinTypes.Stratis, 4, true, 3);

            Assert.Equal("m/44'/105'/4'/1/3", result);
        }

        [Fact]
        public void CreateBip44PathWithoutChangeAddressReturnsPath()
        {
            string result = HdOperations.CreateHdPath((int)KnownCoinTypes.Stratis, 4, false, 3);

            Assert.Equal("m/44'/105'/4'/0/3", result);
        }

        [Fact]
        public void StopSavesWallets()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            walletManager.Stop();

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            var resultWallet = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);

            var resultWallet2 = JsonConvert.DeserializeObject<Types.Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));
            Assert.Equal(wallet2.Name, resultWallet2.Name);
            Assert.Equal(wallet2.EncryptedSeed, resultWallet2.EncryptedSeed);
            Assert.Equal(wallet2.ChainCode, resultWallet2.ChainCode);
            Assert.Equal(wallet2.Network, resultWallet2.Network);
            Assert.Equal(wallet2.AccountsRoot.Count, resultWallet2.AccountsRoot.Count);
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithChainedBlockUpdatesWallets()
        {
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");

            var chain = new ChainIndexer(wallet.Network);
            ChainedHeader chainedBlock = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain).ChainedHeader;

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(chainedBlock);

            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
            foreach (Types.Wallet w in walletManager.Wallets)
            {
                Assert.Equal(chainedBlock.GetLocator().Blocks, w.BlockLocator);
                Assert.Equal(chainedBlock.Height, w.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
                Assert.Equal(chainedBlock.HashBlock, w.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            }
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithWalletAndChainedBlockUpdatesGivenWallet()
        {
            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            Types.Wallet wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");

            var chain = new ChainIndexer(wallet.Network);
            ChainedHeader chainedBlock = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain).ChainedHeader;

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(wallet, chainedBlock);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.NotEqual(chainedBlock.HashBlock, walletManager.WalletTipHash);

            Assert.NotEqual(chainedBlock.GetLocator().Blocks, wallet2.BlockLocator);
            Assert.NotEqual(chainedBlock.Height, wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.NotEqual(chainedBlock.HashBlock, wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
        }

        [Fact]
        public void RemoveAllTransactionsInWalletReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // Generate a wallet with an account and a few transactions.
            Types.Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            wallet.walletStore = new WalletMemoryStore();
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);
            walletManager.LoadKeysLookup();

            IHdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            for (int i = 0; i < 3; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, Id = trxId >> counter++, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, Id = trxId >> counter++, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
            }

            // Add two confirmed transactions.
            for (int i = 3; i < 6; i++)
            {
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), i), Amount = 10, Id = trxId >> counter++, Address = firstAccount.InternalAddresses.ElementAt(i).Address });
                wallet.walletStore.InsertOrUpdate(new TransactionOutputData { OutPoint = new OutPoint(new uint256(2), i), Amount = 10, Id = trxId >> counter++, Address = firstAccount.ExternalAddresses.ElementAt(i).Address });
            }

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)).Count();
            Assert.Equal(12, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Empty(firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)));
            Assert.Equal(12, result.Count);
        }

        [Fact]
        public void RemoveAllTransactionsWhenNoTransactionsArePresentReturnsEmptyList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // Generate a wallet with an account and no transactions.
            Types.Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            wallet.walletStore = new WalletMemoryStore();

            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);
            walletManager.LoadKeysLookup();

            IHdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)).Count();
            Assert.Equal(0, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Empty(firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)));
            Assert.Empty(result);
        }

        [Fact]
        public void RemoveTransactionsByIdsWhenTransactionsAreUnconfirmedReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // Generate a wallet with an account and a few transactions.
            Types.Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            wallet.walletStore = new WalletMemoryStore();
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);
            walletManager.LoadKeysLookup();

            IHdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;
            DateTimeOffset creationTime = default;

            var trxUnconfirmed1 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Amount = 10, Id = trxId >> counter++, Address = firstAccount.ExternalAddresses.ElementAt(0).Address, CreationTime = creationTime };
            var trxUnconfirmed2 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 2), Amount = 10, Id = trxId >> counter++, Address = firstAccount.InternalAddresses.ElementAt(0).Address, CreationTime = creationTime };
            var trxConfirmed1 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 3), Amount = 10, Id = trxId >> counter++, BlockHeight = 50000, Address = firstAccount.ExternalAddresses.ElementAt(1).Address, CreationTime = creationTime };
            var trxConfirmed2 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 4), Amount = 10, Id = trxId >> counter++, BlockHeight = 50001, Address = firstAccount.InternalAddresses.ElementAt(1).Address, CreationTime = creationTime };

            wallet.walletStore.InsertOrUpdate(trxUnconfirmed1);
            wallet.walletStore.InsertOrUpdate(trxConfirmed1);
            wallet.walletStore.InsertOrUpdate(trxUnconfirmed2);
            wallet.walletStore.InsertOrUpdate(trxConfirmed2);

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)).Count();
            Assert.Equal(4, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveTransactionsByIds("wallet1", new[] { trxUnconfirmed1.Id, trxUnconfirmed2.Id, trxConfirmed1.Id, trxConfirmed2.Id });

            // Assert.
            List<TransactionOutputData> remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)).ToList();
            Assert.Equal(2, remainingTrxs.Count());
            Assert.Equal(2, result.Count);
            Assert.Contains(result, i => i.Item1 == trxUnconfirmed1.Id);
            Assert.Contains(result, i => i.Item1 == trxUnconfirmed2.Id);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.DoesNotContain(trxUnconfirmed2, remainingTrxs);
        }

        [Fact]
        public void ConfirmedTransactionsShouldWipeOutUnconfirmedWithSameInputs()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            Types.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                //Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                //  Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                // Transactions = new List<TransactionData>()
            };

            // Generate a spendable transaction.
            (ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionOutputData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingTransaction.Address = spendingAddress.Address;
            wallet.walletStore.InsertOrUpdate(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // Set up a transaction that will arrive through the mempool.
            Transaction transaction1 = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            // Set up a different transaction spending the same inputs, with a higher fee, that will arrive in a block.
            Transaction transaction2 = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(10_000));

            Assert.Equal(transaction1.Inputs[0].PrevOut, transaction2.Inputs[0].PrevOut);
            Assert.NotEqual(transaction1.GetHash(), transaction2.GetHash());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookup();

            // First add transaction 1 via mempool.
            walletManager.ProcessTransaction(transaction1);

            // The first transaction should be present in the wallet.
            Assert.Contains(wallet.walletStore.GetForAddress(destinationAddress.Address), t => t.Id == transaction1.GetHash());

            // Now add transaction 2 via block.
            Block block = this.Network.CreateBlock();
            block.AddTransaction(transaction2);

            walletManager.ProcessTransaction(transaction2, 10, block);

            // The first transaction should no longer be present in the wallet.
            Assert.DoesNotContain(wallet.walletStore.GetForAddress(destinationAddress.Address), t => t.Id == transaction1.GetHash());

            // The second transaction should be present.
            Assert.Contains(wallet.walletStore.GetForAddress(destinationAddress.Address), t => t.Id == transaction2.GetHash());
        }

        [Fact]
        public void RemoveTransactionsByIdsAlsoRemovesUnconfirmedSpendingDetailsTransactions()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // Generate a wallet with an account and a few transactions.
            Types.Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.walletStore = new WalletMemoryStore();
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);
            walletManager.LoadKeysLookup();

            IHdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            DateTimeOffset creationTime = default;

            // Confirmed transaction with confirmed spending.
            var confirmedSpendingDetails = new SpendingDetails { TransactionId = trxId >> counter++, BlockHeight = 500002, CreationTime = creationTime };
            var trxConfirmed1 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 3), Amount = 10, Id = trxId >> counter++, BlockHeight = 50000, SpendingDetails = confirmedSpendingDetails, Address = firstAccount.ExternalAddresses.ElementAt(1).Address, CreationTime = creationTime };

            // Confirmed transaction with unconfirmed spending.
            uint256 unconfirmedTransactionId = trxId >> counter++;
            var unconfirmedSpendingDetails1 = new SpendingDetails { TransactionId = unconfirmedTransactionId, CreationTime = creationTime };
            var trxConfirmed2 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 1), Amount = 10, Id = trxId >> counter++, BlockHeight = 50001, SpendingDetails = unconfirmedSpendingDetails1, Address = firstAccount.InternalAddresses.ElementAt(1).Address, CreationTime = creationTime };

            // Unconfirmed transaction.
            var trxUnconfirmed1 = new TransactionOutputData { OutPoint = new OutPoint(new uint256(1), 2), Amount = 10, Id = unconfirmedTransactionId, Address = firstAccount.ExternalAddresses.ElementAt(0).Address, CreationTime = creationTime };

            wallet.walletStore.InsertOrUpdate(trxUnconfirmed1);
            wallet.walletStore.InsertOrUpdate(trxConfirmed1);
            wallet.walletStore.InsertOrUpdate(trxConfirmed2);

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)).Count();
            Assert.Equal(3, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveTransactionsByIds("wallet1", new[]
            {
                trxConfirmed1.Id, // Shouldn't be removed.
                unconfirmedTransactionId, // A transaction + a spending transaction should be removed.
                trxConfirmed2.Id, // Shouldn't be removed.
                confirmedSpendingDetails.TransactionId, // Shouldn't be removed.
            });

            // Assert.
            List<TransactionOutputData> remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => wallet.walletStore.GetForAddress(a.Address)).ToList();
            Assert.Equal(2, remainingTrxs.Count);
            Assert.Single(result);
            Assert.Contains(result, i => i.Item1 == unconfirmedTransactionId);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.Null(remainingTrxs.Single(s => s.OutPoint == trxConfirmed2.OutPoint).SpendingDetails);
        }

        [Fact]
        public void Start_takes_account_of_address_buffer_even_for_existing_wallets()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            WalletManager walletManager = this.CreateWalletManager(dataFolder, this.Network);
            walletManager.CreateWallet("test", "mywallet", passphrase: new Mnemonic(Wordlist.English, WordCount.Eighteen).ToString());

            // Default of 20 addresses becuause walletaddressbuffer not set
            IHdAccount hdAccount = walletManager.Wallets.Single().AccountsRoot.Single().Accounts.Single();
            Assert.Equal(20, hdAccount.ExternalAddresses.Count);
            Assert.Equal(20, hdAccount.InternalAddresses.Count);

            walletManager.Stop();

            // Restart with walletaddressbuffer set
            walletManager = this.CreateWalletManager(dataFolder, this.Network, "-walletaddressbuffer=30");
            walletManager.Start();

            // Addresses populated to fill the buffer set
            hdAccount = walletManager.Wallets.Single().AccountsRoot.Single().Accounts.Single();
            Assert.Equal(30, hdAccount.ExternalAddresses.Count);
            Assert.Equal(30, hdAccount.InternalAddresses.Count);
        }

        [Fact]
        public void Recover_via_xpubkey_can_recover_wallet_without_mnemonic()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            const string stratisAccount0ExtPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";
            var walletManager = this.CreateWalletManager(dataFolder, this.Network);
            walletManager.RecoverWallet("testWallet", ExtPubKey.Parse(stratisAccount0ExtPubKey), 0, DateTime.Now.AddHours(-2));

            var wallet = walletManager.LoadWallet("password", "testWallet");

            wallet.IsExtPubKeyWallet.Should().BeTrue();
            wallet.EncryptedSeed.Should().BeNull();
            wallet.ChainCode.Should().BeNull();

            wallet.AccountsRoot.SelectMany(x => x.Accounts).Single().ExtendedPubKey
                .Should().Be(stratisAccount0ExtPubKey);
        }

        [Fact]
        public void AddNewAccount_via_xpubkey_prevents_adding_an_account_as_an_existing_account_index()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            const string stratisAccount0ExtPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";
            const string stratisAccount1ExtPubKey = "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1";
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain);
            var wallet = walletManager.RecoverWallet("wallet1", ExtPubKey.Parse(stratisAccount0ExtPubKey), 0, DateTime.Now.AddHours(-2));

            try
            {
                wallet.AddNewAccount(ExtPubKey.Parse(stratisAccount1ExtPubKey), 0, DateTime.Now.AddHours(-2));

                Assert.True(false, "should have thrown exception but didn't.");
            }
            catch (WalletException e)
            {
                Assert.Equal("There is already an account in this wallet with index: " + 0, e.Message);
            }
        }

        [Fact]
        public void AddNewAccount_via_xpubkey_prevents_adding_the_same_xpub_key_as_different_account()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            const string stratisAccount0ExtPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain);
            var wallet = walletManager.RecoverWallet("wallet1", ExtPubKey.Parse(stratisAccount0ExtPubKey), 0, DateTime.Now.AddHours(-2));

            var addNewAccount = new Action(() => wallet.AddNewAccount(ExtPubKey.Parse(stratisAccount0ExtPubKey), 1, DateTime.Now.AddHours(-2)));

            addNewAccount.Should().Throw<WalletException>()
                .WithMessage("There is already an account in this wallet with this xpubkey: " + stratisAccount0ExtPubKey);
        }

        [Fact]
        public void CreateDefaultWalletAndVerifyTheDefaultPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            // Load the default wallet.
            var wallet = walletManager.LoadWallet("default", "default");

            Assert.NotNull(wallet);
        }

        [Fact]
        public void CreateDefaultWalletAndVerifyWrongPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default", "-defaultwalletpassword=default2");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            Assert.Throws<System.Security.SecurityException>(() =>
            {
                // Attempt to load the default wallet with wrong password.
                var wallet = walletManager.LoadWallet("default", "default");
            });
        }

        /// <summary>
        /// Test that first creates an unlocked default wallet, retrieves the extkey to verify it is actually unlocked. Lock the wallet, verify
        /// it is not possible to get extkey. Unlock manually, and verify that returned key is same as before.
        /// </summary>
        [Fact]
        public void CreateDefaultWalletAndVerifyUnlockAndLocking()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default", "-unlockdefaultwallet");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var wallet = walletManager.GetWalletByName("default");

            IHdAccount account = walletManager.GetAccounts("default").Single();
            var reference = new WalletAccountReference("default", account.Name);

            var extKey1 = walletManager.GetExtKey(reference);
            walletManager.LockWallet("default");

            Assert.Throws<System.Security.SecurityException>(() =>
            {
                walletManager.GetExtKey(reference);
            });

            walletManager.UnlockWallet("default", "default", 10);

            var extKey2 = walletManager.GetExtKey(reference);

            Assert.Equal(extKey1.ToString(wallet.Network), extKey2.ToString(wallet.Network));
        }

        private WalletManager CreateWalletManager(DataFolder dataFolder, Network network, params string[] cmdLineArgs)
        {
            var nodeSettings = new NodeSettings(KnownNetworks.RegTest, agent: network.Name, args: cmdLineArgs);
            var walletSettings = new WalletSettings(nodeSettings);

            return new WalletManager(this.LoggerFactory.Object, network, new ChainIndexer(network),
                walletSettings, dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
        }

        private (Mnemonic mnemonic, Types.Wallet wallet) CreateWalletOnDiskAndDeleteWallet(DataFolder dataFolder, string password, string passphrase, string walletName, ChainIndexer chainIndexer)
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            // create the wallet
            Mnemonic mnemonic = walletManager.CreateWallet(password, walletName, passphrase);
            Types.Wallet wallet = walletManager.Wallets.ElementAt(0);

            walletManager.Stop();

            File.Delete(dataFolder.WalletPath + $"/{walletName}.wallet.json");

            return (mnemonic, wallet);
        }
    }
}