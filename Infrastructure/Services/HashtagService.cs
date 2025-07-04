using Application.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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

    // --- Forex (FX) & Currencies ---
    "#Forex", "#FXTrading", "#CurrencyTrading", "#ForeignExchange", "#ForexSignals", "#ForexLife",
    "#ForexMarket", "#Currency", "#CurrencyPairs", "#FXAnalysis", "#ForexEducation", "#Pip", "#Pips",
    // Major Currencies
    "#USD", "#Dollar", "#EUR", "#Euro", "#JPY", "#Yen", "#GBP", "#Pound", "#CHF", "#SwissFranc",
    "#CAD", "#Loonie", "#AUD", "#Aussie", "#NZD", "#Kiwi",
    // Major Pairs
    "#EURUSD", "#GBPUSD", "#USDJPY", "#USDCAD", "#AUDUSD", "#USDCHF", "#NZDUSD",
    // Euro Crosses
    "#EURJPY", "#EURGBP", "#EURCHF", "#EURAUD", "#EURCAD", "#EURNZD", "#EURNOK", "#EURSEK",
    // Yen Crosses
    "#GBPJPY", "#CHFJPY", "#CADJPY", "#AUDJPY", "#NZDJPY",
    // Pound Crosses
    "#GBPAUD", "#GBPCAD", "#GBPCHF", "#GBPNZD",
    // Other Crosses
    "#AUDCAD", "#AUDCHF", " #AUDNZD", "#CADCHF", "#NZDCAD", "#NZDCHF",
    // Exotic Currencies & Pairs
    "#CNY", "#CNH", "#HKD", "#SGD", "#KRW", "#INR", "#RUB", "#BRL", "#ZAR", "#TRY", "#MXN",
    "#NOK", "#SEK", "#PLN", "#DKK", "#HUF", "#CZK", "#ILS", "#CLP", "#THB", "#IDR", "#PHP",
    "#USDTRY", "#USDZAR", "#USDMXN", "#USDSGD", "#USDHKD", "#USDCNH", "#USDNOK", "#USDSEK",

    // --- Cryptocurrencies & Blockchain ---
    "#Crypto", "#Cryptocurrency", "#Blockchain", "#CryptoTrading", "#CryptoNews", "#Altcoin",
    "#Altcoins", "#DeFi", "#DecentralizedFinance", "#NFT", "#NFTs", "#NFTCommunity", "#Metaverse",
    "#Web3", "#Web3Gaming", "#DAO", "#SmartContracts", "#DigitalAssets", "#CryptoCommunity", "#HODL",
    // Top-Tier Coins
    "#Bitcoin", "#BTC", "#Ethereum", "#ETH", "#Ripple", "#XRP", "#Cardano", "#ADA", "#Solana", "#SOL",
    "#BinanceCoin", "#BNB", "#Dogecoin", "#DOGE", "#Avalanche", "#AVAX", "#Polkadot", "#DOT",
    "#TRON", "#TRX", "#Chainlink", "#LINK", "#Polygon", "#MATIC",
    // Popular & Mid-Cap Coins
    "#Litecoin", "#LTC", "#Stellar", "#XLM", "#Monero", "#XMR", "#Uniswap", "#UNI", "#Cosmos", "#ATOM",
    "#Toncoin", "#TON", "#Aptos", "#APT", "#Sui", "#SUI", "#NEARProtocol", "#NEAR", "#InternetComputer", "#ICP",
    "#Hedera", "#HBAR", "#VeChain", "#VET", "#Filecoin", "#FIL", "#Algorand", "#ALGO", "#Tezos", "#XTZ",
    "#Fantom", "#FTM", "#Aave", "#AAVE", "#Maker", "#MKR", "#TheGraph", "#GRT", "#Decentraland", "#MANA",
    "#TheSandbox", "#SAND", "#AxieInfinity", "#AXS", "#Gala", "#GALA", "#Injective", "#INJ",
    "#Thorchain", "#RUNE", "#Kaspa", "#KAS", "#Render", "#RNDR", "#Optimism", "#OP", "#Arbitrum", "#ARB",
    "#LidoDAO", "#LDO", "#FetchAI", "#FET", "#SingularityNET", "#AGIX", "#Pepe", "#PEPE", "#Bonk", "#BONK",
    "#Dogwifhat", "#WIF",
    // Crypto Concepts
    "#CryptoMining", "#Staking", "#YieldFarming", "#CryptoWallet", "#HardwareWallet", "#ColdStorage",
    "#HotWallet", "#DEX", "#CEX", "#Layer1", "#Layer2", "#BitcoinHalving", "#EthereumMerge", "#GasFees",
    "#Oracles", "#Tokenomics", "#DigitalCurrency", "#CBDC", "#CryptoRegulation", "#SEC", "#Tokenization",
    "#Stablecoin", "#USDT", "#USDC", "#DAI", "#OnChainAnalysis", "#ProofOfWork", "#ProofOfStake",

    // --- Stocks, Equities & Indices ---
    "#Stocks", "#StockMarket", "#Equities", "#Shares", "#StockTrading", "#StockPicking", "#DayTradingStocks",
    "#ValueInvesting", "#GrowthInvesting", "#DividendInvesting", "#BlueChipStocks", "#PennyStocks",
    "#Earnings", "#EarningsSeason", "#EPS", "#Shareholder", "#IPO", "#MarketCap", "#PricetoEarnings",
    "#StockBuyback", "#ActivistInvestor", "#MergersAndAcquisitions",
    // Major US Indices
    "#SP500", "#SPX", "#DowJones", "#DJIA", "#NASDAQ", "#NDX", "#Russell2000",
    // Major Global Indices
    "#FTSE100", "#DAX40", "#CAC40", "#Nikkei225", "#HangSeng", "#HSI", "#ASX200", "#EuroStoxx50",
    "#SENSEX", "#NIFTY50", "#KOSPI", "#IBOVESPA", "#TSX", "#MOEX", "#ShanghaiComposite",
    // Major Company Tickers (Examples)
    "#AAPL", "#MSFT", "#GOOGL", "#AMZN", "#NVDA", "#TSLA", "#META", "#BRK", "#JPM", "#V", "#JNJ", "#WMT",
    "#PG", "#MA", "#UNH", "#HD", "#DIS", "#NKE", "#CRM", "#BAC",
    // Sectors
    "#TechStocks", "#Financials", "#Healthcare", "#EnergySector", "#Industrials", "#RealEstate",
    "#Utilities", "#ConsumerDiscretionary", "#ConsumerStaples", "#Materials", "#Semiconductors",
    "#Biotech", "#EV", "#AI", "#ArtificialIntelligence", "#CloudComputing",

    // --- Economic Indicators & Events ---
    "#Economy", "#EconomicData", "#Inflation", "#CPI", "#CoreCPI", "#PPI", "#Recession", "#GDP", "#GDPGrowth",
    "#InterestRates", "#NFP", "#NonFarmPayrolls", "#Unemployment", "#JobsReport", "#RetailSales",

    "#ConsumerConfidence", "#ConsumerSentiment", "#ManufacturingPMI", "#ServicesPMI", "#ISM",
    "#DurableGoods", "#InitialJoblessClaims", "#HousingStarts", "#BuildingPermits", "#TradeBalance",
    "#CurrentAccount",
    
    // --- Central Banks & Monetary Policy ---
    "#CentralBank", "#MonetaryPolicy", "#FederalReserve", "#TheFed", "#FOMC", "#JeromePowell",
    "#ECB", "#EuropeanCentralBank", "#ChristineLagarde", "#BOE", "#BankOfEngland",
    "#BOJ", "#BankOfJapan", "#SNB", "#SwissNationalBank", "#BOC", "#BankOfCanada",
    "#RBA", "#ReserveBankOfAustralia", "#RBNZ", "#ReserveBankOfNewZealand", "#PBOC",
    "#RateHike", "#RateCut", "#InterestRateHike", "#QuantitativeEasing", "#QE",
    "#QuantitativeTightening", "#QT", "#Hawkish", "#Dovish", "#ForwardGuidance", "#YieldCurveControl",
    "#BalanceSheet", "#CentralBankPolicy",

    // --- Trading Strategies & Technical Analysis ---
    "#MarketAnalysis", "#TechnicalAnalysis", "#TA", "#FundamentalAnalysis", "#FA", "#PriceAction",
    "#Candlesticks", "#ChartPatterns", "#Indicators", "#DayTrading", "#SwingTrading", "#Scalping",
    "#PositionTrading", "#AlgorithmicTrading", "#HFT", "#PairsTrading", "#Arbitrage",
    "#Support", "#Resistance", "#SupportAndResistance", "#Trendline", "#Channels", "#Fibonacci",
    "#MovingAverage", "#EMA", "#SMA", "#RSI", "#MACD", "#BollingerBands", "#Ichimoku", "#Stochastics",
    "#BreakoutTrading", "#Pullback", "#Reversal", "#Continuation", "#Doji", "#Hammer", "#EngulfingPattern",
    "#MovingAverageCrossover", "#GoldenCross", "#DeathCross", "#RSIOversold", "#RSIOverbought",
    "#MACDCrossover", "#Divergence", "#ElliottWave", "#WyckoffMethod", "#MarketStructure",
    "#OrderFlow", "#LiquidityGrab", "#OrderBlock",
    
    // --- Risk Management & Trading Psychology ---
    "#RiskManagement", "#StopLoss", "#TakeProfit", "#RiskReward", "#PositionSizing", "#TradeManagement",
    "#TradingPsychology", "#TradingPlan", "#TradingJournal", "#Discipline", "#Patience",
    "#GreedAndFear", "#FOMO", "#FUD", "#ConfirmationBias", "#LossAversion",
    
    // --- Market Sentiment & Conditions ---
    "#MarketSentiment", "#Bullish", "#Bearish", "#BullMarket", "#BearMarket", "#Correction",
    "#MarketCrash", "#Volatility", "#VIX", "#RiskOn", "#RiskOff", "#Contrarian", "#ContrarianInvesting",
    "#HerdingBehavior", "#Capitulation", "#ATH", "#AllTimeHigh", "#ATL", "#AllTimeLow",
    
    // --- Commodities ---
    "#Commodities", "#CommodityTrading",
    // Metals
    "#Gold", "#XAUUSD", "#Silver", "#XAGUSD", "#Platinum", "#Palladium", "#Copper", "#Aluminum", "#Zinc", "#Nickel",
    // Energy
    "#Oil", "#CrudeOil", "#WTI", "#BrentCrude", "#NaturalGas", "#OPEC", "#EnergyCrisis",
    // Agricultural
    "#Corn", "#Wheat", "#Soybeans", "#Coffee", "#Sugar", "#Cocoa", "#Cotton", "#Lumber", "#Livestock",
    
    // --- Bonds & Fixed Income ---
    "#Bonds", "#Treasuries", "#YieldCurve", "#FixedIncome", "#GovernmentBonds", "#CorporateBonds",
    "#JunkBonds", "#BondYields", "#10YearTreasury", "#YieldCurveInversion",
    
    // --- Geopolitics & Global Events ---
    "#Geopolitics", "#WorldNews", "#TradeWar", "#TradeDeals", "#Sanctions", "#Politics", "#Elections",
    "#GlobalTensions", "#SupplyChain",
    
    // --- Derivatives & Miscellaneous Finance ---
    "#Derivatives", "#OptionsTrading", "#FuturesTrading", "#CFD", "#HedgeFund", "#VentureCapital", "#PrivateEquity"
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