using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ViewModelLib;

namespace gDiamondViewer.ViewModel
{
    class MainWindowViewModel : ViewModelBase
    {
        public StatusViewModel StatusVM { get; set; }
        public CameraViewModel CameraVM { get; set; }
        public SpectrometerViewModel SpectrometerVM { get; set; }
        public MeasurementViewModel MeasurementVM { get; set; }

        public MainWindowViewModel()
        {
            base.DisplayName = "MainWindowViewModel";

            StatusVM = new StatusViewModel();
            StatusVM.Busy = 0;

            CameraVM = new CameraViewModel(StatusVM);
            SpectrometerVM = new SpectrometerViewModel(StatusVM);
            MeasurementVM = new MeasurementViewModel(StatusVM, CameraVM, SpectrometerVM);
            MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            CameraVM.setImageFilterUserControl(ref mainWindow.imageFilterUserControlInstance);
        }



        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            CameraVM.Dispose();
            SpectrometerVM.Dispose();
            MeasurementVM.Dispose();
            App.Current.Shutdown();

        }
    }
}
