﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Blockcore.AsyncWork;
using Blockcore.Base;
using Blockcore.Base.Deployments;
using Blockcore.Configuration;
using Blockcore.Configuration.Settings;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.Checkpoints;
using Blockcore.Consensus.Rules;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.ColdStaking.Api.Controllers;
using Blockcore.Features.ColdStaking.Api.Models;
using Blockcore.Features.Consensus;
using Blockcore.Features.Consensus.CoinViews;
using Blockcore.Features.Consensus.Interfaces;
using Blockcore.Features.Consensus.ProvenBlockHeaders;
using Blockcore.Features.Consensus.Rules;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.MemoryPool.Fee;
using Blockcore.Features.MemoryPool.Rules;
using Blockcore.Features.Wallet;
using Blockcore.Features.Wallet.Database;
using Blockcore.Features.Wallet.Exceptions;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Features.Wallet.Types;
using Blockcore.Networks.Stratis.Deployments;
using Blockcore.Networks.Stratis.Policies;
using Blockcore.Signals;
using Blockcore.Tests.Common;
using Blockcore.Utilities;
using Blockcore.Utilities.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using Xunit;

namespace Blockcore.Features.ColdStaking.Tests
{
    /// <summary>
    /// This class tests the functionality provided by the <see cref="ColdStakingController"/>.
    /// </summary>
    public class ColdStakingControllerTest : TestBase
    {
        private const string walletName1 = "wallet1";
        private const string walletName2 = "wallet2";
        private const string walletAccount = "account 0";
        private const string walletPassword = "test";
        private const string walletPassphrase = "passphrase";
        private const string walletMnemonic1 = "close vanish burden journey attract open soul romance beach surprise home produce";
        private const string walletMnemonic2 = "wish happy anchor lava path reject cinnamon absurd energy mammal cliff version";
        private const string coldWalletAddress1 = "SNiAnXM2WmbMhUij9cbit62sR8U9FjFJr3";
        private const string hotWalletAddress1 = "SaVUwmJSvRiofghrePxrBQGoke1pLfmfXN";
        private const string coldWalletAddress2 = "Sagbh9LuzNAV7y2FHyUQJcgmjcuogSssef";
        private const string hotWalletAddress2 = "SVoMim67CMF1St6j6toAWnnQ2mCvb8V4mT";
        private const string coldWalletSegwitAddress1 = "strat1qp79yqxx44gza9mzmwjk25cxmyg6wdee4hnf7c9";
        private const string hotWalletSegwitAddress1 = "strat1qjrzc9ju366mdwa7rrjy36j2rlm2wmtp63vre6g";
        private const string coldWalletSegwitAddress2 = "strat1qjt0ms2wnrnh7dgrnru6r9h4yzkt2y7xedlgcp9";
        private const string hotWalletSegwitAddress2 = "strat1qt489d0ct9snhutaam7dmttrtf4f3hfk23xmn0s";

        private ColdStakingManager coldStakingManager;
        private ColdStakingController coldStakingController;
        private IAsyncProvider asyncProvider;
        private NodeSettings nodeSettings;
        private IDateTimeProvider dateTimeProvider;
        private ILoggerFactory loggerFactory;
        private ChainIndexer chainIndexer;
        private NodeDeployments nodeDeployments;
        private ConsensusSettings consensusSettings;
        private MempoolSettings mempoolSettings;
        private Mock<ICoinView> coinView;
        private Dictionary<OutPoint, UnspentOutput> unspentOutputs;
        private TxMempool txMemPool;
        private Mock<IStakeChain> stakeChain;
        private Mock<IStakeValidator> stakeValidator;
        private MempoolManager mempoolManager;

        public ColdStakingControllerTest() : base(KnownNetworks.StratisMain)
        {
            // Register the cold staking script template.
            this.Network.StandardScriptsRegistry.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);
            var registery = (StratisStandardScriptsRegistry)this.Network.StandardScriptsRegistry;
            registery.GetScriptTemplates.Remove(registery.GetScriptTemplates.OfType<TxNullDataTemplate>().Single()); // remove teh default standard script
            this.Network.StandardScriptsRegistry.RegisterStandardScriptTemplate(TxNullDataTemplate.Instance);
        }

        /// <summary>
        /// Mock the stake validator.
        /// </summary>
        private void MockStakeValidator()
        {
            this.stakeValidator = new Mock<IStakeValidator>();

            // Since we are mocking the stake validator ensure that GetNextTargetRequired returns something sensible. Otherwise we get the "bad-diffbits" error.
            this.stakeValidator.Setup(s => s.GetNextTargetRequired(It.IsAny<IStakeChain>(), It.IsAny<ChainedHeader>(), It.IsAny<IConsensus>(), It.IsAny<bool>()))
                .Returns(this.Network.Consensus.PowLimit);
        }

        /// <summary>
        /// Mock the stake chain.
        /// </summary>
        private void MockStakeChain()
        {
            this.stakeChain = new Mock<IStakeChain>();

            // Since we are mocking the stakechain ensure that the Get returns a BlockStake. Otherwise this results in "previous stake is not found".
            this.stakeChain.Setup(d => d.Get(It.IsAny<uint256>())).Returns(new BlockStake()
            {
                Flags = BlockFlag.BLOCK_PROOF_OF_STAKE,
                StakeModifierV2 = 0,
                StakeTime = (this.chainIndexer.Tip.Header.Time + 60) & ~this.Network.Consensus.ProofOfStakeTimestampMask
            });
        }

        /// <summary>
        /// Mock the coin view.
        /// </summary>
        private void MockCoinView()
        {
            this.unspentOutputs = new Dictionary<OutPoint, UnspentOutput>();
            this.coinView = new Mock<ICoinView>();

            // Mock the coinviews "FetchCoinsAsync" method. We will use the "unspentOutputs" dictionary to track spendable outputs.
            this.coinView.Setup(d => d.FetchCoins(It.IsAny<OutPoint[]>()))
                .Returns((OutPoint[] txIds) =>
                {
                    var result = new FetchCoinsResponse();

                    for (int i = 0; i < txIds.Length; i++)
                        result.UnspentOutputs[txIds[i]] = this.unspentOutputs.TryGetValue(txIds[i], out UnspentOutput unspent) ? unspent : null;

                    return result;
                });

            // Mock the coinviews "GetTipHashAsync" method.
            this.coinView.Setup(d => d.GetTipHash()).Returns(() =>
                {
                    return new HashHeightPair(this.chainIndexer.Tip);
                });
        }

        /// <summary>
        /// Create the MempoolManager used for testing whether transactions are accepted to the memory pool.
        /// </summary>
        private void CreateMempoolManager()
        {
            this.mempoolSettings = new MempoolSettings(this.nodeSettings);
            this.consensusSettings = new ConsensusSettings(this.nodeSettings);
            this.txMemPool = new TxMempool(this.dateTimeProvider, new BlockPolicyEstimator(
                new MempoolSettings(this.nodeSettings), this.loggerFactory, this.nodeSettings), this.loggerFactory, this.nodeSettings);
            this.chainIndexer = new ChainIndexer(this.Network);
            this.nodeDeployments = new NodeDeployments(this.Network, this.chainIndexer);

            this.MockCoinView();
            this.MockStakeChain();
            this.MockStakeValidator();

            // Create POS consensus rules engine.
            var checkpoints = new Mock<ICheckpoints>();
            var chainState = new ChainState();

            var consensusRulesContainer = new ConsensusRulesContainer();
            foreach (var ruleType in this.Network.Consensus.ConsensusRules.HeaderValidationRules)
                consensusRulesContainer.HeaderValidationRules.Add(Activator.CreateInstance(ruleType) as HeaderValidationConsensusRule);
            foreach (var ruleType in this.Network.Consensus.ConsensusRules.FullValidationRules)
                consensusRulesContainer.FullValidationRules.Add(Activator.CreateInstance(ruleType) as FullValidationConsensusRule);

            ConsensusRuleEngine consensusRuleEngine = new PosConsensusRuleEngine(this.Network, this.loggerFactory, this.dateTimeProvider,
                this.chainIndexer, this.nodeDeployments, this.consensusSettings, checkpoints.Object, this.coinView.Object, this.stakeChain.Object,
                this.stakeValidator.Object, chainState, new InvalidBlockHashStore(this.dateTimeProvider), new Mock<INodeStats>().Object, new Mock<IRewindDataIndexCache>().Object, this.asyncProvider, consensusRulesContainer)
                .SetupRulesEngineParent();

            // Create mempool validator.
            var mempoolLock = new MempoolSchedulerLock();

            // The mempool rule constructors aren't parameterless, so we have to manually inject the dependencies for every rule
            var mempoolRules = new List<MempoolRule>
            {
                new CheckConflictsMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CheckCoinViewMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CreateMempoolEntryMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, consensusRuleEngine, this.loggerFactory),
                new CheckSigOpsMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CheckFeeMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CheckRateLimitMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CheckAncestorsMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CheckReplacementMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, this.loggerFactory),
                new CheckAllInputsMempoolRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer, consensusRuleEngine, this.nodeDeployments, this.loggerFactory),
                new CheckTxOutDustRule(this.Network, this.txMemPool, this.mempoolSettings, this.chainIndexer,  this.loggerFactory)
            };

            // We also have to check that the manually instantiated rules match the ones in the network, or the test isn't valid
            for (int i = 0; i < this.Network.Consensus.MempoolRules.Count; i++)
            {
                if (this.Network.Consensus.MempoolRules[i] != mempoolRules[i].GetType())
                {
                    throw new Exception("Mempool rule type mismatch");
                }
            }

            Assert.Equal(this.Network.Consensus.MempoolRules.Count, mempoolRules.Count);

            var mempoolValidator = new MempoolValidator(this.txMemPool, mempoolLock, this.dateTimeProvider, this.mempoolSettings, this.chainIndexer,
                this.coinView.Object, this.loggerFactory, this.nodeSettings, consensusRuleEngine, mempoolRules, this.nodeDeployments);

            // Create mempool manager.
            var mempoolPersistence = new Mock<IMempoolPersistence>();
            this.mempoolManager = new MempoolManager(mempoolLock, this.txMemPool, mempoolValidator, this.dateTimeProvider, this.mempoolSettings,
                mempoolPersistence.Object, this.coinView.Object, this.loggerFactory, this.Network);
        }

        /// <summary>
        /// Initializes each test case.
        /// </summary>
        /// <param name="callingMethod">The test method being executed.</param>
        private void Initialize([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            DataFolder dataFolder = CreateDataFolder(this, callingMethod);
            this.nodeSettings = new NodeSettings(this.Network);
            this.dateTimeProvider = DateTimeProvider.Default;
            var walletSettings = new WalletSettings(this.nodeSettings);
            this.loggerFactory = this.nodeSettings.LoggerFactory;

            this.coldStakingManager = new ColdStakingManager(this.Network, new ChainIndexer(this.Network), walletSettings, dataFolder,
                new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), new ScriptAddressReader(),
                this.loggerFactory, DateTimeProvider.Default);

            var walletTransactionHandler = new WalletTransactionHandler(this.loggerFactory, this.coldStakingManager,
                new Mock<IWalletFeePolicy>().Object, this.Network, new StandardTransactionPolicy(this.Network));

            this.coldStakingController = new ColdStakingController(this.loggerFactory, this.coldStakingManager, walletTransactionHandler);

            this.asyncProvider = new AsyncProvider(this.loggerFactory, new Mock<ISignals>().Object, new NodeLifetime());
        }

        /// <summary>
        /// Adds a spendable transaction to a wallet.
        /// </summary>
        /// <param name="wallet">Wallet to add the transaction to.</param>
        /// <returns>The spendable transaction that was added to the wallet.</returns>
        private Transaction AddSpendableTransactionToWallet(Wallet.Types.Wallet wallet)
        {
            HdAddress address = wallet.GetAllAddresses().FirstOrDefault();

            Transaction transaction = this.Network.CreateTransaction();

            transaction.Outputs.Add(new TxOut(Money.Coins(101), address.ScriptPubKey));

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData()
            {
                OutPoint = new OutPoint(transaction.GetHash(), 0),
                Address = address.Address,
                Hex = transaction.ToHex(this.Network.Consensus.ConsensusFactory),
                Amount = transaction.Outputs[0].Value,
                Id = transaction.GetHash(),
                BlockHeight = 0,
                Index = 0,
                IsCoinBase = false,
                IsCoinStake = false,
                IsPropagated = true,
                BlockHash = this.Network.GenesisHash,
                ScriptPubKey = address.ScriptPubKey
            });

            return transaction;
        }

        /// <summary>
        /// Verifies that all the cold staking addresses are as expected. This allows us to use the
        /// previously established addresses instead of re-generating the addresses for each test case.
        /// </summary>
        [Fact]
        public void ColdStakingVerifyWalletAddresses()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));
            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, true, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, false, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName2, true, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName2, false, walletPassword);

            var wallet1 = this.coldStakingManager.GetWalletByName(walletName1);
            var wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            HdAddress coldAddress1 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName1, true);
            HdAddress hotAddress1 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName1, false);
            HdAddress coldAddress2 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName2, true);
            HdAddress hotAddress2 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName2, false);

            Assert.Equal(coldWalletAddress1, coldAddress1.Address);
            Assert.Equal(hotWalletAddress1, hotAddress1.Address);
            Assert.Equal(coldWalletAddress2, coldAddress2.Address);
            Assert.Equal(hotWalletAddress2, hotAddress2.Address);
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// will fail if the wallet does not contain the relevant account.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForMissingAccountThrowsWalletException()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result1 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = true
            });

            var errorResult1 = Assert.IsType<ErrorResult>(result1);
            var errorResponse1 = Assert.IsType<ErrorResponse>(errorResult1.Value);
            Assert.Single(errorResponse1.Errors);
            ErrorModel error1 = errorResponse1.Errors[0];

            IActionResult result2 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = false
            });

            var errorResult2 = Assert.IsType<ErrorResult>(result2);
            var errorResponse2 = Assert.IsType<ErrorResponse>(errorResult2.Value);
            Assert.Single(errorResponse2.Errors);
            ErrorModel error2 = errorResponse1.Errors[0];

            Assert.Equal((int)HttpStatusCode.BadRequest, error1.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error1.Description);
            Assert.StartsWith("The cold staking account does not exist.", error1.Message);

            Assert.Equal((int)HttpStatusCode.BadRequest, error2.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error2.Description);
            Assert.StartsWith("The cold staking account does not exist.", error2.Message);
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// will return an address if the wallet contains the relevant account.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForExistingAccountReturnsAddress()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            // Create existing accounts.
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, true, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, false, walletPassword);

            // Try to get cold wallet address on existing account without supplying the wallet password.
            IActionResult result1 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = true
            });

            var jsonResult1 = Assert.IsType<JsonResult>(result1);
            var response1 = Assert.IsType<GetColdStakingAddressResponse>(jsonResult1.Value);

            // Try to get hot wallet address on existing account without supplying the wallet password.
            IActionResult result2 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = false
            });

            var jsonResult2 = Assert.IsType<JsonResult>(result2);
            var response2 = Assert.IsType<GetColdStakingAddressResponse>(jsonResult2.Value);

            Assert.Equal(coldWalletAddress1, response1.Address);
            Assert.Equal(hotWalletAddress1, response2.Address);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to use the same wallet
        /// as both cold wallet and hot wallet.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithSameWalletThrowsWalletException()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress1,
                WalletName = walletName1,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal((int)HttpStatusCode.BadRequest, error.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error.Description);
            Assert.StartsWith("You can't use this wallet as both hot wallet and cold wallet.", error.Message);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to use hot and cold wallet addresses
        /// where neither of the addresses is known to the wallet creating a cold staking setup.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithBothAddressesUnknownThrowsWalletException()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = new Key().PubKey.GetAddress(this.Network).ToString(),
                ColdWalletAddress = new Key().PubKey.GetAddress(this.Network).ToString(),
                WalletName = walletName1,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal((int)HttpStatusCode.BadRequest, error.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error.Description);
            Assert.StartsWith("The hot and cold wallet addresses could not be found in the corresponding accounts.", error.Message);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to use coins from a cold
        /// staking account to act as inputs to a cold staking setup transaction.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithInvalidAccountThrowsWalletException()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            string coldWalletAccountName = typeof(ColdStakingManager).GetPrivateConstantValue<string>("ColdWalletAccountName");
            string hotWalletAccountName = typeof(ColdStakingManager).GetPrivateConstantValue<string>("HotWalletAccountName");

            // Attempt to set up cold staking with a cold wallet account name.
            IActionResult result1 = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = coldWalletAccountName,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult1 = Assert.IsType<ErrorResult>(result1);
            var errorResponse1 = Assert.IsType<ErrorResponse>(errorResult1.Value);
            Assert.Single(errorResponse1.Errors);
            ErrorModel error1 = errorResponse1.Errors[0];

            // Attempt to set up cold staking with a hot wallet account name.
            IActionResult result2 = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = hotWalletAccountName,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult2 = Assert.IsType<ErrorResult>(result2);
            var errorResponse2 = Assert.IsType<ErrorResponse>(errorResult2.Value);
            Assert.Single(errorResponse2.Errors);
            ErrorModel error2 = errorResponse2.Errors[0];

            Assert.Equal((int)HttpStatusCode.BadRequest, error1.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error1.Description);
            // TODO: Restore this line.
            // Assert.StartsWith($"Can't find wallet account '{coldWalletAccountName}'.", error1.Message);

            Assert.Equal((int)HttpStatusCode.BadRequest, error2.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error2.Description);
            // TODO: Restore this line.
            // Assert.StartsWith($"Can't find wallet account '{hotWalletAccountName}'.", error2.Message);
        }

        /// <summary>
        /// Confirms that cold staking setup with the hot wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithHotWalletSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            Wallet.Types.Wallet wallet1 = this.coldStakingManager.GetWalletByName(walletName1);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet1);

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 970e19fc2f6565b0b1c65fd88ef1512cb3da4d7b OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        /// <summary>
        /// Confirms that cold staking setup with the hot wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithHotWalletSegwitSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            Wallet.Types.Wallet wallet1 = this.coldStakingManager.GetWalletByName(walletName1);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet1);

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletSegwitAddress1,
                ColdWalletAddress = coldWalletSegwitAddress2,
                WalletName = walletName1,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 970e19fc2f6565b0b1c65fd88ef1512cb3da4d7b OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        [Fact]
        public void VerifyThatColdStakeTransactionCanBeFiltered()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            Wallet.Types.Wallet wallet1 = this.coldStakingManager.GetWalletByName(walletName1);

            // This will add a normal account to our wallet.
            Transaction trx1 = this.AddSpendableTransactionToWallet(wallet1);

            // This will add a secondary account to our wallet.
            Transaction trx2 = this.AddSpendableColdstakingTransactionToWallet(wallet1);

            // THis will add a cold staking transaction to the secondary normal account address. This simulates activation of cold staking onto any normal address.
            Transaction trx3 = this.AddSpendableColdstakingTransactionToNormalWallet(wallet1);

            var accounts = wallet1.GetAccounts(Wallet.Types.Wallet.AllAccounts).ToArray();

            // We should have 2 accounts in our wallet.
            Assert.Equal(2, accounts.Length);

            // But not if we use default or specify to only return normal accounts.
            Assert.Single(wallet1.GetAccounts().ToArray()); // Defaults to NormalAccounts
            Assert.Single(wallet1.GetAccounts(Wallet.Types.Wallet.NormalAccounts).ToArray());

            // Verify that we actually have an cold staking activation UTXO in the wallet of 202 coins.
            // This should normally not be returned by the GetAllTransactions, and should never be included in balance calculations.
            Assert.True(wallet1.walletStore.GetForAddress(accounts[0].ExternalAddresses.ToArray()[1].Address).ToArray()[0].IsColdCoinStake);
            Assert.Equal(new Money(202, MoneyUnit.BTC), wallet1.walletStore.GetForAddress(accounts[0].ExternalAddresses.ToArray()[1].Address).ToArray()[0].Amount);

            Assert.Single(wallet1.GetAllTransactions().ToArray()); // Default to NormalAccounts, should filter out cold staking (trx3) from normal wallet.
            Assert.Single(wallet1.GetAllTransactions(Wallet.Types.Wallet.NormalAccounts).ToArray());
            Assert.Single(wallet1.GetAllSpendableTransactions(wallet1.walletStore, 5, 0, Wallet.Types.Wallet.NormalAccounts).ToArray()); // Default to NormalAccounts
            Assert.Equal(2, wallet1.GetAllTransactions(Wallet.Types.Wallet.AllAccounts).ToArray().Length);
            Assert.Equal(2, wallet1.GetAllSpendableTransactions(wallet1.walletStore, 5, 0, Wallet.Types.Wallet.AllAccounts).ToArray().Length); // Specified AllAccounts, should include cold-staking transaction.

            // Verify balance on normal account
            var balance1 = accounts[0].GetBalances(wallet1.walletStore, true);
            var balance2 = accounts[0].GetBalances(wallet1.walletStore, false);

            Assert.Equal(new Money(101, MoneyUnit.BTC), balance1.ConfirmedAmount);
            Assert.Equal(new Money(303, MoneyUnit.BTC), balance2.ConfirmedAmount);

            // Verify balance on special account.
            // Verify balance on normal account
            var balance3 = accounts[1].GetBalances(wallet1.walletStore, true);
            var balance4 = accounts[1].GetBalances(wallet1.walletStore, false);

            // The only transaction that exists in the cold staking wallet, is a normal one, and should be returned for both balance queries.
            Assert.Equal(new Money(101, MoneyUnit.BTC), balance3.ConfirmedAmount);
            Assert.Equal(new Money(101, MoneyUnit.BTC), balance4.ConfirmedAmount);
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithColdWalletSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            Wallet.Types.Wallet wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet2);

            // Create the cold staking setup transaction.
            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName2,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 3d36028dc0fd3d3e433c801d9ebfff05ea663816 OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet and segwit address will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithColdWalletSegwitSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            Wallet.Types.Wallet wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet2);

            // Create the cold staking setup transaction.
            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletSegwitAddress1,
                ColdWalletAddress = coldWalletSegwitAddress2,
                WalletName = walletName2,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 3d36028dc0fd3d3e433c801d9ebfff05ea663816 OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        [Fact]
        public void SetupScriptColdStakingWithColdWalletSegwitSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            Wallet.Types.Wallet wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet2);

            // Create the cold staking setup transaction.
            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletSegwitAddress1,
                ColdWalletAddress = coldWalletSegwitAddress2,
                WalletName = walletName2,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01",
                PayToScript = true
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(3, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 3d36028dc0fd3d3e433c801d9ebfff05ea663816 OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("0 344874146cfe398540d00bf978e747781f29a77ff586049ad23d2fe6df4f458b", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        /// <summary>
        /// Cold staking info only confirms that a cold staking account exists once it has been created.
        /// </summary>
        [Fact]
        public void GetColdStakingInfoOnlyConfirmAccountExistenceOnceCreated()
        {
            this.Initialize();

            this.coldStakingManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result1 = this.coldStakingController.GetColdStakingInfo(new GetColdStakingInfoRequest
            {
                WalletName = walletName1
            });

            var jsonResult1 = Assert.IsType<JsonResult>(result1);
            var response1 = Assert.IsType<GetColdStakingInfoResponse>(jsonResult1.Value);

            Assert.False(response1.ColdWalletAccountExists);
            Assert.False(response1.HotWalletAccountExists);

            IActionResult result2 = this.coldStakingController.CreateColdStakingAccount(new CreateColdStakingAccountRequest
            {
                WalletName = walletName1,
                WalletPassword = walletPassword,
                IsColdWalletAccount = true
            });

            var jsonResult2 = Assert.IsType<JsonResult>(result2);
            var response2 = Assert.IsType<CreateColdStakingAccountResponse>(jsonResult2.Value);

            Assert.NotEmpty(response2.AccountName);

            IActionResult result3 = this.coldStakingController.GetColdStakingInfo(new GetColdStakingInfoRequest
            {
                WalletName = walletName1
            });

            var jsonResult3 = Assert.IsType<JsonResult>(result3);
            var response3 = Assert.IsType<GetColdStakingInfoResponse>(jsonResult3.Value);

            Assert.True(response3.ColdWalletAccountExists);
            Assert.False(response3.HotWalletAccountExists);
        }

        /// <summary>
        /// Adds a spendable cold staking transaction to a wallet.
        /// </summary>
        /// <param name="wallet">Wallet to add the transaction to.</param>
        /// <returns>The spendable transaction that was added to the wallet.</returns>
        private Transaction AddSpendableColdstakingTransactionToWallet(Wallet.Types.Wallet wallet, bool script = false)
        {
            // Get first unused cold staking address.
            IHdAccount account = this.coldStakingManager.GetOrCreateColdStakingAccount(wallet.Name, true, walletPassword);
            HdAddress address = this.coldStakingManager.GetFirstUnusedColdStakingAddress(wallet.Name, true);

            TxDestination hotPubKey = BitcoinAddress.Create(hotWalletAddress1, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);
            TxDestination coldPubKey = BitcoinAddress.Create(coldWalletAddress2, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);

            var scriptPubKey = new Script(OpcodeType.OP_DUP, OpcodeType.OP_HASH160, OpcodeType.OP_ROT, OpcodeType.OP_IF,
                OpcodeType.OP_CHECKCOLDSTAKEVERIFY, Op.GetPushOp(hotPubKey.ToBytes()), OpcodeType.OP_ELSE, Op.GetPushOp(coldPubKey.ToBytes()),
                OpcodeType.OP_ENDIF, OpcodeType.OP_EQUALVERIFY, OpcodeType.OP_CHECKSIG);

            var transaction = this.Network.CreateTransaction();

            transaction.Outputs.Add(new TxOut(Money.Coins(101), script ? scriptPubKey.WitHash.ScriptPubKey : scriptPubKey));

            if (script)
                address.RedeemScript = scriptPubKey;

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData()
            {
                OutPoint = new OutPoint(transaction.GetHash(), 0),
                Address = address.Address,
                AccountIndex = account.Index,
                Hex = transaction.ToHex(this.Network.Consensus.ConsensusFactory),
                Amount = transaction.Outputs[0].Value,
                Id = transaction.GetHash(),
                BlockHeight = 0,
                Index = 0,
                IsCoinBase = false,
                IsCoinStake = false,
                IsPropagated = true,
                BlockHash = this.Network.GenesisHash,
                ScriptPubKey = script ? scriptPubKey.WitHash.ScriptPubKey : scriptPubKey,
            });

            return transaction;
        }

        /// <summary>
        /// Adds a spendable cold staking transaction to a normal account, as oppose to dedicated special account.
        /// </summary>
        /// <param name="wallet">Wallet to add the transaction to.</param>
        /// <returns>The spendable transaction that was added to the wallet.</returns>
        private Transaction AddSpendableColdstakingTransactionToNormalWallet(Wallet.Types.Wallet wallet, bool script = false)
        {
            // This will always be added to the secondary address.
            HdAddress address = wallet.GetAllAddresses().ToArray()[1];

            var transaction = this.Network.CreateTransaction();

            // Use the normal wallet address here.
            TxDestination hotPubKey = BitcoinAddress.Create(address.Address, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);
            TxDestination coldPubKey = BitcoinAddress.Create(coldWalletAddress2, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);

            var scriptPubKey = new Script(OpcodeType.OP_DUP, OpcodeType.OP_HASH160, OpcodeType.OP_ROT, OpcodeType.OP_IF,
                OpcodeType.OP_CHECKCOLDSTAKEVERIFY, Op.GetPushOp(hotPubKey.ToBytes()), OpcodeType.OP_ELSE, Op.GetPushOp(coldPubKey.ToBytes()),
                OpcodeType.OP_ENDIF, OpcodeType.OP_EQUALVERIFY, OpcodeType.OP_CHECKSIG);

            transaction.Outputs.Add(new TxOut(Money.Coins(202), script ? scriptPubKey.WitHash.ScriptPubKey : scriptPubKey));

            if (script)
                address.RedeemScript = scriptPubKey;

            wallet.walletStore.InsertOrUpdate(new TransactionOutputData()
            {
                OutPoint = new OutPoint(transaction.GetHash(), 0),
                Address = address.Address,
                Hex = transaction.ToHex(this.Network.Consensus.ConsensusFactory),
                Amount = transaction.Outputs[0].Value,
                Id = transaction.GetHash(),
                BlockHeight = 0,
                Index = 0,
                IsCoinBase = false,
                IsCoinStake = false,
                IsColdCoinStake = true,
                IsPropagated = true,
                BlockHash = this.Network.GenesisHash,
                ScriptPubKey = script ? scriptPubKey.WitHash.ScriptPubKey : scriptPubKey,
            });

            return transaction;
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void ColdStakingWithdrawalWithColdWalletSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableColdstakingTransactionToWallet(wallet2);

            BitcoinPubKeyAddress receivingAddress = new Key().PubKey.GetAddress(this.Network);

            IActionResult result = this.coldStakingController.ColdStakingWithdrawal(new ColdStakingWithdrawalRequest
            {
                ReceivingAddress = receivingAddress.ToString(),
                WalletName = walletName2,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<ColdStakingWithdrawalResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal(receivingAddress.ScriptPubKey, transaction.Outputs[1].ScriptPubKey);
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void ColdStakingWithdrawalToSegwitWithColdWalletSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableColdstakingTransactionToWallet(wallet2);

            BitcoinWitPubKeyAddress receivingAddress = new Key().PubKey.GetSegwitAddress(this.Network);

            IActionResult result = this.coldStakingController.ColdStakingWithdrawal(new ColdStakingWithdrawalRequest
            {
                ReceivingAddress = receivingAddress.ToString(),
                WalletName = walletName2,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<ColdStakingWithdrawalResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal(receivingAddress.ScriptPubKey, transaction.Outputs[1].ScriptPubKey);
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");
        }

        [Fact]
        public void ColdStakingScriptWithdrawalToSegwitWithColdWalletSucceeds()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableColdstakingTransactionToWallet(wallet2, true);

            BitcoinWitPubKeyAddress receivingAddress = new Key().PubKey.GetSegwitAddress(this.Network);

            IActionResult result = this.coldStakingController.ColdStakingWithdrawal(new ColdStakingWithdrawalRequest
            {
                ReceivingAddress = receivingAddress.ToString(),
                WalletName = walletName2,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<ColdStakingWithdrawalResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("0 344874146cfe398540d00bf978e747781f29a77ff586049ad23d2fe6df4f458b", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal(receivingAddress.ScriptPubKey, transaction.Outputs[1].ScriptPubKey);
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);

            // Record the spendable outputs.
            this.unspentOutputs[new OutPoint(prevTran, 0)] = new UnspentOutput(new OutPoint(prevTran, 0), new Coins(1, prevTran.Outputs[0], false, false));

            // activate segwit
            BIP9DeploymentsParameters current = this.Network.Consensus.BIP9Deployments[StratisBIP9Deployments.Segwit];
            this.Network.Consensus.BIP9Deployments[StratisBIP9Deployments.Segwit] =
                new BIP9DeploymentsParameters("Segwit", 1, BIP9DeploymentsParameters.AlwaysActive, BIP9DeploymentsParameters.AlwaysActive, BIP9DeploymentsParameters.AlwaysActive);

            // Verify that the transaction would be accepted to the memory pool.
            var state = new MempoolValidationState(true);
            Assert.True(this.mempoolManager.Validator.AcceptToMemoryPool(state, transaction).GetAwaiter().GetResult(), "Transaction failed mempool validation.");

            // Revert back changes
            this.Network.Consensus.BIP9Deployments[StratisBIP9Deployments.Segwit] = current;
        }

        /// <summary>
        /// Confirms that cold staking setup sending money to a cold staking account will raise an error.
        /// </summary>
        /// <remarks>
        /// It is only possible to perform this test against the known wallet. Money can still be sent to
        /// a cold staking account if it is in a different wallet.
        /// </remarks>
        [Fact]
        public void ColdStakingWithdrawalToColdWalletAccountThrowsWalletException()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableColdstakingTransactionToWallet(wallet2);

            HdAddress receivingAddress = this.coldStakingManager.GetOrCreateColdStakingAccount(walletName2, true, walletPassword).ExternalAddresses.First();

            IActionResult result = this.coldStakingController.ColdStakingWithdrawal(new ColdStakingWithdrawalRequest
            {
                ReceivingAddress = receivingAddress.Address.ToString(),
                WalletName = walletName2,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];

            Assert.Equal((int)HttpStatusCode.BadRequest, error.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error.Description);
            Assert.StartsWith("You can't send the money to a cold staking cold wallet account.", error.Message);
        }

        /// <summary>
        /// Confirms that trying to withdraw money from a non-existent cold staking account will raise an error.
        /// </summary>
        [Fact]
        public void ColdStakingWithdrawalFromNonExistingColdWalletAccountThrowsWalletException()
        {
            this.Initialize();
            this.CreateMempoolManager();

            this.coldStakingManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet2 = this.coldStakingManager.GetWalletByName(walletName2);

            BitcoinPubKeyAddress receivingAddress = new Key().PubKey.GetAddress(this.Network);

            IActionResult result = this.coldStakingController.ColdStakingWithdrawal(new ColdStakingWithdrawalRequest
            {
                ReceivingAddress = receivingAddress.ToString(),
                WalletName = walletName2,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];

            Assert.Equal((int)HttpStatusCode.BadRequest, error.Status);
            Assert.StartsWith($"{nameof(Blockcore)}.{nameof(Blockcore.Features)}.{nameof(Blockcore.Features.Wallet)}.{nameof(Blockcore.Features.Wallet.Exceptions)}.{nameof(WalletException)}", error.Description);
            Assert.StartsWith("The cold wallet account does not exist.", error.Message);
        }
    }
}