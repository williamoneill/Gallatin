using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpStreamParser : IHttpStreamParser, IHttpStreamParserContext
    {
        private readonly object _mutex = new object();
        private MemoryStream _bodyData;
        private IHttpStreamParserState _state;

        public HttpStreamParser()
        {
            _bodyData = new MemoryStream();
            State = new ReadHeaderState(this);
        }

        #region IHttpStreamParser Members

        public event EventHandler<HttpRequestHeaderEventArgs> ReadRequestHeaderComplete;
        public event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        public event EventHandler MessageReadComplete;
        public event EventHandler<HttpDataEventArgs> BodyAvailable;
        public event EventHandler AdditionalDataRequested;
        public event EventHandler<HttpDataEventArgs> PartialDataAvailable;

        public void AppendData( byte[] data )
        {
            lock ( _mutex )
            {
                State.AcceptData( data );
            }
        }

        public void Reset()
        {
        }

        #endregion

        #region IHttpStreamParserContext Members

        public void OnReadRequestHeaderComplete( string version, IHttpHeaders headers, string method, string path )
        {
            WebLog.Logger.Verbose( "ReadRequestHeaderComplete event raised" );

            EventHandler<HttpRequestHeaderEventArgs> requestEvent = ReadRequestHeaderComplete;
            lock ( _mutex )
            {
                if ( requestEvent != null )
                {
                    requestEvent( this, new HttpRequestHeaderEventArgs( version, headers, method, path ) );
                }
            }
        }

        public void OnReadResponseHeaderComplete( string version, IHttpHeaders headers, int statusCode, string statusMessage )
        {
            WebLog.Logger.Verbose( "ReadResponseHeaderComplete event raised" );

            EventHandler<HttpResponseHeaderEventArgs> responseEvent = ReadResponseHeaderComplete;
            lock ( _mutex )
            {
                if ( responseEvent != null )
                {
                    responseEvent( this, new HttpResponseHeaderEventArgs( version, headers, statusCode, statusMessage ) );
                }
            }
        }

        public void AppendBodyData( byte[] data )
        {
            WebLog.Logger.Verbose( () => string.Format( "Appending data to body: {0}", Encoding.UTF8.GetString( data ) ) );

            // Only write to the memory stream if someone is subscribed to the event. The profiler
            // showed this was an expensive operation. Avoid this work if possible.
            EventHandler<HttpDataEventArgs> bodyAvailable = BodyAvailable;
            lock ( _mutex )
            {
                if ( bodyAvailable != null )
                {
                    _bodyData.Write( data, 0, data.Length );
                }
            }
        }

        public void OnAdditionalDataRequested()
        {
            WebLog.Logger.Verbose( "AdditionalDataRequested event raised" );

            EventHandler needMoreData = AdditionalDataRequested;
            lock ( _mutex )
            {
                if ( needMoreData != null )
                {
                    needMoreData( this, new EventArgs() );
                }
            }
        }

        public void OnPartialDataAvailable( byte[] partialData )
        {
            WebLog.Logger.Verbose( "PartialDataAvailable event raised" );

            EventHandler<HttpDataEventArgs> partialDataAvailable = PartialDataAvailable;
            lock ( _mutex )
            {
                if ( partialDataAvailable != null )
                {
                    partialDataAvailable( this, new HttpDataEventArgs( partialData ) );
                }
            }
        }

        public void OnMessageReadComplete()
        {
            WebLog.Logger.Verbose( "MessageReadComplete event raised" );

            EventHandler readComplete = MessageReadComplete;

            lock ( _mutex )
            {
                if ( readComplete != null )
                {
                    readComplete( this, new EventArgs() );
                }
            }

            //OnAdditionalDataRequested();
        }

        public void OnBodyAvailable()
        {
            WebLog.Logger.Verbose( "BodyAvailable event raised" );

            EventHandler<HttpDataEventArgs> bodyAvailable = BodyAvailable;
            lock ( _mutex )
            {
                if ( bodyAvailable != null )
                {
                    bodyAvailable( this, new HttpDataEventArgs( _bodyData.ToArray() ) );
                }

                // Reset contents after raising the event
                _bodyData = new MemoryStream();
            }
        }


        public IHttpStreamParserState State
        {
            get
            {
                return _state;
            }
            set
            {
                WebLog.Logger.Verbose( () => string.Format( "Changing state to {0}", value.GetType() ) );
                _state = value;
            }
        }

        #endregion
    }
}