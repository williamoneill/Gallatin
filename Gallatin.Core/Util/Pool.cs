using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using Gallatin.Core.Service;

namespace Gallatin.Core.Util
{
    internal interface IPool<T>
        where T : class, IPooledObject
    {
        IEnumerable<T> ReleasedObjectList { get; }
        void Init( int size, Func<T> creationDelegate );
        T Get();
        void Put( T item );
    }

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(IPool<>))]
    internal class Pool<T> : IPool<T>
        where T : class, IPooledObject
    {
        private readonly object _mutex = new object();
        private bool _isInitialized;
        private List<T> _outOfPoolList;
        private Queue<T> _pool;
        private int _poolSize;

        public IEnumerable<T> ReleasedObjectList
        {
            get
            {
                return _outOfPoolList.AsEnumerable();
            }
        }

        public void Init( int size, Func<T> creationDelegate )
        {
            Contract.Requires( size > 0 );
            Contract.Requires(creationDelegate != null);
            Contract.Ensures( _poolSize == size );
            Contract.Ensures( _isInitialized );
            Contract.Ensures(_pool!=null);
            Contract.Ensures(_poolSize == size);
            Contract.Ensures(_pool.Count == size);
            Contract.Ensures(_isInitialized);

            lock ( _mutex )
            {
                if ( _isInitialized )
                {
                    throw new InvalidOperationException( "Instance has already been initialized" );
                }

                _pool = new Queue<T>( size );
                _poolSize = size;

                for ( int i = 0; i < size; i++ )
                {
                    _pool.Enqueue( creationDelegate() );
                }

                _outOfPoolList = new List<T>( size );

                _isInitialized = true;
            }
        }

        public T Get()
        {
            lock ( _mutex )
            {
                if (_pool.Count == 0)
                    throw new InvalidOperationException( "Pool is empty" );

                T item = _pool.Dequeue();
                _outOfPoolList.Add( item );
                return item;
            }
        }


        public void Put( T item )
        {
            Contract.Requires( item != null );

            lock ( _mutex )
            {
                if ( _pool.Count <= _poolSize )
                {
                    item.Reset();
                    _outOfPoolList.Remove( item );
                    _pool.Enqueue( item );
                }
                else
                {
                    throw new InvalidOperationException( "Pool size exceeded" );
                }
            }
        }
    }
}