using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoxVault;

// Renders every screen with synthetic data into PNGs (both themes) and runs the window-level checks along the way.
internal static class Renders
{
    internal static void Run(TestContext test, VaultData restored, bool includeNative)
    {
        var previewData = restored.Clone();
        previewData.Accounts[0].Website = "https://example.invalid";
        previewData.Accounts[0].Fields.Add(new SecretField { Label = "Wiederherstellungscode", Value = "Synthetic recovery" });
        previewData.Accounts.Add(new Account
        {
            Title = "Gelöschter Testaccount", Category = previewData.Categories[0],
            DeletedUtc = DateTime.UtcNow
        });
        var preview = new MainWindow(Path.Combine(test.Folder, "preview.nox"), previewData);
        Shell(test, preview, "");
        SettingsPages(test, preview);
        DialogChecks(test, preview);
        var gate = new MainWindow(Path.Combine(test.Folder, "lifecycle.nox"), designMode: true);
        preview.TestQuickFillIsolation();
        test.Check(true, "Riot picker, unlock and notices leave the main window hidden");
        Save(gate, 1180, 780, Path.Combine(test.OutDir, "welcome-preview.png"));
        Save(gate, 980, 630, Path.Combine(test.OutDir, "welcome-compact.png"));
        Theme.Apply("Hell");
        Shell(test, preview, "-light");
        Save(gate, 1180, 780, Path.Combine(test.OutDir, "welcome-preview-light.png"));
        var settings = preview.CreateSettingsWindow();
        preview.PushDialog(settings);
        Save(preview, 1180, 780, Path.Combine(test.OutDir, "settings-preview-light.png"));
        preview.PopDialog(settings);
        preview.TestFeatureDialogs(name => Save(preview, 980, 660, Path.Combine(test.OutDir, name + "-light.png")));
        Theme.Apply("Dunkel");
        test.Check(true, "Both themes render every screen from the same tokens");
        Lifecycle(test, gate, includeNative);
    }

    static void Shell(TestContext test, MainWindow preview, string suffix)
    {
        Save(preview, 1180, 780, Path.Combine(test.OutDir, "vault-preview" + suffix + ".png"));
        Save(preview, 980, 660, Path.Combine(test.OutDir, "vault-compact" + suffix + ".png"));
        Save(preview, 1920, 1080, Path.Combine(test.OutDir, "vault-fullscreen" + suffix + ".png"));
        var fillPreview = preview.CreateFillPreview();
        Save(fillPreview, 420, 420, Path.Combine(test.OutDir, "autofill-picker" + suffix + ".png"));
        fillPreview.Close();
        test.Check(true, "Autofill picker renders with synthetic account and concealed password");
    }

    static void SettingsPages(TestContext test, MainWindow preview)
    {
        new System.Windows.Interop.WindowInteropHelper(preview).EnsureHandle();
        var settings = preview.CreateSettingsWindow();
        preview.PushDialog(settings);
        Save(preview, 980, 660, Path.Combine(test.OutDir, "settings-preview.png"));
        var view = preview.SettingsPreview!;
        test.Check(view.PageCount == 5 && !view.IsCompact, "Settings expose five sections with wide navigation");
        for (int page = 0; page < view.PageCount; page++)
        {
            view.SelectPage(page);
            Save(preview, 1180, 780, Path.Combine(test.OutDir, $"settings-page-{page}.png"));
            test.Check(view.SelectedPage == page && view.ActualWidth > 0, "Settings navigation selects page " + page);
            Save(preview, 980, 660, Path.Combine(test.OutDir, $"settings-page-{page}-150.png"), 1.5);
            test.Check(view.IsCompact, "Settings use compact navigation at 150 percent on page " + page);
            Save(preview, 980, 660, Path.Combine(test.OutDir, $"settings-page-{page}-200.png"), 2);
            test.Check(view.IsCompact && view.ActualHeight > 0, "Settings remain reachable at 200 percent on page " + page);
            var scroll = view.Children.OfType<ScrollViewer>().Single();
            scroll.ScrollToEnd();
            scroll.UpdateLayout();
            test.Check(scroll.ScrollableHeight == 0 || scroll.VerticalOffset >= scroll.ScrollableHeight - 1,
                "Settings content scrolls to its last action at 200 percent on page " + page);
        }
        view.SelectPage(0);
        Save(preview, 980, 660, Path.Combine(test.OutDir, "settings-preview.png"));
        preview.PopDialog(settings);
    }

    static void DialogChecks(TestContext test, MainWindow preview)
    {
        var forced = InAppDialog.Create(preview, "Forced close test", new StackPanel());
        bool closeRequested = false;
        forced.Closing += (_, args) => { closeRequested = true; args.Cancel = true; };
        preview.Dispatcher.BeginInvoke(new Action(() => forced.Close(true)));
        forced.ShowDialog();
        test.Check(!closeRequested, "Security closure bypasses unsaved-change confirmations");
        preview.TestFeatureDialogs(name => Save(preview, 980, 660, Path.Combine(test.OutDir, name + ".png")));
        test.Check(true, "Editor, generator, trash and local password check render and close safely");
        NestedDialogs(test, preview);
    }

    static void NestedDialogs(TestContext test, MainWindow preview)
    {
        int baseLayerCount = ((Grid)preview.Content).Children.Count;
        var outer = InAppDialog.Create(preview, "Test overlay", new StackPanel());
        Exception? failure = null;
        preview.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                test.Check(preview.OwnedWindows.Count == 0, "In-app dialog creates no native window");
                var inner = InAppDialog.Create(outer, "Nested overlay", new StackPanel());
                preview.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { test.Check(!outer.IsEnabled && inner.IsEnabled, "Nested dialog disables underlying dialog"); }
                    catch (Exception ex) { failure = ex; } // Rethrown once the nested frame has ended.
                    finally { inner.DialogResult = true; }
                }));
                test.Check(inner.ShowDialog() == true && outer.IsEnabled, "Nested dialog returns result and restores parent");
            }
            catch (Exception ex) { failure = ex; } // Rethrown once the nested frame has ended.
            finally { outer.Close(); }
        }));
        outer.ShowDialog();
        if (failure != null) throw failure;
        test.Check(((Grid)preview.Content).Children.Count == baseLayerCount && ((Grid)preview.Content).Children[0].IsEnabled,
            "Closing overlays restores main view");
    }

    static void Lifecycle(TestContext test, MainWindow gate, bool includeNative)
    {
        if (includeNative) { gate.TestHotkeys(); test.Check(true, "Native shortcut registration, replacement and conflict tests"); }
        gate.TestLifecycle();
        test.Check(true, "UI save, lock, unlock, rebuild and search lifecycle");
        gate.TestSecurityOptions();
        test.Check(true, "Configured idle limits and password concealment work through the UI lifecycle");
        test.Check(true, "Locked session file cannot prevent lock or restore in a new instance");
        test.Check(true, "Search batches input, selects text for keyboard access and cancels on lock");
        Save(gate, 1180, 780, Path.Combine(test.OutDir, "unlock-preview.png"));
        test.Check(true, "WPF layout rendered at standard and minimum size");
        if (includeNative)
            File.WriteAllText(Path.Combine(test.OutDir, "shortcut-check.json"),
                JsonSerializer.Serialize(Native.ProbeHotkeys(), new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static void Save(ContentControl window, int width, int height, string path, double scale = 1)
    {
        window.Width = width;
        window.Height = height;
        var content = (FrameworkElement)window.Content;
        content.LayoutTransform = new ScaleTransform(scale, scale);
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}

internal static class BrandExport
{
    // Use the same vector for every Windows icon size, avoiding bitmap upscaling.
    internal static void Save(string folder)
    {
        var frames = new List<(int Size, byte[] Png)>();
        foreach (var size in new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 })
        {
            var logo = Brand.Logo(size);
            logo.Measure(new Size(size, size));
            logo.Arrange(new Rect(0, 0, size, size));
            logo.UpdateLayout();
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(logo);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            frames.Add((size, stream.ToArray()));
        }
        File.WriteAllBytes(Path.Combine(folder, "logo-preview.png"), frames[^1].Png);
        using var file = File.Create(Path.Combine(folder, "vault.ico"));
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)frames.Count);
        int offset = 6 + 16 * frames.Count;
        foreach (var frame in frames)
        {
            writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
            writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(frame.Png.Length);
            writer.Write(offset);
            offset += frame.Png.Length;
        }
        foreach (var frame in frames) writer.Write(frame.Png);
    }
}
