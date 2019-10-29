using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SolutionPacker
{
    public class CSProjParser
    {
        private static readonly XName xReferenceTagName = XName.Get("Reference", "http://schemas.microsoft.com/developer/msbuild/2003");
        private static readonly XName xProjectReferenceTagName = XName.Get("ProjectReference", "http://schemas.microsoft.com/developer/msbuild/2003");
        private static readonly XName xNameTagName = XName.Get("Name", "http://schemas.microsoft.com/developer/msbuild/2003");
        private static readonly XName xIncludeAttributeName = "Include";

        internal static IEnumerable<string> GetReferenceNames(string csprojFilePath)
        {
            XDocument csproj = XDocument.Load(csprojFilePath);
            foreach (var referenceTag in csproj.Descendants(xReferenceTagName))
                yield return ExtractSimpleReferenceName(referenceTag.Attribute(xIncludeAttributeName));
            foreach (var nameTag in csproj.Descendants(xProjectReferenceTagName).SelectMany(tag => tag.Descendants(xNameTagName)))
                yield return nameTag.Value;
        }

        private static string ExtractSimpleReferenceName(XAttribute includeAttribute)
        {
            string referneceName = includeAttribute.Value;
            var seperatorIndex = referneceName.IndexOf(",", StringComparison.Ordinal);
            if (seperatorIndex > -1)
                return referneceName.Substring(0, seperatorIndex);
            return referneceName;
        }
    }
}