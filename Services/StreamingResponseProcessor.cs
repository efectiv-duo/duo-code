using System.Text;
using System.Text.Json;
using duo_code.Models;

namespace duo_code.Services;

/// <summary>
/// Handles processing a streaming API response to provide user feedback
/// for "thinking" states and returns the response and thinking content separately.
/// </summary>
public static class StreamingResponseProcessor
{
    /// <summary>
    /// Processes a streaming response, showing a static "Thinking..." message for <think> blocks.
    /// Returns the clean response and extracted thinking content separately.
    /// </summary>
    /// <param name="httpResponse">The HttpResponseMessage from the API.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="provider">The API provider to determine parsing format.</param>
    /// <returns>ProcessedResponse containing the response and thinking content.</returns>
    public static async Task<ProcessedResponse> ProcessAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken = default, ApiProvider? provider = null)
    {
        // Auto-detect provider if not specified
        var detectedProvider = provider ?? DetectProvider(httpResponse);
        
        return detectedProvider switch
        {
            ApiProvider.Gemini => await ProcessGeminiStreamAsync(httpResponse, cancellationToken),
            ApiProvider.Cerebras => await ProcessCerebrasStreamAsync(httpResponse, cancellationToken),
            _ => await ProcessCerebrasStreamAsync(httpResponse, cancellationToken) // Default to Cerebras
        };
    }

    private static ApiProvider DetectProvider(HttpResponseMessage httpResponse)
    {
        // Try to detect based on response headers or URL
        var contentType = httpResponse.Content.Headers.ContentType?.MediaType;
        
        // Gemini typically returns application/json, Cerebras returns text/plain for SSE
        if (contentType == "application/json")
        {
            return ApiProvider.Gemini;
        }
        
        return ApiProvider.Cerebras; // Default
    }

    private static async Task<ProcessedResponse> ProcessCerebrasStreamAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
    {
        var responseBuilder = new StringBuilder(); // Clean response without thinking blocks
        var thinkingBuilder = new StringBuilder(); // All thinking content
        var buffer = new StringBuilder();
        var currentThinkingBuilder = new StringBuilder(); // Accumulates current thinking block
        bool inThinkBlock = false;

        var stream = await httpResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null || !line.StartsWith("data: ")) continue;
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();

            var dataJson = line.Substring("data: ".Length).Trim();
            if (string.IsNullOrWhiteSpace(dataJson) || dataJson.Equals("[DONE]", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(dataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var contentChunk = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                if (contentChunk == null) continue;

                buffer.Append(contentChunk);

                ProcessContentBuffer(buffer, responseBuilder, thinkingBuilder, currentThinkingBuilder, ref inThinkBlock);
            }
            catch (JsonException) { /* Ignore malformed JSON chunks */ }
        }

        return FinalizeResponse(responseBuilder, thinkingBuilder, currentThinkingBuilder, buffer, inThinkBlock, cancellationToken);
    }

    private static async Task<ProcessedResponse> ProcessGeminiStreamAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
    {
        var responseBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var buffer = new StringBuilder();
        var currentThinkingBuilder = new StringBuilder();
        bool inThinkBlock = false;

        var stream = await httpResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        
        var jsonContent = await reader.ReadToEndAsync();
        
        // Parse as JSON array
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            
            if (root.ValueKind == JsonValueKind.Array)
            {
                // It's a proper JSON array
                foreach (var element in root.EnumerateArray())
                {
                    ProcessGeminiJsonElement(element, buffer);
                }
            }
        }
        catch (JsonException)
        {
            // If it's not a valid JSON array, try parsing as comma-separated objects
            var jsonObjects = SplitGeminiJsonObjects(jsonContent);
        
            foreach (var jsonObj in jsonObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var cleanJson = jsonObj.Trim();
                    if (string.IsNullOrEmpty(cleanJson)) continue;
                    
                    using var doc = JsonDocument.Parse(cleanJson);
                    ProcessGeminiJsonElement(doc.RootElement, buffer);
                }
                catch (JsonException ex) 
                { 
                    // Log the error for debugging
                    Console.WriteLine($"\nJSON Parse Error: {ex.Message}");
                    Console.WriteLine($"JSON: {jsonObj.Substring(0, Math.Min(100, jsonObj.Length))}...");
                }
            }
        }
        
        // Process the complete buffer for thinking blocks
        ProcessContentBuffer(buffer, responseBuilder, thinkingBuilder, currentThinkingBuilder, ref inThinkBlock);

        return FinalizeResponse(responseBuilder, thinkingBuilder, currentThinkingBuilder, buffer, inThinkBlock, cancellationToken);
    }

    private static void ProcessContentBuffer(StringBuilder buffer, StringBuilder responseBuilder, StringBuilder thinkingBuilder, StringBuilder currentThinkingBuilder, ref bool inThinkBlock)
    {
        while (true) // Process the buffer repeatedly until no more tags can be found
        {
            if (inThinkBlock)
            {
                int endTagIndex = buffer.ToString().IndexOf("</think>");
                if (endTagIndex != -1)
                {
                    // Capture the thinking content
                    currentThinkingBuilder.Append(buffer.ToString(0, endTagIndex));
                    
                    // Add to thinking collection
                    if (thinkingBuilder.Length > 0) thinkingBuilder.AppendLine();
                    thinkingBuilder.Append(currentThinkingBuilder.ToString());
                    
                    ShowDoneMessage();
                    buffer.Remove(0, endTagIndex + "</think>".Length);
                    currentThinkingBuilder.Clear();
                    inThinkBlock = false;
                    continue; // Re-process the buffer for more tags
                }
                else
                {
                    // Accumulate thinking content
                    currentThinkingBuilder.Append(buffer);
                    buffer.Clear();
                    break; // Need more data
                }
            }
            else // Not in a think block
            {
                int startTagIndex = buffer.ToString().IndexOf("<think>");
                if (startTagIndex != -1)
                {
                    string preThoughtText = buffer.ToString(0, startTagIndex);
                    responseBuilder.Append(preThoughtText);

                    buffer.Remove(0, startTagIndex + "<think>".Length);
                    ShowThinkingMessage();
                    inThinkBlock = true;
                    continue; // Re-process the buffer
                }
                else
                {
                    break; // No start tag found, need more data
                }
            }
        }
    }

    private static ProcessedResponse FinalizeResponse(StringBuilder responseBuilder, StringBuilder thinkingBuilder, StringBuilder currentThinkingBuilder, StringBuilder buffer, bool inThinkBlock, CancellationToken cancellationToken)
    {
        // Check if we were cancelled
        if (cancellationToken.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[Processing cancelled]");
            Console.ResetColor();
            throw new OperationCanceledException();
        }
        
        // After the loop, handle any remaining content
        if (!inThinkBlock && buffer.Length > 0)
        {
            // No unclosed thinking block, append to response
            var remaining = buffer.ToString();
            responseBuilder.Append(remaining);
            Console.Write(remaining); // Print remaining content
        }
        else if (inThinkBlock && currentThinkingBuilder.Length > 0)
        {
            // Unclosed thinking block - still add to thinking
            if (thinkingBuilder.Length > 0) thinkingBuilder.AppendLine();
            thinkingBuilder.Append(currentThinkingBuilder.ToString());
        }

        return new ProcessedResponse
        {
            Content = responseBuilder.ToString().TrimStart(),
            Thinking = thinkingBuilder.ToString()
        };
    }

    private static void ShowThinkingMessage()
    {
        Console.WriteLine(); // Start on a new line
        Console.Write("Thinking...");
    }

    private static void ShowDoneMessage()
    {
        Console.WriteLine();
        Console.Write("Done thinking.\n"); 
    }

    private static void ProcessGeminiJsonElement(JsonElement element, StringBuilder buffer)
    {
        if (element.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("content", out var content) && 
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                var part = parts[0];
                if (part.TryGetProperty("text", out var text))
                {
                    var contentChunk = text.GetString();
                    if (!string.IsNullOrEmpty(contentChunk))
                    {
                        buffer.Append(contentChunk);
                        Console.Write(contentChunk); // Show content as it comes
                    }
                }
            }
        }
    }

    private static List<string> SplitGeminiJsonObjects(string jsonContent)
    {
        var jsonObjects = new List<string>();
        var cleanContent = jsonContent.Trim();
        
        // Remove outer brackets if present
        if (cleanContent.StartsWith('[')) cleanContent = cleanContent.Substring(1);
        if (cleanContent.EndsWith(']')) cleanContent = cleanContent.Substring(0, cleanContent.Length - 1);
        
        var currentObject = new StringBuilder();
        int braceCount = 0;
        bool inString = false;
        bool escapeNext = false;
        
        for (int i = 0; i < cleanContent.Length; i++)
        {
            char c = cleanContent[i];
            
            if (escapeNext)
            {
                escapeNext = false;
                currentObject.Append(c);
                continue;
            }
            
            if (c == '\\')
            {
                escapeNext = true;
                currentObject.Append(c);
                continue;
            }
            
            if (c == '"')
            {
                inString = !inString;
            }
            
            if (!inString)
            {
                if (c == '{')
                {
                    braceCount++;
                }
                else if (c == '}')
                {
                    braceCount--;
                }
            }
            
            currentObject.Append(c);
            
            // When we complete an object and hit a comma
            if (!inString && braceCount == 0 && c == ',' && currentObject.Length > 1)
            {
                var objStr = currentObject.ToString().TrimEnd(',').Trim();
                if (!string.IsNullOrEmpty(objStr))
                {
                    jsonObjects.Add(objStr);
                }
                currentObject.Clear();
            }
        }
        
        // Add the last object
        if (currentObject.Length > 0)
        {
            var objStr = currentObject.ToString().Trim();
            if (!string.IsNullOrEmpty(objStr))
            {
                jsonObjects.Add(objStr);
            }
        }
        
        return jsonObjects;
    }
}