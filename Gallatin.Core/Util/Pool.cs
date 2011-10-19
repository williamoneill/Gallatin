using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Util
{
    public interface IPooledObject
    {
        void Reset();
    }

    internal class Pool<T> where T: class, IPooledObject, new()
    {
        private Stack<T> _pool;
        private List<T> _outOfPoolList; 
        private object _mutex = new object();
        private bool _isInitialized;
        private int _poolSize;

        public void Init(int size)
        {
            Contract.Requires(size > 0);
            Contract.Ensures(_poolSize == size);
            Contract.Ensures(_isInitialized);

            lock(_mutex)
            {
                if (_isInitialized)
                    throw new InvalidOperationException( "Instance has already been initialized" );

                _pool = new Stack<T>(size);
                _poolSize = size;

                for (int i = 0; i < size; i++)
                {
                    _pool.Push(new T());
                }

                _outOfPoolList = new List<T>(size);

                _isInitialized = true;
            }
        }

        public IEnumerable<T> OutlierCollection
        {
            get
            {
                return _outOfPoolList.AsEnumerable();
            }
        }

        public T Get()
        {
            lock(_mutex)
            {
                var item = _pool.Pop();
                _outOfPoolList.Add(item);
                return item;
            }
        }



        public void Put(T item)
        {
            Contract.Requires(item != null);

            lock( _mutex )
            {
                if(_pool.Count <= _poolSize)
                {
                    item.Reset();
                    _outOfPoolList.Remove( item );
                    _pool.Push(item);
                }
                else
                {
                    throw new InvalidOperationException( "Pool size exceeded" );
                }
            }
        }
    }
}
