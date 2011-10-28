using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;
using Gallatin.Core.Service;
using Gallatin.Core.Util;

namespace Gallatin.Core.Web
{
    internal abstract class HttpHeaderEventArgs :EventArgs
    {
        protected HttpHeaderEventArgs( string version, HttpHeaders headers)
        {
            Contract.Requires(!string.IsNullOrEmpty(version));
            Contract.Requires(headers!=null);

            Version = version;
            Headers = headers;
        }

        public string Version { get; protected set; }
        public HttpHeaders Headers { get; protected set; }

        protected abstract string CreateFirstLine();

        public byte[] GetBuffer()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat("{0}\r\n", CreateFirstLine());

            foreach ( var header in Headers.AsEnumerable() )
            {
                builder.AppendFormat("{0}: {1}\r\n", header.Key, header.Value);
            }

            builder.AppendFormat("\r\n");

            return Encoding.UTF8.GetBytes( builder.ToString() );
        }


    }

    internal class HttpResponseHeaderEventArgs : HttpHeaderEventArgs
    {
        public HttpResponseHeaderEventArgs( string version, HttpHeaders headers, int statusCode, string statusText )
            : base(version, headers)
        {
            Contract.Requires(!string.IsNullOrEmpty(statusText));
            Contract.Requires(statusCode > 99);
            Contract.Requires(statusCode<1000);

            StatusCode = statusCode;
            StatusText = statusText;
        }

        public bool IsPersistent
        {
            get
            {
                // HTTP 1.1, assume persistent connection
                if (Version == "1.1")
                {
                    string persistentConnection = Headers["connection"];

                    return ( persistentConnection != null
                             && persistentConnection.Equals( "close", StringComparison.InvariantCultureIgnoreCase ) );
                }
                return false;
            }
        }
        public int StatusCode { get; private set; }
        public string StatusText { get; private set; }

        protected override string CreateFirstLine()
        {
            return string.Format("HTTP/{0} {1} {2}", Version, StatusCode, StatusText);
        }
    }

    internal class HttpRequestHeaderEventArgs : HttpHeaderEventArgs
    {
        public HttpRequestHeaderEventArgs(string version, HttpHeaders headers, string method, string path)
            : base(version, headers)
        {
            Contract.Requires(!string.IsNullOrEmpty(method));
            Contract.Requires(!string.IsNullOrEmpty(path));

            Path = path;
            Method = method;
        }

        public bool IsSsl { 
            get
            {
                return Method.Equals( "connect", StringComparison.InvariantCultureIgnoreCase );
            }
        }
        public string Method { get; private set; }
        public string Path { get; private set; }

        protected override string CreateFirstLine()
        {
            return string.Format("{0} {1} HTTP/{2}", Method, Path, Version);
        }
    }

    internal class HttpDataEventArgs : EventArgs
    {
        public HttpDataEventArgs(byte[] data)
        {
            Contract.Requires(data != null);

            Data = data;
        }

        public byte[] Data { get; private set; }
    }

    internal interface IHttpStreamParser : IPooledObject
    {
        /// <summary>
        /// Raised when the HTTP request header is read.
        /// </summary>
        event EventHandler<HttpRequestHeaderEventArgs> ReadRequestHeaderComplete;

        /// <summary>
        /// Raised when the HTTP response header is read.
        /// </summary>
        event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;

        /// <summary>
        /// Read when the entire HTTP message has been read (body and header)
        /// </summary>
        event EventHandler MessageReadComplete;

        /// <summary>
        /// Raised when the entire HTTP body is read. Subscribing to this event has a negative impact on performance.
        /// </summary>
        event EventHandler<HttpDataEventArgs> BodyAvailable;

        /// <summary>
        /// Raised when the parser needs additonal data to complete the message
        /// </summary>
        event EventHandler AdditionalDataRequested;
        
        /// <summary>
        /// Raised when new data is available. This may not constitute the entire message.
        /// </summary>
        event EventHandler<HttpDataEventArgs> PartialDataAvailable;

        /// <summary>
        /// Appends new data to be parsed and evaluated
        /// </summary>
        /// <param name="data"></param>
        void AppendData( byte[] data );

    }

}
