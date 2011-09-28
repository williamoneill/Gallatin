namespace Gallatin.Core
{
    public class HttpResponse : HttpMessageOld
    {
        public int ResponseCode { get; set; }
        public string Status { get; set; }
    }
}