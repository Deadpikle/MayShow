using MayShow.Helpers;
using MayShow.Interfaces;
using System.Collections.Generic;

namespace MayShow.ViewModels;

class MainViewModel : ChangeNotifier, IChangeViewModel
{
    BaseViewModel _currentViewModel;
    Stack<BaseViewModel> _viewModels;

    public MainViewModel(ITopLevelGrabber topLevelGrabber)
    {
        _viewModels = new Stack<BaseViewModel>();
        var initialViewModel = new StartNewChooseReportViewModel(this)
        {
            TopLevelGrabber = topLevelGrabber
        };
        _viewModels.Push(initialViewModel);
        _currentViewModel = initialViewModel;
    }

    public BaseViewModel CurrentViewModel
    {
        get { return _currentViewModel; }
        set { _currentViewModel = value; NotifyPropertyChanged(); }
    }

    #region IChangeViewModel

    public void PushViewModel(BaseViewModel model)
    {
        _viewModels.Push(model);
        CurrentViewModel = model;
    }

    public void PopViewModel()
    {
        if (_viewModels.Count > 1)
        {
            _viewModels.Pop();
            CurrentViewModel = _viewModels.Peek();
        }
    }

    #endregion
}
