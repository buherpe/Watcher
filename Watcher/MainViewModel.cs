using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using DynamicData;
using DynamicData.Binding;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using NLog;
using Tools;
using Timer = System.Timers.Timer;

namespace Watcher
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private static Logger Log = LogManager.GetCurrentClassLogger();

        public string AppNameWithVersion => Helper.AppNameWithVersion;

        //public ObservableCollection<Change> Changes { get; } = new ObservableCollection<Change>();

        //public ObservableCollection<DataGridColumn> Columns { get; } = new ObservableCollection<DataGridColumn>();

        public ObservableCollection<ChangeWatcher> Watchers { get; } = new ObservableCollection<ChangeWatcher>();

        private int _changesCounter;

        private int _watchersCounter;

        private RelayCommand _addWatcherCommand;

        public RelayCommand AddWatcherCommand =>
            _addWatcherCommand ??
            (_addWatcherCommand = new RelayCommand(obj => AddWatcher()));

        private RelayCommand _closingCommand;

        public RelayCommand ClosingCommand =>
            _closingCommand ??
            (_closingCommand = new RelayCommand(obj =>
            {
                if (Settings.SavingTimer.Enabled)
                {
                    Settings.SavingTimer.Enabled = false;
                    SaveSettings();
                }
            }));

        private RelayCommand _sortingCommand;

        public RelayCommand SortingCommand =>
            _sortingCommand ??
            (_sortingCommand = new RelayCommand(obj =>
            {
                //Console.WriteLine($"obj == null: {obj == null}");
                //RestartSavingTimer();
            }));

        private RelayCommand _aboutCommand;

        public RelayCommand AboutCommand =>
            _aboutCommand ??
            (_aboutCommand = new RelayCommand(obj =>
            {
                DialogCoordinator.Instance.ShowMessageAsync(this, $"About",
                    $"{Helper.AppNameWithVersion}\r\n\r\nIcons made by Freepik from www.flaticon.com");
            }));

        private RelayCommand _updatedCommand;

        public RelayCommand UpdatedCommand =>
            _updatedCommand ??
            (_updatedCommand = new RelayCommand(obj =>
            {
                Log.Info($"UpdatedCommand start");
                Process.Start("..\\Watcher.exe");
                Application.Current.Shutdown(0);
            }));

        //public string SettingsPath { get; } = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\{Helper.AppName}\\Settings.json";
        //public string SettingsPath { get; } = $"..\\Settings.json";

        public ThisSettings Settings { get; } = new ThisSettings();

        public SourceCache<Change, int> ChangeCache { get; set; } = new SourceCache<Change, int>(x => x.Id);

        public ReadOnlyObservableCollection<Change> ChangeData => _changeData;

        private readonly ReadOnlyObservableCollection<Change> _changeData;

        private readonly IDisposable _cleanUp;

        public PageParameterData PageParameters { get; } = new PageParameterData(1, 100);

        private string _searchText;

        public string SearchText
        {
            get => _searchText;
            set => OnPropertyChanged(ref _searchText, value);
        }

        private WindowState _windowState = WindowState.Normal;

        public WindowState WindowState
        {
            get => _windowState;
            set
            {
                if (value == WindowState.Minimized) return;

                if (OnPropertyChanged(ref _windowState, value))
                {
                    Settings.RestartSavingTimer();
                }
            }
        }

        private bool _updated;

        public bool Updated
        {
            get => _updated;
            set => OnPropertyChanged(ref _updated, value);
        }

        public MainViewModel()
        {
            //avoid a "object reference not set to an instance of an object@ exception in XAML code while design time
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                AddWatcher("C:\\");
                return;
            }

            Helper.AppUpdated += OnUpdated;

//#if DEBUG
//            Observable.Timer(DateTimeOffset.Now.AddSeconds(2))
//                .Subscribe(l => Updated = true);
//#endif

            var filter = this.WhenValueChanged(t => t.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(BuildFilter);

            var pager = PageParameters.WhenChanged(vm => vm.PageSize, vm => vm.CurrentPage,
                    (_, size, pge) => new PageRequest(pge, size))
                .StartWith(new PageRequest(1, 100))
                .DistinctUntilChanged()
                .Sample(TimeSpan.FromMilliseconds(100));

            _cleanUp = ChangeCache.Connect()
                .Filter(filter)
                .Sort(SortExpressionComparer<Change>.Descending(t => t.Id),
                    SortOptimisations.ComparesImmutableValuesOnly, 25)
                .Page(pager)
                .ObserveOnDispatcher()
                .Do(changes => PageParameters.Update(changes.Response))
                .Bind(out _changeData)
                .DisposeMany()
                .Subscribe();

            Log.Info($"SettingsPath: {Helper.SettingsPath}");

            Settings.SavingTimer.Elapsed += (s, e) => { SaveSettings(); };

            LoadSettings();
        }

        private void OnUpdated()
        {
            Updated = true;
        }

        private Func<Change, bool> BuildFilter(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) return change => true;

            return change => searchText
                .Split(' ')
                .All(x => change.FullPath.Contains(x, StringComparison.OrdinalIgnoreCase));
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
            watcher.OnError += (watcherId, e) =>
            {
                Log.Error(e.GetException());
                AddChange(watcherId, ChangeType.Error, e.GetException().Message);
            };
            watcher.OnWatcherDeleted += watcherId =>
            {
                Watchers.Remove(Watchers.FirstOrDefault(x => x.Id == watcherId));
                Settings.RestartSavingTimer();
            };
            watcher.Id = _watchersCounter++;
            watcher.IncludeSubdirectories = includeSubdirectories;
            watcher.EnableRaisingEvents = enableRaisingEvents;

            watcher.PropertyChanged += WatcherOnPropertyChanged;

            Watchers.Add(watcher);
        }

        private void WatcherOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Settings.RestartSavingTimer();
        }

        public void AddChange(int watcherId, ChangeType changeType, string fullPath, string oldFullPath = "")
        {
            ChangeCache.AddOrUpdate(new Change
            {
                Id = _changesCounter++,
                DateTime = DateTime.Now,
                WatcherId = watcherId,
                ChangeType = changeType,
                FullPath = fullPath,
                OldFullPath = oldFullPath
            });
        }

        public void SaveSettings()
        {
            Log.Info("Settings saving...");
            //var settings = new ThisSettings();

            Settings.Watchers = new List<ChangeWatcher>(Watchers);
            //settings.SortingChanges = CollectionViewSource.GetDefaultView(Changes).SortDescriptions;
            Settings.WindowState = WindowState;

            Settings.Save();

            Log.Info("Settings saved");
        }

        public void LoadSettings()
        {
            Log.Info("Settings loading...");

            var settings = Settings.Load();

            if (settings != null)
            {
                if (settings.Watchers?.Any() ?? false)
                    foreach (var watcher in settings.Watchers)
                    {
                        AddWatcher(watcher);
                    }

                WindowState = settings.WindowState;

                //if (settings.SortingChanges?.Any() ?? false)
                //    foreach (var settingsSortingChange in settings.SortingChanges)
                //    {
                //        CollectionViewSource.GetDefaultView(Changes).SortDescriptions.Add(settingsSortingChange);
                //    }
            }
            else
            {
                // first run
                AddWatcher(@"C:\", enableRaisingEvents: true);
                //CollectionViewSource.GetDefaultView(Changes).SortDescriptions
                //    .Add(new SortDescription("Id", ListSortDirection.Descending));
            }

            Log.Info("Settings loaded");
        }

        public void Dispose()
        {
            //Settings.SavingTimer?.Dispose();
            _cleanUp.Dispose();
        }
    }
}