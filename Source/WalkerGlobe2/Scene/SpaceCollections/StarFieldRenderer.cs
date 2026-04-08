using System;
using System.Drawing;
using System.Drawing.Imaging;
using WalkerGlobe2.Core;
using WalkerGlobe2.Renderer;

namespace WalkerGlobe2.Scene
{
    /// <summary>
    /// Renders a static star field using billboards on a large sphere.
    /// Stars have fixed pixel size regardless of zoom level.
    /// </summary>
    public class StarFieldRenderer : IRenderable, IDisposable
    {
        private const int DefaultStarCount = 2000;
        private const double StarSphereRadius = 5.0e8; // meters — direction matters, not distance

        public StarFieldRenderer(Context context, int starCount = DefaultStarCount, int seed = 42)
        {
            _billboards = new BillboardCollection(context, starCount, false);
            _billboards.DepthTestEnabled = false;
            _billboards.DepthWrite = false;
            _billboards.Texture = CreateStarTexture();

            var rng = new Random(seed);
            for (int i = 0; i < starCount; i++)
            {
                // Uniform random distribution on sphere
                double z = 2.0 * rng.NextDouble() - 1.0;
                double theta = 2.0 * Math.PI * rng.NextDouble();
                double r = Math.Sqrt(1.0 - z * z);
                var pos = new Vector3D(
                    r * Math.Cos(theta),
                    r * Math.Sin(theta),
                    z) * StarSphereRadius;

                // Vary brightness
                int brightness = 140 + rng.Next(116); // 140–255
                var star = new Billboard
                {
                    Position = pos,
                    Color = Color.FromArgb(brightness, brightness, brightness)
                };
                _billboards.Add(star);
            }
        }

        private static Texture2D CreateStarTexture()
        {
            // Small soft circle — billboard size in pixels = texture size
            const int size = 4;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            double center = (size - 1) / 2.0;
            double maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = x - center;
                    double dy = y - center;
                    double dist = Math.Sqrt(dx * dx + dy * dy) / maxDist;
                    int alpha = dist <= 1.0 ? (int)(255 * (1.0 - dist * dist)) : 0;
                    bmp.SetPixel(x, y, Color.FromArgb(alpha, 255, 255, 255));
                }
            }

            return Device.CreateTexture2D(bmp, TextureFormat.RedGreenBlueAlpha8, false);
        }

        public void Render(Context context, SceneState sceneState)
        {
            _billboards.Render(context, sceneState);
        }

        public void Dispose()
        {
            _billboards.Dispose();
        }

        private readonly BillboardCollection _billboards;
    }
}
