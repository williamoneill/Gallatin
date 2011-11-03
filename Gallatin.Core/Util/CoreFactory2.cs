using System.ComponentModel.Composition.Hosting;
using System.Reflection;

namespace Gallatin.Core.Util
{
    /// <summary>
    /// Core object factory
    /// </summary>
    public static class CoreFactory2
    {
        private static CompositionContainer _container;

        static CoreFactory2()
        {
            var catalog = new AssemblyCatalog( Assembly.GetExecutingAssembly() );
            _container = new CompositionContainer( catalog );
        }

        /// <summary>
        /// Creates an instance of the specified type or interface
        /// </summary>
        /// <typeparam name="T">Target type</typeparam>
        /// <returns>Instance of specified type</returns>
        public static T Compose<T>()
        {
            return _container.GetExportedValue<T>();
        }
    }
}