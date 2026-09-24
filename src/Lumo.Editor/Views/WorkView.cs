using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lumo.Editor.Rendering;
using Lumo.Engine.Core;
using Lumo.Engine.Rendering.Abstractions;
using Lumo.Engine.Scene;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using Timer = System.Timers.Timer;

namespace Lumo.Editor.Views;

public class WorkView : UserControl
{
    private readonly LumoEngine _engine;
    private Scene _scene;
    private ProjectInfo _project;
    private readonly Action<string?> _onNavigateHome;
    private readonly Action _onClose;

    private TextBlock _statusText = null!;
    private TextBlock _fpsText = null!;
    private TextBlock _entityCountText = null!;
    private StackPanel _hierarchyList = null!;
    private StackPanel _inspectorContent = null!;
    private TextBlock _consoleLog = null!;
    private GlViewport? _glViewport;
    private Entity? _selectedEntity;
    private bool _isPlaying;
    private bool _is2DMode;
    private readonly System.Collections.Generic.List<string> _logMessages = [];

    private static readonly Color UE_Dark = Color.Parse("#1e1e1e");
    private static readonly Color UE_Panel = Color.Parse("#2b2b2b");
    private static readonly Color UE_PanelHeader = Color.Parse("#333333");
    private static readonly Color UE_Border = Color.Parse("#1a1a1a");
    private static readonly Color UE_Selection = Color.Parse("#264f78");
    private static readonly Color UE_Hover = Color.Parse("#3a3a3a");
    private static readonly Color UE_ToolbarHover = Color.Parse("#454545");
    private static readonly Color UE_TextNormal = Color.Parse("#cccccc");
    private static readonly Color UE_TextDim = Color.Parse("#888888");
    private static readonly Color UE_TextBright = Color.Parse("#ffffff");
    private static readonly Color UE_AccentBlue = Color.Parse("#3d9cd2");
    private static readonly Color UE_Green = Color.Parse("#5fa84e");
    private static readonly Color UE_Orange = Color.Parse("#d4a040");
    private static readonly Color UE_Red = Color.Parse("#cc4444");
    private static readonly Color UE_Purple = Color.Parse("#9966bb");
    private static readonly Color UE_Cyan = Color.Parse("#5cc9c9");

    public WorkView(ProjectInfo project, Action<string?> onNavigateHome, Action onClose)
    {
        _project = project;
        _onNavigateHome = onNavigateHome;
        _onClose = onClose;
        _engine = new LumoEngine();
        _engine.Initialize();
        _scene = new Scene { Name = project.Name };

        LoadProject();
        BuildUI();
        StartFpsTimer();
        Log("Editor loaded.");
    }

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
        var sceneFile = Path.Combine(scenesDir, "scene.json");
        _scene.Save(sceneFile);
        ProjectManager.UpdateLastModified(_project.Path);
    }

    private void StartFpsTimer()
    {
        var timer = new Timer(500);
        timer.Elapsed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _fpsText.Text = $"FPS: {_engine.Time.FPS:F0}";
                _entityCountText.Text = $"Objects: {_scene.AllEntities.Count}";
            });
        };
        timer.Start();

        var renderTimer = new Timer(16.0);
        renderTimer.Elapsed += (_, _) =>
        {
            if (_glViewport != null && _glViewport.IsVisible)
            {
                _glViewport.SetSceneObjects(_scene);
                _glViewport.RenderFrame();
            }
        };
        renderTimer.Start();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        var menuBar = BuildMenuBar();
        DockPanel.SetDock(menuBar, Dock.Top);
        root.Children.Add(menuBar);

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var statusBar = BuildStatusBar();
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(statusBar);

        var center = new DockPanel();

        var bottomPanel = BuildBottomPanel();
        DockPanel.SetDock(bottomPanel, Dock.Bottom);
        center.Children.Add(bottomPanel);

        var contentRow = BuildContent();
        center.Children.Add(contentRow);

        root.Children.Add(center);
        Content = root;
    }

    private Menu BuildMenuBar()
    {
        var menu = new Menu { Background = new SolidColorBrush(UE_Panel), FontSize = 12 };

        var file = new MenuItem { Header = "_File" };
        file.Items.Add(MakeMenuItem("_New", () => Log("New project.")));
        file.Items.Add(MakeMenuItem("_Open...", () => Log("Open project...")));
        file.Items.Add(MakeMenuItem("Save", () => { SaveProject(); Log("Saved."); }));
        file.Items.Add(new Separator());
        file.Items.Add(MakeMenuItem("Export Scene as OBJ...", ExportSceneAsObj));
        file.Items.Add(MakeMenuItem("Import OBJ...", ImportObj));
        file.Items.Add(new Separator());
        file.Items.Add(MakeMenuItem("_Home", () => _onNavigateHome?.Invoke(null)));
        file.Items.Add(new Separator());
        file.Items.Add(MakeMenuItem("E_xit", () => _onClose()));
        menu.Items.Add(file);

        var edit = new MenuItem { Header = "_Edit" };
        edit.Items.Add(MakeMenuItem("_Undo    Ctrl+Z", () => Log("Undo.")));
        edit.Items.Add(MakeMenuItem("_Redo    Ctrl+Y", () => Log("Redo.")));
        edit.Items.Add(new Separator());
        edit.Items.Add(MakeMenuItem("_Duplicate    Ctrl+D", DuplicateSelected));
        edit.Items.Add(MakeMenuItem("_Delete    Del", DeleteSelected));
        menu.Items.Add(edit);

        var create = new MenuItem { Header = "_Create" };
        create.Items.Add(MakeMenuItem("_Empty Actor", () => CreateEntity("Empty")));
        create.Items.Add(MakeMenuItem("_Cube", () => CreateEntityWithMesh("Cube", "Cube")));
        create.Items.Add(MakeMenuItem("_Sphere", () => CreateEntityWithMesh("Sphere", "Sphere")));
        create.Items.Add(new Separator());
        create.Items.Add(MakeMenuItem("_Camera", CreateCamera));
        create.Items.Add(MakeMenuItem("Directional _Light", CreateLight));
        create.Items.Add(MakeMenuItem("_Point Light", CreatePointLight));
        menu.Items.Add(create);

        var help = new MenuItem { Header = "_Help" };
        help.Items.Add(MakeMenuItem("_About Lumo Engine", () => Log($"{EngineConstants.Name} v{EngineConstants.Version}")));
        menu.Items.Add(help);

        return menu;
    }

    private MenuItem MakeMenuItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private Panel BuildToolbar()
    {
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(UE_Panel),
            Spacing = 0,
            Margin = new Thickness(4, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Lumo.Editor.Assets.lumo_icon.png");
            if (stream != null)
            {
                bar.Children.Add(new Image
                {
                    Source = new Bitmap(stream),
                    Width = 20, Height = 20,
                    Margin = new Thickness(4, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
        }
        catch { }

        FlatToolBtn(bar, "New", new PathIcon { Data = Geometry.Parse("M12 5v14M5 12h14") }, () => Log("New level."));
        FlatToolBtn(bar, "Open", new PathIcon { Data = Geometry.Parse("M19 19H5V5h7V3H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z") }, () => Log("Open level."));
        FlatToolBtn(bar, "Save", new PathIcon { Data = Geometry.Parse("M17 3H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z") }, () => { SaveProject(); Log("Saved."); });
        ToolSep(bar);
        FlatToolBtn(bar, "Undo", new PathIcon { Data = Geometry.Parse("M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z") }, () => Log("Undo."));
        FlatToolBtn(bar, "Redo", new PathIcon { Data = Geometry.Parse("M18.4 10.6C16.55 8.99 14.15 8 12.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16c1.05-3.19 4.05-5.5 7.6-5.5 1.95 0 3.73.72 5.12 1.88L13 16h9V7h-3.6l1 1.5z") }, () => Log("Redo."));
        ToolSep(bar);

        Button? playBtn = null;
        playBtn = FlatToolBtn(bar, "Play", new PathIcon { Data = Geometry.Parse("M8 5v14l11-7z") }, () =>
        {
            _isPlaying = !_isPlaying;
            if (playBtn != null) playBtn.Content = _isPlaying ? new TextBlock { Text = "Stop" } : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new PathIcon { Data = Geometry.Parse("M8 5v14l11-7z") }, new TextBlock { Text = "Play" } } };
            Log(_isPlaying ? "PIE started." : "PIE stopped.");
            _statusText.Text = _isPlaying ? "Simulating" : "Ready";
        });
        FlatToolBtn(bar, "Pause", new PathIcon { Data = Geometry.Parse("M6 19h4V5H6v14zm8-14v14h4V5h-4z") }, () => Log("Paused."));
        ToolSep(bar);

        Button? modeBtn = null;
        modeBtn = FlatToolBtn(bar, "3D", new PathIcon { Data = Geometry.Parse("M21 16v-2l-8-5V3.5c0-.83-.67-1.5-1.5-1.5S10 2.67 10 3.5V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5l8 2.5z") }, () =>
        {
            _is2DMode = !_is2DMode;
            if (modeBtn != null) modeBtn.Content = _is2DMode ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new PathIcon { Data = Geometry.Parse("M21 16v-2l-8-5V3.5c0-.83-.67-1.5-1.5-1.5S10 2.67 10 3.5V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5l8 2.5z") }, new TextBlock { Text = "3D" } } } : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new PathIcon { Data = Geometry.Parse("M21 16v-2l-8-5V3.5c0-.83-.67-1.5-1.5-1.5S10 2.67 10 3.5V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5l8 2.5z") }, new TextBlock { Text = "2D" } } };
            Log($"View: {(_is2DMode ? "2D" : "3D")}");
        });
        ToolSep(bar);

        FlatToolBtn(bar, "Cube", new PathIcon { Data = Geometry.Parse("M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5") }, () => CreateEntityWithMesh("Cube", "Cube"));
        FlatToolBtn(bar, "Light", new PathIcon { Data = Geometry.Parse("M9 21c0 .5.4 1 1 1h4c.6 0 1-.5 1-1v-1H9v1zm3-19C8.1 2 5 5.1 5 9c0 2.4 1.2 4.5 3 5.7V17c0 .5.4 1 1 1h6c.6 0 1-.5 1-1v-2.3c1.8-1.3 3-3.4 3-5.7 0-3.9-3.1-7-7-7z") }, () => CreateLight());
        FlatToolBtn(bar, "Camera", new PathIcon { Data = Geometry.Parse("M12 15c-1.7 0-3-1.3-3-3s1.3-3 3-3 3 1.3 3 3-1.3 3-3 3zm1-9H9V3h4v3z") }, () => CreateCamera());

        return bar;
    }

    private Button FlatToolBtn(Panel bar, string label, Control icon, Action onClick)
    {
        var btn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { icon, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } }
            },
            Padding = new Thickness(6, 3),
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = new SolidColorBrush(UE_TextNormal),
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(1, 0),
        };
        btn.PointerEntered += (_, _) => { btn.Background = new SolidColorBrush(UE_ToolbarHover); btn.Foreground = new SolidColorBrush(UE_TextBright); };
        btn.PointerExited += (_, _) => { btn.Background = new SolidColorBrush(Colors.Transparent); btn.Foreground = new SolidColorBrush(UE_TextNormal); };
        btn.Click += (_, _) => onClick();
        bar.Children.Add(btn);
        return btn;
    }

    private void ToolSep(Panel bar)
    {
        bar.Children.Add(new Border
        {
            Width = 1, Height = 16,
            Background = new SolidColorBrush(UE_Border),
            Margin = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
    }

    private Grid BuildContent()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(260)),
                new ColumnDefinition(new GridLength(3)),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(3)),
                new ColumnDefinition(new GridLength(300)),
            }
        };

        var hierPanel = BuildHierarchyPanel();
        Grid.SetColumn(hierPanel, 0);
        grid.Children.Add(hierPanel);
        grid.Children.Add(new Border { Background = new SolidColorBrush(UE_Border) });
        Grid.SetColumn(grid.Children[^1], 1);

        var viewport = BuildViewport();
        Grid.SetColumn(viewport, 2);
        grid.Children.Add(viewport);
        grid.Children.Add(new Border { Background = new SolidColorBrush(UE_Border) });
        Grid.SetColumn(grid.Children[^1], 3);

        var inspPanel = BuildInspectorPanel();
        Grid.SetColumn(inspPanel, 4);
        grid.Children.Add(inspPanel);

        return grid;
    }

    private DockPanel BuildHierarchyPanel()
    {
        _hierarchyList = new StackPanel { Spacing = 0 };
        var scroll = new ScrollViewer { Content = _hierarchyList };

        var searchBox = new TextBox
        {
            Watermark = "Search actors...",
            FontSize = 11,
            Height = 24,
            Margin = new Thickness(4, 4, 4, 2),
            Background = new SolidColorBrush(UE_Dark),
            Foreground = new SolidColorBrush(UE_TextNormal),
            BorderBrush = new SolidColorBrush(UE_Border),
            BorderThickness = new Thickness(1),
        };

        var header = PanelHeader("WORLD OUTLINER");

        var panel = new DockPanel { Background = new SolidColorBrush(UE_Panel) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(searchBox, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(searchBox);
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
            _hierarchyList.Children.Add(HierItem(entity, 0));
            foreach (var child in entity.Children)
                _hierarchyList.Children.Add(HierItem(child, 1));
        }
    }

    private Border HierItem(Entity entity, int indent)
    {
        bool sel = _selectedEntity?.Id == entity.Id;
        string icon = entity switch
        {
            { Camera: not null } => "\U0001f4f7",
            { Light: not null } => "\u2600",
            { MeshRenderer: not null } => "\u25b2",
            _ => "\u25cb"
        };

        var label = new TextBlock
        {
            Text = $"{icon}  {entity.Name}",
            Foreground = new SolidColorBrush(sel ? UE_TextBright : UE_TextNormal),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4 + indent * 16, 0),
        };

        var bg = new Border
        {
            Background = new SolidColorBrush(sel ? UE_Selection : Colors.Transparent),
            Child = label,
            Padding = new Thickness(4, 3),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        bg.PointerEntered += (_, _) => { if (!sel) bg.Background = new SolidColorBrush(UE_Hover); };
        bg.PointerExited += (_, _) => { if (!sel) bg.Background = new SolidColorBrush(Colors.Transparent); };
        bg.PointerPressed += (_, _) =>
        {
            _selectedEntity = entity;
            RefreshHierarchy();
            RefreshInspector();
            _statusText.Text = $"Selected: {entity.Name}";
        };

        return bg;
    }

    private Panel BuildViewport()
    {
        _glViewport = new GlViewport();

        var hint = new TextBlock
        {
            Text = "LMB: Orbit | MMB: Pan | Scroll: Zoom",
            FontSize = 10,
            Foreground = new SolidColorBrush(UE_TextDim),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 6),
            IsHitTestVisible = false,
        };

        var panel = new Panel { Background = new SolidColorBrush(UE_Dark) };
        panel.Children.Add(_glViewport);
        panel.Children.Add(hint);
        return panel;
    }

    private DockPanel BuildInspectorPanel()
    {
        _inspectorContent = new StackPanel { Spacing = 0 };
        var scroll = new ScrollViewer { Content = _inspectorContent };

        var header = PanelHeader("DETAILS");

        var panel = new DockPanel { Background = new SolidColorBrush(UE_Panel) };
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
            _inspectorContent.Children.Add(new TextBlock
            {
                Text = "No actor selected.\n\nSelect an actor in the World Outliner.",
                Foreground = new SolidColorBrush(UE_TextDim),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(16, 32),
            });
            return;
        }

        var e = _selectedEntity;

        _inspectorContent.Children.Add(new Border
        {
            Background = new SolidColorBrush(UE_PanelHeader),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = e switch { { Camera: not null } => "\U0001f4f7", { Light: not null } => "\u2600", { MeshRenderer: not null } => "\u25b2", _ => "\u25cb" }, FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = e.Name, Foreground = new SolidColorBrush(UE_TextBright), FontSize = 13, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center }
                }
            }
        });

        var tSection = UESection("TRANSFORM");
        UEVec3Field(tSection, "Location", e.Transform.Position.X, e.Transform.Position.Y, e.Transform.Position.Z);
        var euler = e.Transform.GetEulerAngles();
        UEVec3Field(tSection, "Rotation", euler.X, euler.Y, euler.Z);
        UEVec3Field(tSection, "Scale", e.Transform.Scale.X, e.Transform.Scale.Y, e.Transform.Scale.Z);
        _inspectorContent.Children.Add(tSection);

        if (e.Camera != null)
        {
            var s = UESection("CAMERA");
            UECheckboxField(s, "Primary", e.Camera.IsPrimary);
            UETextField(s, "FOV", $"{e.Camera.FieldOfView}\u00b0");
            UETextField(s, "Near Clip", e.Camera.NearPlane.ToString());
            UETextField(s, "Far Clip", e.Camera.FarPlane.ToString());
            _inspectorContent.Children.Add(s);
        }

        if (e.MeshRenderer != null)
        {
            var s = UESection("STATIC MESH");
            UETextField(s, "Mesh", e.MeshRenderer.MeshName ?? "None");
            UECheckboxField(s, "Visible", e.MeshRenderer.IsVisible);
            _inspectorContent.Children.Add(s);
        }

        if (e.Light != null)
        {
            var s = UESection("LIGHT");
            UETextField(s, "Type", e.Light.LightType.ToString());
            UETextField(s, "Intensity", e.Light.Intensity.ToString("F2"));
            _inspectorContent.Children.Add(s);
        }

        _inspectorContent.Children.Add(new Border
        {
            Margin = new Thickness(6, 8),
            Child = UEActionButton("+ Add Component", () => Log("Add component")),
        });
    }

    private DockPanel BuildBottomPanel()
    {
        _consoleLog = new TextBlock
        {
            Text = "",
            Foreground = new SolidColorBrush(UE_Green),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6, 4),
        };

        var scroll = new ScrollViewer { Content = _consoleLog, Background = new SolidColorBrush(UE_Dark) };

        var clearBtn = UEActionButton("Clear", () => { _logMessages.Clear(); _consoleLog.Text = ""; });
        clearBtn.Margin = new Thickness(4, 0);

        var tab1 = UETabButton("Output Log", true);
        var tab2 = UETabButton("Build", false);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        tabs.Children.Add(tab1);
        tabs.Children.Add(tab2);

        var headerBar = new DockPanel { Background = new SolidColorBrush(UE_PanelHeader), Height = 26 };
        DockPanel.SetDock(tabs, Dock.Left);
        DockPanel.SetDock(clearBtn, Dock.Right);
        headerBar.Children.Add(clearBtn);
        headerBar.Children.Add(tabs);

        var panel = new DockPanel { Background = new SolidColorBrush(UE_Panel), Height = 160 };
        DockPanel.SetDock(headerBar, Dock.Top);
        panel.Children.Add(headerBar);
        panel.Children.Add(scroll);

        return panel;
    }

    private DockPanel BuildStatusBar()
    {
        _fpsText = new TextBlock { Text = "FPS: 0", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        _entityCountText = new TextBlock { Text = $"Objects: {_scene.AllEntities.Count}", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        _statusText = new TextBlock { Text = "Ready", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        right.Children.Add(_statusText);

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        left.Children.Add(_fpsText);
        left.Children.Add(_entityCountText);
        DockPanel.SetDock(left, Dock.Right);

        var bar = new DockPanel { Background = new SolidColorBrush(UE_Panel), Height = 22 };
        bar.Children.Add(left);
        bar.Children.Add(right);
        return bar;
    }

    private static Border PanelHeader(string title)
    {
        return new Border
        {
            Background = new SolidColorBrush(UE_PanelHeader),
            Height = 24,
            ClipToBounds = true,
            Child = new TextBlock
            {
                Text = $"  {title}",
                Foreground = new SolidColorBrush(UE_TextDim),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                FontFamily = new FontFamily("Segoe UI, Arial"),
                VerticalAlignment = VerticalAlignment.Center,
                LetterSpacing = 1.0,
            }
        };
    }

    private static StackPanel UESection(string title)
    {
        var content = new StackPanel
        {
            Background = new SolidColorBrush(UE_Panel),
            Margin = new Thickness(0, 0, 0, 1),
        };
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(UE_PanelHeader),
            Padding = new Thickness(8, 4),
            Child = new TextBlock { Text = $"  \u25bc {title}", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 10, FontWeight = FontWeight.SemiBold, FontFamily = new FontFamily("Segoe UI, Arial") }
        });
        return content;
    }

    private static void UEVec3Field(StackPanel section, string label, float x, float y, float z)
    {
        section.Children.Add(new TextBlock { Text = $"  {label}", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 10, Margin = new Thickness(8, 6, 0, 2) });
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
            Margin = new Thickness(8, 0, 8, 4),
        };
        grid.Children.Add(UENumberBox("X", x, new SolidColorBrush(Color.Parse("#cc5555")))); Grid.SetColumn(grid.Children[0], 0);
        grid.Children.Add(UENumberBox("Y", y, new SolidColorBrush(Color.Parse("#55aa55")))); Grid.SetColumn(grid.Children[1], 1);
        grid.Children.Add(UENumberBox("Z", z, new SolidColorBrush(Color.Parse("#5588dd")))); Grid.SetColumn(grid.Children[2], 2);
        section.Children.Add(grid);
    }

    private static Border UENumberBox(string axis, float value, Brush axisColor)
    {
        return new Border
        {
            Background = new SolidColorBrush(UE_Dark),
            BorderBrush = new SolidColorBrush(UE_Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2, 1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = axis, Foreground = axisColor, FontSize = 10, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Width = 14 },
                    new TextBlock { Text = value.ToString("F2"), Foreground = new SolidColorBrush(UE_TextNormal), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) }
                }
            }
        };
    }

    private static void UETextField(StackPanel section, string label, string value)
    {
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(90)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
            Margin = new Thickness(8, 2),
        };
        row.Children.Add(new TextBlock { Text = $"  {label}", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(row.Children[0], 0);
        row.Children.Add(new TextBlock { Text = value, Foreground = new SolidColorBrush(UE_TextBright), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(row.Children[1], 1);
        section.Children.Add(row);
    }

    private static void UECheckboxField(StackPanel section, string label, bool value)
    {
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(90)), new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
            Margin = new Thickness(8, 2),
        };
        row.Children.Add(new TextBlock { Text = $"  {label}", Foreground = new SolidColorBrush(UE_TextDim), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(row.Children[0], 0);
        row.Children.Add(new TextBlock { Text = value ? "\u2611" : "\u2610", Foreground = new SolidColorBrush(value ? UE_AccentBlue : UE_TextDim), FontSize = 13, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(row.Children[1], 1);
        section.Children.Add(row);
    }

    private static Button UEActionButton(string label, Action onClick)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(12, 4),
            FontSize = 11,
            Background = new SolidColorBrush(UE_Dark),
            Foreground = new SolidColorBrush(UE_TextNormal),
            BorderBrush = new SolidColorBrush(UE_Border),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        btn.PointerEntered += (_, _) => { btn.Background = new SolidColorBrush(UE_Hover); btn.BorderBrush = new SolidColorBrush(UE_TextDim); };
        btn.PointerExited += (_, _) => { btn.Background = new SolidColorBrush(UE_Dark); btn.BorderBrush = new SolidColorBrush(UE_Border); };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button UETabButton(string label, bool active)
    {
        return new Button
        {
            Content = label,
            Padding = new Thickness(10, 0),
            Height = 24,
            FontSize = 10,
            Background = active ? new SolidColorBrush(UE_Dark) : new SolidColorBrush(Colors.Transparent),
            Foreground = active ? new SolidColorBrush(UE_TextBright) : new SolidColorBrush(UE_TextDim),
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
    }

    private void CreateEntity(string name)
    {
        var entity = _scene.CreateEntity(name);
        _selectedEntity = entity;
        RefreshHierarchy();
        RefreshInspector();
        _statusText.Text = $"Created: {name}";
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

    private void CreatePointLight()
    {
        var e = _scene.CreateEntity("Point Light");
        e.Light = new LightComponent { LightType = LightType.Point, Intensity = 1.0f };
        _selectedEntity = e;
        RefreshHierarchy();
        RefreshInspector();
        Log("Created Point Light.");
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
        _consoleLog.Text = string.Join("\n", _logMessages);
        _statusText.Text = message;
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
                var meshName = entity.MeshRenderer.MeshName ?? "Cube";
                var pos = entity.Transform.Position;
                var rot = entity.Transform.GetEulerAngles();
                var scale = entity.Transform.Scale;

                float radY = MathHelper.DegreesToRadians(rot.Y);
                float radX = MathHelper.DegreesToRadians(rot.X);
                float radZ = MathHelper.DegreesToRadians(rot.Z);

                float cx = pos.X, cy = pos.Y, cz = pos.Z;
                float s = scale.X;

                float[,] cubeVerts = new float[,] {
                    {-0.5f,-0.5f,-0.5f}, {0.5f,-0.5f,-0.5f}, {0.5f,0.5f,-0.5f}, {-0.5f,0.5f,-0.5f},
                    {-0.5f,-0.5f,0.5f}, {0.5f,-0.5f,0.5f}, {0.5f,0.5f,0.5f}, {-0.5f,0.5f,0.5f}
                };
                int[,] cubeFaces = new int[,] {
                    {0,1,2,3}, {5,4,7,6}, {4,0,3,7}, {1,5,6,2}, {3,2,6,7}, {4,5,1,0}
                };

                sb.AppendLine($"o {entity.Name}");
                for (int i = 0; i < 8; i++)
                {
                    float x = cubeVerts[i,0]*s, y = cubeVerts[i,1]*s, z = cubeVerts[i,2]*s;
                    float rx = x*MathF.Cos(radY) - z*MathF.Sin(radY);
                    float rz = x*MathF.Sin(radY) + z*MathF.Cos(radY);
                    float ry = y*MathF.Cos(radX) - rz*MathF.Sin(radX);
                    float rz2 = y*MathF.Sin(radX) + rz*MathF.Cos(radX);
                    sb.AppendLine($"v {rx+cx} {ry+cy} {rz2+cz}");
                }
                for (int i = 0; i < 6; i++)
                {
                    sb.AppendLine($"vn 0 0 1");
                    sb.AppendLine($"vn 0 0 -1");
                    sb.AppendLine($"vn 0 1 0");
                    sb.AppendLine($"vn 0 -1 0");
                    sb.AppendLine($"vn 1 0 0");
                    sb.AppendLine($"vn -1 0 0");
                }
                sb.AppendLine($"usemtl {meshName}");
                for (int i = 0; i < 6; i++)
                {
                    int baseV = vertexOffset + i*4 + 1;
                    sb.AppendLine($"f {baseV}/{baseV} {baseV+1}/{baseV+1} {baseV+2}/{baseV+2} {baseV+3}/{baseV+3}");
                }
                vertexOffset += 8;
            }

            var savePath = Path.Combine(_project.Path, "Scenes", "scene.obj");
            File.WriteAllText(savePath, sb.ToString());
            Log($"Exported scene to {savePath}");
        }
        catch (Exception ex)
        {
            Log($"Export failed: {ex.Message}");
        }
    }

    private void ImportObj()
    {
        Log("OBJ import triggered.");
    }
}
