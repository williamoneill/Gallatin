using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Session state registry, maintains instances of active session states
    /// </summary>
    [Export( typeof (ISessionStateRegistry) )]
    internal class SessionStateRegistry : ISessionStateRegistry
    {
        #region ISessionStateRegistry Members

        [ImportingConstructor]
        public SessionStateRegistry( IProxyFilter filter, INetworkFacadeFactory factory )
        {
            // TODO: figure out this error. Cannot compose part 'Gallatin.Core.Service.SessionState.ClientConnectingState' because a cycle exists in the dependencies between the exports being composed.
            // For now, force the dependencies to be resolved
        }

        [ImportMany]
        public IEnumerable<Lazy<ISessionState, ISessionStateMetadata>> States { get; set; }

        public ISessionState GetState( SessionStateType sessionStateType )
        {
            return States.Where( v => v.Metadata.SessionStateType.Equals( sessionStateType ) ).Single().Value;
        }

        #endregion
    }
}