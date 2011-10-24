namespace Gallatin.Core.Web
{
    public interface IHttpStreamParserContext
    {
        IHttpStreamParserState State { get; set; }
        void OnPartialDataAvailable( byte[] partialData );
        void OnMessageReadComplete();
        void OnBodyAvailable();
        void OnAdditionalDataRequested();
        void OnReadRequestHeaderComplete( string version, HttpHeaders headers, string method, string path );
        void OnReadResponseHeaderComplete( string version, HttpHeaders headers, int statusCode, string statusMessage );
        void AppendBodyData( byte[] buffer );
    }
}