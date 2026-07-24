namespace CheckoutAndBuild2.Contracts.Settings
{
    public interface IDefaultBehavior
    {
        bool? ShouldIncludedByDefault(ISolutionProjectModel solution);
    }
}