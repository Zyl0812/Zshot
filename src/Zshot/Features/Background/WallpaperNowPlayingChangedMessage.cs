namespace Zshot.Features.Background;

/// <summary>
/// 文件夹随机模式下当前壁纸文件名变更消息。AppBackground 每次随机选中文件后广播，
/// 设置页据此显示 NowPlaying。FileName 为纯文件名（不含路径），null = 清空。
/// </summary>
public sealed class WallpaperNowPlayingChangedMessage
{
    public string? FileName { get; init; }
}
