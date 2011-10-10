
namespace Gallatin.Core.Service
{
    public interface IProxyService
    {
        void Start( int port );

        void Stop();
    }
}