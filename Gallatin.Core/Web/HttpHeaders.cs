using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpHeaders : IHttpHeaders
    {
        private readonly List<KeyValuePair<string, string>> _headers;

        public HttpHeaders( List<KeyValuePair<string, string>> headers )
        {
            Contract.Requires( headers != null );
            _headers = headers;
        }

        #region IHttpHeaders Members

        public IEnumerable<KeyValuePair<string, string>> AsEnumerable()
        {
            return _headers.AsEnumerable();
        }

        public int Count
        {
            get
            {
                return _headers.Count;
            }
        }

        public void Remove( string key )
        {
            KeyValuePair<string, string> header;
            if ( TryFindByKey( key, out header ) )
            {
                _headers.Remove( header );
            }
        }

        public string this[ string key ]
        {
            get
            {
                KeyValuePair<string, string> header;
                if ( TryFindByKey( key, out header ) )
                {
                    return header.Value;
                }

                return null;
            }
        }

        public void RenameKey( string oldKeyName, string newKeyName )
        {
            KeyValuePair<string, string> header;
            if ( TryFindByKey( oldKeyName, out header ) )
            {
                // HTTP spec: do not change header order in proxy
                int index = _headers.IndexOf( header );
                _headers.Remove( header );
                _headers.Insert( index, new KeyValuePair<string, string>( newKeyName, header.Value ) );
            }
        }

        public void RemoveKeyValue( string key, string value )
        {
            KeyValuePair<string, string> header;
            if ( TryFindByKey( key, out header ) )
            {
                // HTTP spec: do not change header order in proxy
                int index = _headers.IndexOf( header );

                // Check for the trailing ";" if there are multiple values for the key. E.g. "text/html;charset=utf-8"
                Regex regex = new Regex( string.Format( @"\s*{0}\s*(;?)", value ), RegexOptions.IgnoreCase );

                string newValue = regex.Replace( header.Value, "" );
                newValue = newValue.TrimEnd( ';' );

                // Remove the header. We will re-write it below. If the value ends up being empty then remove the 
                // header from the collection.
                _headers.Remove( header );

                // Still something left in the value? Add the key/value pair back, less the value we are removing.
                if ( !string.IsNullOrEmpty( newValue ) )
                {
                    _headers.Insert( index, new KeyValuePair<string, string>( key, newValue ) );
                }
            }
        }

        public void UpsertKeyValue( string key, string value )
        {
            KeyValuePair<string, string> header;
            if ( TryFindByKey( key, out header ) )
            {
                // HTTP spec: do not change header order in proxy
                int index = _headers.IndexOf( header );

                // Find dups and delete them all, including the one we found above.
                bool wasFound;
                do
                {
                    KeyValuePair<string, string> searchHeader;
                    wasFound = TryFindByKey( key, out searchHeader );
                    if ( wasFound )
                    {
                        _headers.Remove( searchHeader );
                    }
                }
                while ( wasFound );

                _headers.Insert( index, new KeyValuePair<string, string>( key, value ) );
            }
            else
            {
                _headers.Add( new KeyValuePair<string, string>( key, value ) );
            }
        }

        #endregion

        private bool TryFindByKey( string key, out KeyValuePair<string, string> header )
        {
            header = default( KeyValuePair<string, string> );

            if ( _headers != null )
            {
                header =
                    _headers.FirstOrDefault(
                        s => s.Key.Equals( key, StringComparison.InvariantCultureIgnoreCase ) );
            }

            return !header.Equals( default( KeyValuePair<string, string> ) );
        }
    }
}