using System.IO;

string workViewPath = @"C:\Users\User\Pictures\lumo\src\Lumo.Editor\Views\WorkView.cs";
string content = File.ReadAllText(workViewPath);

// Fix constructor
content = content.Replace(
    "private readonly Action<string?> _onNavigateHome;\n\n    public WorkView(ProjectInfo project, Action<string?> onNavigateHome)\n    {\n        _project = project;\n        _onNavigateHome = onNavigateHome;\n        _engine",
    "private readonly Action _onClose;\n    private readonly Action<string?> _onNavigateHome;\n\n    public WorkView(ProjectInfo project, Action<string?> onNavigateHome, Action onClose)\n    {\n        _project = project;\n        _onNavigateHome = onNavigateHome;\n        _onClose = onClose;\n        _engine");

// Fix Application.Windows
content = content.Replace(
    "Application.Current?.Windows[0]?.Close()",
    "(this.GetVisualRoot() as Avalonia.Window)?.Close()");

File.WriteAllText(workViewPath, content);

// Fix MainWindow.cs
string mainPath = @"C:\Users\User\Pictures\lumo\src\Lumo.Editor\MainWindow.cs";
string mainContent = File.ReadAllText(mainPath);
mainContent = mainContent.Replace(
    "new WorkView(info, (_) => ShowHome())",
    "new WorkView(info, (_) => ShowHome(), Close)");
File.WriteAllText(mainPath, mainContent);

File.WriteAllText(@"C:\Users\User\Pictures\lumo\fix.bat", "done");
