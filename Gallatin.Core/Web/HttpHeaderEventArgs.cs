using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// HTTP header event arguments base class
    /// </summary>
    public abstract class HttpHeaderEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="version">HTTP version</param>
        /// <param name="headers">HTTP headers</param>
        protected HttpHeaderEventArgs( string version, IHttpHeaders headers )
        {
            Contract.Requires( !string.IsNullOrEmpty( version ) );
            Contract.Requires( headers != null );

            Version = version;
            Headers = headers;
        }

        /// <summary>
        /// Gets a flag indicating if the message has a HTTP body
        /// </summary>
        public bool HasBody
        {
            get
            {
                // HTTP 1.0 always has the potential for a body even if the content-length is not specified.
                if ( Version == "1.0" )
                {
                    return true;
                }

                string contentLength = Headers["content-length"];
                string chunkedData = Headers["transfer-encoding"];

                return ( ( contentLength != null && int.Parse( contentLength ) > 0 )
                         || ( chunkedData != null && chunkedData.ToLower().Contains( "chunked" ) ) );
            }
        }

        /// <summary>
        /// Gets the HTTP version
        /// </summary>
        public string Version { get; protected set; }

        /// <summary>
        /// Gets a reference to the HTTP headers
        /// </summary>
        public IHttpHeaders Headers { get; protected set; }

        /// <summary>
        /// Creates the first line in the HTTP message
        /// </summary>
        /// <returns>Formatted line for the HTTP message</returns>
        protected abstract string CreateFirstLine();

        /// <summary>
        /// Gets the raw representation used to send the message on the network
        /// </summary>
        /// <returns>Raw representation of the instance</returns>
        public byte[] GetBuffer()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat( "{0}\r\n", CreateFirstLine() );

            foreach ( KeyValuePair<string, string> header in Headers.AsEnumerable() )
            {
                builder.AppendFormat( "{0}: {1}\r\n", header.Key, header.Value );
            }

            builder.AppendFormat( "\r\n" );

            return Encoding.UTF8.GetBytes( builder.ToString() );
        }
    }
}