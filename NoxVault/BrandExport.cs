using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoxVault;

internal static class BrandExport
{
    // Use the same vector for every Windows icon size, avoiding bitmap upscaling.
    internal static void Save(string folder)
    {
        var frames = new List<(int Size, byte[] Png)>();
        foreach (var size in new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 })
        {
            var logo = Ui.Logo(size);
            logo.Measure(new Size(size, size)); logo.Arrange(new Rect(0, 0, size, size)); logo.UpdateLayout();
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(logo);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); frames.Add((size, stream.ToArray()));
        }
        File.WriteAllBytes(Path.Combine(folder, "logo-preview.png"), frames[^1].Png);
        using var file = File.Create(Path.Combine(folder, "vault.ico")); using var writer = new BinaryWriter(file);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)frames.Count);
        int offset = 6 + 16 * frames.Count;
        foreach (var frame in frames)
        {
            writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size)); writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(frame.Png.Length); writer.Write(offset); offset += frame.Png.Length;
        }
        foreach (var frame in frames) writer.Write(frame.Png);
    }
}
