using UnityEngine;

/// <summary>
/// Sonidos épicos del jefe sintetizados en tiempo de ejecución: rugido, gruñido de dolor,
/// chillido antes de la picada, estruendo al estrellarse y rugido de muerte.
/// Para usar audio propio, asigna los clips "custom*" en el objeto "BossSfx".
/// </summary>
public class BossSfx : MonoBehaviour
{
    public enum Sound { Roar, Grunt, Screech, Slam, DeathRoar }

    public AudioClip customRoar, customGrunt, customScreech, customSlam, customDeathRoar;
    [Range(0f, 1f)] public float volume = 0.85f;

    private static BossSfx instance;
    private AudioSource source;
    private AudioClip roar, grunt, screech, slam, deathRoar;
    const int Rate = 44100;

    public static void Play(Sound sound)
    {
        if (instance == null) instance = new GameObject("BossSfx").AddComponent<BossSfx>();
        instance.PlayInternal(sound);
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var rng = new System.Random(11);
        roar = Make("boss_roar", Growl(1.6f, 62f, 95f, 0.9f, rng));
        grunt = Make("boss_grunt", Growl(0.38f, 105f, 130f, 0.7f, rng));
        deathRoar = Make("boss_death", Growl(2.6f, 85f, 38f, 1f, rng));
        screech = Make("boss_screech", Screech(0.65f, rng));
        slam = Make("boss_slam", Slam(0.9f, rng));
    }

    private void PlayInternal(Sound s)
    {
        AudioClip clip;
        switch (s)
        {
            case Sound.Roar: clip = customRoar != null ? customRoar : roar; break;
            case Sound.Grunt: clip = customGrunt != null ? customGrunt : grunt; break;
            case Sound.Screech: clip = customScreech != null ? customScreech : screech; break;
            case Sound.Slam: clip = customSlam != null ? customSlam : slam; break;
            default: clip = customDeathRoar != null ? customDeathRoar : deathRoar; break;
        }
        source.pitch = Random.Range(0.95f, 1.05f);
        source.PlayOneShot(clip, volume);
    }

    private static AudioClip Make(string name, float[] data)
    {
        float peak = 0.0001f;
        foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < data.Length; i++) data[i] *= 0.95f / peak;
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Rugido/gruñido: sierra grave con vibrato irregular + ruido de garganta, saturado
    private static float[] Growl(float length, float startFreq, float endFreq, float rasp, System.Random rng)
    {
        int n = Mathf.CeilToInt(length * Rate);
        var d = new float[n];
        float ph = 0f, sub = 0f, lp = 0f, noiseLp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = t / length;
            float env = Mathf.Min(1f, k * 8f) * Mathf.Pow(1f - k, 1.3f);
            // Curva de tono: sube un poco al principio y cae (como un rugido)
            float curve = Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI * 0.5f);
            float freq = Mathf.Lerp(startFreq, endFreq, k) * (1f + 0.25f * curve * (1f - k));
            freq *= 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 7f * t) + 0.04f * Mathf.Sin(2f * Mathf.PI * 13.3f * t);
            ph += freq / Rate; ph -= Mathf.Floor(ph);
            sub += 2f * Mathf.PI * freq * 0.5f / Rate;
            float saw = ph * 2f - 1f;
            lp += 0.12f * (saw - lp);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            noiseLp += 0.25f * (noise - noiseLp);
            // Ruido modulado por la fase: el "raspado" de la garganta
            float throat = noiseLp * (0.5f + 0.5f * Mathf.Sin(ph * 2f * Mathf.PI)) * rasp;
            float s = lp * 0.9f + Mathf.Sin(sub) * 0.5f + throat * 0.8f;
            d[i] = (float)System.Math.Tanh(s * 2.2f) * env; // saturación
        }
        return d;
    }

    // Chillido agudo que sube: aviso de la picada
    private static float[] Screech(float length, System.Random rng)
    {
        int n = Mathf.CeilToInt(length * Rate);
        var d = new float[n];
        float ph1 = 0f, ph2 = 0f, band = 0f, low = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = t / length;
            float env = Mathf.Min(1f, k * 10f) * Mathf.Pow(1f - k, 0.8f);
            float freq = Mathf.Lerp(520f, 1500f, k * k);
            float trem = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 22f * t);
            ph1 += 2f * Mathf.PI * freq / Rate;
            ph2 += 2f * Mathf.PI * freq * 1.51f / Rate;
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(freq * 2f, 8000f) / Rate);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            low += f * band; band += f * (noise - low - 0.3f * band);
            float s = (Mathf.Sin(ph1) + 0.5f * Mathf.Sin(ph2)) * 0.6f + band * 0.35f;
            d[i] = (float)System.Math.Tanh(s * 1.8f) * env * trem;
        }
        return d;
    }

    // Estruendo al estrellarse contra el suelo
    private static float[] Slam(float length, System.Random rng)
    {
        int n = Mathf.CeilToInt(length * Rate);
        var d = new float[n];
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            ph += 2f * Mathf.PI * Mathf.Lerp(95f, 28f, Mathf.Min(1f, t * 3f)) / Rate;
            float boom = Mathf.Sin(ph) * Mathf.Exp(-t * 4f);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += 0.08f * (noise - lp);
            float rubble = lp * Mathf.Exp(-t * 6f) * 1.5f;
            float crack = noise * Mathf.Exp(-t * 70f) * 0.6f;
            d[i] = (float)System.Math.Tanh((boom + rubble + crack) * 1.6f);
        }
        return d;
    }
}
