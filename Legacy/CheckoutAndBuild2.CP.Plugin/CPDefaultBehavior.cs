using System.ComponentModel.Composition;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.CP.Plugin
{
    [Export(typeof(IDefaultBehavior))]
    public class CPDefaultBehavior : IDefaultBehavior
    {
        public bool? ShouldIncludedByDefault(ISolutionProjectModel solution)
        {
            if (solution.IsDelphiProject)
            {
                return solution.SolutionFileName == "CPlannerEmbedded.dproj" || solution.SolutionFileName == "CPServerEmbedded.dproj";
            }
            return solution.SolutionFileName.StartsWith("CP.");
        }
    }
    
}