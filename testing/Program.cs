using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace testing
{
    class Program
    {
        static void Main(string[] args)
        {
            string encodedData = "3534E:002EnFJa4Q>?R6SEAN20SA";
            string inBinary = "";

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

                Console.WriteLine(aChar + ":" + asciiValue.ToString() + ":" + tranform + ":" + binaryValue);
            }

            Console.WriteLine(encodedData);
            Console.WriteLine(inBinary);

            uint messageType = Convert.ToUInt32(inBinary.Substring(0, 6), 2);
            string mmsi;
            int convertedLat, convertedLong;

            if (messageType == 1 || messageType == 2 || messageType == 3)
            {
                try
                {
                    // only care about mmsi, lat, and long
                    mmsi = Convert.ToUInt32(inBinary.Substring(8, 30), 2).ToString();
                    convertedLat = Convert.ToInt32(inBinary.Substring(89, 27), 2);
                    convertedLong = Convert.ToInt32(inBinary.Substring(60, 28), 2);

                    Console.WriteLine(string.Format("        MMSI: {0}\n        Latitude: {1}\n        Longitude: {2}", mmsi, convertedLat.ToString(), convertedLong.ToString()));
                }
                catch
                {

                }
            }
            
        }
    }
}
