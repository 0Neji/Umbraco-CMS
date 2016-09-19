using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Umbraco.Core.Logging;

namespace Umbraco.Core.Manifest
{
    internal class ManifestWatcher : DisposableObject
    {
        private static readonly object Locker = new object();
        private static volatile bool _isRestarting;

        private readonly ILogger _logger;
        private readonly List<FileSystemWatcher> _fws = new List<FileSystemWatcher>();

        public ManifestWatcher(ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger;
        }

        public void Start(params string[] packageFolders)
        {
            foreach (var packageFolder in packageFolders.Where(IsWatchable))
            {
                // for some reason *.manifest doesn't work
                var fsw = new FileSystemWatcher(packageFolder, "*package.*")
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite 
                };

                _fws.Add(fsw);

                fsw.Changed += FswChanged;
                fsw.EnableRaisingEvents = true;
            }
        }

        private static bool IsWatchable(string folder)
        {
            return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "package.manifest"));
        }

        private void FswChanged(object sender, FileSystemEventArgs e)
        {
            if (e.Name.InvariantContains("package.manifest") == false) return;

            // ensure the app is not restarted multiple times for multiple
            // savings during the same app domain execution - restart once
            lock (Locker)
            {
                if (_isRestarting) return;

                _isRestarting = true;
                _logger.Info<ManifestWatcher>("manifest has changed, app pool is restarting (" + e.FullPath + ")");
                HttpRuntime.UnloadAppDomain();
                Dispose(); // uh? if the app restarts then this should be disposed anyways?
            }
        }

        protected override void DisposeResources()
        {
            foreach (var fw in _fws)
                fw.Dispose();
        }
    }
}