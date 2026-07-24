using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using EnvDTE80;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VsPackageService;
using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.ExtensionsExplorer;

namespace FG.CheckoutAndBuild2.VisualStudio
{
    public static class ExtensionUpdater
    {
        private static IServiceProvider serviceProvider { get { return CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>(); } }

        public static bool CheckForUpdate(IInstalledExtension extension, out IInstallableExtension update)
        {
            var _repository = serviceProvider.Get<IVsExtensionRepository>();
            // Find the vsix on the vs gallery
            // IMPORTANT: The .AsEnumerble() call is REQUIRED. Don't remove it or the update service won't work.
            GalleryEntry entry = _repository.CreateQuery<GalleryEntry>(false, true)
                                            .Where(e => e.VsixID == extension.Header.Identifier)
                                            .AsEnumerable()
                                            .FirstOrDefault();

            // If we're running an older version then update
            if (entry != null && entry.NonNullVsixVersion > extension.Header.Version)
            {
                update = _repository.Download(entry);
                return UpdateCanInstalledToCurrentProduct(update);
            }
            return CheckForUpdateWithReflection(extension, out update) && UpdateCanInstalledToCurrentProduct(update);
        }

        private static bool UpdateCanInstalledToCurrentProduct(IInstallableExtension extension)
        {
            var dte2 = serviceProvider.Get<DTE2>();
            var vsVersion = Version.Parse(dte2.Version);
            IExtensionReference r;
            return extension.References.Any(t => vsVersion >= t.VersionRange.Minimum && vsVersion <= t.VersionRange.Maximum);
        }

        private static bool CheckForUpdateWithReflection(IInstalledExtension extension, out IInstallableExtension update)
        {
            var _repository = serviceProvider.Get<IVsExtensionRepository>();
            update = null;
            var extensionRepositoryService = serviceProvider.Get<IVsExtensionRepository>();
            var methodInfo = extensionRepositoryService.GetType().GetMethod("GetRepositories");
            if (methodInfo != null)
            {
                IEnumerable<IInstalledExtension> extensionsToCheck = new List<IInstalledExtension> { extension };
                var repositories = methodInfo.Invoke(extensionRepositoryService, new object[0]) as IEnumerable<object>;
                if (repositories != null)
                {
                    var onlineExtension = ((from repository in repositories.ToList()
                                            let getUpdateMethod = repository.GetType().GetMethod("GetUpdates")
                                            where getUpdateMethod != null
                                            let repo = repository
                                            select Check.TryCatch<IEnumerable<object>, Exception>(() => getUpdateMethod.Invoke(repo, new object[] { extensionsToCheck }) as IEnumerable<object>)
                             into possibleUpdates
                                            where possibleUpdates != null
                                            select possibleUpdates.FirstOrDefault())).ToList().OfType<IVsExtension>().FirstOrDefault();
                    {
                        if (onlineExtension != null)
                        {
                            bool isAllreadyInstalled = onlineExtension.GetType().GetProperty("UpdateIsInstalled").GetValue(onlineExtension) as bool? ?? false;
                            if (!isAllreadyInstalled)
                            {
                                var updateEntry = onlineExtension.GetType().GetProperty("UpdateEntry").GetValue(onlineExtension) as IRepositoryEntry;
                                if (updateEntry != null)
                                    update = _repository.Download(updateEntry);
                                return true;
                            }
                        }
                    }

                }
            }
            return false;
        }

        public static bool CheckForUpdate(Uri galleryUri, IInstalledExtension extension)
        {
            //ProvideExtensionRepositoryAttribute gallery = typeof(CheckoutAndBuild2Package).GetAttributes<ProvideExtensionRepositoryAttribute>(false).FirstOrDefault();
            VsIdeServiceClient c = new VsIdeServiceClient(new WSHttpBinding(SecurityMode.None), new EndpointAddress(galleryUri));
            string[] list = c.GetCurrentVersionsForVsixList(new[] { GuidList.guidCheckoutAndBuild2PkgString }, null);
            string res = list.FirstOrDefault();
            Version onlineVersion;
            if (res != null && Version.TryParse(res, out onlineVersion))
            {
                if (onlineVersion > extension.Header.Version)
                    return true;
            }
            return false;
        }

        public static void UpdateExtension(IInstalledExtension extension, IInstallableExtension installableExtension)
        {
            var extensionManager = serviceProvider.Get<IVsExtensionManager>();
            extensionManager.Disable(extension);
            extensionManager.Uninstall(extension);
            extensionManager.InstallAsync(installableExtension, false);
        }

    }
}