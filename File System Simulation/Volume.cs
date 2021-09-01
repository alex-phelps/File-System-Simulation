using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_System_Simulation
{
    public class Volume
    {
        public const int ENTRY_SIZE = 64;
        public const int BLOCK_SIZE = 4096;

        private static FileStream file = null;
        private static Volume current_vol = null;

        public bool isMounted => current_vol != null;
        private long entriesOffset;
        private long entriesLength => (locksOffset) / ENTRY_SIZE;
        private long locksOffset;
        private long locksLength => storageLength; // Unlike directory entries, this is explicity tied to storage blocks
        private long storageOffset;
        private long storageLength => (file.Length) / BLOCK_SIZE;

        private Volume(string filename)
        {
            file = new FileStream(filename, FileMode.Open);

            // Calculate the volume section offsets            
            long size = file.Length;

            // I described this math in the design document but I had miscalculated.
            // Here are the correct values:
            //
            // - 8 file directory entries(512 bytes)
            // - 8 lock table entries(1 byte)
            // - 8 storage blocks(32768 bytes)
            //
            // This totals 33281 bytes and that is what the numbers below mean.
            // We are spliting the volume based on these percentages.
            this.entriesOffset = 0;
            this.locksOffset = (size * 512) / 33281;
            this.storageOffset = locksOffset + (size - 1) / 33281 + 1; // This is slightly different so that we round up with int math.
            ////
        }

        /// <summary>
        /// Creates a file system.
        /// </summary>
        /// <param name="filename">Name of the volume to be created.</param>
        /// <param name="size">The byte length of the volume.</param>
        /// <returns>True upon the creation of the system.</returns>
        public static bool Allocate(string filename, long size)
        {
            using (FileStream create = new FileStream(filename, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(create))
            {
                writer.Write(new byte[size]);
            }

            return true;
        }

        /// <summary>
        /// Physically deletes a volume.
        /// </summary>
        /// <param name="filename">Name of the volume to delete.</param>
        /// <returns>True if volume was deleted. False if the volume does not exist.</returns>
        public static bool Deallocate(string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Initializes (erases) a volume.
        /// </summary>
        /// <param name="filename">Name of the volume to truncate.</param>
        /// <returns>True upon success. False if the volume does not exist.</returns>
        public static bool Truncate(string filename)
        {
            if (File.Exists(filename))
            {
                using (FileStream trunc = new FileStream(filename, FileMode.Open))
                using (BinaryWriter writer = new BinaryWriter(trunc))
                {
                    writer.Write(new byte[trunc.Length]);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Writes the contents of a volume to the console.
        /// </summary>
        /// <param name="filename">The name of the volume to dump.</param>
        public static void Dump(string filename)
        {
            if (File.Exists(filename))
            {
                using (FileStream dump = new FileStream(filename, FileMode.Open))
                using (BinaryReader reader = new BinaryReader(dump))
                {
                    for (long i = 0; i < dump.Length; i++)
                    {
                        Console.Write("{0}", reader.Read().ToString("X2"));
                    }
                }
            }
        }

        /// <summary>
        /// Mounts a volume.
        /// </summary>
        /// <param name="filename">Name of the volume to mount.</param>
        /// <returns>The volume that has been mounted. NULL if the volume does not exist.</returns>
        public static Volume Mount(string filename)
        {
            if (File.Exists(filename))
            {
                current_vol = new Volume(filename);
            }

            return current_vol;
        }

        /// <summary>
        /// Closes the current volume.
        /// </summary>
        public void UnMount()
        {
            if (file != null)
            {
                file.Close();
                file.Dispose();
                file = null;

                current_vol = null;
            }
        }

        /// <summary>
        /// Creates an empty file on the volume.
        /// </summary>
        /// <param name="filename">Name of the file to create</param>
        /// <returns>True upon creation. False if file could not be created.</returns>
        public bool Create(string filename)
        {
            if (GetFile(filename) != null) // file already exists
            {
                return TruncateFile(filename);
            }

            long offset = FindEmptyEntry(); // offset location to store directory entry in file
            if (offset == -1)
            {
                return false;
            }

            short openBlock = AllocateBlocks(1);
            if (openBlock == -1)
            {
                return false;
            }

            DirectoryEntry entry = new DirectoryEntry()
            {
                filename = filename,
                startBlock = openBlock
            };

            BinaryWriter writer = new BinaryWriter(file);
            writer.BaseStream.Position = offset; // move to offset

            writer.Write(entry.ToBytes());
            writer.Flush();

            return true;
        }

        /// <summary>
        /// Deletes a file from the volume.
        /// </summary>
        /// <param name="filename">Name of the file to delete.</param>
        /// <returns>True upon deletion. False if file could not be deleted.</returns>
        public bool Delete(string filename)
        {
            long offset = GetFileOffset(filename);
            DirectoryEntry entry = GetFileFromOffset(offset);

            if (entry == null || entry.read_only)
            {
                return false;
            }


            int numBlocks = Math.Max((entry.size - 1) / BLOCK_SIZE + 1, 1); // rounds up, min of 1
            DeallocateBlocks(entry.startBlock, numBlocks);

            file.Position = offset;
            for (int i = 0; i < ENTRY_SIZE; i++)
            {
                file.WriteByte((byte)'\0');
            }
            file.Flush();

            return true;
        }

        /// <summary>
        /// Deletes a file and then re-creates it.
        /// </summary>
        /// <param name="filename">Name of the file to truncate.</param>
        /// <returns>True upon success. False if file could not be truncated.</returns>
        public bool TruncateFile(string filename)
        {
            if (!Delete(filename))
            {
                return false;
            }

            return Create(filename);
        }

        /// <summary>
        /// Writes data to a given file.
        /// </summary>
        /// <param name="filename">Name of the file to write to.</param>
        /// <param name="offset">Location within to file to start writing from.</param>
        /// <param name="data">Data to be written.</param>
        /// <returns>True upon completion. False if file could not be written to.</returns>
        public bool Write(string filename, long start, string data)
        {
            long fileOffset = GetFileOffset(filename);
            DirectoryEntry entry = GetFileFromOffset(fileOffset);
            BinaryWriter writer = new BinaryWriter(file);

            if (entry == null || entry.read_only)
            {
                return false;
            }

            int numBlocks = Math.Max((entry.size - 1) / BLOCK_SIZE + 1, 1); // rounds up, min of 1
            int newMaxBlocks = (int)((start + data.Length - 1) / BLOCK_SIZE + 1);
            if (newMaxBlocks > numBlocks) // need to reallocate
            {
                DeallocateBlocks(entry.startBlock, numBlocks);

                short newStartBlock = AllocateBlocks(newMaxBlocks);

                if (entry.startBlock != newStartBlock) // If we just entend the current area, we dont need to change anything
                {
                    // Copy old data into new blocks
                    BinaryReader reader = new BinaryReader(file);

                    reader.BaseStream.Position = storageOffset + (entry.startBlock * BLOCK_SIZE);
                    byte[] bytes = reader.ReadBytes(entry.size);

                    writer.BaseStream.Position = storageOffset + (newStartBlock * BLOCK_SIZE);
                    writer.Write(bytes);
                    writer.Flush();
                    ////

                    entry.startBlock = newStartBlock; // this will get written to file later
                }
            }

            // NULL out new space - We don't want our file containing any deleted data
            long difference = start + data.Length - entry.size;
            if (difference > 0)
            {
                writer.BaseStream.Position = storageOffset + (entry.startBlock * BLOCK_SIZE) + entry.size;
                writer.Write(new byte[difference]);
            }

            // Writing to storage
            long blockOffset = storageOffset + (entry.startBlock * BLOCK_SIZE);

            byte[] byteData = Encoding.ASCII.GetBytes(data);

            writer.BaseStream.Position = blockOffset + start;
            writer.Write(byteData);
            writer.Flush();

            // Edit modified date and size
            entry.size = Math.Max(entry.size, (int)(start + data.Length));
            entry.ModifiedAt = DateTime.Now;

            writer.BaseStream.Position = fileOffset; // move to file entry offset
            writer.Write(entry.ToBytes()); // also writes start block if that changed
            writer.Flush();

            return true;
        }

        /// <summary>
        /// Reads data from the given file.
        /// </summary>
        /// <param name="filename">Name of the file to read from.</param>
        /// <param name="start">Location within to file to start reading from.</param>
        /// <param name="end">Location within to file to stop reading at.</param>
        /// <returns>Data from the file. NULL if file is not found.</returns>
        public string Read(string filename, long start, long end)
        {
            DirectoryEntry entry = GetFile(filename);

            if (entry == null)
            {
                return null;
            }

            end = Math.Min(entry.size, end); // make sure we don't read outside the file
            int length = (int)Math.Max(end - start, 0); // Minimum of 0

            // Reading from storage
            long blockOffset = storageOffset + (entry.startBlock * BLOCK_SIZE);

            BinaryReader reader = new BinaryReader(file);
            reader.BaseStream.Position = blockOffset + start;

            byte[] byteData = reader.ReadBytes(length);
            string data = Encoding.ASCII.GetString(byteData);

            return data;
        }

        /// <summary>
        /// Sets the readonly byte for a file.
        /// </summary>
        /// <param name="filename">Name of the file to modify.</param>
        /// <param name="value">Value to set the readonly byte to.</param>
        /// <returns>True upon completion. False if file is not found.</returns>
        public bool SetReadOnly(string filename, bool value)
        {
            long fileOffset = GetFileOffset(filename);
            DirectoryEntry entry = GetFileFromOffset(fileOffset);

            if (entry == null)
            {
                return false;
            }

            if (value != entry.read_only)
            {
                BinaryWriter writer = new BinaryWriter(file);
                entry.read_only = value;
                entry.ModifiedAt = DateTime.Now;

                writer.BaseStream.Position = fileOffset; // move to file entry offset
                writer.Write(entry.ToBytes());
                writer.Flush();
            }

            return true;
        }

        /// <summary>
        /// Writes file info to the console.
        /// </summary>
        /// <param name="filename">Name of the file to retrieve info for.</param>
        public void FileInfo(string filename)
        {
            DirectoryEntry entry = GetFile(filename);

            if (entry == null)
            {
                Console.WriteLine("File not found!");
                return;
            }

            string formatter = "{0,42} | {1,10} | {2,10} | {3,20} | {4,20}";
            Console.WriteLine(formatter, "Filename", "Size", "Read Only", "Date Created", "Last Modified");
            Console.WriteLine(formatter, entry.filename, entry.size, entry.read_only, entry.CreatedAt, entry.ModifiedAt);
        }

        /// <summary>
        /// Writes volume info to the console.
        /// </summary>
        public void VolumeInfo()
        {
            string formatter = "{0,10} | {1,10} | {2,10}";
            Console.WriteLine(formatter, "Size", "Free Space", "File Count");
            Console.WriteLine(formatter, file.Length, 0, GetFiles().Count); // TODO get free space
        }

        /// <summary>
        /// Writes file info for all files on the volume.
        /// </summary>
        public void Catalog()
        {
            string formatter = "{0,42} | {1,10} | {2,10} | {3,25} | {4,25} | {5,12}";
            Console.WriteLine(formatter, "Filename", "Size", "Read Only", "Date Created", "Last Modified", "Start Block");

            foreach (DirectoryEntry entry in GetFiles())
            {
                Console.WriteLine(formatter, entry.filename, entry.size, entry.read_only, entry.CreatedAt, entry.ModifiedAt, entry.startBlock);
            }
        }

        /// <summary>
        /// Gets a list of all files on the volume.
        /// </summary>
        /// <returns>A list of directory entries.</returns>
        private List<DirectoryEntry> GetFiles()
        {
            List<DirectoryEntry> entries = new List<DirectoryEntry>();

            BinaryReader reader = new BinaryReader(file);
            for (int i = 0; i < entriesLength; i++)
            {
                reader.BaseStream.Position = entriesOffset + (i * ENTRY_SIZE);
                byte[] entryBytes = reader.ReadBytes(ENTRY_SIZE);

                // If any of the directory bytes are not null, then it is considered a valid file entry
                if (entryBytes.Any(b => b != (byte)'\0'))
                {
                    entries.Add(new DirectoryEntry(entryBytes));
                }
            }

            return entries;
        }

        /// <summary>
        /// Retrieves a file from the directory.
        /// </summary>
        /// <param name="filename">Name of the file to retrieve.</param>
        /// <returns>DirectoryEntry object for the file. NULL if the file is not found.</returns>
        private DirectoryEntry GetFile(string filename)
        {
            long offset = GetFileOffset(filename);
            if (offset == -1)
            {
                return null;
            }

            return GetFileFromOffset(offset);
        }


        /// <summary>
        /// Retrieves a file from the directory from its offset location.
        /// </summary>
        /// <param name="offset">Offset of the file.</param>
        /// <returns>DirectoryEntry object for the file. NULL if the file is not found.</returns>
        private DirectoryEntry GetFileFromOffset(long offset)
        {
            BinaryReader reader = new BinaryReader(file);
            reader.BaseStream.Position = offset;
            return new DirectoryEntry(reader.ReadBytes(ENTRY_SIZE));
        }

        /// <summary>
        /// Retrieves a files location on the volume.
        /// </summary>
        /// <param name="filename">Name of the file to retrieve.</param>
        /// <returns>Entry offset for the file. -1 if the file is not found.</returns>
        private long GetFileOffset(string filename)
        {
            BinaryReader reader = new BinaryReader(file);
            for (int i = 0; i < entriesLength; i++)
            {
                reader.BaseStream.Position = entriesOffset + (i * ENTRY_SIZE);
                byte[] nameBytes = reader.ReadBytes(DirectoryEntry.FILENAME_LENGTH);
                string foundName = Encoding.ASCII.GetString(nameBytes).Trim();

                if (filename.Trim().Equals(foundName))
                {
                    return entriesOffset + (i * ENTRY_SIZE);
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds an empty directory entry slot for file addition.
        /// </summary>
        /// <returns>Offset for the directory entry. -1 if there is no file space.</returns>
        private long FindEmptyEntry()
        {
            BinaryReader reader = new BinaryReader(file);
            for (int i = 0; i < entriesLength; i++)
            {
                reader.BaseStream.Position = entriesOffset + (i * ENTRY_SIZE);
                byte[] entryBytes = reader.ReadBytes(ENTRY_SIZE);

                // If all bytes are null, this is an open slot
                if (entryBytes.All(b => b == (byte)'\0'))
                {
                    return entriesOffset + (i * ENTRY_SIZE);
                }
            }

            return -1; // no space on volume
        }

        /// <summary>
        /// Finds a set of blocks that are open for allocation.
        /// </summary>
        /// <param name="length">The number of consecutive blocks that need to be found.</param>
        /// <returns>Lock table index for the start of open blocks. -1 if no space is found on the volume.</returns>
        private short FindOpenBlocks(int length)
        {
            if (length <= 0)
            {
                return -1;
            }

            BinaryReader reader = new BinaryReader(file);

            short numOpenBlocks = 0; // counter for consecutive open blocks
            short i = 0;
            while (i < locksLength)
            {
                reader.BaseStream.Position = locksOffset + (i / 8);

                // Since we store the locks as bits, we have to read 8 at a time and step through each
                byte locks = reader.ReadByte();

                // j is bit index within the byte
                for (int j = 0; j < 8 && i < locksLength; j++)
                {
                    if ((locks & 0b_0000_0001) == 0) // this checks if the bit furthest right is not set
                    {
                        numOpenBlocks++;
                    }
                    else
                    {
                        numOpenBlocks = 0; // reset counter
                    }

                    locks = (byte)(locks >> 1); // bitshift for next iteration
                    i++;

                    if (numOpenBlocks >= length) // we have found enough open blocks
                    {
                        return (short)( i - numOpenBlocks );
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Sets entries in the lock table.
        /// </summary>
        /// <param name="index">Lock index of the starting block.</param>
        /// <param name="length">Number of blocks to set.</param>
        /// <param name="value">Value to set locks to. Can be either 0 or 1.</param>
        private void Setlocks(int index, int length, int value)
        {
            if (length <= 0 || index < 0)
            {
                return;
            }

            BinaryReader reader = new BinaryReader(file);
            BinaryWriter writer = new BinaryWriter(file);
            for (int i = 0; i < length; i++)
            {
                reader.BaseStream.Position = locksOffset + ((i + index) / 8); // both streams have the same base position
                byte locks = reader.ReadByte();

                reader.BaseStream.Position--; // go back so we can write over the same byte

                if (value == 0)
                {
                    byte overwrite = (byte)~(1 << ((i + index) % 8)); // something like 0b_1111_1011, but with 0 in the correct position
                    locks = (byte)(locks & overwrite); // this sets that individual bit to 0
                }
                else
                {
                    byte overwrite = (byte)(1 << ((i + index) % 8)); // something like 0b_0000_0100, but with 1 in the correct position
                    locks = (byte)(locks | overwrite); // this sets that individual bit to 1
                }

                writer.Write(locks);
            }
            writer.Flush();
        }

        /// <summary>
        /// Allocates a set of blocks in the lock table.
        /// </summary>
        /// <param name="length">Number of blocks to allocate.</param>
        /// <returns>The index of the starting block in the lock table. -1 if no space is found.</returns>
        private short AllocateBlocks(int length)
        {
            short index = FindOpenBlocks(length);

            if (index != -1) // we have space
            {
                Setlocks(index, length, 1);
            }

            return index;
        }

        /// <summary>
        /// Deallocates a set of blocks in the lock table.
        /// </summary>
        /// <param name="index">Starting index of the blocks in the lock table.</param>
        /// <param name="length">Number of blocks to deallocate.</param>
        private void DeallocateBlocks(short index, int length)
        {
            Setlocks(index, length, 0);
        }
    }
}
