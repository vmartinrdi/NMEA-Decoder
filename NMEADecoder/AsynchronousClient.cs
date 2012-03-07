using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.IO;

namespace NMEAClient
{
	public class AsynchronousClient
	{
		// The port number for the remote device.
		private const int port = 10116;

		// ManualResetEvent instances signal completion.
		private static ManualResetEvent connectDone =
			new ManualResetEvent(false);
		private static ManualResetEvent sendDone =
			new ManualResetEvent(false);
		private static ManualResetEvent receiveDone =
			new ManualResetEvent(false);

		// The response from the remote device.
		private static String response = String.Empty;
        //private static StreamWriter writeTo;

		private static void StartClient(string dataString)
		{
			// Connect to a remote device.
			try
			{
                //writeTo = new StreamWriter(@"C:\nmeaTestFile.txt", false);
                
				// Establish the remote endpoint for the socket.
				// The name of the 
				// remote device is "host.contoso.com".
				//IPHostEntry ipHostInfo = Dns.Resolve("host.contoso.com");
				IPHostEntry ipHostInfo = new IPHostEntry();
				//ipHostInfo.AddressList = new IPAddress[] { new IPAddress(new Byte[] { 127, 0, 0, 1 }) };
                ipHostInfo.AddressList = new IPAddress[] { new IPAddress(new Byte[] { 192, 168, 70, 128 }) };
				IPAddress ipAddress = ipHostInfo.AddressList[0];
				IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

				// Create a TCP/IP socket.
				Socket client = new Socket(AddressFamily.InterNetwork,
					SocketType.Stream, ProtocolType.Tcp);

				// Connect to the remote endpoint.
				client.BeginConnect(remoteEP,
					new AsyncCallback(ConnectCallback), client);
				connectDone.WaitOne();

				// Send test data to the remote device.
                sendDone.Reset();
				Send(client, dataString);
				sendDone.WaitOne();

                receiveDone.Reset();
                Receive(client);
                receiveDone.WaitOne();

                //// Receive the response from the remote device.
                //while (true)
                //{
                //    Receive(client);
                //    receiveDone.WaitOne();

                //    // Write the response to the console.
                //    Console.WriteLine("Response received : {0}\r\n", response);
                //}

				// Release the socket.
				client.Shutdown(SocketShutdown.Both);
				client.Close();

			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private static void ConnectCallback(IAsyncResult ar)
		{
			try
			{
				// Retrieve the socket from the state object.
				Socket client = (Socket)ar.AsyncState;

				// Complete the connection.
				client.EndConnect(ar);

				Console.WriteLine("Socket connected to {0}",
					client.RemoteEndPoint.ToString());
                //writeTo.WriteLine("Socket connected to {0}",
                //    client.RemoteEndPoint.ToString());

				// Signal that the connection has been made.
				connectDone.Set();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private static void Receive(Socket client)
		{
			try
			{
				// Create the state object.
				StateObject state = new StateObject();
				state.workSocket = client;

				// Begin receiving the data from the remote device.
				client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
					new AsyncCallback(ReceiveCallback), state);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private static void ReceiveCallback(IAsyncResult ar)
		{
			try
			{
				// Retrieve the state object and the client socket 
				// from the asynchronous state object.
				StateObject state = (StateObject)ar.AsyncState;
				Socket client = state.workSocket;

				// Read data from the remote device.
				int bytesRead = client.EndReceive(ar);

				if (bytesRead > 0)
				{
                    string data = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);

                    //// There might be more data, so store the data received so far.
                    //state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    Console.WriteLine(data);
                    //writeTo.WriteLine(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // parse the data into separate lines for decoding
                    string[] nmeaLines = data.Split(new string[] { @"\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string nmeaLine in nmeaLines)
                    {
                        string[] nmeaData = nmeaLine.Split(new char[] {','}, StringSplitOptions.None);

                        if (nmeaLine.StartsWith("$PGHP"))
                        {
                            /*
                             * Gatehouse wrapper
                             * Format is (two lines, but the second line will be decoded by itself):
                             * $PGH1,<msgtype>,<date format>,<country>,<region>,<pss>,<online data>,<cc>*hh<CR><LF>
                             * <NMEA message>
                                    <msgtype> is 1 for this message type (Gatehouse internal message type 1)
                                    <date format> as specified above
                                    <country> is the MMSI country code where the message originates from.
                                    <region> the MMSI number of the region
                                    <pss> the MMSI number of the site transponder
                                    <online data> buffered data from a BSC will be designated with 0. Online data with 1.
                                    <cc> the checksum value of the following NMEA sentence
                                    <*hh> checksum as described in /IEC 61162-1/
                                    <NMEA message> is the NMEA message that follows immediately after the $PGHP sentence 
                             */
                            
                            Console.WriteLine("Message from Gatehouse");
                            Console.WriteLine("     '" + nmeaLine + "'");
                            Console.WriteLine(string.Format("   Type: {0}\nDate: {1}\n   Country: {2}\n  Region: {3}\n   MMSI: {4}", nmeaData[1], nmeaData[2], nmeaData[3], nmeaData[4], nmeaData[5]));
                        }
                        else if (nmeaLine.StartsWith("!AIVDM"))
                        {
                            // contain encoded data

                            Console.WriteLine("Message from Gatehouse");
                            Console.WriteLine("     '" + nmeaLine + "'");
                        }
                        else if (nmeaLine.StartsWith("$PSHI"))
                        {
                            // do not contain encoded data

                            Console.WriteLine("Unrecognized identifier: $PSHI");
                            Console.WriteLine("     '" + nmeaLine + "'");
                        }
                        else if (nmeaLine.StartsWith("$GPGGA"))
                        {
                            /*
                             *  Format is:
                             *  $GPGGA,121505,4807.038,N,01131.324,E,1,08,0,9,133.4,M,46.9,M,,*42
                                - $GPGGA is the NMEA 0183 sentence ID for the GPS fix data.
                                - 121505 is the fix taken at 12:15:05 UTC
                                - 4807.038, N is latitude 48d 07.038'N
                                - 01131.324,E is longitude 11d 31.324'E
                                - 1 is the fix quality. The fix quality can have a value between 0 and 3, defined as follows
                                    - 0=no fix
                                    - 1=GPS or standard positioning service (SPS) fix
                                    - 2=DGPS fix
                                    - 3=Precise positioning service (PPS) fix
                                - 08 is the number of SV's being tracked
                                - 0.9 is the horizontal dilution of position (HDOP)
                                - 133.4,M is the altitude, in meters, above mean sea level
                                - 46.9,M is the height of the geoid (mean sea level) above the WGS84 ellipsoid
                                - (empty field) is the DGPS station ID number
                                - *42 is the checksum field
                             */
                            string messageDate, messageLatitude, messageLongitude, messageFixQuality, messageSatellites, messageHorizontalDilution, messageAltitude, messageHeight, messageStationID;
                            messageDate = nmeaData[1];

                            Console.WriteLine("Recognized Identifier: $GPGGA");
                        }
                        else // unrecognized
                        {
                            Console.WriteLine("Unrecognized identifier");
                            Console.WriteLine("     '" + nmeaLine + "'");
                        }
                    }

					// Get the rest of the data.
					client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
						new AsyncCallback(ReceiveCallback), state);
				}
				else
				{
					// All the data has arrived; put it in response.
                    //if (state.sb.Length > 1)
                    //{
                    //    //response = state.sb.ToString();
                    //    Console.WriteLine("Received: " + Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    //}
					// Signal that all bytes have been received.
					receiveDone.Set();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private static void Send(Socket client, String data)
		{
			// Convert the string data to byte data using ASCII encoding.
			byte[] byteData = Encoding.ASCII.GetBytes(data);

			// Begin sending the data to the remote device.
			client.BeginSend(byteData, 0, byteData.Length, 0,
				new AsyncCallback(SendCallback), client);
		}

		private static void SendCallback(IAsyncResult ar)
		{
			try
			{
				// Retrieve the socket from the state object.
				Socket client = (Socket)ar.AsyncState;

				// Complete sending the data to the remote device.
				int bytesSent = client.EndSend(ar);
				Console.WriteLine("Sent {0} bytes to server.", bytesSent);

				// Signal that all bytes have been sent.
				sendDone.Set();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		public static int Main(String[] args)
		{
			StartClient(args[0]);
			return 0;
		}
	}
}
