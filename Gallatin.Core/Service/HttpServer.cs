using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Filters;
using Gallatin.Core.Web;

namespace Gallatin.Core.Net
{
    internal class HttpServer : IHttpServer
    {
        private readonly INetworkConnection _connection;
        private readonly IHttpStreamParser _parser;
        private readonly IHttpResponseFilter _responseFilter;
        private IHttpResponse _lastResponse;

        public HttpServer( INetworkConnection connection, IHttpResponseFilter responseFilter )
        {
            Contract.Requires( connection != null );

            _responseFilter = responseFilter;
            _connection = connection;

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

        IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> _callbacks;

        private void ParserReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            _lastResponse = HttpResponse.CreateResponse( e );

            // Always unsubscribe. This could be left over from the last request.
            _parser.PartialDataAvailable -= ParserPartialDataAvailable;
            _parser.BodyAvailable -= ParserBodyAvailable;

            if ( _responseFilter != null )
            {
                
                byte[] filterResponse = _responseFilter.ApplyResponseHeaderFilters( _lastResponse, out _callbacks );

                if ( _callbacks != null )
                {
                    // Get the full body before sending the response
                    _parser.BodyAvailable += ParserBodyAvailable;
                }

                else if ( filterResponse == null )
                {
                    // Send the received header, unmodified, and re-subscribe to publish raw data coming from the server
                    _parser.PartialDataAvailable += ParserPartialDataAvailable;
                    OnDataAvailable( e.GetBuffer() );
                }

                else
                {
                    // Send the modified response since we have enough information to know to filter the response.
                    OnDataAvailable( filterResponse );
                    Close();
                }
            }

            else
            {
                // Send the received header, unmodified, and re-subscribe to publish raw data coming from the server
                _parser.PartialDataAvailable += ParserPartialDataAvailable;
                OnDataAvailable(e.GetBuffer());
            }
        }

        private void ParserBodyAvailable( object sender, HttpDataEventArgs e )
        {
            byte[] filterResponse = _responseFilter.ApplyResponseBodyFilter( _lastResponse, e.Data, _callbacks );

            if ( filterResponse == null )
            {
                throw new InvalidOperationException( "Response body filter did not return a response for the client" );
            }

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