using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Xunit;
using Xunit.Abstractions;

namespace Lumo.Tests;

public class GlDiagnostic31
{
    private readonly ITestOutputHelper _output;
    public GlDiagnostic31(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Check_OpenGL_31_Only()
    {
        var configs = new (string name, GraphicsAPI api, bool visible)[]
        {
            ("GL-1.1-visible", new GraphicsAPI(ContextAPI.OpenGL, (ContextProfile)0, ContextFlags.Default, new APIVersion(1, 1)), true),
            ("GL-1.5-visible", new GraphicsAPI(ContextAPI.OpenGL, (ContextProfile)0, ContextFlags.Default, new APIVersion(1, 5)), true),
            ("GL-2.0-visible", new GraphicsAPI(ContextAPI.OpenGL, (ContextProfile)0, ContextFlags.Default, new APIVersion(2, 0)), true),
            ("GL-2.1-visible", new GraphicsAPI(ContextAPI.OpenGL, (ContextProfile)0, ContextFlags.Default, new APIVersion(2, 1)), true),
            ("GL-3.0-visible", new GraphicsAPI(ContextAPI.OpenGL, (ContextProfile)0, ContextFlags.Default, new APIVersion(3, 0)), true),
            ("GL-3.1-visible", new GraphicsAPI(ContextAPI.OpenGL, (ContextProfile)0, ContextFlags.Default, new APIVersion(3, 1)), true),
        };

        foreach (var (name, api, visible) in configs)
        {
            try
            {
                var opts = WindowOptions.Default with
                {
                    API = api,
                    IsVisible = visible,
                    Size = new Silk.NET.Maths.Vector2D<int>(256, 256),
                };

                using var window = Window.Create(opts);
                window.Initialize();

                var gl = GL.GetApi(window);
                string version = gl.GetStringS(StringName.Version);
                string renderer = gl.GetStringS(StringName.Renderer);
                string vendor = gl.GetStringS(StringName.Vendor);
                string glsl = gl.GetStringS(StringName.ShadingLanguageVersion);
                _output.WriteLine($"SUCCESS [{name}]: GL={version}, Renderer={renderer}, Vendor={vendor}, GLSL={glsl}");
                gl.Dispose();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"FAILED [{name}]: {ex.Message}");
            }
        }
    }
}
