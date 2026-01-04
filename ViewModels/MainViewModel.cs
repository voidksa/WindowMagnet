using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WindowMagnet.Models;
using WindowMagnet.Services;

namespace WindowMagnet.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly WindowManager _windowManager;
        private readonly MagnetService _magnetService;
        
        private WindowInfo _selectedParent;
        private WindowInfo _selectedChild;
        private ObservableCollection<WindowInfo> _windows;
        private string _statusMessage;

        public MainViewModel()
        {
            _windowManager = new WindowManager();
            _magnetService = new MagnetService();
            _windows = new ObservableCollection<WindowInfo>();
            
            RefreshCommand = new RelayCommand(_ => RefreshWindows());
            MagnetizeCommand = new RelayCommand(_ => Magnetize(), _ => CanMagnetize());
            ClearAllCommand = new RelayCommand(_ => ClearAll());
            
            RefreshWindows();
            StatusMessage = "Ready. Select a Parent (Master) and a Child (Slave) window.";
        }

        public ObservableCollection<WindowInfo> Windows
        {
            get => _windows;
            set
            {
                _windows = value;
                OnPropertyChanged();
            }
        }

        public WindowInfo SelectedParent
        {
            get => _selectedParent;
            set
            {
                _selectedParent = value;
                OnPropertyChanged();
            }
        }

        public WindowInfo SelectedChild
        {
            get => _selectedChild;
            set
            {
                _selectedChild = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand MagnetizeCommand { get; }
        public RelayCommand ClearAllCommand { get; }

        private void RefreshWindows()
        {
            var currentWindows = _windowManager.GetOpenWindows();
            Windows.Clear();
            foreach (var w in currentWindows)
            {
                Windows.Add(w);
            }
            StatusMessage = $"Found {Windows.Count} windows.";
        }

        private bool CanMagnetize()
        {
            return SelectedParent != null && SelectedChild != null && SelectedParent.Handle != SelectedChild.Handle;
        }

        private void Magnetize()
        {
            if (CanMagnetize())
            {
                try
                {
                    _magnetService.AddBond(SelectedParent.Handle, SelectedChild.Handle);
                    StatusMessage = $"Magnetized: {SelectedChild.Title} -> {SelectedParent.Title}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                }
            }
        }

        private void ClearAll()
        {
            _magnetService.ClearAll();
            StatusMessage = "All magnets cleared.";
        }

        public void Dispose()
        {
            _magnetService.Dispose();
        }
    }
}
