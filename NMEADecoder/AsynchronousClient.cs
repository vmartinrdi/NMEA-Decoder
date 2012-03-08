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
        private static StreamWriter writeTo;

		private static void StartClient(string dataString)
		{
			// Connect to a remote device.
			try
			{
                writeTo = new StreamWriter(@"C:\nmeaTestFile.txt", false);
                
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
                    //Console.WriteLine(data);
                    //writeTo.WriteLine(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // parse the data into separate lines for decoding
                    string[] nmeaLines = data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

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

                            if (nmeaData.Length >= 6)
                            {
                                Console.WriteLine("Message from Gatehouse");
                                Console.WriteLine("        '" + nmeaLine + "'");
                                Console.WriteLine(string.Format("        Type: {0}\n        Date: {1}\n        Country: {2}\n        Region: {3}\n        MMSI: {4}", nmeaData[1], nmeaData[2], nmeaData[3], nmeaData[4], nmeaData[5]));

                                writeTo.WriteLine("Message from Gatehouse");
                                writeTo.WriteLine("        '" + nmeaLine + "'");
                                writeTo.WriteLine(string.Format("        Type: {0}\n        Date: {1}\n        Country: {2}\n        Region: {3}\n        MMSI: {4}", nmeaData[1], nmeaData[2], nmeaData[3], nmeaData[4], nmeaData[5]));
                            }
                        }
                        else if (nmeaLine.StartsWith("!AIVDM"))
                        {
                            // contain encoded data
                            /*
                             * Contains encoded data
                             * Format is:
                             * !AIVDM,1,1,,A,14eG;o@034o8sd<L9i:a;WF>062D,0*7D
                             *      - !AIVDM is the NMEA 0183 sentence ID
                             *      - Number of sentences
                             *      - Sentence number
                             *      - Sequential message ID for multi-sentence messages
                             *      - The AIS channel (A or B)
                             *      - The encoded data
                             *      - End of encoded data
                             *      - NMEA checksum
                             */

                            if (nmeaData.Length == 7)
                            {
                                // only interested in encoded data - index 5
                                string encodedData = nmeaData[5];
                                string inBinary = "";

                                string mmsi, latitude, longitude;

                                byte[] asciiChars = Encoding.ASCII.GetBytes(encodedData);

                                foreach (byte aChar in asciiChars)
                                {
                                    int asciiValue = int.Parse(aChar.ToString());

                                    int tranform = asciiValue - 48;
                                    if (tranform > 40)
                                        tranform = tranform - 8;

                                    string binaryValue = Convert.ToString(tranform, 2);
                                    binaryValue = binaryValue.PadLeft(6, '0');

                                    inBinary += binaryValue;
                                }

                                uint messageType = Convert.ToUInt32(inBinary.Substring(0, 6), 2);
                                int convertedLat, convertedLong;
                                

                                // different formats for different message types - first character is message type
                                if (messageType == 1 || messageType == 2 || messageType == 3)
                                {
                                    try
                                    {
                                        // only care about mmsi, lat, and long
                                        mmsi = Convert.ToUInt32(inBinary.Substring(8, 30), 2).ToString();
                                        convertedLat = Convert.ToInt32(inBinary.Substring(89, 27), 2) / 600000;
                                        convertedLong = Convert.ToInt32(inBinary.Substring(60, 28), 2) / 600000;

                                        Console.WriteLine("Position Data");
                                        Console.WriteLine("        '" + nmeaLine + "'");
                                        Console.WriteLine(string.Format("        MMSI: {0}\n        Latitude: {1}\n        Longitude: {2}", mmsi, convertedLat.ToString(), convertedLong.ToString()));

                                        writeTo.WriteLine("Position Data");
                                        writeTo.WriteLine("        '" + nmeaLine + "'");
                                        writeTo.WriteLine(string.Format("        MMSI: {0}\n        Latitude: {1}\n        Longitude: {2}", mmsi, convertedLat.ToString(), convertedLong.ToString()));
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                            //else
                            //{
                            //    // the sentence was cut off, or the encoded portion of the data contains a comma
                            //}
                        }
                        else if (nmeaLine.StartsWith("$PSHI"))
                        {
                            // do not contain encoded data

                            Console.WriteLine("Unrecognized identifier: $PSHI");
                            Console.WriteLine("        '" + nmeaLine + "'");

                            writeTo.WriteLine("Unrecognized identifier: $PSHI");
                            writeTo.WriteLine("        '" + nmeaLine + "'");
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
                            if (nmeaData.Length >= 15)
                            {
                                string messageDate, messageLatitude, messageLongitude, messageFixQuality, messageSatellites, messageHorizontalDilution, messageAltitude, messageHeight, messageStationID;
                                messageDate = nmeaData[1];
                                messageFixQuality = nmeaData[6];
                                messageSatellites = nmeaData[7];
                                messageHorizontalDilution = nmeaData[8] + "." + nmeaData[9];
                                messageAltitude = nmeaData[10] + ", " + nmeaData[11];
                                messageHeight = nmeaData[12] + ", " + nmeaData[13];
                                messageStationID = nmeaData[14];

                                // lat and long are the most important and need to be formatted
                                string formattedLatitude, formattedLongitude;
                                formattedLatitude = string.Format("{0}d {1}'{2}", nmeaData[2].Substring(0, 2), nmeaData[2].Substring(2), nmeaData[3]);
                                formattedLongitude = string.Format("{0}d {1}'{2}", nmeaData[4].Substring(0, 2), nmeaData[4].Substring(2), nmeaData[5]);

                                Console.WriteLine("Recognized Identifier: $GPGGA");
                                Console.WriteLine("        '" + nmeaLine + "'");
                                Console.WriteLine(string.Format("        Date: {0}\n        Lat: {1}\n        Long: {2}", messageDate, formattedLatitude, formattedLongitude));
                                Console.WriteLine(string.Format("        Type: {0}\n        Date: {1}\n        Country: {2}\n        Region: {3}\n        MMSI: {4}", nmeaData[1], nmeaData[2], nmeaData[3], nmeaData[4], nmeaData[5]));

                                writeTo.WriteLine("Recognized Identifier: $GPGGA");
                                writeTo.WriteLine("        '" + nmeaLine + "'");
                                writeTo.WriteLine(string.Format("        Date: {0}\n        Lat: {1}\n        Long: {2}", messageDate, formattedLatitude, formattedLongitude));
                                writeTo.WriteLine(string.Format("        Type: {0}\n        Date: {1}\n        Country: {2}\n        Region: {3}\n        MMSI: {4}", nmeaData[1], nmeaData[2], nmeaData[3], nmeaData[4], nmeaData[5]));
                            }
                        }
                        else // unrecognized
                        {
                            Console.WriteLine("Unrecognized identifier: '" + nmeaData[0] + "'");
                            Console.WriteLine("        '" + nmeaLine + "'");

                            writeTo.WriteLine("Unrecognized identifier: '" + nmeaData[0] + "'");
                            writeTo.WriteLine("        '" + nmeaLine + "'");
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
