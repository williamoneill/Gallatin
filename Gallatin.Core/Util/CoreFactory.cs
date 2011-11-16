using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Gallatin.Core.Util
{
    /// <summary>
    /// Core object factory
    /// </summary>
    public static class CoreFactory
    {
        private static readonly CompositionContainer _container;
        private static readonly CompositionContainer _mockContainer;

        static CoreFactory()
        {
            // TODO: document this behavior and naming convention...
            const string AddinDirectory = ".\\addins";

            var aggregateCatalog = new AggregateCatalog();

            if (Directory.Exists(AddinDirectory))
            {
                var filterCatalog = new DirectoryCatalog(AddinDirectory, "*filter.dll");
                aggregateCatalog.Catalogs.Add(filterCatalog);
            }

            aggregateCatalog.Catalogs.Add( new AssemblyCatalog(Assembly.GetExecutingAssembly()) );
            var mainExportProvider = new CatalogExportProvider( aggregateCatalog );

            _mockContainer = new CompositionContainer();

            // See http://stackoverflow.com/questions/3828290/how-to-replace-an-exported-part-object-in-a-mef-container
            // See http://codebetter.com/glennblock/2009/05/14/customizing-container-behavior-part-2-of-n-defaults/
            _container = new CompositionContainer(_mockContainer, mainExportProvider);
            mainExportProvider.SourceProvider = _container;
        }

        /// <summary>
        /// When asking to compose type, use the delegate to create the instance
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal static void Register<T>( Func<T> creationDelegate )
        {
            Contract.Requires(creationDelegate!=null);

            _mockContainer.ComposeExportedValue( creationDelegate() );
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