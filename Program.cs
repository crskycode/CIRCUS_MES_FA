using System;
using System.Text;

namespace CIRCUS_MES_FA
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args.Length < 2)
            {
                Console.WriteLine("CIRCUS MES Tool");
                Console.WriteLine("  -- Created by Crsky");
                Console.WriteLine("Usage:");
                Console.WriteLine("  Export text     : ScriptTool -e [file|folder]");
                Console.WriteLine("  Rebuild script  : ScriptTool -b shift_jis [file|folder]");
                Console.WriteLine();
                Console.WriteLine("Note:");
                Console.WriteLine("  This tool works with old version of Circus engine.");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            var mode = args[0];

            switch (mode)
            {
                case "-e":
                {
                    var path = Path.GetFullPath(args[1]);

                    static void ExportText(string filePath)
                    {
                        Console.WriteLine($"Exporting text from {Path.GetFileName(filePath)}");

                        try
                        {
                            var script = new Script();
                            script.Load(filePath);
                            script.ExportText(Path.ChangeExtension(filePath, "txt"));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                    if (Utility.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*.mes"))
                        {
                            ExportText(item);
                        }
                    }
                    else
                    {
                        ExportText(path);
                    }

                    break;
                }
                case "-b":
                {
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Not enough parameters.");
                        return;
                    }

                    var encoding = Encoding.GetEncoding(args[1]);
                    var path = Path.GetFullPath(args[2]);

                    void RebuildScript(string filePath)
                    {
                        Console.WriteLine($"Rebuilding script {Path.GetFileName(filePath)}");

                        try
                        {
                            string textFilePath = Path.ChangeExtension(filePath, "txt");
                            string newFilePath = Path.Combine(Path.GetDirectoryName(filePath), "rebuild", Path.GetFileName(filePath));
                            Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                            var script = new Script();
                            script.Load(filePath);
                            script.ImportText(textFilePath, encoding);
                            script.Save(newFilePath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                    if (Utility.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*.mes"))
                        {
                            RebuildScript(item);
                        }
                    }
                    else
                    {
                        RebuildScript(path);
                    }

                    break;
                }
            }
        }
    }
}
