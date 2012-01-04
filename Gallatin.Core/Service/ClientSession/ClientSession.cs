using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.ClientSession
{
    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (IProxySession) )]
    internal class ClientSession : IProxySession
    {
        private readonly ManualResetEvent _connectToServerEvent = new ManualResetEvent( false );
        private readonly IServerDispatcher _dispatcher;
        private readonly string _id;
        private readonly object _resetMutex = new object();
        private INetworkFacade _clientConnection;
        private bool _hasClientStoppedSendingData;
        private IHttpStreamParser _parser;

        [ImportingConstructor]
        public ClientSession( IServerDispatcher dispatcher )
        {
            Contract.Requires( dispatcher != null );

            ServiceLog.Logger.Verbose( "{0} ClientSession -- constructor", Id );

            _id = Guid.NewGuid().ToString();
            _dispatcher = dispatcher;
            _dispatcher.SessionId = Id;
            _dispatcher.FatalErrorOccurred += _dispatcher_FatalErrorOccurred;
            _dispatcher.PartialDataAvailable += _dispatcher_PartialDataAvailable;
            _dispatcher.ReadResponseHeaderComplete += _dispatcher_ReadResponseHeaderComplete;
            _dispatcher.AllServersInactive += _dispatcher_AllServersInactive;
        }

        #region IProxySession Members

        public string Id
        {
            get
            {
                return _id;
            }
        }

        public event EventHandler SessionEnded;

        public void Start( INetworkFacade connection )
        {
            Contract.Ensures( _clientConnection != null );
            Contract.Ensures( _parser != null );

            ServiceLog.Logger.Info( "{0} Starting new proxy client session with client ID {1}", Id, connection.Id );

            _parser = new HttpStreamParser();
            _parser.AdditionalDataRequested += HandleParserAdditionalDataRequested;
            _parser.ReadRequestHeaderComplete += HandleParserReadRequestHeaderComplete;
            _parser.PartialDataAvailable += HandleParserPartialDataAvailable;

            _clientConnection = connection;
            _clientConnection.ConnectionClosed += HandleClientConnectionClosed;

            _clientConnection.BeginReceive( ReceiveDataFromClient );

            _hasClientStoppedSendingData = false;
        }

        public void Reset()
        {
            Contract.Ensures( _clientConnection == null );

            ServiceLog.Logger.Info( "{0} Session reset", Id );

            try
            {
                lock ( _resetMutex )
                {
                    if ( _clientConnection != null )
                    {
                        _clientConnection.ConnectionClosed -= HandleClientConnectionClosed;
                        _clientConnection.BeginClose(HandleClose);

                        // Unwire client events
                        _parser.AdditionalDataRequested -= HandleParserAdditionalDataRequested;
                        _parser.ReadRequestHeaderComplete -= HandleParserReadRequestHeaderComplete;
                        _parser.PartialDataAvailable -= HandleParserPartialDataAvailable;

                        _parser = null;
                        _clientConnection = null;

                        _dispatcher.Reset();

                        EventHandler sessionEnded = SessionEnded;
                        if ( sessionEnded != null )
                        {
                            SessionEnded( this, new EventArgs() );
                        }

                        _hasClientStoppedSendingData = false;
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception resetting client session.", Id ), ex );
                Reset();
            }
        }

        #endregion

        private void _dispatcher_AllServersInactive( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- All servers inactive event handled", Id );

            Reset();
        }

        private void _dispatcher_ReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            ServiceLog.Logger.Verbose( () => string.Format( "{0} ClientSession -- read HTTP response header complete\r\n{1}", Id, Encoding.UTF8.GetString( HttpResponse.CreateResponse(e).GetBuffer() )  ));

            if(_clientConnection!=null)
                _clientConnection.BeginSend( e.GetBuffer(), HandleSendToClient );
        }

        private void HandleSendToClient( bool success, INetworkFacade facade )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- handle send data to client", Id );

            if ( !success )
            {
                ServiceLog.Logger.Warning( "{0} Unable to send to client", Id );
                Reset();
            }
        }

        private void _dispatcher_PartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- partial data available from server -- send to client", Id );

            if(_clientConnection!=null)
            _clientConnection.BeginSend( e.Data, HandleSendToClient );
        }


        private void _dispatcher_FatalErrorOccurred( object sender, EventArgs e )
        {
            ServiceLog.Logger.Error( "{0} A fatal error occured in the server dispatcher. Shutting down client.", Id );
            Reset();
        }


        private void HandleParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            try
            {
                ServiceLog.Logger.Verbose( "{0} ClientSession -- waiting to send partial data to server", Id );
                _connectToServerEvent.WaitOne();
                ServiceLog.Logger.Verbose( "{0} ClientSession -- sending partial data to server", Id );

                _dispatcher.SendServerData( e.Data, HandleServerSend );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception sending data to server.", Id ), ex );
                Reset();
            }
        }

        private void HandleServerSend( bool success )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- handle data sent to server", Id );

            try
            {
                if ( success )
                {
                    if ( !_hasClientStoppedSendingData )
                    {
                        _clientConnection.BeginReceive( ReceiveDataFromClient );
                    }
                }
                else
                {
                    ServiceLog.Logger.Info( "{0} Failed to send request to server. Resetting connection.", Id );
                    Reset();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception sending data to server.", Id ), ex );
                Reset();
            }
        }


        private void HandleParserReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- read HTTP request header from client\r\n{1}", Id, Encoding.UTF8.GetString(e.GetBuffer()) );

            try
            {
                // TODO: apply filter here

                // Hold off sending data until the connection is established
                _connectToServerEvent.Reset();

                _dispatcher.BeginConnect( HttpRequest.CreateRequest( e ), HandleServerConnect );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception connecting to remote host.", Id ), ex );
                Reset();
            }
        }

        private void HandleServerConnect( bool success, IHttpRequest request )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- handle server connect", Id );

            try
            {
                // Send the header before releasing the connect event. This ensures that the header is sent before
                // any additional data in the HTTP request body
                _dispatcher.SendServerData( request.GetBuffer(), HandleServerSend );

                _connectToServerEvent.Set();

                if ( !success )
                {
                    ServiceLog.Logger.Warning( "{0} Unable to connecto to remote host. Facade returned false.", Id );
                    Reset();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception connecting to remote host.", Id ), ex );
                Reset();
            }
        }


        private void HandleParserAdditionalDataRequested( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- requested additional data from client", Id );

            try
            {
                if ( _hasClientStoppedSendingData )
                {
                    ServiceLog.Logger.Verbose(
                        "{0} ClientSession -- client stopped sending data. Parser request for more data will be ignored.", Id );
                }
                else
                {
                    _clientConnection.BeginReceive( ReceiveDataFromClient );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception receiving data from client.", Id ), ex );
                Reset();
            }
        }

        private void HandleClientConnectionClosed( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- client closed connection", Id );
            Reset();
        }

        private void ReceiveDataFromClient( bool success, byte[] rawData, INetworkFacade client )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- received data from client", Id );

            try
            {
                if ( success )
                {
                    if ( rawData == null )
                    {
                        ServiceLog.Logger.Info( "{0} Client socket has stopped sending data.", Id );
                        _hasClientStoppedSendingData = true;

                        if ( _dispatcher.PipeLineDepth == 0 )
                        {
                            ServiceLog.Logger.Info( "{0} No active servers remain and client shutdown socket. Resetting.", Id );
                            Reset();
                        }
                    }
                    else
                    {
                        _parser.AppendData( rawData );
                    }
                }
                else
                {
                    ServiceLog.Logger.Info( "{0} Client receive reported failure. Resetting client.", Id );
                    Reset();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception receiving data from client.", Id ), ex );
                Reset();
            }
        }


        private void HandleClose( bool success, INetworkFacade client )
        {
            ServiceLog.Logger.Verbose( "{0} ClientSession -- client closing socket", Id );

            try
            {
                // Not much we can do here if unsuccessful...
                if ( !success )
                {
                    ServiceLog.Logger.Warning( "{0} Unable to shutdown client session", Id );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception closing client connection", Id ), ex );
            }
        }
    }
}