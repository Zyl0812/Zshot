namespace Zshot.Core.Overlay;

/// <summary>
/// 截图工具栏可选项。顺序固定；设置只决定显示/隐藏。复制不在工具栏上。
/// </summary>
public static class OverlayToolbarCatalog
{
    public const string Color = "color";
    public const string Select = "select";
    public const string Rect = "rect";
    public const string Ellipse = "ellipse";
    public const string Line = "line";
    public const string Arrow = "arrow";
    public const string Pen = "pen";
    public const string Text = "text";
    public const string Mosaic = "mosaic";
    public const string Number = "number";
    public const string Undo = "undo";
    public const string Redo = "redo";
    public const string Clear = "clear";
    public const string Ocr = "ocr";
    public const string Translate = "translate";
    public const string LongCapture = "longCapture";
    public const string Save = "save";
    public const string Complete = "complete";
    public const string Cancel = "cancel";
    public const string Copy = "copy";

    public static readonly string[] Customizable =
    [
        Color, Select, Rect, Ellipse, Line, Arrow, Pen, Text,
        Mosaic, Number, Undo, Redo, Clear, Ocr, Translate, LongCapture, Save,
    ];

    public static readonly string[] DefaultHidden = [Ellipse, Text, Number, Redo];

    public static readonly string DefaultHiddenValue = "ellipse,text,number,redo";

    private static readonly HashSet<string> CustomizableSet = new(Customizable, StringComparer.Ordinal);

    public static IReadOnlySet<string> ParseHidden(string? stored)
    {
        if (stored is null)
        {
            return new HashSet<string>(DefaultHidden, StringComparer.Ordinal);
        }

        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (CustomizableSet.Contains(part))
            {
                hidden.Add(part);
            }
        }

        return hidden;
    }

    public static string SerializeHidden(IEnumerable<string> hidden)
    {
        var set = hidden as IReadOnlySet<string> ?? hidden.ToHashSet(StringComparer.Ordinal);
        return string.Join(',', Customizable.Where(set.Contains));
    }

    public static bool IsVisible(string id, IReadOnlySet<string> hidden)
    {
        if (id == Copy)
        {
            return false;
        }

        if (id is Complete or Cancel)
        {
            return true;
        }

        return !CustomizableSet.Contains(id) || !hidden.Contains(id);
    }
}
