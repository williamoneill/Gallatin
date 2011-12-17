using System;
using System.ComponentModel.Composition;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Session state export attribute used to identify specific states in MEF
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false )]
    public class ExportSessionStateAttribute : ExportAttribute
    {
        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        public ExportSessionStateAttribute() : base( typeof (ISessionState) )
        {
        }

        /// <summary>
        /// Gets and sets the session state type
        /// </summary>
        public SessionStateType SessionStateType { get; set; }
    }
}