using System.Windows.Input;
using DynamicData.Binding;
using DynamicData.Operators;
using Tools;

namespace Watcher
{
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

            _nextPageCommand =
                new RelayCommand<object>(o => CurrentPage = CurrentPage + 1, o => CurrentPage < PageCount);
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