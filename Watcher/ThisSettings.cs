using System.Collections.Generic;
using System.Windows;
using Tools;

namespace Watcher
{
    public class ThisSettings : Settings<ThisSettings>
    {
        public List<ChangeWatcher> Watchers { get; set; }
        //public SortDescriptionCollection SortingChanges { get; set; }

        public WindowState WindowState { get; set; }
    }
}