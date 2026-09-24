using System.Text.Json;

namespace Lumo.Engine.Core;

public class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public string Version { get; set; } = "0.1.0";
    public string Thumbnail { get; set; } = string.Empty;
}

public static class ProjectManager
{
    private static readonly string ProjectsRoot;

    static ProjectManager()
    {
        ProjectsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LumoProjects");
        Directory.CreateDirectory(ProjectsRoot);
    }

    public static string ProjectsRootPath => ProjectsRoot;

    public static List<ProjectInfo> GetAllProjects()
    {
        var projects = new List<ProjectInfo>();
        if (!Directory.Exists(ProjectsRoot)) return projects;

        foreach (var dir in Directory.GetDirectories(ProjectsRoot))
        {
            var projFile = Path.Combine(dir, "Project.json");
            if (!File.Exists(projFile)) continue;

            try
            {
                var json = File.ReadAllText(projFile);
                var info = JsonSerializer.Deserialize<ProjectInfo>(json);
                if (info != null)
                {
                    info.Path = dir;
                    projects.Add(info);
                }
            }
            catch { }
        }

        return projects;
    }

    public static void CreateProject(string name, string description = "")
    {
        var projectPath = Path.Combine(ProjectsRoot, SanitizeName(name));
        if (Directory.Exists(projectPath)) return;

        Directory.CreateDirectory(projectPath);
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectPath, "Scenes"));

        var info = new ProjectInfo
        {
            Name = name,
            Path = projectPath,
            Description = description,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now,
            Version = EngineConstants.Version
        };

        SaveProjectInfo(info);
    }

    public static void SaveProjectInfo(ProjectInfo info)
    {
        var projFile = Path.Combine(info.Path, "Project.json");
        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(projFile, json);
    }

    public static ProjectInfo? LoadProjectInfo(string projectPath)
    {
        var projFile = Path.Combine(projectPath, "Project.json");
        if (!File.Exists(projFile)) return null;

        try
        {
            var json = File.ReadAllText(projFile);
            var info = JsonSerializer.Deserialize<ProjectInfo>(json);
            if (info != null)
            {
                info.Path = projectPath;
                return info;
            }
        }
        catch { }
        return null;
    }

    public static void UpdateLastModified(string projectPath)
    {
        var info = LoadProjectInfo(projectPath);
        if (info != null)
        {
            info.LastModified = DateTime.Now;
            SaveProjectInfo(info);
        }
    }

    private static string SanitizeName(string name)
    {
        return string.Concat(name.Split(Path.GetInvalidFileNameChars()))
            .Replace(" ", "_");
    }
}
