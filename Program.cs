using System.Net.Sockets;
using System.Net;
using System.Text;

namespace net_rtmp_server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var ipEndPoint = IPEndPoint.Parse(@"127.0.0.1:13599");
            TcpListener listener = new(ipEndPoint);

            try
            {
                listener.Start();
                Console.WriteLine($"Listening on {ipEndPoint.Address}:{ipEndPoint.Port}...");
                byte[] bytes = new byte[3000];
                while (true)
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;
                    int packetSize = 1536;
                    int time = 0;
                    int c0Index = 0;
                    int c1Index = 1;
                    byte s0 = 0;
                    byte[] c1 = new byte[packetSize];
                    byte[] s1 = GetS1Packet();
                    byte[] s2 = new byte[packetSize];
                    byte[] c2 = new byte[packetSize];
                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        if (time == 0)
                        {
                            s0 = bytes[c0Index];
                            c1 = bytes[c1Index..(packetSize + 1)];
                            s2 = GetS2Packet(c1, s1);

                            Console.WriteLine("----------------------------------------");
                            Console.WriteLine("Handshake...");
                            stream.Write([s0], 0, 1);
                            stream.Write(s1, 0, s1.Length);
                            Console.WriteLine($"Sent s0: {s0}, s1: {Encoding.ASCII.GetString(s1)}");
                            Console.WriteLine("----------------------------------------");
                        }
                        else if (time == 1)
                        {
                            Console.WriteLine("----------------------------------------");
                            c2 = bytes[..packetSize];
                            stream.Write(s2, 0, s2.Length);
                            Console.WriteLine("Received c2 and sent s2");
                            Console.WriteLine("----------------------------------------");
                        }
                        else if (time == 2)
                        {
                            Console.WriteLine("----------------------------------------");
                            var connectCommand = bytes[..3000];
                            var windowAckSize = BitConverter.GetBytes(4);
                            stream.Write(windowAckSize, 0, windowAckSize.Length);
                            Console.WriteLine($"Received connect command: {Encoding.ASCII.GetString(connectCommand)}");
                            Console.WriteLine("----------------------------------------");
                        }
                        else
                        {
                            Console.WriteLine("----------------------------------------");
                            Console.WriteLine($"Received connect command: {Encoding.ASCII.GetString(bytes[..3000])}");
                            Console.WriteLine("----------------------------------------");
                        }

                        time++;
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                listener.Stop();
                Console.WriteLine("\nHit enter to continue...");
                Console.Read();
            }
        }

        static Byte[] GetS1Packet()
        {
            int length = 1536;
            byte[] s1 = new byte[length];
            var timestampBytes = BitConverter.GetBytes((DateTime.UtcNow - DateTime.UnixEpoch).Milliseconds);
            for (int index = 0; index < 4; index++)
            {
                s1[index] = timestampBytes[index];
            }
            for (int index = 4; index < 8; index++)
            {
                s1[index] = Convert.ToByte(0);
            }
            for (int index = 8; index < length; index++)
            {
                byte rndNumber = Convert.ToByte(new Random(DateTime.UtcNow.Millisecond).GetItems(
                    [1, 2, 3, 4, 5, 12, 24, 35, 99, 64, 3, 7], 1
                    )[0]);
                s1[index] = rndNumber;
            }
            return s1;
        }

        static Byte[] GetS2Packet(Byte[] c1, Byte[] s1)
        {
            int length = 1536;
            byte[] s2 = new byte[length];
            for (int index = 0; index < 4; index++)
            {
                s2[index] = c1[index];
            }
            var timestampChunk = s2.AsSpan(4, 4);
            for (int i = 0; i < timestampChunk.Length; i++)
            {
                timestampChunk[i] = s1[i];
            }
            for (int index = 8; index < length; index++)
            {
                byte rndNumber = Convert.ToByte(new Random(DateTime.UtcNow.Millisecond).GetItems(
                    [1, 2, 3, 4, 5, 12, 24, 35, 99, 64, 3, 7], 1
                    )[0]);
                s2[index] = rndNumber;
            }
            return s2;
        }

        static Byte[] GetS3Packet(Byte[] c2, Byte[] s2)
        {
            int length = 1536;
            byte[] s3 = new byte[length];
            for (int index = 0; index < 4; index++)
            {
                s3[index] = c2[index];
            }
            var timestampChunk = s3.AsSpan(4, 4);
            for (int i = 0; i < timestampChunk.Length; i++)
            {
                timestampChunk[i] = s2[i];
            }
            for (int index = 8; index < length; index++)
            {
                byte rndNumber = Convert.ToByte(new Random(DateTime.UtcNow.Millisecond).GetItems(
                    [1, 2, 3, 4, 5, 12, 24, 35, 99, 64, 3, 7], 1
                    )[0]);
                s3[index] = rndNumber;
            }
            return s3;
        }
    }
}
