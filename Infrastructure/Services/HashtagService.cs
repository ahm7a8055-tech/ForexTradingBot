using Application.Interfaces;
using System;
using System.Collections.Concurrent;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implements a "very smart," stateful system for providing unique and varied sets of hashtags.
    /// It maintains a shuffled queue of all hashtags to ensure that the same hashtags are not
    /// repeated frequently. This service must be registered as a Singleton.
    /// </summary>
    public class HashtagService : IHashtagService
    {
        // The master list of all possible hashtags.
        private static readonly IReadOnlyList<string> AllHashtags = new List<string>
{
    // --- Core & General Finance ---
    "#Finance", "#Investing", "#Trading", "#Market", "#Markets", "#MarketNews", "#FinancialNews",
    "#BreakingNews", "#MarketUpdate", "#DailyBriefing", "#Money", "#Wealth", "#Capital", "#CapitalMarkets",
    "#Business", "#GlobalMarkets", "#Economics", "#WallStreet", "#Portfolio", "#Trader", "#Investor",
    "#DayTrader", "#SwingTrader", "#FinancialFreedom", "#Assets", "#Liquidity", "#WealthManagement",
    "#FinancialLiteracy", "#AssetAllocation", "#EconomicOutlook", "#MarketWrap", "#BusinessNews",
    "#PersonalFinance", "#FinancialPlanning", "#FinancialGoals", "#NetWorth", "#BullVsBear",
    
    // --- Forex (FX) & Currencies ---
    "#Forex", "#FXTrading", "#CurrencyTrading", "#ForeignExchange", "#ForexSignals", "#ForexLife",
    "#ForexMarket", "#Currency", "#CurrencyPairs", "#FXAnalysis", "#ForexEducation", "#Pip", "#Pips",
    "#ForexTrader", "#MajorPairs", "#MinorPairs", "#ExoticPairs", "#CrossPairs",
    // Major Currencies
    "#USD", "#Dollar", "#Greenback", "#EUR", "#Euro", "#JPY", "#Yen", "#GBP", "#Pound", "#Cable",
    "#CHF", "#SwissFranc", "#Swissy", "#CAD", "#Loonie", "#AUD", "#Aussie", "#NZD", "#Kiwi",
    // Major Pairs
    "#EURUSD", "#GBPUSD", "#USDJPY", "#USDCAD", "#AUDUSD", "#USDCHF", "#NZDUSD",
    // Euro Crosses
    "#EURJPY", "#EURGBP", "#EURCHF", "#EURAUD", "#EURCAD", "#EURNZD", "#EURNOK", "#EURSEK", "#EURPLN", "#EURTRY",
    // Yen Crosses
    "#GBPJPY", "#CHFJPY", "#CADJPY", "#AUDJPY", "#NZDJPY", "#TRYJPY", "#ZARJPY", "#MXNJPY",
    // Pound Crosses
    "#GBPAUD", "#GBPCAD", "#GBPCHF", "#GBPNZD", "#GBPSEK", "#GBPNOK",
    // Other Crosses
    "#AUDCAD", "#AUDCHF", "#AUDNZD", "#CADCHF", "#NZDCAD", "#NZDCHF", "#EURSGD", "#GBPSGD",
    // Exotic Currencies
    "#CNY", "#Yuan", "#CNH", "#OffshoreYuan", "#HKD", "#HongKongDollar", "#SGD", "#SingaporeDollar", "#KRW", "#KoreanWon",
    "#INR", "#IndianRupee", "#RUB", "#RussianRuble", "#BRL", "#BrazilianReal", "#ZAR", "#SouthAfricanRand",
    "#TRY", "#TurkishLira", "#MXN", "#MexicanPeso", "#NOK", "#NorwegianKrone", "#SEK", "#SwedishKrona",
    "#PLN", "#PolishZloty", "#DKK", "#DanishKrone", "#HUF", "#HungarianForint", "#CZK", "#CzechKoruna",
    "#ILS", "#IsraeliShekel", "#CLP", "#ChileanPeso", "#THB", "#ThaiBaht", "#IDR", "#IndonesianRupiah",
    "#PHP", "#PhilippinePeso", "#MYR", "#MalaysianRinggit", "#TWD", "#TaiwanDollar",
    // Exotic Pairs
    "#USDTRY", "#USDZAR", "#USDMXN", "#USDSGD", "#USDHKD", "#USDCNH", "#USDNOK", "#USDSEK", "#USDRUB", "#USDINR", "#USDBRL",
    "#EURZAR", "#EURMXN",

    // --- Cryptocurrencies & Blockchain ---
    "#Crypto", "#Cryptocurrency", "#Blockchain", "#CryptoTrading", "#CryptoNews", "#Altcoin",
    "#Altcoins", "#DeFi", "#DecentralizedFinance", "#NFT", "#NFTs", "#NFTCommunity", "#Metaverse",
    "#Web3", "#Web3Gaming", "#DAO", "#SmartContracts", "#DigitalAssets", "#CryptoCommunity", "#HODL",
    "#DiamondHands", "#PaperHands", "#CryptoGems", "#ToTheMoon", "#WhaleAlert",
    // Top-Tier Coins
    "#Bitcoin", "#BTC", "#Ethereum", "#ETH", "#Ripple", "#XRP", "#Cardano", "#ADA", "#Solana", "#SOL",
    "#BinanceCoin", "#BNB", "#Dogecoin", "#DOGE", "#Avalanche", "#AVAX", "#Polkadot", "#DOT",
    "#TRON", "#TRX", "#Chainlink", "#LINK", "#Polygon", "#MATIC", "#Toncoin", "#TON",
    // Popular & Mid-Cap Coins
    "#Litecoin", "#LTC", "#Stellar", "#XLM", "#Monero", "#XMR", "#Uniswap", "#UNI", "#Cosmos", "#ATOM",
    "#Aptos", "#APT", "#Sui", "#SUI", "#NEARProtocol", "#NEAR", "#InternetComputer", "#ICP",
    "#Hedera", "#HBAR", "#VeChain", "#VET", "#Filecoin", "#FIL", "#Algorand", "#ALGO", "#Tezos", "#XTZ",
    "#Fantom", "#FTM", "#Aave", "#AAVE", "#Maker", "#MKR", "#TheGraph", "#GRT", "#Decentraland", "#MANA",
    "#TheSandbox", "#SAND", "#AxieInfinity", "#AXS", "#Gala", "#GALA", "#Injective", "#INJ",
    "#Thorchain", "#RUNE", "#Kaspa", "#KAS", "#Render", "#RNDR", "#Optimism", "#OP", "#Arbitrum", "#ARB",
    "#LidoDAO", "#LDO", "#FetchAI", "#FET", "#SingularityNET", "#AGIX", "#Stacks", "#STX", "#Theta", "#THETA",
    "#Quant", "#QNT", "#ImmutableX", "#IMX", "#Celestia", "#TIA", "#Sei", "#SEI", "#Synthetix", "#SNX",
    "#Helium", "#HNT", "#Arweave", "#AR", "#CurveDAO", "#CRV", "#JasmyCoin", "#JASMY",
    // Meme Coins
    "#ShibaInu", "#SHIB", "#Pepe", "#PEPE", "#Bonk", "#BONK", "#Dogwifhat", "#WIF",
    // Crypto Concepts
    "#CryptoMining", "#Staking", "#YieldFarming", "#LiquidityPool", "#ImpermanentLoss", "#CryptoWallet",
    "#HardwareWallet", "#ColdStorage", "#HotWallet", "#DEX", "#CEX", "#Layer1", "#Layer2", "#Layer0",
    "#Rollups", "#ZKRollups", "#OptimisticRollups", "#Sharding", "#BitcoinHalving", "#EthereumMerge", "#GasFees",
    "#Oracles", "#Tokenomics", "#DigitalCurrency", "#CBDC", "#CryptoRegulation", "#SEC", "#CFTC", "#MiCA",
    "#Tokenization", "#RWA", "#RealWorldAssets", "#Stablecoin", "#USDT", "#Tether", "#USDC", "#DAI",
    "#OnChainAnalysis", "#ProofOfWork", "#PoW", "#ProofOfStake", "#PoS", "#ProofOfHistory", "#PoH",
    "#Airdrop", "#ICO", "#IEO", "#IDO", "#SeedPhrase", "#Cryptography", "#BitcoinPizzaDay",
    
    // --- Stocks, Equities & Indices ---
    "#Stocks", "#StockMarket", "#Equities", "#Shares", "#StockTrading", "#StockPicking", "#DayTradingStocks",
    "#ValueInvesting", "#GrowthInvesting", "#DividendInvesting", "#BlueChipStocks", "#PennyStocks",
    "#Earnings", "#EarningsSeason", "#EPS", "#Shareholder", "#IPO", "#MarketCap", "#PricetoEarnings", "#PERatio",
    "#StockBuyback", "#ActivistInvestor", "#MergersAndAcquisitions", "#MNA", "#SPAC",
    // Major US Indices
    "#SP500", "#SPX", "$SPX", "#DowJones", "#DJIA", "$DJI", "#NASDAQ", "#NDX", "$NDX", "#Russell2000", "#RUT",
    // Major Global Indices
     "#ForexAlgorithmic",            // Algorithmic strategies specifically for Forex
    "#CryptoAlgorithmic",           // Algorithmic strategies specifically for Crypto
    "#FTSE100", "#DAX40", "#CAC40", "#Nikkei225", "#HangSeng", "#HSI", "#ASX200", "#EuroStoxx50",
    "#SENSEX", "#NIFTY50", "#KOSPI", "#IBOVESPA", "#TSX", "#MOEX", "#ShanghaiComposite", "#CSI300",
    // Major Company Tickers (Examples)
    "#AAPL", "#MSFT", "#GOOG", "#GOOGL", "#AMZN", "#NVDA", "#TSLA", "#META", "#BRKA", "#BRKB", "#JPM", "#V",
    "#JNJ", "#WMT", "#PG", "#MA", "#UNH", "#HD", "#DIS", "#NKE", "#CRM", "#BAC", "#XOM", "#CVX", "#KO", "#PEP",
    "#PFE", "#MRK", "#LLY", "#ABBV", "#MCD", "#CSCO", "#INTC", "#AMD", "#QCOM", "#BA", "#CAT", "#GS", "#ORCL",
    // Sectors & Industries
    "#TechStocks", "#Financials", "#Healthcare", "#EnergySector", "#Industrials", "#RealEstate", "#REIT",
    "#Utilities", "#ConsumerDiscretionary", "#ConsumerStaples", "#Materials", "#CommunicationServices",
    "#Software", "#Hardware", "#SaaS", "#Cybersecurity", "#FinTech", "#CleanEnergy", "#RenewableEnergy",
    "#EV", "#ArtificialIntelligence", "#AI", "#CloudComputing", "#Semiconductors", "#Biotech", "#Pharma",
    "#Defense", "#Aerospace", "#Retail", "#Ecommerce", "#Gaming", "#Streaming", "#Airlines", "#Automotive",
    "#Banking", "#Insurance", "#BigOil",
     "#Forex",                 // The primary focus for currency trading
    "#Crypto",                // The primary focus for digital asset trading
    "#Stocks",                // Essential for understanding broader market context
    "#Commodities",           // Key drivers of inflation and macro trends
    "#InterestRates",         // Major influence on all financial markets
     "#Volatility",            // The engine of profit and loss
    "#Liquidity",
     "#RiskManagement",        // The absolute foundation of all trading
    "#TradingPsychology",     // Mastery of self is key to consistency
    "#PriceAction",           // The purest form of market analysis
    // --- Economic Indicators & Events ---
    "#Economy", "#EconomicData", "#Inflation", "#CPI", "#CoreCPI", "#PPI", "#CorePPI", "#Stagflation", "#Deflation",
    "#Recession", "#GDP", "#GDPGrowth", "#InterestRates", "#NFP", "#NonFarmPayrolls", "#Unemployment",
    "#JobsReport", "#RetailSales", "#CoreRetailSales", "#ConsumerConfidence", "#ConsumerSentiment",
    "#ManufacturingPMI", "#ServicesPMI", "#ISM", "#DurableGoods", "#InitialJoblessClaims", "#ContinuingClaims",
    "#HousingStarts", "#BuildingPermits", "#ExistingHomeSales", "#NewHomeSales", "#CaseShiller",
    "#TradeBalance", "#CurrentAccount", "#FactoryOrders", "#IndustrialProduction", "#CapacityUtilization",
    "#BeigeBook", "#LeadingIndicators",
    
    // --- Central Banks & Monetary Policy ---
    "#CentralBank", "#MonetaryPolicy", "#FederalReserve", "#TheFed", "#FOMC", "#FOMCMinutes", "#JeromePowell",
    "#ECB", "#EuropeanCentralBank", "#ChristineLagarde", "#BOE", "#BankOfEngland",
    "#BOJ", "#BankOfJapan", "#SNB", "#SwissNationalBank", "#BOC", "#BankOfCanada",

    "#RBA", "#ReserveBankOfAustralia", "#RBNZ", "#ReserveBankOfNewZealand", "#PBOC", "#PeoplesBankOfChina",
    "#RateHike", "#RateCut", "#InterestRateHike", "#QuantitativeEasing", "#QE",
    "#QuantitativeTightening", "#QT", "#Hawkish", "#Dovish", "#ForwardGuidance", "#YieldCurveControl",
    "#BalanceSheet", "#CentralBankPolicy", "#DotPlot", "#FedWatch", "#InflationTargeting",
    
    // --- Trading Strategies & Technical Analysis ---
    "#MarketAnalysis", "#TechnicalAnalysis", "#TA", "#FundamentalAnalysis", "#FA", "#PriceAction",
    "#Candlesticks", "#ChartPatterns", "#Indicators", "#DayTrading", "#SwingTrading", "#Scalping",
    "#PositionTrading", "#AlgorithmicTrading", "#AlgoTrading", "#HFT", "#PairsTrading", "#Arbitrage",
    "#Support", "#Resistance", "#SupportAndResistance", "#Trendline", "#Channels", "#Fibonacci", "#Fibs",
    "#MovingAverage", "#EMA", "#SMA", "#RSI", "#MACD", "#BollingerBands", "#Ichimoku", "#IchimokuCloud", "#Stochastics",
    "#BreakoutTrading", "#Pullback", "#Reversal", "#Continuation", "#MeanReversion", "#TrendFollowing",
    // Candlestick Patterns
    "#Doji", "#Hammer", "#InvertedHammer", "#HangingMan", "#ShootingStar", "#EngulfingPattern",
    "#BullishEngulfing", "#BearishEngulfing", "#Harami", "#PiercingPattern", "#DarkCloudCover",
    "#MorningStar", "#EveningStar", "#ThreeWhiteSoldiers", "#ThreeBlackCrows", "#Marubozu",
    // Chart Patterns
    "#DoubleTop", "#DoubleBottom", "#TripleTop", "#TripleBottom", "#HeadAndShoulders", "#InverseHeadAndShoulders",
    "#BullFlag", "#BearFlag", "#Pennant", "#Wedge", "#FallingWedge", "#RisingWedge", "#Triangle",
    "#AscendingTriangle", "#DescendingTriangle", "#SymmetricalTriangle", "#CupAndHandle", "#Rectangle",
    // TA Concepts
    "#MovingAverageCrossover", "#GoldenCross", "#DeathCross", "#RSIOversold", "#RSIOverbought",
    "#MACDCrossover", "#Divergence", "#BullishDivergence", "#BearishDivergence", "#HiddenDivergence",
    "#ElliottWave", "#ImpulseWave", "#CorrectiveWave", "#WaveAnalysis", "#MarketStructure", "#HigherHighs",
    "#LowerLows", "#MarketStructureShift", "#BOS", "#CHoCH", "#VolumeAnalysis", "#OnBalanceVolume", "#OBV",
    // Advanced TA / Niche Strategies
    "#WyckoffMethod", "#Accumulation", "#Distribution", "#Spring", "#Upthrust", "#SignOfStrength", "#CompositeMan",
    "#ICT", "#SmartMoneyConcepts", "#SMC", "#OrderFlow", "#LiquidityGrab", "#StopHunt", "#OrderBlock",
    "#BreakerBlock", "#MitigationBlock", "#FairValueGap", "#FVG", "#Imbalance", "#JudasSwing", "#PremiumAndDiscount",
    "#MarketProfile", "#VolumeProfile", "#PointOfControl", "#POC", "#ValueArea", "#TPO", "#Gann", "#HarmonicPatterns",
    "#Gartley", "#BatPattern", "#ButterflyPattern", "#CrabPattern",
    
    // --- Risk Management & Trading Psychology ---
    "#RiskManagement", "#StopLoss", "#TakeProfit", "#RiskReward", "#PositionSizing", "#TradeManagement",
    "#TradingPsychology", "#TraderMindset", "#TradingPlan", "#TradingJournal", "#Discipline", "#Patience",
    "#GreedAndFear", "#FOMO", "#FearOfMissingOut", "#FUD", "#FearUncertaintyDoubt", "#RevengeTrading",
    "#Overtrading", "#ConfirmationBias", "#LossAversion", "#AnalysisParalysis", "#EmotionalTrading",
    "#Mindfulness", "#MentalEdge", "#TradingMistakes", "#TradingRules", "#CapitalPreservation",
    
    // --- Market Sentiment & Conditions ---
    "#MarketSentiment", "#Bullish", "#Bearish", "#BullMarket", "#BearMarket", "#Correction",
    "#MarketCrash", "#Volatility", "#VIX", "$VIX", "#RiskOn", "#RiskOff", "#Contrarian", "#ContrarianInvesting",
    "#HerdingBehavior", "#Capitulation", "#ATH", "#AllTimeHigh", "#ATL", "#AllTimeLow", "#Euphoria", "#PanicSelling",
    
    // --- Commodities ---
    "#Commodities", "#CommodityTrading",
    // Metals
    "#Gold", "#XAUUSD", "#Silver", "#XAGUSD", "#Platinum", "#XPTUSD", "#Palladium", "#XPDUSD",
    "#PreciousMetals", "#IndustrialMetals", "#Copper", "#Aluminum", "#Zinc", "#Nickel", "#IronOre", "#Lithium",

    // Energy
    "#Oil", "#CrudeOil", "#WTI", "#BrentCrude", "#NaturalGas", "#NatGas", "#HenryHub", "#OPEC", "#OPECPlus",
    "#EnergyCrisis", "#Gasoline", "#HeatingOil", "#Uranium",
    // Agricultural
    "#Agriculture", "#SoftCommodities", "#Corn", "#Wheat", "#Soybeans", "#Coffee", "#Sugar", "#Cocoa", "#Cotton",
    "#Lumber", "#Livestock", "#LeanHogs", "#LiveCattle",
    
    // --- Bonds & Fixed Income ---
    "#Bonds", "#Treasuries", "#YieldCurve", "#FixedIncome", "#GovernmentBonds", "#CorporateBonds",
    "#JunkBonds", "#HighYieldBonds", "#MunicipalBonds", "#BondYields", "#10YearTreasury", "#2YearTreasury",
    "#YieldCurveInversion", "#BondMarket", "#CreditSpreads", "#Duration",
    
    // --- Geopolitics & Global Events ---
    "#Geopolitics", "#WorldNews", "#TradeWar", "#TradeDeals", "#Sanctions", "#Politics", "#Elections",
    "#GlobalTensions", "#SupplyChain", "#GlobalConflict", "#PoliticalRisk",
    
    // --- Derivatives & Miscellaneous Finance ---
    "#Derivatives", "#OptionsTrading", "#FuturesTrading", "#CFD", "#HedgeFund", "#VentureCapital", "#VC",
    "#PrivateEquity", "#PE", "#ETF", "#ETFs", "#IndexFunds", "#MutualFunds", "#Puts", "#Calls", "#OptionsStrategy",
    "#TheGreeks", "#Delta", "#Gamma", "#Theta", "#Vega", "#VolatilitySmile", "#Leverage", "#Margin",
    
    // --- Educational & Community ---
    "#LearnToTrade", "#TradingForBeginners", "#TradingEducation", "#TradingTips", "#Trading101",
    "#TraderLife", "#DayTraderLife", "#FullTimeTrader", "#TradingCommunity", "#Mentorship", "#StockMarketTips",
      "#ResearchAnalyst", "#Trader", "#ExecutionTrader", "#PropTrader", "#MarketMaker", "#InvestmentBanker"
}.AsReadOnly();

        // A single, thread-safe queue to hold the shuffled hashtags for the channel.
        private ConcurrentQueue<string> _hashtagQueue;

        // A lock object specifically for the less-frequent refill operation to prevent race conditions.
        private readonly object _refillLock = new();

        /// <summary>
        /// Initializes the HashtagService by creating the first shuffled queue.
        /// </summary>
        public HashtagService()
        {
            _hashtagQueue = CreateNewShuffledConcurrentQueue();
        }

        /// <summary>
        /// Gets a specified number of unique, non-repeating hashtags from the queue.
        /// </summary>
        /// <param name="count">The number of hashtags to return.</param>
        /// <returns>A formatted string of space-separated hashtags.</returns>
        public string GetRandomHashtags(int count = 3)
        {
            if (count <= 0) return string.Empty;

            var selectedHashtags = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                // Attempt to get the next hashtag from the queue.
                if (!_hashtagQueue.TryDequeue(out var hashtag))
                {
                    // If the queue is empty, we need to refill it.
                    // We lock here to ensure only one thread performs the refill.
                    lock (_refillLock)
                    {
                        // Double-check if another thread already refilled it while we waited for the lock.
                        if (!_hashtagQueue.TryDequeue(out hashtag))
                        {
                            _hashtagQueue = CreateNewShuffledConcurrentQueue();

                            // Try one last time after refilling.
                            if (!_hashtagQueue.TryDequeue(out hashtag))
                            {
                                // This only happens if the master list is empty. Break the loop.
                                break;
                            }
                        }
                    }
                }
                selectedHashtags.Add(hashtag);
            }

            return string.Join(" ", selectedHashtags);
        }

        /// <summary>
        /// Creates a new, thread-safe ConcurrentQueue with all hashtags in a random order.
        /// </summary>
        private static ConcurrentQueue<string> CreateNewShuffledConcurrentQueue()
        {
            if (!AllHashtags.Any())
            {
                return new ConcurrentQueue<string>();
            }

            // Use an efficient, in-place shuffle for better performance and memory usage.
            var shuffledArray = AllHashtags.ToArray();
            Shuffle(shuffledArray);

            return new ConcurrentQueue<string>(shuffledArray);
        }

        /// <summary>
        /// Implements the Fisher-Yates shuffle algorithm for in-place, unbiased randomization.
        /// </summary>
        private static void Shuffle<T>(T[] array)
        {
            int n = array.Length;
            // Use Random.Shared for a thread-safe, shared Random instance (.NET 6+).
            var random = Random.Shared;

            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]); // Swap using tuple deconstruction
            }
        }
    }
}