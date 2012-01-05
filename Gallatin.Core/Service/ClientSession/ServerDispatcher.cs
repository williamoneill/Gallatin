﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.ClientSession
{
    internal interface IServerDispatcher : IPooledObject
    {
        void BeginConnect( IHttpRequest requestHeader, Action<bool, IHttpRequest> serverConnectedCallback );
        void SendServerData( byte[] data, Action<bool> sendDataCallback );
        string SessionId { set; }
        int PipeLineDepth { get; }

        event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        event EventHandler<HttpDataEventArgs> PartialDataAvailable;
        event EventHandler FatalErrorOccurred;
        event EventHandler AllServersInactive;
    }

    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (IServerDispatcher) )]
    internal class ServerDispatcher : IServerDispatcher
    {
        private readonly Dictionary<string, IServerSession> _activeSessions = new Dictionary<string, IServerSession>();
        private readonly INetworkFacadeFactory _factory;

        [ImportingConstructor]
        public ServerDispatcher( INetworkFacadeFactory factory )
        {
            Contract.Requires( factory != null );

            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- constructor", SessionId );

            _factory = factory;
        }

        private IServerSession ActiveServer { get; set; }

        #region IServerDispatcher Members

        ManualResetEvent _connectingEvent = new ManualResetEvent(true);
        private string _host;
        private int _port;
        private Action<bool, IHttpRequest> _serverConnectedDelegate;
        private IHttpRequest _requestHeader;

        private int _pipeLineDepth;

        public void BeginConnect( IHttpRequest requestHeader, Action<bool, IHttpRequest> serverConnectedDelegate )
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- connecting to remote host", SessionId );

            try
            {
                int port;
                string host;

                if ( TryParseAddress( requestHeader, out host, out port ) )
                {
                    ServiceLog.Logger.Info("{0} Evaluating connection to {1}:{2}", SessionId, host, port);

                    Interlocked.Increment( ref _pipeLineDepth );

                    bool shouldConnect = true;

                    lock (_activeSessions)
                    {
                        IServerSession session;
                        if ( _activeSessions.TryGetValue( CreateKey( host, port ), out session ) )
                        {
                            if ( session.HasStoppedSendingData || session.HasClosed )
                            {
                                ServiceLog.Logger.Info( "{0} A connection has already been established to {1}:{2} host but it has shut down. This old session will be removed and a new server connection will be established.", SessionId, host, port );
                                _activeSessions.Remove( CreateKey( host, port ) );
                            }
                            else
                            {
                                ServiceLog.Logger.Info( "{0} Reusing existing server connection to {1}:{2}", SessionId, host, port );
                                shouldConnect = false;
                                ActiveServer = session;
                                serverConnectedDelegate( true, requestHeader );
                            }
                        }
                        
                    }

                    if (shouldConnect)
                    {
                        _connectingEvent.Reset();

                        _host = host;
                        _port = port;
                        _serverConnectedDelegate = serverConnectedDelegate;
                        _requestHeader = requestHeader;

                        ServiceLog.Logger.Info("{0} Connecting to remote host {1}:{2}", SessionId, host, port);
                        _factory.BeginConnect(host, port, HandleConnect);
                    }
                }
                else
                {
                    serverConnectedDelegate( false, requestHeader );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} An unhandled exception was encountered while attempting to connect to remote host", SessionId), ex );
                OnFatalErrorOccurred();
            }
        }

        private void HandleConnect(bool success, INetworkFacade serverConnection)
        {
            try
            {
                if (success)
                {
                    ServiceLog.Logger.Info(
                        "{0} ServerDispatcher -- new server connect, server id = {1}", SessionId, serverConnection.Id);

                    ServerSession server = new ServerSession();
                    _activeSessions.Add(CreateKey(_host, _port), server);

                    server.PartialDataAvailableForClient += Parser_PartialDataAvailable;
                    server.HttpResponseHeaderAvailable += Parser_ReadResponseHeaderComplete;
                    server.FullResponseReadComplete += Parser_MessageReadComplete;

                    ActiveServer = server;

                    serverConnection.ConnectionClosed += HandleServerConnectionClose;

                    server.Start(serverConnection);

                    // Only call the delegate after all events are wired
                    _serverConnectedDelegate(true, _requestHeader);
                }
                else
                {
                    ServiceLog.Logger.Info("{0} Unable to connect to remote host", SessionId);
                    _serverConnectedDelegate(false, _requestHeader);
                }

            }
            catch (Exception ex)
            {
                ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception connecting to remote host", SessionId), ex);
                OnFatalErrorOccurred();
            }
            finally
            {
                _connectingEvent.Set();
            }
        }

        public void SendServerData( byte[] data, Action<bool> sendDataCallback )
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- sending data to server\r\n{1}", SessionId, Encoding.UTF8.GetString(data) );

            if ( ActiveServer != null )
            {
                ActiveServer.Connection.BeginSend( data, HandleSend );
            }
            else
            {
                throw new InvalidOperationException( "Unable to send data when no server connection exists" );
            }
        }

        public string SessionId
        {
            get;
            set;
        }

        public int PipeLineDepth
        {
            get
            {
                return _pipeLineDepth;
            }
        }


        public event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        public event EventHandler<HttpDataEventArgs> PartialDataAvailable;
        public event EventHandler FatalErrorOccurred;
        public event EventHandler AllServersInactive;

        public void Reset()
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- reset", SessionId );


            lock ( _activeSessions )
            {
                foreach ( IServerSession serverSession in _activeSessions.Values )
                {
                    serverSession.Connection.ConnectionClosed -= HandleServerConnectionClose;

                    serverSession.PartialDataAvailableForClient -= Parser_PartialDataAvailable;
                    serverSession.HttpResponseHeaderAvailable -= Parser_ReadResponseHeaderComplete;
                    serverSession.FullResponseReadComplete -= Parser_MessageReadComplete;
                }

                _activeSessions.Clear();

                ActiveServer = null;
            }
        }

        #endregion

        public static bool TryParseAddress( IHttpRequest e, out string host, out int port )
        {
            const int HttpPort = 80;

            host = e.Headers["Host"];
            port = HttpPort;

            // Get the port from the host address if it set
            string[] tokens = host.Split( ':' );
            if ( tokens.Length == 2 )
            {
                host = tokens[0];

                if ( !int.TryParse( tokens[1], out port ) )
                {
                    return false;
                }
            }

            return !string.IsNullOrEmpty( host ) && port > 0;
        }

        private string CreateKey( string host, int port )
        {
            return string.Format( "{0}:{1}", host, port );
        }

        private IEnumerable<IServerSession> FindActiveServers()
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- finding active servers", SessionId );

            IEnumerable<IServerSession> activeSessions;

            lock ( _activeSessions )
            {
                activeSessions =
                    _activeSessions.Values.Where(
                        s => !s.HasClosed && !s.HasStoppedSendingData && s.LastResponseHeader != null && s.LastResponseHeader.IsPersistent );
            }

            ServiceLog.Logger.Verbose( () =>
                                       {
                                           string message = string.Format("{0} ACTIVE SERVERS: ", SessionId);

                                           foreach (var activeSession in activeSessions)
                                           {
                                               message += " " + activeSession.Connection.Id + " ";
                                           }

                                           return message;
                                       });

            return activeSessions;
        }

        private void HandleServerConnectionClose( object sender, EventArgs e )
        {
            Contract.Requires( sender is INetworkFacade );

            ServiceLog.Logger.Verbose( "{0}ServerDispatcher -- handle server connection close", SessionId );

            INetworkFacade server = sender as INetworkFacade;

            // Since this is an event handler, we are not guaranteed the invocation order. The "HasClosed" property
            // on the server may not be set yet. If the sender is the only active server then close down.

            IEnumerable<IServerSession> activeServers = FindActiveServers().Where( s => s.Connection != server );

            if ( activeServers.Count() == 0 )
            {
                ServiceLog.Logger.Verbose( "{0} No active servers remain.", SessionId );
                OnAllServersInactive();
            }
        }

        private void OnAllServersInactive()
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- raising all servers inactive event", SessionId );

            EventHandler allServersInactive = AllServersInactive;
            if ( allServersInactive != null )
            {
                allServersInactive( this, new EventArgs() );
            }
        }

        private void Parser_MessageReadComplete( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- server parser message read complete", SessionId );

            try
            {
                Interlocked.Decrement( ref _pipeLineDepth );

                // If this is not the active server then shut it down
                //var serverSession = sender as IServerSession;
                //if (serverSession != null && serverSession != ActiveServer)
                //{
                //    ServiceLog.Logger.Info("{0} Shutting down non-active server");
                //}

                // Notifiy the client when all servers have closed their connections, stopped sending data, are are non-persistent
                if ( !FindActiveServers().Any() )
                {
                    ServiceLog.Logger.Verbose( "{0} No active servers remain", SessionId );
                    OnAllServersInactive();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format("{0} Server Dispatcher - parser message read complete",SessionId), ex );
                OnFatalErrorOccurred();
            }
        }

        private void Parser_ReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- server parser read response header complete", SessionId );

            try
            {
                EventHandler<HttpResponseHeaderEventArgs> responseHeaderComplete = ReadResponseHeaderComplete;
                if ( responseHeaderComplete != null )
                {
                    responseHeaderComplete( this, e );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Server Dispatcher - parser read response header", SessionId), ex );
                OnFatalErrorOccurred();
            }
        }

        private void Parser_PartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- partial server data available", SessionId );

            try
            {
                EventHandler<HttpDataEventArgs> dataAvailable = PartialDataAvailable;
                if ( dataAvailable != null )
                {
                    dataAvailable( this, e );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0}Server Dispatcher - parser partial data available", SessionId), ex );
                OnFatalErrorOccurred();
            }
        }


        private void HandleSend( bool success, INetworkFacade server )
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- handle data sent to server", SessionId );

            if ( !success )
            {
                ServiceLog.Logger.Warning( "{0} Failed to send data to remote host", SessionId );
                OnFatalErrorOccurred();
            }
        }

        private void OnFatalErrorOccurred()
        {
            ServiceLog.Logger.Verbose( "{0} ServerDispatcher -- raising fatal error event", SessionId );

            EventHandler networkErrorOccurred = FatalErrorOccurred;
            if ( networkErrorOccurred != null )
            {
                networkErrorOccurred( this, new EventArgs() );
            }
        }
    }
}