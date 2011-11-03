using System;
using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Util
{
    /// <summary>
    /// Creates instances for public interfaces
    /// </summary>
    public static class CoreFactory
    {
        private static readonly Dictionary<Type, Func<object>> _dictionary = new Dictionary<Type, Func<object>>();

        static CoreFactory()
        {
            //Register<IProxyService, ProxyService>();
            //Register( SettingsMapper.Load );
            //Register<INetworkFacadeFactory, NetworkFacadeFactory>();
            //Register<IProxySession,ProxySession>();
        }

        /// <summary>
        /// Registers the concrete type to be created for the specified interface
        /// </summary>
        /// <typeparam name="T">Interface type</typeparam>
        /// <typeparam name="U">Concerete type</typeparam>
        public static void Register<T, U>()
            where T : class
            where U : class, T, new()
        {
            if ( _dictionary.ContainsKey( typeof (T) ) )
            {
                _dictionary.Remove( typeof (T) );
            }

            _dictionary.Add( typeof (T), () => Activator.CreateInstance( typeof (U) ) );
        }

        /// <summary>
        /// Registers a delegate that will create the interface type
        /// </summary>
        /// <typeparam name="T">Interface type</typeparam>
        /// <param name="creator">Delegate used to create the concrete instance</param>
        public static void Register<T>( Func<T> creator )
            where T : class
        {
            if ( _dictionary.ContainsKey( typeof (T) ) )
            {
                _dictionary.Remove( typeof (T) );
            }

            _dictionary.Add( typeof (T), creator );
        }

        /// <summary>
        /// Creates a concrete instance of the specified interface
        /// </summary>
        /// <typeparam name="T">Interface type</typeparam>
        /// <returns>Instance of the specified interface type</returns>
        public static T Create<T>() where T : class
        {
            return _dictionary[typeof (T)]() as T;
        }
    }
}