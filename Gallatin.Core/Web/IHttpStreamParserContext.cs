using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// Interfaces for classes tha expose state for HTTP transitions
    /// </summary>
    public interface IHttpStreamParserContext
    {
        /// <summary>
        /// Gets and sets the context state reference
        /// </summary>
        IHttpStreamParserState State { get; set; }
        
        /// <summary>
        /// Raises the partial data available event
        /// </summary>
        /// <param name="partialData">Partial HTTP data received</param>
        void OnPartialDataAvailable( byte[] partialData );
        
        /// <summary>
        /// Raises the message read complete event
        /// </summary>
        void OnMessageReadComplete();

        /// <summary>
        /// Raises the HTTP body available event
        /// </summary>
        void OnBodyAvailable();

        /// <summary>
        /// Raises the event to request additional data from the network
        /// </summary>
        void OnAdditionalDataRequested();

        /// <summary>
        /// Raises the HTTP request header complete event
        /// </summary>
        /// <param name="version">HTTP version</param>
        /// <param name="headers">HTTP headers</param>
        /// <param name="method">HTTP method</param>
        /// <param name="path">HTTP path</param>
        void OnReadRequestHeaderComplete( string version, IHttpHeaders headers, string method, string path );
        
        /// <summary>
        /// Raises the HTTP response header complete event
        /// </summary>
        /// <param name="version">HTTP version</param>
        /// <param name="headers">HTTP headers</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="statusMessage">HTTP status message</param>
        void OnReadResponseHeaderComplete( string version, IHttpHeaders headers, int statusCode, string statusMessage );
        
        /// <summary>
        /// Appends data to the internal HTTP body buffer
        /// </summary>
        /// <param name="buffer">Data to append to the buffer</param>
        void AppendBodyData( byte[] buffer );

        /// <summary>
        /// Flushes all pending data-related events. Usually invoked when the end of a HTTP message is detected.
        /// </summary>
        void Flush();
    }
}