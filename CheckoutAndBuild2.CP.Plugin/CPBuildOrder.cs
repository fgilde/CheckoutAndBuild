using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.CP.Plugin
{
	[Export(typeof(IDefaultBuildPriorityManager))]
	public class CPBuildOrder : IDefaultBuildPriorityManager
	{
		public int GetDefaultBuildPriority(ISolutionProjectModel solution)
		{
            var solutionFile = solution.ItemPath;
			if (solutionFile.Contains(SolutionNames.Contracts))
				return 0;
			if (solutionFile.Contains(SolutionNames.Common))
				return 1;
			if (solutionFile.Contains(SolutionNames.Suite))
				return 2;
            if (solutionFile.Contains(SolutionNames.WebModule))
                return 2;
		    if (solutionFile.Contains(SolutionNames.ApplicationServer))
		        return 3;
            if (solutionFile.Contains(SolutionNames.Database))
		        return 4;
            if (solutionFile.Contains(SolutionNames.ControlCenter))
				return 5;
			if (solutionFile.Contains(SolutionNames.CommonBO))
				return 6;
			if (solutionFile.Contains(SolutionNames.Finance))
				return 7;
			if (solutionFile.Contains(SolutionNames.Integration))
				return 7;
			if (solutionFile.Contains(SolutionNames.WebComponents))
				return 8;
			if (solutionFile.Contains(SolutionNames.DataExchange))
				return 8;
		    if (solutionFile.Contains(SolutionNames.CPServer))
		        return 9;
		    if (solutionFile.Contains(SolutionNames.Sales))
		        return 10;
		    if (solutionFile.Contains(SolutionNames.Dashboard))
		        return 10;
		    if (solutionFile.Contains(SolutionNames.Air))
		        return 100;
            return 99;
		}

	}
}