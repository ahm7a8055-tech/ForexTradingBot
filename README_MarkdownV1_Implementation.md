# Telegram Markdown V1 Implementation

## Overview

This implementation provides comprehensive markdown parsing support for sending messages to Telegram channels using Markdown V1 formatting. The system automatically parses markdown text and converts it to Telegram message entities, ensuring proper formatting when messages are sent.

## Features

### ✅ Implemented Features

1. **Automatic Markdown Parsing**: The `SendMessageAsync` and `SendMediaGroupAsync` methods automatically parse markdown syntax in message text and album captions.

2. **Markdown V1 Support**: Full support for Telegram's Markdown V1 syntax including:
   - **Bold text**: `**bold**` or `__bold__`
   - **Italic text**: `*italic*` or `_italic_`
   - **Inline code**: `` `code` ``
   - **Code blocks**: ```` ```language\ncode\n``` ````
   - **Links**: `[text](url)`
   - **Headers**: `# Header 1`, `## Header 2`, etc.
   - **Lists**: `- Item 1`, `- Item 2`
   - **Quotes**: `> Quote text`

3. **Entity Conversion**: Converts markdown syntax to Telegram message entities:
   - `MessageEntityBold` for bold text
   - `MessageEntityItalic` for italic text
   - `MessageEntityCode` for inline code
   - `MessageEntityPre` for code blocks
   - `MessageEntityTextUrl` for links

4. **Resilient Parsing**: Graceful fallback to plain text if markdown parsing fails.

5. **Comprehensive Logging**: Detailed logging for debugging and monitoring.

## Architecture

### Clean Architecture Implementation

The implementation follows clean architecture principles:

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              ITelegramUserApiClient                 │   │
│  │              (Interface)                            │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              TelegramUserApiClient                  │   │
│  │              (Implementation)                       │   │
│  └─────────────────────────────────────────────────────┘   │
│                              │                              │
│                              ▼                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              MarkdownParserService                  │   │
│  │              (Markdown Processing)                  │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

1. **`ITelegramUserApiClient`**: Interface defining the contract for Telegram user API operations.

2. **`TelegramUserApiClient`**: Main implementation that handles message sending with markdown parsing.

3. **`MarkdownParserService`**: Service responsible for parsing markdown and converting to Telegram entities.

## Usage

### Basic Usage

The markdown parsing is automatically applied when sending messages:

```csharp
// The message will be automatically parsed for markdown
await telegramClient.SendMessageAsync(
    peer: targetPeer,
    message: "# Welcome\nThis is **bold** and *italic* text with `code` and [link](https://example.com)",
    cancellationToken: cancellationToken
);
```

### Advanced Usage

You can also use the markdown parser service directly:

```csharp
var markdownParser = new MarkdownParserService(logger);
var (plainText, entities) = markdownParser.ParseMarkdownToTelegramEntities(
    "# Header\n**Bold text** and *italic text*"
);

// Use the parsed text and entities
await telegramClient.SendMessageAsync(
    peer: targetPeer,
    message: plainText,
    entities: entities,
    cancellationToken: cancellationToken
);
```

## Configuration

### Dependency Injection

The services are automatically registered in the DI container:

```csharp
// In Infrastructure/Data/ServiceCollectionExtensions.cs
services.AddSingleton<MarkdownParserService>();
services.AddSingleton<ITelegramUserApiClient, TelegramUserApiClient>();
```

### Settings

The implementation uses the latest .NET 9 and follows the project's configuration patterns:

```json
{
  "TelegramUserApi": {
    "ApiId": "your_api_id",
    "ApiHash": "your_api_hash",
    "PhoneNumber": "your_phone_number"
  }
}
```

## Testing

### Built-in Test Method

The `MarkdownParserService` includes a comprehensive test method:

```csharp
var testService = new MarkdownParserService(logger);
var testCases = testService.TestMarkdownParsing();

foreach (var testCase in testCases)
{
    Console.WriteLine($"Test: {testCase.Key}");
    Console.WriteLine($"Text: {testCase.Value.plainText}");
    Console.WriteLine($"Entities: {testCase.Value.entities.Length}");
}
```

### Demonstration Method

Run the demonstration to see the parser in action:

```csharp
MarkdownParserService.DemonstrateMarkdownParsing(logger);
```

## Supported Markdown Syntax

### Text Formatting

| Markdown | Telegram Entity | Example |
|----------|----------------|---------|
| `**text**` | Bold | **Hello World** |
| `*text*` | Italic | *Hello World* |
| `` `text` `` | Code | `var message = "Hello";` |

### Code Blocks

```markdown
```csharp
public async Task SendMessageAsync(string message)
{
    // Implementation
}
```
```

### Links

```markdown
[Visit our website](https://example.com)
```

### Headers

```markdown
# Main Header
## Sub Header
### Small Header
```

### Lists

```markdown
- Item 1
- **Bold Item 2**
- *Italic Item 3*
```

### Quotes

```markdown
> This is a quote
> With multiple lines
```

## Error Handling

The implementation includes robust error handling:

1. **Graceful Fallback**: If markdown parsing fails, the original text is used without entities.

2. **Comprehensive Logging**: All parsing operations are logged for debugging.

3. **Exception Safety**: Parsing errors don't prevent message sending.

## Performance Considerations

1. **Efficient Parsing**: Uses Markdig library for fast and reliable markdown parsing.

2. **Caching**: The markdown parser is registered as a singleton for optimal performance.

3. **Memory Management**: Proper disposal of resources and efficient string handling.

## Integration with Existing Features

The markdown parsing integrates seamlessly with existing features:

1. **Advice Service**: Markdown parsing occurs after advice footers are added.

2. **Hashtag Service**: Markdown parsing occurs after hashtag footers are added.

3. **AI Enhancement**: Markdown parsing occurs before AI enhancement, ensuring AI can work with the parsed text.

4. **Media Groups**: Markdown parsing works for both text messages and album captions.

## Logging

The implementation provides comprehensive logging:

```csharp
// Debug level logging for parsing operations
_logger.LogDebug("Attempting to parse markdown in message for Peer {PeerId}.", peerIdForLog);

// Information level logging for successful parsing
_logger.LogInformation("Successfully parsed markdown for Peer {PeerId}. Found {EntityCount} entities.", 
    peerIdForLog, parsedEntities.Length);

// Warning level logging for parsing failures
_logger.LogWarning(ex, "An exception occurred during markdown parsing for Peer {PeerId}. Proceeding with original message.", peerIdForLog);
```

## Best Practices

1. **Use Regions**: All code is organized using regions for better readability.

2. **Clean Architecture**: Follows clean architecture principles with proper separation of concerns.

3. **Latest .NET**: Uses .NET 9 and the latest NuGet packages.

4. **Hangfire Integration**: Compatible with Hangfire for background job processing.

5. **Resilience**: Includes Polly retry policies for robust operation.

## Troubleshooting

### Common Issues

1. **No Entities Generated**: Check if the markdown syntax is correct and supported.

2. **Parsing Errors**: Review logs for specific error messages and ensure the markdown is valid.

3. **Performance Issues**: Monitor logging to ensure parsing is not taking too long.

### Debug Mode

Enable debug logging to see detailed parsing information:

```csharp
// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Infrastructure.Services.MarkdownParserService": "Debug"
    }
  }
}
```

## Future Enhancements

Potential future improvements:

1. **Markdown V2 Support**: Add support for Telegram's Markdown V2 syntax.
2. **Custom Entities**: Support for custom message entities.
3. **Performance Optimization**: Further optimization of parsing performance.
4. **Extended Syntax**: Support for additional markdown features like tables, images, etc.

## Conclusion

This implementation provides a robust, feature-rich markdown parsing solution for Telegram messages that integrates seamlessly with the existing codebase. It follows clean architecture principles, uses the latest .NET technologies, and provides comprehensive error handling and logging. 