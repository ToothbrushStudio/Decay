using Decay.Core;
using Decay.Renderer;

namespace Decay.App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // The title is the watermark: same glyphs, same plate, rendered
            // by the same engine code that marks Free exports.
            byte[] badge = DecayPng.Encode(Watermark.RenderBadge(scale: 3));

            TitleBanner.Source = ImageSource.FromStream(() => new MemoryStream(badge));
        }
    }
}
