// Decompiled with JetBrains decompiler
// Type: Microsoft.Build.Construction.ConfigurationInSolution
// Assembly: Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 136A510C-236B-41B8-A3EF-1C5E435AD94B
// Assembly location: C:\Windows\Microsoft.NET\Framework\v4.0.30319\Microsoft.Build.dll

using System;
using System.Globalization;

namespace Microsoft.Build.Construction
{
    internal class ConfigurationInSolution
    {
        internal const char configurationPlatformSeparator = '|';
        private string configurationName;
        private string platformName;
        private string fullName;

        internal string ConfigurationName
        {
            get
            {
                return this.configurationName;
            }
        }

        internal string PlatformName
        {
            get
            {
                return this.platformName;
            }
        }

        internal string FullName
        {
            get
            {
                return this.fullName;
            }
        }

        internal ConfigurationInSolution(string configurationName, string platformName)
        {
            this.configurationName = configurationName;
            this.platformName = platformName;
            if (platformName != null && platformName.Length > 0)
            {
                CultureInfo invariantCulture = CultureInfo.InvariantCulture;
                string format = "{0}{1}{2}";
                object[] objArray = new object[3];
                int index1 = 0;
                string str1 = configurationName;
                objArray[index1] = (object)str1;
                int index2 = 1;
                // ISSUE: variable of a boxed type
                var local = (ValueType)'|';
                objArray[index2] = (object)local;
                int index3 = 2;
                string str2 = platformName;
                objArray[index3] = (object)str2;
                this.fullName = string.Format((IFormatProvider)invariantCulture, format, objArray);
            }
            else
                this.fullName = configurationName;
        }
    }
}
