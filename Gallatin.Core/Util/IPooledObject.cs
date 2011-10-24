namespace Gallatin.Core.Util
{
    /// <summary>
    /// Interface for pooled objects
    /// </summary>
    public interface IPooledObject
    {
        /// <summary>
        /// Resets the state of the object so it can be reused in a stateless manner
        /// </summary>
        void Reset();
    }
}