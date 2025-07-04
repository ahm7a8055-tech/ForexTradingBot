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
"The goal isn't just to be profitable; it's to become a competent, confident, and professional decision-maker under pressure.",
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

        // --- Expanded Mindset & Psychology ---
        "Cultivate unwavering discipline; it's the cornerstone of sustained trading profitability.",
        "True patience in trading is the ability to wait for your A+ setup, even if it takes days.",
        "The markets don't care about your opinions, only your execution of a proven strategy.",
        "Shift your perspective: trading is about managing probabilities, not chasing guarantees.",
        "Emotional neutrality allows for clear, unbiased decision-making, crucial for trading.",
        "Conquer your inner biases and fears; they are the true barriers to trading mastery.",
        "Ego-driven decisions are costly. Learn to admit when you are wrong and adapt.",
        "Maintain humility in profit and unwavering resolve in drawdown.",
        "Never trade out of anger or frustration; these impulses guarantee further losses.",
        "Anxiety about a trade signals an imbalance; reduce your exposure until comfort returns.",
        "Trading success is built brick by brick, over years, not in a single day or week.",
        "Confidence stems from thorough preparation and consistent adherence to your rules.",
        "Beware the euphoria of a winning streak; it often precedes overconfidence and recklessness.",
        "Sometimes, the most profitable decision is to do nothing at all.",
        "Capital flows from the impulsive to the patient; embrace the waiting game.",
        "Focus on asymmetrical risk-reward, where small losses are offset by much larger wins.",
        "Execution perfection of your plan is the direct path to consistent profitability.",
        "React to what the market is doing, not what you wish it would do or think it should do.",
        "Filter out the noise; external opinions can derail your carefully crafted strategy.",
        "Understand that random outcomes are part of short-term market dynamics; embrace them.",
        "Markets can be illogical. Your capital must outlast irrationality.",
        "Your role is to respond intelligently to market signals, not to play oracle.",
        "If your mental state is compromised, pressing the trade button is a high-risk gamble.",
        "Consistency in your actions, not just your results, builds true trading prowess.",
        "Uncertainty is the market's constant state. Develop mental resilience within it.",
        "Let the market confirm your thesis before committing capital; patience is golden.",
        "The regret of a missed rule-based trade is a far better teacher than a forced loss.",
        "Action bias is dangerous. A simple, executed plan beats a complex, procrastinated one.",
        "Reward the process, not just the profit. Discipline is the habit that creates wealth.",
        "Quality over quantity: focus on a few high-conviction setups rather than constant trading.",
        "Your control extends only to your strategy, risk, and emotional response.",
        "Chart your own course; comparing yourself to others is a thief of joy and focus.",
        "Visualize both success and failure within your plan to desensitize to outcomes.",
        "Each trade is an independent event; past results do not dictate future ones.",
        "Mastering your inner game is the ultimate competitive advantage in trading.",
        "A single trade's outcome is irrelevant to your worth as a trader; focus on the long game.",
        "Treat every position as a neutral vehicle for potential profit or loss, nothing more.",
        "Think systematically; react emotionally, and your account will suffer.",
        "Complex strategies often mask a lack of understanding. Seek elegant simplicity.",
        "The need for absolute certainty paralyses action and leads to missed opportunities.",
        "Trading is not a game of perfect predictions, but of disciplined responses.",
        "Develop emotional intelligence to navigate market swings without derailing your plan.",
        "A strong routine anchors your mind, reducing susceptibility to impulsive decisions.",
        "Self-awareness is critical: know your triggers, biases, and emotional states.",
        "Mindfulness practices can sharpen your focus and reduce reactivity during trading hours.",
        "Journaling your thoughts and feelings is as important as journaling your trades.",
        "Understand 'cognitive errors' like confirmation bias and anchoring effect to mitigate them.",
        "Fear of missing out (FOMO) is a common trap; resist the urge to chase runaway moves.",
        "Greed can be as destructive as fear; learn to take profits when your plan dictates.",
        "Practice detachment from money; view it as points in a game, not personal value.",
        "Build psychological resilience: learn to bounce back from losses without dwelling.",
        "Entering the 'flow state' in trading means being fully absorbed and performing optimally.",
        "Identify unconscious biases that may be sabotaging your trading performance.",
        "Stress management is vital; consider meditation, breaks, and physical activity.",
        "Your mindset determines your altitude in the trading world.",
        "A positive trading mindset views challenges as opportunities for growth.",
        "Cultivate gratitude for the learning opportunities, even from losing trades.",
        "Affirmations can reprogram your subconscious for discipline and success.",
        "Visualize successful trade execution, not just the profitable outcome.",
        "Energy management extends to your mental and emotional reserves.",
        "Prioritize sleep hygiene for optimal cognitive function and decision-making.",
        "Proper nutrition fuels your brain; what you eat impacts your trading performance.",
        "Regular exercise is a powerful stress reducer and mood enhancer for traders.",
        "Practice deep breathing exercises to calm your nervous system before critical decisions.",
        "Prevent burnout by scheduling regular breaks and disconnecting from the screens.",
        "Achieve work-life balance to maintain mental freshness for trading.",
        "Develop discipline through small, consistent habits, inside and outside trading.",
        "Patience is built by resisting the urge to overtrade or force setups.",
        "Improve focus by minimizing distractions in your trading environment.",
        "Enhance concentration through dedicated, uninterrupted trading blocks.",
        "Boost memory for past market behavior and pattern recognition.",
        "Sharpen problem-solving skills to adapt to evolving market conditions.",
        "Cultivate critical thinking to question assumptions and market narratives.",
        "Develop intuition through extensive screen time and post-trade analysis.",
        "Embrace creativity in identifying unique trading opportunities.",
        "Adaptability is paramount; fixed strategies fail in dynamic markets.",
        "Resilience allows you to absorb drawdowns and continue executing your plan.",
        "Self-awareness helps you recognize when you're compromised and need to step away.",
        "Emotional regulation is about acknowledging emotions, not suppressing them.",
        "Practice self-compassion after losses; harsh self-criticism hinders learning.",
        "Trading mindfully means being present with the charts, free from internal chatter.",
        "Seek the 'optimal arousal zone' – alert but not anxious, focused but not stressed.",
        "Manage pressure by focusing on process over outcome, trade by trade.",
        "Build confidence through consistent, disciplined execution, even with small wins.",
        "Confront FOMO by reminding yourself that there will always be another opportunity.",
        "Process regret constructively; extract lessons without succumbing to despair.",
        "Combat overtrading by adhering strictly to your defined daily limits.",
        "Avoid impulsive trading by always waiting for your entry criteria to be met.",
        "Let go of perfectionism; focus on consistent improvement, not flawless execution.",
        "Overcome analysis paralysis by trusting your plan and executing decisively.",
        "Identify and break self-sabotaging patterns through journal analysis.",
        "Consistency in process leads to consistency in results.",
        "Establish a winning daily routine that primes you for peak performance.",
        "Review profitable trades to reinforce good habits and successful patterns.",
        "Review losing trades dispassionately to identify weaknesses and learn lessons.",
        "Clearly define your trading edge; without it, you're merely gambling.",
        "Constantly refine your strategy based on market feedback and your journal.",
        "Your strategy must evolve as market conditions shift from trending to consolidating.",
        "Algorithmic trading demands a different psychological approach: trust the code.",
        "AI in forex trading requires a mindset shift towards data-driven insights, not human intuition.",
        "The potential of blockchain in forex: understand the transparent, decentralized future.",
        "Quantum computing may revolutionize market efficiency; stay open to new paradigms.",
        "Integrate sustainable trading practices: long-term, responsible growth.",
        "Ethical trading means avoiding manipulative practices and respecting market integrity.",
        "Consider socially responsible investing (SRI) principles in your long-term forex exposure.",
        "Explore impact investing in forex through currency pairs influenced by sustainable development.",
        "Gamification can make learning trading psychology more engaging.",
        "Virtual reality simulations offer a safe space to practice emotional control under pressure.",
        "Augmented reality tools could provide real-time psychological cues; explore their impact.",
        "Neurofeedback training may enhance brain states conducive to optimal trading.",
        "Predictive analytics, while powerful, still require psychological discipline to follow.",
        "Big data reveals market behavior patterns; learn to interpret them without bias.",
        "Machine learning can identify trade opportunities, but human oversight remains crucial.",
        "Deep learning models can spot complex patterns, but don't blindly trust the black box.",
        "Sentiment analysis tools can gauge market mood; use them to inform, not dictate, trades.",
        "Natural Language Processing helps interpret news sentiment, but always verify context.",
        "Recognize cognitive biases embedded even in automated trading systems.",
        "Human-computer interaction in trading requires a balance of trust and critical evaluation.",
        "Cybersecurity is not just technical; it's psychological peace of mind.",
        "Regulatory changes impact market structure and your trading psychology; stay informed.",
        "Global economic shifts demand flexibility in your trading approach and mental models.",
        "The future of forex trading psychology: ever-increasing demands for mental resilience.",
        "Embrace the evolution of your trading mindset; it's an ongoing journey.",
        "Seek innovative trading education that adapts to modern market dynamics.",
        "Personalized learning paths accelerate your growth as a trader.",
        "Adaptive assessment helps identify your strengths and weaknesses as a trader.",
        "Gamified learning makes complex trading concepts more accessible and engaging.",
        "Virtual trading coaches can provide objective feedback on your psychological state.",
        "AI-powered insights into your trading psychology can be transformative.",
        "Understand blockchain's potential for greater transparency in forex markets.",
        "Decentralized finance (DeFi) trading introduces new psychological challenges and opportunities.",
        "Quantum computing may impact market efficiency, demanding constant adaptation.",
        "Develop sustainable finance mental models for long-term trading success.",
        "Insist on ethical AI in trading to ensure fairness and prevent manipulation.",
        "Commit to responsible trading practices that benefit all market participants.",
        "Support inclusive trading education, broadening access to financial literacy.",
        "Recognize neurodiversity in trading; leverage unique cognitive strengths.",
        "Connect your mind and body; physical well-being directly impacts trading performance.",
        "Adopt a holistic approach to trading that includes mental, physical, and financial health.",
        "Integrative trading psychology combines various techniques for complete development.",
        "Prioritize well-being for traders; it's not a luxury, but a necessity.",
        "Build resilient trading systems that can withstand market shocks.",
        "Future-proof your trading skills by embracing continuous learning.",
        "Commit to lifelong learning; the markets are an infinite source of knowledge.",
        "Strive for mastery in trading: a continuous pursuit of excellence.",
        "Appreciate the 'craft of trading' – the skill and artistry involved.",
        "Embrace the 'art of trading' – the intuitive and creative aspects.",
        "Understand the 'science of trading' – the data, statistics, and probabilities.",
        "Explore the 'philosophy of trading' – your core beliefs and values.",
        "Connect with the 'spirit of trading' – your passion and purpose.",
        "Seek wisdom in trading, beyond mere knowledge or tactics.",
        "Embrace the journey of a trader: full of highs, lows, and profound lessons.",
        "Recognize the evolution of a trader – from novice to expert.",
        "Cultivate a trader identity that is resilient, disciplined, and adaptable.",
        "Define your purpose in trading beyond just making money.",
        "Find meaning in the challenges and triumphs of your trading career.",
        "Consider your legacy in trading; how will you contribute?",
        "Explore contributing to positive societal impact through your trading.",
        "Trade for good: align your financial goals with ethical considerations.",
        "Practice conscious trading, aware of your impact and intentions.",
        "Seek enlightened trading – a path to personal and financial freedom.",
        "View trading as a transformative process for personal growth.",
        "Embrace trading as a form of art, expressed through your unique strategy.",
        "Allow trading to be a creative endeavor, finding new solutions.",
        "Consider trading as a service, contributing to market efficiency.",
        "Align trading with philanthropy; use profits to make a difference.",
        "Engage in trading for social impact; support companies or assets with positive goals.",
        "Invest for environmental impact through green and sustainable instruments.",
        "Integrate ethical investing principles into your forex decisions.",
        "Support sustainable development goals through your trading choices.",
        "Trade for a better world, one responsible decision at a time.",
        "Operate with integrity in all your trading interactions.",
        "Be authentic in your trading approach; true to your personality and risk tolerance.",
        "Cultivate compassion, for yourself and for other market participants.",
        "Trade with wisdom, applying learned lessons and deeper insights.",
        "Embody courage to take calculated risks and stand by your convictions.",
        "Practice discipline in every aspect of your trading life.",
        "Develop patience that transcends market fluctuations and waiting periods.",
        "Maintain unwavering focus on your process, not just the outcome.",
        "Sharpen concentration for sustained periods of market analysis.",
        "Reinforce determination to overcome obstacles and pursue your goals.",
        "Cultivate persistence; success often comes just after the point of giving up.",
        "Embody perseverance, especially during challenging market phases.",
        "Trade with passion; let genuine interest drive your continuous learning.",
        "Identify your core purpose in trading; it provides motivation and direction.",
        "Find meaning in the process, not just the profit.",
        "Experience fulfillment from executing your plan well, regardless of outcome.",
        "Derive satisfaction from mastering your craft.",
        "Cultivate happiness in the journey of becoming a better trader.",
        "Embrace joy in the intellectual challenge and dynamic nature of the markets.",
        "Seek inner peace amidst market volatility.",
        "Trade with a sense of love for the markets and the opportunities they present.",
        "Achieve financial freedom through disciplined trading.",
        "Leverage trading for lifestyle design and personal independence.",
        "Utilize trading as a catalyst for personal growth and self-discovery.",
        "Engage in trading as a path to self-mastery.",
        "Consider trading as a spiritual practice, connecting with universal principles.",
        "See trading as a path to enlightenment, understanding cause and effect.",
        "View trading as a form of art, expressed through elegant strategies.",
        "Embrace trading as a creative endeavor, finding innovative solutions.",
        "Offer trading as a service, contributing to market efficiency and liquidity.",
        "Use trading profits for philanthropic endeavors.",
        "Contribute to social impact through your investment choices.",
        "Support environmental causes via your trading portfolio.",
        "Integrate ethical investing principles into your forex decisions.",
        "Align your trading with sustainable development goals.",
        "Trade with the intention of making the world a better place.",
        "Always trade with integrity, honesty, and transparency.",
        "Be authentic to your trading style and personality.",
        "Practice compassion towards yourself and others in the market.",
        "Apply wisdom from past experiences to current decisions.",
        "Embody courage when facing uncertainty and taking calculated risks.",
        "Demonstrate discipline consistently in all your trading actions.",
        "Cultivate patience for optimal entry and exit points.",
        "Maintain unwavering focus on your trading plan.",
        "Enhance concentration during market analysis and execution.",
        "Strengthen determination to push through challenging periods.",
        "Develop persistence to stay in the game for the long run.",
        "Embody perseverance, seeing setbacks as opportunities to refine.",
        "Trade with passion, letting your enthusiasm fuel your learning.",
        "Trade with purpose, aligning your actions with your deepest values.",
        "Find profound meaning in your trading journey.",
        "Experience fulfillment through successful execution and continuous growth.",
        "Achieve deep satisfaction from mastering the complexities of the market.",
        "Cultivate a sense of happiness in the daily practice of trading.",
        "Embrace joy in every learning experience, whether win or loss.",
        "Attain inner peace by accepting market outcomes with equanimity.",
        "Approach trading with a sense of love for the challenge.",
        "Seek freedom in the flexibility and potential of trading.",
        "Embrace independence as you chart your own financial course.",
        "Feel empowered by your ability to navigate the markets.",
        "Cultivate self-reliance through continuous skill development.",
        "Build robust self-confidence through consistent discipline.",
        "Develop strong self-esteem based on your process, not just profit.",
        "Recognize your inherent self-worth beyond your trading account balance.",
        "Practice self-love, caring for your mental and physical well-being.",
        "Cultivate inner peace by managing your reactions to market events.",
        "Tap into your inner strength to persevere through difficult times.",
        "Access your inner wisdom for intuitive insights.",
        "Trust your inner guidance when making difficult decisions.",
        "Connect with your higher self for clarity and perspective.",
        "Harmonize with universal consciousness for deeper market understanding.",
        "Align with cosmic intelligence for enhanced pattern recognition.",
        "Seek divine guidance to navigate unpredictable market conditions.",
        "Apply spiritual principles to your trading, such as detachment and generosity.",
        "Understand universal laws that govern market behavior.",
        "Embrace the law of attraction: focus on desired outcomes, not fears.",
        "Utilize the law of vibration: align your energy with success.",
        "Understand the law of cause and effect: every action has a consequence.",
        "Recognize the law of rhythm: markets ebb and flow; adapt to cycles.",
        "Apply the law of polarity: highs and lows are part of the market.",
        "Understand the law of gender: creation and growth are cyclical processes.",
        "Embrace the law of oneness: all markets are interconnected.",
        "Attract abundance through a mindset of wealth and possibility.",
        "Cultivate prosperity consciousness in your trading endeavors.",
        "Build wealth strategically and systematically.",
        "Manifest success through consistent action and belief.",
        "Develop a winning mindset that embraces both challenge and victory.",
        "Maintain a positive outlook, even during drawdowns.",
        "Commit to continuous growth as a trader and individual.",
        "Foster resilience to bounce back stronger from setbacks.",
        "Cultivate adaptability to thrive in ever-changing market conditions.",
        "Embrace flexibility in your trading strategies and execution.",
        "Unleash creativity in identifying unique market opportunities.",
        "Trust your intuition, developed through experience and self-awareness.",
        "Seek wisdom that transcends mere knowledge.",
        "Gain insight from deep market analysis and self-reflection.",
        "Achieve clarity through simplifying your approach.",
        "Maintain laser focus on your trading objectives.",
        "Enhance concentration for prolonged periods of market engagement.",
        "Demonstrate unwavering determination to achieve your goals.",
        "Cultivate persistence through consistent effort and learning.",
        "Embody perseverance, knowing that consistency leads to success.",
        "Ignite your passion for trading; it fuels your journey.",
        "Define your clear purpose in the markets.",
        "Find profound meaning in every trading experience.",
        "Experience deep fulfillment from your trading mastery.",
        "Derive satisfaction from a well-executed plan.",
        "Cultivate happiness through mindful trading.",
        "Embrace joy in the intellectual challenge.",
        "Attain peace through acceptance of outcomes.",
        "Trade with a sense of love for the craft.",
        "Achieve freedom through financial independence.",
        "Embrace independence in your trading decisions.",
        "Feel empowered by your growing skills.",
        "Build self-reliance through continuous learning.",
        "Strengthen self-confidence with every disciplined trade.",
        "Boost self-esteem through consistent process adherence.",
        "Recognize your self-worth beyond profit and loss.",
        "Practice self-love by prioritizing your well-being.",
        "Cultivate inner peace through emotional regulation.",
        "Tap into inner strength to navigate market stress.",
        "Access inner wisdom for clarity in complex situations.",
        "Trust your inner guidance for optimal decisions.",
        "Connect with your higher self for perspective.",
        "Align with universal consciousness for market insights.",
        "Receive cosmic intelligence for pattern recognition.",
        "Open to divine guidance for market direction.",
        "Apply spiritual principles in your trading practice.",
        "Understand and utilize universal laws for success."
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