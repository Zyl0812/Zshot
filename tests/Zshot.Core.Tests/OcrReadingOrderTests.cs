using Zshot.Core.Ocr;
using Xunit;

namespace Zshot.Core.Tests;

public class OcrReadingOrderTests
{
    [Fact]
    public void Sort_reads_left_to_right_then_top_to_bottom()
    {
        var blocks = new[]
        {
            Block("B", 80, 10),
            Block("A", 10, 12),
            Block("C", 12, 50),
        };

        var ordered = OcrReadingOrder.Sort(blocks);
        Assert.Equal(new[] { "A", "B", "C" }, ordered.Select(b => b.Text));
    }

    [Fact]
    public void ToPlainText_keeps_line_breaks_between_rows()
    {
        var blocks = new[]
        {
            Block("hello", 10, 10),
            Block("world", 80, 11),
            Block("next", 12, 40),
        };

        string text = OcrReadingOrder.ToPlainText(blocks);
        Assert.Equal("hello world" + Environment.NewLine + "next", text);
    }

    [Fact]
    public async Task LocalOcrEngine_applies_reading_order()
    {
        var recognizer = new StubRecognizer(new[]
        {
            Block("two", 90, 8),
            Block("one", 10, 8),
        });
        var engine = new LocalOcrEngine(recognizer);
        var result = await engine.RecognizeAsync(new OcrRequest
        {
            BgraPixels = new byte[8 * 8 * 4],
            Width = 8,
            Height = 8,
        });

        Assert.Equal("LocalStub", engine.Name);
        Assert.Equal("one two", result.PlainText);
        Assert.Equal(new[] { "one", "two" }, result.Blocks.Select(b => b.Text));
    }

    private static OcrBlock Block(string text, double x, double y)
        => new()
        {
            Text = text,
            Confidence = 0.9,
            Polygon =
            [
                new OcrPoint(x, y),
                new OcrPoint(x + 20, y),
                new OcrPoint(x + 20, y + 10),
                new OcrPoint(x, y + 10),
            ],
        };

    private sealed class StubRecognizer : IOcrRecognizer
    {
        private readonly IReadOnlyList<OcrBlock> _blocks;
        public StubRecognizer(IReadOnlyList<OcrBlock> blocks) => _blocks = blocks;
        public string Name => "LocalStub";
        public Task<IReadOnlyList<OcrBlock>> DetectAsync(OcrRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_blocks);
    }
}
