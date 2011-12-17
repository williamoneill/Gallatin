namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Session state metadata, used by <see cref="SessionStateRegistry"/>
    /// </summary>
    public interface ISessionStateMetadata
    {
        /// <summary>
        /// Gets the session state type
        /// </summary>
        SessionStateType SessionStateType { get; }
    }
}