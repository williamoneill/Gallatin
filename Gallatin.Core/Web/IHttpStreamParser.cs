using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Util;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// Interface for classes that parse raw HTTP streams
    /// </summary>
    [ContractClass( typeof (IHttpStreamParserContract) )]
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
        /// <param name = "data"></param>
        void AppendData( byte[] data );
    }

    [ContractClassFor( typeof (IHttpStreamParser) )]
    internal abstract class IHttpStreamParserContract : IHttpStreamParser
    {
        #region IHttpStreamParser Members

        public abstract event EventHandler<HttpRequestHeaderEventArgs> ReadRequestHeaderComplete;
        public abstract event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        public abstract event EventHandler MessageReadComplete;
        public abstract event EventHandler<HttpDataEventArgs> BodyAvailable;
        public abstract event EventHandler AdditionalDataRequested;
        public abstract event EventHandler<HttpDataEventArgs> PartialDataAvailable;

        public void AppendData( byte[] data )
        {
            Contract.Requires(data != null);
            Contract.Requires(data.Length > 0);
        }

        #endregion

        public abstract void Reset();
    }
}