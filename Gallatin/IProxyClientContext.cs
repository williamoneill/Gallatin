namespace Gallatin.Core
{
    internal interface IProxyClientContext
    {
        IProxyClientState State { get; set; }
    }
}