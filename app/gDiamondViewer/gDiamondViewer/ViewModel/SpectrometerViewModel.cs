using gDiamondViewer.Model;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ViewModelLib;

namespace gDiamondViewer.ViewModel
{
    class AvantesSpectrum
    {
        public ulong Id { get; set; }
        public ulong TimeStamp { get; set; }
        public Point[] Spectrum { get; set; }
    }

    class SpectrometerViewModel : ViewModelBase
    {
        StatusViewModel _statusVM;
        Stack<DateTime> _timestamps = new Stack<DateTime>();

        AvantesSpectrometer _avantes;
        AutoResetEvent _spectrumUpdated = new AutoResetEvent(false);
        AutoResetEvent _continuousMode = new AutoResetEvent(false);
        bool _specrometerConnected = false;
        double[] _wavelengths;
        double[] _counts;

        uint _lastSpectrumTimestamp;
        ulong _phosStartTimestamp;
        List<AvantesSpectrum> _phosSpectra = new List<AvantesSpectrum>();
        ManualResetEvent _phosMeasureCompletionEvent = new ManualResetEvent(false);

        public SpectrometerViewModel(StatusViewModel svm)
        {
            base.DisplayName = "SpectrometerViewModel";

            _statusVM = svm;
            _avantes = new AvantesSpectrometer();

            CommandStop = new RelayCommand(param => Stop());
            CommandStart = new RelayCommand(param => Start(-1));
            CommandSnapshot = new RelayCommand(param => Start(1));
            CommandResetSettings = new RelayCommand(param => ResetSettings());
            CommandSave = new RelayCommand(param => Save());

            IntegrationTime = Properties.Settings.Default.IntegrationTime;
            NumAverages = Properties.Settings.Default.NumAverages;

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += ConnectToSpectrometer;
            bw.RunWorkerCompleted += ConnectToSpectrometerCompleted;
            //bw.RunWorkerAsync();
        }

        public RelayCommand CommandStop { get; set; }
        public RelayCommand CommandStart { get; set; }
        public RelayCommand CommandSnapshot{ get; set; }
        public RelayCommand CommandResetSettings { get; set; }
        public RelayCommand CommandSave { get; set; }

        #region properties
        public IntPtr MainWindowHandleForAvantesCallback { get; set; }

        ObservableDataSource<Point> _spectrum;
        object _spectrumLock = new object();
        public ObservableDataSource<Point> Spectrum
        {
            get { return _spectrum; }
            set
            {
                _spectrum = value;
                OnPropertyChanged("Spectrum");
            }
        }

        double _integrationTime;
        public double IntegrationTime
        {
            get { return _integrationTime; }
            set
            {
                if (_integrationTime != value && value > 0)
                {
                    _integrationTime = value;
                    Properties.Settings.Default.IntegrationTime = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged("IntegrationTime");
                }
            }
        }

        uint _numAverages;
        public uint NumAverages
        {
            get { return _numAverages; }
            set
            {
                if (_numAverages != value && value >= 1 && value <= 10)
                {
                    _numAverages = value;
                    Properties.Settings.Default.NumAverages = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged("NumAverages");
                }
            }
        }

        bool _spectrometerBusy;
        public bool SpectrometerBusy
        {
            get { return _spectrometerBusy; }
            set
            {
                _spectrometerBusy = value;
                OnPropertyChanged("SpectrometerBusy");
            }
        }

        bool _ready;
        public bool Ready
        {
            get { return _ready; }
            set
            {
                _ready = value;
                OnPropertyChanged("Ready");
            }
        }

        #endregion

        #region default_settings

        readonly int DEFAULT_INTEGRATION_TIME = 200;
        readonly uint DEFAULT_NUM_AVGS = 1;

        void ResetSettings()
        {
            IntegrationTime = DEFAULT_INTEGRATION_TIME;
            NumAverages = DEFAULT_NUM_AVGS;
        }
        
        #endregion

        #region connect_spectrometer

        void ConnectToSpectrometer(object sender, DoWorkEventArgs e)
        {
            _statusVM.Busy++;
            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = "Trying to connect to spectrometer..." };
            _statusVM.Messages.Add(sts);
            _timestamps.Push(timestamp);

            e.Result = null;
            _wavelengths = new double[0];

            try
            {
                if (_avantes.Connect())
                {
                    if (_avantes.GetWavelengths(out _wavelengths))
                        e.Result = _avantes.Name;
                }
            }
            catch(Exception ex)
            {
                e.Result = null;
            }
        }

        void ConnectToSpectrometerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var ts = _timestamps.Pop();
            var sm = _statusVM.Messages.Where(s => s.Timestamp == ts).First();
            _statusVM.Messages.Remove(sm);
            _statusVM.Busy--;

            if (e.Result != null)
            {
                _statusVM.Messages.Add(new StatusMessage { Timestamp = DateTime.Now, Message = "Connected to " + e.Result.ToString() });
                _specrometerConnected = true;
                Ready = true;
                Start(-1);
            }
            else
            {
                var res = MessageBox.Show("Failed to connect to Spectrometer", "Spectrometer connection error, retry?", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += ConnectToSpectrometer;
                    bw.RunWorkerCompleted += ConnectToSpectrometerCompleted;
                    bw.RunWorkerAsync();
                }
                else
                {
                    var sts = new StatusMessage { Timestamp = DateTime.Now,
                        Message = "Spectrometer connect error!" };
                    _statusVM.Messages.Add(sts);
                }
            }
        }

        #endregion


        #region continuous_scan
        struct ScanParms
        {
            public short numScans;
            public uint delay;
        }
        bool Start(short numScans, uint delay = 0, bool externalTrigger = false)
        {
            try
            {
                if (_avantes.StartMeasurement(MainWindowHandleForAvantesCallback, IntegrationTime,
                            NumAverages, numScans, delay, externalTrigger))
                {
                    ScanParms sp;
                    sp.delay = delay;
                    sp.numScans = numScans;

                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += ScanDoWork;
                    bw.RunWorkerCompleted += ScanCompleted;
                    bw.RunWorkerAsync(sp);

                    return true;
                }
                else
                    throw new Exception("error");
            }
            catch(Exception ex)
            {

            }

            return false;
                
        }

        void ScanDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                SpectrometerBusy = true;
                e.Result = false;
                _statusVM.Busy++;
                
                var sp = (ScanParms)(e.Argument);
                
                Point[] _xyData = new Point[0];
                lock (_spectrumLock)
                {
                    Spectrum = new ObservableDataSource<Point>(_xyData);
                }

                _spectrumUpdated.Reset();
                WaitHandle[] events = new WaitHandle[] { _continuousMode, _spectrumUpdated };

                ulong id = 0;
                short numScans = sp.numScans;
                while (true)
                {
                    int eventIndex = WaitHandle.WaitAny(events, (int)(IntegrationTime * NumAverages) + (int)sp.delay + 10000);
                    if (eventIndex == 1)//got spectrum
                    {
                        bool isSaturated = false;
                        double[] spectrumData;

                        if (_avantes.GetSpectrum(out isSaturated, out spectrumData, out _lastSpectrumTimestamp))
                        {
                            _counts = spectrumData.ToArray();

                            _xyData = _wavelengths.Zip(_counts, (x, y) => (new Point(x, y))).ToArray();

                            lock (_spectrumLock)
                            {
                                Spectrum = new ObservableDataSource<Point>(_xyData.Take(2048));
                            }

                            if (numScans != -1)
                            {
                                _phosSpectra.Add(new AvantesSpectrum()
                                {
                                    Id = id++,
                                    TimeStamp = _lastSpectrumTimestamp * 10,
                                    Spectrum = _xyData.Take(2048).ToArray()
                                });
                                if (--numScans == 0)
                                {
                                    _phosMeasureCompletionEvent.Set();
                                    return;//all scans completed
                                }
                            }
                        }

                    }
                    else //external stop or timeout
                    {
                        _avantes.StopMeasurement();
                        return;
                    }

                }

                
            }
            finally
            {
                _statusVM.Busy--;
                SpectrometerBusy = false;
            }
        }

        void ScanCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            
        }
        #endregion


        #region phos_capture
        struct BWArgument { public Callback measurementEnd; public int waitTime; }

        public bool StartPhosphorescenceMeasurement(long delay, uint count,
            Callback whenMeasurementEnds, out string error)
        {
            bool result = false;
            error = "";

            try
            {
                if (!_specrometerConnected)
                    throw new Exception("Spectrometer not connected");

                Ready = false;

                //stop continuous scan
                if (SpectrometerBusy)
                {
                    Stop();
                    while (SpectrometerBusy)
                        Thread.Sleep(100);
                }

                _phosStartTimestamp = _avantes.GetTimestamp() * 10;

                //enable external trigger with count
                //enable delay
                //set phos count
                //start scan
                if (Start((short)count, (uint)delay, true))
                {
                    while (!SpectrometerBusy)
                        Thread.Sleep(100);
                }
                else
                    throw new Exception("Could not start");

                //start backgroundworker to wait for completion
                BWArgument arg;
                arg.measurementEnd = whenMeasurementEnds;
                arg.waitTime = (int)(5000 + count * (IntegrationTime * NumAverages + delay));


                //empty phos spectra buffer
                _phosSpectra.Clear();
                _phosMeasureCompletionEvent.Reset();

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += WaitForPhosCompletion;
                bw.RunWorkerCompleted += PhosMeasurementComplete;
                bw.RunWorkerAsync(arg);
                
                result = true;

            }
            catch (Exception ex)
            {
                error = ex.Message;
                Ready = true;
                if (SpectrometerBusy)
                {
                    Stop();
                    while (SpectrometerBusy)
                        Thread.Sleep(100);
                }
                Start(-1);
            }

            return result;
        }

        public bool AbortPhosMeasurement()
        {
            try
            {
                Ready = true;
                if (SpectrometerBusy)
                {
                    Stop();
                    while (SpectrometerBusy)
                        Thread.Sleep(100);
                }
                Start(-1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        void WaitForPhosCompletion(object sender, DoWorkEventArgs e)
        {
            try
            {
                var arg = (BWArgument)e.Argument;
                e.Result = arg.measurementEnd;
                bool got_signal = _phosMeasureCompletionEvent.WaitOne(arg.waitTime);
            }
            catch (Exception ex)
            {

            }
        }

        void PhosMeasurementComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            Callback measurementEnds = (Callback)e.Result;

            if (!Ready)
            {
                Ready = true;
                if (SpectrometerBusy)
                {
                    Stop();
                    while (SpectrometerBusy)
                        Thread.Sleep(100);
                }

                //restore normal trigger mode
                //start video
                Start(-1);

                measurementEnds(this, new SpectrometerPhosResults(_phosStartTimestamp, _phosSpectra));
            }
        }



        #endregion



        #region save
        void Save()
        {
            List<Point> saveData = null;
            lock (_spectrumLock)
            {
                if (Spectrum !=null && Spectrum.Collection.Count > 0)
                    saveData = Spectrum.Collection.ToList();
            }

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += SaveDoWork;
            bw.RunWorkerCompleted += SaveCompleted;
            bw.RunWorkerAsync(saveData);
        }

        void SaveDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = false;
            _statusVM.Busy++;
            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = "Saving spectrometer..." };
            _statusVM.Messages.Add(sts);
            _timestamps.Push(timestamp);

            try
            {
                List<Point> spect = (List<Point>)(e.Argument);

                if (spect == null || spect.Count == 0)
                    throw new Exception("Bad data");

                double[] spectrumData = spect.Select(p => p.Y).ToArray(); ;
                double[] wavelengthData = spect.Select(p => p.X).ToArray();               

                SaveFileDialog saveDlg = new SaveFileDialog();
                saveDlg.Filter = "SPC file (*.spc)|*.spc|CSV file (*.csv)|*.csv";
                if (saveDlg.ShowDialog() == true)
                {
                    if (Path.GetExtension(saveDlg.FileName).ToUpper().Contains("CSV"))
                    {
                        e.Result = SPCHelper.SaveToTXT(wavelengthData,
                            spectrumData,
                            saveDlg.FileName, "Wavelength (nm)", "Counts");
                    }
                    else
                    {
                        e.Result = SPCHelper.SaveToSPC(wavelengthData,
                           spectrumData,
                           saveDlg.FileName, "Wavelength (nm)", "Counts");
                    }
                }
            }
            catch
            {
                e.Result = false;
            }
            finally
            {

            }
        }


        void SaveCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var ts = _timestamps.Pop();
            var sm = _statusVM.Messages.Where(s => s.Timestamp == ts).First();
            _statusVM.Messages.Remove(sm);
            _statusVM.Busy--;

            string message = "Spectrum not saved";

            if ((bool)e.Result == true)
                message = "Spectrum saved";

            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = message };
            _statusVM.Messages.Add(sts);
            Task.Delay(2000).ContinueWith(t => RemoveSaveStatusMessage(timestamp));

        }

        void RemoveSaveStatusMessage(DateTime ts)
        {
            var sm = _statusVM.Messages.Where(s => s.Timestamp == ts).First();
            _statusVM.Messages.Remove(sm);
        }
        #endregion


        void Stop()
        {
            _continuousMode.Set();
        }

        public void UpdateSpectrum()
        {
            _spectrumUpdated.Set();
        }

        #region dispose

        private bool _disposed = false;

        void CloseSpectrometer()
        {
            if (SpectrometerBusy)
            {
                Stop();
                while (SpectrometerBusy)
                    Thread.Sleep(100);
            }
            _avantes.Disconnect();
        }

        protected override void OnDispose()
        {
            ViewModelDispose(true);
            GC.SuppressFinalize(this);
        }

        void ViewModelDispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CloseSpectrometer();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
