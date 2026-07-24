namespace CheckoutAndBuild.Core.Contracts.Settings
{
    public interface IDefaultBehavior
    {
        bool? ShouldIncludedByDefault(ISolutionProjectModel solution);
    }
}
