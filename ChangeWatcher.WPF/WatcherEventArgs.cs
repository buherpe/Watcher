namespace Watcher
{
    public class WatcherEventArgs
    {
        public int WatcherId { get; set; }

        public WatcherEventArgs(int watcherId)
        {
            WatcherId = watcherId;
        }
    }
}