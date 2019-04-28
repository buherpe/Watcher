using System;

namespace Watcher
{
    public class Change
    {
        public int Id { get; set; }

        public DateTime DateTime { get; set; }

        public int WatcherId { get; set; }

        public ChangeType ChangeType { get; set; }

        public string FullPath { get; set; }

        public string OldFullPath { get; set; }
    }
}