namespace Lumo.Engine.Audio;

/// <summary>
/// Audio system abstraction for sound playback.
/// </summary>
public sealed class AudioSystem : IDisposable
{
    private float _masterVolume = 1.0f;
    private float _musicVolume = 1.0f;
    private float _sfxVolume = 1.0f;

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set => _musicVolume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public void Initialize()
    {
        // Audio initialization placeholder
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
