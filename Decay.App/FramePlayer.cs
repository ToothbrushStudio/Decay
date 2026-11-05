using Decay.Core;
using Decay.Renderer;

namespace Decay.App
{
    /// <summary>
    /// Pre-renders a decay animation (on a background thread) and plays it
    /// into an Image control. Forward = the rot consuming the carrier
    /// (encrypt preview); reverse = the un-rot reveal (decrypt); crossfade =
    /// the Rot style. The ops always run at full resolution, but the stored
    /// frames are preview pictures (see PreviewImage), which cuts a 1024 px
    /// reveal from about 10 s of rendering to 1.5 s. The
    /// full-resolution end state is kept as FinalImage for saving.
    ///
    /// A player plays once at a time; Stop ends playback early, and pages
    /// stop the current player before starting anything that would also
    /// draw into the preview.
    /// </summary>
    public sealed class FramePlayer
    {
        private readonly List<byte[]> _framePngs;
        private IDispatcherTimer? _timer;

        public int FrameCount => _framePngs.Count;

        /// <summary>
        /// The animation's end state at full resolution. For a reveal this is
        /// the recovered carrier; for the encrypt preview, the exported image.
        /// </summary>
        public PixelBuffer FinalImage { get; }

        private FramePlayer(List<byte[]> framePngs, PixelBuffer finalImage)
        {
            _framePngs = framePngs;
            FinalImage = finalImage;
        }

        public static Task<FramePlayer> RenderForwardAsync(
            DecaySequence sequence,
            PixelBuffer startState
        )
        {
            return Task.Run(() =>
            {
                List<byte[]> frames = new List<byte[]>(DecaySequence.FrameCount + 1);
                PixelBuffer working = startState.Clone();

                frames.Add(PreviewImage.Png(working));

                for (int frame = 0; frame < DecaySequence.FrameCount; frame++)
                {
                    sequence.ApplyFrame(working, frame);
                    frames.Add(PreviewImage.Png(working));
                }

                return new FramePlayer(frames, working);
            });
        }

        public static Task<FramePlayer> RenderReverseAsync(
            DecaySequence sequence,
            PixelBuffer receivedImage
        )
        {
            return Task.Run(() =>
            {
                List<byte[]> frames = new List<byte[]>(DecaySequence.FrameCount + 1);
                PixelBuffer working = receivedImage.Clone();

                frames.Add(PreviewImage.Png(working));

                for (int frame = DecaySequence.FrameCount - 1; frame >= 0; frame--)
                {
                    sequence.RevertFrame(working, frame);
                    frames.Add(PreviewImage.Png(working));
                }

                return new FramePlayer(frames, working);
            });
        }

        /// <summary>
        /// Rot style: a cross-fade between two images. Encrypt previews
        /// carrier → rotted; decrypt reveals rotted → restored.
        /// </summary>
        public static Task<FramePlayer> RenderCrossfadeAsync(
            PixelBuffer from,
            PixelBuffer to,
            int steps = 24
        )
        {
            return Task.Run(() =>
            {
                // Fading the preview-size pictures gives the same frames for a
                // fraction of the work.
                PixelBuffer smallFrom = PreviewImage.Shrink(from);
                PixelBuffer smallTo = PreviewImage.Shrink(to);
                List<byte[]> frames = new List<byte[]>(steps + 1);

                for (int step = 0; step <= steps; step++)
                {
                    frames.Add(PreviewImage.Png(RotDecay.Interpolate(smallFrom, smallTo, step, steps)));
                }

                return new FramePlayer(frames, to);
            });
        }

        /// <summary>
        /// Plays the frames into the target, replacing any earlier playback
        /// of this player. onFrame reports (index, total) each tick — the
        /// decrypt page uses it to reveal the message text in step with the
        /// un-rot. onDone runs only if playback reaches the end.
        /// </summary>
        public void Play(
            Image target,
            IDispatcher dispatcher,
            TimeSpan frameInterval,
            Action<int, int>? onFrame = null,
            Action? onDone = null
        )
        {
            Stop();

            int index = 0;
            IDispatcherTimer timer = dispatcher.CreateTimer();

            _timer = timer;
            timer.Interval = frameInterval;
            timer.Tick += (_, _) =>
            {
                if (_timer != timer)
                {
                    // Stopped, or replaced by a newer playback.
                    timer.Stop();

                    return;
                }

                if (index >= _framePngs.Count)
                {
                    timer.Stop();
                    _timer = null;
                    onDone?.Invoke();

                    return;
                }

                target.Source = PreviewImage.Source(_framePngs[index]);
                onFrame?.Invoke(index, _framePngs.Count - 1);
                index++;
            };
            timer.Start();
        }

        /// <summary>Stops playback without running onDone. Safe to call at any time.</summary>
        public void Stop()
        {
            IDispatcherTimer? timer = _timer;

            _timer = null;
            timer?.Stop();
        }
    }
}
