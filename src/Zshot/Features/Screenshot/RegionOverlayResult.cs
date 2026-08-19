using Microsoft.Graphics.Canvas;

namespace Zshot.Features.Screenshot;

public enum OverlayPhase
{
    Selecting,
    SelectionActive,
    LongCapturing,
}

public enum RegionOverlayEnd
{
    Cancelled,
    Complete,
    CopyOnly,
    Save,
    LongCapture,
}

public sealed class RegionOverlayResult
{
    public RegionOverlayEnd End { get; init; }
    public CanvasRenderTarget? FlattenedSdr { get; init; }
    public bool Confirmed => End is not RegionOverlayEnd.Cancelled;
}
