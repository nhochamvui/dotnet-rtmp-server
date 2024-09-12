using System.Collections;
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
                int packetSize = 1536;
                byte[] bytes = new byte[packetSize+5];
                while (true)
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int time = 0;
                    int c0Index = 0;
                    int c1Index = 1;
                    byte[] c1;
                    byte[] s1 = GetS1Packet();
                    byte[] s2 = new byte[packetSize];
                    // Loop to receive all the data sent by the client.
                    while (stream.Read(bytes, 0, bytes.Length) != 0)
                    {
                        switch (time)
                        {
                            case 0:
                                var s0 = bytes[c0Index];
                                c1 = bytes[c1Index..(packetSize + 1)];
                                s2 = GetS2Packet(c1, s1);

                                Console.WriteLine("----------------------------------------");
                                Console.WriteLine("Handshake...");
                                stream.Write([s0], 0, 1);
                                stream.Write(s1, 0, s1.Length);
                                Console.WriteLine($"Sent s0: {s0}, s1: {Encoding.ASCII.GetString(s1)}");
                                break;
                            case 1:
                                Console.WriteLine("----------------------------------------");
                                stream.Write(s2, 0, s2.Length);
                                Console.WriteLine("Connected!");
                                break;
                            default:
                            {
                                Console.WriteLine("----------------------------------------");
                                var connectCommand = bytes;
                                HandleChunkStreamPacket(bytes);
                                Console.WriteLine($"Received connect command: {Encoding.ASCII.GetString(connectCommand)}");
                                break;
                            }
                        }

                        Console.WriteLine("----------------------------------------");
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

        private const String chunksHaveNoHeader = """
                                                  chunks have no message header. 
                                                  The stream ID, message length and timestamp delta fields are not present;
                                                  chunks of this type take values from the preceding chunk for the same Chunk Stream ID
                                                  """;

        static void HandleChunkStreamPacket(byte[] chunkStreamPacket)
        {
            int chunkType = GetChunkType(chunkStreamPacket);
            int formHeaderByteType = ByteFormHeader(chunkStreamPacket); 
            int chunkStreamId = formHeaderByteType switch
            {
                1 => chunkStreamPacket[0] >> 2,
                _ => throw new NotImplementedException()
            };
            int chunkMessageHeaderSize = chunkType switch
            {
                0 => 11,
                1 => 7,
                2 => 3,
                _ => throw new NotImplementedException(chunksHaveNoHeader)
            };
            var chunkMessageHeader = chunkStreamPacket[formHeaderByteType..(chunkMessageHeaderSize + 1)];
            int timestampDeltaSize = 3;
            var timestampDelta = BitConverter.ToInt32([
                0x00, chunkMessageHeader[0], chunkMessageHeader[1], chunkMessageHeader[2]
            ], 0);
            if (timestampDelta > 0xFFFFFF)
            {
                timestampDeltaSize = 4;
                // timestampDelta = BitConverter.ToInt32(chunkMessageHeader.AsSpan()[..4]);
            }

            var messageLength = BitConverter.ToInt32([
                0x00,
                chunkMessageHeader[timestampDeltaSize],
                chunkMessageHeader[timestampDeltaSize+1],
                chunkMessageHeader[timestampDeltaSize+2],
            ]);
            int messageTypeId = chunkMessageHeader[timestampDeltaSize + 3];
            int messageStreamId = BitConverter.ToInt32(chunkMessageHeader[(timestampDeltaSize + 4)..], 0);
        }

        static int GetChunkType(byte[] bytes) => bytes[0] >> 6;

        static int ByteFormHeader(byte[] chunkStreamPacket)
        {
            return chunkStreamPacket.First() switch
            {
                0 => 2,
                1 => 3,
                _ => 1
            };
        }
    }
}
