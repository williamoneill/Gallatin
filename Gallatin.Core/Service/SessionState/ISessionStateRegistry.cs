using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Registry that maintains all active session states
    /// </summary>
    public interface ISessionStateRegistry
    {
        /// <summary>
        /// Gets and sets the session states
        /// </summary>
        [ImportMany]
        IEnumerable<Lazy<ISessionState, ISessionStateMetadata>> States { get; set; }

        /// <summary>
        /// Gets the specified state
        /// </summary>
        /// <param name="sessionStateType">Target state type</param>
        /// <returns>Instance for the specified state type</returns>
        ISessionState GetState( SessionStateType sessionStateType );
    }
}