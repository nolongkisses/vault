using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace NoxVault;

// A damped spring solved analytically for the error x = value - target, from absolute time since the start.
internal sealed class Spring
{
    internal Spring(double response, double damping)
    {
        Response = response;
        Damping = damping;
    }

    internal double Response { get; }
    internal double Damping { get; }

    internal (double X, double V) State(double x0, double v0, double t)
    {
        double omega = 2 * Math.PI / Response;
        if (Damping >= 1)
        {
            double slope = v0 + omega * x0, decay1 = Math.Exp(-omega * t);
            return ((x0 + slope * t) * decay1, (v0 - omega * t * slope) * decay1);
        }
        double rate = Damping * omega, wd = omega * Math.Sqrt(1 - Damping * Damping), decay = Math.Exp(-rate * t);
        double c = Math.Cos(wd * t), s = Math.Sin(wd * t), b = (v0 + rate * x0) / wd;
        return (decay * (x0 * c + b * s), decay * (v0 * c - (x0 * wd + rate * b) * s));
    }

    // Settled once the rest and the next frame's move are both below the precision (in the property's own unit).
    internal double SettleTime(double x0, double v0, double precision)
    {
        double t = 0;
        for (; t < 3; t += 1.0 / 240)
        {
            var (x, v) = State(x0, v0, t);
            if (Math.Abs(x) < precision && Math.Abs(v) / 60 < precision) break;
        }
        return t;
    }
}

// Leech's springs: glide moves things, settle brings things in. Quick (hover, colour) lives in Controls.xaml.
internal static class Motion
{
    internal static readonly Spring Glide = new(0.34, 0.82);
    internal static readonly Spring Settle = new(0.30, 0.86);
    static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, Track>> Tracks = new();

    sealed record Track(double From, double To, double Velocity, Spring Spring, long Started)
    {
        internal double Duration { get; } = Spring.SettleTime(From - To, Velocity, Precision(From, To));
    }

    // Sub-pixel for moves, a fraction of the distance for small values such as opacity.
    static double Precision(double from, double to) => Math.Max(0.001, Math.Abs(to - from) * 0.002);

    // Renders in the self-test need settled frames; the spring itself is checked there separately.
    internal static bool Instant { get; set; }
    internal static bool Reduced => Instant || !SystemParameters.ClientAreaAnimation;

    // "quick": 140 ms ease for colour and hover, the same curve as the hover storyboards in Controls.xaml.
    internal static void Fade(UIElement element, double to)
    {
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), new KeySpline(0.25,
            0.1, 0.25, 1)));
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    // Retargeting samples the running track, so position and velocity carry over into the new spring.
    internal static void Animate(UIElement owner, DependencyProperty property, double to, Spring spring) =>
        Run((IAnimatable)owner, owner, property, to, spring);

    internal static void Animate(Animatable owner, DependencyProperty property, double to, Spring spring) =>
        Run(owner, owner, property, to, spring);

    static void Run(IAnimatable target, DependencyObject owner, DependencyProperty property, double to, Spring spring)
    {
        long now = Stopwatch.GetTimestamp();
        var tracks = Tracks.GetOrCreateValue(owner);
        double from = (double)owner.GetValue(property), velocity = 0;
        if (tracks.TryGetValue(property, out var track))
        {
            double t = Stopwatch.GetElapsedTime(track.Started, now).TotalSeconds;
            if (t < track.Duration)
            {
                var (x, v) = track.Spring.State(track.From - track.To, track.Velocity, t);
                (from, velocity) = (track.To + x, v);
            }
        }
        owner.SetValue(property, to);
        double precision = Precision(from, to);
        if (Reduced || (Math.Abs(to - from) < precision && Math.Abs(velocity) < precision))
        {
            target.BeginAnimation(property, null);
            tracks.Remove(property);
            return;
        }
        var next = new Track(from, to, velocity, spring, now);
        tracks[property] = next;
        target.BeginAnimation(property, new SpringAnimation(from - to, velocity, spring, next.Duration),
            HandoffBehavior.SnapshotAndReplace);
    }

    // Plays the spring as an offset from the base value, which already holds the exact target.
    sealed class SpringAnimation : DoubleAnimationBase
    {
        static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(nameof(Offset), typeof(double),
            typeof(SpringAnimation));
        static readonly DependencyProperty VelocityProperty = DependencyProperty.Register(nameof(Velocity), typeof(double),
            typeof(SpringAnimation));
        static readonly DependencyProperty ResponseProperty = DependencyProperty.Register(nameof(Response), typeof(double),
            typeof(SpringAnimation));
        static readonly DependencyProperty DampingProperty = DependencyProperty.Register(nameof(Damping), typeof(double),
            typeof(SpringAnimation));

        public SpringAnimation() { }

        internal SpringAnimation(double offset, double velocity, Spring spring, double duration)
        {
            (Offset, Velocity, Response, Damping) = (offset, velocity, spring.Response, spring.Damping);
            Duration = TimeSpan.FromSeconds(duration);
            FillBehavior = FillBehavior.Stop;
        }

        double Offset { get => (double)GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }
        double Velocity { get => (double)GetValue(VelocityProperty); set => SetValue(VelocityProperty, value); }
        double Response { get => (double)GetValue(ResponseProperty); set => SetValue(ResponseProperty, value); }
        double Damping { get => (double)GetValue(DampingProperty); set => SetValue(DampingProperty, value); }

        protected override double GetCurrentValueCore(double origin, double destination, AnimationClock clock)
        {
            double t = clock.CurrentTime?.TotalSeconds ?? 0;
            return destination + new Spring(Response, Damping).State(Offset, Velocity, t).X;
        }

        protected override Freezable CreateInstanceCore() => new SpringAnimation();
    }
}
