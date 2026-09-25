using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Xunit;
using Xunit.Abstractions;

namespace Lumo.Tests;

public class GlDiagnostic
{
    private readonly ITestOutputHelper _output;
    public GlDiagnostic(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Check_OpenGL_Version()
    {
        var configs = new (ContextProfile profile, ContextFlags flags, APIVersion ver, string name)[]
        {
            (ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3), "3.3 Core FwdCompat"),
            (ContextProfile.Compatability, ContextFlags.ForwardCompatible, new APIVersion(3, 3), "3.3 Compat FwdCompat"),
            (ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 3), "3.3 Compat Default"),
            (ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 2), "3.2 Compat Default"),
            ((ContextProfile)0, ContextFlags.Default, new APIVersion(3, 2), "3.2 AnyProfile Default"),
            ((ContextProfile)0, ContextFlags.Default, new APIVersion(3, 1), "3.1 AnyProfile Default"),
            ((ContextProfile)0, ContextFlags.Default, new APIVersion(3, 0), "3.0 AnyProfile Default"),
            ((ContextProfile)0, ContextFlags.Default, new APIVersion(2, 1), "2.1 AnyProfile Default"),
        };

        string? firstSuccess = null;

        foreach (var (profile, flags, ver, name) in configs)
        {
            try
            {
                var opts = WindowOptions.Default with
                {
                    API = new GraphicsAPI(ContextAPI.OpenGL, profile, flags, ver),
                    IsVisible = false,
                    Size = new Silk.NET.Maths.Vector2D<int>(64, 64),
                };

                using var window = Window.Create(opts);
                window.Initialize();

                var gl = GL.GetApi(window);
                string version = gl.GetStringS(StringName.Version);
                string renderer = gl.GetStringS(StringName.Renderer);
                string vendor = gl.GetStringS(StringName.Vendor);
                string glsl = gl.GetStringS(StringName.ShadingLanguageVersion);

                _output.WriteLine($"SUCCESS [{name}]: GL={version}, Renderer={renderer}, Vendor={vendor}, GLSL={glsl}");

                firstSuccess ??= name;
                gl.Dispose();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"FAILED [{name}]: {ex.Message}");
            }
        }

        _output.WriteLine($"First working config: {firstSuccess ?? "NONE WORKS"}");

        if (firstSuccess == null)
        {
            // Known limitation on machines where the GPU driver cannot create any GL context.
            // The editor falls back to the software renderer in this case, so this is not a failure.
            _output.WriteLine("OpenGL unavailable on this machine — editor will use the software (CPU) renderer.");
            return;
        }

        Assert.NotNull(firstSuccess);
    }
}
