using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lumo.Engine.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lumo.Editor.Views;

public class HomeView : UserControl
{
    private List<ProjectInfo> _projects = [];
    private readonly Action<string?> _onNewProject;
    private readonly Action<string> _onOpenProject;

    public HomeView(Action<string?> onNewProject, Action<string> onOpenProject)
    {
        _onNewProject = onNewProject;
        _onOpenProject = onOpenProject;
        BuildUI();
    }

    private WrapPanel? _gridPanel;

    private void BuildUI()
    {
        _projects = ProjectManager.GetAllProjects();

        var root = new DockPanel();

        // Header
        var header = new DockPanel
        {
            Height = 80,
            Background = new SolidColorBrush(Color.Parse("#1a1a2e")),
        };

        var logoArea = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(30, 0, 0, 0),
        };

        var logoText = new TextBlock
        {
            Text = "LUMO",
            Foreground = new SolidColorBrush(Color.Parse("#00d4ff")),
            FontSize = 36,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var logoSub = new TextBlock
        {
            Text = "Engine",
            Foreground = new SolidColorBrush(Color.Parse("#00d4ff")),
            FontSize = 18,
            FontWeight = FontWeight.Light,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };

        logoArea.Children.Add(logoText);
        logoArea.Children.Add(logoSub);
        DockPanel.SetDock(logoArea, Dock.Left);
        header.Children.Add(logoArea);

        var headerRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 30, 0),
        };
        DockPanel.SetDock(headerRight, Dock.Right);

        var verText = new TextBlock
        {
            Text = EngineConstants.Version,
            Foreground = new SolidColorBrush(Color.Parse("#666688")),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        headerRight.Children.Add(verText);

        header.Children.Add(headerRight);
        root.Children.Add(header);

        // Content area
        var content = new DockPanel();
        DockPanel.SetDock(content, Dock.Bottom);

        var searchRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(30, 16, 30, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var searchBox = new TextBox
        {
            Watermark = "Search projects...",
            FontSize = 13,
            Height = 36,
            Background = new SolidColorBrush(Color.Parse("#2a2a3e")),
            Foreground = new SolidColorBrush(Color.Parse("#cccccc")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3a3a55")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6),
        };
        searchBox.TextChanged += (_, _) => { };

        var newBtn = new Button
        {
            Content = "+  New Project",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(16, 8),
            Background = new SolidColorBrush(Color.Parse("#00d4ff")),
            Foreground = new SolidColorBrush(Color.Parse("#0a0a1a")),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        newBtn.Click += (_, _) => _onNewProject(null);

        searchRow.Children.Add(searchBox);
        searchRow.Children.Add(newBtn);
        DockPanel.SetDock(searchRow, Dock.Top);
        content.Children.Add(searchRow);

        // Project grid
        var scroll = new ScrollViewer
        {
            Background = new SolidColorBrush(Color.Parse("#0f0f1a")),
        };

        _gridPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(30, 10, 30, 30),
        };
        scroll.Content = _gridPanel;
        content.Children.Add(scroll);

        DockPanel.SetDock(content, Dock.Bottom);
        root.Children.Add(content);

        // Add New Project card
        var newCard = CreateProjectCard(
            "Create New Project",
            "\u2795",
            "Start a fresh project from scratch",
            Color.Parse("#1a1a3e"),
            Color.Parse("#00d4ff"),
            () => _onNewProject(null));
        _gridPanel.Children.Add(newCard);

        RefreshProjects();

        Content = root;
    }

    private void RefreshProjects()
    {
        if (_gridPanel == null) return;
        _gridPanel.Children.Clear();

        var newCard = CreateProjectCard(
            "Create New Project",
            "\u2795",
            "Start a fresh project from scratch",
            Color.Parse("#1a1a3e"),
            Color.Parse("#00d4ff"),
            () => _onNewProject(null));
        _gridPanel.Children.Add(newCard);

        foreach (var proj in _projects.OrderByDescending(p => p.LastModified))
        {
            var card = CreateProjectCard(
                proj.Name,
                "\U0001f3ae",
                proj.Description ?? "No description",
                Color.Parse("#1e1e2e"),
                Color.Parse("#555577"),
                () => _onOpenProject(proj.Path));
            _gridPanel.Children.Add(card);
        }
    }

    private Border CreateProjectCard(string title, string icon, string desc, Color bgColor, Color accentColor, Action onClick)
    {
        var panel = new StackPanel
        {
            Width = 240,
            Height = 160,
            Spacing = 8,
            Margin = new Thickness(4),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        var iconBg = new Border
        {
            Width = 48,
            Height = 48,
            Background = new SolidColorBrush(bgColor),
            CornerRadius = new CornerRadius(12),
            Child = new TextBlock
            {
                Text = icon,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Margin = new Thickness(0, 0, 0, 4),
        };

        var titleText = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.Parse("#e6edf3")),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
        };

        var descText = new TextBlock
        {
            Text = desc,
            Foreground = new SolidColorBrush(Color.Parse("#8888aa")),
            FontSize = 11,
        };

        panel.Children.Add(iconBg);
        panel.Children.Add(titleText);
        panel.Children.Add(descText);

        var card = new Border
        {
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(Color.Parse("#2a2a44")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = panel,
        };

        card.PointerEntered += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.Parse("#252540"));
            card.BorderBrush = new SolidColorBrush(accentColor);
        };
        card.PointerExited += (_, _) =>
        {
            card.Background = new SolidColorBrush(bgColor);
            card.BorderBrush = new SolidColorBrush(Color.Parse("#2a2a44"));
        };
        card.PointerPressed += (_, _) => onClick();

        return card;
    }
}
