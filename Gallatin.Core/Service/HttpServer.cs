using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Gallatin.Core.Web;

namespace Gallatin.Core.Net
{
    internal class HttpServer : IHttpServer
    {
        private readonly INetworkConnection _connection;
        private readonly IHttpStreamParser _parser;
        private readonly IProxyFilter _proxyFilter;

        private IHttpResponse _lastResponse;

        public HttpServer( INetworkConnection connection, IProxyFilter proxyFilter )
        {
            Contract.Requires( connection != null );
            Contract.Requires( proxyFilter != null );

            _connection = connection;
            _proxyFilter = proxyFilter;

            connection.ConnectionClosed += ConnectionConnectionClosed;
            connection.Shutdown += ConnectionConnectionClosed;
            connection.DataAvailable += ConnectionDataAvailable;

            _parser = new HttpStreamParser();
            _parser.ReadResponseHeaderComplete += ParserReadResponseHeaderComplete;
        }

        #region IHttpServer Members

        public event EventHandler SessionClosed;
        public event EventHandler<DataAvailableEventArgs> DataAvailable;

        public void Send( byte[] data )
        {
            _connection.SendData( data );
        }

        public void Close()
        {
            _parser.Flush();
            _connection.Close();
        }

        #endregion

        private void ParserReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            _lastResponse = HttpResponse.CreateResponse( e );

            _parser.PartialDataAvailable -= ParserPartialDataAvailable;
            _parser.BodyAvailable -= ParserBodyAvailable;

            bool isBodyRequired;
            byte[] response = _proxyFilter.EvaluateResponseFilters( _lastResponse, "todo", out isBodyRequired );

            if ( isBodyRequired )
            {
                // Get the full body before sending the response
                _parser.BodyAvailable += ParserBodyAvailable;
            }

            else if ( response == null )
            {
                // Re-subscribe to partial HTTP body data being sent from the server.
                // Send the received header, unmodified.
                _parser.PartialDataAvailable += ParserPartialDataAvailable;
                OnDataAvailable( e.GetBuffer() );
            }

            else
            {
                // Send the modified response since we have enough information to know to filter the response.
                OnDataAvailable( response );
                Close();
            }
        }

        private void ParserBodyAvailable( object sender, HttpDataEventArgs e )
        {
            var filterResponse = _proxyFilter.EvaluateResponseFiltersWithBody(_lastResponse, "todo", e.Data);

            if(filterResponse==null)
                throw new InvalidOperationException("Response body filter did not return a response for the client");

            // Unsubscribe to the parser events. If we don't, the Close invocation will recursively 
            // invoke this method and we get a stack overflow.

            _parser.BodyAvailable -= ParserBodyAvailable;

            OnDataAvailable( filterResponse );
            Close();
        }

        private void ParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            OnDataAvailable( e.Data );
        }

        private void OnDataAvailable( byte[] data )
        {
            EventHandler<DataAvailableEventArgs> dataAvailableEvent = DataAvailable;
            if ( dataAvailableEvent != null )
            {
                dataAvailableEvent( this, new DataAvailableEventArgs( data ) );
            }
        }

        private void ConnectionDataAvailable( object sender, DataAvailableEventArgs e )
        {
            _parser.AppendData( e.Data );
        }

        private void ConnectionConnectionClosed( object sender, EventArgs e )
        {
            EventHandler sessionClosedEvent = SessionClosed;
            if ( sessionClosedEvent != null )
            {
                sessionClosedEvent( this, new EventArgs() );
            }
        }
    }
}