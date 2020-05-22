using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HttpClient.Extension
{
    public class MultipartFormFileStreamData
    {
        public MultipartFormFileStreamData()
        { }

        public MultipartFormFileStreamData(string fileName, Stream fileStream, string name = "file")
        {
            Name = name;
            FileName = fileName;
            FileStream = fileStream;
        }
        public string Name { get; set; } = "file";

        public string FileName { get; set; }

        public Stream FileStream { get; set; }
    }

    public class MultipartFormFileBytesData
    {
        public MultipartFormFileBytesData()
        { }

        public MultipartFormFileBytesData(string fileName, byte[] bytes, string name = "file")
        {
            Name = name;
            FileName = fileName;
            FileBytes = bytes;
        }
        public string Name { get; set; } = "file";

        public string FileName { get; set; }

        public byte[] FileBytes { get; set; }
    }
}
