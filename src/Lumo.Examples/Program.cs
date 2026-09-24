using Lumo.Engine.Core;
using Lumo.Engine.Rendering.Abstractions;
using Lumo.Engine.Scene;
using System.Numerics;

namespace Lumo.Examples;

/// <summary>
/// Basic example demonstrating the Lumo Engine core systems.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine($"=== {EngineConstants.Name} v{EngineConstants.Version} ===\n");

        // 1. Create and initialize engine
        using var engine = new LumoEngine();
        engine.Initialize();

        // 2. Create a scene
        var scene = new Scene { Name = "Example Scene" };

        // 3. Create entities
        var camera = scene.CreateEntity("Main Camera");
        camera.Camera = new CameraComponent { IsPrimary = true };
        camera.Transform.Position = new Vector3(0, 2, 5);
        camera.Transform.LookAt(Vector3.Zero, Vector3.UnitY);

        var cube = scene.CreateEntity("Cube");
        cube.MeshRenderer = new MeshRendererComponent
        {
            MeshName = "Cube",
            MaterialName = "DefaultMaterial"
        };

        var light = scene.CreateEntity("Directional Light");
        light.Light = new LightComponent
        {
            LightType = LightType.Directional,
            Intensity = 1.0f,
            Color = Vector3.One
        };
        light.Transform.SetRotationFromEuler(-45, 0, 0);

        var child = cube.AddChild("Child Cube");
        child.Transform.Position = new Vector3(2, 0, 0);
        child.MeshRenderer = new MeshRendererComponent { MeshName = "Cube" };

        // 4. Display scene info
        Console.WriteLine($"Scene: {scene.Name}");
        Console.WriteLine($"Entities: {scene.AllEntities.Count}");
        Console.WriteLine();

        foreach (var entity in scene.AllEntities)
        {
            Console.WriteLine($"  {entity}");
            Console.WriteLine($"    Position: ({entity.Transform.Position.X:F1}, {entity.Transform.Position.Y:F1}, {entity.Transform.Position.Z:F1})");
            if (entity.Camera != null)
                Console.WriteLine($"    Camera: Primary={entity.Camera.IsPrimary}, FOV={entity.Camera.FieldOfView}");
            if (entity.Light != null)
                Console.WriteLine($"    Light: {entity.Light.LightType}, Intensity={entity.Light.Intensity}");
            if (entity.MeshRenderer != null)
                Console.WriteLine($"    Mesh: {entity.MeshRenderer.MeshName}");
            Console.WriteLine();
        }

        // 5. Test serialization
        string sceneJson = scene.Serialize();
        Console.WriteLine("--- Scene JSON ---");
        Console.WriteLine(sceneJson);

        // 6. Test time and events
        engine.Start();
        Console.WriteLine("\n--- Running Engine Loop (5 frames) ---");

        for (int i = 0; i < 5; i++)
        {
            float dt = engine.Tick();
            Console.WriteLine($"Frame {engine.Time.FrameCount}: dt={dt:F4}s, FPS={engine.Time.FPS:F1}");
        }

        engine.Stop();
        Console.WriteLine("\nExample completed successfully.");
        return 0;
    }
}
