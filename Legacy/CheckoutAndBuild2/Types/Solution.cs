using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;

namespace FG.CheckoutAndBuild2.Types
{
	public class Solution
	{
		public List<ProjectInSolution> Projects { get; private set; }

		public Solution(string solutionFileName)
		{
            var parser = new SolutionParser();
            
            using (var streamReader = new StreamReader(solutionFileName))
            {
                parser.SolutionReader = streamReader;
                parser.ParseSolution();
			}
            Projects = parser.Projects.ToList();
		}
	}
}
