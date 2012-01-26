using System;
using System.Diagnostics.Contracts;
using System.Threading;
using Gallatin.Core.Web;

namespace Gallatin.Core.Net
{
    internal class HttpServer : IHttpServer
    {
        private readonly INetworkConnection _connection;
        //private IHttpStreamParser _parser;

        public HttpServer( INetworkConnection connection )
        {
            Contract.Requires( connection != null );

            _connection = connection;

            connection.ConnectionClosed += ConnectionConnectionClosed;
            connection.Shutdown += ConnectionConnectionClosed;
            connection.DataAvailable += ConnectionDataAvailable;

            //_parser = new HttpStreamParser();
            //_parser.ReadResponseHeaderComplete += new EventHandler<HttpResponseHeaderEventArgs>(_parser_ReadResponseHeaderComplete);
            //_parser.PartialDataAvailable += new EventHandler<HttpDataEventArgs>(_parser_PartialDataAvailable);
            //_parser.MessageReadComplete += new EventHandler(_parser_MessageReadComplete);
        }

        //void _parser_MessageReadComplete(object sender, EventArgs e)
        //{
        //    var ev = this.ReceivedCompleteHttpResponse;
        //    if(ev!=null)
        //        ev(this, new EventArgs());
        //}


        //void _parser_PartialDataAvailable(object sender, HttpDataEventArgs e)
        //{
        //    OnDataAvailable(e.Data);
        //}

        //void _parser_ReadResponseHeaderComplete(object sender, HttpResponseHeaderEventArgs e)
        //{
        //    OnDataAvailable(e.GetBuffer());
        //}

        #region IHttpServer Members

        public event EventHandler SessionClosed;
        public event EventHandler<DataAvailableEventArgs> DataAvailable;

        public void Send( byte[] data )
        {
            _connection.SendData( data );
        }

        public void Close()
        {
            //_parser.Flush();
            _connection.Close();
        }

        //public event EventHandler ReceivedCompleteHttpResponse;

        #endregion

        private void ConnectionDataAvailable( object sender, DataAvailableEventArgs e )
        {
            //_parser.AppendData(e.Data);
            EventHandler<DataAvailableEventArgs> dataAvailableEvent = DataAvailable;
            if (dataAvailableEvent != null)
            {
                dataAvailableEvent(this, e);
            }
        }

        private void ConnectionConnectionClosed( object sender, EventArgs e )
        {
            EventHandler sessionClosedEvent = SessionClosed;
            if (sessionClosedEvent != null)
            {
                sessionClosedEvent(this, new EventArgs());
            }

        }
    }
}