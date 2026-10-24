using UnityEngine;

/// <summary>
/// Animador simple por fotogramas para enemigos y efectos que no tienen Animator Controller.
/// Cambia de animación con Play(frames, fps, loop).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 10f;
    public bool loop = true;
    public bool destroyOnFinish = false;

    public bool IsFinished { get; private set; }
    public int CurrentFrame { get; private set; }

    private SpriteRenderer sr;
    private float timer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    public void Play(Sprite[] newFrames, float newFps, bool newLoop)
    {
        if (newFrames == frames && !IsFinished) return; // ya se está reproduciendo
        frames = newFrames;
        fps = newFps;
        loop = newLoop;
        timer = 0f;
        CurrentFrame = 0;
        IsFinished = false;
        if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || IsFinished || fps <= 0f) return;

        timer += Time.deltaTime;
        float frameTime = 1f / fps;
        while (timer >= frameTime)
        {
            timer -= frameTime;
            CurrentFrame++;
            if (CurrentFrame >= frames.Length)
            {
                if (loop) CurrentFrame = 0;
                else
                {
                    CurrentFrame = frames.Length - 1;
                    IsFinished = true;
                    if (destroyOnFinish) Destroy(gameObject);
                    break;
                }
            }
        }
        sr.sprite = frames[CurrentFrame];
    }
}
