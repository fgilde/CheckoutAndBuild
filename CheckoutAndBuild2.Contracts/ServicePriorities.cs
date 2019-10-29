namespace CheckoutAndBuild2.Contracts
{
	public static class ServicePriorities
	{
		public const int CleanupServicePriority = 0;		
		public const int CheckoutServicePriority = 100;
        public const int NugetRestoreServicePriority = 200;
        public const int BuildServicePriority = 300;
		public const int UnitTestServicePriority = 400;
	}
}