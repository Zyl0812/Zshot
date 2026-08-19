using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Zshot.Core.Translation;

public sealed class CustomApiTranslationSettings
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string TargetLanguage { get; set; } = "zh-CN";
    public string? SystemPrompt { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class CustomApiTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly CustomApiTranslationSettings _settings;

    public CustomApiTranslationProvider(HttpClient http, CustomApiTranslationSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            throw new InvalidOperationException("Translation Base URL is not configured.");
        }

        string endpoint = _settings.BaseUrl.TrimEnd('/');
        if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            && !endpoint.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? "/chat/completions" : "/v1/chat/completions";
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        var payload = new
        {
            model = request.Model ?? _settings.Model,
            messages = new object[]
            {
                new { role = "system", content = TranslationPrompt.BuildSystemPrompt(request.SystemPrompt ?? _settings.SystemPrompt, request.TargetLanguage) },
                new { role = "user", content = request.Text },
            },
            temperature = 0.2,
        };
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Translation API failed ({(int)response.StatusCode}).");
        }

        string? text = ChatCompletionParser.ExtractFirstMessageContent(body);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Translation API returned an empty result.");
        }

        return new TranslationResult { TranslatedText = text.Trim(), Model = request.Model ?? _settings.Model };
    }
}
