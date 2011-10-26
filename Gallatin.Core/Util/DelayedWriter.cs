using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Util
{
    public class DelayedWriter : TraceListener
    {
        private string _fileName;

        public DelayedWriter(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            if(File.Exists(fileName))
                File.Delete(fileName);

            _fileName = fileName;

            ThreadPool.QueueUserWorkItem( Worker );
        }

        private void Worker(object state)
        {
            while (true)
            {
                Thread.Sleep(5000);

                lock (_messages)
                {
                    using (FileStream fs = new FileStream(_fileName, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        foreach (var message in _messages)
                        {
                            var bytes = Encoding.UTF8.GetBytes( message + "\r\n" );
                            fs.Write( bytes, 0, bytes.Length );
                        }
                    }
                    
                }

            }
        }

        private List<string> _messages = new List<string>();

        public override void Write( string message )
        {
        }

        public override void WriteLine( string message )
        {
            lock (_messages)
            {
                _messages.Add(message);
            }
            
        }
    }
}
