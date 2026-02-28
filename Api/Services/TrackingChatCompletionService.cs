using System.Collections;
using AiAgileTeam.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiAgileTeam.Services;

public sealed class TrackingChatCompletionService : IChatCompletionService
{
    private readonly IChatCompletionService _inner;
    private readonly ITokenUsageTracker _tracker;
    private readonly IModelPricingService _pricingService;
    private readonly ITokenUsageContextAccessor _contextAccessor;
    private readonly string _agentName;
    private readonly string _executionId;
    private readonly ILogger<TrackingChatCompletionService> _logger;

    public TrackingChatCompletionService(
        IChatCompletionService inner,
        ITokenUsageTracker tracker,
        IModelPricingService pricingService,
        ITokenUsageContextAccessor contextAccessor,
        string agentName,
        string executionId,
        ILogger<TrackingChatCompletionService> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(pricingService);
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _tracker = tracker;
        _pricingService = pricingService;
        _contextAccessor = contextAccessor;
        _agentName = agentName;
        _executionId = executionId;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, object?> Attributes => _inner.Attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var results = await _inner.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        var effectiveModelId = ResolveModelId(executionSettings?.ModelId);
        var visionTokens = CalculateVisionTokens(chatHistory, effectiveModelId);

        if (TryExtractUsage(results.Select(m => m.Metadata).ToArray(), out var promptTokens, out var completionTokens))
        {
            RecordUsage(effectiveModelId, promptTokens, completionTokens, visionTokens);
        }
        else if (results.Count > 0)
        {
            RecordUsage(effectiveModelId, EstimatePromptTokens(chatHistory), EstimateCompletionTokens(results), visionTokens);
        }

        return results;
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamed = new List<StreamingChatMessageContent>();
        var usageMetadata = new List<IReadOnlyDictionary<string, object?>?>();
        var effectiveModelId = ResolveModelId(executionSettings?.ModelId);
        var visionTokens = CalculateVisionTokens(chatHistory, effectiveModelId);

        await foreach (var chunk in _inner.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
        {
            streamed.Add(chunk);
            usageMetadata.Add(chunk.Metadata);
            yield return chunk;
        }

        if (TryExtractUsage(usageMetadata.ToArray(), out var promptTokens, out var completionTokens))
        {
            RecordUsage(effectiveModelId, promptTokens, completionTokens, visionTokens);
            yield break;
        }

        if (streamed.Count > 0)
        {
            RecordUsage(effectiveModelId, EstimatePromptTokens(chatHistory), EstimateCompletionTokens(streamed), visionTokens);
        }
    }

    private string ResolveModelId(string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            return modelId;
        }

        if (_inner.Attributes.TryGetValue("ModelId", out var attr) && attr is string attrModel && !string.IsNullOrWhiteSpace(attrModel))
        {
            return attrModel;
        }

        return "unknown";
    }

    private void RecordUsage(string? modelId, int promptTokens, int completionTokens, int imageTokens)
    {
        var context = _contextAccessor.Current;
        var executionId = !string.IsNullOrWhiteSpace(_executionId) ? _executionId : context?.ExecutionId ?? "unknown";
        var step = context?.Step ?? "Response";
        var resolvedModelId = ResolveModelId(modelId);
        var provider = resolvedModelId.Contains("gemini", StringComparison.OrdinalIgnoreCase)
            ? "GoogleGemini"
            : "OpenAI";

        var textCost = _pricingService.CalculateCost(
            provider,
            resolvedModelId,
            promptTokens,
            completionTokens,
            inputType: "text",
            contextLength: promptTokens);
        var mediaCost = _pricingService.CalculateCost(
            provider,
            resolvedModelId,
            imageTokens,
            0,
            inputType: "image",
            contextLength: promptTokens);
        var estimatedCost = textCost + mediaCost;
        var record = new TokenUsageRecord(
            ExecutionId: executionId,
            AgentName: _agentName,
            Step: step,
            ModelId: resolvedModelId,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            Timestamp: DateTime.UtcNow,
            EstimatedCost: estimatedCost,
            ImageTokens: imageTokens,
            MediaCost: mediaCost);

        _tracker.Record(record);

        Console.WriteLine($"[TokenTracking] ExecutionId={executionId}, Agent={_agentName}, Model={resolvedModelId}, Prompt={promptTokens}, Completion={completionTokens}, Cost={estimatedCost:F6}");

        _logger.LogInformation(
            "Token usage tracked. ExecutionId: {ExecutionId}, Agent: {AgentName}, Model: {ModelId}, Prompt: {PromptTokens}, Completion: {CompletionTokens}, Cost: {Cost}",
            executionId,
            _agentName,
            resolvedModelId,
            promptTokens,
            completionTokens,
            estimatedCost);
    }

    private static bool TryExtractUsage(
        IReadOnlyList<IReadOnlyDictionary<string, object?>?> metadataCollection,
        out int promptTokens,
        out int completionTokens)
    {
        promptTokens = 0;
        completionTokens = 0;

        foreach (var metadata in metadataCollection)
        {
            if (metadata is null)
            {
                continue;
            }

            if (!TryGetUsageValue(metadata, out var usageObject) || usageObject is null)
            {
                continue;
            }

            if (TryExtractUsageFromObject(usageObject, out promptTokens, out completionTokens))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUsageValue(IReadOnlyDictionary<string, object?> metadata, out object? usageObject)
    {
        if (metadata.TryGetValue("Usage", out usageObject) || metadata.TryGetValue("usage", out usageObject))
        {
            return true;
        }

        usageObject = null;
        return false;
    }

    private static bool TryExtractUsageFromObject(object usageObject, out int promptTokens, out int completionTokens)
    {
        promptTokens = 0;
        completionTokens = 0;

        if (usageObject is IDictionary usageDictionary)
        {
            promptTokens = TryReadFromDictionary(usageDictionary, "input_tokens", "prompt_tokens", "promptTokens");
            completionTokens = TryReadFromDictionary(usageDictionary, "output_tokens", "completion_tokens", "completionTokens");
            return promptTokens > 0 || completionTokens > 0;
        }

        var usageType = usageObject.GetType();
        promptTokens = TryReadByReflection(usageObject, usageType, "InputTokenCount", "PromptTokens", "PromptTokenCount", "InputTokens");
        completionTokens = TryReadByReflection(usageObject, usageType, "OutputTokenCount", "CompletionTokens", "CompletionTokenCount", "OutputTokens");

        return promptTokens > 0 || completionTokens > 0;
    }

    private static int TryReadByReflection(object target, Type targetType, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = targetType.GetProperty(propertyName);
            if (property?.GetValue(target) is int intValue)
            {
                return intValue;
            }

            if (property?.GetValue(target) is long longValue)
            {
                return (int)longValue;
            }
        }

        return 0;
    }

    private static int TryReadFromDictionary(IDictionary dictionary, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!dictionary.Contains(key))
            {
                continue;
            }

            var value = dictionary[key];
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return (int)longValue;
            }

            if (value is string stringValue && int.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static int EstimatePromptTokens(ChatHistory history)
    {
        if (history.Count == 0)
        {
            return 0;
        }

        var totalLength = history.Sum(message => message.Content?.Length ?? 0);
        return EstimateTokensFromLength(totalLength);
    }

    private static int EstimateCompletionTokens(IEnumerable<ChatMessageContent> messages)
    {
        var totalLength = messages.Sum(message => message.Content?.Length ?? 0);
        return EstimateTokensFromLength(totalLength);
    }

    private static int EstimateCompletionTokens(IEnumerable<StreamingChatMessageContent> chunks)
    {
        var totalLength = chunks.Sum(chunk => chunk.Content?.Length ?? 0);
        return EstimateTokensFromLength(totalLength);
    }

    private static int EstimateTokensFromLength(int textLength)
    {
        if (textLength <= 0)
        {
            return 0;
        }

        return Math.Max(1, textLength / 4);
    }

    private static int CalculateVisionTokens(ChatHistory chatHistory, string? modelId)
    {
        if (chatHistory.Count == 0 || string.IsNullOrWhiteSpace(modelId))
        {
            return 0;
        }

        if (!modelId.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var imageItems = chatHistory
            .Where(message => message.Items is not null)
            .SelectMany(message => message.Items!)
            .Where(item => item.GetType().Name.Contains("ImageContent", StringComparison.Ordinal))
            .ToList();

        if (imageItems.Count == 0)
        {
            return 0;
        }

        var totalTokens = 0;
        foreach (var item in imageItems)
        {
            var width = TryReadByReflection(item, item.GetType(), "Width", "PixelWidth", "ImageWidth");
            var height = TryReadByReflection(item, item.GetType(), "Height", "PixelHeight", "ImageHeight");
            if (width <= 0)
            {
                width = 1024;
            }

            if (height <= 0)
            {
                height = 1024;
            }

            var detailText = item.GetType().GetProperty("Detail")?.GetValue(item)?.ToString()
                             ?? item.GetType().GetProperty("DetailLevel")?.GetValue(item)?.ToString()
                             ?? "standard";

            totalTokens += CalculateVisionTokensForImage(width, height, detailText);
        }

        return totalTokens;
    }

    private static int CalculateVisionTokensForImage(int width, int height, string? detailLevel)
    {
        var tilesX = (int)Math.Ceiling(width / 512d);
        var tilesY = (int)Math.Ceiling(height / 512d);
        var tileTokens = tilesX * tilesY * 85;

        if (string.Equals(detailLevel, "high", StringComparison.OrdinalIgnoreCase))
        {
            return tileTokens * 2;
        }

        return tileTokens;
    }
}
