using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Gallatin.Filter.Util
{
    /// <summary>
    /// 
    /// </summary>
    public static class IpAddressParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipFilter"></param>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public static bool IsMatch( string ipFilter, string ipAddress )
        {
            if(string.IsNullOrEmpty(ipFilter))
            {
                throw new ArgumentNullException( "ipFilter" );
            }

            if(ipAddress == null)
            {
                throw new ArgumentException( "ipAddress" );
            }

            // Accept:
            // 192.168.0.1
            // 192.168.0.2-192.168.0.6
            // 192.168.0.0/24

            string[] tokens = ipFilter.Split( '.', '-', '/' );

            const int TokensInIpAddress = 4;
            const int TokensInRange = 8;
            const int TokensInMask = 5;

            switch( tokens.Length )
            {
                case TokensInIpAddress:
                    return ipFilter == ipAddress;

                case TokensInRange:
                    return RangeCheck( ipFilter, ipAddress );

                case TokensInMask:
                    return MaskCheck( ipFilter, ipAddress );

                default:
                    throw new ArgumentException( "Unrecognized IP filter" );
            }

        }

        // Found at http://stackoverflow.com/questions/1470792/how-to-calculate-the-ip-range-when-the-ip-address-and-the-netmask-is-given
        private static bool MaskCheck(string ipFilter, string ipAddress)
        {
            string[] tokens = ipFilter.Split( '/' );

            IPAddress ip = IPAddress.Parse( tokens[0] );
            int bits = int.Parse(tokens[1]);

            uint mask = ~(uint.MaxValue >> bits);

            // Convert the IP address to bytes.
            byte[] ipBytes = ip.GetAddressBytes();

            // BitConverter gives bytes in opposite order to GetAddressBytes().
            byte[] maskBytes = BitConverter.GetBytes(mask).Reverse().ToArray();

            byte[] startIPBytes = new byte[ipBytes.Length];
            byte[] endIPBytes = new byte[ipBytes.Length];

            // Calculate the bytes of the start and end IP addresses.
            for (int i = 0; i < ipBytes.Length; i++)
            {
                startIPBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
                endIPBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            // Convert the bytes to IP addresses.
            IPAddress startIP = new IPAddress(startIPBytes);
            IPAddress endIP = new IPAddress(endIPBytes);

            return RangeCheck( startIP.ToString() + "-" + endIP.ToString(), ipAddress );
        }

        // Taken from http://stackoverflow.com/questions/461742/how-to-convert-an-ipv4-address-into-a-integer-in-c
        private static long ToNumber(string addr)
        {
            // careful of sign extension: convert to uint first;
            // unsigned NetworkToHostOrder ought to be provided.
#pragma warning disable 612,618
            return (long)(uint)IPAddress.NetworkToHostOrder((int)IPAddress.Parse(addr).Address);
#pragma warning restore 612,618
        }

        private static string ToAddr(long address)
        {
            return IPAddress.Parse(address.ToString()).ToString();
            // This also works:
            // return new IPAddress((uint) IPAddress.HostToNetworkOrder(
            //    (int) address)).ToString();
        }

        private static bool RangeCheck( string ipFilter, string ipAddress )
        {
            string[] tokens = ipFilter.Split( '-' );

            if (tokens.Count() != 2)
                throw new ArgumentException( "Unparsable IP address range" );

            long lowAddress = ToNumber( tokens[0] );
            long highAddress = ToNumber( tokens[1] );
            long targetAddress = ToNumber( ipAddress );

            if (lowAddress > highAddress)
            {
                throw new ArgumentException( "IP address range is invalid. First address is higher than the second." );
            }

            return (lowAddress <= targetAddress) && (targetAddress <= highAddress);
        }

        
    }
}
