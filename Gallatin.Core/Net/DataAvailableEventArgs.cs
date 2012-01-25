using System;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// 
    /// </summary>
    public  class DataAvailableEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public DataAvailableEventArgs(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            Data = data;
        }

        /// <summary>
        /// 
        /// </summary>
        public byte[] Data { get; private set; }
    }
}