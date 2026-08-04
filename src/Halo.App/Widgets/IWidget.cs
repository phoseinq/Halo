using System;
using System.Collections.Generic;
using System.Drawing;

namespace Halo.Widgets;

internal readonly record struct AgentNotice(string? State, DateTimeOffset? CompactedAt, string? Message)
{
    internal static AgentNotice None => new(null, null, null);
}

internal static class AgentActivity
{
    internal static long Rank(string? state, DateTimeOffset? startedAt, DateTimeOffset now)
    {
        int bucket = state switch
        {
            "working" => 5,
            "compacting" => 4,
            "waiting_input" => 3,
            "waiting" => 2,
            null or "" or "idle" => 0,
            _ => 1,
        };
        if (bucket == 0) return 0;
        long secs = startedAt is { } t ? (long)Math.Clamp((now - t).TotalSeconds, 0, 999_999) : 0;
        return bucket * 1_000_000L + secs;
    }
}

internal interface IWidget
{
    string Icon { get; }

    Bitmap? IconImage => null;

    float IconOffsetX => 0f;

    bool IsActive { get; }
    int Version { get; }

    bool Animating => false;

    void Tick() { }

    bool Sprinting => false;

    Color? Ring => null;

    float RingProgress => -1f;

    AgentNotice AgentNotice => AgentNotice.None;

    long ActivityRank => 0;

    IEnumerable<int> OwnerPids => Array.Empty<int>();

    string? RevealProcess => null;

    void DrawContent(Graphics g, int w, int h, float expandFade);

    void DrawCollapsed(Graphics g, int w, int h, float fade) { }

    IReadOnlyList<(RectangleF rect, Action<PointF> onClick)> Buttons(int w, int h);

    IReadOnlyList<(RectangleF rect, Action<PointF> onClick)> CollapsedButtons(int w, int h)
        => Array.Empty<(RectangleF, Action<PointF>)>();
}
