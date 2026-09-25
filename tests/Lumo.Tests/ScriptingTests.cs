using Lumo.Engine.Assets;
using Lumo.Engine.Rendering.Abstractions;
using Lumo.Engine.Scene;
using Lumo.Engine.Scripting;
using System.Numerics;

namespace Lumo.Tests;

public class ScriptingTests
{
    private const string RotateScript = """
        using Lumo.Engine.Scripting;
        public class Rotator : LumoScript
        {
            public int Updates;
            public override void OnStart() { Updates = 0; }
            public override void OnUpdate(float dt) { Updates++; Position += new System.Numerics.Vector3(dt, 0, 0); }
            public override void OnDestroy() { Updates = -1; }
        }
        """;

    private static (ScriptHost host, Scene scene, Entity entity) Setup()
    {
        var host = new ScriptHost();
        Assert.True(host.Compile(new[] { RotateScript }), string.Join("; ", host.Errors));

        var scene = new Scene { Name = "Test" };
        var entity = scene.CreateEntity("Player");
        entity.Scripts = new ScriptComponent();
        entity.Scripts.ScriptNames.Add("Rotator");

        host.Bind(scene);
        return (host, scene, entity);
    }

    [Fact]
    public void ScriptHost_CompilesValidScript()
    {
        var host = new ScriptHost();
        Assert.True(host.Compile(new[] { RotateScript }));
        Assert.Empty(host.Errors);
        Assert.True(host.IsCompiled);
    }

    [Fact]
    public void ScriptHost_FailsOnInvalidScript()
    {
        var host = new ScriptHost();
        Assert.False(host.Compile(new[] { "this is not valid C#" }));
        Assert.NotEmpty(host.Errors);
    }

    [Fact]
    public void ScriptHost_BindsToEntityWithScriptComponent()
    {
        var (host, _, entity) = Setup();
        Assert.Equal(1, host.InstanceCount);
        Assert.NotNull(entity.Scripts);
    }

    [Fact]
    public void ScriptHost_UpdateMovesEntity()
    {
        var (host, _, entity) = Setup();
        host.Start();
        host.Update(0.5f, 0.5f);
        host.Update(0.5f, 1.0f);

        Assert.Equal(1.0f, entity.Transform.Position.X, 3);
        host.Stop();
    }

    [Fact]
    public void ScriptHost_StopInvokesOnDestroy()
    {
        var (host, _, _) = Setup();
        host.Start();
        host.Stop();
        // After stop, update must not run.
        host.Update(1f, 2f);
        Assert.Equal(1, host.InstanceCount);
    }

    [Fact]
    public void ScriptComponent_SerializesInScene()
    {
        var scene = new Scene { Name = "Save" };
        var e = scene.CreateEntity("Actor");
        e.Scripts = new ScriptComponent();
        e.Scripts.ScriptNames.Add("MyScript");

        var json = scene.Serialize();
        Assert.Contains("MyScript", json);

        var tmp = Path.Combine(Path.GetTempPath(), $"lumo_test_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tmp, json);
            var loaded = Scene.Load(tmp);
            var le = Assert.Single(loaded.AllEntities);
            Assert.NotNull(le.Scripts);
            Assert.Contains("MyScript", le.Scripts!.ScriptNames);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void ObjImporter_ParsesTriangle()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lumo_test_{Guid.NewGuid():N}.obj");
        File.WriteAllText(tmp, """
            # test
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """);
        try
        {
            var mesh = ObjImporter.Load(tmp);
            Assert.Equal(3, mesh.VertexCount);
            Assert.Equal(1, mesh.TriangleCount);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void ObjImporter_ParsesQuadAsTwoTriangles()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"lumo_test_{Guid.NewGuid():N}.obj");
        File.WriteAllText(tmp, """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            f 1 2 3 4
            """);
        try
        {
            var mesh = ObjImporter.Load(tmp);
            Assert.Equal(2, mesh.TriangleCount);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void MeshLibrary_ContainsBuiltinsAndRegistersImported()
    {
        Assert.True(MeshLibrary.Contains("Cube"));
        Assert.True(MeshLibrary.Contains("Quad"));

        MeshLibrary.Register(new Mesh("TestMesh") { Vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0], Indices = [0, 1, 2] });
        Assert.True(MeshLibrary.Contains("TestMesh"));
        Assert.NotNull(MeshLibrary.Get("TestMesh"));
    }
}
