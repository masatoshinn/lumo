using Lumo.Engine.Core;
using Lumo.Engine.Rendering;
using Lumo.Engine.Rendering.Abstractions;
using Lumo.Engine.Scene;
using System.Numerics;

namespace Lumo.Runtime;

/// <summary>
/// Standalone game runtime for running games outside the editor.
/// </summary>
public sealed class GameRuntime : IDisposable
{
    private LumoEngine? _engine;
    private SoftwareRenderer? _renderer;
    private Scene? _scene;
    private bool _isRunning;

    public LumoEngine? Engine => _engine;
    public Scene? ActiveScene => _scene;
    public bool IsRunning => _isRunning;

    public void Initialize(EngineConfiguration? config = null)
    {
        _engine = new LumoEngine(config);
        _engine.Initialize();

        _renderer = new SoftwareRenderer();
        _renderer.Initialize(1280, 720);
    }

    public void LoadScene(string scenePath)
    {
        if (!File.Exists(scenePath))
            throw new FileNotFoundException($"Scene file not found: {scenePath}");

        _scene = Scene.Load(scenePath);
        _engine?.Logger.LogInformation("Loaded scene: {Name}", _scene.Name);
    }

    public void LoadScene(Scene scene)
    {
        _scene = scene;
    }

    public void Run()
    {
        if (_engine == null || _renderer == null)
            throw new InvalidOperationException("Runtime not initialized.");

        _isRunning = true;
        _engine.Start();

        _engine.Logger.LogInformation("Game runtime started.");

        while (_isRunning)
        {
            float dt = _engine.Tick();

            if (_scene != null)
            {
                UpdateScene(dt);
            }
        }

        _engine.Stop();
        _isRunning = false;
    }

    public void Stop()
    {
        _isRunning = false;
    }

    private void UpdateScene(float deltaTime)
    {
        // Update game logic - placeholder for scripting system integration
        foreach (var entity in _scene!.AllEntities)
        {
            if (!entity.IsActive)
                continue;

            // Transform updates, component updates, etc.
        }
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _engine?.Dispose();
        _scene = null;
    }
}
