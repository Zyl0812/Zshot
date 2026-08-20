using Zshot.Core.Translation;
using Xunit;

namespace Zshot.Core.Tests;

public class TranslationTests
{
    [Fact]
    public void Parser_reads_openai_chat_completion_content()
    {
        const string json = """
            {"choices":[{"message":{"role":"assistant","content":"你好"}}]}
            """;

        Assert.Equal("你好", ChatCompletionParser.ExtractFirstMessageContent(json));
    }

    [Fact]
    public void Parser_returns_null_for_html_and_missing_message()
    {
        Assert.Null(ChatCompletionParser.ExtractFirstMessageContent("<html>error</html>"));
        Assert.Null(ChatCompletionParser.ExtractFirstMessageContent("""{"choices":[{"delta":{}}]}"""));
        Assert.Null(ChatCompletionParser.ExtractFirstMessageContent("not-json"));
    }

    [Fact]
    public void Prompt_includes_target_language()
    {
        string prompt = TranslationPrompt.BuildSystemPrompt(null, "en-US");
        Assert.Contains("en-US", prompt, StringComparison.Ordinal);
        Assert.Contains("translator", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_posts_to_chat_completions_and_returns_text()
    {
        var handler = new StubHandler(req =>
        {
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("secret", req.Headers.Authorization?.Parameter);
            Assert.Contains("/v1/chat/completions", req.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
            return """{"choices":[{"message":{"content":"Hello"}}]}""";
        });
        using var http = new HttpClient(handler);
        var provider = new CustomApiTranslationProvider(http, new CustomApiTranslationSettings
        {
            BaseUrl = "https://example.test/v1",
            ApiKey = "secret",
            Model = "demo",
            TargetLanguage = "en",
        });

        var result = await provider.TranslateAsync(new TranslationRequest
        {
            Text = "你好",
            TargetLanguage = "en",
        });

        Assert.Equal("Hello", result.TranslatedText);
        Assert.Equal("demo", result.Model);
        Assert.Equal("https://example.test/v1/chat/completions", handler.LastUri);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responder;
        public string? LastUri { get; private set; }

        public StubHandler(Func<HttpRequestMessage, string> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.ToString();
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_responder(request)),
            };
            return Task.FromResult(response);
        }
    }
}
