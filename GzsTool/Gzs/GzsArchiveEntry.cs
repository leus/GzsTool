﻿using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using GzsTool.Utility;

namespace GzsTool.Gzs
{
    [XmlType("Entry")]
    public class GzsArchiveEntry
    {
        [XmlAttribute("Hash")]
        public ulong Hash { get; set; }

        [XmlIgnore]
        public bool FileNameFound { get; set; }

        [XmlIgnore]
        public uint Offset { get; set; }

        [XmlIgnore]
        public uint Size { get; set; }

        [XmlAttribute("FilePath")]
        public string FilePath { get; set; }

        public bool ShouldSerializeHash()
        {
            return FileNameFound == false;
        }

        public static GzsArchiveEntry ReadGzArchiveEntry(Stream input)
        {
            GzsArchiveEntry gzsArchiveEntry = new GzsArchiveEntry();
            gzsArchiveEntry.Read(input);
            return gzsArchiveEntry;
        }

        public void Read(Stream input)
        {
            BinaryReader reader = new BinaryReader(input, Encoding.Default, true);
            Hash = reader.ReadUInt64();
            Offset = reader.ReadUInt32();
            Size = reader.ReadUInt32();
            string filePath;
            FileNameFound = TryGetFilePath(out filePath);
            FilePath = filePath;
        }

        public void ExportFile(Stream input, string outputDirectory)
        {
            var data = ReadFile(input);

            string outputFilePath = GetOutputPath(outputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
            using (FileStream output = new FileStream(outputFilePath, FileMode.Create))
            {
                output.Write(data, 0, data.Length);
            }
        }

        private string GetOutputPath(string outputDirectory)
        {
            string filePath = FilePath;
            if (filePath.StartsWith("/"))
                filePath = filePath.Substring(1, filePath.Length - 1);
            filePath = filePath.Replace("/", "\\");
            return Path.Combine(outputDirectory, filePath);
        }

        private bool TryGetFilePath(out string filePath)
        {
            int fileExtensionId = (int) (Hash >> 52 & 0xFFFF);
            bool fileNameFound = Hashing.TryGetFileNameFromHash(Hash, fileExtensionId, out filePath);
            return fileNameFound;
        }

        private byte[] ReadFile(Stream input)
        {
            BinaryReader reader = new BinaryReader(input, Encoding.Default, true);
            uint dataOffset = 16*Offset;
            input.Seek(dataOffset, SeekOrigin.Begin);
            byte[] data = reader.ReadBytes((int) Size);
            data = Encryption.DeEncryptQar(data, Offset);
            const uint keyConstant = 0xA0F8EFE6;
            uint peekData = BitConverter.ToUInt32(data, 0);
            if (peekData == keyConstant)
            {
                uint key = BitConverter.ToUInt32(data, 4);
                Size -= 8;
                byte[] data2 = new byte[data.Length - 8];
                Array.Copy(data, 8, data2, 0, data.Length - 8);
                data = Encryption.DeEncrypt(data2, key);
            }
            return data;
        }
    }
}
