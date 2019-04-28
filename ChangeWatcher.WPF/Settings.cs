using System.Collections.Generic;
using System.ComponentModel;

namespace Watcher
{
    public class Settings
    {
        public List<ChangeWatcher> Watchers { get; set; }
        public SortDescriptionCollection SortingChanges { get; set; }
    }
}