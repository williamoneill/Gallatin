using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// This class contains the HTTP header values extracted from the raw network data
    /// </summary>
    internal class HttpHeaders : IHttpHeaders
    {
        private readonly List<KeyValuePair<string, string>> _headers;

        /// <summary>
        /// Gets an enumeration of the HTTP header values
        /// </summary>
        /// <returns>Reference to an enumeration of the HTTP header values</returns>
        public IEnumerable<KeyValuePair<string,string>> AsEnumerable()
        {
            return _headers.AsEnumerable();

        }

        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="headers">
        /// List of parsed header values
        /// </param>
        public HttpHeaders( List<KeyValuePair<string,string>> headers )
        {
            Contract.Requires(headers != null);
            _headers = headers;
        }

        /// <summary>
        /// Gets the number of header pairs in the collection
        /// </summary>
        public int Count
        {
            get
            {
                return _headers.Count;
            }
        }

        /// <summary>
        /// Removes the matching key/value pair from the collection that matches the supplied key.
        /// </summary>
        /// <remarks>If the key does not exist in the collection, the request is ignored.</remarks>
        /// <param name="key">Search key</param>
        public void Remove(string key)
        {
            KeyValuePair<string, string> header;
            if (TryFindByKey(key, out header))
            {
                _headers.Remove(header);
            }
        }

        /// <summary>
        /// Attempts to find a header key/value pair using the specified key.
        /// </summary>
        /// <param name="key">Key value</param>
        /// <param name="header">Matching key/value pair for the specified key</param>
        /// <returns><c>True</c> if a matching key/value pair exists in the collection.</returns>
        private bool TryFindByKey(string key, out KeyValuePair<string,string> header)
        {
            header = default( KeyValuePair<string, string> );

            if (_headers != null)
            {
                header =
                    _headers.FirstOrDefault(
                        s => s.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            }

            return !header.Equals(default(KeyValuePair<string, string>));
        }

        /// <summary>
        /// Gets the key/value pair for the specified key. They comparision is case-insensitive.
        /// </summary>
        /// <param name="key">Search key</param>
        /// <returns>The matching key/value pair or <c>null</c> if the key did not exist in the collection</returns>
        public string this[string key]
        {
            get
            {
                KeyValuePair<string, string> header;
                if (TryFindByKey(key, out header))
                {
                    return header.Value;
                }

                return null;
            }
        }

        /// <summary>
        /// Renames a key in the HTTP header collection
        /// </summary>
        /// <param name="oldKeyName">Old key name</param>
        /// <param name="newKeyName">New key name</param>
        public void RenameKey( string oldKeyName, string newKeyName )
        {
            KeyValuePair<string, string> header;
            if (TryFindByKey(oldKeyName, out header))
            {
                // HTTP spec: do not change header order in proxy
                var index = _headers.IndexOf( header );
                _headers.Remove( header );
                _headers.Insert( index, new KeyValuePair<string, string>(newKeyName, header.Value) );
            }
            
        }


        public void RemoveKeyValue(string key, string value)
        {
            KeyValuePair<string, string> header;
            if (TryFindByKey(key, out header))
            {
                // HTTP spec: do not change header order in proxy
                var index = _headers.IndexOf(header);

                // Check for the trailing ";" if there are multiple values for the key. E.g. "text/html;charset=utf-8"
                Regex regex = new Regex(string.Format(@"\s*{0}\s*(;?)", value), RegexOptions.IgnoreCase);

                var newValue = regex.Replace( header.Value, "" );

                // Remove the header. We will re-write it below. If the value ends up being empty then remove the 
                // header from the collection.
                _headers.Remove(header);

                // Still something left in the value? Add the key/value pair back, less the value we are removing.
                if (!string.IsNullOrEmpty(newValue))
                {
                    _headers.Insert(index, new KeyValuePair<string, string>(key, newValue));
                }
            }
        }

        public void UpsertKeyValue(string key, string value)
        {
            KeyValuePair<string, string> header;
            if (TryFindByKey(key, out header))
            {
                // HTTP spec: do not change header order in proxy
                var index = _headers.IndexOf(header);

                // Find dups and delete them all, including the one we found above.
                bool wasFound;
                do
                {
                    KeyValuePair<string, string> searchHeader;
                    wasFound = TryFindByKey( key, out searchHeader );
                    if(wasFound)
                    {
                        _headers.Remove( searchHeader );
                    }
                }
                while ( wasFound );

                _headers.Insert(index, new KeyValuePair<string, string>(key, value));
            }
            else
            {
                _headers.Add( new KeyValuePair<string, string>(key, value));
            }
            
        }

    }
}