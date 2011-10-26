using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace Gallatin.Core.Web
{
    internal class HttpStreamParser : IHttpStreamParser, IHttpStreamParserContext
    {
        private MemoryStream _bodyData;

        public void Reset()
        {
            _bodyData = new MemoryStream();
            State = new ReadHeaderState(this);
        }

        public HttpStreamParser()
        {
            Reset();
        }

        public void OnReadRequestHeaderComplete( string version, HttpHeaders headers, string method, string path )
        {
            WebLog.Logger.Verbose("ReadRequestHeaderComplete event raised");

            var requestEvent = ReadRequestHeaderComplete;
            lock (_mutex)
            {
                if (requestEvent != null)
                {
                    requestEvent(this, new HttpRequestHeaderEventArgs(version, headers, method, path));
                }
            }
            
        }

        public void OnReadResponseHeaderComplete( string version, HttpHeaders headers, int statusCode, string statusMessage)
        {
            WebLog.Logger.Verbose("ReadResponseHeaderComplete event raised");

            var responseEvent = ReadResponseHeaderComplete;
            lock (_mutex)
            {
                if (responseEvent != null)
                {
                    responseEvent(this, new HttpResponseHeaderEventArgs(version, headers, statusCode, statusMessage));
                }
            }
        }

        public event EventHandler<HttpRequestHeaderEventArgs> ReadRequestHeaderComplete;
        public event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        public event EventHandler MessageReadComplete;
        public event EventHandler<HttpDataEventArgs> BodyAvailable;
        public event EventHandler AdditionalDataRequested;
        public event EventHandler<HttpDataEventArgs> PartialDataAvailable;

        public void AppendBodyData( byte[] data )
        {
            WebLog.Logger.Verbose( () => string.Format("Appending data to body: {0}", Encoding.UTF8.GetString(data) ));

            // Only write to the memory stream if someone is subscribed to the event. The profiler
            // showed this was an expensive operation. Avoid this work if possible.
            var bodyAvailable = BodyAvailable;
            lock (_mutex)
            {
                if (bodyAvailable != null)
                {
                    _bodyData.Write(data, 0, data.Length);
                }
            }
        }

        public void OnAdditionalDataRequested()
        {
            WebLog.Logger.Verbose("AdditionalDataRequested event raised");

            var needMoreData = AdditionalDataRequested;
            lock ( _mutex )
            {
                if (needMoreData != null)
                {
                    needMoreData(this, new EventArgs());
                }
            }
        }

        public void OnPartialDataAvailable( byte[] partialData )
        {
            WebLog.Logger.Verbose("PartialDataAvailable event raised");

            var partialDataAvailable = PartialDataAvailable;
            lock (_mutex)
            {
                if (partialDataAvailable != null)
                {
                    partialDataAvailable(this, new HttpDataEventArgs(partialData));
                }
            }
        }

        private object _mutex = new object();

        public void AppendData( byte[] data )
        {
            Contract.Requires(data != null);
            Contract.Requires(data.Length > 0);

            lock (_mutex)
            {
                State.AcceptData(data);
            }

            WebLog.Logger.Verbose( () => string.Format( "Appending data to body: {0}", Encoding.UTF8.GetString(data)) );

        }

        public void OnMessageReadComplete()
        {
            WebLog.Logger.Verbose("MessageReadComplete event raised");

            var readComplete = MessageReadComplete;

            lock (_mutex)
            {
                if (readComplete != null)
                {
                    readComplete(this, new EventArgs());
                }
            }

            OnAdditionalDataRequested();
        }

        public void OnBodyAvailable()
        {
            WebLog.Logger.Verbose("BodyAvailable event raised");

            var bodyAvailable = BodyAvailable;
            lock (_mutex)
            {
                if (bodyAvailable != null)
                {
                    bodyAvailable(this, new HttpDataEventArgs(_bodyData.ToArray()));
                }

                // Reset contents after raising the event
                _bodyData = new MemoryStream();
            }
        }


        private IHttpStreamParserState _state;

        public IHttpStreamParserState State
        {
            get
            {
                return _state;
            }
            set
            {
                WebLog.Logger.Verbose(() => string.Format("Changing state to {0}", value.GetType()));
                _state = value;
            }
        }
    }
}