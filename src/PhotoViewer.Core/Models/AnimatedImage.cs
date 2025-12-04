using SkiaSharp;

namespace PhotoViewer.Core.Models;

public class AnimatedImage : IDisposable
{
    public List<SKBitmap> Frames { get; } = new();
    public List<int> Durations { get; } = new();
    public int TotalDuration => Durations.Sum();
    public int FrameCount => Frames.Count;
    public int Width => Frames.FirstOrDefault()?.Width ?? 0;
    public int Height => Frames.FirstOrDefault()?.Height ?? 0;

    public void AddFrame(SKBitmap frame, int duration)
    {
        Frames.Add(frame);
        Durations.Add(duration);
    }

    public void Dispose()
    {
        foreach (var frame in Frames)
        {
            frame.Dispose();
        }
        Frames.Clear();
        Durations.Clear();
    }
}
