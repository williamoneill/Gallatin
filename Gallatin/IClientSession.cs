using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface IClientSession
    {
        DateTime CreationTime { get;  }
        DateTime LastActivity { get; set; }
        void EndSession( bool inError );
        bool IsActive { get; }
        
    }
}
