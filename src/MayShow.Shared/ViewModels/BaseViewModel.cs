using MayShow.Helpers;
using MayShow.Interfaces;

namespace MayShow.ViewModels;

class BaseViewModel : ChangeNotifier
{
    IChangeViewModel _viewModelChanger;
    ITopLevelGrabber? _topLevelGrabber;

    public BaseViewModel(IChangeViewModel viewModelChanger): base()
    {
        _viewModelChanger = viewModelChanger;
        _topLevelGrabber = null;
    }

    public ITopLevelGrabber? TopLevelGrabber
    {
        get => _topLevelGrabber;
        set { _topLevelGrabber = value; }
    }

    public IChangeViewModel ViewModelChanger
    {
        get { return _viewModelChanger; }
        set { _viewModelChanger = value; }
    }

    #region IChangeViewModel

    public void PopViewModel()
    {
        _viewModelChanger?.PopViewModel();
    }

    public void PushViewModel(BaseViewModel model)
    {
        _viewModelChanger?.PushViewModel(model);
    }

    #endregion
}
