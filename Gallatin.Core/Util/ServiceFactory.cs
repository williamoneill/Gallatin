using System;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Reflection;

namespace Gallatin.Core.Util
{
    /// <summary>
    /// Creates default instances of the requested type
    /// </summary>
    public static class ServiceFactory
    {
        private static CompositionContainer _container;

        static ServiceFactory()
        {
            AssemblyCatalog assemblyCatalog = new AssemblyCatalog( Assembly.GetExecutingAssembly() );

            _container = new CompositionContainer(assemblyCatalog);
        }

        /// <summary>
        /// Creates the default instance of the requested type
        /// </summary>
        /// <typeparam name="T">Requested type</typeparam>
        /// <returns>Reference to the default instance</returns>
        public static T Compose<T>()
        {
            return _container.GetExportedValue<T>();
        }

        /// <summary>
        /// Overrides the default registration. Useful for unit testing.
        /// </summary>
        /// <typeparam name="T">Type to override</typeparam>
        /// <param name="newObject">Method that creates a new exported object</param>
        public static void OverrideRegistration<T>(Func<object> newObject)
        {
            _container.ReleaseExport( new Export( typeof(T).ToString(), newObject ) );
        }
    }
}