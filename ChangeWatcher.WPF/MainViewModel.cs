using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Watcher
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Change> Changes { get; set; } = new ObservableCollection<Change>();

        public ObservableCollection<ChangeWatcher> Watchers { get; set; } = new ObservableCollection<ChangeWatcher>();

        private int _changesCounter;

        private int _watchersCounter;

        private RelayCommand _addWatcherCommand;

        public RelayCommand AddWatcherCommand =>
            _addWatcherCommand ??
            (_addWatcherCommand = new RelayCommand(obj => { AddWatcher(); }));

        object _lockChanges = new object();

        public MainViewModel()
        {
            BindingOperations.EnableCollectionSynchronization(Changes, _lockChanges);

            //avoid a "object reference not set to an instance of an object@ exception in XAML code while design time
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                AddWatcher(@"C:\");

                return;
            }

            var q = @"[
  {
    ""Path"": ""C:\\"",
    ""Filter"": """",
    ""IncludeSubdirectories"": true,
    ""EnableRaisingEvents"": true
  },
  {
    ""Path"": ""G:\\"",
    ""Filter"": """",
    ""IncludeSubdirectories"": true,
    ""EnableRaisingEvents"": false
  },
]";

            var watchers = JsonConvert.DeserializeObject<ObservableCollection<ChangeWatcher>>(q);

            foreach (var watcher in watchers)
            {
                AddWatcher(watcher);
            }

            //AddWatcher(@"C:\", enableRaisingEvents: true);

            //var o = JsonConvert.SerializeObject(Watchers, Formatting.Indented);
            //Console.WriteLine(o);
        }

        public void AddWatcher(ChangeWatcher watcher)
        {
            AddWatcher(watcher.Path, watcher.Filter, watcher.IncludeSubdirectories, watcher.EnableRaisingEvents);
        }

        public void AddWatcher(string path = "", string filter = "", bool includeSubdirectories = true,
            bool enableRaisingEvents = false)
        {
            var watcher = new ChangeWatcher(path, filter);

            watcher.Created += (watcherId, e) => AddChange(watcherId, ChangeType.Created, e.FullPath);
            watcher.Changed += (watcherId, e) => AddChange(watcherId, ChangeType.Changed, e.FullPath);
            watcher.Renamed += (watcherId, e) => AddChange(watcherId, ChangeType.Renamed, e.FullPath, e.OldFullPath);
            watcher.Deleted += (watcherId, e) => AddChange(watcherId, ChangeType.Deleted, e.FullPath);
            watcher.Error += (watcherId, e) => AddChange(watcherId, ChangeType.Error, e.GetException().Message);
            watcher.OnWatcherDeleted += watcherId => Watchers.Remove(Watchers.FirstOrDefault(x => x.Id == watcherId));
            watcher.Id = _watchersCounter++;
            watcher.IncludeSubdirectories = includeSubdirectories;
            watcher.EnableRaisingEvents = enableRaisingEvents;

            Watchers.Add(watcher);
        }

        public void AddChange(int watcherId, ChangeType changeType, string fullPath, string oldFullPath = "")
        {
            Changes.Add(new Change
            {
                Id = _changesCounter++,
                DateTime = DateTime.Now,
                WatcherId = watcherId,
                ChangeType = changeType,
                FullPath = fullPath,
                OldFullPath = oldFullPath
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [Annotations.NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}