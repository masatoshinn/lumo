using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Reflection;

namespace Lumo.Editor.Ui;

public static class UiTheme
{
    public static readonly Color Bg = Color.Parse("#0a0e17");
    public static readonly Color Sidebar = Color.Parse("#0d1220");
    public static readonly Color Panel = Color.Parse("#111726");
    public static readonly Color Card = Color.Parse("#141b2e");
    public static readonly Color CardHover = Color.Parse("#1a2340");
    public static readonly Color Border = Color.Parse("#1e2740");
    public static readonly Color Accent = Color.Parse("#4f5ef5");
    public static readonly Color AccentSoft = Color.Parse("#2a3399");
    public static readonly Color Purple = Color.Parse("#7c5cf0");
    public static readonly Color Text = Color.Parse("#e6eaf5");
    public static readonly Color Dim = Color.Parse("#8892ab");
    public static readonly Color Faint = Color.Parse("#5c6580");
    public static readonly Color Green = Color.Parse("#3ddc84");
    public static readonly Color Cyan = Color.Parse("#35c6d4");
    public static readonly Color Orange = Color.Parse("#e8a33d");
    public static readonly Color Red = Color.Parse("#e05555");

    public static readonly FontFamily Body = new("Segoe UI, Inter, Arial");
    public static readonly FontFamily Mono = new("Cascadia Code, Consolas, Courier New");

    public static SolidColorBrush B(Color c) => new(c);

    public static TextBlock Txt(string text, double size = 12, Color? color = null, FontWeight weight = FontWeight.Normal)
        => new() { Text = text, FontSize = size, Foreground = B(color ?? Text), FontWeight = weight, FontFamily = Body, VerticalAlignment = VerticalAlignment.Center };

    public static TextBlock TxtAt(string text, double size, Color? color, FontWeight weight, HorizontalAlignment ha, Thickness? margin = null)
    {
        var t = Txt(text, size, color, weight);
        t.HorizontalAlignment = ha;
        t.VerticalAlignment = VerticalAlignment.Center;
        if (margin.HasValue) t.Margin = margin.Value;
        return t;
    }

    public static PathIcon Ico(string path, double size = 16, Color? color = null)
        => new() { Data = Geometry.Parse(path), Width = size, Height = size, Foreground = B(color ?? Dim) };

    private static Bitmap? _logoBitmap;

    public static Bitmap LogoBitmap
    {
        get
        {
            if (_logoBitmap == null)
            {
                try
                {
                    using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lumo.Editor.Assets.lumo_logo_transparent.png");
                    if (s != null) _logoBitmap = new Bitmap(s);
                }
                catch { }
                if (_logoBitmap == null)
                {
                    var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
                    _logoBitmap = new Bitmap(new System.IO.MemoryStream(png));
                }
            }
            return _logoBitmap;
        }
    }

    public static Image Logo(double height)
        => new()
        {
            Source = LogoBitmap,
            Height = height,
            Width = Math.Round(height * 177.0 / 201.0, 1),
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    public static StackPanel Row(params Control[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    public static Border CardBorder(Control child, Color? bg = null, double radius = 10)
        => new()
        {
            Background = B(bg ?? Card),
            BorderBrush = B(Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Child = child,
        };

    public static TextBlock SectionTitle(string text)
        => new() { Text = text, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = B(Text), FontFamily = Body, Margin = new Thickness(0, 0, 0, 12) };

    public static Button NavButton(string icon, string label, bool active, Action? onClick = null)
    {
        var btn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { Ico(icon, 16, active ? Color.Parse("#8fa4ff") : Dim), Txt(label, 13, active ? Text : Dim, active ? FontWeight.SemiBold : FontWeight.Normal) },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 9),
            Margin = new Thickness(10, 1),
            Background = active ? B(Color.Parse("#1c2a6b")) : B(Colors.Transparent),
            Foreground = B(Dim),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        if (!active)
        {
            btn.PointerEntered += (_, _) => btn.Background = B(Color.Parse("#151d38"));
            btn.PointerExited += (_, _) => btn.Background = B(Colors.Transparent);
        }
        if (onClick != null) btn.Click += (_, _) => onClick();
        return btn;
    }

    public static Button ActionButton(string label, string? icon = null, Action? onClick = null, bool primary = false, double size = 13)
    {
        var children = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon != null) children.Children.Add(Ico(icon, 15, primary ? Colors.White : Dim));
        children.Children.Add(Txt(label, size, primary ? Colors.White : Text, FontWeight.Medium));

        var btn = new Button
        {
            Content = children,
            Padding = new Thickness(16, 9),
            Background = primary ? B(Accent) : B(Card),
            Foreground = B(primary ? Colors.White : Text),
            BorderBrush = primary ? B(Accent) : B(Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            FontFamily = Body,
        };
        btn.PointerEntered += (_, _) => { if (primary) btn.Background = B(Color.Parse("#6472ff")); else { btn.Background = B(CardHover); btn.BorderBrush = B(Color.Parse("#2e3a60")); } };
        btn.PointerExited += (_, _) => { if (primary) btn.Background = B(Accent); else { btn.Background = B(Card); btn.BorderBrush = B(Border); } };
        if (onClick != null) btn.Click += (_, _) => onClick();
        return btn;
    }

    public static Button IconBtn(string icon, Action? onClick = null, double size = 16, Color? color = null)
    {
        var btn = new Button
        {
            Content = Ico(icon, size, color ?? Dim),
            Padding = new Thickness(7),
            Background = B(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.PointerEntered += (_, _) => btn.Background = B(Color.Parse("#1a2340"));
        btn.PointerExited += (_, _) => btn.Background = B(Colors.Transparent);
        if (onClick != null) btn.Click += (_, _) => onClick();
        return btn;
    }

    public static Border Badge(string text, Color bg, Color fg)
        => new()
        {
            Background = B(bg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Child = Txt(text, 10, fg, FontWeight.SemiBold),
        };

    public static Border Divider(double height = 1)
        => new() { Background = B(Border), Height = height, HorizontalAlignment = HorizontalAlignment.Stretch };

    public static Color PickGradient(string name, int salt = 0)
    {
        string[] palette = { "#4f5ef5", "#7c5cf0", "#35c6d4", "#e8a33d", "#3ddc84", "#e05555", "#5b8def" };
        int h = 0;
        foreach (var ch in name) h = unchecked(h * 31 + ch);
        return Color.Parse(palette[(Math.Abs(h + salt) % palette.Length)]);
    }
}

