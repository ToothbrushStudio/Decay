using Decay.Core;
using Decay.Renderer;

namespace Decay.App
{
    /// <summary>
    /// Pre-renders a decay sequence into per-frame PNGs (on a background
    /// thread) and plays them into an Image control. Forward = the rot
    /// consuming the carrier (encrypt preview); reverse = the un-rot reveal
    /// (decrypt). Frames are PNG bytes, not ImageSource, so rendering is
    /// thread-safe; sources are materialized on the UI thread at play time.
    /// </summary>
    public sealed class FramePlayer
    {
        private readonly List<byte[]> _framePngs;

        public int FrameCount => _framePngs.Count;

        private FramePlayer(List<byte[]> framePngs)
        {
            _framePngs = framePngs;
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

                frames.Add(DecayPng.Encode(working));

                for (int frame = 0; frame < DecaySequence.FrameCount; frame++)
                {
                    sequence.ApplyFrame(working, frame);
                    frames.Add(DecayPng.Encode(working));
                }

                return new FramePlayer(frames);
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

                frames.Add(DecayPng.Encode(working));

                for (int frame = DecaySequence.FrameCount - 1; frame >= 0; frame--)
                {
                    sequence.RevertFrame(working, frame);
                    frames.Add(DecayPng.Encode(working));
                }

                return new FramePlayer(frames);
            });
        }

        /// <summary>
        /// Plays the frames into the target. onFrame reports (index, total)
        /// each tick — the decrypt page uses it to reveal the message text in
        /// step with the un-rot.
        /// </summary>
        public void Play(
            Image target,
            IDispatcher dispatcher,
            TimeSpan frameInterval,
            Action<int, int>? onFrame = null,
            Action? onDone = null
        )
        {
            int index = 0;
            IDispatcherTimer timer = dispatcher.CreateTimer();

            timer.Interval = frameInterval;
            timer.Tick += (_, _) =>
            {
                if (index >= _framePngs.Count)
                {
                    timer.Stop();
                    onDone?.Invoke();

                    return;
                }

                byte[] png = _framePngs[index];

                target.Source = ImageSource.FromStream(() => new MemoryStream(png));
                onFrame?.Invoke(index, _framePngs.Count - 1);
                index++;
            };
            timer.Start();
        }
    }
}
