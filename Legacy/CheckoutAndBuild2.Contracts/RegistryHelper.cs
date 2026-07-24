using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Permissions;
using Microsoft.Win32;

namespace CheckoutAndBuild2.Contracts
{
    public static class RegistryHelper
    {
     
        /// <summary>
        /// Wert Typsicher aus der Registry lesen
        /// </summary>
        /// <typeparam name="T">Der angefragte Typ</typeparam>
        /// <param name="registryHive">RegistryHive</param>
        /// <param name="basekey">Basiskey, z.b. Software</param>
        /// <param name="subkey"></param>

        /// <param name="registryView"></param>
        /// <returns></returns>
        public static T ReadValue<T>(RegistryHive registryHive, string basekey, string subkey, RegistryView registryView = RegistryView.Registry64)
        {
            using (
                var openSubKey = RegistryKey.OpenBaseKey(registryHive, registryView).OpenSubKey(basekey))
            {
                if (openSubKey != null)
                {
                    var regValue = openSubKey.GetValue(subkey, null);
                    if (regValue != null)
                        return (T)Convert.ChangeType(regValue, typeof(T), CultureInfo.InvariantCulture);

                    return default(T);
                }
            }
            return default(T);
        }

        /// <summary>
        /// Wert typsicher aus registry lesen
        /// </summary>
        public static T ReadValue<T>(RegistryHive hive, string path, string key, RegistryValueKind kind = RegistryValueKind.Unknown)
        {
            T result;
            TryReadValue(hive, path, key, kind, out result);
            return result;
        }

        /// <summary>
        /// Wert typsicher aus registry lesen
        /// </summary>
        public static T ReadValue<T>(RegistryKey registry, string key, RegistryValueKind kind = RegistryValueKind.Unknown)
        {
            T result;
            TryReadValue(registry, key, kind, out result);
            return result;
        }

        /// <summary>
        /// Wert typsicher aus registry lesen
        /// </summary>
        public static bool TryReadValue<T>(RegistryHive hive, string path, string key, RegistryValueKind kind, out T data)
        {
            data = default(T);

            using (RegistryKey baseKey = RegistryKey.OpenRemoteBaseKey(hive, String.Empty))
            {
                using (RegistryKey registryKey = baseKey.OpenSubKey(path, RegistryKeyPermissionCheck.ReadSubTree))
                {
                    if (registryKey != null)
                    {
                        return TryReadValue(registryKey, key, kind, out data);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Wert typsicher aus registry lesen
        /// </summary>
        public static bool TryReadValue<T>(RegistryKey registry, string key, RegistryValueKind kind, out T data)
        {
            data = default(T);
            using (registry)
            {
                // If the key was opened, try to retrieve the value.
                RegistryValueKind kindFound = registry.GetValueKind(key);
                if (kind == RegistryValueKind.Unknown || kindFound == kind)
                {
                    object regValue = registry.GetValue(key, null);
                    if (regValue != null)
                    {
                        data = (T)Convert.ChangeType(regValue, typeof(T), CultureInfo.InvariantCulture);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Wert typsicher in die registry schreiben
        /// </summary>        
        [PrincipalPermission(SecurityAction.Demand, Role = "Administrators")]
        public static bool WriteValue<T>(RegistryHive hive, string path, string key, RegistryValueKind kind, T value)
        {

            using (RegistryKey baseKey = RegistryKey.OpenRemoteBaseKey(hive, String.Empty))
            {
                using (RegistryKey registryKey = baseKey.OpenSubKey(path, true))
                {
                    if (registryKey != null)
                    {
                        return WriteValue(registryKey, key, kind, value);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Wert typsicher in die registry schreiben
        /// </summary>
        public static bool WriteValue<T>(RegistryKey registry, string key, RegistryValueKind kind, T value)
        {
            using (registry)
            {
                if (value == null)
                {
                    registry.DeleteValue(key);
                    return registry.GetValue(key) == null;
                }
                // If the key was opened, try to retrieve the value.

                registry.SetValue(key, value, kind);
                return ReadValue<T>(registry, key, kind).Equals(value);
            }
        }

        /// <summary>
        /// Registriert ein URL Protokoll
        /// </summary>
        public static bool RegisterUrlProtocol(string protocolName, string description, string applicationPath)
        {
            UnRegisterUrlProtocol(protocolName);

            try
            {
                // Neuer Schlüssel für das gewünschte URL Protokoll erstellen
                RegistryKey myKey = Registry.ClassesRoot.CreateSubKey(protocolName);
                if (myKey == null)
                    return false;
                // Protokoll zuweisen
                myKey.SetValue(null, description);
                myKey.SetValue("URL Protocol", String.Empty);

                // Shellwerte eintragen
                Registry.ClassesRoot.CreateSubKey(protocolName + "\\Shell");
                Registry.ClassesRoot.CreateSubKey(protocolName + "\\Shell\\open");
                myKey = Registry.ClassesRoot.CreateSubKey(protocolName + "\\Shell\\open\\command");
                if (myKey == null)
                    return false;
                // Anwendung festlegen, die das URL-Protokoll behandelt
                myKey.SetValue(null, "\"" + applicationPath + "\" %1");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Deregistriert ein URL Protokoll
        /// </summary>
        public static bool UnRegisterUrlProtocol(string protocolName)
        {
            try
            {
                if (Registry.ClassesRoot.OpenSubKey(protocolName) != null)
                    Registry.ClassesRoot.DeleteSubKeyTree(protocolName);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


      
        /// <summary>
        /// Liefert die lokal installierten SQL Server Instanzen
        /// </summary>
        /// <returns>Die lokalen SQL Server Instanzen</returns>
        public static IEnumerable<string> GetLocalInstalledInstances(string lockSwitch, string registryPath)
        {
            var localInstanceNames = new List<string>();
            var registryViews = new List<RegistryView> { RegistryView.Registry32, RegistryView.Registry64 };
            foreach (var registryView in registryViews)
            {
                using (var openLocalMachineSoftwareRegEntry = OpenLocalMachineSoftwareRegEntry(registryView, lockSwitch, registryPath))
                {
                    using (var openSubKey =
                            openLocalMachineSoftwareRegEntry.OpenSubKey(@"Microsoft\Microsoft SQL Server\Instance Names\SQL"))
                    {
                        if (openSubKey != null)
                        {
                            var valueNames = openSubKey.GetValueNames();
                            foreach (var instanceName in valueNames)
                            {
                                if (!localInstanceNames.Contains(instanceName))
                                    localInstanceNames.Add(instanceName);
                            }
                        }
                    }
                }
            }

            var installedInstances = new List<string>();

            foreach (var localInstanceName in localInstanceNames)
            {
                if (!localInstanceName.Contains(Environment.MachineName))
                    installedInstances.Add(Environment.MachineName + @"\" + localInstanceName);


            }
            return installedInstances;
        }

        /// <summary>
        /// Zugriff auf Localmachine/SOFTWARE in Abhängigkeit vom registryview / Registry Virtualisierung 32/64bit
        /// </summary>
        /// <returns></returns>
        public static RegistryKey OpenLocalMachineSoftwareRegEntry(RegistryView registryView, string lockSwitch, string registryPath)
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView).OpenSubKey(registryPath);
        }

    }
}