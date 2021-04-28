using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Tmds.Systemd.Tests
{
    public static class ReadMessageFields
    {
        
        public static Dictionary<string, string> ReadFields(Socket socket)
        {
            var datas = new List<ArraySegment<byte>>();
            int length = socket.Available;
            if (length > 0)
            {
                byte[] data = new byte[length];
                int bytesReceived = socket.Receive(data);
                datas.Add(new ArraySegment<byte>(data, 0, bytesReceived));
            }
            return ReadFields(datas);
        }

        public static Dictionary<string, string> ReadFields(JournalMessage message)
            => ReadFields(message.GetData());

        public static Dictionary<string, string> ReadFields(List<ArraySegment<byte>> datas)
        {
            var fields = new Dictionary<string, string>();
            byte[] bytes;
            using (var memoryStream = new MemoryStream())
            {
                foreach (var data in datas)
                {
                    memoryStream.Write(data.Array, data.Offset, data.Count);
                }
                bytes = memoryStream.ToArray();
            }
            Span<byte> remainder = bytes;
            while (remainder.Length > 0)
            {
                int fieldNameLength = remainder.IndexOf((byte)'\n');
                string fieldName = Encoding.UTF8.GetString(remainder.Slice(0, fieldNameLength));
                remainder = remainder.Slice(fieldNameLength + 1);
                int fieldValueLength = (int)BinaryPrimitives.ReadUInt64LittleEndian(remainder);
                remainder = remainder.Slice(8);
                string fieldValue = Encoding.UTF8.GetString(remainder.Slice(0, fieldValueLength));
                remainder = remainder.Slice(fieldValueLength + 1);
                fields.Add(fieldName, fieldValue);
            }
            return fields;
        }
    }
}
