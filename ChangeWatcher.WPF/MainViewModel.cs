using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

namespace Watcher
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

        public ObservableCollection<Change> Changes { get; set; } = new ObservableCollection<Change>();

        public ObservableCollection<ChangeWatcher> Watchers { get; set; } = new ObservableCollection<ChangeWatcher>();

        private int _changesCounter;

        private int _watchersCounter;

        private RelayCommand _addWatcherCommand;

        public RelayCommand AddWatcherCommand =>
            _addWatcherCommand ??
            (_addWatcherCommand = new RelayCommand(obj => { AddWatcher(); }));

        private RelayCommand _closingCommand;

        public RelayCommand ClosingCommand =>
            _closingCommand ??
            (_closingCommand = new RelayCommand(obj =>
            {
                if (_savingTimer.Enabled)
                {
                    _savingTimer.Enabled = false;
                    SaveSettings();
                }
            }));

        object _lockChanges = new object();

        private Timer _savingTimer = new Timer(5_000)
        {
            AutoReset = false,
            Enabled = false,
        };

        public MainViewModel()
        {
            //avoid a "object reference not set to an instance of an object@ exception in XAML code while design time
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                //AddWatcher(@"C:\");
                return;
            }

            BindingOperations.EnableCollectionSynchronization(Changes, _lockChanges);

            _savingTimer.Elapsed += (s, e) => { SaveSettings(); };

            LoadSettings();
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
            watcher.OnWatcherDeleted += watcherId =>
            {
                Watchers.Remove(Watchers.FirstOrDefault(x => x.Id == watcherId));
                RestartSavingTimer();
            };
            watcher.Id = _watchersCounter++;
            watcher.IncludeSubdirectories = includeSubdirectories;
            watcher.EnableRaisingEvents = enableRaisingEvents;

            watcher.PropertyChanged += (s, e) =>
            {
                RestartSavingTimer();
            };

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

        public void RestartSavingTimer()
        {
            Console.WriteLine("Restart saving timer");
            _savingTimer.Stop();
            _savingTimer.Start();
        }

        public void SaveSettings()
        {
            Console.WriteLine("Settings saving...");
            var settings = new Settings();
            settings.Watchers = new List<ChangeWatcher>(Watchers);

            var settingsJson = JsonConvert.SerializeObject(settings, Formatting.Indented);

            File.WriteAllText("Watcher.json", settingsJson);
            Console.WriteLine("Settings saved");
        }

        public void LoadSettings()
        {
            Console.WriteLine("Settings loading...");
            if (File.Exists("Watcher.json"))
            {
                var settingsJson = File.ReadAllText("Watcher.json");
                var settings = JsonConvert.DeserializeObject<Settings>(settingsJson);

                foreach (var watcher in settings.Watchers)
                {
                    AddWatcher(watcher);
                }
            }
            else
            {
                // first run
                AddWatcher(@"C:\", enableRaisingEvents: true);
            }

            Console.WriteLine("Settings loaded");
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