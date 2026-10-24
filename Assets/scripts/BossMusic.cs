using System.Collections;
using UnityEngine;

/// <summary>
/// Música épica del jefe, sintetizada en tiempo de ejecución (el proyecto no tiene audio):
/// tambores taiko, metales y cuerdas graves en Re menor y un coro suave, en bucle de 8 compases.
/// Si consigues una pista real, asígnala a customTrack en el objeto "BossMusic" y se usará esa.
/// </summary>
public class BossMusic : MonoBehaviour
{
    public AudioClip customTrack;
    [Range(0f, 1f)] public float volume = 0.6f;
    public float fadeTime = 1.5f;

    private static BossMusic instance;
    private static AudioClip generated;
    private AudioSource source;
    private Coroutine fade;

    const int Rate = 44100;
    const float Bpm = 96f;

    private static System.Threading.Tasks.Task<float[]> composing;

    // Compone la música en un hilo aparte (tarda ~2 s); el AudioClip se crea luego en el hilo principal
    public static void Prewarm()
    {
        if (generated == null && composing == null)
            composing = System.Threading.Tasks.Task.Run(() => ComposeSamples());
    }

    private static bool TryFinishComposing()
    {
        if (generated != null) return true;
        if (composing == null || !composing.IsCompleted) return false;
        var samples = composing.Result;
        generated = AudioClip.Create("BossMusic_Epic", samples.Length, 1, Rate, false);
        generated.SetData(samples, 0);
        composing = null;
        return true;
    }

    public static void Play()
    {
        if (instance == null) instance = new GameObject("BossMusic").AddComponent<BossMusic>();
        instance.StartCoroutine(instance.StartWhenReady());
    }

    private IEnumerator StartWhenReady()
    {
        if (customTrack == null)
        {
            Prewarm();
            while (!TryFinishComposing()) yield return null;
        }
        StartTrack();
    }

    public static void Stop()
    {
        if (instance == null) return;
        instance.StopAllCoroutines(); // por si aún esperaba a que terminara de componerse
        instance.fade = null;
        instance.FadeTo(0f, true);
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void StartTrack()
    {
        source.clip = customTrack != null ? customTrack : generated;
        if (!source.isPlaying) source.Play();
        FadeTo(volume, false);
    }

    private void FadeTo(float target, bool stopAtEnd)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(target, stopAtEnd));
    }

    private IEnumerator FadeRoutine(float target, bool stopAtEnd)
    {
        float start = source.volume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        source.volume = target;
        if (stopAtEnd) source.Stop();
        fade = null;
    }

    // ------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------
    private static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

    // Solo matemáticas (sin API de Unity salvo Mathf), así puede ejecutarse fuera del hilo principal
    private static float[] ComposeSamples()
    {
        float beat = 60f / Bpm;
        float bar = beat * 4f;
        const int bars = 8;
        int total = Mathf.CeilToInt(bar * bars * Rate);
        var mix = new float[total];

        // Re menor épico: Dm Dm Bb Bb Gm Gm A A (notas MIDI: raíz, tercera, quinta)
        int[][] chords =
        {
            new[] { 50, 53, 57 }, new[] { 50, 53, 57 },
            new[] { 46, 50, 53 }, new[] { 46, 50, 53 },
            new[] { 43, 46, 50 }, new[] { 43, 46, 50 },
            new[] { 45, 49, 52 }, new[] { 45, 49, 52 },
        };

        var rng = new System.Random(7);

        for (int b = 0; b < bars; b++)
        {
            int barStart = Mathf.RoundToInt(b * bar * Rate);
            int[] chord = chords[b];

            // Cuerdas/metales graves: sierras desafinadas con filtro y crecida
            foreach (int n in chord)
            {
                AddPad(mix, barStart, bar, Midi(n - 12), 0.10f, 900f);
                AddPad(mix, barStart, bar, Midi(n), 0.07f, 1600f);
            }
            // Bajo de órgano/drone en la raíz
            AddPad(mix, barStart, bar, Midi(chord[0] - 24), 0.16f, 300f);

            // Golpe de metales en el primer tiempo de cada compás
            foreach (int n in chord) AddBrassStab(mix, barStart, Midi(n), 0.09f);

            // Coro "ahh" suave en la octava alta, compases pares
            if (b % 2 == 0)
                foreach (int n in chord) AddChoir(mix, barStart, bar * 2f, Midi(n + 12), 0.035f);

            // Taikos: patrón en corcheas, redoble en el último compás
            int[] pattern = (b == bars - 1) ? new[] { 0, 1, 2, 3, 4, 5, 6, 7 } : new[] { 0, 3, 4, 6, 7 };
            foreach (int eighth in pattern)
            {
                int pos = barStart + Mathf.RoundToInt(eighth * beat * 0.5f * Rate);
                bool accent = eighth == 0 || eighth == 4;
                AddTaiko(mix, pos, accent ? 0.55f : 0.32f, rng);
            }
            if (b == bars - 1)
                for (int s = 0; s < 8; s++) // semicorcheas finales antes de volver al inicio
                    AddTaiko(mix, barStart + Mathf.RoundToInt((2f * beat + s * beat * 0.25f) * Rate), 0.18f + s * 0.03f, rng);
        }

        // Normalizar
        float peak = 0.0001f;
        foreach (float v in mix) peak = Mathf.Max(peak, Mathf.Abs(v));
        float gain = 0.9f / peak;
        for (int i = 0; i < total; i++) mix[i] *= gain;
        return mix;
    }

    private static void AddPad(float[] mix, int start, float length, float freq, float amp, float cutoff)
    {
        int n = Mathf.CeilToInt(length * Rate);
        float[] detune = { 0.996f, 1f, 1.004f };
        float[] ph = new float[detune.Length];
        float lp = 0f;
        float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
        for (int i = 0; i < n && start + i < mix.Length; i++)
        {
            float k = i / (float)n;
            float env = Mathf.Min(1f, k * 6f) * Mathf.Min(1f, (1f - k) * 10f); // crecida y corte suave
            float s = 0f;
            for (int d = 0; d < detune.Length; d++)
            {
                ph[d] += freq * detune[d] / Rate;
                ph[d] -= Mathf.Floor(ph[d]);
                s += ph[d] * 2f - 1f; // sierra
            }
            lp += a * (s / detune.Length - lp);
            mix[start + i] += lp * env * amp;
        }
    }

    private static void AddBrassStab(float[] mix, int start, float freq, float amp)
    {
        int n = Mathf.CeilToInt(0.6f * Rate);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < n && start + i < mix.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 4f);
            float cutoff = 400f + 3000f * env; // se abre en el ataque, como un metal soplado fuerte
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            ph += freq / Rate; ph -= Mathf.Floor(ph);
            lp += a * ((ph * 2f - 1f) - lp);
            mix[start + i] += lp * env * amp;
        }
    }

    private static void AddChoir(float[] mix, int start, float length, float freq, float amp)
    {
        int n = Mathf.CeilToInt(length * Rate);
        float ph1 = 0f, ph2 = 0f;
        for (int i = 0; i < n && start + i < mix.Length; i++)
        {
            float t = i / (float)Rate;
            float k = i / (float)n;
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 3f)) * Mathf.Min(1f, (1f - k) * 4f);
            float vib = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5f * t);
            ph1 += 2f * Mathf.PI * freq * vib / Rate;
            ph2 += 2f * Mathf.PI * freq * 2.003f * vib / Rate;
            mix[start + i] += (Mathf.Sin(ph1) + 0.3f * Mathf.Sin(ph2)) * env * amp;
        }
    }

    private static void AddTaiko(float[] mix, int start, float amp, System.Random rng)
    {
        int n = Mathf.CeilToInt(0.5f * Rate);
        float ph = 0f;
        for (int i = 0; i < n && start + i < mix.Length; i++)
        {
            float t = i / (float)Rate;
            float freq = Mathf.Lerp(130f, 48f, Mathf.Min(1f, t * 8f));
            ph += 2f * Mathf.PI * freq / Rate;
            float body = Mathf.Sin(ph) * Mathf.Exp(-t * 7f);
            float slap = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.35f;
            mix[start + i] += (body + slap) * amp;
        }
    }
}
