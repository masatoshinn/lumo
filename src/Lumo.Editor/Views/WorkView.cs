using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Lumo.Editor.Rendering;
using Lumo.Editor.Ui;
using Lumo.Engine.Assets;
using Lumo.Engine.Core;
using Lumo.Engine.Rendering.Abstractions;
using Lumo.Engine.Scene;
using Lumo.Engine.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;
using LumoInput = Lumo.Engine.Input.InputState;
using LumoKey = Lumo.Engine.Input.Key;
using SceneType = Lumo.Engine.Scene.Scene;

namespace Lumo.Editor.Views;

public class WorkView : UserControl
{
    private readonly LumoEngine _engine;
    private SceneType _scene;
    private ProjectInfo _project;
    private readonly Action<string?> _onNavigateHome;
    private readonly Action _onClose;

    private TextBlock _statusText = null!;
    private TextBlock _fpsText = null!;
    private TextBlock _entityCountText = null!;
    private StackPanel _hierarchyList = null!;
    private StackPanel _inspectorContent = null!;
    private TextBlock _consoleLog = null!;
    private ContentControl _bottomHost = null!;
    private StackPanel _bottomTabsPanel = null!;
    private StackPanel _sidebarNav = null!;
    private Panel _viewportPanel = null!;
    private Button _playButton = null!;
    private GlViewport? _glViewport;
    private SoftwareViewport? _softwareViewport;
    private Entity? _selectedEntity;
    private bool _isPlaying;
    private ViewportMode _viewMode = ViewportMode.Scene;
    private string _activeSidebar = "Scene";
    private string _bottomTab = "Project";
    private string _selectedFolder = "";
    private string _hierarchySearch = "";
    private string _activeTool = "Move";
    private bool _showGrid = true;
    private bool _showGizmos = true;
    private bool _showStats = true;
    private readonly List<string> _logMessages = [];
    private string? _playSnapshot;
    private string? _currentScriptPath;
    private TextBox? _scriptEditor;
    private TextBlock? _scriptErrorText;
    private TextBlock? _profilerStats = null!;
    private LumoInput? _playInput;
    private readonly ScriptHost _scriptHost = new();

    public WorkView(ProjectInfo project, Action<string?> onNavigateHome, Action onClose)
    {
        _project = project;
        _onNavigateHome = onNavigateHome;
        _onClose = onClose;
        Focusable = true;
        _engine = new LumoEngine();
        _engine.Initialize();
        _scene = new SceneType { Name = project.Name };

        ScriptHost.MessageLogged += OnScriptMessage;

        LoadProject();
        BuildUI();
        StartFpsTimer();
        Log("Editor loaded.");
    }

    private void OnScriptMessage(string message) => Log(message);

    private void LoadProject()
    {
        var sceneFile = Path.Combine(_project.Path, "Scenes", "scene.json");
        if (File.Exists(sceneFile))
        {
            try { _scene = Scene.Load(sceneFile); } catch { }
        }
        _selectedEntity = _scene.AllEntities.FirstOrDefault();
    }

    private void SaveProject()
    {
        var scenesDir = Path.Combine(_project.Path, "Scenes");
        Directory.CreateDirectory(scenesDir);
        _scene.Save(Path.Combine(scenesDir, "scene.json"));
        ProjectManager.UpdateLastModified(_project.Path);
    }

    private void StartFpsTimer()
    {
        var timer = new Timer(500);
        timer.Elapsed += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _fpsText.Text = $"FPS: {_engine.Time.FPS:F0}";
                _entityCountText.Text = $"{_scene.AllEntities.Count} objects";
                if (_profilerStats != null)
                {
                    _profilerStats.Text =
                        $"FPS: {_engine.Time.FPS:F0}\n" +
                        $"Frame: {_engine.Time.DeltaTime * 1000f:F2} ms\n" +
                        $"Entities: {_scene.AllEntities.Count}\n" +
                        $"Scripts: {_scriptHost.InstanceCount}\n" +
                        $"Playing: {(_isPlaying ? "Yes" : "No")}";
                }
            });
        timer.Start();

        var renderTimer = new Timer(16.0);
        renderTimer.Elapsed += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                float dt = _engine.Tick();

                if (_isPlaying)
                {
                    _playInput?.BeginFrame();
                    _scriptHost.Update(dt, (float)_engine.Time.ElapsedTime);
                }

                if (_glViewport != null && _glViewport.IsVisible && _glViewport.IsReady)
                {
                    _glViewport.SetSceneObjects(_scene);
                    _glViewport.RenderFrame();
                }
                else if (_softwareViewport != null && _softwareViewport.IsVisible)
                {
                    _softwareViewport.SetScene(_scene);
                    _softwareViewport.InvalidateVisual();
                }
            }, Avalonia.Threading.DispatcherPriority.Render);
        renderTimer.Start();
    }

    // ================= Play mode =================
    private void TogglePlay()
    {
        if (!_isPlaying) StartPlay();
        else StopPlay();
        RefreshPlayButton();
    }

    private void StartPlay()
    {
        // Snapshot scene so Stop can restore editor state.
        try { _playSnapshot = _scene.Serialize(); }
        catch { _playSnapshot = null; }

        var scriptsDir = Path.Combine(_project.Path, "Scripts");
        Directory.CreateDirectory(scriptsDir);
        var sources = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToList();

        if (sources.Count > 0)
        {
            if (!_scriptHost.Compile(sources))
            {
                foreach (var err in _scriptHost.Errors)
                    Log($"CS: {err}");
                Log($"Script compile failed ({_scriptHost.Errors.Count} error(s)). Fix and retry.");
                _playSnapshot = null;
                return;
            }

            _playInput = new LumoInput();
            _scriptHost.Input = _playInput;
            _scriptHost.Bind(_scene);

            if (_scriptHost.Errors.Count > 0)
            {
                foreach (var err in _scriptHost.Errors)
                    Log(err);
            }
        }

        _engine.Start();
        _scriptHost.Start((float)_engine.Time.ElapsedTime);
        _isPlaying = true;
        Log(sources.Count > 0
            ? $"Playing — {sources.Count} script file(s), {_scriptHost.InstanceCount} instance(s)."
            : "Playing (no scripts in Scripts/).");
    }

    private void StopPlay()
    {
        _scriptHost.Stop();
        _engine.Stop();
        _isPlaying = false;

        // Restore scene to pre-play state.
        if (_playSnapshot != null)
        {
            try
            {
                var data = JsonSerializer.Deserialize<SceneData>(_playSnapshot,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data != null)
                {
                    _scene = SceneSerializer.Deserialize(data);
                    if (_selectedEntity != null)
                        _selectedEntity = _scene.FindById(_selectedEntity.Id);
                    RefreshHierarchy();
                    RefreshInspector();
                }
            }
            catch (Exception ex) { Log($"Restore failed: {ex.Message}"); }
            _playSnapshot = null;
        }

        _playInput = null;
        _scriptHost.Input = null;
        Log("Stopped — scene restored.");
    }

    private void RefreshPlayButton()
    {
        if (_playButton == null) return;
        _playButton.Content = UiTheme.ActionButton(
            _isPlaying ? "Stop" : "Play",
            _isPlaying ? Icons.Stop : Icons.Play,
            null, primary: true).Content;
        _playButton.Background = UiTheme.B(_isPlaying ? UiTheme.Red : UiTheme.Accent);
    }

    // ================= UI =================
    private void BuildUI()
    {
        _consoleLog = new TextBlock { Text = "", Foreground = UiTheme.B(UiTheme.Green), FontFamily = UiTheme.Mono, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 8) };

        var root = new DockPanel { Background = UiTheme.B(UiTheme.Bg) };

        var topbar = BuildTopBar();
        DockPanel.SetDock(topbar, Dock.Top);
        root.Children.Add(topbar);

        var sidebar = BuildSidebar();
        DockPanel.SetDock(sidebar, Dock.Left);
        root.Children.Add(sidebar);

        var status = BuildStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        root.Children.Add(BuildEditorArea());
        Content = root;
    }

    // ---------- Top bar ----------
    private Control BuildTopBar()
    {
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0),
            Children =
            {
                UiTheme.Logo(22),
                UiTheme.IconBtn(Icons.Home, () => _onNavigateHome?.Invoke(null)),
                new Border
                {
                    Background = UiTheme.B(UiTheme.Card),
                    BorderBrush = UiTheme.B(UiTheme.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(11, 6),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            UiTheme.Ico(Icons.Cube, 14, UiTheme.Cyan),
                            UiTheme.Txt(_project.Name, 12, UiTheme.Text, FontWeight.Medium),
                            UiTheme.Ico(Icons.ChevronDown, 13, UiTheme.Dim),
                        }
                    },
                },
            }
        };

        _playButton = UiTheme.ActionButton("Play", Icons.Play, () => TogglePlay(), primary: true);
        _playButton.Padding = new Thickness(18, 7);
        _playButton.HorizontalAlignment = HorizontalAlignment.Left;

        var platform = new Border
        {
            Background = UiTheme.B(UiTheme.Card),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(11, 7),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children = { UiTheme.Ico(Icons.Monitor, 14, UiTheme.Dim), UiTheme.Txt("Windows (x64)", 12, UiTheme.Text), UiTheme.Ico(Icons.ChevronDown, 13, UiTheme.Dim) },
            },
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            Children =
            {
                _playButton,
                platform,
                UiTheme.IconBtn(Icons.Rocket, BuildProject),
                UiTheme.IconBtn(Icons.Gear, OpenSettings),
                UiTheme.IconBtn(Icons.Help, () => Log($"{EngineConstants.Name} v{EngineConstants.Version}")),
                BuildUserChip(),
            }
        };
        DockPanel.SetDock(right, Dock.Right);

        var bar = new DockPanel { Height = 52, Background = UiTheme.B(UiTheme.Sidebar), Children = { right, left } };
        return new Border { BorderBrush = UiTheme.B(UiTheme.Border), BorderThickness = new Thickness(0, 0, 0, 1), Child = bar };
    }

    private Control BuildUserChip()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            Margin = new Thickness(8, 0, 0, 0),
            Children =
            {
                new Border
                {
                    Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(UiTheme.Accent, 0), new GradientStop(UiTheme.Purple, 1) },
                    },
                    Child = UiTheme.Txt("G", 13, Colors.White, FontWeight.Bold),
                },
                new StackPanel
                {
                    Spacing = -1,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        UiTheme.Txt("Golam Mostofa Sadhin", 12, UiTheme.Text, FontWeight.SemiBold),
                        UiTheme.Txt("Creator", 10, UiTheme.Faint),
                    }
                },
                UiTheme.Ico(Icons.ChevronDown, 14, UiTheme.Dim),
            }
        };
    }

    // ---------- Sidebar ----------
    private Control BuildSidebar()
    {
        var side = new StackPanel { Width = 226, Background = UiTheme.B(UiTheme.Sidebar) };

        side.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(16, 14, 16, 18),
            Children =
            {
                UiTheme.Logo(24),
                new StackPanel { Spacing = -2, Children = { UiTheme.Txt("Lumo", 17, UiTheme.Text, FontWeight.Bold), UiTheme.Txt("Game Engine", 9, UiTheme.Faint) } },
            }
        });

        side.Children.Add(SideLabel("Project"));
        _sidebarNav = new StackPanel();
        RebuildSidebarNav();
        side.Children.Add(_sidebarNav);

        side.Children.Add(SideLabel("Quick Actions"));
        var actions = new StackPanel();
        foreach (var (icon, name, act) in new (string, string, Action)[]
        {
            (Icons.FilePlus, "New Scene", NewScene),
            (Icons.Upload, "Import Asset", () => _ = ImportAssetAsync()),
            (Icons.Code, "Add Script", AddScript),
            (Icons.Rocket, "Build Project", BuildProject),
        })
        {
            var b = new Border
            {
                Margin = new Thickness(10, 3),
                Padding = new Thickness(12, 9),
                CornerRadius = new CornerRadius(8),
                Background = UiTheme.B(UiTheme.Card),
                BorderBrush = UiTheme.B(UiTheme.Border),
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { UiTheme.Ico(icon, 15, UiTheme.Cyan), UiTheme.Txt(name, 12, UiTheme.Text) } },
            };
            b.PointerEntered += (_, _) => b.Background = UiTheme.B(UiTheme.CardHover);
            b.PointerExited += (_, _) => b.Background = UiTheme.B(UiTheme.Card);
            b.PointerPressed += (_, _) => act();
            actions.Children.Add(b);
        }
        side.Children.Add(actions);

        // promo
        side.Children.Add(new Border
        {
            Margin = new Thickness(12, 16, 12, 8),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 16),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#1b2559"), 0), new GradientStop(Color.Parse("#2a1b52"), 1) },
            },
            Child = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    UiTheme.Logo(26),
                    UiTheme.TxtAt("Build Worlds", 13, UiTheme.Text, FontWeight.Bold, HorizontalAlignment.Center),
                    UiTheme.TxtAt("Create Stories", 13, UiTheme.Text, FontWeight.Bold, HorizontalAlignment.Center),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0),
                        Children = { UiTheme.Logo(13), UiTheme.Txt("Lumo", 12, UiTheme.Text, FontWeight.SemiBold) },
                    },
                }
            }
        });

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
        => new() { Text = text, FontSize = 10, Foreground = UiTheme.B(UiTheme.Faint), FontWeight = FontWeight.SemiBold, Margin = new Thickness(22, 10, 0, 5), LetterSpacing = 1.2 };

    private Control SideBtn(string icon, string label)
    {
        bool active = label == _activeSidebar;
        var btn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { UiTheme.Ico(icon, 15, active ? Color.Parse("#8fa4ff") : UiTheme.Dim), UiTheme.Txt(label, 12, active ? UiTheme.Text : UiTheme.Dim, active ? FontWeight.SemiBold : FontWeight.Normal) },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 8),
            Margin = new Thickness(10, 1),
            Background = active ? UiTheme.B(Color.Parse("#1c2a6b")) : UiTheme.B(Colors.Transparent),
            Foreground = UiTheme.B(UiTheme.Dim),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        if (!active)
        {
            btn.PointerEntered += (_, _) => btn.Background = UiTheme.B(Color.Parse("#151d38"));
            btn.PointerExited += (_, _) => btn.Background = UiTheme.B(Colors.Transparent);
        }
        btn.Click += (_, _) => SidebarNavigate(label);
        return btn;
    }

    private void RebuildSidebarNav()
    {
        if (_sidebarNav == null) return;
        _sidebarNav.Children.Clear();
        foreach (var (icon, name) in new[]
        {
            (Icons.Dashboard, "Overview"), (Icons.Scene, "Scene"), (Icons.Box, "Game Objects"),
            (Icons.Folder, "Assets"), (Icons.Code, "Scripts"), (Icons.Rocket, "Build & Run"), (Icons.Gear, "Settings"),
        })
            _sidebarNav.Children.Add(SideBtn(icon, name));
    }

    private void SidebarNavigate(string label)
    {
        _activeSidebar = label;
        RebuildSidebarNav();

        switch (label)
        {
            case "Assets":
                _bottomTab = "Project";
                _selectedFolder = Path.Combine(_project.Path, "Assets");
                RebuildBottom();
                break;
            case "Scripts":
                _bottomTab = "Scripts";
                RebuildBottom();
                break;
            case "Build & Run":
                BuildProject();
                break;
            case "Settings":
                OpenSettings();
                break;
            case "Game Objects":
                _bottomTab = "Project";
                RebuildBottom();
                Log($"{_scene.AllEntities.Count} game objects in scene.");
                break;
            case "Scene":
                _bottomTab = "Console";
                RebuildBottom();
                break;
            case "Overview":
                _onNavigateHome?.Invoke(null);
                break;
        }
    }

    // ---------- Editor area ----------
    private Control BuildEditorArea()
    {
        // Scene tab strip
        var sceneTab = new Border
        {
            Background = UiTheme.B(UiTheme.Panel),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1, 1, 1, 0),
            CornerRadius = new CornerRadius(7, 7, 0, 0),
            Padding = new Thickness(12, 7),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    UiTheme.Ico(Icons.Scene, 13, UiTheme.Cyan),
                    UiTheme.Txt($"{_project.Name}.scene", 12, UiTheme.Text, FontWeight.Medium),
                    UiTheme.Ico(Icons.Close, 13, UiTheme.Faint),
                }
            },
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        var plusTab = new Button
        {
            Content = UiTheme.Ico(Icons.Plus, 14, UiTheme.Dim),
            Background = UiTheme.B(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(9, 5),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        var tabBar = new Border
        {
            Background = UiTheme.B(UiTheme.Bg),
            Padding = new Thickness(10, 6, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Children = { sceneTab, plusTab } },
        };

        // content: viewport | hierarchy | inspector
        var viewport = BuildViewport();
        var bottom = BuildBottomPanel();
        var hierarchy = BuildHierarchyPanel();
        var inspector = BuildInspectorPanel();

        hierarchy.Width = 262;
        inspector.Width = 318;

        var leftCol = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
                new RowDefinition(new GridLength(250)),
            },
            Children = { viewport, bottom },
        };
        Grid.SetRow(viewport, 0);
        Grid.SetRow(bottom, 1);

        var center = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Auto)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Auto)),
            },
            Children = { leftCol, hierarchy, inspector },
        };
        Grid.SetColumn(leftCol, 0);
        Grid.SetColumn(hierarchy, 1);
        Grid.SetColumn(inspector, 2);

        var inner = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(new GridLength(1, GridUnitType.Star)) } };
        Grid.SetRow(tabBar, 0);
        Grid.SetRow(center, 1);
        inner.Children.Add(tabBar);
        inner.Children.Add(center);

        return inner;
    }

    // ---------- Viewport ----------
    private Control BuildViewport()
    {
        _glViewport = new GlViewport();
        _softwareViewport = new SoftwareViewport();
        _softwareViewport.Mode = _viewMode;

        _viewportPanel = new Panel { Background = UiTheme.B(Color.Parse("#0b1020")) };

        if (!_glViewport.IsReady)
        {
            _viewportPanel.Children.Add(_softwareViewport);
            _glViewport.IsVisible = false;
        }
        else
        {
            _viewportPanel.Children.Add(_glViewport);
            _softwareViewport.IsVisible = false;
        }

        _viewportPanel.Children.Add(BuildViewportOverlay());

        _viewportPanel.AttachedToVisualTree += (_, _) =>
            System.Threading.Tasks.Task.Delay(250).ContinueWith(_ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_glViewport!.IsReady)
                    {
                        _softwareViewport!.IsVisible = false;
                        _glViewport.IsVisible = true;
                        Log("OpenGL viewport active.");
                    }
                    else
                    {
                        _softwareViewport!.IsVisible = true;
                        _glViewport.IsVisible = false;
                        Log("OpenGL unavailable — software renderer active.");
                    }
                }));

        return _viewportPanel;
    }

    private void SetViewMode(ViewportMode mode)
    {
        if (_viewMode == mode) return;
        _viewMode = mode;
        if (_softwareViewport != null)
        {
            _softwareViewport.Mode = mode;
            _softwareViewport.InvalidateVisual();
        }
        Log($"View: {mode switch { ViewportMode.Game => "Game", ViewportMode.Mode2D => "2D", _ => "Scene" }}");
        InvalidateOverlay();
    }

    private Control BuildViewportOverlay()
    {
        // top-left: Scene / Game / 2D toggle
        var segInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        void SegBtn(string label, ViewportMode mode)
        {
            bool on = _viewMode == mode;
            var b = new Button
            {
                Content = UiTheme.Txt(label, 11, on ? Colors.White : UiTheme.Dim, on ? FontWeight.SemiBold : FontWeight.Normal),
                Background = on ? UiTheme.B(UiTheme.Accent) : UiTheme.B(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(13, 5),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            b.Click += (_, _) => SetViewMode(mode);
            segInner.Children.Add(b);
        }
        SegBtn("Scene", ViewportMode.Scene);
        SegBtn("Game", ViewportMode.Game);
        SegBtn("2D", ViewportMode.Mode2D);

        var segHost = new Border { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12, 12, 0, 0), Background = UiTheme.B(Color.Parse("#e610172a")), CornerRadius = new CornerRadius(8), Padding = new Thickness(3), Child = segInner };

        // left tool strip
        var tools = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        foreach (var (ic, tool) in new[]
        {
            (Icons.MoveTool, "Move"), (Icons.Cube, "Rotate"), (Icons.Grid, "Scale"),
            (Icons.Box, "Rect"), (Icons.Dots, "More"),
        })
        {
            string t = tool;
            bool activeTool = t == _activeTool;
            var btn = UiTheme.IconBtn(ic, () => { _activeTool = t; Log($"Tool: {t}"); InvalidateOverlay(); }, 17,
                activeTool ? UiTheme.Cyan : null);
            tools.Children.Add(btn);
        }
        var toolsHost = new Border { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12, 62, 0, 0), Background = UiTheme.B(Color.Parse("#e610172a")), CornerRadius = new CornerRadius(9), Padding = new Thickness(5), Child = tools };

        // top-right icon cluster
        var rightIcons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        rightIcons.Children.Add(UiTheme.IconBtn(Icons.Camera, CreateCamera, 15));
        rightIcons.Children.Add(UiTheme.IconBtn(Icons.MoveTool, () => { _activeTool = "Move"; Log("Tool: Move"); InvalidateOverlay(); }, 15, _activeTool == "Move" ? UiTheme.Cyan : null));
        rightIcons.Children.Add(UiTheme.IconBtn(Icons.Gear, OpenSettings, 15));
        rightIcons.Children.Add(UiTheme.IconBtn(Icons.Bulb, () =>
        {
            _showGizmos = !_showGizmos;
            if (_softwareViewport != null) { _softwareViewport.ShowGizmos = _showGizmos; _softwareViewport.InvalidateVisual(); }
            Log($"Gizmos: {(_showGizmos ? "on" : "off")}");
            InvalidateOverlay();
        }, 15, _showGizmos ? UiTheme.Orange : null));
        rightIcons.Children.Add(UiTheme.IconBtn(Icons.Grid, () =>
        {
            _showGrid = !_showGrid;
            if (_softwareViewport != null) { _softwareViewport.ShowGrid = _showGrid; _softwareViewport.InvalidateVisual(); }
            Log($"Grid: {(_showGrid ? "on" : "off")}");
            InvalidateOverlay();
        }, 15, _showGrid ? UiTheme.Cyan : null));
        rightIcons.Children.Add(UiTheme.IconBtn(Icons.Monitor, () => { _showStats = !_showStats; Log($"Stats overlay: {(_showStats ? "on" : "off")}"); InvalidateOverlay(); }, 15, _showStats ? UiTheme.Cyan : null));
        var rightHost = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0), Background = UiTheme.B(Color.Parse("#e610172a")), CornerRadius = new CornerRadius(8), Padding = new Thickness(5), Child = rightIcons };

        // bottom-left projection chip (click toggles perspective/ortho in 3D)
        string projLabel = _viewMode switch
        {
            ViewportMode.Mode2D => "Orthographic",
            ViewportMode.Game => "Game Camera",
            _ => "Perspective",
        };
        var persp = new Border
        {
            Background = UiTheme.B(Color.Parse("#10172acc")),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(11, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(12, 0, 0, 12),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { UiTheme.Ico(Icons.Grid, 13, UiTheme.Cyan), UiTheme.Txt(projLabel, 11, UiTheme.Text), UiTheme.Ico(Icons.ChevronDown, 12, UiTheme.Dim) } },
        };

        // bottom-right hint
        var hint = new TextBlock
        {
            Text = _viewMode == ViewportMode.Mode2D ? "LMB/MMB: Pan  |  Scroll: Zoom" : "LMB: Orbit  |  MMB: Pan  |  Scroll: Zoom",
            FontSize = 10,
            Foreground = UiTheme.B(UiTheme.Faint),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 14, 14),
            IsVisible = _showStats,
        };

        // Root panel has no background, so empty areas pass clicks to the viewport.
        return new Panel { Children = { segHost, toolsHost, rightHost, persp, hint } };
    }

    private void InvalidateOverlay()
    {
        if (_viewportPanel == null) return;
        int last = _viewportPanel.Children.Count - 1;
        if (last < 0) return;
        _viewportPanel.Children.RemoveAt(last);
        _viewportPanel.Children.Add(BuildViewportOverlay());
    }

    // ---------- Hierarchy ----------
    private Control BuildHierarchyPanel()
    {
        _hierarchyList = new StackPanel { Spacing = 1 };
        var scroll = new ScrollViewer { Content = _hierarchyList, Background = UiTheme.B(UiTheme.Panel) };

        var header = new Border
        {
            Height = 34,
            Background = UiTheme.B(UiTheme.Panel),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0),
            Child = new DockPanel
            {
                Children =
                {
                    UiTheme.IconBtn(Icons.Plus, () => CreateEntity("Empty Actor"), 14),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = { UiTheme.Ico(Icons.Cube, 15, UiTheme.Cyan), UiTheme.Txt("Hierarchy", 13, UiTheme.Text, FontWeight.SemiBold) },
                    },
                }
            },
        };
        DockPanel.SetDock(header, Dock.Right);

        var search = new TextBox
        {
            Watermark = "Search...",
            FontSize = 11,
            Height = 28,
            Margin = new Thickness(10, 8, 10, 6),
            Background = UiTheme.B(UiTheme.Bg),
            Foreground = UiTheme.B(UiTheme.Text),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 4),
        };
        search.TextChanged += (_, _) =>
        {
            _hierarchySearch = search.Text ?? "";
            RefreshHierarchy();
        };

        var panel = new DockPanel { Background = UiTheme.B(UiTheme.Panel) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(search, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(search);
        panel.Children.Add(scroll);

        RefreshHierarchy();
        return panel;
    }

    private void RefreshHierarchy()
    {
        _hierarchyList.Children.Clear();
        foreach (var entity in _scene.AllEntities)
        {
            if (entity.Parent != null) continue;
            if (_hierarchySearch.Length > 0 &&
                !entity.Name.Contains(_hierarchySearch, StringComparison.OrdinalIgnoreCase))
                continue;
            _hierarchyList.Children.Add(HierItem(entity, 0));
            foreach (var child in entity.Children)
                _hierarchyList.Children.Add(HierItem(child, 1));
        }
    }

    private static string EntityIcon(Entity e) => e switch
    {
        { Camera: not null } => Icons.Camera,
        { Light: not null } => Icons.Bulb,
        { MeshRenderer: not null } => Icons.Cube,
        _ => Icons.Box,
    };

    private Border HierItem(Entity entity, int indent)
    {
        bool sel = _selectedEntity?.Id == entity.Id;
        var name = UiTheme.Txt(entity.Name, 12, sel ? Colors.White : UiTheme.Text, sel ? FontWeight.SemiBold : FontWeight.Normal);
        name.TextTrimming = TextTrimming.CharacterEllipsis;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8 + indent * 14, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                UiTheme.Ico(Icons.ChevronDown, 11, UiTheme.Faint),
                UiTheme.Ico(EntityIcon(entity), 14, sel ? UiTheme.Cyan : UiTheme.Dim),
                name,
            },
        };

        var eye = UiTheme.IconBtn(Icons.Eye, () =>
        {
            if (entity.MeshRenderer != null)
            {
                entity.MeshRenderer.IsVisible = !entity.MeshRenderer.IsVisible;
                Log($"{entity.Name}: {(entity.MeshRenderer.IsVisible ? "visible" : "hidden")}");
            }
        }, 14);
        DockPanel.SetDock(eye, Dock.Right);

        var wrap = new DockPanel { Children = { eye, row } };

        var bg = new Border
        {
            Background = sel ? UiTheme.B(Color.Parse("#1c2a6b")) : UiTheme.B(Colors.Transparent),
            Child = wrap,
            Padding = new Thickness(3, 6),
            Margin = new Thickness(5, 0),
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        if (!sel)
        {
            bg.PointerEntered += (_, _) => bg.Background = UiTheme.B(Color.Parse("#151d38"));
            bg.PointerExited += (_, _) => bg.Background = UiTheme.B(Colors.Transparent);
        }
        bg.PointerPressed += (_, _) =>
        {
            _selectedEntity = entity;
            RefreshHierarchy();
            RefreshInspector();
        };
        return bg;
    }

    // ---------- Inspector ----------
    private Control BuildInspectorPanel()
    {
        _inspectorContent = new StackPanel { Spacing = 0 };
        var scroll = new ScrollViewer { Content = _inspectorContent, Background = UiTheme.B(UiTheme.Panel) };

        var header = new Border
        {
            Height = 34,
            Background = UiTheme.B(UiTheme.Panel),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { UiTheme.Ico(Icons.Gear, 15, UiTheme.Cyan), UiTheme.Txt("Inspector", 13, UiTheme.Text, FontWeight.SemiBold) },
            }
        };

        var panel = new DockPanel { Background = UiTheme.B(UiTheme.Panel) };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(scroll);

        RefreshInspector();
        return panel;
    }

    private void RefreshInspector()
    {
        _inspectorContent.Children.Clear();
        if (_selectedEntity == null)
        {
            _inspectorContent.Children.Add(new StackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(16, 40),
                Children =
                {
                    UiTheme.Ico(Icons.Box, 34, UiTheme.Faint),
                    UiTheme.TxtAt("No actor selected", 13, UiTheme.Dim, FontWeight.Medium, HorizontalAlignment.Center),
                    UiTheme.TxtAt("Select an actor in the Hierarchy.", 11, UiTheme.Faint, FontWeight.Normal, HorizontalAlignment.Center),
                }
            });
            return;
        }

        var e = _selectedEntity;

        // name card
        _inspectorContent.Children.Add(new Border
        {
            Background = UiTheme.B(Color.Parse("#161d33")),
            Padding = new Thickness(14, 12),
            Margin = new Thickness(10, 10, 10, 8),
            CornerRadius = new CornerRadius(9),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Children =
                        {
                            UiTheme.Ico(EntityIcon(e), 20, UiTheme.Cyan),
                            UiTheme.Txt(e.Name, 15, UiTheme.Text, FontWeight.Bold),
                        }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            MiniChip("Tag", "Player"),
                            MiniChip("Layer", "Default"),
                        }
                    },
                }
            }
        });

        // Transform
        var t = InsSection("Transform");
        var eul = e.Transform.GetEulerAngles();
        Vec3Row(t, "Position", e.Transform.Position.X, e.Transform.Position.Y, e.Transform.Position.Z);
        Vec3Row(t, "Rotation", eul.X, eul.Y, eul.Z);
        Vec3Row(t, "Scale", e.Transform.Scale.X, e.Transform.Scale.Y, e.Transform.Scale.Z);
        _inspectorContent.Children.Add(t);

        if (e.MeshRenderer != null)
        {
            var s = InsSection("Static Mesh");
            InsRow(s, "Mesh", e.MeshRenderer.MeshName ?? "None");
            InsRow(s, "Visible", e.MeshRenderer.IsVisible ? "Yes" : "No");
            _inspectorContent.Children.Add(s);
        }
        if (e.Light != null)
        {
            var s = InsSection("Light");
            InsRow(s, "Type", e.Light.LightType.ToString());
            InsRow(s, "Intensity", e.Light.Intensity.ToString("F2"));
            _inspectorContent.Children.Add(s);
        }
        if (e.Camera != null)
        {
            var s = InsSection("Camera");
            InsRow(s, "Primary", e.Camera.IsPrimary ? "Yes" : "No");
            InsRow(s, "FOV", e.Camera.FieldOfView + "\u00b0");
            InsRow(s, "Clip", $"{e.Camera.NearPlane} — {e.Camera.FarPlane}");
            _inspectorContent.Children.Add(s);
        }

        // actions
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 14, 12, 20),
            Children =
            {
                ActionSmall("+ Add Component", ShowAddComponentMenu, true),
                ActionSmall("Duplicate", DuplicateSelected),
                ActionSmall("Delete", DeleteSelected),
            }
        };
        _inspectorContent.Children.Add(actions);
    }

    private static Border MiniChip(string label, string value)
        => new()
        {
            Background = UiTheme.B(UiTheme.Card),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 4),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children = { UiTheme.Txt(label, 10, UiTheme.Faint), UiTheme.Txt(value, 11, UiTheme.Text) },
            },
        };

    private static StackPanel InsSection(string title)
    {
        var body = new StackPanel { Background = UiTheme.B(UiTheme.Panel), Margin = new Thickness(0, 0, 0, 6) };
        body.Children.Add(new Border
        {
            Background = UiTheme.B(Color.Parse("#161d33")),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(6, 0, 6, 6),
            Padding = new Thickness(9, 7),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { UiTheme.Ico(Icons.ChevronDown, 12, UiTheme.Dim), UiTheme.Txt(title.ToUpperInvariant(), 11, UiTheme.Dim, FontWeight.SemiBold) },
            }
        });
        return body;
    }

    private static void InsRow(StackPanel section, string label, string value)
    {
        section.Children.Add(new DockPanel
        {
            Margin = new Thickness(12, 3),
            Children =
            {
                UiTheme.TxtAt(value, 11, UiTheme.Text, FontWeight.Medium, HorizontalAlignment.Right),
                UiTheme.Txt(label, 11, UiTheme.Dim),
            }
        });
    }

    private static void Vec3Row(StackPanel section, string label, float x, float y, float z)
    {
        section.Children.Add(UiTheme.TxtAt(label, 11, UiTheme.Dim, FontWeight.Normal, HorizontalAlignment.Left, new Thickness(12, 6, 0, 3)));
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
            Margin = new Thickness(12, 0, 12, 4),
        };
        void Add(int col, string axis, float val, Color c)
        {
            var cell = new Border
            {
                Background = UiTheme.B(UiTheme.Bg),
                BorderBrush = UiTheme.B(UiTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 4),
                Margin = new Thickness(0, 0, 5, 0),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 7,
                    Children = { UiTheme.Txt(axis, 10, c, FontWeight.Bold), UiTheme.Txt(val.ToString("F2"), 11, UiTheme.Text) },
                },
            };
            Grid.SetColumn(cell, col);
            grid.Children.Add(cell);
        }
        Add(0, "X", x, Color.Parse("#e06666"));
        Add(1, "Y", y, Color.Parse("#66c266"));
        Add(2, "Z", z, Color.Parse("#6699e6"));
        section.Children.Add(grid);
    }

    private static Button ActionSmall(string label, Action act, bool primary = false)
    {
        var b = new Button
        {
            Content = UiTheme.Txt(label, 11, primary ? Colors.White : UiTheme.Text, FontWeight.Medium),
            Background = UiTheme.B(primary ? UiTheme.Accent : UiTheme.Card),
            Foreground = UiTheme.B(primary ? Colors.White : UiTheme.Text),
            BorderBrush = UiTheme.B(primary ? UiTheme.Accent : UiTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        b.Click += (_, _) => act();
        return b;
    }

    // ---------- Bottom panel ----------
    private Control BuildBottomPanel()
    {
        _bottomTabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        RebuildBottomTabs();

        var clear = UiTheme.ActionButton("Clear", null, () => { _logMessages.Clear(); _consoleLog.Text = ""; });
        clear.Padding = new Thickness(11, 4);
        DockPanel.SetDock(clear, Dock.Right);

        var head = new Border
        {
            Background = UiTheme.B(Color.Parse("#161d33")),
            Height = 32,
            Padding = new Thickness(8, 0),
            Child = new DockPanel { Children = { clear, _bottomTabsPanel } },
        };
        DockPanel.SetDock(head, Dock.Top);

        _bottomHost = new ContentControl { Content = BuildProjectBrowser() };

        var panel = new DockPanel { Background = UiTheme.B(UiTheme.Panel) };
        panel.Children.Add(head);
        panel.Children.Add(_bottomHost);
        return panel;
    }

    private Control BottomTab(string name)
    {
        bool active = name == _bottomTab;
        var b = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    UiTheme.Ico(name switch { "Project" => Icons.Folder, "Console" => Icons.Terminal, "Scripts" => Icons.Code, "Animation" => Icons.PlayCircle, _ => Icons.Dashboard }, 13, active ? UiTheme.Cyan : UiTheme.Faint),
                    UiTheme.Txt(name, 11, active ? UiTheme.Text : UiTheme.Faint, active ? FontWeight.SemiBold : FontWeight.Normal),
                }
            },
            Background = UiTheme.B(active ? UiTheme.Panel : Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 5),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        b.Click += (_, _) => { _bottomTab = name; RebuildBottom(); };
        return b;
    }

    private void RebuildBottom()
    {
        if (_bottomHost == null) return;
        _bottomHost.Content = _bottomTab switch
        {
            "Console" => BuildConsolePanel(),
            "Scripts" => BuildScriptsPanel(),
            "Animation" => BuildAnimationPanel(),
            "Profiler" => BuildProfilerPanel(),
            _ => BuildProjectBrowser(),
        };
        RebuildBottomTabs();
    }

    private void RebuildBottomTabs()
    {
        if (_bottomTabsPanel == null) return;
        _bottomTabsPanel.Children.Clear();
        foreach (var name in new[] { "Project", "Console", "Scripts", "Animation", "Profiler" })
            _bottomTabsPanel.Children.Add(BottomTab(name));
    }

    private Control BuildProjectBrowser()
    {
        // folder tree
        var folders = new StackPanel { Spacing = 1 };
        var root = _project.Path;
        string[] dirs = Directory.Exists(root)
            ? Directory.GetDirectories(root).Where(d => !d.EndsWith("bin") && !d.EndsWith("obj")).OrderBy(Path.GetFileName).ToArray()
            : [];
        if (dirs.Length == 0 && !string.IsNullOrEmpty(_selectedFolder) == false)
        {
            // show project root as single node
            folders.Children.Add(FolderRow("Assets", root, Path.GetFileName(root) == Path.GetFileName(_selectedFolder)));
        }
        foreach (var d in dirs)
            folders.Children.Add(FolderRow(Path.GetFileName(d), d, _selectedFolder == d));

        var folderScroll = new ScrollViewer { Content = folders, Width = 190, Background = UiTheme.B(UiTheme.Bg) };

        // files grid
        var current = string.IsNullOrEmpty(_selectedFolder) ? root : _selectedFolder;
        string[] files = Directory.Exists(current) ? Directory.GetFiles(current).OrderBy(Path.GetFileName).ToArray() : Array.Empty<string>();
        var grid = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12) };
        foreach (var f in files)
            grid.Children.Add(FileCard(f));

        if (!files.Any())
            grid.Children.Add(UiTheme.Txt("No assets in this folder.", 11, UiTheme.Faint));

        var crumb = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(14, 10, 0, 4),
            Children =
            {
                UiTheme.Ico(Icons.FolderOutline, 14, UiTheme.Cyan),
                UiTheme.Txt("Assets", 11, UiTheme.Cyan),
                UiTheme.Ico(Icons.ChevronRight, 11, UiTheme.Faint),
                UiTheme.Txt(string.IsNullOrEmpty(_selectedFolder) ? "Root" : Path.GetFileName(_selectedFolder), 11, UiTheme.Text),
            }
        };

        var filesPanel = new DockPanel();
        DockPanel.SetDock(crumb, Dock.Top);
        filesPanel.Children.Add(crumb);
        filesPanel.Children.Add(new ScrollViewer { Content = grid, Background = UiTheme.B(UiTheme.Bg) });

        var split = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(190)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) } };
        Grid.SetColumn(folderScroll, 0);
        Grid.SetColumn(filesPanel, 1);
        split.Children.Add(folderScroll);
        split.Children.Add(filesPanel);
        var splitLine = new Border { Background = UiTheme.B(UiTheme.Border), Width = 1, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(splitLine, 1);
        split.Children.Add(splitLine);

        return split;
    }

    private Control FolderRow(string name, string path, bool selected)
    {
        var row = new Border
        {
            Background = selected ? UiTheme.B(Color.Parse("#1c2a6b")) : UiTheme.B(Colors.Transparent),
            Padding = new Thickness(12, 7),
            Margin = new Thickness(5, 0),
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { UiTheme.Ico(Icons.FolderOutline, 14, selected ? UiTheme.Cyan : UiTheme.Dim), UiTheme.Txt(name, 11, selected ? UiTheme.Text : UiTheme.Dim) },
            },
        };
        if (!selected)
        {
            row.PointerEntered += (_, _) => row.Background = UiTheme.B(Color.Parse("#151d38"));
            row.PointerExited += (_, _) => row.Background = UiTheme.B(Colors.Transparent);
        }
        row.PointerPressed += (_, _) => { _selectedFolder = path; RebuildBottom(); };
        return row;
    }

    private Control FileCard(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        Color bg = ext switch
        {
            ".cs" => Color.Parse("#6a3fb5"),
            ".json" => Color.Parse("#2f7fd0"),
            ".obj" => Color.Parse("#2f8f5f"),
            ".png" or ".jpg" => Color.Parse("#b57f3f"),
            _ => Color.Parse("#3a4a70"),
        };

        var card = new Border
        {
            Width = 104,
            Margin = new Thickness(0, 0, 10, 10),
            CornerRadius = new CornerRadius(8),
            Background = UiTheme.B(UiTheme.Card),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Spacing = 7,
                Children =
                {
                    new Border
                    {
                        Height = 54,
                        CornerRadius = new CornerRadius(6),
                        Background = UiTheme.B(Color.Parse("#0d1220")),
                        Child = UiTheme.Ico(ext == ".cs" ? Icons.Code : Icons.File, 22, bg),
                    },
                    new TextBlock
                    {
                        Text = name,
                        FontSize = 10,
                        Foreground = UiTheme.B(UiTheme.Text),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontFamily = UiTheme.Body,
                    },
                }
            },
        };
        card.PointerPressed += (_, _) =>
        {
            if (ext == ".cs") OpenScript(path);
            else if (ext == ".obj") ImportObjFile(path);
            else Log($"Opened {name}");
        };
        return card;
    }

    private Control BuildConsolePanel()
    {
        return new ScrollViewer { Content = _consoleLog, Background = UiTheme.B(UiTheme.Bg) };
    }

    // ---------- Scripts panel ----------
    private string ScriptsDir => Path.Combine(_project.Path, "Scripts");

    private Control BuildScriptsPanel()
    {
        Directory.CreateDirectory(ScriptsDir);
        var files = Directory.GetFiles(ScriptsDir, "*.cs").OrderBy(Path.GetFileName).ToArray();

        // left: script file list
        var list = new StackPanel { Spacing = 2 };
        var newBtn = UiTheme.ActionButton("New Script", Icons.Plus, AddScript);
        newBtn.Padding = new Thickness(10, 5);
        newBtn.Margin = new Thickness(8, 8, 8, 4);
        list.Children.Add(newBtn);

        foreach (var f in files)
        {
            string path = f;
            string name = Path.GetFileName(f);
            bool sel = _currentScriptPath == path;
            var row = new Border
            {
                Background = sel ? UiTheme.B(Color.Parse("#1c2a6b")) : UiTheme.B(Colors.Transparent),
                Padding = new Thickness(12, 6),
                Margin = new Thickness(5, 0),
                CornerRadius = new CornerRadius(6),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { UiTheme.Ico(Icons.Code, 13, UiTheme.Purple), UiTheme.Txt(name, 11, sel ? Colors.White : UiTheme.Text) } },
            };
            if (!sel)
            {
                row.PointerEntered += (_, _) => row.Background = UiTheme.B(Color.Parse("#151d38"));
                row.PointerExited += (_, _) => row.Background = UiTheme.B(Colors.Transparent);
            }
            row.PointerPressed += (_, _) => OpenScript(path);
            list.Children.Add(row);
        }

        var listScroll = new ScrollViewer { Content = list, Width = 190, Background = UiTheme.B(UiTheme.Bg) };

        // right: editor
        var editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = UiTheme.Mono,
            FontSize = 12,
            Background = UiTheme.B(UiTheme.Bg),
            Foreground = UiTheme.B(UiTheme.Text),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12),
        };
        editor.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        editor.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);

        string? scriptPath = _currentScriptPath;
        if (scriptPath != null && File.Exists(scriptPath))
            editor.Text = File.ReadAllText(scriptPath);
        else
            editor.Text = "// Select or create a script.\n// Scripts compile when you press Play.\n";
        _scriptEditor = editor;

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 10, 12, 0) };
        var saveBtn = UiTheme.ActionButton("Save", Icons.Save, () =>
        {
            if (_currentScriptPath == null) { Log("No script open."); return; }
            File.WriteAllText(_currentScriptPath, editor.Text);
            Log($"Saved {Path.GetFileName(_currentScriptPath)}");
        });
        saveBtn.Padding = new Thickness(12, 6);
        var compileBtn = UiTheme.ActionButton("Compile", Icons.Code, () =>
        {
            if (_currentScriptPath != null) File.WriteAllText(_currentScriptPath, editor.Text);
            CompileScriptsNow();
        });
        compileBtn.Padding = new Thickness(12, 6);
        toolbar.Children.Add(saveBtn);
        toolbar.Children.Add(compileBtn);
        if (_currentScriptPath != null)
            toolbar.Children.Add(UiTheme.Txt(Path.GetFileName(_currentScriptPath), 11, UiTheme.Dim));

        _scriptErrorText = UiTheme.Txt("", 11, UiTheme.Red);
        _scriptErrorText.Margin = new Thickness(12, 6, 12, 0);
        _scriptErrorText.TextWrapping = TextWrapping.Wrap;
        _scriptErrorText.IsVisible = false;

        var right = new StackPanel();
        right.Children.Add(toolbar);
        right.Children.Add(_scriptErrorText);
        right.Children.Add(new Border
        {
            Margin = new Thickness(12, 8),
            Background = UiTheme.B(UiTheme.Bg),
            BorderBrush = UiTheme.B(UiTheme.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Child = new ScrollViewer { Content = editor, MaxHeight = 130 },
        });

        var split = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(190)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) } };
        Grid.SetColumn(listScroll, 0);
        Grid.SetColumn(right, 1);
        split.Children.Add(listScroll);
        split.Children.Add(right);
        var line = new Border { Background = UiTheme.B(UiTheme.Border), Width = 1, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(line, 1);
        split.Children.Add(line);
        return split;
    }

    private void OpenScript(string path)
    {
        _currentScriptPath = path;
        _bottomTab = "Scripts";
        RebuildBottom();
    }

    private void AddScript()
    {
        Directory.CreateDirectory(ScriptsDir);
        int n = 1;
        string path;
        do { path = Path.Combine(ScriptsDir, $"Script{n}.cs"); n++; } while (File.Exists(path));

        string cls = Path.GetFileNameWithoutExtension(path);
        File.WriteAllText(path, $$"""
            using Lumo.Engine.Scripting;

            public class {{cls}} : LumoScript
            {
                public override void OnStart()
                {
                    Log("{{cls}} started");
                }

                public override void OnUpdate(float dt)
                {
                    // Your game logic here, e.g.:
                    // Position += new System.Numerics.Vector3(dt, 0, 0);
                }
            }
            """);

        OpenScript(path);
        Log($"Created {Path.GetFileName(path)}");
    }

    private void CompileScriptsNow()
    {
        Directory.CreateDirectory(ScriptsDir);
        var sources = Directory.GetFiles(ScriptsDir, "*.cs").Select(File.ReadAllText).ToList();
        if (sources.Count == 0) { Log("No scripts to compile."); return; }

        if (_scriptHost.Compile(sources))
        {
            if (_scriptErrorText != null) { _scriptErrorText.IsVisible = false; _scriptErrorText.Text = ""; }
            Log($"Compiled {sources.Count} script(s) OK.");
        }
        else
        {
            var msg = string.Join("\n", _scriptHost.Errors.Take(6));
            if (_scriptErrorText != null) { _scriptErrorText.Text = msg; _scriptErrorText.IsVisible = true; }
            foreach (var e in _scriptHost.Errors) Log($"CS: {e}");
        }
    }

    private void AttachScriptToSelected(string className)
    {
        if (_selectedEntity == null) { Log("Select an entity first."); return; }
        _selectedEntity.Scripts ??= new Lumo.Engine.Scripting.ScriptComponent();
        if (!_selectedEntity.Scripts.ScriptNames.Contains(className))
            _selectedEntity.Scripts.ScriptNames.Add(className);
        RefreshInspector();
        Log($"Attached {className} to {_selectedEntity.Name}");
    }

    // ---------- Animation / Profiler ----------
    private Control BuildAnimationPanel()
    {
        return new ScrollViewer
        {
            Background = UiTheme.B(UiTheme.Bg),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    UiTheme.Txt("Animation", 13, UiTheme.Text, FontWeight.SemiBold),
                    UiTheme.Txt("No animation clips in this project yet.", 11, UiTheme.Faint),
                }
            }
        };
    }

    private Control BuildProfilerPanel()
    {
        _profilerStats = UiTheme.Txt("FPS: 0", 12, UiTheme.Green);
        return new ScrollViewer
        {
            Background = UiTheme.B(UiTheme.Bg),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    UiTheme.Txt("Profiler", 13, UiTheme.Text, FontWeight.SemiBold),
                    _profilerStats,
                }
            }
        };
    }

    // ---------- Actions ----------
    private void NewScene()
    {
        if (_isPlaying) { Log("Stop play mode first."); return; }
        _scene = new SceneType { Name = _project.Name };
        _selectedEntity = null;
        RefreshHierarchy();
        RefreshInspector();
        SaveProject();
        Log("New scene created.");
    }

    private void BuildProject()
    {
        Log($"Building {_project.Name} (Windows x64)...");
        try
        {
            Directory.CreateDirectory(ScriptsDir);
            var sources = Directory.GetFiles(ScriptsDir, "*.cs").Select(File.ReadAllText).ToList();
            if (sources.Count > 0 && !_scriptHost.Compile(sources))
            {
                Log($"Build failed: {_scriptHost.Errors.Count} script error(s).");
                foreach (var e in _scriptHost.Errors.Take(3)) Log($"CS: {e}");
                return;
            }
            SaveProject();
            Log($"Build succeeded: {sources.Count} script(s), {_scene.AllEntities.Count} entity(ies).");
        }
        catch (Exception ex) { Log($"Build failed: {ex.Message}"); }
    }

    private void OpenSettings()
    {
        Log($"Settings — {EngineConstants.Name} v{EngineConstants.Version} | Renderer: {(_glViewport != null && _glViewport.IsReady ? "OpenGL" : "Software")} | Project: {_project.Path}");
    }

    private void CompileScriptsSafe()
    {
        try { CompileScriptsNow(); }
        catch (Exception ex) { Log($"Compile error: {ex.Message}"); }
    }

    // ---------- Status bar ----------
    private Control BuildStatusBar()
    {
        _fpsText = UiTheme.Txt("FPS: 0", 11, UiTheme.Dim);
        _entityCountText = UiTheme.Txt($"{_scene.AllEntities.Count} objects", 11, UiTheme.Dim);
        _statusText = UiTheme.Txt("Ready", 11, UiTheme.Dim);

        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0),
            Children = { UiTheme.Txt("Lumo Game Engine", 11, UiTheme.Faint), UiTheme.Txt("•", 11, UiTheme.Faint), UiTheme.Txt("Build the next generation", 11, UiTheme.Faint) },
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            Children =
            {
                _statusText,
                _fpsText,
                _entityCountText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = UiTheme.B(UiTheme.Green) },
                        UiTheme.Txt("Engine Ready", 11, UiTheme.Green, FontWeight.SemiBold),
                    }
                },
            }
        };
        DockPanel.SetDock(right, Dock.Right);

        var bar = new DockPanel { Height = 30, Background = UiTheme.B(UiTheme.Sidebar), Children = { right, left } };
        return new Border { BorderBrush = UiTheme.B(UiTheme.Border), BorderThickness = new Thickness(0, 1, 0, 0), Child = bar };
    }

    // ---------- Actions ----------
    private void CreateEntity(string name)
    {
        var entity = _scene.CreateEntity(name);
        _selectedEntity = entity;
        RefreshHierarchy();
        RefreshInspector();
        Log($"Created: {name}");
    }

    private void CreateEntityWithMesh(string name, string mesh)
    {
        var entity = _scene.CreateEntity(name);
        entity.MeshRenderer = new MeshRendererComponent { MeshName = mesh };
        _selectedEntity = entity;
        RefreshHierarchy();
        RefreshInspector();
        Log($"Created {name} with {mesh} mesh.");
    }

    private void CreateCamera()
    {
        var e = _scene.CreateEntity("Camera");
        e.Camera = new CameraComponent { IsPrimary = false };
        e.Transform.Position = new System.Numerics.Vector3(0, 1, 5);
        _selectedEntity = e;
        RefreshHierarchy();
        RefreshInspector();
        Log("Created Camera.");
    }

    private void CreateLight()
    {
        var e = _scene.CreateEntity("Directional Light");
        e.Light = new LightComponent { LightType = LightType.Directional, Intensity = 1.0f };
        e.Transform.SetRotationFromEuler(-45, 0, 0);
        _selectedEntity = e;
        RefreshHierarchy();
        RefreshInspector();
        Log("Created Directional Light.");
    }

    private void DuplicateSelected()
    {
        if (_selectedEntity == null) { Log("Nothing to duplicate."); return; }
        var copy = _scene.CreateEntity($"{_selectedEntity.Name} (Copy)");
        copy.Transform.Position = _selectedEntity.Transform.Position + new System.Numerics.Vector3(1, 0, 0);
        _selectedEntity = copy;
        RefreshHierarchy();
        RefreshInspector();
        Log($"Duplicated: {copy.Name}");
    }

    private void DeleteSelected()
    {
        if (_selectedEntity == null) { Log("Nothing to delete."); return; }
        var name = _selectedEntity.Name;
        _scene.DestroyEntity(_selectedEntity);
        _selectedEntity = null;
        RefreshHierarchy();
        RefreshInspector();
        Log($"Deleted: {name}");
    }

    private void Log(string message)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");
        _logMessages.Add($"[{ts}] {message}");
        if (_consoleLog != null) _consoleLog.Text = string.Join("\n", _logMessages);
        if (_statusText != null) _statusText.Text = message;
    }

    private void ExportSceneAsObj()
    {
        try
        {
            var sb = new StringBuilder();
            int vertexOffset = 0;
            sb.AppendLine("# Lumo Engine OBJ Export");
            sb.AppendLine($"# Objects: {_scene.AllEntities.Count}");
            sb.AppendLine();

            foreach (var entity in _scene.AllEntities)
            {
                if (entity.MeshRenderer == null || entity.Transform == null) continue;
                var pos = entity.Transform.Position;
                var scale = entity.Transform.Scale;
                float s = scale.X;

                float[,] cubeVerts = {
                    {-0.5f,-0.5f,-0.5f}, {0.5f,-0.5f,-0.5f}, {0.5f,0.5f,-0.5f}, {-0.5f,0.5f,-0.5f},
                    {-0.5f,-0.5f,0.5f}, {0.5f,-0.5f,0.5f}, {0.5f,0.5f,0.5f}, {-0.5f,0.5f,0.5f}
                };
                sb.AppendLine($"o {entity.Name}");
                for (int i = 0; i < 8; i++)
                    sb.AppendLine($"v {cubeVerts[i,0]*s+pos.X} {cubeVerts[i,1]*s+pos.Y} {cubeVerts[i,2]*s+pos.Z}");
                sb.AppendLine("usemtl Cube");
                for (int i = 0; i < 6; i++)
                {
                    int b = vertexOffset + i * 4 + 1;
                    sb.AppendLine($"f {b} {b+1} {b+2} {b+3}");
                }
                vertexOffset += 8;
            }

            var savePath = Path.Combine(_project.Path, "Scenes", "scene.obj");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            File.WriteAllText(savePath, sb.ToString());
            Log($"Exported scene to {savePath}");
        }
        catch (Exception ex) { Log($"Export failed: {ex.Message}"); }
    }

    private async System.Threading.Tasks.Task ImportAssetAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) { Log("No top level for file dialog."); return; }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Asset",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("3D Models") { Patterns = ["*.obj"] },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] },
                ],
            });

            if (files.Count == 0) { Log("Import cancelled."); return; }
            var path = files[0].TryGetLocalPath();
            if (path == null) { Log("Could not resolve file path."); return; }

            if (Path.GetExtension(path).ToLowerInvariant() != ".obj")
            {
                Log($"Unsupported format: {Path.GetExtension(path)} (OBJ supported).");
                return;
            }

            ImportObjFile(path);
        }
        catch (Exception ex) { Log($"Import failed: {ex.Message}"); }
    }

    private void ImportObjFile(string path)
    {
        try
        {
            var mesh = ObjImporter.Load(path);
            MeshLibrary.Register(mesh);

            var entity = _scene.CreateEntity(mesh.Name);
            entity.MeshRenderer = new MeshRendererComponent { MeshName = mesh.Name };

            _selectedEntity = entity;
            RefreshHierarchy();
            RefreshInspector();
            SaveProject();
            Log($"Imported {mesh.Name}: {mesh.VertexCount} verts, {mesh.TriangleCount} tris.");
        }
        catch (Exception ex) { Log($"OBJ import failed: {ex.Message}"); }
    }

    // ---------- Keyboard (play mode) ----------
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_isPlaying || _playInput == null) return;
        var key = MapKey(e.Key);
        if (key != LumoKey.Unknown) { _playInput.KeyPressed(key); e.Handled = true; }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_playInput == null) return;
        var key = MapKey(e.Key);
        if (key != LumoKey.Unknown) { _playInput.KeyReleased(key); e.Handled = true; }
    }

    private static LumoKey MapKey(Key key) => key switch
    {
        Key.W => LumoKey.W, Key.A => LumoKey.A, Key.S => LumoKey.S, Key.D => LumoKey.D,
        Key.Q => LumoKey.Q, Key.E => LumoKey.E, Key.R => LumoKey.R, Key.F => LumoKey.F,
        Key.Z => LumoKey.Z, Key.X => LumoKey.X, Key.C => LumoKey.C, Key.V => LumoKey.V,
        Key.B => LumoKey.B, Key.N => LumoKey.N, Key.M => LumoKey.M, Key.P => LumoKey.P,
        Key.G => LumoKey.G, Key.H => LumoKey.H, Key.J => LumoKey.J, Key.K => LumoKey.K,
        Key.L => LumoKey.L, Key.Y => LumoKey.Y, Key.T => LumoKey.T, Key.U => LumoKey.U,
        Key.I => LumoKey.I, Key.O => LumoKey.O,
        Key.Space => LumoKey.Space, Key.Escape => LumoKey.Escape, Key.Enter => LumoKey.Enter,
        Key.Tab => LumoKey.Tab, Key.Back => LumoKey.Backspace,
        Key.Left => LumoKey.Left, Key.Right => LumoKey.Right, Key.Up => LumoKey.Up, Key.Down => LumoKey.Down,
        Key.LeftShift => LumoKey.LeftShift, Key.LeftCtrl => LumoKey.LeftControl, Key.LeftAlt => LumoKey.LeftAlt,
        _ => LumoKey.Unknown,
    };

    // ---------- Add component ----------
    private void ShowAddComponentMenu()
    {
        if (_selectedEntity == null) { Log("Select an entity first."); return; }

        var menu = new MenuFlyout { Placement = PlacementMode.Bottom };
        void Item(string label, Action act)
        {
            var mi = new MenuItem { Header = label };
            mi.Click += (_, _) => act();
            menu.Items.Add(mi);
        }

        var e = _selectedEntity;
        if (e.MeshRenderer == null) Item("Static Mesh", () => { e.MeshRenderer = new MeshRendererComponent { MeshName = "Cube" }; RefreshInspector(); RefreshHierarchy(); Log("Added Static Mesh (Cube)."); });
        if (e.Camera == null) Item("Camera", () => { e.Camera = new CameraComponent { IsPrimary = _scene.AllEntities.All(x => x.Camera == null) }; RefreshInspector(); RefreshHierarchy(); Log("Added Camera."); });
        if (e.Light == null) Item("Light (Directional)", () => { e.Light = new LightComponent { LightType = LightType.Directional }; RefreshInspector(); RefreshHierarchy(); Log("Added Light."); });
        if (e.SpriteRenderer == null) Item("Sprite Renderer", () => { e.SpriteRenderer = new SpriteRendererComponent(); RefreshInspector(); RefreshHierarchy(); Log("Added Sprite Renderer."); });
        Item("Script", ShowAttachScriptMenu);

        menu.ShowAt(this);
    }

    private void ShowAttachScriptMenu(object? sender, EventArgs e) => ShowAttachScriptMenu();

    private void ShowAttachScriptMenu()
    {
        if (_selectedEntity == null) return;
        Directory.CreateDirectory(ScriptsDir);
        var files = Directory.GetFiles(ScriptsDir, "*.cs").OrderBy(Path.GetFileName).ToArray();

        var menu = new MenuFlyout { Placement = PlacementMode.Bottom };
        if (files.Length == 0)
        {
            var none = new MenuItem { Header = "No scripts — create one", IsEnabled = true };
            none.Click += (_, _) => AddScript();
            menu.Items.Add(none);
        }
        foreach (var f in files)
        {
            string cls = Path.GetFileNameWithoutExtension(f);
            var mi = new MenuItem { Header = $"Attach {cls}" };
            mi.Click += (_, _) => AttachScriptToSelected(cls);
            menu.Items.Add(mi);
        }
        menu.ShowAt(this);
    }
}
