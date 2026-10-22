using System;using System.IO;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Collections.Generic;
class GenerateMbgfIcon
{
 static Bitmap Draw(int n)
 {
  var b=new Bitmap(n,n);
  using(var g=Graphics.FromImage(b))
  {
   g.Clear(Color.FromArgb(17,38,65));g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   using(var pen=new Pen(Color.FromArgb(29,211,176),Math.Max(1,n/64f)))
   {g.DrawLine(pen,n*.12f,n*.5f,n*.88f,n*.5f);g.DrawLine(pen,n*.5f,n*.12f,n*.5f,n*.88f);}
   using(var font=new Font("Segoe UI",n*.32f,FontStyle.Bold,GraphicsUnit.Pixel))
   using(var format=new StringFormat(StringFormat.GenericTypographic))
   {
    format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;format.FormatFlags|=StringFormatFlags.NoWrap;
    string[] letters={"M","B","G","F"};
    for(int k=0;k<4;k++)using(var brush=new SolidBrush(k==0||k==3?Color.FromArgb(29,211,176):Color.White))
     g.DrawString(letters[k],font,brush,new RectangleF((k%2)*n*.5f,(k/2)*n*.5f,n*.5f,n*.5f),format);
   }
  }
  return b;
 }
 static void Main(string[] args)
 {
  Directory.CreateDirectory(args[0]);var images=new List<byte[]>();int[] sizes={16,24,32,48,64,128,256};
  foreach(int n in sizes)using(var b=Draw(n))using(var ms=new MemoryStream())
  {b.Save(ms,ImageFormat.Png);images.Add(ms.ToArray());if(n==256)b.Save(Path.Combine(args[0],"app.png"),ImageFormat.Png);}
  using(var w=new BinaryWriter(File.Create(Path.Combine(args[0],"app.ico"))))
  {
   w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
   for(int k=0;k<sizes.Length;k++)
   {w.Write((byte)(sizes[k]==256?0:sizes[k]));w.Write((byte)(sizes[k]==256?0:sizes[k]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(images[k].Length);w.Write(offset);offset+=images[k].Length;}
   foreach(var bytes in images)w.Write(bytes);
  }
 }
}
