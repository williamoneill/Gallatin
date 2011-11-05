using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// Represents the HTTP request
    /// </summary>
    public class HttpRequest : IHttpRequest
    {
        internal static HttpRequest CreateRequest(HttpRequestHeaderEventArgs args)
        {
            HttpRequest request = new HttpRequest
                                  {
                                      Path = args.Path,
                                      Version = args.Version,
                                      Method = args.Method,
                                      Headers = args.Headers,
                                      IsSsl = args.IsSsl
                                  };

            return request;
        }

        /// <summary>
        /// Relative path of the remote resource
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// HTTP version
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// HTTP method
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// HTTP headers
        /// </summary>
        public IHttpHeaders Headers { get; private set; }

        /// <summary>
        /// Gets the flag indicating if the request uses SSL (HTTPS)
        /// </summary>
        public bool IsSsl { get; private set; }

        /// <summary>
        /// Gets the HTTP body
        /// </summary>
        public byte[] Body { get; private set; }
    }
}