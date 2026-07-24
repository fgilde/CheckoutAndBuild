namespace FG.CheckoutAndBuild2.WorkItemPrinting
{
	public class StoryCard
	{
		public int Id { get; private set; }
		public string Iteration { get; private set; }
		public string Title { get; private set; }

		public StoryCard(int id, string iteration, string title)
		{
			Id = id;
			Iteration = iteration;
			Title = title;
		}
	}
}