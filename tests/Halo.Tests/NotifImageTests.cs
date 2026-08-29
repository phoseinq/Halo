using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Halo.Notifications;
using Xunit;

namespace Halo.Tests;

public class NotifImageTests : IDisposable
{
    private readonly string _dir;

    public NotifImageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "halo-notifimage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string WritePng(int w, int h)
    {
        string path = Path.Combine(_dir, $"img-{w}x{h}-{Guid.NewGuid():N}.png");
        using var b = new Bitmap(w, h);
        using (var g = Graphics.FromImage(b)) g.Clear(Color.CornflowerBlue);
        b.Save(path, ImageFormat.Png);
        return path;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPathIsNothing(string path) => Assert.Null(NotifImage.Load(path));

    [Fact]
    public void MissingFileIsNothing()
        => Assert.Null(NotifImage.Load(Path.Combine(_dir, "nope.png")));

    // a caller that names its own README gets no banner image rather than an exception on the listener
    // thread, which is the same rule every probe in this codebase follows
    [Fact]
    public void FileThatIsNotAnImageIsNothing()
    {
        string path = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(path, "this is not a png");
        Assert.Null(NotifImage.Load(path));
    }

    // the cap is checked from the file length BEFORE the bytes are read: the point is to not pull a
    // gigabyte into memory because an HTTP caller pointed at a video file
    [Fact]
    public void OversizedFileIsNothing()
    {
        string path = Path.Combine(_dir, "huge.png");
        File.WriteAllBytes(path, new byte[NotifImage.MaxBytes + 1]);
        Assert.Null(NotifImage.Load(path));
    }

    // Image.FromFile keeps the file locked for the lifetime of the bitmap, which would leave the
    // caller unable to delete or rewrite the very file it just handed us. Decoding through a byte
    // copy is the whole reason Load exists rather than a one-line FromFile.
    [Fact]
    public void SourceFileIsNotLeftLocked()
    {
        string path = WritePng(200, 120);
        using var loaded = NotifImage.Load(path);
        Assert.NotNull(loaded);
        File.Delete(path);                 // throws if the bitmap still holds the handle
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void OversizeIsScaledDownKeepingAspect()
    {
        using var loaded = NotifImage.Load(WritePng(1920, 1080));
        Assert.NotNull(loaded);
        Assert.True(loaded!.Width <= NotifImage.MaxW);
        Assert.True(loaded.Height <= NotifImage.MaxH);
        Assert.Equal(16.0 / 9.0, (double)loaded.Width / loaded.Height, 2);
    }

    [Fact]
    public void TallImageIsBoundedByHeight()
    {
        using var loaded = NotifImage.Load(WritePng(600, 1800));
        Assert.NotNull(loaded);
        Assert.True(loaded!.Height <= NotifImage.MaxH);
        Assert.True(loaded.Width <= NotifImage.MaxW);
    }

    // the banner draws the thumb at 128x72; blowing a 40px image up to 512 wide would only make it
    // blurrier, so small images are passed through at their own size
    [Fact]
    public void SmallImageIsNotUpscaled()
    {
        using var loaded = NotifImage.Load(WritePng(64, 36));
        Assert.NotNull(loaded);
        Assert.Equal(64, loaded!.Width);
        Assert.Equal(36, loaded.Height);
    }

    // premultiplied, because everything that reaches the layered surface has to be: a non-premul
    // source sprayed white garbage across the pill the last time one got through
    [Fact]
    public void ResultIsPremultiplied()
    {
        using var loaded = NotifImage.Load(WritePng(1200, 800));
        Assert.NotNull(loaded);
        Assert.Equal(PixelFormat.Format32bppPArgb, loaded!.PixelFormat);
    }
}
