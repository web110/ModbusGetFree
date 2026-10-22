using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Regenerate document.png / document.ico from this editable drawing.
class GenerateDocumentIcon
{
    static byte[] Draw(int size)
    {
        using (var bitmap = new Bitmap(size, size))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.ScaleTransform(size / 32f, size / 32f);
            var outline = new[] { new PointF(5, 2), new PointF(22, 2), new PointF(27, 7), new PointF(27, 24), new PointF(20, 30), new PointF(5, 30) };
            graphics.FillPolygon(Brushes.White, outline);
            graphics.DrawPolygon(Pens.Black, outline);
            graphics.FillPolygon(Brushes.Gainsboro, new[] { new PointF(22, 2), new PointF(22, 8), new PointF(27, 8) });
            graphics.DrawLines(Pens.Gray, new[] { new PointF(22, 2), new PointF(22, 8), new PointF(27, 8) });
            graphics.FillPolygon(Brushes.Gainsboro, new[] { new PointF(20, 30), new PointF(20, 24), new PointF(27, 24) });
            graphics.DrawLines(Pens.Gray, new[] { new PointF(20, 30), new PointF(20, 24), new PointF(27, 24) });
            using (var font = new Font("Arial", 6, FontStyle.Regular, GraphicsUnit.Pixel))
                graphics.DrawString("DOC1", font, Brushes.Firebrick, 6, 7);
            graphics.DrawLine(Pens.Gray, 7, 16, 24, 16);
            graphics.DrawLine(Pens.Gray, 7, 20, 24, 20);
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
    static void Main(string[] args)
    {
        int[] sizes = { 16, 20, 24, 32, 48, 64 };
        byte[][] images = Array.ConvertAll(sizes, Draw);
        using (var stream = File.Create(Path.Combine(args[0], "document.ico")))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)sizes[i]); writer.Write((byte)sizes[i]); writer.Write((ushort)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (byte[] bytes in images) writer.Write(bytes);
        }
        File.WriteAllBytes(Path.Combine(args[0], "document.png"), Draw(64));
        using (var icon = new Icon(Path.Combine(args[0], "document.ico")))
            Console.WriteLine("Document icon generated: " + icon.Size);
    }
}
