using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Newtonsoft.Json;
using Watcher.Annotations;

namespace Watcher
{
    public class ChangeWatcher : INotifyPropertyChanged, IDisposable, IDataErrorInfo
    {
        private readonly FileSystemWatcher _watcher = new FileSystemWatcher();

        private int _id;

        [JsonIgnore]
        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        private string _path;

        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                if (!ValidatePath(value)) return;
                _watcher.Path = value;
                OnPropertyChanged();
            }
        }

        private string _filter;

        public string Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                _watcher.Filter = value;
                OnPropertyChanged();
            }
        }

        private bool _includeSubdirectories;

        public bool IncludeSubdirectories
        {
            get => _includeSubdirectories;
            set
            {
                _includeSubdirectories = value;
                _watcher.IncludeSubdirectories = value;
                OnPropertyChanged();
            }
        }

        private bool _enableRaisingEvents;

        public bool EnableRaisingEvents
        {
            get => _enableRaisingEvents;
            set
            {
                _enableRaisingEvents = value;
                _watcher.EnableRaisingEvents = value;
                OnPropertyChanged();
            }
        }

        string IDataErrorInfo.Error { get; }

        public string this[string columnName]
        {
            get
            {
                var error = string.Empty;
                switch (columnName)
                {
                    case nameof(Path):
                        if (!ValidatePath(Path)) error = "Invalid path";
                        break;
                }

                return error;
            }
        }

        private RelayCommand _deleteCommand;

        [JsonIgnore]
        public RelayCommand DeleteCommand =>
            _deleteCommand ??
            (_deleteCommand = new RelayCommand(obj =>
            {
                OnWatcherDeleted?.Invoke(Id);
                Dispose();
            }));

        public ChangeWatcher(string path = "", string filter = "")
        {
            Path = path;
            Filter = filter;

            _watcher.Created += (s, e) => Created?.Invoke(Id, e);
            _watcher.Changed += (s, e) => Changed?.Invoke(Id, e);
            _watcher.Renamed += (s, e) => Renamed?.Invoke(Id, e);
            _watcher.Deleted += (s, e) => Deleted?.Invoke(Id, e);
            _watcher.Error += (s, e) => Error?.Invoke(Id, e);
        }

        private bool ValidatePath(string path)
        {
            return Directory.Exists(path);
        }

        public delegate void CreatedEventHandler(int watcherId, FileSystemEventArgs args);

        public event CreatedEventHandler Created;


        public delegate void ChangedEventHandler(int watcherId, FileSystemEventArgs args);

        public event ChangedEventHandler Changed;


        public delegate void RenamedEventHandler(int watcherId, RenamedEventArgs args);

        public event RenamedEventHandler Renamed;


        public delegate void DeletedEventHandler(int watcherId, FileSystemEventArgs args);

        public event DeletedEventHandler Deleted;


        public delegate void ErrorEventHandler(int watcherId, ErrorEventArgs args);

        public event ErrorEventHandler Error;


        public delegate void WatcherDeletedEventHandler(int id);

        public event WatcherDeletedEventHandler OnWatcherDeleted;


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            CommandManager.InvalidateRequerySuggested();
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}