using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using DynamicData.Operators;
using Newtonsoft.Json;
using NLog;
using Tools;
using Timer = System.Timers.Timer;

namespace Watcher
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public string AppNameWithVersion => Helper.AppNameWithVersion;

        //public ObservableCollection<Change> Changes { get; } = new ObservableCollection<Change>();

        //public ObservableCollection<DataGridColumn> Columns { get; } = new ObservableCollection<DataGridColumn>();

        public ObservableCollection<ChangeWatcher> Watchers { get; } = new ObservableCollection<ChangeWatcher>();

        private int _changesCounter;

        private int _watchersCounter;

        private RelayCommand<object> _addWatcherCommand;

        public RelayCommand<object> AddWatcherCommand =>
            _addWatcherCommand ??
            (_addWatcherCommand = new RelayCommand<object>(obj => AddWatcher()));

        private RelayCommand<object> _closingCommand;

        public RelayCommand<object> ClosingCommand =>
            _closingCommand ??
            (_closingCommand = new RelayCommand<object>(obj =>
            {
                if (_savingTimer.Enabled)
                {
                    _savingTimer.Enabled = false;
                    SaveSettings();
                }
            }));

        private RelayCommand<object> _sortingCommand;

        public RelayCommand<object> SortingCommand =>
            _sortingCommand ??
            (_sortingCommand = new RelayCommand<object>(obj =>
            {
                //Console.WriteLine($"obj == null: {obj == null}");
                //RestartSavingTimer();
            }));

        private object _lockChanges = new object();

        //private object _lockColumns = new object();

        private Timer _savingTimer = new Timer(5_000)
        {
            AutoReset = false,
            Enabled = false,
        };

        public string SettingsPath { get; } = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\buh\\{Helper.AppName}\\Settings.json";

        public SourceCache<Change, int> ChangeCache { get; set; } = new SourceCache<Change, int>(x => x.Id);

        public ReadOnlyObservableCollection<Change> ChangeData => _changeData;

        private readonly ReadOnlyObservableCollection<Change> _changeData;

        private readonly IDisposable _cleanUp;

        public PageParameterData PageParameters { get; } = new PageParameterData(1, 100);

        public MainViewModel()
        {
            //avoid a "object reference not set to an instance of an object@ exception in XAML code while design time
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            var pager = PageParameters.WhenChanged(vm => vm.PageSize, vm => vm.CurrentPage, (_, size, pge) => new PageRequest(pge, size))
                .StartWith(new PageRequest(1, 25))
                .DistinctUntilChanged()
                .Sample(TimeSpan.FromMilliseconds(100));

            _cleanUp = ChangeCache.Connect()
                //.Filter(x => x.Id == 1)
                .Sort(SortExpressionComparer<Change>.Descending(t => t.Id), SortOptimisations.ComparesImmutableValuesOnly, 25)
                .Page(pager)
                .ObserveOnDispatcher()
                .Do(changes => PageParameters.Update(changes.Response))
                .Bind(out _changeData)
                .DisposeMany()
                .Subscribe();

            _logger.Info($"SettingsPath: {SettingsPath}");

            BindingOperations.CollectionRegistering += BindingOperationsOnCollectionRegistering;

            _savingTimer.Elapsed += (s, e) => { SaveSettings(); };

            LoadSettings();
        }

        private void BindingOperationsOnCollectionRegistering(object sender, CollectionRegisteringEventArgs e)
        {
            //if (Equals(e.Collection, Changes))
            //{
            //    _logger.Debug("CollectionRegistering Event: EnableCollectionSynchronization for Changes");
            //    BindingOperations.EnableCollectionSynchronization(Changes, _lockChanges);
            //}

            //else if (Equals(e.Collection, Columns))
            //{
            //    Console.WriteLine("CollectionRegistering Event: EnableCollectionSynchronization for Columns");
            //    BindingOperations.EnableCollectionSynchronization(Columns, _lockColumns);
            //}
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

            watcher.PropertyChanged += (s, e) => { RestartSavingTimer(); };

            Watchers.Add(watcher);
        }

        public void AddChange(int watcherId, ChangeType changeType, string fullPath, string oldFullPath = "")
        {
            //lock (_lockChanges)
            {
                //Changes.Add(new Change
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
        }

        public void RestartSavingTimer()
        {
            _logger.Info("Restart saving timer");
            _savingTimer.Stop();
            _savingTimer.Start();
        }

        public void SaveSettings()
        {
            _logger.Info("Settings saving...");
            var settings = new Settings();

            settings.Watchers = new List<ChangeWatcher>(Watchers);
            //settings.SortingChanges = CollectionViewSource.GetDefaultView(Changes).SortDescriptions;

            var settingsJson = JsonConvert.SerializeObject(settings, Formatting.Indented);

            var settingsFolder = new DirectoryInfo(SettingsPath).Parent;

            if (!settingsFolder.Exists)
            {
                settingsFolder.Create();
            }

            File.WriteAllText(SettingsPath, settingsJson, Encoding.UTF8);

            _logger.Info("Settings saved");
        }

        public void LoadSettings()
        {
            _logger.Info("Settings loading...");
            if (File.Exists(SettingsPath))
            {
                var settingsJson = File.ReadAllText(SettingsPath);
                var settings = JsonConvert.DeserializeObject<Settings>(settingsJson);

                if (settings.Watchers?.Any() ?? false)
                    foreach (var watcher in settings.Watchers)
                    {
                        AddWatcher(watcher);
                    }

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

            _logger.Info("Settings loaded");
        }

        public void Dispose()
        {
            _savingTimer?.Dispose();
            _cleanUp.Dispose();
        }
    }

    public class PageParameterData : AbstractNotifyPropertyChanged
    {
        private readonly RelayCommand<object> _nextPageCommand;
        private readonly RelayCommand<object> _previousPageCommand;
        private int _currentPage;
        private int _pageCount;
        private int _pageSize;
        private int _totalCount;

        public PageParameterData(int currentPage, int pageSize)
        {
            _currentPage = currentPage;
            _pageSize = pageSize;

            _nextPageCommand = new RelayCommand<object>(o => CurrentPage = CurrentPage + 1, o => CurrentPage < PageCount);
            _previousPageCommand = new RelayCommand<object>(o => CurrentPage = CurrentPage - 1, o => CurrentPage > 1);
        }

        public ICommand NextPageCommand => _nextPageCommand;

        public ICommand PreviousPageCommand => _previousPageCommand;

        public int TotalCount
        {
            get => _totalCount;
            private set => SetAndRaise(ref _totalCount, value);
        }

        public int PageCount
        {
            get => _pageCount;
            private set => SetAndRaise(ref _pageCount, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            private set => SetAndRaise(ref _currentPage, value);
        }


        public int PageSize
        {
            get => _pageSize;
            private set => SetAndRaise(ref _pageSize, value);
        }


        public void Update(IPageResponse response)
        {
            CurrentPage = response.Page;
            PageSize = response.PageSize;
            PageCount = response.Pages;
            TotalCount = response.TotalSize;
            _nextPageCommand.Refresh();
            _previousPageCommand.Refresh();
        }
    }
}