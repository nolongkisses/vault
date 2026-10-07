using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoxVault;

// vault's mark. The vector is the source of Assets/vault.ico (see Tests/BrandExport.cs); the window uses the icon itself.
internal static class Brand
{
    internal static readonly Uri IconUri = new("pack://application:,,,/Assets/vault.ico");

    internal static Image Icon(double size) => new()
    {
        Source = BitmapFrame.Create(IconUri), Width = size, Height = size, Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center,
    };

    internal static Image Logo(double size)
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Theme.Color("#141414"), new Pen(Theme.Color("#343434"), 1),
            new RectangleGeometry(new Rect(2, 2, 60, 60), 14, 14)));
        drawing.Children.Add(new GeometryDrawing(Theme.Color("#F2F2EE"), null,
            Geometry.Parse("M16,19 L24.5,19 L32,39 L39.5,19 L48,19 L36,47 L28,47 Z")));
        drawing.Freeze();
        return new Image { Source = new DrawingImage(drawing), Width = size, Height = size, Stretch = Stretch.Uniform };
    }
}
