using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace gDiamondViewer.Model
{
    enum TriggerType
    {
        Software,
        Hardware,
        Unknown
    };

    enum TriggerMode
    {
        Off,
        On
    };

    class PtGreyCameraImage
    {
        public ulong FrameId { get; set; }
        public ulong TimeStamp { get; set; }
        public System.Drawing.Bitmap Image { get; set; }
    }

    class PtGreyCamera
    {
        ManagedSystem system = null;
        IList<IManagedCamera> camList = null;
        IManagedCamera managedCamera = null;
        FlyCapture2Managed.ManagedCamera flycapManagedCamera = null;
        INodeMap nodeMap = null;
        ImageEventListener imageEventListener = null;
        ConcurrentQueue<PtGreyCameraImage> imageQueue = null;
        bool _videoMode = false;

        class ImageEventListener : ManagedImageEvent
        {
            ConcurrentQueue<PtGreyCameraImage> _imageQueue = null;

            public ImageEventListener(ConcurrentQueue<PtGreyCameraImage> imageQueue)
            {
                _imageQueue = imageQueue;
            }


            override protected void OnImageEvent(ManagedImage image)
            {
                //Debug.WriteLine("OnImageEvent");

                try
                {
                    if (!image.IsIncomplete)
                    {
                        // Convert image
                        using (var temp = image.Convert(PixelFormatEnums.BGR8))
                        {
                            if (_imageQueue.Count <= 10)
                            {
                                _imageQueue.Enqueue(
                                    new PtGreyCameraImage
                                    {
                                        FrameId = image.FrameID,
                                        TimeStamp = image.TimeStamp,
                                        Image = new System.Drawing.Bitmap(temp.bitmap)
                                    }
                                    );
                            }
                            else
                                Debug.WriteLine("Dropped frame");
                        }

                    }
                    image.Release();
                }
                catch (SpinnakerException ex)
                {
                    Debug.WriteLine("Error: {0}", ex.Message);
                }
                catch (Exception ex1)
                {
                    Debug.WriteLine("Error: {0}", ex1.Message);
                }
                finally
                {
                    // Must manually release the image to prevent buffers on the camera stream from filling up
                    //image.Release();
                }
            }


        }

        public bool Open(ConcurrentQueue<PtGreyCameraImage> imageQ, out string message, out long width, out long height)
        {
            bool result = false;
            message = "";
            width = height = 0;

            system = new ManagedSystem();

            // Retrieve list of cameras from the system
            camList = system.GetCameras();

            // Finish if there are no cameras
            if (camList.Count != 1)
            {
                foreach (IManagedCamera mc in camList)
                    mc.Dispose();

                // Clear camera list before releasing system
                camList.Clear();

                // Release system
                system.Dispose();
                message = "Camera count is " + camList.Count;
            }
            else
            {
                try
                {
                    #region FlyCaptureAPI
                    FlyCapture2Managed.ManagedBusManager busMgr = new FlyCapture2Managed.ManagedBusManager();
                    var guid = busMgr.GetCameraFromIndex(0);
                    flycapManagedCamera = new FlyCapture2Managed.ManagedCamera();
                    flycapManagedCamera.Connect(guid);
                    #endregion

                    managedCamera = camList[0];

                    if (managedCamera.TLDevice.DeviceDisplayName != null && managedCamera.TLDevice.DeviceDisplayName.IsReadable)
                    {
                        message = managedCamera.TLDevice.DeviceDisplayName.ToString();
                    }

                    // Initialize camera
                    managedCamera.Init();

                    width = managedCamera.Width.Value;
                    height = managedCamera.Height.Value;

                    // Retrieve GenICam nodemap
                    nodeMap = managedCamera.GetNodeMap();

                    imageQueue = imageQ;
                    result = true;

                }
                catch (SpinnakerException ex)
                {
                    Debug.WriteLine("Error: {0}", ex.Message);
                    message = ex.Message;
                    result = false; ;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    result = false;
                }
            }

            return result;

        }

        bool RestoreDefaultSettings()
        {
            bool result = false;
            try
            {
                #region default_settings
                IEnum iUserSetSelector = nodeMap.GetNode<IEnum>("UserSetSelector");
                if (iUserSetSelector == null || !iUserSetSelector.IsWritable)
                {
                    return false;
                }

                IEnumEntry iUserSetSelectorDefault = iUserSetSelector.GetEntryByName("Default");
                if (iUserSetSelectorDefault == null || !iUserSetSelectorDefault.IsReadable)
                {
                    return false;
                }

                iUserSetSelector.Value = iUserSetSelectorDefault.Symbolic;

                ICommand iUserSetLoad = nodeMap.GetNode<ICommand>("UserSetLoad");
                if (iUserSetLoad == null || !iUserSetLoad.IsWritable)
                {
                    return false;
                }

                iUserSetLoad.Execute();
                #endregion

                result = true;
            }
            catch (Exception ex)
            {
                result = false;
            }

            return result;
        }

        public bool SetStreamBufferCount(long count)
        {
            try
            {
                // set to manual
                INodeMap sNodeMap = managedCamera.GetTLStreamNodeMap();
                IEnum sBufferCountSelector = sNodeMap.GetNode<IEnum>("StreamBufferCountMode");
                if (sBufferCountSelector == null || !sBufferCountSelector.IsWritable)
                {
                    return false;
                }
                IEnumEntry iBufferCountManual = sBufferCountSelector.GetEntryByName("Manual");
                if (iBufferCountManual == null || !iBufferCountManual.IsReadable)
                {
                    return false;
                }
                sBufferCountSelector.Value = iBufferCountManual.Symbolic;

                // set the value
                IInteger streamNode = sNodeMap.GetNode<IInteger>("StreamDefaultBufferCount");
                if (streamNode == null || !streamNode.IsWritable)
                {
                    return false;
                }

                streamNode.Value = count;
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool SetVideoMode(int mode)
        {
            try
            {
                IEnum iVideoMode = nodeMap.GetNode<IEnum>("VideoMode");
                if (iVideoMode == null || !iVideoMode.IsWritable)
                {
                    return false;
                }

                IEnumEntry iVideoMode0 = iVideoMode.GetEntryByName("Mode0");
                if (iVideoMode0 == null || !iVideoMode0.IsReadable)
                {
                    return false;
                }
                IEnumEntry iVideoMode1 = iVideoMode.GetEntryByName("Mode1");
                if (iVideoMode1 == null || !iVideoMode1.IsReadable)
                {
                    return false;
                }

                if (mode == 0)
                    iVideoMode.Value = iVideoMode0.Symbolic;
                else if (mode == 1)
                    iVideoMode.Value = iVideoMode1.Symbolic;
                else
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DefaultSettings()
        {
            bool result = false;
            try
            {
                StopVideo();
                if (RestoreDefaultSettings())
                {
                    if (!SetStreamBufferCount(1))
                        return false;

                    //mode 0 and pixel format raw8/bayer rg 8
                    if (!SetVideoMode(0))
                        return false;

                    //managedCamera.PixelFormat.Value = PixelFormatEnums.BayerGB8.ToString();
                    managedCamera.PixelFormat.Value = PixelFormatEnums.RGB8.ToString();

                    //shutter, gain, wb and frame rate auto off
                    #region exposure_auto_compensation_off
                    IEnum iExposure = nodeMap.GetNode<IEnum>("pgrExposureCompensationAuto");
                    if (iExposure == null || !iExposure.IsWritable)
                    {
                        return false;
                    }

                    IEnumEntry iExposureOff = iExposure.GetEntryByName("Off");
                    if (iExposureOff == null || !iExposureOff.IsReadable)
                    {
                        return false;
                    }

                    iExposure.Value = iExposureOff.Symbolic;
                    #endregion

                    managedCamera.ExposureAuto.Value = ExposureAutoEnums.Off.ToString();
                    managedCamera.ExposureMode.Value = ExposureModeEnums.Timed.ToString();

                    managedCamera.GainAuto.Value = GainAutoEnums.Off.ToString();

                    #region frame_rate_off
                    IEnum iFrameRate = nodeMap.GetNode<IEnum>("AcquisitionFrameRateAuto");
                    if (iFrameRate == null || !iFrameRate.IsWritable)
                    {
                        return false;
                    }

                    IEnumEntry iFrameRateOff = iFrameRate.GetEntryByName("Off");
                    if (iFrameRateOff == null || !iFrameRateOff.IsReadable)
                    {
                        return false;
                    }

                    iFrameRate.Value = iFrameRateOff.Symbolic;

                    IBool iFrameRateEnabled = nodeMap.GetNode<IBool>("AcquisitionFrameRateEnabled");
                    if (iFrameRateEnabled == null || !iFrameRateEnabled.IsWritable)
                    {
                        return false;
                    }

                    iFrameRateEnabled.Value = false;

                    #endregion

                    // saturation enable off
                    {
                        IBool iSaturationEnabled = nodeMap.GetNode<IBool>("SaturationEnabled");
                        if (iSaturationEnabled == null || !iSaturationEnabled.IsWritable)
                        {
                            return false;
                        }

                        iSaturationEnabled.Value = false;
                    }
                    managedCamera.BalanceWhiteAuto.Value = BalanceWhiteAutoEnums.Off.ToString();

                    result = true;
                }

                //result = EnableChunkData();
            }
            catch (Exception ex)
            {
                result = false;
            }

            return result;
        }

        public void StartVideo(bool isContinue = true)
        {
            try
            {
                if (managedCamera != null && _videoMode == false)
                {
                    if (isContinue)
                    {
                        // Set acquisition mode to continuous
                        IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");

                        IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");

                        iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;
                    }
                    // Configure image events
                    imageEventListener = new ImageEventListener(imageQueue);
                    managedCamera.RegisterEvent(imageEventListener);

                    // Begin acquiring images
                    managedCamera.BeginAcquisition();

                    _videoMode = true;
                }
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
            }
        }

        public void StopVideo()
        {
            try
            {
                if (managedCamera != null)
                {
                    // End acquisition
                    managedCamera.EndAcquisition();

                    managedCamera.UnregisterEvent(imageEventListener);

                    //clear queue
                    PtGreyCameraImage item;
                    while (imageQueue.TryDequeue(out item))
                    {
                        // do nothing
                    }

                    _videoMode = false;
                }
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
            }
        }

        public ulong GetImageTimeStamp()
        {
            try
            {
                managedCamera.BeginAcquisition();
                while (true)
                {
                    using (IManagedImage rawImage = managedCamera.GetNextImage())
                    {
                        if (!rawImage.IsIncomplete)
                        {
                            return rawImage.TimeStamp;
                        }
                    }
                }
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
            }
            finally
            {
                managedCamera.EndAcquisition();
            }

            return 0;
        }

        public void Close()
        {
            if (camList != null)
            {
                foreach (IManagedCamera mc in camList)
                {
                    StopVideo();

                    // Deinitialize camera
                    mc.DeInit();

                    mc.Dispose();
                }

                camList.Clear();
            }

            if (system != null)
                system.Dispose();
        }


        public bool SetShutterTime(double value)
        {
            bool result = false;

            try
            {
                managedCamera.ExposureTime.Value = value;//microseconds
                result = true;
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
                result = false;
            }


            return result;
        }

        public double GetShutterTime()
        {
            return managedCamera.ExposureTime.Value;//microseconds
        }

        public bool SetGain(double value)
        {
            bool result = false;

            try
            {
                managedCamera.Gain.Value = value;
                result = true;
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
                result = false;
            }


            return result;
        }

        public double GetGain()
        {
            return managedCamera.Gain.Value;
        }

        public bool SetFrameRate(double value)
        {
            bool result = false;

            try
            {
                managedCamera.AcquisitionFrameRate.Value = value;
                result = true;
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
                result = false;
            }


            return result;
        }

        public double GetFrameRate()
        {
            return managedCamera.AcquisitionFrameRate.Value;
        }

        public bool SetWhiteBalanceRed(double wbRed)
        {
            bool result = false;

            try
            {
                managedCamera.BalanceRatioSelector.Value = BalanceRatioSelectorEnums.Red.ToString();
                managedCamera.BalanceRatio.Value = wbRed;
                result = true;
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
                result = false;
            }


            return result;
        }

        public double GetWhiteBalanceRed()
        {
            managedCamera.BalanceRatioSelector.Value = BalanceRatioSelectorEnums.Red.ToString();
            return (double)managedCamera.BalanceRatio.Value;
        }

        public bool SetWhiteBalanceBlue(double wbBlue)
        {
            bool result = false;

            try
            {
                managedCamera.BalanceRatioSelector.Value = BalanceRatioSelectorEnums.Blue.ToString();
                managedCamera.BalanceRatio.Value = wbBlue;
                result = true;
            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
                result = false;
            }


            return result;
        }

        public double GetWhiteBalanceBlue()
        {
            managedCamera.BalanceRatioSelector.Value = BalanceRatioSelectorEnums.Blue.ToString();
            return (double)managedCamera.BalanceRatio.Value;
        }


        bool EnableChunkData()
        {
            bool result = true;

            try
            {
                managedCamera.ChunkSelector.Value = ChunkSelectorEnums.FrameID.ToString();
                managedCamera.ChunkEnable.Value = true;

                managedCamera.ChunkSelector.Value = ChunkSelectorEnums.Timestamp.ToString();
                managedCamera.ChunkEnable.Value = true;

                managedCamera.ChunkModeActive.Value = true;
                #region chunkenable_using_node
                /*
                //
                // Activate chunk mode
                //
                // *** NOTES ***
                // Once enabled, chunk data will be available at the end of the 
                // payload of every image captured until it is disabled. Chunk 
                // data can also be retrieved from the nodemap.
                //
                IBool iChunkModeActive = nodeMap.GetNode<IBool>("ChunkModeActive");
                iChunkModeActive.Value = true;

                
                //
                // Enable all types of chunk data
                //
                // *** NOTES ***
                // Enabling chunk data requires working with nodes: 
                // "ChunkSelector" is an enumeration selector node and 
                // "ChunkEnable" is a boolean. It requires retrieving the 
                // selector node (which is of enumeration node type), selecting
                // the entry of the chunk data to be enabled, retrieving the 
                // corresponding boolean, and setting it to true. 
                //
                // In this example, all chunk data is enabled, so these steps 
                // are performed in a loop. Once this is complete, chunk mode
                // still needs to be activated.
                //
                // Retrieve selector node
                IEnum iChunkSelector = nodeMap.GetNode<IEnum>("ChunkSelector");

                // Retrieve entries
                EnumEntry[] entries = iChunkSelector.Entries;

                for (int i = 0; i < entries.Length; i++)
                {
                    // Select entry to be enabled
                    IEnumEntry iChunkSelectorEntry = entries[i];

                    // Go to next node if problem occurs
                    if (!iChunkSelectorEntry.IsAvailable || !iChunkSelectorEntry.IsReadable)
                    {
                        continue;
                    }

                    iChunkSelector.Value = iChunkSelectorEntry.Value;

                    // Retrieve corresponding boolean
                    IBool iChunkEnable = nodeMap.GetNode<IBool>("ChunkEnable");

                    // Enable the boolean, thus enabling the corresponding chunk
                    // data
                    if (iChunkEnable == null)
                    {

                    }
                    else if (iChunkEnable.Value)
                    {
                    }
                    else if (iChunkEnable.IsWritable)
                    {
                        iChunkEnable.Value = true;
                    }
                    else
                    {
                    }
                    
                }
                */
                #endregion

            }
            catch (SpinnakerException ex)
            {
                Debug.WriteLine("Error: {0}", ex.Message);
                result = false;
            }

            return result;
        }

        public bool TriggerDelay(bool enable, double ms)
        {
            bool result = false;
            try
            {
                #region triggerdelayenable
                IBool iTriggerDelayEnabled = nodeMap.GetNode<IBool>("TriggerDelayEnabled");
                iTriggerDelayEnabled.Value = enable;
                #endregion

                if (enable)
                    managedCamera.TriggerDelay.Value = (long)(1000 * ms);//microsecs

                result = true;
            }
            catch(Exception ex)
            {

            }

            return result;
        }

        public bool PhosTriggerMode(bool enable, uint count)
        {
            bool result = false;
            try
            {
                bool spinnaker = false;
                if (spinnaker)
                {
                    result = configTrigger(enable ? TriggerMode.On : TriggerMode.Off, enable ? TriggerType.Hardware : TriggerType.Unknown, count);
                }
                else
                {
                    const uint TRIGGER_MODE_REG = 0x830;
                    uint TriggerValue = 0x810F0000;//mode15, gpio 0, rising edge

                    if (enable)
                    {
                        TriggerValue |= (count & 0xFFF);//bit 20 - 31 has parameter
                        TriggerValue |= 0x02000000;//bit 6 enable
                    }

                    flycapManagedCamera.WriteRegister(TRIGGER_MODE_REG, TriggerValue);
                    result = true;
                }
            }
            catch (Exception ex)
            {

            }

            return result;
        }

        bool configTrigger(TriggerMode triggerMode, TriggerType triggerType, uint count = 1)
        {
            bool result = false;
            if (triggerMode == TriggerMode.Off)
            {
                IEnum triMode = nodeMap.GetNode<IEnum>("TriggerMode");
                if (triMode == null || !triMode.IsWritable)
                {
                    Console.WriteLine("configTrigger: Unable to disable trigger mode (enum retrieval). Aborting...");
                    return false;
                }

                IEnumEntry iTriggerModeOff = triMode.GetEntryByName("Off");
                if (iTriggerModeOff == null || !iTriggerModeOff.IsReadable)
                {
                    Console.WriteLine("configTrigger: Unable to disable trigger mode (entry retrieval). Aborting...");
                    return false;
                }
                triMode.Value = iTriggerModeOff.Value;
                result = true;
            }
            else if (triggerMode == TriggerMode.On)
            {
                IEnum triMode = nodeMap.GetNode<IEnum>("TriggerMode");
                if (triMode == null || !triMode.IsWritable)
                {
                    Console.WriteLine("configTrigger: Unable to enable trigger mode (enum retrieval). Aborting...");
                    return false;
                }

                IEnumEntry iTriggerModeOn = triMode.GetEntryByName("On");
                if (iTriggerModeOn == null || !iTriggerModeOn.IsReadable)
                {
                    Console.WriteLine("configTrigger: Unable to enable trigger mode (entry retrieval). Aborting...");
                    return false;
                }
                triMode.Value = iTriggerModeOn.Value;

                IEnum triggerSource = nodeMap.GetNode<IEnum>("TriggerSource");
                if (triggerType == TriggerType.Software)
                {
                    // Set trigger mode to software
                    IEnumEntry iTriggerSourceSoftware = triggerSource.GetEntryByName("Software");
                    if (iTriggerSourceSoftware == null || !iTriggerSourceSoftware.IsReadable)
                    {
                        Console.WriteLine("configTrigger: Unable to set software trigger mode (entry retrieval). Aborting...");
                        return false;
                    }
                    triggerSource.Value = iTriggerSourceSoftware.Value;

                    Console.WriteLine("configTrigger: Trigger source set to software...");
                }
                else if (triggerType == TriggerType.Hardware)
                {
                    // Set trigger mode to hardware ('Line0')
                    IEnumEntry iTriggerSourceHardware = triggerSource.GetEntryByName("Line0");
                    if (iTriggerSourceHardware == null || !iTriggerSourceHardware.IsReadable)
                    {
                        Console.WriteLine("configTrigger: Unable to set hardware trigger mode (entry retrieval). Aborting...");
                        return false;
                    }
                    triggerSource.Value = iTriggerSourceHardware.Value;

                    Console.WriteLine("configTrigger: Trigger source set to hardware...");
                }
                else
                {
                    Console.WriteLine("configTrigger: Trigger source Unknown");
                    return false;
                }

                {
                    IEnum triggerSelector = nodeMap.GetNode<IEnum>("TriggerSelector");
                    IEnumEntry iTriggerSelector = triggerSelector.GetEntryByName("FrameStart");
                    if (iTriggerSelector == null || !iTriggerSelector.IsReadable)
                    {
                        Console.WriteLine("configTrigger: Unable to set trigger selector (entry retrieval). Aborting...");
                        return false;
                    }
                    triggerSelector.Value = iTriggerSelector.Value;
                }
                {
                    IEnum triggerActivation = nodeMap.GetNode<IEnum>("TriggerActivation");
                    triggerActivation.Value = "RisingEdge";
                    IEnumEntry iTriggerActivation = triggerActivation.GetEntryByName("RisingEdge");
                    if (iTriggerActivation == null || !iTriggerActivation.IsReadable)
                    {
                        Console.WriteLine("configTrigger: Unable to set trigger activation (entry retrieval). Aborting...");
                        return false;
                    }
                    triggerActivation.Value = iTriggerActivation.Value;
                }
                // multi frame 
                if(count >= 1)
                {
                    IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");

                    IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("MultiFrame");

                    iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;

                    IInteger acquCount = nodeMap.GetNode<IInteger>("AcquisitionFrameCount");
                    acquCount.Value = count;

                    //single frame acquisition mode set to triggered
                    {
                        IEnum iSingleFrameAcquisitionMode = nodeMap.GetNode<IEnum>("SingleFrameAcquisitionMode");

                        IEnumEntry iSingleAcquisitionMode = iSingleFrameAcquisitionMode.GetEntryByName("Triggered");

                        iSingleFrameAcquisitionMode.Value = iSingleAcquisitionMode.Symbolic;
                    }
                } else
                {
                    IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");

                    IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");

                    iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;
                }


                result = true;
            }
            return result;
        }
    }
}
