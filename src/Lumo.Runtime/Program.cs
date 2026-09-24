using Lumo.Engine.Core;
using Lumo.Runtime;

namespace Lumo.Runtime;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Lumo Runtime v" + EngineConstants.Version);

        using var runtime = new GameRuntime();

        var config = new EngineConfiguration
        {
            Name = "Lumo Runtime",
            LogLevel = Microsoft.Extensions.Logging.LogLevel.Information
        };

        runtime.Initialize(config);

        if (args.Length > 0 && File.Exists(args[0]))
        {
            runtime.LoadScene(args[0]);
        }

        runtime.Run();
        return 0;
    }
}
