using System;

namespace File_System_Simulation
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 0)
            {
                Console.WriteLine("Invalid command.");
                return;
            }

            string command = args[0];

            if (command.Equals("ALLOCATE"))
            {
                if (args.Length != 3)
                {
                    Console.WriteLine("Invalid command arguments.");
                    return;
                }

                string volName = args[1];
                string size = args[2];

                Volume.Allocate(volName, long.Parse(size));
            }
            else if (command.Equals("DEALLOCATE"))
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Invalid command arguments.");
                    return;
                }

                string volName = args[1];

                if (!Volume.Deallocate(volName))
                {
                    Console.WriteLine("Volume not found!");
                }
            }
            else if (command.Equals("TRUNCATE"))
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Invalid command arguments.");
                    return;
                }

                string volName = args[1];

                if (!Volume.Truncate(volName))
                {
                    Console.WriteLine("Volume not found!");
                }
            }
            else if (command.Equals("DUMP"))
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Invalid command arguments.");
                    return;
                }

                string volName = args[1];

                Volume.Dump(volName);
            }
            else if (command.Equals("MOUNT"))
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Invalid command arguments.");
                    return;
                }

                string volName = args[1];

                Volume volume = Volume.Mount(volName);

                if (volume == null)
                {
                    Console.WriteLine("Volume not found!");
                }

                // Access to filesystem
                Console.WriteLine("Volume {0} has been mounted.", volName);
                while (volume.isMounted)
                {
                    Console.Write("{0}:> ", volName);
                    string[] commandArgs = Console.ReadLine().Split(' ');
                    command = commandArgs[0];


                    if (command.Equals("UNMOUNT"))
                    {
                        volume.UnMount();
                    }
                    else if (command.Equals("CATALOG"))
                    {
                        volume.Catalog();
                    }
                    else if (command.Equals("INFO"))
                    {
                        if (commandArgs.Length == 1)
                        {
                            volume.VolumeInfo();
                        }
                        else if (commandArgs.Length == 2)
                        {
                            string filename = commandArgs[1];

                            volume.FileInfo(filename);
                        }
                        else
                        {
                            Console.WriteLine("Invalid command arguments.");
                            continue;
                        }
                    }
                    else if (command.Equals("CREATE"))
                    {
                        if (commandArgs.Length != 2)
                        {
                            Console.WriteLine("Invalid command arguments.");
                            continue;
                        }

                        string filename = commandArgs[1];
                        if (volume.Create(filename))
                        {
                            Console.WriteLine("File Created.");
                        }
                        else
                        {
                            Console.WriteLine("Could not write to file!");
                        }
                    }
                    else if (command.Equals("DELETE"))
                    {
                        if (commandArgs.Length != 2)
                        {
                            Console.WriteLine("Invalid command arguments.");
                            continue;
                        }

                        string filename = commandArgs[1];

                        if (volume.Delete(filename))
                        {
                            Console.WriteLine("File Deleted.");
                        }
                        else
                        {
                            Console.WriteLine("Could not delete file!");
                        }
                    }
                    else if (command.Equals("TRUNCATE"))
                    {
                        if (commandArgs.Length != 2)
                        {
                            Console.WriteLine("Invalid command arguments.");
                            continue;
                        }

                        string filename = commandArgs[1];

                        if (volume.TruncateFile(filename))
                        {
                            Console.WriteLine("File Truncated.");
                        }
                        else
                        {
                            Console.WriteLine("Could not truncate file!");
                        }
                    }
                    else if (command.Equals("READ"))
                    {
                        if (commandArgs.Length != 4)
                        {
                            Console.WriteLine("Invalid command arguments.");
                            continue;
                        }

                        string filename = commandArgs[1];
                        long start = long.Parse(commandArgs[2]);
                        long end = long.Parse(commandArgs[3]);

                        string data = volume.Read(filename, start, end);

                        if (data == null)
                        {
                            Console.WriteLine("File not found!");
                        }
                        else
                        {
                            Console.WriteLine(data);
                        }
                    }
                    else if (command.Equals("WRITE"))
                    {
                        if (commandArgs.Length != 4)
                        {
                            Console.WriteLine("Invalid command arguments.");
                            continue;
                        }

                        string filename = commandArgs[1];
                        long start = long.Parse(commandArgs[2]);
                        string data = commandArgs[3];

                        if (volume.Write(filename, start, data))
                        {
                            Console.WriteLine("Wrote to file.");
                        }
                        else
                        {
                            Console.WriteLine("Could not write to file!");
                        }
                    }
                    else if (command.Equals("SET"))
                    {
                        if (commandArgs.Length == 3)
                        {
                            string filename = commandArgs[1];
                            string[] values = commandArgs[2].Split('=');

                            if (values[0].Equals("READONLY") && values.Length == 2)
                            {
                                bool tf;
                                if (bool.TryParse(values[1], out tf))
                                {
                                    if (volume.SetReadOnly(filename, tf))
                                    {
                                        Console.WriteLine("Set readonly.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("File not found!");
                                    }
                                    continue;
                                }
                            }
                        }

                        Console.WriteLine("Invalid command arguments.");
                    }
                }
            }
        }
    }
}
