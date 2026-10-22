using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ModbusGetFree;
class IconAssetsTests
{
    static int count;
    static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Console.WriteLine("PASS: "+message);}
    static void Main(string[] args)
    {
        string active=Path.Combine(args[0],"assets","icons","active");
        int[] ids={57600,57601,57603,57607,32801,32771,32773,32838,32854,32842,32844,32846,32848,32894,32849,32839,32826,32825,32936,57664,57670};
        foreach(int id in ids)
        {
            using(var image=ClassicIcons.Create(id,active))
                Check(image.Width==18&&image.Height==18&&File.Exists(Path.Combine(active,ClassicIcons.FileName(id))),"active icon "+id);
        }
        string temp=Path.GetFullPath(args[1]);Directory.CreateDirectory(temp);
        using(var fallback=ClassicIcons.Create(57600,temp))Check(fallback.Width==18,"missing asset uses built-in fallback");
        string file=Path.Combine(temp,"new.png");
        using(var image=new Bitmap(24,24)){image.SetPixel(0,0,Color.Magenta);image.Save(file,ImageFormat.Png);}
        using(var image=ClassicIcons.Create(57600,temp))Check(image.Width==24&&image.GetPixel(0,0).ToArgb()==Color.Magenta.ToArgb(),"replacement PNG is used");
        using(var locked=File.Open(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(locked.Length>0,"loaded PNG is not left locked");
        File.WriteAllText(file,"invalid PNG");
        using(var fallback=ClassicIcons.Create(57600,temp))Check(fallback.Width==18,"corrupt PNG uses built-in fallback");
        Console.WriteLine("Checks: "+count+"; Exit: 0");
    }
}
