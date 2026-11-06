using System;
using System.IO;
using System.Linq;
using ModbusGetFree;
class MbpImportTests
{
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static void Reject(string file) { try { MbpImporter.Load(file); } catch (IOException) { checks++; return; } throw new Exception("Malformed file accepted: " + file); }
    static int Main(string[] args)
    {
        try
        {
            string root = args[0], fixtures = Path.Combine(root, "tests/fixtures/mbp-poll922");
            var h = MbpImporter.Load(Path.Combine(fixtures, "HMT_pH.mbp"));
            Check(h.Address == 2089 && h.Quantity == 10 && h.Function == 3 && h.Unit == 1 && h.ScanRate == 1000, "HMT configuration");
            Check(h.Names[2] == "pH", "HMT alias");
            Check(h.Definitions.Count == 7 && h.Definitions.Where(d => d.Format == 6).Count() == 3, "HMT mixed groups");
            Check(h.Definitions[2].Order == 1, "HMT word swap");
            string[] names = { "signed16", "unsigned16", "hex16", "binary16", "signed32", "unsigned32", "signed64", "unsigned64", "float32", "double64" };
            int[] formats = { 0, 1, 2, 3, 4, 5, 7, 8, 6, 9 };
            int[] orders = { 0, 3, 2, 1 };
            // Known nonzero wire vectors in canonical big-endian order, independent of ValueCodec.Parse.
            string[] hex = { "FFFE", "FEDC", "4142", "A55A", "F8A432EB", "EE6B2801", "EEDDEF0B82167EEB", "AB54A98CEB1F0AD2", "47F12000", "C0FE240800000000" };
            string[] expected = { "-2", "65244", "(AB) 0x4142", "1010010101011010", "-123456789", "4000000001", "-1234567890123456789", "12345678901234567890", "123456", "-123456.5" };
            for (int t=0; t<names.Length; t++) for (int o=0; o<(t<4?1:4); o++)
            {
                string path = Path.Combine(fixtures, names[t]+(t<4?"":"-"+o)+".mbp");
                var state=MbpImporter.Load(path); var d=state.Definitions[0];
                Check(d.Format==formats[t] && d.Order==orders[o], "Mapping: "+path);
                int width=hex[t].Length/4; var words=new ushort[width];
                for(int k=0;k<width;k++)
                {
                    ushort w=Convert.ToUInt16(hex[t].Substring(k*4,4),16);
                    if(o==1 || o==2) w=(ushort)((w>>8)|(w<<8));
                    words[o==1 || o==3 ? width-1-k:k]=w;
                }
                Check(ValueCodec.Format(words,0,d.Format,d.Order,1,0)==expected[t], "Nonzero decode: "+path);
            }
            string tmp=Path.Combine(args[1],"malformed.mbp");
            byte[] data=File.ReadAllBytes(Path.Combine(fixtures,"signed16.mbp"));
            Array.Copy(BitConverter.GetBytes(5),0,data,24+9*4,4); File.WriteAllBytes(tmp,data); Reject(tmp);
            Array.Copy(BitConverter.GetBytes(16),0,data,24+9*4,4); File.WriteAllBytes(tmp,data); Reject(tmp);
            Array.Copy(BitConverter.GetBytes(999),0,data,24+9*4,4); File.WriteAllBytes(tmp,data); Reject(tmp);
            File.WriteAllBytes(tmp,data.Take(100).ToArray()); Reject(tmp);
            Console.WriteLine("PASS MBP import: "+checks+" checks, 28 formats and HMT fixture"); return 0;
        }
        catch(Exception e) { Console.WriteLine(e); return 1; }
    }
}
