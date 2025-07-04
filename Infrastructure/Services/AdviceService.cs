using Application.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implements a robust, stateful system for providing unique advice.
    /// It manages a single, shared queue for a channel and individual queues for each user
    /// to prevent advice from repeating until the respective lists are exhausted.
    /// This service must be registered as a Singleton to maintain state.
    /// </summary>
    public class AdviceService : IAdviceService
    {
        // 1. --- MASTER DATA SOURCE ---
        private static readonly IReadOnlyList<string> AllAdvices = new List<string>
    {
    
// --- Mindset & Psychology ---
"Discipline is your bridge between goals and accomplishment in trading.",
"Patience in trading is not inactivity; it's calculated waiting.",
"The market rewards discipline, not excitement or intelligence.",
"Approach trading as a game of probabilities, not certainties.",
"Emotional detachment is the key to objective, logical decision-making.",
"Your greatest opponent in the markets is the person in the mirror.",
"Let go of your ego; the market neither knows nor cares about your opinions.",
"Remain humble in victory and resilient in defeat.",
"Avoid 'revenge trading' after a loss at all costs; it only compounds the damage.",
"If you feel anxious about a trade, your position size is too large.",
"Success in trading is a marathon, not a sprint. Focus on longevity.",
"Base your confidence on your tested strategy, not on your recent results.",
"A winning streak can be more dangerous than a losing streak if it breeds recklessness.",
"The best trade is often no trade at all. Capital preservation is a valid position.",
"The market is a device for transferring wealth from the impatient to the patient.",
"You don't need a high win rate if your winners are substantially larger than your losers.",
"Concentrate on executing your plan flawlessly; the profits are a byproduct.",
"Trade the price action you see, not the future you predict.",
"Isolate yourself from market noise and the opinions of others. Trust your system.",
"Accept the inherent randomness of the market in the short term.",
"The market can remain irrational longer than you can remain solvent. Respect its power.",
"Your objective isn't to predict the future, but to react intelligently to the present.",
"Clarity of mind is paramount. If stressed, tired, or unfocused, step away.",
"Consistency in your daily process leads to consistency in your long-term results.",
"Embrace uncertainty and learn to thrive in it; it's the nature of the beast.",
"Do not anticipate a market move. Wait for it to happen, then react.",
"The pain of regretting a trade you didn't take per your rules is worse than a small loss.",
"Avoid analysis paralysis. A good plan, executed now, is better than a perfect plan tomorrow.",
"Celebrate your discipline and good execution, not just the profitable outcomes.",
"Don't seek constant action; seek high-probability, high-quality setups.",
"Control what you can control: your entry, your exit, your position size, and your emotions.",
"Your trading journey is unique. Don't compare your results to anyone else's.",
"Mental rehearsal of both winning and losing scenarios prepares you for live trading.",
"The market has no memory. Your last trade's outcome has no bearing on the next.",
"To be a successful trader is to be a master of managing your own psychology.",
"A losing trade does not make you a loser, just as a winning trade does not make you a genius.",
"Never get emotionally attached to a position. It is just a trade.",
"Be a systematic thinker, not an emotional reactor.",
"Simplicity in your approach often leads to the greatest clarity and success.",
"The desire for certainty is a primary cause of trading failure.",

// --- Risk Management: The Cornerstone of Trading ---
"Capital preservation is Rule #1. Rule #2: Never forget Rule #1.",
"Never, ever risk more than you are prepared and can afford to lose.",
"A hard stop-loss is your non-negotiable insurance policy on every single trade.",
"Define your exit strategy for both profit and loss before you even think about entering.",
"Position sizing is a more critical decision than your entry point.",
"Cut your losses quickly and without hesitation. Small losses are a business expense.",
"Let your winners run. Don't snatch small profits out of fear.",
"The primary goal of risk management is to ensure you survive long enough to be successful.",
"Never add to a losing position. That's 'averaging down' and it's a path to ruin.",
"Your minimum risk-to-reward ratio should be at least 1:2. Risk less to make more.",
"Avoid taking trades with a poor risk-to-reward profile, no matter how certain they seem.",
"Protect your breakeven point by moving your stop-loss once a trade has moved significantly in your favor.",
"Understand the concept of a maximum drawdown and be mentally prepared for it.",
"Diversification helps manage unsystematic risk, but it won't save you from a market-wide crash.",
"Never widen your stop-loss to 'give a trade more room'. You are simply increasing your risk.",
"Use limit orders to ensure you get the entry and exit prices you want, reducing slippage.",
"Take profits at pre-determined, logical levels based on your analysis. Greed kills accounts.",
"Your trading account is the lifeblood of your business. Protect it with extreme prejudice.",
"A series of small, manageable losses is infinitely better and more professional than one catastrophic loss.",
"Understand the impact of correlation between your positions. Don't place five trades that are essentially the same bet.",
"Risk management gives you longevity, and with longevity comes opportunity.",
"Always know the maximum dollar amount you can lose on a trade before you enter.",
"Don't confuse a high win rate with a profitable system. The size of wins vs. losses is what matters.",
"Risk is not just about losing money; it's also about opportunity cost.",
"Your risk tolerance may change over time. Re-evaluate it periodically.",
"The amount of leverage you use should be a conscious and conservative choice.",
"Never hold a position through a major, market-moving news event without a clear plan and stop-loss.",
"A profitable trader thinks first about how much they can lose, an amateur thinks only of how much they can win.",
"Never let a winning trade turn into a losing one.",
"Risk is the price you pay for opportunity.",

// --- Strategy & Technical Analysis ---
"The trend is your friend until it bends at the end. Trade with it.",
"Keep your technical analysis simple and clean. A cluttered chart leads to a cluttered mind and poor decisions.",
"Support and resistance are dynamic zones or areas, not exact, unbreakable lines.",
"Look for confluence: where multiple technical signals and patterns align to confirm a trade idea.",
"Volume precedes price. Pay close attention to volume patterns for clues about trend strength.",
"Price action is the purest and most reliable indicator of all.",
"Master one trading strategy completely before you attempt to learn ten.",
"Thoroughly backtest your strategy across a wide range of historical market conditions.",
"Always create a trading plan, and then religiously trade that plan.",
"A clear uptrend is defined by higher highs and higher lows. Don't fight it; join it.",
"A clear downtrend is defined by lower highs and lower lows. Don't try to catch a falling knife.",
"Moving averages are lagging indicators; use them for trend confirmation, not for primary signals.",
"Candlestick patterns are significantly more reliable when they appear at key support or resistance levels.",
"Understand the critical difference between a minor pullback within a trend and a major trend reversal.",
"The most obvious and easiest-to-spot trades are often the most effective.",
"Don't force a trade if your specific setup isn't present. Patience is a key part of any strategy.",
"Chart patterns like triangles, flags, and head-and-shoulders are visual representations of market psychology.",
"Always wait for a price candle to close before making a trading decision based on its pattern.",
"The time frame you trade on dictates your style. Ensure your analysis and execution are consistent with it.",
"The purpose of technical analysis is to identify high-probability setups and manage risk, not to predict the future.",
"A breakout is not confirmed until there is a retest of the broken level or a strong follow-through on high volume.",
"Divergence between price and an oscillator (like RSI or MACD) can be a powerful leading indicator of a potential reversal.",
"Market structure is king. Identify swing highs and lows to understand the current market environment.",
"The best entry point often comes after the initial impulse move, during a period of consolidation or pullback.",
"False breakouts are common. Have a plan for what to do if a breakout fails.",
"Use multiple time frames for a more comprehensive view: long-term for trend, short-term for entry.",
"Volatility is not the same as risk. Understand how to measure and adapt to changing volatility.",
"Indicators should simplify your decision-making, not complicate it.",
"Never rely on a single indicator. Look for a combination of confirming factors.",
"The market alternates between periods of trend and periods of consolidation. Identify which phase you're in.",

// --- Fundamental Analysis & Market Awareness ---
"Understand the fundamental economic and business drivers of the assets you trade.",
"Be acutely aware of major economic news releases on the calendar. Don't get caught by surprise.",
"Interest rates, inflation (CPI), and employment data (NFP) are consistent, major market movers.",
"Don't trade the news headlines themselves; trade the market's collective reaction to the news.",
"A great company does not always equal a great stock investment, especially if it's overpriced.",
"Geopolitical events can inject sudden, unpredictable volatility into the markets.",
"Central bank policy and statements are primary drivers of currency and equity market trends.",
"A strong economy does not always equate to a strong stock market, and vice versa.",
"Read the annual (10-K) and quarterly (10-Q) reports of companies you invest in.",
"Thoroughly understand the competitive landscape and 'moat' of the businesses in your portfolio.",
"Analyze supply and demand dynamics for commodities and currencies.",
"Follow earnings reports during earnings season, as they can set the tone for the entire market.",
"Look at valuation metrics like P/E, P/S, and P/B to gauge if an asset is cheap or expensive relative to its peers.",
"A market's reaction to bad news can be very telling. If it shrugs off bad news, it's a sign of strength.",
"Pay attention to what 'smart money' (institutional investors) is doing by following fund flows and positioning reports.",

// --- Long-Term Investing Principles ---
"Time in the market consistently beats timing the market.",
"Compound interest is the eighth wonder of the world. Start as early as possible and be consistent.",
"Invest in high-quality businesses you understand and believe in for the long haul.",
"Buy and hold quality assets. Simplicity and patience are often the most effective strategies.",
"Dollar-cost averaging is a powerful technique to remove emotion and reduce market timing risk.",
"Your investment horizon—the length of time you plan to hold an asset—should determine your risk tolerance.",
"Reinvest your dividends and capital gains to accelerate the power of compounding.",
"A diversified, low-cost index fund (like an S&P 500 ETF) is one of the most reliable ways to build long-term wealth.",
"Be greedy when others are fearful, and be fearful when others are greedy. - Warren Buffett",
"Ignore the distracting short-term noise. Focus on your long-term financial goals.",
"Don't check your long-term portfolio every day. It will only tempt you to make emotional decisions.",
"Understand that market corrections and bear markets are a normal, healthy part of the long-term cycle.",
"Never invest money that you will need in the short term (less than 3-5 years).",
"Asset allocation is responsible for a majority of your portfolio's returns.",
"Invest in secular trends, like technology, demographics, or sustainability, for long-term growth.",

// --- Learning & Continuous Improvement ---
"Keep a detailed trading journal. It is your single most valuable and personalized teacher.",
"Rigorously analyze your losing trades to learn lessons. Analyze your winning trades to reinforce correct behavior.",
"The moment you believe you have stopped learning is the moment you have stopped growing as a trader.",
"Read everything you can: classic books on trading, market history, investor biographies, and psychology.",
"Find a mentor or join a community of serious, like-minded traders to accelerate your learning curve.",
"Study the history of the markets. You'll find that the patterns of human behavior and emotion repeat endlessly.",
"Paper trade any new strategy until you have proven it is consistently profitable before risking a single dollar of real capital.",
"Your strategy must be dynamic. It needs to adapt and evolve as the market conditions change over time.",
"The best investment you will ever make is in your own knowledge and skills.",
"After every trading day, conduct a brief review. Ask 'What did I do well?' and 'What could I improve?'",
"Learn to identify your own psychological biases, such as confirmation bias, recency bias, and loss aversion.",
"The markets are a giant, ongoing puzzle. Enjoy the intellectual challenge of trying to solve it.",
"Never stop being a student of the markets.",
"Master one market and one strategy before trying to conquer the world.",
"The goal isn't just to be profitable; it's to become a competent, confident, and professional decision-maker under pressure."
    // ... and so on for all 500+ strings.
}.AsReadOnly();

        // 2. --- STATE FOR CHANNEL ---
        private ConcurrentQueue<string> _channelAdviceQueue;
        private readonly object _channelRefillLock = new(); // A lock only for the less-frequent refill operation.

        // --- STATE FOR INDIVIDUAL USERS ---
        // UPGRADE: Storing ConcurrentQueue directly for better thread-safety on a per-user basis.
        private readonly ConcurrentDictionary<long, ConcurrentQueue<string>> _userAdviceQueues = new();

        /// <summary>
        /// Initializes the AdviceService.
        /// </summary>
        public AdviceService()
        {
            _channelAdviceQueue = CreateNewShuffledConcurrentQueue();
        }

        // --- CHANNEL-SPECIFIC IMPLEMENTATION ---
        public string GetNextUniqueAdviceForChannel()
        {
            // UPGRADE: Using TryDequeue for a lock-free read attempt.
            if (!_channelAdviceQueue.TryDequeue(out var advice))
            {
                // If the queue is empty, we enter a lock to refill it.
                // This ensures only one thread refills the queue, preventing race conditions.
                lock (_channelRefillLock)
                {
                    // Double-check if another thread refilled it while we were waiting for the lock.
                    if (!_channelAdviceQueue.TryDequeue(out advice))
                    {
                        _channelAdviceQueue = CreateNewShuffledConcurrentQueue();
                        // Try one last time after refilling.
                        if (!_channelAdviceQueue.TryDequeue(out advice))
                        {
                            // This only happens if AllAdvices is empty.
                            return "Stay focused and disciplined in your trading endeavors.";
                        }
                    }
                }
            }
            return advice;
        }

        // --- USER-SPECIFIC IMPLEMENTATION ---
        public string GetUniqueAdviceForUser(long userId)
        {
            // Get or create the queue for the user. `GetOrAdd` is a thread-safe factory method.
            var userQueue = _userAdviceQueues.GetOrAdd(userId, (id) => CreateNewShuffledConcurrentQueue());

            // UPGRADE: Check and refill the queue in a thread-safe way.
            if (!userQueue.TryDequeue(out var advice))
            {
                // The user's queue is empty. We need to replace it.
                var newQueue = CreateNewShuffledConcurrentQueue();
                _userAdviceQueues.TryUpdate(userId, newQueue, userQueue);

                // If the update was successful, get the first item from the new queue.
                if (!newQueue.TryDequeue(out advice))
                {
                    // This is the failsafe for an empty master list.
                    return "Plan your trade and trade your plan.";
                }
            }

            return advice;
        }

        // --- PRIVATE HELPER METHODS ---

        /// <summary>
        /// UPGRADE: Creates a new, thread-safe ConcurrentQueue with all advice in random order.
        /// </summary>
        private static ConcurrentQueue<string> CreateNewShuffledConcurrentQueue()
        {
            if (!AllAdvices.Any())
            {
                return new ConcurrentQueue<string>();
            }

            // UPGRADE: Using a modern, in-place Fisher-Yates shuffle for better memory efficiency.
            var shuffledArray = AllAdvices.ToArray();
            Shuffle(shuffledArray);

            return new ConcurrentQueue<string>(shuffledArray);
        }

        /// <summary>
        /// UPGRADE: Implements the Fisher-Yates shuffle algorithm for in-place, unbiased randomization.
        /// </summary>
        /// <typeparam name="T">The type of elements in the array.</typeparam>
        /// <param name="array">The array to shuffle.</param>
        private static void Shuffle<T>(T[] array)
        {
            int n = array.Length;
            // UPGRADE: Using Random.Shared for a thread-safe, shared Random instance. (.NET 6+)
            // If using an older .NET version, fall back to a static Random instance.
            var random = Random.Shared;

            for (int i = n - 1; i > 0; i--)
            {
                // Pick a random index from the remaining elements.
                int j = random.Next(i + 1);
                // Swap array[i] with the element at the random index.
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
}
}