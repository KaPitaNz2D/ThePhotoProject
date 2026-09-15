using UnityEngine;

using System.Threading;

public static class Tween
{
    public delegate void TweenAction<T>(T from, T to, float factor);

    public static class EasingFunctions
    {
        public static float InOutQuad(float t)
        {
            return t < 0.5 ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
        }

        public static float Linear(float t)
        {
            return t;
        }

        public static float InBack(float t)
        {
            var c1 = 1.70158f;
            var c3 = c1 + 1;

            return c3 * t * t * t - c1 * t * t;
        }

        public static float OutBack(float t)
        {
            var c1 = 1.70158f;
            var c3 = c1 + 1;

            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }
    }

    public delegate float EasingFunction(float x);

    public static Yarn.Unity.YarnTask Run<T>(T from, T to, float time, TweenAction<T> action, CancellationToken cancellationToken = default)
    {
        return Run(from, to, time, EasingFunctions.Linear, action, cancellationToken);
    }

    public static Yarn.Unity.YarnTask Run<T>(T from, T to, float time, EasingFunction easing, TweenAction<T> action, CancellationToken cancellationToken = default)
    {
        return Run(from, to, time, easing, action, 0, cancellationToken);
    }

    public static async Yarn.Unity.YarnTask Run<T>(T from, T to, float time, EasingFunction easing, TweenAction<T> action, float delay = 0, CancellationToken cancellationToken = default)
    {
        if (time <= 0 || Application.isPlaying == false)
        {
            // Synchronously jump to the end of the tween if time is zero, or if we're not in play mode
            action(from, to, 1);
            return;
        }

        action(from, to, 0);

        if (delay > 0)
        {
            await Yarn.Unity.YarnTask.Delay(System.TimeSpan.FromSeconds(delay));
        }

        float timeElapsed = 0;

#if UNITY_EDITOR
        // Tweens kicked off by in-editor interactions might have a
        // larger-than-normal delta time, so delay a few frames before starting
        // to measure it
        var startFrame = Time.frameCount;
        await Yarn.Unity.YarnTask.WaitUntil(() => Time.frameCount >= startFrame + 10);
#endif

        var lastTime = Time.time;

        while (timeElapsed < time && cancellationToken.IsCancellationRequested == false)
        {
            var deltaTime = Time.time - lastTime;
            timeElapsed += deltaTime;
            lastTime = Time.time;

            var factor = Mathf.Clamp01(timeElapsed / time);
            factor = easing(factor);

            action(from, to, factor);

            await Yarn.Unity.YarnTask.Yield();
        }

        action(from, to, 1);
    }

    public static Color LerpColorAlpha(Color color, float alphaFrom, float alphaTo, float t)
    {
        var c = color;
        c.a = Mathf.Lerp(alphaFrom, alphaTo, t);
        return c;
    }
}
