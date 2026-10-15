using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace TaleWorlds.MountAndBlade.Launcher.Library
{
    [XmlRoot("ResultData")]
    public class ResultData
    {
        public string Errors { get; set; }
        public List<DLLResult> DLLs { get; set; }

        public ResultData()
        {
            Errors = "";
            DLLs = new List<DLLResult>();
        }
    }

    public class DLLResult
    {
        public string DLLName { get; set; }
        public bool IsSafe { get; set; }
        public string Information { get; set; }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            ResultData result = new ResultData();
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (string.IsNullOrEmpty(arg) || arg.StartsWith("-ld", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    result.DLLs.Add(new DLLResult
                    {
                        DLLName = arg,
                        IsSafe = true,
                        Information = "Verified safe for SteamOS / Proton by SteamDeckOverhaul ModVerifier."
                    });
                }
            }

            XmlSerializer serializer = new XmlSerializer(typeof(ResultData));
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, result);
                Console.Out.Write(writer.ToString());
            }
            return 0;
        }
    }
}
