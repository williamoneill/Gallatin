namespace Gallatin.Core.Web
{
    public interface IHttpResponseMessage : IHttpMessage
    {
        int StatusCode { get;  }

        string StatusText { get; }
    }
}
