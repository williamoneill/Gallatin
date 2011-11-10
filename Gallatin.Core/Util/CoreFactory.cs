using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Reflection;

namespace Gallatin.Core.Util
{
    /// <summary>
    /// Core object factory
    /// </summary>
    public static class CoreFactory
    {
        private static CompositionContainer _container;

        static CoreFactory()
        {
            const string AddinDirectory = ".\\addins";

            var aggregateCatalog = new AggregateCatalog();

            if (Directory.Exists(AddinDirectory))
            {
                var filterCatalog = new DirectoryCatalog(AddinDirectory, "*filter.dll");
                aggregateCatalog.Catalogs.Add(filterCatalog);
            }

            var catalog = new AssemblyCatalog(Assembly.GetExecutingAssembly());
            aggregateCatalog.Catalogs.Add( catalog );

            _container = new CompositionContainer(aggregateCatalog);
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