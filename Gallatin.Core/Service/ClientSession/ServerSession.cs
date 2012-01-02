using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.ClientSession
{
    internal interface IServerSession
    {
        void Start( INetworkFacade serverConnection );

        bool HasStoppedSendingData { get; set; }

        bool HasClosed { get; }

        INetworkFacade Connection { get; }

        IHttpResponse LastResponseHeader { get; }

        event EventHandler<HttpDataEventArgs> PartialDataAvailableForClient;

        event EventHandler<HttpResponseHeaderEventArgs> HttpResponseHeaderAvailable;

        event EventHandler FullResponseReadComplete;
    }

    internal class ServerSession : IServerSession
    {

        public void Start( INetworkFacade serverConnection )
        {
            //Contract.Requires(serverConnection != null);

            ServiceLog.Logger.Verbose("{0} ServerSession -- started", serverConnection.Id);

            Connection = serverConnection;
            Connection.ConnectionClosed += serverConnection_ConnectionClosed;

            Parser = new HttpStreamParser();
            Parser.ReadResponseHeaderComplete += Parser_ReadResponseHeaderComplete;
            Parser.AdditionalDataRequested += Parser_AdditionalDataRequested;
            Parser.PartialDataAvailable += Parser_PartialDataAvailable;
            Parser.MessageReadComplete += Parser_MessageReadComplete;
            HasStoppedSendingData = false;
            HasClosed = false;

            Connection.BeginReceive(HandleReceive);
            
        }

        private IHttpStreamParser Parser { get; set; }

        #region IServerSession Members

        public bool HasStoppedSendingData { get; set; }

        public bool HasClosed { get; private set; }

        public INetworkFacade Connection { get; private set; }

        public IHttpResponse LastResponseHeader { get; private set; }

        public event EventHandler<HttpDataEventArgs> PartialDataAvailableForClient;
        public event EventHandler<HttpResponseHeaderEventArgs> HttpResponseHeaderAvailable;
        public event EventHandler FullResponseReadComplete;

        #endregion

        private void Parser_MessageReadComplete( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerSession -- message read complete event handler", Connection.Id );

            EventHandler readComplete = FullResponseReadComplete;
            if ( readComplete != null )
            {
                readComplete( this, e );
            }
        }

        private void Parser_PartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerSession -- partial data available event handler", Connection.Id );

            EventHandler<HttpDataEventArgs> partialDataAvailable = PartialDataAvailableForClient;
            if ( partialDataAvailable != null )
            {
                partialDataAvailable( this, e );
            }
        }

        private void Parser_AdditionalDataRequested( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerSession -- additional data requested event handler", Connection.Id );

            Connection.BeginReceive( HandleReceive );
        }

        private void HandleReceive( bool success, byte[] data, INetworkFacade server )
        {
            ServiceLog.Logger.Verbose( "{0} ServerSession -- handle facade receive", Connection.Id );

            try
            {
                if ( success )
                {
                    if ( data == null )
                    {
                        HasStoppedSendingData = true;
                    }
                    else
                    {
                        Parser.AppendData( data );
                    }
                }
                else
                {
                    Connection.BeginClose( ( s, f ) => ServiceLog.Logger.Info( "ServerSession force close" ) );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( "Failed to receive data from server. Terminating connection.", ex );
                Connection.BeginClose( ( s, f ) => ServiceLog.Logger.Info( "ServerSession force close" ) );
            }
        }

        private void Parser_ReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerSession -- read response header event handler", Connection.Id );

            LastResponseHeader = HttpResponse.CreateResponse( e );

            EventHandler<HttpResponseHeaderEventArgs> responseHeaderComplete = HttpResponseHeaderAvailable;
            if ( responseHeaderComplete != null )
            {
                responseHeaderComplete( this, e );
            }
        }

        private void serverConnection_ConnectionClosed( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} ServerSession -- socket connection closed event handler", Connection.Id );
            HasClosed = true;
        }
    }
}