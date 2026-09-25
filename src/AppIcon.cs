using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace CodexNetFix
{
    // 图标：白色圆角底板 + 浅蓝渐变盾牌 + 终端提示符（现代简约风）
    public static class AppIcon
    {
        public static Bitmap DrawMaster(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                float s = size / 256f;
                DrawPlate(g, s);
                DrawShield(g, s);
                DrawPrompt(g, s);
                DrawBar(g, s);
            }
            return bmp;
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void DrawPlate(Graphics g, float s)
        {
            RectangleF r = new RectangleF(10 * s, 10 * s, 236 * s, 236 * s);
            using (GraphicsPath p = RoundRect(r, 52 * s))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new PointF(r.Left, r.Top), new PointF(r.Left, r.Bottom),
                    Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 241, 244, 249)))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(255, 226, 230, 238), Math.Max(1f, 2f * s)))
                    g.DrawPath(pen, p);
            }
        }

        static GraphicsPath ShieldPath(float s)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddBezier(128 * s, 46 * s, 128 * s, 46 * s, 190 * s, 70 * s, 190 * s, 70 * s);
            p.AddLine(190 * s, 70 * s, 190 * s, 118 * s);
            p.AddBezier(190 * s, 118 * s, 190 * s, 170 * s, 166 * s, 194 * s, 128 * s, 212 * s);
            p.AddBezier(128 * s, 212 * s, 90 * s, 194 * s, 66 * s, 170 * s, 66 * s, 118 * s);
            p.AddLine(66 * s, 118 * s, 66 * s, 70 * s);
            p.AddBezier(66 * s, 70 * s, 128 * s, 46 * s, 128 * s, 46 * s, 128 * s, 46 * s);
            p.CloseFigure();
            return p;
        }

        static void DrawShield(Graphics g, float s)
        {
            using (GraphicsPath p = ShieldPath(s))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new PointF(100 * s, 46 * s), new PointF(156 * s, 212 * s),
                    Color.FromArgb(255, 137, 187, 249), Color.FromArgb(255, 82, 140, 244)))
                {
                    ColorBlend cb = new ColorBlend(3);
                    cb.Colors = new Color[] { Color.FromArgb(255, 158, 200, 252), Color.FromArgb(255, 108, 166, 246), Color.FromArgb(255, 74, 130, 240) };
                    cb.Positions = new float[] { 0f, 0.55f, 1f };
                    b.InterpolationColors = cb;
                    g.FillPath(b, p);
                }
                using (Pen pen = new Pen(Color.FromArgb(70, 255, 255, 255), 2.5f * s))
                    g.DrawPath(pen, p);
            }
        }

        static void DrawPrompt(Graphics g, float s)
        {
            using (Font f = new Font("Consolas", 46f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(">_", f, b, new RectangleF(74 * s, 88 * s, 108 * s, 56 * s), sf);
            }
        }

        static void DrawBar(Graphics g, float s)
        {
            RectangleF bar = new RectangleF(94 * s, 156 * s, 68 * s, 12 * s);
            using (GraphicsPath p = RoundRect(bar, 6 * s))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                g.FillPath(b, p);
        }

        static byte[] EncodePng(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        static byte[] EncodeDib(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            int maskStride = ((w + 31) / 32) * 4;
            int xorSize = w * h * 4;
            int andSize = maskStride * h;
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(40); bw.Write(w); bw.Write(h * 2); bw.Write((short)1); bw.Write((short)32);
            bw.Write(0); bw.Write(xorSize + andSize); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] row = new byte[w * 4];
            for (int y = h - 1; y >= 0; y--)
            {
                System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(bd.Scan0, y * bd.Stride), row, 0, w * 4);
                bw.Write(row);
            }
            bmp.UnlockBits(bd);
            bw.Write(new byte[andSize]);
            bw.Flush();
            return ms.ToArray();
        }

        public static void WriteIco(string path, int[] sizes)
        {
            List<KeyValuePair<int, byte[]>> entries = new List<KeyValuePair<int, byte[]>>();
            foreach (int size in sizes)
            {
                using (Bitmap master = DrawMaster(256))
                using (Bitmap scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawImage(master, new Rectangle(0, 0, size, size));
                    }
                    byte[] data = (size <= 64) ? EncodeDib(scaled) : EncodePng(scaled);
                    entries.Add(new KeyValuePair<int, byte[]>(size, data));
                }
            }
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((short)0); bw.Write((short)1); bw.Write((short)entries.Count);
                int offset = 6 + entries.Count * 16;
                foreach (KeyValuePair<int, byte[]> e in entries)
                {
                    int size = e.Key;
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((short)1); bw.Write((short)32);
                    bw.Write(e.Value.Length);
                    bw.Write(offset);
                    offset += e.Value.Length;
                }
                foreach (KeyValuePair<int, byte[]> e in entries) bw.Write(e.Value);
            }
        }

        public static void SavePreview(string path, int size)
        {
            using (Bitmap b = DrawMaster(size)) b.Save(path, ImageFormat.Png);
        }
    }
}