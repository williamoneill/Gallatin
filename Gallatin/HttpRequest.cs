namespace Gallatin.Core
{
    public class HttpRequest : HttpMessageOld
    {
        public string DestinationAddress { get; set; }
        public HttpActionType RequestType { get; set; }
    }
}