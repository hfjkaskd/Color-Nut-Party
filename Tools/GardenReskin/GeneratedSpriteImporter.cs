// Mechanical image import only: crop generated art, preserve alpha, resize.
// No colour transforms, painting, shape generation or Unity asset edits.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace GardenReskin
{
    public sealed class SpriteImportResult
    {
        public byte[] Png;
        public int SourceWidth, SourceHeight, OutputWidth, OutputHeight;
        public int[] SourceCell, SourceBounds, OriginalAlphaBounds, DestinationSlicePixels;
        public int LargestComponentPixels, RetainedComponents;
        public bool NineSlice, PreservedOriginalAlpha;
    }

    public static class GeneratedSpriteImporter
    {
        private static Bitmap Load(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var image = Image.FromStream(stream))
            {
                var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(result))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(image, 0, 0);
                }
                return result;
            }
        }

        private static byte[] Pixels(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var data = new byte[bitmap.Width * bitmap.Height * 4];
                for (int y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), data, y * bitmap.Width * 4, bitmap.Width * 4);
                return data;
            }
            finally { bitmap.UnlockBits(bits); }
        }

        private static Rectangle AlphaBounds(Bitmap bitmap)
        {
            var pixels = Pixels(bitmap);
            int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    if (pixels[(y * bitmap.Width + x) * 4 + 3] != 0)
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y);
                        right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                    }
            if (right < left) throw new InvalidDataException("Original PNG is fully transparent; destination bounds are undefined.");
            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        // Eight-connected alpha components avoid breaking antialiased diagonal edges.
        private static Rectangle SubjectAlphaBounds(Bitmap bitmap, Rectangle cell, int threshold, int padding, out int componentPixels, out int retainedComponents)
        {
            var pixels = Pixels(bitmap);
            var visited = new bool[cell.Width * cell.Height];
            var queue = new int[visited.Length];
            int bestCount = 0;
            var componentBounds = new List<Rectangle>();
            var componentCounts = new List<int>();
            for (int start = 0; start < visited.Length; start++)
            {
                if (visited[start]) continue;
                visited[start] = true;
                int sx = start % cell.Width, sy = start / cell.Width;
                if (pixels[((cell.Y + sy) * bitmap.Width + cell.X + sx) * 4 + 3] <= threshold) continue;
                int head = 0, tail = 1, left = sx, right = sx, top = sy, bottom = sy;
                queue[0] = start;
                while (head < tail)
                {
                    int current = queue[head++];
                    int x = current % cell.Width, y = current / cell.Width;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= cell.Width || ny >= cell.Height || (dx == 0 && dy == 0)) continue;
                            int index = ny * cell.Width + nx;
                            if (visited[index]) continue;
                            visited[index] = true;
                            if (pixels[((cell.Y + ny) * bitmap.Width + cell.X + nx) * 4 + 3] > threshold)
                                queue[tail++] = index;
                        }
                }
                bestCount = Math.Max(bestCount, tail);
                componentBounds.Add(Rectangle.FromLTRB(cell.X + left, cell.Y + top, cell.X + right + 1, cell.Y + bottom + 1));
                componentCounts.Add(tail);
            }
            componentPixels = bestCount;
            if (bestCount == 0) throw new InvalidDataException("Generated cell has no alpha component above the configured threshold.");
            int minimumComponent = Math.Min(bestCount, Math.Max(64, (int)Math.Ceiling(bestCount * 0.03)));
            Rectangle best = Rectangle.Empty;
            retainedComponents = 0;
            for (int i = 0; i < componentBounds.Count; i++)
                if (componentCounts[i] >= minimumComponent)
                {
                    best = retainedComponents == 0 ? componentBounds[i] : Rectangle.Union(best, componentBounds[i]);
                    retainedComponents++;
                }
            return Rectangle.FromLTRB(Math.Max(cell.Left, best.Left - padding), Math.Max(cell.Top, best.Top - padding),
                Math.Min(cell.Right, best.Right + padding), Math.Min(cell.Bottom, best.Bottom + padding));
        }

        private static int[] RectArray(Rectangle rectangle)
        {
            return new[] { rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height };
        }

        private static void Draw(Graphics graphics, Bitmap source, Rectangle from, Rectangle to)
        {
            if (from.Width <= 0 || from.Height <= 0 || to.Width <= 0 || to.Height <= 0) return;
            using (var attributes = new ImageAttributes())
            {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(source, to, from.X, from.Y, from.Width, from.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private static int Round(double value) { return (int)Math.Round(value, MidpointRounding.AwayFromZero); }
        private static int Clamp(int value, int low, int high) { return Math.Max(low, Math.Min(value, high)); }

        private static void ApplyOriginalAlpha(Bitmap output, Bitmap original)
        {
            var originalPixels = Pixels(original);
            var outputPixels = Pixels(output);
            for (int i = 3; i < outputPixels.Length; i += 4)
                outputPixels[i] = (byte)((outputPixels[i] * originalPixels[i] + 127) / 255);
            var bits = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < output.Height; y++)
                    Marshal.Copy(outputPixels, y * output.Width * 4, IntPtr.Add(bits.Scan0, y * bits.Stride), output.Width * 4);
            }
            finally { output.UnlockBits(bits); }
        }

        public static SpriteImportResult Build(byte[] originalPng, string generatedPath, string mode,
            int columns, int rows, int cellIndex, int threshold, int padding, double[] oldBorder, double[] sourceSliceRatio, bool preserveOriginalAlpha)
        {
            if (threshold < 0 || threshold > 254 || padding < 0) throw new ArgumentOutOfRangeException("Invalid threshold or padding.");
            if (oldBorder == null || oldBorder.Length != 4 || sourceSliceRatio == null || sourceSliceRatio.Length != 4)
                throw new ArgumentException("Borders and source ratios use four values: left, bottom, right, top.");
            for (int i = 0; i < 4; i++)
                if (oldBorder[i] < 0 || sourceSliceRatio[i] < 0 || sourceSliceRatio[i] >= 0.5)
                    throw new ArgumentException("Borders must be nonnegative; source ratios must be in [0, 0.5).");
            using (var original = Load(originalPng))
            using (var source = Load(File.ReadAllBytes(generatedPath)))
            using (var output = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb))
            {
                bool full = mode == "full";
                if (!full && mode != "grid") throw new ArgumentException("mode must be grid or full.");
                if (!full && (columns < 1 || rows < 1 || cellIndex < 0 || cellIndex >= columns * rows || columns > source.Width || rows > source.Height))
                    throw new ArgumentException("Invalid grid columns, rows or zero-based cell.");
                Rectangle sourceCell = new Rectangle(0, 0, source.Width, source.Height);
                if (!full)
                {
                    int column = cellIndex % columns, row = cellIndex / columns;
                    sourceCell = Rectangle.FromLTRB(column * source.Width / columns, row * source.Height / rows,
                        (column + 1) * source.Width / columns, (row + 1) * source.Height / rows);
                }
                int componentPixels = 0, retainedComponents = 0;
                Rectangle sourceBounds = full ? sourceCell : SubjectAlphaBounds(source, sourceCell, threshold, padding, out componentPixels, out retainedComponents);
                Rectangle destination = full ? new Rectangle(0, 0, original.Width, original.Height) : AlphaBounds(original);
                var destinationBorders = new int[4];
                bool nineSlice = !full && (oldBorder[0] > 0 || oldBorder[1] > 0 || oldBorder[2] > 0 || oldBorder[3] > 0);
                using (var graphics = Graphics.FromImage(output))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    if (!nineSlice) Draw(graphics, source, sourceBounds, destination);
                    else
                    {
                        if (oldBorder[0] + oldBorder[2] > original.Width + 0.01 || oldBorder[1] + oldBorder[3] > original.Height + 0.01)
                            throw new InvalidDataException("Original borders exceed the PNG canvas dimensions; specify a valid oldBorder override.");
                        // Unity measures borders from the entire canvas. Subtract the
                        // original transparent insets before fitting to its alpha rect.
                        int dl = Clamp(Round(oldBorder[0]) - destination.Left, 0, destination.Width);
                        int db = Clamp(Round(oldBorder[1]) - (original.Height - destination.Bottom), 0, destination.Height);
                        int dr = Clamp(Round(oldBorder[2]) - (original.Width - destination.Right), 0, destination.Width - dl);
                        int dt = Clamp(Round(oldBorder[3]) - destination.Top, 0, destination.Height - db);
                        destinationBorders = new[] { dl, db, dr, dt };
                        int sl = dl > 0 ? Math.Max(1, Round(sourceBounds.Width * sourceSliceRatio[0])) : 0;
                        int sb = db > 0 ? Math.Max(1, Round(sourceBounds.Height * sourceSliceRatio[1])) : 0;
                        int sr = dr > 0 ? Math.Max(1, Round(sourceBounds.Width * sourceSliceRatio[2])) : 0;
                        int st = dt > 0 ? Math.Max(1, Round(sourceBounds.Height * sourceSliceRatio[3])) : 0;
                        if (sl + sr >= sourceBounds.Width || st + sb >= sourceBounds.Height)
                            throw new InvalidDataException("Generated source is too small for the requested slices.");
                        int[] sx = { sourceBounds.Left, sourceBounds.Left + sl, sourceBounds.Right - sr, sourceBounds.Right };
                        int[] sy = { sourceBounds.Top, sourceBounds.Top + st, sourceBounds.Bottom - sb, sourceBounds.Bottom };
                        int[] dx = { destination.Left, destination.Left + dl, destination.Right - dr, destination.Right };
                        int[] dy = { destination.Top, destination.Top + dt, destination.Bottom - db, destination.Bottom };
                        for (int y = 0; y < 3; y++)
                            for (int x = 0; x < 3; x++)
                                Draw(graphics, source, Rectangle.FromLTRB(sx[x], sy[y], sx[x + 1], sy[y + 1]),
                                    Rectangle.FromLTRB(dx[x], dy[y], dx[x + 1], dy[y + 1]));
                    }
                }
                if (preserveOriginalAlpha) ApplyOriginalAlpha(output, original);
                using (var encoded = new MemoryStream())
                {
                    output.Save(encoded, ImageFormat.Png);
                    return new SpriteImportResult {
                        Png = encoded.ToArray(), SourceWidth = source.Width, SourceHeight = source.Height,
                        OutputWidth = output.Width, OutputHeight = output.Height,
                        SourceCell = RectArray(sourceCell), SourceBounds = RectArray(sourceBounds),
                        OriginalAlphaBounds = RectArray(destination), DestinationSlicePixels = destinationBorders,
                        LargestComponentPixels = componentPixels, RetainedComponents = retainedComponents,
                        NineSlice = nineSlice, PreservedOriginalAlpha = preserveOriginalAlpha
                    };
                }
            }
        }
    }
}
