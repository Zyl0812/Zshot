namespace Zshot.Core.Translation;

public sealed class TranslationRequest
{
    public required string Text { get; init; }
    public required string TargetLanguage { get; init; }
    public string? SystemPrompt { get; init; }
    public string? Model { get; init; }
}

public sealed class TranslationResult
{
    public required string TranslatedText { get; init; }
    public string? Model { get; init; }
}

public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}

public static class TranslationPrompt
{
    public const string DefaultSystemPrompt =
        "You are a translator. Output only the translation. Keep paragraph structure. Do not explain. Do not translate URLs, file paths, or code. Keep numbers and units.";

    public static string BuildSystemPrompt(string? customPrompt, string targetLanguage)
    {
        string basePrompt = string.IsNullOrWhiteSpace(customPrompt) ? DefaultSystemPrompt : customPrompt.Trim();
        return $"{basePrompt}\nTarget language: {targetLanguage}";
    }
}

public static class ChatCompletionParser
{
    public static string? ExtractFirstMessageContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }

        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString();
        }

        return null;
    }
}
