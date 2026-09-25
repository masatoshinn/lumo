using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lumo.Editor.Ui;
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
    private WrapPanel? _projectGrid;
    private StackPanel? _recentList;
    private StackPanel? _topTabs = null;
    private string _activeTab = "Home";

    public HomeView(Action<string?> onNewProject, Action<string> onOpenProject)
    {
        _onNewProject = onNewProject;
        _onOpenProject = onOpenProject;
        BuildUI();
    }

    private void BuildUI()
    {
        _projects = ProjectManager.GetAllProjects();

        var root = new DockPanel { Background = UiTheme.B(UiTheme.Bg) };

        var sidebar = BuildSidebar();
        DockPanel.SetDock(sidebar, Dock.Left);
        root.Children.Add(sidebar);

        var topbar = BuildTopBar();
        DockPanel.SetDock(topbar, Dock.Top);
        root.Children.Add(topbar);

        var footer = BuildFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        root.Children.Add(BuildContent());
        Content = root;
    }

    private Control BuildSidebar()
    {
        var side = new StackPanel { Width = 232, Spacing = 0, Background = UiTheme.B(UiTheme.Sidebar) };

        // Logo
        var logoRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(18, 18, 18, 22),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                UiTheme.Logo(26),
                new StackPanel
                {
                    Spacing = -2,
                    Children =
                    {
                        UiTheme.Txt("Lumo", 19, UiTheme.Text, FontWeight.Bold),
                        UiTheme.Txt("Game Engine", 10, UiTheme.Faint),
                    }
                }
            }
        };
        side.Children.Add(logoRow);

        side.Children.Add(SideLabel("Main"));
        side.Children.Add(UiTheme.NavButton(Icons.Home, "Home", true));
        side.Children.Add(UiTheme.NavButton(Icons.Folder, "Projects", false, () => _onNewProject(null)));
        side.Children.Add(UiTheme.NavButton(Icons.Scene, "Scene Editor", false, () => { if (_projects.Count > 0) _onOpenProject(_projects[0].Path); }));
        side.Children.Add(UiTheme.NavButton(Icons.Box, "Asset Library", false));
        side.Children.Add(UiTheme.NavButton(Icons.Rocket, "Build & Deploy", false));
        side.Children.Add(UiTheme.NavButton(Icons.Gear, "Settings", false));

        var sep = new Separator { Background = UiTheme.B(UiTheme.Border), Margin = new Thickness(16, 14, 16, 6), Height = 1 };
        side.Children.Add(sep);

        side.Children.Add(SideLabel("Resources"));
        side.Children.Add(UiTheme.NavButton(Icons.Book, "Documentation", false));
        side.Children.Add(UiTheme.NavButton(Icons.PlayCircle, "Tutorials", false));
        side.Children.Add(UiTheme.NavButton(Icons.Users, "Community", false));
        side.Children.Add(UiTheme.NavButton(Icons.Help, "Support", false));

        // Promo card
        var promo = new Border
        {
            Margin = new Thickness(14, 18, 14, 10),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#1b2559"), 0), new GradientStop(Color.Parse("#2a1b52"), 1) },
            },
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    UiTheme.Logo(30),
                    UiTheme.Txt("Build Worlds", 15, UiTheme.Text, FontWeight.Bold),
                    UiTheme.Txt("Not Just Games", 15, UiTheme.Text, FontWeight.Bold),
                    UiTheme.Txt("Lumo — the next generation game engine.", 11, UiTheme.Dim),
                    new Border
                    {
                        Margin = new Thickness(0, 6, 0, 0),
                        Child = UiTheme.Row(UiTheme.Logo(14), UiTheme.Txt("Lumo", 12, UiTheme.Text, FontWeight.SemiBold)),
                    }
                }
            }
        };
        side.Children.Add(promo);

        var spacer = new Border { Height = 8 };
        side.Children.Add(spacer);

        side.Children.Add(new Border
        {
            Height = 26,
            Background = UiTheme.B(UiTheme.Panel),
            Child = new DockPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 6,
                        Margin = new Thickness(14, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = UiTheme.B(UiTheme.Green), VerticalAlignment = VerticalAlignment.Center },
                            UiTheme.Txt("Online", 10, UiTheme.Green),
                        }
                    },
                    new TextBlock
                    {
                        Text = "v0.1.0 (Beta)",
                        FontSize = 10,
                        Foreground = UiTheme.B(UiTheme.Faint),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 14, 0),
                        HorizontalAlignment = HorizontalAlignment.Right,
                    }
                }
            }
        });

        return side;
    }

    private static TextBlock SideLabel(string text)
        => new()
        {
            Text = text,
            FontSize = 10,
            Foreground = UiTheme.B(UiTheme.Faint),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(24, 12, 0, 6),
            LetterSpacing = 1.2,
        };

    private Control BuildTopBar()
    {
        _topTabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

        foreach (var tab in new[] { "Home", "Projects", "Assets", "Marketplace", "Documentation" })
        {
            string icon = tab switch { "Home" => Icons.Home, "Projects" => Icons.Folder, "Assets" => Icons.Box, "Marketplace" => Icons.Grid, _ => Icons.Book };
            _topTabs.Children.Add(TopTab(icon, tab));
        }

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0),
            Children =
            {
                UiTheme.IconBtn(Icons.Bell),
                UiTheme.IconBtn(Icons.Gear),
                BuildUserChip(),
            }
        };

        var bar = new DockPanel
        {
            Height = 54,
            Background = UiTheme.B(UiTheme.Sidebar),
            Children =
            {
                right,
                new Border
                {
                    Child = _topTabs,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            }
        };
        DockPanel.SetDock(right, Dock.Right);

        return new Border { BorderBrush = UiTheme.B(UiTheme.Border), BorderThickness = new Thickness(0, 0, 0, 1), Child = bar };
    }

    private Control TopTab(string icon, string label)
    {
        bool active = label == _activeTab;
        var btn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { UiTheme.Ico(icon, 15, active ? Colors.White : UiTheme.Dim), UiTheme.Txt(label, 13, active ? Colors.White : UiTheme.Dim, active ? FontWeight.SemiBold : FontWeight.Normal) },
            },
            Padding = new Thickness(14, 7),
            Background = active ? UiTheme.B(UiTheme.Accent) : UiTheme.B(Colors.Transparent),
            Foreground = UiTheme.B(UiTheme.Dim),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.PointerEntered += (_, _) => { if (label != _activeTab) btn.Background = UiTheme.B(Color.Parse("#151d38")); };
        btn.PointerExited += (_, _) => { if (label != _activeTab) btn.Background = UiTheme.B(Colors.Transparent); };
        btn.Click += (_, _) => SwitchTab(label);
        return btn;
    }

    private void SwitchTab(string label)
    {
        _activeTab = label;
        if (_topTabs == null) return;
        _topTabs.Children.Clear();
        foreach (var tab in new[] { "Home", "Projects", "Assets", "Marketplace", "Documentation" })
        {
            string icon = tab switch { "Home" => Icons.Home, "Projects" => Icons.Folder, "Assets" => Icons.Box, "Marketplace" => Icons.Grid, _ => Icons.Book };
            _topTabs.Children.Add(TopTab(icon, tab));
        }
    }

    private Control BuildUserChip()
    {
        var avatar = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(UiTheme.Accent, 0), new GradientStop(UiTheme.Purple, 1) },
            },
            Child = UiTheme.Txt("G", 13, Colors.White, FontWeight.Bold),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var chip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Children =
            {
                avatar,
                new StackPanel
                {
                    Spacing = -1,
                    Children =
                    {
                        UiTheme.Txt("Golam Mostofa Sadhin", 12, UiTheme.Text, FontWeight.SemiBold),
                        UiTheme.Txt("Creator", 10, UiTheme.Faint),
                    }
                },
                UiTheme.Ico(Icons.ChevronDown, 14, UiTheme.Dim),
            }
        };
        return chip;
    }

    private Control BuildFooter()
    {
        return new Border
        {
            Height = 34,
            Background = UiTheme.B(UiTheme.Sidebar),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    UiTheme.Txt("Lumo Game Engine", 11, UiTheme.Faint),
                    UiTheme.Txt("•", 11, UiTheme.Faint),
                    UiTheme.Txt("Build the next generation", 11, UiTheme.Faint),
                    UiTheme.Txt("•", 11, UiTheme.Faint),
                    UiTheme.Txt("C# Powered", 11, UiTheme.Faint),
                }
            }
        };
    }

    private Control BuildContent()
    {
        var main = new StackPanel { Spacing = 22, Margin = new Thickness(26, 22, 26, 22) };
        main.Children.Add(BuildHeroRow());
        main.Children.Add(BuildProjectsSection());
        main.Children.Add(BuildRecentSection());

        var mainScroll = new ScrollViewer { Content = main, Background = UiTheme.B(UiTheme.Bg) };
        var rightRail = BuildRightRail();

        var body = new DockPanel();
        DockPanel.SetDock(rightRail, Dock.Right);
        body.Children.Add(rightRail);
        body.Children.Add(mainScroll);
        return body;
    }

    // ---------- Hero + Quick Start ----------
    private Control BuildHeroRow()
    {
        var hero = BuildHero();
        var quick = BuildQuickStartColumn();
        quick.Width = 316;

        var row = new DockPanel { Height = 258 };
        DockPanel.SetDock(quick, Dock.Right);
        row.Children.Add(quick);
        row.Children.Add(hero);
        return row;
    }

    private Control BuildHero()
    {
        var bg = new Panel { ClipToBounds = true };

        bg.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#141d3f"), 0), new GradientStop(Color.Parse("#251a4d"), 1) },
            },
        });

        // decorative rings
        bg.Children.Add(new Border
        {
            Width = 300, Height = 300,
            CornerRadius = new CornerRadius(150),
            BorderBrush = new SolidColorBrush(Color.Parse("#3a4ad044")),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 40, 0),
        });
        bg.Children.Add(new Border
        {
            Width = 180, Height = 180,
            CornerRadius = new CornerRadius(90),
            BorderBrush = new SolidColorBrush(Color.Parse("#7c5cf044")),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 100, 0),
        });

        var left = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(34, 30, 0, 30),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 560,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        UiTheme.Logo(38),
                        UiTheme.Txt("Lumo", 40, UiTheme.Text, FontWeight.Bold),
                    }
                },
                UiTheme.Txt("Create. Build. Play.", 26, UiTheme.Text, FontWeight.Bold),
                new TextBlock
                {
                    Text = "A powerful, modern game engine built for creators, dreamers and developers. Bring your imagination to life with Lumo.",
                    FontSize = 13,
                    Foreground = UiTheme.B(UiTheme.Dim),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 480,
                    FontFamily = UiTheme.Body,
                    LineHeight = 20,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Margin = new Thickness(0, 8, 0, 0),
                    Children =
                    {
                        MakeHeroBtn("Open Project", Icons.PlayCircle, true, () =>
                        {
                            if (_projects.Count > 0) _onOpenProject(_projects[0].Path);
                            else _onNewProject(null);
                        }),
                        MakeHeroBtn("Watch Tutorial", Icons.PlayCircle, false, () => { }),
                    }
                },
            }
        };
        bg.Children.Add(left);

        var tag = new TextBlock
        {
            Text = "Your World\nStarts Here",
            FontSize = 17,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#9aa8ff")),
            FontFamily = new FontFamily("Segoe Script, Segoe UI"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 26, 40, 0),
            TextAlignment = TextAlignment.Right,
        };
        bg.Children.Add(tag);

        return new Border { CornerRadius = new CornerRadius(12), Child = bg };
    }

    private static Button MakeHeroBtn(string label, string icon, bool primary, Action onClick)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { UiTheme.Ico(icon, 15, primary ? Colors.White : UiTheme.Cyan), UiTheme.Txt(label, 13, primary ? Colors.White : UiTheme.Text, FontWeight.SemiBold) },
        };
        var btn = new Button
        {
            Content = content,
            Padding = new Thickness(20, 10),
            Background = primary ? UiTheme.B(UiTheme.Accent) : UiTheme.B(Color.Parse("#101830cc")),
            Foreground = UiTheme.B(Colors.White),
            BorderBrush = primary ? UiTheme.B(UiTheme.Accent) : UiTheme.B(Color.Parse("#3a4a80")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Control BuildQuickStartColumn()
    {
        var panel = new StackPanel { Spacing = 14 };

        var qs = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#3d3ce0"), 0), new GradientStop(Color.Parse("#7c4df0"), 1) },
            },
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Children =
                        {
                            new Border
                            {
                                Width = 34, Height = 34, CornerRadius = new CornerRadius(9),
                                Background = new SolidColorBrush(Color.Parse("#ffffff2a")),
                                Child = UiTheme.Ico(Icons.Rocket, 18, Colors.White),
                            },
                            new StackPanel
                            {
                                Spacing = 2,
                                VerticalAlignment = VerticalAlignment.Center,
                                Children =
                                {
                                    UiTheme.Txt("Quick Start", 15, Colors.White, FontWeight.Bold),
                                }
                            }
                        }
                    },
                    UiTheme.Txt("Create a new project and start building your dream game.", 12, new Color(255, 235, 240, 255)),
                    MakeHeroBtn("+  New Project", Icons.Plus, true, () => _onNewProject(null)),
                }
            }
        };
        panel.Children.Add(qs);

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
            RowDefinitions = { new RowDefinition(new GridLength(1, GridUnitType.Star)), new RowDefinition(new GridLength(1, GridUnitType.Star)) },
        };

        void Add(int r, int c, string label, string icon, Action? act = null)
        {
            var cell = new Border
            {
                Background = UiTheme.B(UiTheme.Card),
                BorderBrush = UiTheme.B(UiTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 7, 7),
                Child = new StackPanel
                {
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { UiTheme.Ico(icon, 22, UiTheme.Cyan), UiTheme.Txt(label, 12, UiTheme.Text, FontWeight.Medium) },
                },
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            cell.PointerEntered += (_, _) => cell.Background = UiTheme.B(UiTheme.CardHover);
            cell.PointerExited += (_, _) => cell.Background = UiTheme.B(UiTheme.Card);
            if (act != null) cell.PointerPressed += (_, _) => act();
            Grid.SetRow(cell, r); Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        Add(0, 0, "Import Asset", Icons.Upload, () => _onNewProject(null));
        Add(0, 1, "Open Project", Icons.Folder, () => { if (_projects.Count > 0) _onOpenProject(_projects[0].Path); });
        Add(1, 0, "Documentation", Icons.Book);
        Add(1, 1, "Community", Icons.Users);

        panel.Children.Add(grid);
        return panel;
    }

    // ---------- Projects ----------
    private Control BuildProjectsSection()
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                UiTheme.Ico(Icons.Folder, 18, UiTheme.Cyan),
                UiTheme.Txt("My Projects", 17, UiTheme.Text, FontWeight.Bold),
            }
        };
        DockPanel.SetDock(title, Dock.Left);
        var viewAll = UiTheme.ActionButton("View All  →", null, () => { });
        viewAll.Padding = new Thickness(10, 4);
        viewAll.Background = UiTheme.B(Colors.Transparent);
        viewAll.BorderThickness = new Thickness(0);
        DockPanel.SetDock(viewAll, Dock.Right);
        header.Children.Add(viewAll);
        header.Children.Add(title);

        var stack = new StackPanel { Spacing = 14, Children = { header } };

        _projectGrid = new WrapPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(_projectGrid);

        RefreshProjects();
        return UiTheme.CardBorder(stack, Color.Parse("#0f1526"), 12);
    }

    private void RefreshProjects()
    {
        if (_projectGrid == null) return;
        _projectGrid.Children.Clear();

        foreach (var proj in _projects.OrderByDescending(p => p.LastModified))
            _projectGrid.Children.Add(ProjectCard(proj));

        if (_projects.Count == 0)
        {
            var empty = UiTheme.CardBorder(
                new StackPanel
                {
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 18),
                    Children =
                    {
                        UiTheme.Ico(Icons.Star, 34, UiTheme.Faint),
                        UiTheme.Txt("No projects yet", 14, UiTheme.Dim, FontWeight.Medium),
                        UiTheme.Txt("Create your first project to get started.", 12, UiTheme.Faint),
                    }
                }, Color.Parse("#0c1120"), 10);
            empty.Width = 340;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            _projectGrid.Children.Add(empty);
        }
    }

    private Control ProjectCard(ProjectInfo proj)
    {
        var accent = UiTheme.PickGradient(proj.Name);

        var thumb = new Border
        {
            Height = 116,
            CornerRadius = new CornerRadius(9, 9, 0, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(accent, 0), new GradientStop(Color.Parse("#0d1220"), 1) },
            },
            Child = new Panel
            {
                Children =
                {
                    UiTheme.Ico(Icons.Scene, 40, new Color(255, 255, 255, 40)),
                    new Border
                    {
                        Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
                        Background = new SolidColorBrush(Color.Parse("#00000066")),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = UiTheme.Ico(Icons.Play, 16, Colors.White),
                    }
                }
            }
        };

        var body = new StackPanel
        {
            Spacing = 7,
            Margin = new Thickness(13, 11, 13, 13),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { UiTheme.Txt(proj.Name, 14, UiTheme.Text, FontWeight.SemiBold) },
                },
                UiTheme.Txt(string.IsNullOrWhiteSpace(proj.Description) ? "No description" : proj.Description!, 11, UiTheme.Dim),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        UiTheme.Badge("C#", Color.Parse("#2b3a8a"), Color.Parse("#9fb4ff")),
                        UiTheme.Txt($"Updated {Ago(proj.LastModified)}", 10, UiTheme.Faint),
                    }
                },
            }
        };

        var card = new Border
        {
            Width = 258,
            Margin = new Thickness(0, 0, 14, 14),
            CornerRadius = new CornerRadius(10),
            Background = UiTheme.B(UiTheme.Card),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Children = { thumb, body } },
        };
        card.PointerEntered += (_, _) => { card.BorderBrush = UiTheme.B(accent); card.Background = UiTheme.B(UiTheme.CardHover); };
        card.PointerExited += (_, _) => { card.BorderBrush = UiTheme.B(UiTheme.Border); card.Background = UiTheme.B(UiTheme.Card); };
        card.PointerPressed += (_, _) => _onOpenProject(proj.Path);
        return card;
    }

    private static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours} hours ago";
        if (d.TotalDays < 30) return $"{(int)d.TotalDays} days ago";
        return t.ToString("MMM d, yyyy");
    }

    // ---------- Recent files ----------
    private Control BuildRecentSection()
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                UiTheme.Ico(Icons.File, 18, UiTheme.Cyan),
                UiTheme.Txt("Recent Files", 17, UiTheme.Text, FontWeight.Bold),
            }
        };
        DockPanel.SetDock(title, Dock.Left);
        var viewAll = UiTheme.ActionButton("View All  →");
        viewAll.Padding = new Thickness(10, 4);
        viewAll.Background = UiTheme.B(Colors.Transparent);
        viewAll.BorderThickness = new Thickness(0);
        DockPanel.SetDock(viewAll, Dock.Right);
        header.Children.Add(viewAll);
        header.Children.Add(title);

        _recentList = new StackPanel { Spacing = 2 };

        var files = GatherRecentFiles();
        if (files.Count == 0)
        {
            files.Add(("PlayerController.cs", "Assets/Scripts/Player/", "Edited 2 hours ago", "cs"));
            files.Add(("MainScene.scene", "Assets/Scenes/", "Edited 4 hours ago", "scene"));
            files.Add(("Environment.prefab", "Assets/Prefabs/", "Edited 6 hours ago", "prefab"));
            files.Add(("GameManager.cs", "Assets/Scripts/", "Edited 1 day ago", "cs"));
        }

        foreach (var (name, path, when, kind) in files)
            _recentList.Children.Add(RecentRow(name, path, when, kind));

        var stack = new StackPanel { Spacing = 6, Children = { header, _recentList } };
        return UiTheme.CardBorder(stack, Color.Parse("#0f1526"), 12);
    }

    private static List<(string name, string path, string when, string kind)> GatherRecentFiles()
    {
        var result = new List<(string, string, string, string)>();
        try
        {
            var root = ProjectManager.ProjectsRootPath;
            if (!Directory.Exists(root)) return result;
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .OrderByDescending(File.GetLastWriteTime)
                .Take(4);
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                var dir = Path.GetDirectoryName(f) ?? "";
                var rel = Path.GetRelativePath(root, dir).Replace('\\', '/');
                var ext = Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
                result.Add((name, rel + "/", $"Edited {Ago(File.GetLastWriteTime(f))}", ext));
            }
        }
        catch { }
        return result;
    }

    private Control RecentRow(string name, string path, string when, string kind)
    {
        Color badgeBg = kind switch
        {
            "cs" => Color.Parse("#6a3fb5"),
            "scene" => Color.Parse("#2f7fd0"),
            "prefab" => Color.Parse("#2f8f5f"),
            _ => Color.Parse("#3a4a70"),
        };

        var row = new DockPanel { Height = 40, Margin = new Thickness(6, 0) };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Children =
            {
                UiTheme.Txt(when, 11, UiTheme.Faint),
                UiTheme.Ico(Icons.Dots, 15, UiTheme.Faint),
            }
        };
        DockPanel.SetDock(right, Dock.Right);

        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Border
                {
                    Width = 26, Height = 26, CornerRadius = new CornerRadius(6),
                    Background = UiTheme.B(badgeBg),
                    Child = UiTheme.Ico(Icons.File, 14, Colors.White),
                },
                new StackPanel
                {
                    Spacing = 1,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        UiTheme.Txt(name, 12, UiTheme.Text, FontWeight.Medium),
                        UiTheme.Txt(path, 10, UiTheme.Faint),
                    }
                },
            }
        };

        row.Children.Add(right);
        row.Children.Add(left);

        var wrap = new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = row,
        };
        wrap.PointerEntered += (_, _) => wrap.Background = UiTheme.B(Color.Parse("#151d38"));
        wrap.PointerExited += (_, _) => wrap.Background = UiTheme.B(Colors.Transparent);
        return wrap;
    }

    // ---------- Right rail ----------
    private Control BuildRightRail()
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(0, 22, 26, 22), Width = 316 };

        panel.Children.Add(BuildUpdatesCard());
        panel.Children.Add(BuildSystemCard());

        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        return scroll;
    }

    private Control BuildUpdatesCard()
    {
        var stack = new StackPanel { Spacing = 12, Margin = new Thickness(16) };

        var head = new DockPanel();
        var t = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { UiTheme.Ico(Icons.Bell, 16, UiTheme.Cyan), UiTheme.Txt("Latest Updates", 14, UiTheme.Text, FontWeight.Bold) },
        };
        DockPanel.SetDock(t, Dock.Left);
        var va = UiTheme.Txt("View All →", 11, UiTheme.Cyan);
        DockPanel.SetDock(va, Dock.Right);
        head.Children.Add(va);
        head.Children.Add(t);
        stack.Children.Add(head);

        void Add(string title, string desc, string date, Color dot)
        {
            stack.Children.Add(new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = UiTheme.B(dot), VerticalAlignment = VerticalAlignment.Center },
                            UiTheme.Txt(title, 12, UiTheme.Text, FontWeight.SemiBold),
                        }
                    },
                    new TextBlock
                    {
                        Text = desc,
                        FontSize = 11,
                        Foreground = UiTheme.B(UiTheme.Dim),
                        Margin = new Thickness(16, 0, 0, 0),
                        FontFamily = UiTheme.Body,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    UiTheme.TxtAt(date, 10, UiTheme.Faint, FontWeight.Normal, HorizontalAlignment.Left, new Thickness(16, 0, 0, 0)),
                }
            });
        }

        Add("Lumo v0.1.0 (Beta) Released", "New features, performance improvements...", "Sep 20, 2026", UiTheme.Cyan);
        Add("Scene Editor Improvements", "Better workflow and UI enhancements.", "Sep 15, 2026", UiTheme.Accent);
        Add("Asset Pipeline Update", "Faster import and better compatibility.", "Sep 10, 2026", UiTheme.Orange);
        Add("C# Scripting Support", "Now fully stable with improved API.", "Sep 5, 2026", UiTheme.Purple);

        return UiTheme.CardBorder(stack);
    }

    private Control BuildSystemCard()
    {
        var stack = new StackPanel { Spacing = 10, Margin = new Thickness(16) };

        var head = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { UiTheme.Ico(Icons.Monitor, 16, UiTheme.Cyan), UiTheme.Txt("System Info", 14, UiTheme.Text, FontWeight.Bold) },
        };
        stack.Children.Add(head);

        void Row(string k, string v)
        {
            stack.Children.Add(new DockPanel
            {
                Children =
                {
                    UiTheme.TxtAt(v, 11, UiTheme.Text, FontWeight.Medium, HorizontalAlignment.Right),
                    UiTheme.Txt(k, 11, UiTheme.Dim),
                }
            });
        }

        Row("Engine Version", EngineConstants.Version);
        Row("Platform", Environment.OSVersion.Platform.ToString());
        Row("Language", "C#");
        Row("Graphics API", "OpenGL 3.3");

        stack.Children.Add(UiTheme.Divider());
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Children =
            {
                new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = UiTheme.B(UiTheme.Green) },
                UiTheme.Txt("Engine Ready", 11, UiTheme.Green, FontWeight.SemiBold),
            }
        });

        return UiTheme.CardBorder(stack);
    }
}
