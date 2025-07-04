-- Script Name: Populate_RssSources_Categories.sql
-- Description: Populates the SignalCategories and RssSources tables with initial data for the ForexSignalBot.
-- Target Database: PostgreSQL

-- Enable UUID generation if not already available (usually included by default)
-- CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- =============================================================================
-- PHASE 1: Populate SignalCategories Table
-- These categories help classify the RSS feeds and the news they contain.
-- =============================================================================

-- Clear existing categories if necessary (use with caution in production!)
-- DELETE FROM public."SignalCategories";

INSERT INTO public."SignalCategories" ("Id", "Name", "Description", "IsActive", "SortOrder") VALUES
(gen_random_uuid(), 'Forex & Economic News', 'Major currency news, central bank policy, economic indicators, and global market trends affecting Forex.', true, 10),
(gen_random_uuid(), 'Cryptocurrency & Blockchain', 'News, analysis, and developments in the digital asset and blockchain space.', true, 20),
(gen_random_uuid(), 'Stock Market & Equities', 'Information on stock markets, company earnings, equity analysis, and index movements.', true, 30),
(gen_random_uuid(), 'Commodities & Energy', 'Updates on commodity prices, energy markets (oil, gas), and related economic factors.', true, 40),
(gen_random_uuid(), 'Technical Analysis & Strategy', 'Articles and insights on charting patterns, indicators, and trading strategies.', true, 50),
(gen_random_uuid(), 'Geopolitics & Global Events', 'News and analysis on geopolitical developments impacting financial markets.', true, 60),
(gen_random_uuid(), 'Bonds & Fixed Income', 'Information related to government and corporate bonds, yield curves, and interest rate policies.', true, 70),
(gen_random_uuid(), 'Trading Psychology & Mindset', 'Articles focused on the mental aspects of trading, discipline, risk management, and cognitive biases.', true, 80),
(gen_random_uuid(), 'Market Sentiment & Macro Trends', 'Analysis of overall market sentiment, large-scale economic trends, and macro-level influences.', true, 90),
(gen_random_uuid(), 'Regulatory & Policy Updates', 'News on financial regulations, government policies, and legal changes affecting markets.', true, 100);

-- =============================================================================
-- PHASE 2: Populate RssSources Table
-- These are the actual RSS feed URLs that the bot will periodically fetch.
-- IMPORTANT: Ensure the `DefaultSignalCategoryId` UUIDs below match the ones generated above!
-- It's best to run this script in a SQL client where you can easily copy the generated UUIDs.
-- If running programmatically, you'd fetch the category IDs first.
-- For this script, we'll use placeholders and assume you'll update them if the generated IDs differ.
-- You can manually copy the IDs from your database after running Phase 1 to replace these placeholders.
-- =============================================================================

-- Placeholder UUIDs (REPLACE WITH ACTUAL UUIDS FROM PHASE 1 OUTPUT)
-- You can find them by running: SELECT "Id", "Name" FROM public."SignalCategories";
-- Example:
-- Forex News Category UUID: '...'
-- Crypto News Category UUID: '...'
-- Stock Market UUID: '...'
-- Commodities UUID: '...'
-- TA Strategy UUID: '...'
-- Geopolitics UUID: '...'
-- Bonds UUID: '...'
-- Psychology UUID: '...'
-- Macro Trends UUID: '...'
-- Regulatory UUID: '...'

-- --- Populate RssSources Table ---
-- Using a common pattern: CREATE TEMPORARY TABLE for easier UUID handling and then INSERT.

-- Temporary table to hold the category IDs for easier referencing.
-- Replace 'YOUR_FOREX_CAT_ID', 'YOUR_CRYPTO_CAT_ID', etc., with the actual UUIDs generated in Phase 1.
-- You can get them by running `SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Forex & Economic News';` etc.
-- If you run this script as a whole, the GUIDs *should* be consistent if run in one transaction.
DO $$
DECLARE
    FOREX_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Forex & Economic News');
    CRYPTO_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Cryptocurrency & Blockchain');
    STOCKS_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Stock Market & Equities');
    COMMODITIES_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Commodities & Energy');
    TA_STRAT_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Technical Analysis & Strategy');
    GEOPOLITICS_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Geopolitics & Global Events');
    BONDS_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Bonds & Fixed Income');
    PSYCH_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Trading Psychology & Mindset');
    MACRO_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Market Sentiment & Macro Trends');
    REGULATORY_CAT_ID uuid := (SELECT "Id" FROM public."SignalCategories" WHERE "Name" = 'Regulatory & Policy Updates');
BEGIN
    -- Ensure categories exist, otherwise the FK constraint will fail.
    IF FOREX_CAT_ID IS NULL OR CRYPTO_CAT_ID IS NULL OR STOCKS_CAT_ID IS NULL OR COMMODITIES_CAT_ID IS NULL OR TA_STRAT_CAT_ID IS NULL OR GEOPOLITICS_CAT_ID IS NULL OR BONDS_CAT_ID IS NULL OR PSYCH_CAT_ID IS NULL OR MACRO_CAT_ID IS NULL OR REGULATORY_CAT_ID IS NULL THEN
        RAISE EXCEPTION 'One or more SignalCategories could not be found. Please ensure Phase 1 has been executed successfully.';
    END IF;

    -- --- CORE FOREX & ECONOMIC NEWS (Category: Forex & Economic News) ---
    INSERT INTO public."RssSources" ("Id", "Url", "SourceName", "IsActive", "DefaultSignalCategoryId", "FetchIntervalMinutes", "Description") VALUES
    (gen_random_uuid(), 'https://www.dailyfx.com/feeds/market-news', 'DailyFX Market News', true, FOREX_CAT_ID, 15, 'Forex market news and analysis from DailyFX.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/analysis/forex', 'FXStreet Analysis', true, FOREX_CAT_ID, 20, 'Forex analysis and news from FXStreet.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news.rss', 'Investing.com Forex News', true, FOREX_CAT_ID, 25, 'Forex news from Investing.com.'),
    (gen_random_uuid(), 'https://feeds.reuters.com/forex', 'Reuters Forex News', true, FOREX_CAT_ID, 30, 'Forex news updates from Reuters.'),
    (gen_random_uuid(), 'https://www.bloomberg.com/markets/feed', 'Bloomberg Markets Feed', true, FOREX_CAT_ID, 30, 'Top market news from Bloomberg.'),
    (gen_random_uuid(), 'https://www.kitco.com/gold-real-time-news/content/kitco-news.rss', 'Kitco Gold News', true, COMMODITIES_CAT_ID, 45, 'Latest gold and precious metals news.'),
    (gen_random_uuid(), 'https://www.cnbc.com/id/10072738/device/rss/rss.html', 'CNBC World News', true, GEOPOLITICS_CAT_ID, 60, 'Global news impacting markets from CNBC.'),
    (gen_random_uuid(), 'https://www.federalreserve.gov/feeds/press_all.xml', 'Federal Reserve Press Releases', true, MACRO_CAT_ID, 120, 'Official press releases from the US Federal Reserve.'),
    (gen_random_uuid(), 'https://www.ecb.europa.eu/press/pr/feed/html/index.en.rss', 'ECB Press Releases', true, MACRO_CAT_ID, 120, 'Press releases from the European Central Bank.'),
    (gen_random_uuid(), 'https://www.bankofengland.co.uk/feed', 'Bank of England News', true, MACRO_CAT_ID, 120, 'News and statements from the Bank of England.'),
    (gen_random_uuid(), 'https://www.boj.or.jp/en/rss/news_press.xml', 'Bank of Japan News', true, MACRO_CAT_ID, 120, 'Press releases from the Bank of Japan.'),
    (gen_random_uuid(), 'https://www.fxempire.com/news/forex/feed', 'Forex Empire News', true, FOREX_CAT_ID, 20, 'Forex news and analysis from Forex Empire.'),
    (gen_random_uuid(), 'https://www.forexcrunch.com/feed/', 'ForexCrunch Analysis', true, FOREX_CAT_ID, 25, 'Forex market analysis and news.'),
    (gen_random_uuid(), 'https://www.fxnewsgroup.com/feed/', 'Forex News Group', true, FOREX_CAT_ID, 15, 'Daily Forex news and analysis.'),
    (gen_random_uuid(), 'https://www.babypips.com/learn/forex/rss.xml', 'BabyPips Learn Forex', true, FOREX_CAT_ID, 30, 'Educational content and market commentary from BabyPips.com.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/', 'ForexLive Analysis', true, FOREX_CAT_ID, 20, 'Real-time Forex news and market commentary.'),
    (gen_random_uuid(), 'https://tradingeconomics.com/rss/news.axd?source=forex', 'Trading Economics Forex', true, FOREX_CAT_ID, 30, 'Economic calendar and forex news from Trading Economics.'),
    (gen_random_uuid(), 'https://tradingeconomics.com/rss/news.axd?source=united-states', 'Trading Economics US News', true, MACRO_CAT_ID, 30, 'US economic news from Trading Economics.'),
    (gen_random_uuid(), 'https://tradingeconomics.com/rss/news.axd?source=euro-area', 'Trading Economics EU News', true, MACRO_CAT_ID, 30, 'Euro Area economic news.'),
    (gen_random_uuid(), 'https://www.forexpros.com/rss/news.rss', 'ForexPros News', true, FOREX_CAT_ID, 20, 'General Forex news from ForexPros.com.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/commodities/news', 'FXStreet Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodities news and analysis.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/gold-silver/news', 'FXStreet Metals News', true, COMMODITIES_CAT_ID, 45, 'Precious metals news and analysis.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/oil-energy/news', 'FXStreet Energy News', true, COMMODITIES_CAT_ID, 45, 'Oil and energy market news.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/geopolitics/news', 'FXStreet Geopolitics', true, GEOPOLITICS_CAT_ID, 60, 'Geopolitical events impacting markets.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/economic-calendar/rss', 'FXStreet Economic Calendar', true, MACRO_CAT_ID, 60, 'Economic calendar events and impacts.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/us-elections/news', 'FXStreet US Elections', true, GEOPOLITICS_CAT_ID, 75, 'US election news and market impact.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/brexit/news', 'FXStreet Brexit News', true, GEOPOLITICS_CAT_ID, 75, 'Brexit related market news.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/crypto/news', 'FXStreet Crypto News', true, CRYPTO_CAT_ID, 20, 'Latest cryptocurrency news and updates.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/commodities/natural-gas/news', 'FXStreet Natural Gas News', true, COMMODITIES_CAT_ID, 45, 'Natural gas market news.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/bonds/news', 'FXStreet Bonds News', true, BONDS_CAT_ID, 40, 'Bond market news and analysis.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/stocks/equities/news', 'FXStreet Stocks News', true, STOCKS_CAT_ID, 25, 'Stock market news and equity analysis.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/markets/emerging-markets/news', 'FXStreet Emerging Markets', true, MACRO_CAT_ID, 40, 'News on emerging market economies.'),
    (gen_random_uuid(), 'https://www.fxstreet.com/commodities/oil-prices/news', 'FXStreet Oil Prices', true, COMMODITIES_CAT_ID, 45, 'Updates on oil prices and the energy market.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/forex-news/', 'ForexLive Forex News', true, FOREX_CAT_ID, 20, 'Forex news from ForexLive.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/technical-analysis/', 'ForexLive Technical Analysis', true, TA_STRAT_CAT_ID, 30, 'Technical analysis insights.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/fundamental-analysis/', 'ForexLive Fundamental Analysis', true, MACRO_CAT_ID, 30, 'Fundamental analysis impacting Forex.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/central-banks/', 'ForexLive Central Banks', true, MACRO_CAT_ID, 40, 'Central bank policy news.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/commodities/', 'ForexLive Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodities market news.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/cryptocurrencies/', 'ForexLive Crypto', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency market updates.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/geopolitics/', 'ForexLive Geopolitics', true, GEOPOLITICS_CAT_ID, 60, 'Geopolitical events affecting markets.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/stocks/', 'ForexLive Stocks', true, STOCKS_CAT_ID, 25, 'Stock market news.'),
    (gen_random_uuid(), 'https://www.forexlive.com/feed/bonds/', 'ForexLive Bonds', true, BONDS_CAT_ID, 40, 'Bond market news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news.rss', 'Investing.com General News', true, MACRO_CAT_ID, 30, 'General financial news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/analysis.rss', 'Investing.com Analysis', true, TA_STRAT_CAT_ID, 30, 'Market analysis articles.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/commodities.rss', 'Investing.com Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodity market news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/cryptocurrencies.rss', 'Investing.com Crypto', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency market news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/stocks.rss', 'Investing.com Stocks', true, STOCKS_CAT_ID, 25, 'Stock market news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/tech.rss', 'Investing.com Tech News', true, STOCKS_CAT_ID, 25, 'Technology sector news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/politics.rss', 'Investing.com Politics', true, GEOPOLITICS_CAT_ID, 60, 'Political news impacting markets.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/bonds.rss', 'Investing.com Bonds', true, BONDS_CAT_ID, 40, 'Bond market news.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/analysis/technical.rss', 'Investing.com Technical Analysis', true, TA_STRAT_CAT_ID, 30, 'Technical analysis reports.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/analysis/fundamental.rss', 'Investing.com Fundamental Analysis', true, MACRO_CAT_ID, 30, 'Fundamental analysis reports.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/top-stories.rss', 'Investing.com Top Stories', true, MACRO_CAT_ID, 15, 'Top financial news stories.'),
    (gen_random_uuid(), 'https://www.investing.com/rss/news/forex.rss', 'Investing.com Forex', true, FOREX_CAT_ID, 20, 'Forex news from Investing.com.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/world', 'Reuters World Markets', true, GEOPOLITICS_CAT_ID, 60, 'Global market events from Reuters.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/commodities', 'Reuters Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodity market news.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/stocks', 'Reuters Stocks', true, STOCKS_CAT_ID, 25, 'Stock market news and analysis.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/forex', 'Reuters Forex', true, FOREX_CAT_ID, 20, 'Forex market news and currency updates.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/technology', 'Reuters Technology Sector', true, STOCKS_CAT_ID, 25, 'News and analysis on the technology sector.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/breakingviews', 'Reuters Breakingviews', true, MACRO_CAT_ID, 30, 'Market opinion and analysis from Breakingviews.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/bonds', 'Reuters Bonds', true, BONDS_CAT_ID, 40, 'Bond market news and fixed income analysis.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/cryptocurrencies', 'Reuters Crypto', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency market news and trends.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/debt-markets', 'Reuters Debt Markets', true, BONDS_CAT_ID, 40, 'News on global debt markets.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/emea', 'Reuters EMEA Markets', true, MACRO_CAT_ID, 40, 'Market news from the Europe, Middle East, and Africa region.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/asia', 'Reuters Asia Markets', true, MACRO_CAT_ID, 40, 'Market news from the Asia-Pacific region.'),
    (gen_random_uuid(), 'https://www.reuters.com/markets/rss/americas', 'Reuters Americas Markets', true, MACRO_CAT_ID, 40, 'Market news from North and South America.'),
    (gen_random_uuid(), 'https://www.bloomberg.com/feeds/podcasts/the-big-take.rss', 'Bloomberg Big Take Podcast', true, MACRO_CAT_ID, 90, 'In-depth market analysis from Bloomberg.'),
    (gen_random_uuid(), 'https://feeds.simplecast.com/xUfS_49c', 'Bloomberg Crypto Minute', true, CRYPTO_CAT_ID, 15, 'Daily crypto market updates.'),
    (gen_random_uuid(), 'https://feeds.simplecast.com/HkI5j72b', 'Bloomberg Money Undercover', true, PSYCH_CAT_ID, 75, 'Trading psychology and investor behavior.'),
    (gengen_random_uuid(), 'https://feeds.simplecast.com/B5_21e1N', 'Bloomberg Masters in Business', true, MACRO_CAT_ID, 60, 'Interviews with business leaders and investors.'),
    (gen_random_uuid(), 'https://feeds.buzzsprout.com/1801282.rss', 'The Pomp Podcast', true, CRYPTO_CAT_ID, 30, 'Discussions on Bitcoin and crypto from Anthony Pompliano.'),
    (gengen_random_uuid(), 'https://api.adviceslip.com/advice', 'AdviceSlip.com API', true, PSYCH_CAT_ID, 60, 'Random motivational advice for traders (external API fallback).'), -- IMPORTANT: This is a fallback, not a standard RSS feed.

    // --- CRYPTOCURRENCY & BLOCKCHAIN NEWS (Category: Cryptocurrency & Blockchain) ---
    (gen_random_uuid(), 'https://cointelegraph.com/rss', 'Cointelegraph News', true, CRYPTO_CAT_ID, 20, 'Global crypto news and market analysis.'),
    (gen_random_uuid(), 'https://decrypt.co/feed/', 'Decrypt News', true, CRYPTO_CAT_ID, 20, 'Crypto news and articles from Decrypt.'),
    (gen_random_uuid(), 'https://www.coindesk.com/arcio/feeds/rss/news/', 'CoinDesk News', true, CRYPTO_CAT_ID, 20, 'Top crypto news and insights from CoinDesk.'),
    (gen_random_uuid(), 'https://www.theblockcrypto.com/feed/', 'The Block Crypto', true, CRYPTO_CAT_ID, 25, 'In-depth crypto research and news.'),
    (gen_random_uuid(), 'https://www.bitcoinnews.com/rss.xml', 'Bitcoin News', true, CRYPTO_CAT_ID, 15, 'Latest news about Bitcoin and the crypto market.'),
    (gen_random_uuid(), 'https://ethereumfoundation.org/en/feed/', 'Ethereum Foundation Blog', true, CRYPTO_CAT_ID, 45, 'Official updates from the Ethereum Foundation.'),
    (gen_random_uuid(), 'https://www.binance.com/en/feed/news/all', 'Binance News Feed', true, CRYPTO_CAT_ID, 30, 'Official news and announcements from Binance.'),
    (gen_random_uuid(), 'https://coinmarketcap.com/headlines/rss/', 'CoinMarketCap Headlines', true, CRYPTO_CAT_ID, 25, 'Crypto market data and news headlines.'),
    (gen_random_uuid(), 'https://satoshi.nakamoto.google.com/feed.xml', 'Satoshi Nakamoto Google Feed', true, CRYPTO_CAT_ID, 180, 'Archived Bitcoin discussions (historical).'),
    (gen_random_uuid(), 'https://blockworks.co/feed/', 'Blockworks News', true, CRYPTO_CAT_ID, 20, 'Crypto and blockchain news.'),
    (gen_random_uuid(), 'https://www.nasdaq.com/topics/bitcoin/rss.xml', 'Nasdaq Bitcoin News', true, CRYPTO_CAT_ID, 25, 'Bitcoin news from Nasdaq.'),
    (gen_random_uuid(), 'https://www.nasdaq.com/topics/ethereum/rss.xml', 'Nasdaq Ethereum News', true, CRYPTO_CAT_ID, 25, 'Ethereum news from Nasdaq.'),
    (gen_random_uuid(), 'https://www.nasdaq.com/topics/blockchain/rss.xml', 'Nasdaq Blockchain News', true, CRYPTO_CAT_ID, 25, 'Blockchain technology news.'),
    (gen_random_uuid(), 'https://cryptorank.io/feed/news', 'CryptoRank News', true, CRYPTO_CAT_ID, 20, 'Crypto market data and news.'),
    (gen_random_uuid(), 'https://thedefiant.io/feed/', 'The Defiant News', true, CRYPTO_CAT_ID, 20, 'DeFi news and analysis.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/', 'CryptoSlate News', true, CRYPTO_CAT_ID, 20, 'Comprehensive crypto news and market intelligence.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/bitcoin/feed/', 'CryptoSlate Bitcoin', true, CRYPTO_CAT_ID, 25, 'Bitcoin specific news.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/ethereum/feed/', 'CryptoSlate Ethereum', true, CRYPTO_CAT_ID, 25, 'Ethereum specific news.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/altcoins/feed/', 'CryptoSlate Altcoins', true, CRYPTO_CAT_ID, 25, 'Altcoin market news.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/defi/feed/', 'CryptoSlate DeFi', true, CRYPTO_CAT_ID, 20, 'Decentralized Finance news.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/nft/feed/', 'CryptoSlate NFTs', true, CRYPTO_CAT_ID, 20, 'Non-Fungible Token news.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/regulation/feed/', 'CryptoSlate Regulation', true, REGULATORY_CAT_ID, 30, 'Crypto regulatory news.'),
    (gen_random_uuid(), 'https://cryptoslate.com/feed/category/security/feed/', 'CryptoSlate Security', true, CRYPTO_CAT_ID, 30, 'Crypto security news and breaches.'),
    (gen_random_uuid(), 'https://www.blockchain.com/feed/', 'Blockchain.com News', true, CRYPTO_CAT_ID, 30, 'Updates from Blockchain.com.'),
    (gen_random_uuid(), 'https://www.ledgerinsights.com/feed/', 'Ledger Insights', true, CRYPTO_CAT_ID, 40, 'News on blockchain technology and applications.'),
    (gen_random_uuid(), 'https://medium.com/feed/@CoinBureau', 'Coin Bureau Medium', true, CRYPTO_CAT_ID, 30, 'Market analysis and crypto insights.'),
    (gen_random_uuid(), 'https://medium.com/feed/@Messari', 'Messari Medium', true, CRYPTO_CAT_ID, 45, 'Crypto research and data from Messari.'),
    (gen_random_uuid(), 'https://medium.com/feed/@bankless', 'Bankless Medium', true, CRYPTO_CAT_ID, 30, 'Focus on Ethereum and DeFi.'),

    // --- STOCK MARKET & EQUITIES NEWS (Category: Stock Market & Equities) ---
    (gen_random_uuid(), 'https://finance.yahoo.com/rss/topstories/', 'Yahoo Finance Top Stories', true, STOCKS_CAT_ID, 15, 'Top financial news from Yahoo Finance.'),
    (gen_random_uuid(), 'https://finance.yahoo.com/rss/sector/technology/', 'Yahoo Finance Tech Sector', true, STOCKS_CAT_ID, 25, 'News on the technology sector.'),
    (gen_random_uuid(), 'https://finance.yahoo.com/rss/sector/healthcare/', 'Yahoo Finance Healthcare Sector', true, STOCKS_CAT_ID, 25, 'News on the healthcare sector.'),
    (gen_random_uuid(), 'https://finance.yahoo.com/rss/sector/financials/', 'Yahoo Finance Financials Sector', true, STOCKS_CAT_ID, 25, 'News on the financial sector.'),
    (gen_random_uuid(), 'https://feeds.finance.yahoo.com/rss/2.0/finance_us.xml', 'Yahoo Finance US Markets', true, STOCKS_CAT_ID, 20, 'US stock market news.'),
    (gen_random_uuid(), 'https://www.wsj.com/public/page/archive/rss/markets_stocks.rss', 'WSJ Stocks', true, STOCKS_CAT_ID, 20, 'Stock market news from The Wall Street Journal.'),
    (gen_random_uuid(), 'https://www.wsj.com/public/page/archive/rss/markets_economy.rss', 'WSJ Economy', true, MACRO_CAT_ID, 30, 'Economic news from The Wall Street Journal.'),
    (gen_random_uuid(), 'https://www.wsj.com/public/page/archive/rss/tech_industry.rss', 'WSJ Tech Industry', true, STOCKS_CAT_ID, 25, 'Technology industry news from WSJ.'),
    (gen_random_uuid(), 'https://www.wsj.com/public/page/archive/rss/markets_commodities.rss', 'WSJ Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodity market news.'),
    (gen_random_uuid(), 'https://www.wsj.com/public/page/archive/rss/markets_bonds.rss', 'WSJ Bonds', true, BONDS_CAT_ID, 40, 'Bond market news.'),
    (gen_random_uuid(), 'https://www.seekingalpha.com/market_news/feed', 'Seeking Alpha Market News', true, STOCKS_CAT_ID, 15, 'Market news and analysis from Seeking Alpha.'),
    (gen_random_uuid(), 'https://seekingalpha.com/feed/author/444/feed', 'Seeking Alpha Earnings Analysis', true, STOCKS_CAT_ID, 30, 'Earnings reports and analysis.'),
    (gen_random_uuid(), 'https://seekingalpha.com/feed/sector/technology', 'Seeking Alpha Tech Sector', true, STOCKS_CAT_ID, 25, 'Technology stock news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/feed/market_outlook', 'Seeking Alpha Market Outlook', true, MACRO_CAT_ID, 30, 'Market outlook and commentary.'),
    (gen_random_uuid(), 'https://feeds.bloomberg.com/markets/technology', 'Bloomberg Technology News', true, STOCKS_CAT_ID, 25, 'Technology sector news from Bloomberg.'),
    (gen_random_uuid(), 'https://feeds.bloomberg.com/markets/economics', 'Bloomberg Economics News', true, MACRO_CAT_ID, 30, 'Economic news and analysis.'),
    (gen_random_uuid(), 'https://feeds.bloomberg.com/markets/cryptocurrencies', 'Bloomberg Crypto News', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency market updates.'),
    (gen_random_uuid(), 'https://feeds.bloomberg.com/markets/commodities', 'Bloomberg Commodities News', true, COMMODITIES_CAT_ID, 45, 'Commodities market news.'),
    (gen_random_uuid(), 'https://feeds.bloomberg.com/markets/bonds', 'Bloomberg Bonds News', true, BONDS_CAT_ID, 40, 'Bond market news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/latestnews', 'MarketWatch Latest News', true, MACRO_CAT_ID, 15, 'Latest financial news from MarketWatch.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/industries/technology', 'MarketWatch Tech Industry', true, STOCKS_CAT_ID, 25, 'Technology industry news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/cryptocurrency', 'MarketWatch Crypto', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency market news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/commodities', 'MarketWatch Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodities market news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/personalfinance', 'MarketWatch Personal Finance', true, PSYCH_CAT_ID, 75, 'Personal finance and investor psychology.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/forex', 'MarketWatch Forex', true, FOREX_CAT_ID, 20, 'Forex market news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/bonds', 'MarketWatch Bonds', true, BONDS_CAT_ID, 40, 'Bond market news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/europe', 'MarketWatch Europe Markets', true, MACRO_CAT_ID, 40, 'European market news.'),
    (gen_random_uuid(), 'https://www.marketwatch.com/rss/markets/asia', 'MarketWatch Asia Markets', true, MACRO_CAT_ID, 40, 'Asian market news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/symbol/AAPL/news', 'Seeking Alpha AAPL News', true, STOCKS_CAT_ID, 25, 'Apple Inc. stock news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/symbol/MSFT/news', 'Seeking Alpha MSFT News', true, STOCKS_CAT_ID, 25, 'Microsoft Corp. stock news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/symbol/NVDA/news', 'Seeking Alpha NVDA News', true, STOCKS_CAT_ID, 25, 'Nvidia Corp. stock news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/symbol/TSLA/news', 'Seeking Alpha TSLA News', true, STOCKS_CAT_ID, 25, 'Tesla Inc. stock news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/symbol/GOOG/news', 'Seeking Alpha GOOG News', true, STOCKS_CAT_ID, 25, 'Alphabet Inc. (Google) stock news.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/sector/technology', 'Seeking Alpha Technology Sector', true, STOCKS_CAT_ID, 25, 'News on the tech sector.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/sector/healthcare', 'Seeking Alpha Healthcare Sector', true, STOCKS_CAT_ID, 25, 'News on the healthcare sector.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/sector/financials', 'Seeking Alpha Financials Sector', true, STOCKS_CAT_ID, 25, 'News on the financial sector.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/sector/energy', 'Seeking Alpha Energy Sector', true, COMMODITIES_CAT_ID, 45, 'News on the energy sector.'),
    (gen_random_uuid(), 'https://seekingalpha.com/api/v2/feed/market_outlook', 'Seeking Alpha Market Outlook', true, MACRO_CAT_ID, 30, 'Market commentary and outlook.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/feed/rss.xml', 'Zero Hedge', true, MACRO_CAT_ID, 20, 'Market commentary, often contrarian and macro focused.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/feed', 'Zero Hedge Markets', true, MACRO_CAT_ID, 20, 'Market analysis from Zero Hedge.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/commodities/feed', 'Zero Hedge Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodity market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/crypto/feed', 'Zero Hedge Crypto', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/energy/feed', 'Zero Hedge Energy', true, COMMODITIES_CAT_ID, 45, 'Energy market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/stocks/feed', 'Zero Hedge Stocks', true, STOCKS_CAT_ID, 25, 'Stock market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/forex/feed', 'Zero Hedge Forex', true, FOREX_CAT_ID, 20, 'Forex market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/bonds/feed', 'Zero Hedge Bonds', true, BONDS_CAT_ID, 40, 'Bond market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/geopolitical-analysis/feed', 'Zero Hedge Geopolitics', true, GEOPOLITICS_CAT_ID, 60, 'Geopolitical analysis.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/technology/feed', 'Zero Hedge Tech', true, STOCKS_CAT_ID, 25, 'Technology news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/currency/feed', 'Zero Hedge Currency', true, FOREX_CAT_ID, 20, 'Currency market news.'),
    (gen_random_uuid(), 'https://www.zerohedge.com/markets/metals/feed', 'Zero Hedge Metals', true, COMMODITIES_CAT_ID, 45, 'Precious metals news.'),
    (gen_random_uuid(), 'https://www.schwab.com/learn/story/market-insights/rss', 'Schwab Market Insights', true, TA_STRAT_CAT_ID, 30, 'Market commentary and insights from Charles Schwab.'),
    (gen_random_uuid(), 'https://blog.thinkorswim.com/feed/', 'thinkorswim Blog', true, TA_STRAT_CAT_ID, 30, 'Trading education and strategy articles from TD Ameritrade/Schwab.'),
    (gen_random_uuid(), 'https://www.interactivebrokers.com/en/index.php?f=48063', 'Interactive Brokers News', true, MACRO_CAT_ID, 30, 'Market commentary and research from Interactive Brokers.'),
    (gen_random_uuid(), 'https://www.interactivebrokers.com/en/index.php?f=48059', 'Interactive Brokers Research', true, TA_STRAT_CAT_ID, 30, 'Research reports and analysis.'),
    (gen_random_uuid(), 'https://www.interactivebrokers.com/en/index.php?f=48055', 'Interactive Brokers Economic Calendar', true, MACRO_CAT_ID, 60, 'Economic calendar events and analysis.'),
    (gen_random_uuid(), 'https://www.interactivebrokers.com/en/index.php?f=48057', 'Interactive Brokers Commentary', true, MACRO_CAT_ID, 30, 'Market commentary and outlook.'),
    (gen_random_uuid(), 'https://www.interactivebrokers.com/en/index.php?f=48061', 'Interactive Brokers Daily Briefing', true, MACRO_CAT_ID, 15, 'Daily market summaries.'),
    (gen_random_uuid(), 'https://www.interactivebrokers.com/en/index.php?f=48071', 'Interactive Brokers Podcasts', true, MACRO_CAT_ID, 90, 'IBKR podcasts on markets and trading.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf.rss', 'ETF.com News', true, STOCKS_CAT_ID, 25, 'Exchange Traded Fund news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-strategy', 'ETF.com Strategy', true, TA_STRAT_CAT_ID, 30, 'ETF strategy articles.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-etfs', 'ETF.com ETFs', true, STOCKS_CAT_ID, 25, 'News on specific ETFs.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-market-movers', 'ETF.com Market Movers', true, STOCKS_CAT_ID, 20, 'ETFs influencing market movements.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-etf-trends', 'ETF.com Trends', true, MACRO_CAT_ID, 30, 'Emerging ETF trends.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-fixed-income', 'ETF.com Fixed Income', true, BONDS_CAT_ID, 40, 'Fixed income ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-commodity', 'ETF.com Commodities', true, COMMODITIES_CAT_ID, 45, 'Commodity ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-crypto', 'ETF.com Crypto', true, CRYPTO_CAT_ID, 20, 'Cryptocurrency ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-geopolitics', 'ETF.com Geopolitics', true, GEOPOLITICS_CAT_ID, 60, 'Geopolitics impacting ETFs.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-stocks', 'ETF.com Stocks', true, STOCKS_CAT_ID, 25, 'General stock ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-analysis', 'ETF.com Analysis', true, TA_STRAT_CAT_ID, 30, 'Analysis of ETF strategies and performance.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-global-markets', 'ETF.com Global Markets', true, MACRO_CAT_ID, 40, 'Global market ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-forex', 'ETF.com Forex', true, FOREX_CAT_ID, 20, 'Forex ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-commodities/oil', 'ETF.com Oil', true, COMMODITIES_CAT_ID, 45, 'Oil ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-commodities/gold', 'ETF.com Gold', true, COMMODITIES_CAT_ID, 45, 'Gold ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-commodities/natural-gas', 'ETF.com Natural Gas', true, COMMODITIES_CAT_ID, 45, 'Natural Gas ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-stocks/us-stocks', 'ETF.com US Stocks', true, STOCKS_CAT_ID, 25, 'US Stock ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-stocks/emea-stocks', 'ETF.com EMEA Stocks', true, STOCKS_CAT_ID, 25, 'EMEA Stock ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-stocks/asia-stocks', 'ETF.com Asia Stocks', true, STOCKS_CAT_ID, 25, 'Asian Stock ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-crypto/bitcoin', 'ETF.com Bitcoin', true, CRYPTO_CAT_ID, 20, 'Bitcoin ETF news.'),
    (gen_random_uuid(), 'https://www.etf.com/sections/feeds/etf-crypto/ethereum', 'ETF.com Ethereum', true, CRYPTO_CAT_ID, 20, 'Ethereum ETF news.');
    
    -- Add more sources here, ensuring each has a unique UUID and belongs to a valid SignalCategory.
    -- Example for a Technical Analysis feed:
    -- INSERT INTO public."RssSources" ("Id", "Url", "SourceName", "IsActive", "DefaultSignalCategoryId", "FetchIntervalMinutes", "Description") VALUES
    -- (gen_random_uuid(), 'https://www.tradingview.com/feed/ideas/', 'TradingView Ideas', true, TA_STRAT_CAT_ID, 45, 'Trading ideas from TradingView users.');

END $$;