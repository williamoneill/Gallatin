using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Gallatin.Core.Web
{
    public class HttpHeaders
    {
        private readonly List<KeyValuePair<string, string>> _headers;

        public IEnumerable<KeyValuePair<string,string>> AsEnumerable()
        {
            return _headers.AsEnumerable();
        }

        public HttpHeaders( List<KeyValuePair<string,string>> headers )
        {
            Contract.Requires(headers != null);

            _headers = headers;
        }

        public int Count
        {
            get
            {
                return _headers.Count;
            }
        }

        public void Remove(string key)
        {
            KeyValuePair<string, string> header;
            if (TryFindByKey(key, out header))
            {
                _headers.Remove( header );
            }
        }

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

    }
}