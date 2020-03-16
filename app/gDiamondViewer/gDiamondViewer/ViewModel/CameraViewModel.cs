using gDiamondViewer.Model;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ViewModelLib;
using ImageProcessorLib;
using gDiamondViewer.UserControls;

namespace gDiamondViewer.ViewModel
{
    enum ImageProcMode
    {
        None = 0,
        ContrastEnhancement,
        LevelAdjustment
    }

    enum MeasureMode
    {
        Led = 0,
        Fluorescence,
        Phosphorescence
    }

    class CameraViewModel : ViewModelBase
    {
        StatusViewModel _statusVM;
        Stack<DateTime> _timestamps = new Stack<DateTime>();

        PtGreyCamera _ptGreyCamera;
        ConcurrentQueue<PtGreyCameraImage> _ptGreyImageQueue = null;
        System.Threading.AutoResetEvent _ptGreyStopImageThreadEvent;
        bool _cameraConnected;
        uint _phosImagesToCollect;
        List<PtGreyCameraImage> _phosImages = new List<PtGreyCameraImage>();
        List<System.Drawing.Bitmap> _phosFilteredImages = new List<System.Drawing.Bitmap>();
        System.Threading.ManualResetEvent _phosMeasureCompletionEvent = new System.Threading.ManualResetEvent(false);
        ulong _phosStartTimestamp;

        BackgroundWorker bwCameraConnector = new BackgroundWorker();
        static readonly object _locker = new object();
        System.Drawing.Bitmap origBmp = null;
        System.Drawing.Bitmap postBmp = null;

        Dictionary<string, string> settings = new Dictionary<string, string>();
        ImageFilterUserControl imgFilterUserControl = null;
        static EventWaitHandle _waitHandle = new AutoResetEvent(false);

        long originalWidth = -1;
        long originalHeight = -1;

        MeasureMode _mMode = MeasureMode.Led;
        public MeasureMode MMode
        {
            get
            {
                return _mMode;
            }
            set
            {
                if(_mMode == value)
                {
                    return;
                }
                _mMode = value;
                OnPropertyChanged("MMode");
                updateSettings("MeasureMode", value.ToString());
            }
        }

        public CameraViewModel(StatusViewModel svm)
        {
            base.DisplayName = "CameraViewModel";
            _statusVM = svm;

            CommandResetSettings = new RelayCommand(param => ResetSettings());
            CommandSave = new RelayCommand(param => Save());

            _cameraConnected = false;

            _phosImagesToCollect = 0;
            bwCameraConnector.DoWork += ConnectCameraDoWork;
            bwCameraConnector.RunWorkerCompleted += ConnectCameraCompleted;
            bwCameraConnector.RunWorkerAsync();
        }

        public RelayCommand CommandResetSettings { get; set; }
        public RelayCommand CommandSave { get; set; }

        #region Properties

        WriteableBitmap _cameraImage;
        public WriteableBitmap CameraImage
        {
            get
            {
                return _cameraImage;
            }
            set
            {
                _cameraImage = value;
                OnPropertyChanged("CameraImage");
            }
           
        }

        long _imageWidth, _imageHeight;
        public long ImageWidth
        {
            get { return _imageWidth; }
            set
            {
                _imageWidth = value;
                OnPropertyChanged("ImageWidth");
                updateSettings("ImageWidth", value.ToString());
            }
        }
        public long ImageHeight
        {
            get { return _imageHeight; }
            set
            {
                _imageHeight = value;
                OnPropertyChanged("ImageHeight");
                updateSettings("ImageHeight", value.ToString());
            }
        }

        double _shutter;
        public double Shutter
        {
            get
            {
                return _shutter;
            }
            set
            {
                if (_ptGreyCamera.SetShutterTime(value * 1000))
                {
                    _shutter = value;
                    //Properties.Settings.Default.ShutterTime = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged("Shutter");
                    updateSettings("Shutter", value.ToString());
                }
            }
        }

        double _gain;
        public double Gain
        {
            get { return _gain; }
            set
            {
                if (_ptGreyCamera.SetGain(value))
                {
                    _gain = value;
                    Properties.Settings.Default.Gain = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged("Gain");
                    updateSettings("Gain", value.ToString());
                }
            }
        }

        double _frameRate;
        public double FrameRate
        {
            get { return _frameRate; }
            set
            {
                if (_ptGreyCamera.SetFrameRate(value))
                {
                    _frameRate = value;
                    OnPropertyChanged("FrameRate");
                }
            }
        }

        double _wbRed;
        public double WBred
        {
            get { return _wbRed; }
            set
            {
                if (_ptGreyCamera.SetWhiteBalanceRed(value))
                {
                    _wbRed = value;
                    Properties.Settings.Default.WBRed = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged("WBred");
                    updateSettings("WBred", value.ToString());
                }
            }
        }

        double _wbBlue;
        public double WBblue
        {
            get { return _wbBlue; }
            set
            {
                if (_ptGreyCamera.SetWhiteBalanceBlue(value))
                {
                    _wbBlue = value;
                    Properties.Settings.Default.WBBlue = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged("WBblue");
                    updateSettings("WBblue", value.ToString());
                }
            }
        }

        int _videoMode;
        public int VideoMode
        {
            get { return _videoMode; }
            set
            {
                try
                {
                    if (_cameraConnected)
                    {
                        if (value == _videoMode)
                            return;
                    }
                    _ptGreyCamera.StopVideo();
                    if (_ptGreyCamera.SetVideoMode(value))
                    {
                        _videoMode = value;
                        OnPropertyChanged("VideoMode");
                    }
                    if (_cameraConnected)
                    {
                        if (_videoMode == 1)
                            Shutter /= 4;
                        else
                            Shutter *= 4;
                    }
                    if (_cameraConnected)
                    {
                        //Properties.Settings.Default.VideoMode = value;
                        Properties.Settings.Default.Save();
                    }
                    updateSettings("VideoMode", value.ToString());

                    _ptGreyCamera.StartVideo();
                }
                catch
                {

                }
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

        double _phosDelay = Properties.Settings.Default.PhosCamDelay;
        public double PhosDelay
        {
            get
            {
                return _phosDelay;
            }
            set
            {
                if(_phosDelay == value)
                {
                    return;
                }
                _phosDelay = value;
                Properties.Settings.Default.PhosCamDelay = value;
                Properties.Settings.Default.Save();
                OnPropertyChanged("PhosDelay");
                updateSettings("PhosDelay", value.ToString());
            }
        }

        #endregion

        #region default_settings

        readonly double DEFAULT_SHUTTER_TIME = 300;
        readonly double DEFAULT_GAIN = 1;
        readonly double DEFAULT_WB_RED = 1.11;
        readonly double DEFAULT_WB_BLUE = 2.24;

        void ResetSettings()
        {
            if (_ptGreyCamera.DefaultSettings())
            {
                Gain = DEFAULT_GAIN;
                Shutter = DEFAULT_SHUTTER_TIME;
                WBred = DEFAULT_WB_RED;
                WBblue = DEFAULT_WB_BLUE;
                VideoMode = 0;//default
                OnPropertyChanged("VideoMode");
                _ptGreyCamera.StartVideo();
            }
            else
            {
                MessageBox.Show("Failed to restore camera settings", "Camera Error");
            }
        }

        #endregion

        #region connect_camera
        void ConnectCameraDoWork(object sender, DoWorkEventArgs e)
        {
            _statusVM.Busy++;
            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = "Trying to connect to camera..." };
            _statusVM.Messages.Add(sts);
            _timestamps.Push(timestamp);

            _ptGreyImageQueue = new ConcurrentQueue<PtGreyCameraImage>();
            _ptGreyCamera = new PtGreyCamera();
            long width, height;
            string message;
            if (_ptGreyCamera.Open(_ptGreyImageQueue, out message, out width, out height))
            {
                _ptGreyStopImageThreadEvent = new System.Threading.AutoResetEvent(false);

                if (_ptGreyCamera.DefaultSettings())
                {
                    Properties.Settings.Default.VideoMode = 0;
                    Properties.Settings.Default.Save();
                    Gain = Properties.Settings.Default.Gain;
                    Shutter = Properties.Settings.Default.ShutterTimePhos;
                    WBred = Properties.Settings.Default.WBRed;
                    WBblue = Properties.Settings.Default.WBBlue;
                    VideoMode = Properties.Settings.Default.VideoMode;
                    _ptGreyCamera.StartVideo();

                    ImageHeight = height;
                    ImageWidth = width;
                }
                else
                    message = "Error: Could not load default camera settings";

                e.Result = message;
            }
            else
            {
                e.Result = "Error: " + message;
            }

        }

        void ConnectCameraCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var ts = _timestamps.Pop();
            var sm = _statusVM.Messages.Where(s => s.Timestamp == ts).First();
            _statusVM.Messages.Remove(sm);
            _statusVM.Busy--;

            if (e.Result.ToString().Substring(0, 6) != "Error:")
            {
                Ready = true;
                //changing this property to a new reference now
                _cameraImage  = new System.Windows.Media.Imaging.WriteableBitmap((int)ImageWidth, (int)ImageHeight,
                           96,
                           96, System.Windows.Media.PixelFormats.Bgr24, null);
                //since this is happening after the original reference was bound to the UI
                //we have to notify UI of new reference
                CameraImage = _cameraImage;

                _statusVM.Messages.Add(new StatusMessage { Timestamp = DateTime.Now, Message = "Connected to " + e.Result.ToString() });
                _cameraConnected = true;

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += PtGreyImageReceiveDoWork;
                bw.WorkerReportsProgress = true;
                bw.ProgressChanged += PtGreyImageReceiveProgressChangedEvent;
                bw.RunWorkerAsync();

            }
            else
            {
                var res = MessageBox.Show(e.Result.ToString(), "Camera connection error, retry?" , MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    bwCameraConnector.RunWorkerAsync();
                }
                else
                {
                    var sts = new StatusMessage { Timestamp = DateTime.Now, Message = "Camera connect error!" };
                    _statusVM.Messages.Add(sts);
                }
            }
            
            
        }
        #endregion

        public void SetPhosDelay(double d)
        {
            PhosDelay = d;
        }

        public void SetMeasureMode(MeasureMode mm)
        {
            MMode = mm;
        }

        public MeasureMode GetMeasureMode()
        {
            return MMode;
        }

        public void setImageFilterUserControl(ref ImageFilterUserControl userControl)
        {
            imgFilterUserControl = userControl;
            imgFilterUserControl.PropertyChanged += OnImageFilterPropertyChanged;
            initialSettings();
        }

        private void initialSettings()
        {
            // camera settings
            settings.Add("MeasureMode", MMode.ToString());
            settings.Add("ImageWidth", ImageWidth.ToString());
            settings.Add("ImageHeight", ImageHeight.ToString());
            settings.Add("Shutter", Shutter.ToString());
            settings.Add("Gain", Gain.ToString());
            settings.Add("WBred", WBred.ToString());
            settings.Add("WBblue", WBblue.ToString());
            settings.Add("VideoMode", VideoMode.ToString());
            settings.Add("PhosDelay", PhosDelay.ToString());
            // image filter settings;
            settings.Add("FilterMode", Enum.GetName(typeof(ImageProcMode), imgFilterUserControl.FilterMode));
            settings.Add("sBrightness", imgFilterUserControl.Brightness.ToString());
            settings.Add("sContrast", imgFilterUserControl.Contrast.ToString());
            settings.Add("sGMin", imgFilterUserControl.GMin.ToString());
            settings.Add("sGMax", imgFilterUserControl.GMax.ToString());
            settings.Add("sFMin", imgFilterUserControl.FMin.ToString());
            settings.Add("sFMax", imgFilterUserControl.FMax.ToString());
            settings.Add("sGamma", imgFilterUserControl.Gamma.ToString());
            settings.Add("FileName", "");
            settings.Add("Captured", "");
        }

        private void OnImageFilterPropertyChanged(object sender, ParameterChangedEventArgs e)
        {
            updateSettings(e.senderName, e.Value);
        }

        private void updateSettings(string key, string value)
        {
            if(settings.ContainsKey(key))
            {
                settings[key] = value;
            } else
            {
                Console.WriteLine("Error: " + key + " does not exist in the dictionary!");
            }
            
        }

        #region camera image callback


        void PtGreyImageReceiveDoWork(object sender, DoWorkEventArgs e)
        {
            PtGreyCameraImage item;
            bool isMeasuring = false;
            while (!_ptGreyStopImageThreadEvent.WaitOne(1))
            {
                if (_ptGreyImageQueue.TryPeek(out item))
                {
                    int filterMode = imgFilterUserControl.FilterMode;

                    if (_ptGreyImageQueue.TryDequeue(out item))
                    {
                        if (_phosImagesToCollect > 0)
                        {
                            isMeasuring = true;
                            //_ptGreyCamera.TriggerDelay(false, 0);
                            _phosImages.Add(
                                new PtGreyCameraImage
                                {
                                    FrameId = item.FrameId,
                                    TimeStamp = item.TimeStamp,
                                    Image = new System.Drawing.Bitmap(item.Image)
                                }
                                );

                            if (--_phosImagesToCollect == 0)
                                _phosMeasureCompletionEvent.Set();
                        } else
                        {
                            isMeasuring = false;
                        }

                        System.Drawing.Bitmap bmp = (System.Drawing.Bitmap)item.Image;
                        //System.Drawing.Imaging.BitmapData data = null;
                        // testing image:
                        //string currentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                        //var tempFileNamePath = Path.Combine(currentDirectory, "110203990144-dv3_1.bmp");
                        //System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(tempFileNamePath);
                        try
                        {
                            lock (_locker)
                            {
                                if (origBmp != null)
                                {
                                    origBmp.Dispose();
                                }

                                origBmp = (System.Drawing.Bitmap)bmp.Clone();
                                if(originalWidth == -1)
                                {
                                    originalWidth = origBmp.Width;
                                    originalHeight = origBmp.Height;
                                }

                                // todo: image processing filter
                                if ((ImageProcMode)filterMode == ImageProcMode.ContrastEnhancement)
                                {
                                    ImageProcessing.ContrastEnhancement(ref bmp, out System.Drawing.Bitmap dst, imgFilterUserControl.Brightness, imgFilterUserControl.Contrast);

                                    bmp.Dispose();
                                    bmp = dst;
                                    if (postBmp != null)
                                    {
                                        postBmp.Dispose();
                                    }
                                    postBmp = (System.Drawing.Bitmap)bmp.Clone();
                                }
                                else if ((ImageProcMode)filterMode == ImageProcMode.LevelAdjustment)
                                {
                                    ImageProcessing.LevelAdjustment(ref bmp, out System.Drawing.Bitmap dst, imgFilterUserControl.GMin, imgFilterUserControl.GMax,
                                        imgFilterUserControl.Gamma, imgFilterUserControl.FMin, imgFilterUserControl.FMax);

                                    bmp.Dispose();
                                    bmp = dst;
                                    if (postBmp != null)
                                    {
                                        postBmp.Dispose();
                                    }
                                    postBmp = (System.Drawing.Bitmap)bmp.Clone();
                                } else if((ImageProcMode)filterMode == ImageProcMode.None)
                                {
                                    if (postBmp != null)
                                    {
                                        postBmp.Dispose();
                                        postBmp = null;
                                    }
                                }
                                if(isMeasuring && (ImageProcMode)filterMode != ImageProcMode.None)
                                {
                                    _phosFilteredImages.Add(new System.Drawing.Bitmap(postBmp));
                                }else
                                {

                                }
                            }
                        }
                        finally
                        {
                            bmp.Dispose();
                            GC.Collect();
                        }

                    }

                    ((BackgroundWorker)sender).ReportProgress(filterMode);
                }
            }
        }

        void PtGreyImageReceiveProgressChangedEvent(object sender, ProgressChangedEventArgs e)
        {
            System.Drawing.Imaging.BitmapData data = null;
            try
            {
                lock (_locker)
                {
                    if (origBmp != null)
                    {
                        if (_cameraImage.Width != origBmp.Width)
                        {
                            ImageWidth = origBmp.Width;
                            ImageHeight = origBmp.Height;
                            _cameraImage = new System.Windows.Media.Imaging.WriteableBitmap((int)ImageWidth, (int)ImageHeight,
                               96,
                               96, System.Windows.Media.PixelFormats.Bgr24, null);
                            //since this is happening after the original reference was bound to the UI
                            //we have to notify UI of new reference
                            CameraImage = _cameraImage;
                        }

                        
                        if ((ImageProcMode)e.ProgressPercentage == ImageProcMode.ContrastEnhancement)
                        {
                            data = postBmp.LockBits(new System.Drawing.Rectangle(0, 0, (int)_cameraImage.Width, (int)_cameraImage.Height),
                                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                        }
                        else if ((ImageProcMode)e.ProgressPercentage == ImageProcMode.LevelAdjustment)
                        {
                            data = postBmp.LockBits(new System.Drawing.Rectangle(0, 0, (int)_cameraImage.Width, (int)_cameraImage.Height),
                                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                        }
                        else
                        {
                            data = origBmp.LockBits(new System.Drawing.Rectangle(0, 0, (int)_cameraImage.Width, (int)_cameraImage.Height),
                                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                        }
                        _cameraImage.WritePixels(new System.Windows.Int32Rect(0, 0, (int)_cameraImage.Width, (int)_cameraImage.Height),
                                            data.Scan0, (int)(_cameraImage.Width * 3 * _cameraImage.Height), data.Stride);
                    }
                }
            }
            finally
            {
                if (data != null)
                {
                    if ((ImageProcMode)e.ProgressPercentage == ImageProcMode.None)
                    {
                        origBmp.UnlockBits(data);
                    }
                    else
                    {
                        if(postBmp != null)
                            postBmp.UnlockBits(data);
                    }
                }
            }
        }

            #endregion

        public bool SetShutterTime(double timeMs)
        {
            return _ptGreyCamera.SetShutterTime(timeMs * 1000);
        }

        public bool SetGain(double g)
        {
            return _ptGreyCamera.SetGain(g);
        }

        public double GetGain()
        {
            return _ptGreyCamera.GetGain();
        }

        #region phos_capture
        struct BWArgument { public Callback measurementEnd; public int waitTime; }

        public bool StartPhosphorescenceMeasurement(double delay, uint count, 
            Callback whenMeasurementEnds, out string error)
        {
            bool result = false;
            error = "";
            
            try
            {
                if (!_cameraConnected)
                    throw new Exception("Camera not connected");

                Ready = false;

                //stop streaming camera
                _ptGreyCamera.StopVideo();

                //increase frame buffer count
                if (!_ptGreyCamera.SetStreamBufferCount(100))
                    throw new Exception("Could not set frame buffer count");

                //do this before we change trigger mode
                _phosStartTimestamp = _ptGreyCamera.GetImageTimeStamp();

                //empty phos image buffer
                _phosImages.Clear();
                _phosFilteredImages.Clear();

                //enable external trigger with count
                if (!_ptGreyCamera.PhosTriggerMode(true, count))
                    throw new Exception("Could not set trigger mode");
                //enable delay
                if (!_ptGreyCamera.TriggerDelay(true, delay))
                    throw new Exception("Could not set delay");

                //set phos count
                _phosImagesToCollect = count;

                //start video
                _ptGreyCamera.StartVideo(false);

                //start backgroundworker to wait for completion
                BWArgument arg;
                arg.measurementEnd = whenMeasurementEnds;
                arg.waitTime = (int)(10000 + (count * Shutter + delay));


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
                _ptGreyCamera.StopVideo();
                _phosImagesToCollect = 0;
                _phosMeasureCompletionEvent.Set();
                _ptGreyCamera.TriggerDelay(false, 0);
                _ptGreyCamera.PhosTriggerMode(false, 0);
                _ptGreyCamera.StartVideo();
            }

            return result;
        }

        public bool AbortPhosMeasurement()
        {
            try
            {
                Ready = true;
                _ptGreyCamera.StopVideo();
                _phosImagesToCollect = 0;
                _phosMeasureCompletionEvent.Set();
                _ptGreyCamera.TriggerDelay(false, 0);
                _ptGreyCamera.PhosTriggerMode(false, 0);
                _ptGreyCamera.SetStreamBufferCount(1);
                _ptGreyCamera.StartVideo();
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
                bool signaled = _phosMeasureCompletionEvent.WaitOne(arg.waitTime);
                if (!signaled)
                {
                    Console.WriteLine("Phos measurement timeout!!!");
                }
            }
            catch(Exception ex)
            {
                
            }
        }

        void PhosMeasurementComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            Callback measurementEnds = (Callback)e.Result;
            if (!Ready)//not aborted
            {
                Ready = true;
                _phosMeasureCompletionEvent.Reset();

                //stop video
                _ptGreyCamera.StopVideo();
                _phosImagesToCollect = 0;

                //restore normal trigger mode
                _ptGreyCamera.TriggerDelay(false, 0);
                _ptGreyCamera.PhosTriggerMode(false, 0);

                //rest frame buffer count 
                _ptGreyCamera.SetStreamBufferCount(1);

                //start video
                _ptGreyCamera.StartVideo();

                measurementEnds(this, new CamPhosResults(_phosStartTimestamp, _phosImages, _phosFilteredImages));
            }
        }

        
        
        #endregion

        #region save
        private System.Drawing.Bitmap BitmapFromWriteableBitmap(WriteableBitmap writeBmp)
        {
            System.Drawing.Bitmap bmp;
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create((BitmapSource)writeBmp));
                enc.Save(outStream);
                bmp = new System.Drawing.Bitmap(outStream);
            }
            return bmp;
        }

        void Save()
        {
            // check if video mode match image size
            long curWidth;
            lock (_locker)
            {
                curWidth = origBmp.Width;
            }
            bool misMatch = false;
            if(Math.Abs(curWidth - originalWidth) < 50)
            {
                if(settings["VideoMode"] != "0")
                {
                    misMatch = true;
                }
            } else
            {
                if(settings["VideoMode"] == "0")
                {
                    misMatch = true;
                }
            }
            if (misMatch)
            {
                MessageBox.Show("Video mode setting is not taken into effect, please wait and try Save again.", "Error");
                return;
            }
            // do saving work
            SaveSettings();
            System.Drawing.Bitmap img;
            System.Drawing.Bitmap imgPost;
            List<System.Drawing.Bitmap> imgList = new List<System.Drawing.Bitmap>();
            lock (_locker)
            {
                img = (System.Drawing.Bitmap)origBmp.Clone();
                imgList.Add(img);
                if(postBmp != null)
                {
                    imgPost = (System.Drawing.Bitmap)postBmp.Clone();
                    imgList.Add(imgPost);
                }
            }
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += SaveDoWork;
            bw.RunWorkerCompleted += SaveCompleted;
            bw.RunWorkerAsync(imgList);
        }

        void SaveDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = false;
            _statusVM.Busy++;
            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = "Saving image..." };
            _statusVM.Messages.Add(sts);
            _timestamps.Push(timestamp);

            try
            {
                SaveFileDialog saveDlg = new SaveFileDialog();
                string directory = @"C:\DiamondView";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                saveDlg.InitialDirectory = directory;
                saveDlg.Filter = "JPG file (*.jpg)|*.jpg|BMP file (*.bmp)|*.bmp";
                if (saveDlg.ShowDialog() == true)
                {
                    string folder = ((int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString();
                    directory = Path.Combine(directory, folder);
                    Directory.CreateDirectory(directory);

                    List<System.Drawing.Bitmap> imgList = (List<System.Drawing.Bitmap>)(e.Argument);
                    string filename_without_ext = Path.GetFileNameWithoutExtension(saveDlg.FileName);
                    settings["FileName"] = directory + "\\" + filename_without_ext;
                    settings["Captured"] = DateTime.Now.ToString();
                    _waitHandle.Set();
                    for (int i = 0; i < imgList.Count; i++)
                    {
                        using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(imgList[i]))
                        {
                            if (i == 1)
                            {
                                filename_without_ext = filename_without_ext + "_filter";
                            }
                            bmp.Save(directory + "\\" + filename_without_ext + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                            bmp.Save(directory + "\\" + filename_without_ext + ".bmp");
                        }
                    }
                    e.Result = true;
                }
            }
            catch (Exception ex)
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

            string message = "Image not saved";

            if ((bool)e.Result == true)
                message = "Image saved";

            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = message };
            _statusVM.Messages.Add(sts);
            Task.Delay(2000).ContinueWith(t => RemoveSaveStatusMessage(timestamp));

        }

        public void SaveSettings()
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += SaveSettingsDoWork;
            bw.RunWorkerCompleted += SaveSettingsCompleted;
            bw.RunWorkerAsync(settings);
        }

        public void ContinueSaveSettings()
        {
            _waitHandle.Set();
        }

        public void SetSettingFilePath(string s)
        {
            updateSettings("FileName", s);
        }

        public void SetSettingCaptureTime(string s)
        {
            updateSettings("Captured", s);
        }

        void SaveSettingsDoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = false;
            _statusVM.Busy++;
            DateTime timestamp = DateTime.Now;
            var sts = new StatusMessage { Timestamp = timestamp, Message = "Saving settings..." };
            _statusVM.Messages.Add(sts);
            _timestamps.Push(timestamp);

            try
            {
                _waitHandle.WaitOne();
                Dictionary<string, string> dic = (Dictionary<string, string>)e.Argument;
                using (StreamWriter file = new StreamWriter(dic["FileName"] + ".txt"))
                {
                    string mm = dic.ElementAt(0).Value;
                    for(int i = 0; i < dic.Count; i++)
                    {
                        var entry = dic.ElementAt(i);
                        if (entry.Key == "FilterMode")
                        {
                            file.WriteLine("[{0} {1}]", entry.Key, entry.Value);
                            if (entry.Value == "None")
                            {

                            }
                            else if (entry.Value == "ContrastEnhancement")
                            {
                                entry = dic.ElementAt(i + 1);
                                file.WriteLine("[{0} {1}]", entry.Key, entry.Value);

                                entry = dic.ElementAt(i + 2);
                                file.WriteLine("[{0} {1}]", entry.Key, entry.Value);
                            }
                            else if (entry.Value == "LevelAdjustment")
                            {
                                for (int j = i + 3; j < i + 8; j++)
                                {
                                    entry = dic.ElementAt(j);
                                    file.WriteLine("[{0} {1}]", entry.Key, entry.Value);
                                }
                            }

                            i += 7;
                        }
                        else if(entry.Key == "PhosDelay")
                        {
                            if(mm == MeasureMode.Phosphorescence.ToString())
                            {
                                file.WriteLine("[{0} {1}]", entry.Key, entry.Value);
                            }
                        }
                        else
                        {
                            file.WriteLine("[{0} {1}]", entry.Key, entry.Value);
                        }
                    }
                }
                e.Result = true;
            }
            catch (Exception ex)
            {
                e.Result = false;
            }
        }

        void SaveSettingsCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var ts = _timestamps.Pop();
            var sm = _statusVM.Messages.Where(s => s.Timestamp == ts).First();
            _statusVM.Messages.Remove(sm);
            _statusVM.Busy--;

            string message = "Settings not saved";

            if ((bool)e.Result == true)
                message = "Settings saved";

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

        #region dispose

        private bool _disposed = false;

        void CloseCamera()
        {
            if (_ptGreyCamera != null)
                _ptGreyCamera.Close();

            if (_ptGreyStopImageThreadEvent != null)
                _ptGreyStopImageThreadEvent.Set();
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
                    CloseCamera();
                }
                _disposed = true;
            }
        }

        #endregion

    }
}
