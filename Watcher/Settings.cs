using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Watcher
{
    public class Settings
    {
        public List<ChangeWatcher> Watchers { get; set; }
        //public SortDescriptionCollection SortingChanges { get; set; }

        public WindowState WindowState { get; set; }
    }
}