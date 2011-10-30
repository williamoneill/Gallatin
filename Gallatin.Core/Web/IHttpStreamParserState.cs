namespace Gallatin.Core.Web
{
    /// <summary>
    /// Interface for HTTP stream parser state classes
    /// </summary>
    public interface IHttpStreamParserState
    {
        /// <summary>
        /// Accepts and processes new network data
        /// </summary>
        /// <param name="data">Data from the network stream</param>
        void AcceptData( byte[] data );
    }
}