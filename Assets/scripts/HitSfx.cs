using UnityEngine;

/// <summary>
/// Sonido de corte de espada sintetizado en tiempo de ejecución (el proyecto no tiene archivos de audio).
/// Mezcla el "swish" de la hoja, un filo metálico y un leve impacto. Uso: HitSfx.Play(0..1) — 1 es el golpe más fuerte.
/// Si más adelante agregas un .wav, asígnalo a customClip en el objeto "HitSfx" y se usará ese.
/// </summary>
public class HitSfx : MonoBehaviour
{
    public AudioClip customClip;
    [Range(0f, 1f)] public float volume = 0.8f;

    private static HitSfx instance;
    private AudioSource source;
    public AudioClip customEpicClip;            // opcional: sonido propio para el golpe final
    private AudioClip lightClip;
    private AudioClip heavyClip;
    private AudioClip epicClip;

    private static void Ensure()
    {
        if (instance == null) instance = new GameObject("HitSfx").AddComponent<HitSfx>();
    }

    public static void Play(float strength = 0f)
    {
        Ensure();
        instance.PlayInternal(Mathf.Clamp01(strength));
    }

    // Suena en cada tajo, acierte o no. epic = golpe final del combo / remate
    public static void PlaySlash(bool epic)
    {
        Ensure();
        if (!epic) { instance.PlayInternal(0f); return; }
        var clip = instance.customEpicClip != null ? instance.customEpicClip : instance.epicClip;
        instance.source.pitch = Random.Range(0.97f, 1.03f);
        instance.source.PlayOneShot(clip, instance.volume);
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        lightClip = SynthesizeSlash("slash_light", 0.22f, 2900f, 0.55f);
        heavyClip = SynthesizeSlash("slash_heavy", 0.32f, 2300f, 0.8f);
        epicClip = SynthesizeEpic();
    }

    private void PlayInternal(float strength)
    {
        var clip = customClip != null ? customClip : (strength > 0.5f ? heavyClip : lightClip);
        source.pitch = Random.Range(0.92f, 1.08f); // variación para que no suene repetitivo
        source.PlayOneShot(clip, volume * Mathf.Lerp(0.8f, 1f, strength));
    }

    // Golpe final: corte más largo y grave + explosión sub-grave + eco, para que se sienta "épico"
    private static AudioClip SynthesizeEpic()
    {
        const int rate = 44100;
        var slash = SynthesizeSlash("slash_epic", 0.45f, 1700f, 1f);
        float[] s = new float[slash.samples];
        slash.GetData(s, 0);

        int total = Mathf.CeilToInt(rate * 1.1f);
        var data = new float[total];
        for (int i = 0; i < s.Length; i++) data[i] = s[i];

        // Boom sub-grave en el momento del impacto
        int impact = Mathf.RoundToInt(0.45f * 0.18f * rate);
        float ph = 0f;
        for (int i = 0; impact + i < total; i++)
        {
            float t = i / (float)rate;
            ph += 2f * Mathf.PI * Mathf.Lerp(110f, 38f, Mathf.Min(1f, t * 5f)) / rate;
            data[impact + i] += Mathf.Sin(ph) * Mathf.Exp(-t * 5f) * 0.55f;
        }

        // Eco corto (dos repeticiones atenuadas) para dar sensación de espacio
        int[] delays = { Mathf.RoundToInt(0.11f * rate), Mathf.RoundToInt(0.23f * rate) };
        float[] gains = { 0.35f, 0.18f };
        var dry = (float[])data.Clone();
        for (int d = 0; d < delays.Length; d++)
            for (int i = 0; i + delays[d] < total; i++) data[i + delays[d]] += dry[i] * gains[d];

        float peak = 0.0001f;
        foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < total; i++) data[i] *= 0.95f / peak;

        var clip = AudioClip.Create("slash_epic_full", total, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Corte de espada: "swish" de aire + filo metálico ("shiing") + un leve golpe de impacto
    private static AudioClip SynthesizeSlash(string name, float length, float ringFreq, float weight)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(rate * length);
        var data = new float[samples];
        var rng = new System.Random(name.GetHashCode());

        // Parciales inarmónicos: es lo que hace que suene a metal y no a tono puro
        float[] ratios = { 1f, 1.47f, 2.09f, 2.76f, 3.51f };
        float[] amps = { 0.5f, 0.35f, 0.25f, 0.18f, 0.12f };
        float[] phases = new float[ratios.Length];

        // Filtro paso banda simple (estado variable) para dar forma al "swish"
        float low = 0f, band = 0f;
        float thumpPhase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float k = t / length;

            // 1) Swish: ruido filtrado cuya frecuencia sube y baja, como la hoja cortando el aire
            float sweep = Mathf.Lerp(900f, 5200f, Mathf.Sin(Mathf.Clamp01(k * 1.6f) * Mathf.PI));
            float f = 2f * Mathf.Sin(Mathf.PI * sweep / rate);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            low += f * band;
            float high = noise - low - 0.35f * band;
            band += f * high;
            float swishEnv = Mathf.Sin(Mathf.Clamp01(k * 2.2f) * Mathf.PI); // entra y sale rápido
            float swish = band * swishEnv * 0.9f;

            // 2) Filo metálico: entra justo en el impacto y resuena brevemente
            float ringT = Mathf.Max(0f, t - length * 0.18f);
            float ringEnv = ringT > 0f ? Mathf.Exp(-ringT * 22f) : 0f;
            float ring = 0f;
            for (int p = 0; p < ratios.Length; p++)
            {
                phases[p] += 2f * Mathf.PI * ringFreq * ratios[p] / rate;
                ring += Mathf.Sin(phases[p]) * amps[p];
            }
            ring *= ringEnv * 0.55f;

            // 3) Impacto: golpe grave corto que da "peso" al corte
            float thumpEnv = ringT > 0f ? Mathf.Exp(-ringT * 40f) : 0f;
            thumpPhase += 2f * Mathf.PI * Mathf.Lerp(180f, 70f, Mathf.Clamp01(ringT * 12f)) / rate;
            float thump = Mathf.Sin(thumpPhase) * thumpEnv * weight * 0.6f;

            data[i] = Mathf.Clamp((swish + ring + thump) * 0.5f, -1f, 1f); // 0.5 evita que sature
        }
        var clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
