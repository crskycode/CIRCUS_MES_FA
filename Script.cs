using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#pragma warning disable IDE0063

namespace CIRCUS_MES_FA
{
    public class Script
    {
        public void Load(string filePath)
        {
            using (var reader = new BinaryReader(File.OpenRead(filePath)))
            {
                Read(reader);
            }
        }

        public void Save(string filePath)
        {
            using (var writer = new BinaryWriter(File.Create(filePath)))
            {
                writer.Write(_jumpList.Count);

                foreach (var e in _jumpList)
                {
                    writer.Write(e);
                }

                writer.Write(_codeBlock);

                writer.Flush();
            }
        }

        List<int> _jumpList;
        byte[] _codeBlock;
        Assembly _assembly;

        void Read(BinaryReader reader)
        {
            // Read jump list

            var count = reader.ReadInt32();

            _jumpList = new List<int>(count);

            for (var i = 0; i < count; i++)
            {
                _jumpList.Add(reader.ReadInt32());
            }

            // Read code block

            _codeBlock = reader.ReadBytes(0x30D40);

            // Parse code

            Parse();
        }

        void Parse()
        {
            var stream = new MemoryStream(_codeBlock);
            var reader = new BinaryReader(stream);

            var version = reader.ReadByte();
            var type = reader.ReadByte();

            Debug.WriteLine($"Version: {version}");
            Debug.WriteLine($"Type: {type}");

            _assembly = new Assembly();

            while (stream.Position < stream.Length)
            {
                var inst = Operation.Unknow;

                var addr = Convert.ToInt32(stream.Position);

                var code = reader.ReadByte();

                if (code >= 0x29)
                {
                    if (code >= 0x2F)
                    {
                        if (code >= 0x4C)
                        {
                            if (code >= 0x50)
                            {
                                reader.ReadUInt16();
                                reader.ReadUInt16();
                                reader.ReadUInt16();
                                reader.ReadUInt16();
                            }
                            else
                            {
                                // Encrypted
                                inst = Operation.LoadEncryptString;
                                reader.ReadCString();
                            }
                        }
                        else
                        {
                            reader.ReadCString();
                        }
                    }
                    else
                    {
                        reader.ReadByte();
                        reader.ReadCString();
                    }
                }
                else
                {
                    reader.ReadByte();
                    reader.ReadByte();
                }

                var length = Convert.ToInt32(stream.Position) - addr;

                _assembly.Add(addr, length, inst);
            }

            if (_assembly.BytesLength != stream.Length - 2)
            {
                throw new Exception("Parsing failed");
            }
        }

        Instruct[] GetStringInstructs()
        {
            return _assembly.Instructs
                .Where(a => a.Op == Operation.LoadEncryptString)
                .ToArray();
        }

        public void ExportText(string filePath)
        {
            var inst = GetStringInstructs();

            if (inst.Length == 0)
            {
                // No string in this script
                return;
            }

            var encoding = Encoding.GetEncoding("shift_jis");

            // Create output text file
            using var writer = File.CreateText(filePath);

            foreach (var e in inst)
            {
                if (e.Length <= 2)
                {
                    // Skip empty string
                    continue;
                }

                // Copy the string bytes to buffer
                var length = e.Length - 2;
                var bytes = new byte[length];
                Array.Copy(_codeBlock, e.Addr + 1, bytes, 0, length);

                // Only the encrypted string here
                DecryptString(bytes);

                var s = encoding.GetString(bytes);

                s = s.Replace("\r", "\\r");
                s = s.Replace("\n", "\\n");

                // Write out text
                writer.WriteLine($"◇{e.Addr:X8}◇{s}");
                writer.WriteLine($"◆{e.Addr:X8}◆{s}");
                writer.WriteLine();
            }

            writer.Flush();
        }

        public void ImportText(string filePath, Encoding encoding)
        {
            var inst = GetStringInstructs();

            if (inst.Length == 0)
            {
                // No string in this script
                return;
            }

            var translated = new Dictionary<int, string>();

            // Read translation file
            using (var reader = new StreamReader(filePath))
            {
                var _lineNo = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var lineNo = _lineNo++;

                    if (line.Length == 0 || line[0] != '◆')
                    {
                        // Skip empty line and original line
                        continue;
                    }

                    // Get input
                    var m = Regex.Match(line, "◆(.+?)◆(.+$)");

                    // Check format
                    if (!m.Success || m.Groups.Count != 3)
                    {
                        throw new Exception($"Bad format at line: {lineNo}");
                    }

                    // Get address and string
                    var addr = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                    var s = m.Groups[2].Value;

                    s = s.Replace("\\r", "\r");
                    s = s.Replace("\\n", "\n");

                    translated.Add(addr, s);
                }
            }

            if (translated.Count == 0)
            {
                // No translated string loaded
                return;
            }

            // Rebuild the code block

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            // Version & Type
            writer.Write(_codeBlock, 0, 2);

            // Write instructs
            foreach (var e in _assembly.Instructs)
            {
                e.NewAddr = Convert.ToInt32(stream.Position);

                // Find translated string
                if (e.Op == Operation.LoadEncryptString &&
                   translated.TryGetValue(e.Addr, out string s))
                {
                    // Write translated string
                    var bytes = encoding.GetBytes(s);

                    EncryptString(bytes);

                    writer.Write(_codeBlock[e.Addr]); // Opcode
                    writer.Write(bytes);
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write(_codeBlock, e.Addr, e.Length);
                }

                // Update jump list
                var jumpIndex = _jumpList.FindIndex(a => (a & 0x7fffffff) == e.Addr);
                if (jumpIndex != -1)
                {
                    var addrVal = (uint)(_jumpList[jumpIndex] & 0x80000000);
                    addrVal |= (uint)e.NewAddr & 0x7fffffff;
                    _jumpList[jumpIndex] = (int)addrVal;
                }
            }

            writer.Flush();

            _codeBlock = stream.ToArray();
        }

        static void DecryptString(byte[] bytes)
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] += 0x20;
            }
        }

        static void EncryptString(byte[] bytes)
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x20)
                {
                    bytes[i] = 0x24;
                }

                bytes[i] -= 0x20;
            }
        }

        enum Operation
        {
            Unknow,
            LoadEncryptString
        }

        class Instruct
        {
            public int Addr { get; }
            public int NewAddr { get; set; }
            public int Length { get; }
            public Operation Op { get; }

            public Instruct(int address, int length, Operation op)
            {
                Addr = address;
                Length = length;
                Op = op;
            }
        }

        class Assembly
        {
            public List<Instruct> Instructs { get; } = new();


            public void Add(int address, int length, Operation op)
            {
                Instructs.Add(new Instruct(address, length, op));
            }

            public void Clear()
            {
                Instructs.Clear();
            }

            public int BytesLength
            {
                get => Instructs.Sum(a => a.Length);
            }
        }
    }
}
