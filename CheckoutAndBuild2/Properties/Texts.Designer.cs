﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace FG.CheckoutAndBuild2.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Texts {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Texts() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("FG.CheckoutAndBuild2.Properties.Texts", typeof(Texts).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Build Priority: In this order your solutions will be build. 
        ///All solutions with the same build priority will be build parallel.
        /// </summary>
        public static string BuildPrioTooltip {
            get {
                return ResourceManager.GetString("BuildPrioTooltip", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Copy Settings from workspace.
        /// </summary>
        public static string DefaultCopySettingsHeader {
            get {
                return ResourceManager.GetString("DefaultCopySettingsHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CheckoutAndBuild.
        /// </summary>
        public static string MainSectionTitle {
            get {
                return ResourceManager.GetString("MainSectionTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Main Options.
        /// </summary>
        public static string OptionsMain {
            get {
                return ResourceManager.GetString("OptionsMain", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Plugins / Extensions.
        /// </summary>
        public static string OptionsPlugins {
            get {
                return ResourceManager.GetString("OptionsPlugins", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TeamExplorer Sections.
        /// </summary>
        public static string OptionsTeamExplorerSections {
            get {
                return ResourceManager.GetString("OptionsTeamExplorerSections", resourceCulture);
            }
        }
    }
}