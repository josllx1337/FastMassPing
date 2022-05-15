using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace FastMassPing
{
    class Program
    {
        static void SendHelp(bool freeLine)
        {
            if (freeLine)
                Console.WriteLine();
            Console.WriteLine("FastMassPing.exe            The fastest ping in the west        May contain 'peanuts'");
            Console.WriteLine();
            Console.WriteLine("Avaliable Parameters");
            Console.WriteLine("--help           Displays this help");
            Console.WriteLine("--start-ip       First IP adress                      Mandatory");
            Console.WriteLine("--end-ip         Last IP address                      Mandatory");
            Console.WriteLine("--port           Port to check                        Mandatory");
            Console.WriteLine("--timeout        Timeout in milliseconds              Default: 1000    Minimum: 0");
            Console.WriteLine("--threads        Number of threads to use             Default: 16      Minimum: 2");
            Console.WriteLine("--output         File to write found addresses to     Default: None");
            Console.WriteLine();
            Console.WriteLine("Use of a proxy or vpn is recommended.");
            Console.WriteLine("If no output file is specified found IPs will be displayed in the console.");
            Console.WriteLine();
            Console.WriteLine("Warning: Use of 128 threads or more can lead to serious internet performance");
            Console.WriteLine("          issues not just for the machine running this program but for the");
            Console.WriteLine("          entire network sharing the same router. Please set appropriate");
            Console.WriteLine("          values that you know your network can handle.");
            return;
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                SendHelp(false);
                return;
            }

            IPAddress startAddress = null;
            IPAddress endAddress = null;
            Int32 port = 0;
            Int32 timeout = 1000;
            Int32 threadCount = 16;
            string outputFile = "";
            Boolean[] varCheck = { false, false, false, false, false, false };

            for (int i = 0; i < args.Length; i++)
            {   
                switch (args[i])
                {
                    case "--help":
                        SendHelp(false);
                        return;
                        break;

                    case "--start-ip":
                        try {
                            startAddress = IPAddress.Parse(args[i + 1]);
                            varCheck[0] = true;
                        } catch {
                            Console.WriteLine("Start IP address invalid. Aborting.");
                            return;
                        }
                        break;

                    case "--end-ip":
                        try
                        {
                            endAddress = IPAddress.Parse(args[i + 1]);
                            varCheck[1] = true;
                        } catch {
                            Console.WriteLine("End IP address invalid. Aborting.");
                            return;
                        }
                        break;

                    case "--port":
                        try {
                            port = Convert.ToInt32(args[i + 1]);
                            varCheck[2] = true;
                        } catch {
                            Console.WriteLine("Port invalid. Aborting.");
                            return;
                        }
                        break;

                    case "--timeout":
                        try {
                            timeout = Convert.ToInt32(args[i + 1]);
                            varCheck[3] = true;
                        } catch {
                            Console.WriteLine("Timeout invalid. Setting to Default.");
                            timeout = 1000;
                        }
                        break;

                    case "--threads":
                        try {
                            threadCount = Convert.ToInt32(args[i + 1]);
                            varCheck[4] = true;
                        } catch (Exception) {
                            Console.WriteLine("Number of Threads invalid. Setting to Default.");
                            threadCount = 16;
                        }
                        break;

                    case "--output":
                        outputFile = args[i + 1];
                        varCheck[5] = true;
                        break;

                    default:
                        break;
                }
            }

            if (!varCheck[0])
            {
                Console.WriteLine("Start IP address unspecified. Aborting.");
                SendHelp(true);
                return;
            }
            if (!varCheck[1])
            {
                Console.WriteLine("End IP address unspecified. Aborting.");
                SendHelp(true);
                return;
            }
            if (!varCheck[2])
            {
                Console.WriteLine("Port unspecified. Aborting.");
                SendHelp(true);
                return;
            }
            else
            {
                if (port < 0)
                {
                    Console.WriteLine("Port cannot be smaller than 0. Aborting.");
                    return;
                }
                if (port > 65535)
                {
                    Console.WriteLine("Port cannot be larger than 65535. Aborting.");
                    return;
                }
            }
            if (varCheck[3])
            {
                if (timeout < 0)
                    Console.WriteLine("Warning: Timeout cannot be smaller than 0. Setting to Minimum.");
                timeout = Math.Max(timeout, 0);
            }
            if (varCheck[4])
            {
                if (threadCount < 2)
                    Console.WriteLine("Warning: No fewer than 2 Threads allowed. Setting to Minimum.");
                threadCount = Math.Max(threadCount, 2);

            }
            if (varCheck[5])
            {
                try
                {
                    using (StreamWriter outFile = new StreamWriter(outputFile, append: true)) { };
                }
                catch
                {
                    Console.WriteLine("Output file could not be opened. Aborting.");
                    return;
                }
            }

            Int64 addressSpace = GetAddressSpace(startAddress, endAddress);
            if (addressSpace <= 0)
            {
                Console.WriteLine("End address cannot be smaller or equal to Start address. Aborting.");
                return;
            }

            IPAddress currentAddress = startAddress;
            Int32 addit = (int)(addressSpace % threadCount);
            Int32 mean = (int)Math.Floor((float)addressSpace / threadCount);

            Console.WriteLine("Searching for addressed with open Port {0} with {1} Threads", port, threadCount);
            Console.WriteLine("Pinging from {0} to {1}  ({2} addresses)\n", startAddress.ToString(), endAddress.ToString(), addressSpace);

            Queue<string> output = new Queue<string>();
            Queue<string> infoOutput = new Queue<string>();
            Int32[] status = new Int32[threadCount+2];
            Thread[] threads = new Thread[threadCount+2];

            for (int i = 0; i < threadCount; i++)
            {
                IPAddress currentAddressCopy = currentAddress;
                Int32 threadNum = i;
                Int32 increm = 0;

                if (i < addit)
                    increm = 1;

                threads[i] = new Thread(() => PingThread(currentAddressCopy, IncrementAddress(currentAddressCopy, mean + increm), port, timeout, threadNum, status, output));
                threads[i].IsBackground = true;
                threads[i].Start();
                currentAddress = IncrementAddress(currentAddress, mean + increm);
            }

            threads[threadCount] = new Thread(() => MonitorThread(addressSpace, threadCount, status, infoOutput));
            threads[threadCount].IsBackground = true;
            threads[threadCount].Start();
            threads[threadCount+1] = new Thread(() => OutputThread(output, infoOutput, outputFile, threadCount+1, status));
            threads[threadCount+1].IsBackground = true;
            threads[threadCount+1].Start();

            for (int i = 0; i < threadCount; i++)
            {
                threads[i].Join();
            }

            status[threadCount] = 1;
            status[threadCount + 1] = 1;
            threads[threadCount].Join();
            threads[threadCount+1].Join();

            return;

            }

        static void PingThread(IPAddress startAddress, IPAddress endAddress, Int32 port, Int32 timeout, Int32 threadNum, Int32[] status, Queue<string> output)
        {
            IPAddress currentAddress = startAddress;
            bool online;
            while (!currentAddress.Equals(endAddress))
            {
                online = TestConnection(currentAddress, port, timeout);
                if (online)
                {
                    lock (output)
                    {
                        output.Enqueue(currentAddress.ToString() + ":" + port.ToString());
                    }
                }
                currentAddress = IncrementAddress(currentAddress, 1);
                status[threadNum]++;
            }
            return;
        }

        static void MonitorThread(Int64 addressSpace, Int32 threadNum, Int32[] status, Queue<string> output)
        {
            DateTime startTime = DateTime.Now;
            TimeSpan elapsedTime;
            TimeSpan eta;
            Int32 pinged;
            while (status[threadNum] == 0)
            {
                Thread.Sleep(500);
                pinged = status.Sum();
                elapsedTime = DateTime.Now.Subtract(startTime);
                eta = elapsedTime.Multiply((double)addressSpace / Math.Max(pinged, 1)).Subtract(elapsedTime);
                output.Enqueue(String.Format("Pinged {0} out of {1} addresses  ( {2} Completed )  T: {3}   ETA: {4}   {5}\r",
                    pinged, addressSpace, ((decimal)pinged/addressSpace).ToString("P"), elapsedTime.ToString(@"dd\.hh\:mm\:ss"), 
                    eta.ToString(@"dd\.hh\:mm\:ss"), startTime.Add(elapsedTime).Add(eta).ToString("G")));
            }
        }

        static void OutputThread(Queue<string> output, Queue<string> infoOutput, string outputFile, Int32 threadNum, Int32[] status)
        {
            try
            {
                using (StreamWriter outFile = new StreamWriter(outputFile, append: true))
                {
                    outFile.AutoFlush = true;

                    while (status[threadNum] == 0)
                    {
                        while (output.Count > 0)
                        {
                            outFile.WriteLine(output.Dequeue());
                        }

                        while (infoOutput.Count > 0)
                        {
                            Console.Write(infoOutput.Dequeue());
                        }
                    }
                }
            }
            catch
            {
                while (status[threadNum] == 0)
                {
                    while (output.Count > 0)
                    {
                        Console.WriteLine(output.Dequeue());
                    }

                    while (infoOutput.Count > 0)
                    {
                        Console.Title = infoOutput.Dequeue();
                    }
                }
            }
        }

        static bool TestConnection(IPAddress address, Int32 port, Int32 timeout)
        {
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    IAsyncResult result = client.BeginConnect(address, port, null, null);
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    while (!result.IsCompleted && stopwatch.ElapsedMilliseconds < timeout)
                    {
                        Thread.Sleep(1);
                    }
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        static bool TestConnectionHttp(IPAddress address, Int32 port, Int32 timeout)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(IPAdressToUri(address, port));
            request.Timeout = timeout;

            try
            {
                request.GetResponse();
            }
            catch (Exception exception)
            {

            }
            return true;
            //This might not be possible
        }

        static Uri IPAdressToUri(IPAddress address, Int32 port)
        {
            return new Uri("http://" + address.ToString() + ":" + port + "/");
        }

        static IPAddress IncrementAddress(IPAddress address, Int32 increment)
        {
            byte[] addressBytes = address.GetAddressBytes();
            Array.Reverse(addressBytes);
            addressBytes = BitConverter.GetBytes(BitConverter.ToInt32(addressBytes, 0) + increment);
            Array.Reverse(addressBytes);
            return new IPAddress(addressBytes);
        }

        static Int64 GetAddressSpace(IPAddress address1, IPAddress address2)
        {
            byte[] address1Bytes = address1.GetAddressBytes();
            byte[] address2Bytes = address2.GetAddressBytes();
            Array.Reverse(address1Bytes);
            Array.Reverse(address2Bytes);
            return (Int64)(BitConverter.ToUInt32(address2Bytes, 0) - BitConverter.ToUInt32(address1Bytes, 0));
        }
    }
}
