namespace CheckoutAndBuild2.Contracts
{
    public class GitRepository
    {
        public string Name { get; private set; }

        public string Path { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public GitRepository(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}