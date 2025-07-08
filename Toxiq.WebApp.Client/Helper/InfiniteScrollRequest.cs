namespace Toxiq.WebApp.Client.Helper
{
    public class InfiniteScrollRequest
    {
        public int Skip { get; }
        public int Take { get; }

        public InfiniteScrollRequest(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }
    }
}
