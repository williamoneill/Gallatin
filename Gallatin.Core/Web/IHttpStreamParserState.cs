namespace Gallatin.Core.Web
{
    public interface IHttpStreamParserState
    {
        void AcceptData( byte[] data );
    }
}