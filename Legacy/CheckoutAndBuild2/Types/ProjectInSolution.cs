// Decompiled with JetBrains decompiler
// Type: Microsoft.Build.Construction.ProjectInSolution
// Assembly: Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 136A510C-236B-41B8-A3EF-1C5E435AD94B
// Assembly location: C:\Windows\Microsoft.NET\Framework\v4.0.30319\Microsoft.Build.dll

using Microsoft.Build.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace Microsoft.Build.Construction
{
    public sealed class ProjectInSolution
    {
        private static readonly char[] charsToCleanse = new char[8]
        {
      '%',
      '$',
      '@',
      ';',
      '.',
      '(',
      ')',
      '\''
        };
        internal static readonly string[] projectNamesToDisambiguate;
        private const char cleanCharacter = '_';
        private SolutionProjectType projectType;
        private string projectName;
        private string relativePath;
        private string projectGuid;
        private ArrayList dependencies;
        private ArrayList projectReferences;
        private string parentProjectGuid;
        private string uniqueProjectName;
        private Hashtable aspNetConfigurations;
        private SolutionParser parentSolution;
        private int dependencyLevel;
        private bool isStaticLibrary;
        private bool childReferencesGathered;
        private string targetFrameworkMoniker;
        private Dictionary<string, ProjectConfigurationInSolution> projectConfigurations;
        private bool checkedIfCanBeMSBuildProjectFile;
        private bool canBeMSBuildProjectFile;
        private string canBeMSBuildProjectFileErrorMessage;
        internal const int DependencyLevelUnknown = -1;
        internal const int DependencyLevelBeingDetermined = -2;

        internal SolutionProjectType ProjectType
        {
            get
            {
                return this.projectType;
            }
            set
            {
                this.projectType = value;
            }
        }

        internal string ProjectName
        {
            get
            {
                return this.projectName;
            }
            set
            {
                this.projectName = value;
            }
        }

        internal string RelativePath
        {
            get
            {
                return this.relativePath;
            }
            set
            {
                this.relativePath = value;
            }
        }

        internal string AbsolutePath
        {
            get
            {
                return Path.Combine(this.ParentSolution.SolutionFileDirectory, this.RelativePath);
            }
        }

        internal string Extension
        {
            get
            {
                return Path.GetExtension(this.relativePath);
            }
        }

        internal string ProjectGuid
        {
            get
            {
                return this.projectGuid;
            }
            set
            {
                this.projectGuid = value;
            }
        }

        internal ArrayList Dependencies
        {
            get
            {
                return this.dependencies;
            }
        }

        internal ArrayList ProjectReferences
        {
            get
            {
                return this.projectReferences;
            }
        }

        internal string ParentProjectGuid
        {
            get
            {
                return this.parentProjectGuid;
            }
            set
            {
                this.parentProjectGuid = value;
            }
        }

        internal SolutionParser ParentSolution
        {
            get
            {
                return this.parentSolution;
            }
            set
            {
                this.parentSolution = value;
            }
        }

        internal Hashtable AspNetConfigurations
        {
            get
            {
                return this.aspNetConfigurations;
            }
            set
            {
                this.aspNetConfigurations = value;
            }
        }

        internal Dictionary<string, ProjectConfigurationInSolution> ProjectConfigurations
        {
            get
            {
                return this.projectConfigurations;
            }
        }

        internal int DependencyLevel
        {
            get
            {
                return this.dependencyLevel;
            }
            set
            {
                this.dependencyLevel = value;
            }
        }

        internal bool IsStaticLibrary
        {
            get
            {
                return this.isStaticLibrary;
            }
            set
            {
                this.isStaticLibrary = value;
            }
        }

        internal bool ChildReferencesGathered
        {
            get
            {
                return this.childReferencesGathered;
            }
            set
            {
                this.childReferencesGathered = value;
            }
        }

        internal string TargetFrameworkMoniker
        {
            get
            {
                return this.targetFrameworkMoniker;
            }
            set
            {
                this.targetFrameworkMoniker = value;
            }
        }

        static ProjectInSolution()
        {
            string[] strArray = new string[4];
            int index1 = 0;
            string str1 = "Build";
            strArray[index1] = str1;
            int index2 = 1;
            string str2 = "Rebuild";
            strArray[index2] = str2;
            int index3 = 2;
            string str3 = "Clean";
            strArray[index3] = str3;
            int index4 = 3;
            string str4 = "Publish";
            strArray[index4] = str4;
            ProjectInSolution.projectNamesToDisambiguate = strArray;
        }

        internal ProjectInSolution(SolutionParser solution)
        {
            this.projectType = SolutionProjectType.Unknown;
            this.projectName = (string)null;
            this.relativePath = (string)null;
            this.projectGuid = (string)null;
            this.dependencies = new ArrayList();
            this.projectReferences = new ArrayList();
            this.parentProjectGuid = (string)null;
            this.uniqueProjectName = (string)null;
            this.parentSolution = solution;
            this.dependencyLevel = -1;
            this.isStaticLibrary = false;
            this.childReferencesGathered = false;
            this.targetFrameworkMoniker = ".NETFramework,Version=v3.5";
            this.aspNetConfigurations = new Hashtable((IEqualityComparer)StringComparer.OrdinalIgnoreCase);
            this.projectConfigurations = new Dictionary<string, ProjectConfigurationInSolution>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
        }

        internal bool CanBeMSBuildProjectFile(out string errorMessage)
        {
            if (this.checkedIfCanBeMSBuildProjectFile)
            {
                errorMessage = this.canBeMSBuildProjectFileErrorMessage;
                return this.canBeMSBuildProjectFile;
            }
            this.checkedIfCanBeMSBuildProjectFile = true;
            this.canBeMSBuildProjectFile = false;
            errorMessage = (string)null;
            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                string absolutePath = this.AbsolutePath;
                xmlDocument.Load(absolutePath);
                XmlElement xmlElement = (XmlElement)null;
                foreach (XmlNode xmlNode in xmlDocument.ChildNodes)
                {
                    if (xmlNode.NodeType == XmlNodeType.Element)
                    {
                        xmlElement = (XmlElement)xmlNode;
                        break;
                    }
                }
                if (xmlElement != null)
                {
                    if (xmlElement.LocalName == "Project")
                    {
                        if (string.Compare(xmlElement.NamespaceURI, "http://schemas.microsoft.com/developer/msbuild/2003", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            this.canBeMSBuildProjectFile = true;
                            return this.canBeMSBuildProjectFile;
                        }
                    }
                }
            }
            catch (SecurityException ex)
            {
                this.canBeMSBuildProjectFileErrorMessage = ex.Message;
            }
            catch (NotSupportedException ex)
            {
                this.canBeMSBuildProjectFileErrorMessage = ex.Message;
            }
            catch (IOException ex)
            {
                this.canBeMSBuildProjectFileErrorMessage = ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                this.canBeMSBuildProjectFileErrorMessage = ex.Message;
            }
            catch (XmlException ex)
            {
            }
            errorMessage = this.canBeMSBuildProjectFileErrorMessage;
            return this.canBeMSBuildProjectFile;
        }

        internal string GetUniqueProjectName()
        {
            if (this.uniqueProjectName == null)
            {
                if (this.ProjectType == SolutionProjectType.WebProject || this.ProjectType == SolutionProjectType.EtpSubProject)
                {
                    this.uniqueProjectName = ProjectInSolution.CleanseProjectName(this.ProjectName);
                }
                else
                {
                    string str = string.Empty;
                    if (this.ParentProjectGuid != null)
                    {
                        ProjectInSolution projectInSolution = (ProjectInSolution)this.ParentSolution.ProjectsByGuid[(object)this.ParentProjectGuid];
                        str = projectInSolution.GetUniqueProjectName() + "\\";
                    }
                    this.uniqueProjectName = ProjectInSolution.CleanseProjectName(str + this.ProjectName);
                }
            }
            return this.uniqueProjectName;
        }

        internal void UpdateUniqueProjectName(string newUniqueName)
        {
            this.uniqueProjectName = newUniqueName;
        }

        private static string CleanseProjectName(string projectName)
        {
            if (projectName.IndexOfAny(ProjectInSolution.charsToCleanse) == -1)
                return projectName;
            StringBuilder stringBuilder = new StringBuilder(projectName);
            foreach (char oldChar in ProjectInSolution.charsToCleanse)
                stringBuilder.Replace(oldChar, '_');
            return stringBuilder.ToString();
        }

        internal static string DisambiguateProjectTargetName(string uniqueProjectName)
        {
            foreach (string strB in ProjectInSolution.projectNamesToDisambiguate)
            {
                if (string.Compare(uniqueProjectName, strB, StringComparison.OrdinalIgnoreCase) == 0)
                    return "Solution:" + uniqueProjectName;
            }
            return uniqueProjectName;
        }
    }
}
