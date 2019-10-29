using Microsoft.Build.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Build.Construction
{
    internal class SolutionParser
    {
        private static readonly Regex crackProjectLine = new Regex("^Project\\(\"(?<PROJECTTYPEGUID>.*)\"\\)\\s*=\\s*\"(?<PROJECTNAME>.*)\"\\s*,\\s*\"(?<RELATIVEPATH>.*)\"\\s*,\\s*\"(?<PROJECTGUID>.*)\"$");
        private static readonly Regex crackPropertyLine = new Regex("^(?<PROPERTYNAME>[^=]*)\\s*=\\s*(?<PROPERTYVALUE>.*)$");
        internal const int slnFileMinUpgradableVersion = 7;
        internal const int slnFileMinVersion = 9;
        internal const int slnFileMaxVersion = 12;
        private const string vbProjectGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        private const string csProjectGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private const string vjProjectGuid = "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}";
        private const string vcProjectGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        private const string fsProjectGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";
        private const string dbProjectGuid = "{C8D11400-126E-41CD-887F-60BD40844F9E}";
        private const string wdProjectGuid = "{2CFEAB61-6A3B-4EB8-B523-560B4BEEF521}";
        private const string webProjectGuid = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        private const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private int slnFileActualVersion;
        private string solutionFile;
        private string solutionFileDirectory;
        private bool solutionContainsWebProjects;
        private bool solutionContainsWebDeploymentProjects;
        private bool parsingForConversionOnly;
        private Hashtable projects;
        private ArrayList projectsInOrder;
        private List<ConfigurationInSolution> solutionConfigurations;
        private string defaultConfigurationName;
        private string defaultPlatformName;
        private ArrayList solutionParserWarnings;
        private ArrayList solutionParserComments;
        private ArrayList solutionParserErrorCodes;
        private StreamReader reader;
        private int currentLineNumber;

        internal ArrayList SolutionParserWarnings
        {
            get
            {
                return this.solutionParserWarnings;
            }
        }

        internal ArrayList SolutionParserComments
        {
            get
            {
                return this.solutionParserComments;
            }
        }

        internal ArrayList SolutionParserErrorCodes
        {
            get
            {
                return this.solutionParserErrorCodes;
            }
        }

        internal int Version
        {
            get
            {
                return this.slnFileActualVersion;
            }
        }

        internal bool ContainsWebProjects
        {
            get
            {
                return this.solutionContainsWebProjects;
            }
        }

        internal bool ContainsWebDeploymentProjects
        {
            get
            {
                return this.solutionContainsWebDeploymentProjects;
            }
        }

        internal ArrayList ProjectsInOrder
        {
            get
            {
                return this.projectsInOrder;
            }
            set
            {
                this.projectsInOrder = value;
            }
        }

        internal Hashtable ProjectsByGuid
        {
            get
            {
                return this.projects;
            }
            set
            {
                this.projects = value;
            }
        }

        internal string SolutionFile
        {
            get
            {
                return this.solutionFile;
            }
            set
            {
                this.solutionFile = value;
            }
        }

        internal string SolutionFileDirectory
        {
            get
            {
                return this.solutionFileDirectory;
            }
            set
            {
                this.solutionFileDirectory = value;
            }
        }

        internal StreamReader SolutionReader
        {
            get
            {
                return this.reader;
            }
            set
            {
                this.reader = value;
            }
        }

        internal ProjectInSolution[] Projects
        {
            get
            {
                return (ProjectInSolution[])this.projectsInOrder.ToArray(typeof(ProjectInSolution));
            }
        }

        internal List<ConfigurationInSolution> SolutionConfigurations
        {
            get
            {
                return this.solutionConfigurations;
            }
        }

        internal SolutionParser()
        {
            this.solutionParserWarnings = new ArrayList();
            this.solutionParserErrorCodes = new ArrayList();
            this.solutionParserComments = new ArrayList();
        }

        //internal static int GetSolutionFileMajorVersion(string solutionFile)
        //{
        //    FileStream fileStream = (FileStream)null;
        //    StreamReader streamReader = (StreamReader)null;
        //    int num1 = 0;
        //    bool flag = false;
        //    try
        //    {
        //        fileStream = File.OpenRead(solutionFile);
        //        streamReader = new StreamReader((Stream)fileStream, Encoding.Default);
        //        for (int index1 = 0; index1 < 2; ++index1)
        //        {
        //            string str = streamReader.ReadLine();
        //            if (str != null)
        //            {
        //                if (str.Trim().StartsWith("Microsoft Visual Studio Solution File, Format Version ", StringComparison.Ordinal))
        //                {
        //                    string input = str.Substring("Microsoft Visual Studio Solution File, Format Version ".Length);
        //                    System.Version version = (System.Version)null;
        //                    // ISSUE: explicit reference operation
        //                    // ISSUE: variable of a reference type
        //                    System.Version & result = @version;
        //                    if (!System.Version.TryParse(input, result))
        //                    {
        //                        int num2 = 0;
        //                        string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
        //                        BuildEventFileInfo projectFile = new BuildEventFileInfo(solutionFile);
        //                        string resourceName = "SolutionParseVersionMismatchError";
        //                        object[] objArray = new object[2];
        //                        int index2 = 0;
        //                        // ISSUE: variable of a boxed type
        //                        __Boxed<int> local1 = (ValueType)7;
        //                        objArray[index2] = (object)local1;
        //                        int index3 = 1;
        //                        // ISSUE: variable of a boxed type
        //                        __Boxed<int> local2 = (ValueType)12;
        //                        objArray[index3] = (object)local2;
        //                        ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(num2 != 0, errorSubCategoryResourceName, projectFile, resourceName, objArray);
        //                    }
        //                    num1 = version.Major;
        //                    int num3 = num1 >= 7 ? 1 : 0;
        //                    string errorSubCategoryResourceName1 = "SubCategoryForSolutionParsingErrors";
        //                    BuildEventFileInfo projectFile1 = new BuildEventFileInfo(solutionFile);
        //                    string resourceName1 = "SolutionParseVersionMismatchError";
        //                    object[] objArray1 = new object[2];
        //                    int index4 = 0;
        //                    // ISSUE: variable of a boxed type
        //                    __Boxed<int> local3 = (ValueType)7;
        //                    objArray1[index4] = (object)local3;
        //                    int index5 = 1;
        //                    // ISSUE: variable of a boxed type
        //                    __Boxed<int> local4 = (ValueType)12;
        //                    objArray1[index5] = (object)local4;
        //                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(num3 != 0, errorSubCategoryResourceName1, projectFile1, resourceName1, objArray1);
        //                    flag = true;
        //                    break;
        //                }
        //            }
        //            else
        //                break;
        //        }
        //    }
        //    finally
        //    {
        //        if (fileStream != null)
        //            fileStream.Close();
        //        if (streamReader != null)
        //            streamReader.Close();
        //    }
        //    if (flag)
        //        return num1;
        //    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo(solutionFile), "SolutionParseNoHeaderError", new object[0]);
        //    return 0;
        //}

        private string ReadLine()
        {
            string str = this.reader.ReadLine();
            this.currentLineNumber = this.currentLineNumber + 1;
            if (str != null)
                str = str.Trim();
            return str;
        }

        internal void ParseSolutionFileForConversion()
        {
            this.parsingForConversionOnly = true;
            this.ParseSolutionFile();
        }

        internal void ParseSolutionFile()
        {
            FileStream fileStream = (FileStream)null;
            this.reader = (StreamReader)null;
            try
            {
                fileStream = File.OpenRead(this.solutionFile);
                this.solutionFileDirectory = Path.GetDirectoryName(this.solutionFile);
                this.reader = new StreamReader((Stream)fileStream, Encoding.Default);
                this.ParseSolution();
            }
            catch (Exception ex)
            {
                BuildEventFileInfo projectFile = new BuildEventFileInfo(this.solutionFile);
                string resourceName = "InvalidProjectFile";
                object[] objArray = new object[1];
                int index = 0;
                string message = ex.Message;
                objArray[index] = (object)message;
                throw;
            }
            finally
            {
                if (fileStream != null)
                    fileStream.Close();
                if (this.reader != null)
                    this.reader.Close();
            }
        }

        internal void ParseSolution()
        {
            this.projects = new Hashtable((IEqualityComparer)StringComparer.OrdinalIgnoreCase);
            this.projectsInOrder = new ArrayList();
            this.solutionContainsWebProjects = false;
            this.slnFileActualVersion = 0;
            this.currentLineNumber = 0;
            this.solutionConfigurations = new List<ConfigurationInSolution>();
            this.defaultConfigurationName = (string)null;
            this.defaultPlatformName = (string)null;
            Hashtable rawProjectConfigurationsEntries = (Hashtable)null;
            this.ParseFileHeader();
            string firstLine;
            while ((firstLine = this.ReadLine()) != null)
            {
                if (firstLine.StartsWith("Project(", StringComparison.Ordinal))
                    this.ParseProject(firstLine);
                else if (firstLine.StartsWith("GlobalSection(NestedProjects)", StringComparison.Ordinal))
                    this.ParseNestedProjects();
                else if (firstLine.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.Ordinal))
                    this.ParseSolutionConfigurations();
                else if (firstLine.StartsWith("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.Ordinal))
                    rawProjectConfigurationsEntries = this.ParseProjectConfigurations();
            }
            if (rawProjectConfigurationsEntries != null)
                this.ProcessProjectConfigurationSection(rawProjectConfigurationsEntries);
            Hashtable hashtable = new Hashtable((IEqualityComparer)StringComparer.OrdinalIgnoreCase);
            foreach (ProjectInSolution projectInSolution1 in this.projectsInOrder)
            {
                string newUniqueName = projectInSolution1.GetUniqueProjectName();
                Uri result;
                if (projectInSolution1.ProjectType == SolutionProjectType.WebProject && Uri.TryCreate(projectInSolution1.RelativePath, UriKind.Absolute, out result) && !result.IsDefaultPort)
                {
                    foreach (ProjectInSolution projectInSolution2 in this.projectsInOrder)
                    {
                        if (projectInSolution1 != projectInSolution2 && string.Equals(projectInSolution2.ProjectName, projectInSolution1.ProjectName, StringComparison.OrdinalIgnoreCase))
                        {
                            CultureInfo invariantCulture = CultureInfo.InvariantCulture;
                            string format = "{0}:{1}";
                            object[] objArray = new object[2];
                            int index1 = 0;
                            string str = newUniqueName;
                            objArray[index1] = (object)str;
                            int index2 = 1;
                            // ISSUE: variable of a boxed type
                            var local = (ValueType)result.Port;
                            objArray[index2] = (object)local;
                            newUniqueName = string.Format((IFormatProvider)invariantCulture, format, objArray);
                            projectInSolution1.UpdateUniqueProjectName(newUniqueName);
                            break;
                        }
                    }
                }
                int num = hashtable[(object)newUniqueName] == null ? 1 : 0;
                string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile);
                string resourceName = "SolutionParseDuplicateProject";
                object[] objArray1 = new object[1];
                int index = 0;
                string str1 = newUniqueName;
                objArray1[index] = (object)str1;
                hashtable[(object)newUniqueName] = (object)projectInSolution1;
            }
        }

        private void ParseFileHeader()
        {
            for (int index = 1; index <= 2; ++index)
            {
                string str = this.ReadLine();
                if (str != null)
                {
                    if (str.StartsWith("Microsoft Visual Studio Solution File, Format Version ", StringComparison.Ordinal))
                    {
                        this.ValidateSolutionFileVersion(str.Substring("Microsoft Visual Studio Solution File, Format Version ".Length));
                        return;
                    }
                }
                else
                    break;
            }
        }

        private void ValidateSolutionFileVersion(string versionString)
        {
            System.Version result = (System.Version)null;
            if (!System.Version.TryParse(versionString, out result))
            {
                int num = 0;
                string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                string resourceName = "SolutionParseVersionMismatchError";
                object[] objArray = new object[2];
                int index1 = 0;
                // ISSUE: variable of a boxed type
                var local1 = (ValueType)7;
                objArray[index1] = (object)local1;
                int index2 = 1;
                // ISSUE: variable of a boxed type
                var local2 = (ValueType)12;
                objArray[index2] = (object)local2;
            }
            this.slnFileActualVersion = result.Major;
            int num1 = this.slnFileActualVersion >= 7 ? 1 : 0;
            string errorSubCategoryResourceName1 = "SubCategoryForSolutionParsingErrors";
            BuildEventFileInfo projectFile1 = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
            string resourceName1 = "SolutionParseVersionMismatchError";
            object[] objArray1 = new object[2];
            int index3 = 0;
            // ISSUE: variable of a boxed type
            var local3 = (ValueType)7;
            objArray1[index3] = (object)local3;
            int index4 = 1;
            // ISSUE: variable of a boxed type
            var local4 = (ValueType)12;
            objArray1[index4] = (object)local4;
            if (this.slnFileActualVersion <= 12)
                return;
            ArrayList arrayList = this.solutionParserComments;
            string resourceName2 = "UnrecognizedSolutionComment";
            object[] objArray2 = new object[1];
            int index5 = 0;
            // ISSUE: variable of a boxed type
            var local5 = (ValueType)this.slnFileActualVersion;
            objArray2[index5] = (object)local5;
            string str = string.Format(resourceName2, objArray2);
            arrayList.Add((object)str);
        }

        private void ParseProject(string firstLine)
        {
            ProjectInSolution projectInSolution = new ProjectInSolution(this);
            this.ParseFirstProjectLine(firstLine, projectInSolution);
        label_10:
            string str1;
            while ((str1 = this.ReadLine()) != null && !(str1 == "EndProject"))
            {
                if (str1.StartsWith("ProjectSection(ProjectDependencies)", StringComparison.Ordinal))
                {
                    string input = this.ReadLine();
                    while (true)
                    {
                        if (input != null && !input.StartsWith("EndProjectSection", StringComparison.Ordinal))
                        {
                            Match match = SolutionParser.crackPropertyLine.Match(input);
                            int num = match.Success ? 1 : 0;
                            string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                            BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                            string resourceName = "SolutionParseProjectDepGuidError";
                            object[] objArray = new object[1];
                            int index = 0;
                            string projectName = projectInSolution.ProjectName;
                            objArray[index] = (object)projectName;
                            string str2 = match.Groups["PROPERTYNAME"].Value.Trim();
                            projectInSolution.Dependencies.Add((object)str2);
                            input = this.ReadLine();
                        }
                        else
                            goto label_10;
                    }
                }
                else if (str1.StartsWith("ProjectSection(WebsiteProperties)", StringComparison.Ordinal))
                {
                    string input = this.ReadLine();
                    while (true)
                    {
                        if (input != null && !input.StartsWith("EndProjectSection", StringComparison.Ordinal))
                        {
                            Match match = SolutionParser.crackPropertyLine.Match(input);
                            int num = match.Success ? 1 : 0;
                            string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                            BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                            string resourceName = "SolutionParseWebProjectPropertiesError";
                            object[] objArray = new object[1];
                            int index = 0;
                            string projectName = projectInSolution.ProjectName;
                            objArray[index] = (object)projectName;
                            string propertyName = match.Groups["PROPERTYNAME"].Value.Trim();
                            string propertyValue = match.Groups["PROPERTYVALUE"].Value.Trim();
                            this.ParseAspNetCompilerProperty(projectInSolution, propertyName, propertyValue);
                            input = this.ReadLine();
                        }
                        else
                            goto label_10;
                    }
                }
            }
            int num1 = str1 != null ? 1 : 0;
            string errorSubCategoryResourceName1 = "SubCategoryForSolutionParsingErrors";
            BuildEventFileInfo projectFile1 = new BuildEventFileInfo(this.SolutionFile);
            string resourceName1 = "SolutionParseProjectEofError";
            object[] objArray1 = new object[1];
            int index1 = 0;
            string projectName1 = projectInSolution.ProjectName;
            objArray1[index1] = (object)projectName1;
            if (projectInSolution == null)
                return;
            this.AddProjectToSolution(projectInSolution);
            if (!this.IsEtpProjectFile(projectInSolution.RelativePath))
                return;
            this.ParseEtpProject(projectInSolution);
        }

        internal void ParseEtpProject(ProjectInSolution etpProj)
        {
            XmlDocument xmlDocument = new XmlDocument();
            string filename = Path.Combine(this.solutionFileDirectory, etpProj.RelativePath);
            string directoryName = Path.GetDirectoryName(etpProj.RelativePath);
            try
            {
                xmlDocument.Load(filename);
                foreach (XmlNode xmlNode1 in xmlDocument.DocumentElement.SelectNodes("/EFPROJECT/GENERAL/References/Reference"))
                {
                    string innerText = xmlNode1.SelectSingleNode("FILE").InnerText;
                    if (innerText != null)
                    {
                        ProjectInSolution projectInSolution1 = new ProjectInSolution(this);
                        projectInSolution1.RelativePath = Path.Combine(directoryName, innerText);
                        this.ValidateProjectRelativePath(projectInSolution1);
                        projectInSolution1.ProjectType = SolutionProjectType.EtpSubProject;
                        ProjectInSolution projectInSolution2 = projectInSolution1;
                        string relativePath = projectInSolution2.RelativePath;
                        projectInSolution2.ProjectName = relativePath;
                        XmlNode xmlNode2 = xmlNode1.SelectSingleNode("GUIDPROJECTID");
                        projectInSolution1.ProjectGuid = xmlNode2 == null ? string.Empty : xmlNode2.InnerText;
                        this.AddProjectToSolution(projectInSolution1);
                        if (this.IsEtpProjectFile(innerText))
                            this.ParseEtpProject(projectInSolution1);
                    }
                }
            }
            catch (SecurityException ex)
            {
                string str1;
                // ISSUE: explicit reference operation
                // ISSUE: variable of a reference type
                string str2;
                // ISSUE: explicit reference operation
                // ISSUE: variable of a reference type
                string resourceName = "Shared.ProjectFileCouldNotBeLoaded";
                object[] objArray = new object[2];
                int index1 = 0;
                string relativePath = etpProj.RelativePath;
                objArray[index1] = (object)relativePath;
                int index2 = 1;
                string message = ex.Message;
                objArray[index2] = (object)message;
            }
            catch (NotSupportedException ex)
            {
                string str1;
                // ISSUE: explicit reference operation
                // ISSUE: variable of a reference type
                string str2;
                // ISSUE: explicit reference operation
                // ISSUE: variable of a reference type
                string resourceName = "Shared.ProjectFileCouldNotBeLoaded";
                object[] objArray = new object[2];
                int index1 = 0;
                string relativePath = etpProj.RelativePath;
                objArray[index1] = (object)relativePath;
                int index2 = 1;
                string message = ex.Message;
                objArray[index2] = (object)message;
            }
          
        }

        private void AddProjectToSolution(ProjectInSolution proj)
        {
            if (!string.IsNullOrEmpty(proj.ProjectGuid))
                this.projects[(object)proj.ProjectGuid] = (object)proj;
            this.projectsInOrder.Add((object)proj);
        }

        private bool IsEtpProjectFile(string projectFile)
        {
            return projectFile.EndsWith(".etp", StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateProjectRelativePath(ProjectInSolution proj)
        {
            int num1 = proj.RelativePath.IndexOfAny(Path.GetInvalidPathChars()) == -1 ? 1 : 0;
            string errorSubCategoryResourceName1 = "SubCategoryForSolutionParsingErrors";
            BuildEventFileInfo projectFile1 = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
            string resourceName1 = "SolutionParseInvalidProjectFileNameCharacters";
            object[] objArray1 = new object[2];
            int index1 = 0;
            string projectName1 = proj.ProjectName;
            objArray1[index1] = (object)projectName1;
            int index2 = 1;
            string relativePath = proj.RelativePath;
            objArray1[index2] = (object)relativePath;
            int num2 = proj.RelativePath.Length > 0 ? 1 : 0;
            string errorSubCategoryResourceName2 = "SubCategoryForSolutionParsingErrors";
            BuildEventFileInfo projectFile2 = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
            string resourceName2 = "SolutionParseInvalidProjectFileNameEmpty";
            object[] objArray2 = new object[1];
            int index3 = 0;
            string projectName2 = proj.ProjectName;
            objArray2[index3] = (object)projectName2;
        }

        private void ParseAspNetCompilerProperty(ProjectInSolution proj, string propertyName, string propertyValue)
        {
            int length = propertyName.IndexOf('.');
            if (length != -1)
            {
                string str1 = propertyName.Substring(0, length);
                string str2 = propertyName.Length - length > 0 ? propertyName.Substring(length + 1, propertyName.Length - length - 1) : "";
                propertyValue = this.TrimQuotes(propertyValue);
                object obj = proj.AspNetConfigurations[(object)str1];
                AspNetCompilerParameters compilerParameters;
                if (obj == null)
                {
                    compilerParameters = new AspNetCompilerParameters();
                    compilerParameters.aspNetVirtualPath = string.Empty;
                    compilerParameters.aspNetPhysicalPath = string.Empty;
                    compilerParameters.aspNetTargetPath = string.Empty;
                    compilerParameters.aspNetForce = string.Empty;
                    compilerParameters.aspNetUpdateable = string.Empty;
                    compilerParameters.aspNetDebug = string.Empty;
                    compilerParameters.aspNetKeyFile = string.Empty;
                    compilerParameters.aspNetKeyContainer = string.Empty;
                    compilerParameters.aspNetDelaySign = string.Empty;
                    compilerParameters.aspNetAPTCA = string.Empty;
                    compilerParameters.aspNetFixedNames = string.Empty;
                }
                else
                    compilerParameters = (AspNetCompilerParameters)obj;
                if (str2 == "AspNetCompiler.VirtualPath")
                    compilerParameters.aspNetVirtualPath = propertyValue;
                else if (str2 == "AspNetCompiler.PhysicalPath")
                    compilerParameters.aspNetPhysicalPath = propertyValue;
                else if (str2 == "AspNetCompiler.TargetPath")
                    compilerParameters.aspNetTargetPath = propertyValue;
                else if (str2 == "AspNetCompiler.ForceOverwrite")
                    compilerParameters.aspNetForce = propertyValue;
                else if (str2 == "AspNetCompiler.Updateable")
                    compilerParameters.aspNetUpdateable = propertyValue;
                else if (str2 == "AspNetCompiler.Debug")
                    compilerParameters.aspNetDebug = propertyValue;
                else if (str2 == "AspNetCompiler.KeyFile")
                    compilerParameters.aspNetKeyFile = propertyValue;
                else if (str2 == "AspNetCompiler.KeyContainer")
                    compilerParameters.aspNetKeyContainer = propertyValue;
                else if (str2 == "AspNetCompiler.DelaySign")
                    compilerParameters.aspNetDelaySign = propertyValue;
                else if (str2 == "AspNetCompiler.AllowPartiallyTrustedCallers")
                    compilerParameters.aspNetAPTCA = propertyValue;
                else if (str2 == "AspNetCompiler.FixedNames")
                    compilerParameters.aspNetFixedNames = propertyValue;
                proj.AspNetConfigurations[(object)str1] = (object)compilerParameters;
            }
            else if (string.Compare(propertyName, "ProjectReferences", StringComparison.OrdinalIgnoreCase) == 0)
            {
                string str1 = propertyValue;
                char[] separator = new char[1];
                int index = 0;
                int num1 = 59;
                separator[index] = (char)num1;
                int num2 = 1;
                foreach (string str2 in str1.Split(separator, (StringSplitOptions)num2))
                {
                    if (str2.IndexOf('|') != -1)
                    {
                        int startIndex = str2.IndexOf('{');
                        if (startIndex != -1)
                        {
                            int num3 = str2.IndexOf('}', startIndex);
                            if (num3 != -1)
                            {
                                string str3 = str2.Substring(startIndex, num3 - startIndex + 1);
                                proj.Dependencies.Add((object)str3);
                                proj.ProjectReferences.Add((object)str3);
                            }
                        }
                    }
                }
            }
            else
            {
                if (string.Compare(propertyName, "TargetFrameworkMoniker", StringComparison.OrdinalIgnoreCase) != 0)
                    return;
                string escapedString = this.TrimQuotes(propertyValue);
                proj.TargetFrameworkMoniker = (escapedString);
            }
        }

        private string TrimQuotes(string property)
        {
            if (property != null && property.Length > 0 && (int)property[0] == 34)
            {
                string str = property;
                int index = str.Length - 1;
                if ((int)str[index] == 34)
                    return property.Substring(1, property.Length - 2);
            }
            return property;
        }

        internal void ParseFirstProjectLine(string firstLine, ProjectInSolution proj)
        {
            Match match = SolutionParser.crackProjectLine.Match(firstLine);
            string strA = match.Groups["PROJECTTYPEGUID"].Value.Trim();
            proj.ProjectName = match.Groups["PROJECTNAME"].Value.Trim();
            proj.RelativePath = match.Groups["RELATIVEPATH"].Value.Trim();
            proj.ProjectGuid = match.Groups["PROJECTGUID"].Value.Trim();
            this.ValidateProjectRelativePath(proj);
            if (string.Compare(strA, "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(strA, "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", StringComparison.OrdinalIgnoreCase) == 0 || (string.Compare(strA, "{F2A71F9B-5D33-465A-A702-920D77279786}", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(strA, "{C8D11400-126E-41CD-887F-60BD40844F9E}", StringComparison.OrdinalIgnoreCase) == 0) || string.Compare(strA, "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}", StringComparison.OrdinalIgnoreCase) == 0)
                proj.ProjectType = SolutionProjectType.KnownToBeMSBuildFormat;
            else if (string.Compare(strA, "{2150E333-8FDC-42A3-9474-1A3956D46DE8}", StringComparison.OrdinalIgnoreCase) == 0)
                proj.ProjectType = SolutionProjectType.SolutionFolder;
            else if (string.Compare(strA, "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", StringComparison.OrdinalIgnoreCase) == 0)
            {
                if (string.Equals(proj.Extension, ".vcxproj", StringComparison.OrdinalIgnoreCase))
                {
                    proj.ProjectType = SolutionProjectType.KnownToBeMSBuildFormat;
                }
                else
                {
                    if (this.parsingForConversionOnly)
                        return;
                    BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile);
                    string resourceName = "ProjectUpgradeNeededToVcxProj";
                    object[] objArray = new object[1];
                    int index = 0;
                    string relativePath = proj.RelativePath;
                    objArray[index] = (object)relativePath;
                }
            }
            else if (string.Compare(strA, "{E24C65DC-7377-472B-9ABA-BC803B73C61A}", StringComparison.OrdinalIgnoreCase) == 0)
            {
                proj.ProjectType = SolutionProjectType.WebProject;
                this.solutionContainsWebProjects = true;
            }
            else if (string.Compare(strA, "{2CFEAB61-6A3B-4EB8-B523-560B4BEEF521}", StringComparison.OrdinalIgnoreCase) == 0)
            {
                proj.ProjectType = SolutionProjectType.WebDeploymentProject;
                this.solutionContainsWebDeploymentProjects = true;
            }
            else
                proj.ProjectType = SolutionProjectType.Unknown;
        }

        internal void ParseNestedProjects()
        {
            while (true)
            {
                string input = this.ReadLine();
                if (input != null && !(input == "EndGlobalSection"))
                {
                    Match match = SolutionParser.crackPropertyLine.Match(input);
                    string str1 = match.Groups["PROPERTYNAME"].Value.Trim();
                    string str2 = match.Groups["PROPERTYVALUE"].Value.Trim();
                    ProjectInSolution projectInSolution = (ProjectInSolution)this.projects[(object)str1];
                    int num = projectInSolution != null ? 1 : 0;
                    string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                    BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                    string resourceName = "SolutionParseNestedProjectUndefinedError";
                    object[] objArray = new object[2];
                    int index1 = 0;
                    string str3 = str1;
                    objArray[index1] = (object)str3;
                    int index2 = 1;
                    string str4 = str2;
                    objArray[index2] = (object)str4;
                    projectInSolution.ParentProjectGuid = str2;
                }
                else
                    break;
            }
        }

        internal void ParseSolutionConfigurations()
        {
            char[] chArray1 = new char[1];
            int index1 = 0;
            int num1 = 61;
            chArray1[index1] = (char)num1;
            char[] chArray2 = chArray1;
            char[] chArray3 = new char[1];
            int index2 = 0;
            int num2 = 124;
            chArray3[index2] = (char)num2;
            char[] chArray4 = chArray3;
            while (true)
            {
                string str1;
                string[] strArray1;
                string strA;
                do
                {
                    str1 = this.ReadLine();
                    if (str1 != null && !(str1 == "EndGlobalSection"))
                    {
                        strArray1 = str1.Split(chArray2);
                        int num3 = strArray1.Length == 2 ? 1 : 0;
                        string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                        BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                        string resourceName = "SolutionParseInvalidSolutionConfigurationEntry";
                        object[] objArray = new object[1];
                        int index3 = 0;
                        string str2 = str1;
                        objArray[index3] = (object)str2;
                        strA = strArray1[0].Trim();
                    }
                    else
                        goto label_4;
                }
                while (string.Compare(strA, "DESCRIPTION", StringComparison.OrdinalIgnoreCase) == 0);
                int num4 = strA == strArray1[1].Trim() ? 1 : 0;
                string errorSubCategoryResourceName1 = "SubCategoryForSolutionParsingErrors";
                BuildEventFileInfo projectFile1 = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                string resourceName1 = "SolutionParseInvalidSolutionConfigurationEntry";
                object[] objArray1 = new object[1];
                int index4 = 0;
                string str3 = str1;
                objArray1[index4] = (object)str3;
                string[] strArray2 = strA.Split(chArray4);
                int num5 = strArray2.Length == 2 ? 1 : 0;
                string errorSubCategoryResourceName2 = "SubCategoryForSolutionParsingErrors";
                BuildEventFileInfo projectFile2 = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                string resourceName2 = "SolutionParseInvalidSolutionConfigurationEntry";
                object[] objArray2 = new object[1];
                int index5 = 0;
                string str4 = str1;
                objArray2[index5] = (object)str4;
                this.solutionConfigurations.Add(new ConfigurationInSolution(strArray2[0], strArray2[1]));
            }
        label_4:;
        }

        internal Hashtable ParseProjectConfigurations()
        {
            Hashtable hashtable = new Hashtable((IEqualityComparer)StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                string str1 = this.ReadLine();
                if (str1 != null && !(str1 == "EndGlobalSection"))
                {
                    string str2 = str1;
                    char[] chArray = new char[1];
                    int index1 = 0;
                    int num1 = 61;
                    chArray[index1] = (char)num1;
                    string[] strArray = str2.Split(chArray);
                    int num2 = strArray.Length == 2 ? 1 : 0;
                    string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                    BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile, this.currentLineNumber, 0);
                    string resourceName = "SolutionParseInvalidProjectSolutionConfigurationEntry";
                    object[] objArray = new object[1];
                    int index2 = 0;
                    string str3 = str1;
                    objArray[index2] = (object)str3;
                    hashtable[(object)strArray[0].Trim()] = (object)strArray[1].Trim();
                }
                else
                    break;
            }
            return hashtable;
        }

        internal void ProcessProjectConfigurationSection(Hashtable rawProjectConfigurationsEntries)
        {
            char[] chArray1 = new char[1];
            int index1 = 0;
            int num1 = 124;
            chArray1[index1] = (char)num1;
            char[] chArray2 = chArray1;
            foreach (ProjectInSolution projectInSolution in this.projectsInOrder)
            {
                if (projectInSolution.ProjectType != SolutionProjectType.SolutionFolder)
                {
                    foreach (ConfigurationInSolution configurationInSolution1 in this.solutionConfigurations)
                    {
                        CultureInfo invariantCulture1 = CultureInfo.InvariantCulture;
                        string format1 = "{0}.{1}.ActiveCfg";
                        object[] objArray1 = new object[2];
                        int index2 = 0;
                        string projectGuid1 = projectInSolution.ProjectGuid;
                        objArray1[index2] = (object)projectGuid1;
                        int index3 = 1;
                        string fullName1 = configurationInSolution1.FullName;
                        objArray1[index3] = (object)fullName1;
                        string str1 = string.Format((IFormatProvider)invariantCulture1, format1, objArray1);
                        CultureInfo invariantCulture2 = CultureInfo.InvariantCulture;
                        string format2 = "{0}.{1}.Build.0";
                        object[] objArray2 = new object[2];
                        int index4 = 0;
                        string projectGuid2 = projectInSolution.ProjectGuid;
                        objArray2[index4] = (object)projectGuid2;
                        int index5 = 1;
                        string fullName2 = configurationInSolution1.FullName;
                        objArray2[index5] = (object)fullName2;
                        string str2 = string.Format((IFormatProvider)invariantCulture2, format2, objArray2);
                        if (rawProjectConfigurationsEntries.ContainsKey((object)str1))
                        {
                            string[] strArray = ((string)rawProjectConfigurationsEntries[(object)str1]).Split(chArray2);
                            int num2 = strArray.Length <= 2 ? 1 : 0;
                            string errorSubCategoryResourceName = "SubCategoryForSolutionParsingErrors";
                            BuildEventFileInfo projectFile = new BuildEventFileInfo(this.SolutionFile);
                            string resourceName = "SolutionParseInvalidProjectSolutionConfigurationEntry";
                            object[] objArray3 = new object[1];
                            int index6 = 0;
                            CultureInfo invariantCulture3 = CultureInfo.InvariantCulture;
                            string format3 = "{0} = {1}";
                            object[] objArray4 = new object[2];
                            int index7 = 0;
                            string str3 = str1;
                            objArray4[index7] = (object)str3;
                            int index8 = 1;
                            object obj = rawProjectConfigurationsEntries[(object)str1];
                            objArray4[index8] = obj;
                            string str4 = string.Format((IFormatProvider)invariantCulture3, format3, objArray4);
                            objArray3[index6] = (object)str4;
                            ProjectConfigurationInSolution configurationInSolution2 = new ProjectConfigurationInSolution(strArray[0], strArray.Length > 1 ? strArray[1] : string.Empty, rawProjectConfigurationsEntries.ContainsKey((object)str2));
                            projectInSolution.ProjectConfigurations[configurationInSolution1.FullName] = configurationInSolution2;
                        }
                    }
                }
            }
        }

        internal string GetDefaultConfigurationName()
        {
            if (this.defaultConfigurationName != null)
                return this.defaultConfigurationName;
            this.defaultConfigurationName = string.Empty;
            foreach (ConfigurationInSolution configurationInSolution in this.SolutionConfigurations)
            {
                if (string.Compare(configurationInSolution.ConfigurationName, "Debug", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    this.defaultConfigurationName = configurationInSolution.ConfigurationName;
                    break;
                }
            }
            if (this.defaultConfigurationName.Length == 0 && this.SolutionConfigurations.Count > 0)
                this.defaultConfigurationName = this.SolutionConfigurations[0].ConfigurationName;
            return this.defaultConfigurationName;
        }

        internal string GetDefaultPlatformName()
        {
            if (this.defaultPlatformName != null)
                return this.defaultPlatformName;
            this.defaultPlatformName = string.Empty;
            foreach (ConfigurationInSolution configurationInSolution in this.SolutionConfigurations)
            {
                if (string.Compare(configurationInSolution.PlatformName, "Mixed Platforms", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    this.defaultPlatformName = configurationInSolution.PlatformName;
                    break;
                }
                if (string.Compare(configurationInSolution.PlatformName, "Any CPU", StringComparison.OrdinalIgnoreCase) == 0)
                    this.defaultPlatformName = configurationInSolution.PlatformName;
            }
            if (this.defaultPlatformName.Length == 0 && this.SolutionConfigurations.Count > 0)
                this.defaultPlatformName = this.SolutionConfigurations[0].PlatformName;
            return this.defaultPlatformName;
        }

        internal string GetProjectUniqueNameByGuid(string projectGuid)
        {
            ProjectInSolution projectInSolution = (ProjectInSolution)this.projects[(object)projectGuid];
            if (projectInSolution != null)
                return projectInSolution.GetUniqueProjectName();
            return (string)null;
        }

        internal string GetProjectRelativePathByGuid(string projectGuid)
        {
            ProjectInSolution projectInSolution = (ProjectInSolution)this.projects[(object)projectGuid];
            if (projectInSolution != null)
                return projectInSolution.RelativePath;
            return (string)null;
        }
    }
}
