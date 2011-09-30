namespace Gallatin.Core.Client
{
    internal interface IProxyClientContext
    {
        IProxyClientState State { get; set; }
    }
}