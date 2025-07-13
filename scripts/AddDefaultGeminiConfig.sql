-- Add default Gemini configuration
-- This script adds a default Gemini API configuration to enable message enhancement

INSERT INTO "AiApiConfigurations" (
    "ProviderName",
    "IsEnabled",
    "ApiKey",
    "ModelName",
    "PromptTemplate",
    "Description",
    "ApiKeyName",
    "CreatedAt",
    "LastUpdatedAt"
) VALUES (
    'Gemini',
    true,
    'YOUR_GEMINI_API_KEY_HERE', -- Replace with your actual Gemini API key
    'gemini-1.5-flash-latest',
    'You are an expert financial content enhancer. Your task is to improve the given trading signal message to make it more professional, engaging, and informative while maintaining all the original trading information.

IMPORTANT RULES:
1. Keep ALL original trading data (prices, levels, symbols) exactly as provided
2. Add professional formatting and structure
3. Enhance the language to be more engaging and professional
4. Add relevant trading context or insights if appropriate
5. Use markdown formatting for better presentation
6. Keep the message concise but informative

Original message: {message}

Enhanced message:',
    'Default Gemini configuration for message enhancement',
    'Default',
    NOW(),
    NOW()
) ON CONFLICT ("ProviderName") DO UPDATE SET
    "IsEnabled" = EXCLUDED."IsEnabled",
    "ApiKey" = EXCLUDED."ApiKey",
    "ModelName" = EXCLUDED."ModelName",
    "PromptTemplate" = EXCLUDED."PromptTemplate",
    "Description" = EXCLUDED."Description",
    "ApiKeyName" = EXCLUDED."ApiKeyName",
    "LastUpdatedAt" = NOW();

-- Verify the configuration was added
SELECT 
    "Id",
    "ProviderName",
    "IsEnabled",
    "ModelName",
    "ApiKeyName",
    "CreatedAt"
FROM "AiApiConfigurations" 
WHERE "ProviderName" = 'Gemini'; 