using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Lemon.Template.Wpf.Infrastructures.Animations
{
    /// <summary>
    /// Animates a <see cref="GridLength"/>. WPF ships no animation for that type, so a grid column can
    /// otherwise only be resized in a single jump — which is what made the side menu snap between its
    /// expanded and collapsed widths.
    /// </summary>
    /// <remarks>
    /// Only absolute (pixel) endpoints are interpolated. Auto or star endpoints cannot be measured here,
    /// so they simply snap to <see cref="To"/> once the clock completes.
    /// </remarks>
    public sealed class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(
                nameof(From),
                typeof(GridLength),
                typeof(GridLengthAnimation),
                new PropertyMetadata(new GridLength(0)));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(
                nameof(To),
                typeof(GridLength),
                typeof(GridLengthAnimation),
                new PropertyMetadata(new GridLength(0)));

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(
                nameof(EasingFunction),
                typeof(IEasingFunction),
                typeof(GridLengthAnimation),
                new PropertyMetadata(null));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public IEasingFunction? EasingFunction
        {
            get => (IEasingFunction?)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress is not { } progress)
            {
                return defaultDestinationValue;
            }

            var from = From;
            var to = To;

            if (!from.IsAbsolute || !to.IsAbsolute)
            {
                return progress >= 1d ? to : from;
            }

            var eased = EasingFunction?.Ease(progress) ?? progress;
            return new GridLength(from.Value + ((to.Value - from.Value) * eased), GridUnitType.Pixel);
        }
    }
}
