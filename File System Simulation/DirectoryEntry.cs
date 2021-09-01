using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_System_Simulation
{

    public class DirectoryEntry
    {
        public const int FILENAME_LENGTH = 41;

        public string filename;
        public int size;
        public bool read_only;
        private long createdAt;
        public DateTime CreatedAt
        {
            get
            {
                return DateTime.FromBinary(this.createdAt);
            }
            set
            {
                this.createdAt = value.ToBinary();
            }
        }
        private long modifiedAt;
        public DateTime ModifiedAt
        {
            get
            {
                return DateTime.FromBinary(this.modifiedAt);
            }
            set
            {
                this.modifiedAt = value.ToBinary();
            }
        }
        public short startBlock;

        public DirectoryEntry()
        {
            this.filename = String.Empty.PadRight(41);
            this.size = 0;
            this.read_only = false;
            this.createdAt = DateTime.Now.ToBinary();
            this.modifiedAt = DateTime.Now.ToBinary();
            this.startBlock = 0;
        }

        public DirectoryEntry(byte[] bytes)
        {
            byte[] subBytes = bytes.Take(41).ToArray();
            this.filename = Encoding.ASCII.GetString(subBytes).Trim();

            subBytes = bytes.Skip(41).Take(4).ToArray();
            this.size = BitConverter.ToInt32(subBytes);

            this.read_only = Convert.ToBoolean(bytes[45]);

            subBytes = bytes.Skip(46).Take(8).ToArray();
            this.createdAt = BitConverter.ToInt64(subBytes);

            subBytes = bytes.Skip(54).Take(8).ToArray();
            this.modifiedAt = BitConverter.ToInt64(subBytes);

            subBytes = bytes.Skip(62).Take(2).ToArray();
            this.startBlock = BitConverter.ToInt16(subBytes);
        }

        /// <summary>
        /// Gets the byte representation of the Directory Entry.
        /// </summary>
        /// <returns>An array of bytes with length 64.</returns>
        public byte[] ToBytes()
        {
            byte[] bytes = new byte[Volume.ENTRY_SIZE];
            int offset = 0;

            byte[] encoding_bytes = Encoding.ASCII.GetBytes(filename.PadRight(41));
            for (int i = 0; i < 41; i++)
            {
                bytes[offset + i] = encoding_bytes[i];
            }

            offset = 41;
            encoding_bytes = BitConverter.GetBytes(size);
            for (int i = 0; i < 2; i++)
            {
                bytes[offset + i] = encoding_bytes[i];
            }

            offset = 45;
            bytes[offset] = BitConverter.GetBytes(read_only)[0];

            offset = 46;
            encoding_bytes = BitConverter.GetBytes(createdAt);
            for (int i = 0; i < 8; i++)
            {
                bytes[offset + i] = encoding_bytes[i];
            }

            offset = 54;
            encoding_bytes = BitConverter.GetBytes(modifiedAt);
            for (int i = 0; i < 8; i++)
            {
                bytes[offset + i] = encoding_bytes[i];
            }

            offset = 62;
            encoding_bytes = BitConverter.GetBytes(startBlock);
            for (int i = 0; i < 2; i++)
            {
                bytes[offset + i] = encoding_bytes[i];
            }

            return bytes;
        }
    }
}
