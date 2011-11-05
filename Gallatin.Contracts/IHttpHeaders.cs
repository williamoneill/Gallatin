using System.Collections.Generic;

namespace Gallatin.Contracts
{
    /// <summary>
    /// Interface for the HTTP headers colleciton
    /// </summary>
    public interface IHttpHeaders
    {
        /// <summary>
        /// Gets an enumeration of the HTTP header values
        /// </summary>
        /// <returns>Reference to an enumeration of the HTTP header values</returns>
        IEnumerable<KeyValuePair<string,string>> AsEnumerable();

        /// <summary>
        /// Gets the number of header pairs in the collection
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Removes the matching key/value pair from the collection that matches the supplied key.
        /// </summary>
        /// <remarks>If the key does not exist in the collection, the request is ignored.</remarks>
        /// <param name="key">Search key</param>
        void Remove(string key);

        /// <summary>
        /// Gets the key/value pair for the specified key. They comparision is case-insensitive.
        /// </summary>
        /// <param name="key">Search key</param>
        /// <returns>The matching key/value pair or <c>null</c> if the key did not exist in the collection</returns>
        string this[ string key ] { get; }

        /// <summary>
        /// Renames a key in the HTTP header collection
        /// </summary>
        /// <param name="oldKeyName">Old key name</param>
        /// <param name="newKeyName">New key name</param>
        void RenameKey( string oldKeyName, string newKeyName );
    }
}