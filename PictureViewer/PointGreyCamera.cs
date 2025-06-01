using System;
using System.Collections.Generic;
using System.ComponentModel;//BackgroundWorker
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;
//using FlyCapture2Managed;
//using FlyCapture2Managed.Gui;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using VideoInputSharp;
using System.Drawing;
using System.Data;

namespace MT3
{
    public partial class Form1 //PointGreyCamera
    {
        double pgr_time_pre = 0;
        double pgr_time_now = 0;
        double pgr_frame_rate = 0;
        double pgr_frame_rate_pre = 0;
        double alpha_pgr_frame_rate = 0.99;
        uint pgr_image_expo;
        uint pgr_image_gain;
        double pgr_EV;
        double pgr_temperature;
        string pgr_bus_speed;
        long pgr_image_frame_count;
        long pgr_timestamp_inc;
        //long pgr_frame_droped_count   = 0; //StreamDroppedFrameCount
        //long pgr_frame_overflow_count = 0; //TransferQueueOverflowCount
        //long pgr_frame_failure_count  = 0; //TransmitFailureCount
        bool pgr_post_save = false;

        // Retrieve singleton reference to system object
        ManagedSystem system;
        // ManagedBusManager busMgr ;
        IManagedCamera pgr_cam;
        // Retrieve list of cameras from the system
        ManagedCameraList camList;

        //private FlyCapture2Managed.Gui.CameraControlDialog m_camCtlDlg;
        private ManagedImage m_rawImage;
        private ManagedImage m_processedImage;
        private AutoResetEvent m_grabThreadExited;


        #region pgr background worker version
        public class PgrData
        {
            // propaty
            public long frameID { get; set; }
            public long pgr_frame_droped_count { get; set; }
            public long pgr_frame_overflow_count { get; set; }
            public long pgr_frame_failure_count { get; set; }
            public Mat img { get; set; }
        }
        PgrData pd = new PgrData();
       

        // 別スレッド処理（キャプチャー）
        private void pgr_worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = (BackgroundWorker)sender;
            Stopwatch sw = new Stopwatch();
            string str;
            int interval = 0;
            int deviceSerialNumber = appSettings.CameraID;

            pgr_Init();
            deviceSerialNumber = Int32.Parse(pgr_cam.DeviceSerialNumber.Value);
            pgr_bus_speed = pgr_BusSpeed(pgr_cam);
            try
            {
                // Begin capturing images
                pgr_cam.BeginAcquisition();
            }
            catch (SpinnakerException se)
            {
                if (se.ErrorCode == Error.SPINNAKER_ERR_RESOURCE_IN_USE)
                {
                    // Expected case where camera is already streaming
                    Console.WriteLine(
                        "Camera {0} - Expected Camera is already streaming : {1}", deviceSerialNumber, se.Message);
                }
                else
                {
                    Console.WriteLine("Camera {0} - Unexpected Exception : {1}", deviceSerialNumber, se.Message);
                    e.Result = -1;
                }
            }

            Console.WriteLine("Camera {0} - GetNextImage()", deviceSerialNumber);
            while (bw.CancellationPending == false)
            {
                try
                {
                    using (IManagedImage rawImage = pgr_cam.GetNextImage()) //GetNextImage(1000))
                    {

                        // Check Inconsistency Errors
                        if (rawImage.IsIncomplete)
                        {
                            Console.WriteLine(
                                "Camera {0} - incomplete image : {1}",
                                deviceSerialNumber,
                                ManagedImage.GetImageStatusDescription(rawImage.ImageStatus));
                        }
                        //画像データをバッファにコピー
                        lock (this)
                        {
                            unsafe
                            {
                                CopyMemory(imgdata.img.Data, rawImage.DataPtr, (int)imgdata.img.Total());
                            }
                        }
                        //Marshal.Copy(temp, 0, img.Data, vi.GetSize(DeviceID));

                        ManagedChunkData managedChunkData = rawImage.ChunkData;
                        pgr_image_frame_count = managedChunkData.FrameID;
                        pgr_time_pre = pgr_time_now;
                        pgr_time_now = managedChunkData.Timestamp * pgr_timestamp_inc ; //[ns] 
                        pgr_frame_rate = 1000000000.0 / (pgr_time_now - pgr_time_pre); // alpha_pgr_frame_rate * pgr_frame_rate_pre + (1 - alpha_pgr_frame_rate) * 1 / (pgr_time_now - pgr_time_pre);
                        UpdatePgrStatus();
                        pd.img = imgdata.img; // 画像データを浅いコピー  

                        //bw.ReportProgress(0, imgdata.img);
                        bw.ReportProgress(0, pd);

                        Application.DoEvents();
                        Thread.Sleep(interval);
                    }
                }
                catch (SpinnakerException se)
                {
                    if (se.ErrorCode == Error.SPINNAKER_ERR_IO)
                    {
                        // The first thread to call EndAcquisition() would have stopped streaming for all other threads
                        // as well
                        if (!pgr_cam.IsStreaming())
                        {
                            Console.WriteLine(
                                "Camera {0} - expected SPINNAKER_ERR_IO because EndAcquisition() was called",
                                deviceSerialNumber);
                        }
                        else
                        {
                            Console.WriteLine("Camera {0} - unexpected SPINNAKER_ERR_IO", deviceSerialNumber);
                            e.Result = -1;
                        }
                    }
                    else if (se.ErrorCode == Error.SPINNAKER_ERR_TIMEOUT)
                    {
                        // If a thread has already called EndAcquisition() and another thread was in the process of
                        // getting an image event, the event will time out
                        if (!pgr_cam.IsStreaming())
                        {
                            Console.WriteLine(
                                "Camera {0} - expected SPINNAKER_ERR_TIMEOUT because EndAcquisition() was called",
                                deviceSerialNumber);
                        }
                        else
                        {
                            Console.WriteLine("Camera {0} - unexpected SPINNAKER_ERR_TIMEOUT", deviceSerialNumber);
                            e.Result = -1;
                        }
                    }
                    else if (se.ErrorCode == Error.SPINNAKER_ERR_INVALID_BUFFER)
                    {
                        // If a thread has already called EndAcquisition() then images may have already been released
                        if (!pgr_cam.IsStreaming())
                        {
                            Console.WriteLine(
                                "Camera {0} - expected SPINNAKER_ERR_INVALID_BUFFER because EndAcquisition() was called",
                                deviceSerialNumber);
                        }
                        else
                        {
                            Console.WriteLine(
                                "Camera {0} - unexpected SPINNAKER_ERR_INVALID_BUFFER", deviceSerialNumber);
                            e.Result = -1;
                        }
                    }
                    else
                    {
                        Console.WriteLine(
                            "Camera {0} - unexpected expectation retrieving image {1}", deviceSerialNumber, se.Message);
                    }

                    if ((int)e.Result == -1)
                    {
                        break;
                    }
                }
            }

            Console.WriteLine("Camera {0} - EndAcquisition()", deviceSerialNumber);
            try
            {
                pgr_cam.EndAcquisition();
            }
            catch (SpinnakerException se)
            {
                if (se.ErrorCode == Error.SPINNAKER_ERR_NOT_INITIALIZED)
                {
                    // If a thread has already called EndAcquisition() when other threads try to call EndAcquisition()
                    // it will be expected to throw the message: camera is not started
                    Console.WriteLine("Camera {0} - Trying to End Acquisition: {1}", deviceSerialNumber, se.Message);
                }
                else
                {
                    e.Result = -1;
                    Console.WriteLine(
                        "Camera {0} - Unexpected error Stopping Acquisition: {1}", deviceSerialNumber, se.Message);
                    return;
                }
            }
        }
        void UpdatePgrStatus() {
            //pgr_image_frame_count = pgr_getIInteger(pgr_cam, "ChunkFrameCounter", 0); //FrameID NG読めない
            //pgr_frame_rate = pgr_getFrameRate(pgr_cam);// pgr_getARFrameRate(pgr_cam); // 取得フレームレート
            pgr_image_expo = (uint)pgr_cam.ExposureTime; //[us]
            pgr_image_gain = (uint)pgr_cam.Gain; //[dB]
            pgr_temperature = pgr_cam.DeviceTemperature; //[C] 
            pgr_EV = pgr_getEV(pgr_cam);   //pgrExposureCompensation
            
            pgr_bus_speed = pgr_BusSpeed(pgr_cam); //USB3=3, USB2=2, GigE=1
            pd.pgr_frame_overflow_count = pgr_cam.TransferQueueOverflowCount;//ここならOK
            pd.pgr_frame_droped_count = (pgr_getStreamIInteger(pgr_cam, "StreamDroppedFrameCount"));
            pd.pgr_frame_failure_count= (pgr_getStreamIInteger(pgr_cam, "TransmitFailureCount"));
        }
        //
        // 画像保存
        //
        private void pgr_worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pd = (PgrData)e.UserState;
            imgdata.img = pd.img;
            ///imgdata.img = (Mat)e.UserState;
            //Mat image = (Mat)e.UserState;                // 20250312 org code
            //image.Split( imgdata.img, null, null, null); // 20250312 org code

            // 表示画像反転 実装場所　要検討
            if (appSettings.FlipOn)
            {
                if (appSettings.Flipmode == OpenCvSharp.FlipMode.X || appSettings.Flipmode == OpenCvSharp.FlipMode.Y)
                {
                    imgdata.img = imgdata.img.Flip(appSettings.Flipmode);
                }
            }

            //++frame_id; 
            //detect(); // これをはずすと160fps
            imgdata_push_FIFO();

            ulong timestamp; //TimeStamp timestamp;
            lock (this)
            {           
                ///pgr_image_expo = (uint)((pgr_image.imageMetadata.embeddedShutter & 65535) * 4.883); // 1 count = 4.88268 ms
                ///pgr_image_gain = pgr_image.imageMetadata.embeddedGain & 65535;
                //    pgr_image_frame_count = pgr_image.FrameID;// .imageMetadata.embeddedFrameCounter;
                //    timestamp = pgr_image.TimeStamp;
            }
            frame_id = (int)pgr_image_frame_count;
            frame_overflow = (int)pd.pgr_frame_overflow_count;
            frame_dropped = (int)pd.pgr_frame_droped_count;
            frame_failure = (int)pd.pgr_frame_failure_count;
            ///pgr_time_now = timestamp.cycleSeconds + timestamp.cycleCount/8000.0 ; // count 8kHz, 1394 cycle timer
            ///pgr_frame_rate = pgr_image.f alpha_pgr_frame_rate * pgr_frame_rate_pre + (1 - alpha_pgr_frame_rate) * 1/(pgr_time_now - pgr_time_pre);
            pgr_frame_rate_pre = pgr_frame_rate;
            pgr_time_pre = pgr_time_now;
        }
        private void pgr_worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // First, handle the case where an exception was thrown.
            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message);
            }
            else if (e.Cancelled)
            {
                // Next, handle the case where the user canceled 
                // the operation.
                // Note that due to a race condition in 
                // the DoWork event handler, the Cancelled
                // flag may not have been set, even though
                // CancelAsync was called.
                this.ObsStart.BackColor = Color.FromKnownColor(KnownColor.Control);
                this.ObsEndButton.BackColor = Color.FromKnownColor(KnownColor.Control);
            }
            this.States = STOP;
        }

        #endregion

        static void PgrPrintBuildInfo(ManagedSystem system)
        {
            LibraryVersion version = system.GetLibraryVersion();
            //FC2Version version = ManagedUtilities.libraryVersion;

            StringBuilder newStr = new StringBuilder();
            newStr.AppendFormat(
                "FlyCapture2 library version: {0}.{1}.{2}.{3}\n",
                version.major, version.minor, version.type, version.build);

            Console.WriteLine(newStr);
        }

        static void PrintCameraInfo(IManagedCamera cam)
        {
            // Retrieve device serial number
            string deviceSerialNumber = "";
            INodeMap nodeMap = cam.GetTLDeviceNodeMap();
            IString iDeviceSerialNumber = nodeMap.GetNode<IString>("DeviceSerialNumber");
            if (iDeviceSerialNumber != null && iDeviceSerialNumber.IsReadable)
            {
                deviceSerialNumber = iDeviceSerialNumber.Value;
            }
            IString istr = nodeMap.GetNode<IString>("ModelName");
            string modelName = "";
            if (istr != null && istr.IsReadable)
            {
                modelName = istr.Value;
            }

            StringBuilder newStr = new StringBuilder();
            newStr.Append("\n*** CAMERA INFORMATION ***\n");
            newStr.AppendFormat("Serial number - {0}\n", deviceSerialNumber);
            newStr.AppendFormat("Camera model - {0}\n", modelName);
            //newStr.AppendFormat("Camera vendor - {0}\n", camInfo.vendorName);
            //newStr.AppendFormat("Sensor - {0}\n", camInfo.sensorInfo);
            //newStr.AppendFormat("Resolution - {0}\n", camInfo.sensorResolution);

            Console.WriteLine(newStr);
        }

        /// <summary>
        /// コールバック関数
        /// </summary>
        /// <param name="capacity">PGRコールバック</param>
        void OnImageGrabbed2(ManagedImage pgr_image)
        {
            //Console.WriteLine("Grabbed image {0} - {1}.{2}", imageCnt++, pgr_image.timeStamp.cycleSeconds, pgr_image.timeStamp.cycleCount);

            //画像データをバッファにコピー
            lock (this)
            {
                unsafe
                {
                    CopyMemory(imgdata.img.Data, pgr_image.DataPtr, (int)imgdata.img.Total());
                }
            }
            #region 他のコピー方法
            /*            // unsafe version　上手くいかない(2016.04.29)
            unsafe
            {
                CopyMemory(imgdata.img.ImageDataPtr, pgr_image.data, imgdata.img.ImageSize);
            }

            unsafe
            {
                int size = sizeof(Hoge);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                *(Hoge*)ptr = obj;
            }

             unsafe
            {
                int size = imgdata.img.ImageSize;// sizeof(Hoge);
                byte[] bytes = new byte[size];
                fixed (byte* pbytes = bytes)
                {
                    *(imgdata.img.ImageDataPtr)pbytes = *(pgr_image.data);
                }
            }
             */
            #endregion

            //++frame_id; 
            detect(); // これをはずすと160fps
            imgdata_push_FIFO();

            ulong timestamp; //TimeStamp timestamp;
            lock (this)
            {
                ///pgr_image_expo = (uint)((pgr_image.imageMetadata.embeddedShutter & 65535) * 4.883); // 1 count = 4.88268 ms
                ///pgr_image_gain = pgr_image.imageMetadata.embeddedGain & 65535;
                //pgr_image_frame_count = pgr_image.FrameID;// .imageMetadata.embeddedFrameCounter;
                //timestamp = pgr_image.TimeStamp;
            }
            frame_id = (int)pgr_image_frame_count;
            ///pgr_time_now = timestamp.cycleSeconds + timestamp.cycleCount/8000.0 ; // count 8kHz, 1394 cycle timer
            ///pgr_frame_rate = pgr_image.f alpha_pgr_frame_rate * pgr_frame_rate_pre + (1 - alpha_pgr_frame_rate) * 1/(pgr_time_now - pgr_time_pre);
            pgr_frame_rate_pre = pgr_frame_rate;
            pgr_time_pre = pgr_time_now;

            //pgr_image.Release(); // Must manually release the image to prevent buffers on the camera stream from filling up
        }

        public void pgr_Init()
        {
            system = new ManagedSystem();
            camList = system.GetCameras();
            // Finish if there are no cameras
            if (camList.Count == 0)
            {
                // Clear camera list before releasing system
                camList.Clear();

                // Release system
                system.Dispose();

                Console.WriteLine("Not enough cameras!");
                Console.WriteLine("Done! Press Enter to exit...");
                Console.ReadLine();

                MessageBox.Show("PGR Camera initializing failed");
                Environment.Exit(0);
            }

            pgr_cam = camList.First();// new ManagedCamera();(ManagedCamera)

            int result = 0;
            int err = 0;
            try
            {
                //pgr_DeviceReset(pgr_cam);
                //Thread.Sleep(5000);

                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = pgr_cam.GetTLDeviceNodeMap();

                result = PrintDeviceInfo2(nodeMapTLDevice);

                // Initialize camera
                pgr_cam.Init();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                MessageBox.Show("PGR Camera initializing failed");
                Environment.Exit(0);
            }

            // Retrieve GenICam nodemap
            INodeMap nodeMap = pgr_cam.GetNodeMap();
            // Activate chunk mode
            IBool iChunkModeActive = nodeMap.GetNode<IBool>("ChunkModeActive");
            if (iChunkModeActive == null || !iChunkModeActive.IsWritable)
            {
                Console.WriteLine("Cannot active chunk mode. Aborting...");
                return ;
            }

            iChunkModeActive.Value = true;

            Console.WriteLine("Chunk mode activated...");

            pgr_timestamp_inc = 1; // pgr_getIInteger(pgr_cam, "TimestampIncrement"); // [ns/tick]
            return;

            // Retrieve selector node
            IEnum iChunkSelector = nodeMap.GetNode<IEnum>("ChunkSelector");
            if (iChunkSelector == null || !iChunkSelector.IsReadable || !iChunkSelector.IsWritable)
            {
                Console.WriteLine("Chunk selector not available. Aborting...");
                return ;
            }

            // Retrieve entries
            EnumEntry[] entries = iChunkSelector.Entries;

            Console.WriteLine("Enabling entries...");

            for (int i = 0; i < entries.Length; i++)
            {
                // Select entry to be enabled
                IEnumEntry iChunkSelectorEntry = entries[i];

                // Go to next node if problem occurs
                if (!iChunkSelectorEntry.IsReadable)
                {
                    continue;
                }

                iChunkSelector.Value = iChunkSelectorEntry.Value;

                Console.Write("\t{0}: ", iChunkSelectorEntry.Symbolic);

                // Retrieve corresponding boolean
                IBool iChunkEnable = nodeMap.GetNode<IBool>("ChunkEnable");

                // Enable the boolean, thus enabling the corresponding chunk
                // data
                if (iChunkEnable == null)
                {
                    Console.WriteLine("not available");
                    result = -1;
                }
                else if (iChunkEnable.Value)
                {
                    Console.WriteLine("enabled");
                }
                else if (iChunkEnable.IsWritable)
                {
                    iChunkEnable.Value = true;
                    Console.WriteLine("enabled");
                }
                else
                {
                    Console.WriteLine("not writable");
                    result = -1;
                }
            }
            //Console.WriteLine();

            //m_rawImage = new ManagedImage();
            //m_processedImage = new ManagedImage();
            //m_camCtlDlg = new CameraControlDialog();
            //m_grabThreadExited = new AutoResetEvent(false);


        }
        /*
        public void OpenFirstPGRcamera()
        {
            //CameraSelectionDialog camSlnDlg = new CameraSelectionDialog();
            //bool retVal = camSlnDlg.ShowModal();
            //ManagedPGRGuid[] selectedGuids = camSlnDlg.GetSelectedCameraGuids();
            //ManagedPGRGuid guidToUse = selectedGuids[0];

            busMgr = new ManagedBusManager();
            pgr_cam = new ManagedCamera();
            busMgr.RescanBus();

            uint numCameras = busMgr.GetNumOfCameras();
            if (numCameras > 0)
            {
                uint pgr_cam_num = 0;
                ManagedPGRGuid guid = busMgr.GetCameraFromIndex(pgr_cam_num);
                RunSingleCamera(guid);
            }
            else
            {
                MessageBox.Show("PGR Camera initializing failed");
                Environment.Exit(0);
            }
        }

        public int pgr_OpenCamera()//(object sender, EventArgs e)
        {
            int result = 0;
            int err = 0;
            try
            {
                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = pgr_cam.GetTLDeviceNodeMap();

                result = PrintDeviceInfo2(nodeMapTLDevice);

                // Initialize camera
                pgr_cam.Init();

                // Retrieve GenICam nodemap
                INodeMap nodeMap = pgr_cam.GetNodeMap();

 
                err = ConfigureImageEvents(pgr_cam, ref imageEventListener);
                if (err < 0)
                {
                    return err;
                }
                // Acquire images using the image event handler
                result = result | AcquireImages(pgr_cam, nodeMap, nodeMapTLDevice, ref imageEventListener);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }

        public int pgr_CloseCamera()//(object sender, EventArgs e)
        {
            // Reset image event handlers
            int result = ResetImageEvents(pgr_cam, ref imageEventListener);

            // Deinitialize camera
            pgr_cam.DeInit();

            return result;
        } */

        /*
                // Set Video Property
                // Video Mode: Custom(Format 7)
                public void pgr_setVideoMode_Format7_1600(float frameRate)
                {
                    pgr_cam.SetVideoModeAndFrameRate(VideoMode.VideoModeFormat7, FrameRate.FrameRateFormat7);
                    Format7ImageSettings imgSettings = new Format7ImageSettings();
                    imgSettings.offsetX =  160;
                    imgSettings.offsetY =    0;
                    imgSettings.width   = 1600;
                    imgSettings.height  = 1200;
                    imgSettings.pixelFormat = PixelFormat.PixelFormatMono8;
                    pgr_cam.SetFormat7Configuration(imgSettings, frameRate);
                }

                // リクエストフレームレート
                public float pgr_getFrameRate()
                {
                    CameraProperty frameRateProp = pgr_cam.GetProperty(PropertyType.FrameRate);
                    return(frameRateProp.absValue);
                }
                public void pgr_setFrameRate(float fr)
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.FrameRate;
                    prop.autoManualMode = false;
                    prop.absControl = true;
                    prop.absValue = fr;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }
                // Gain Auto
                public void pgr_setGainAuto()
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.Gain;
                    prop.autoManualMode = true;
                    prop.absControl = true;
                    //prop.absValue = expo_ms;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }
                // Exposure Auto
                public void pgr_setShutterAuto()
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.Shutter;
                    prop.autoManualMode = true;
                    prop.absControl = true;
                    //prop.absValue = expo_ms;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }
                public void pgr_setShutter(float expo_ms)
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.Shutter;
                    prop.autoManualMode = false;
                    prop.absControl = true;
                    prop.absValue = expo_ms;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }

                // set Brightness 
                public void pgr_setBrightness(float br)
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.Brightness;
                    prop.autoManualMode = false;
                    prop.absControl = true;
                    prop.absValue = br;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }
                // set ganma 
                public void pgr_setGamma(float gamma)
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.Gamma;
                    prop.autoManualMode = false;
                    prop.absControl = true;
                    prop.absValue = gamma;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }
                // set EV 
                public void pgr_setEV(float ev)
                {
                    //Declare a Property struct. 
                    CameraProperty prop = new CameraProperty();
                    prop.type = PropertyType.AutoExposure;
                    prop.autoManualMode = false;
                    prop.absControl = true;
                    prop.absValue = ev;
                    prop.onOff = true;

                    pgr_cam.SetProperty(prop);
                }
                // get EV 
                public float pgr_getEV()
                { 
                    return pgr_get_property(PropertyType.AutoExposure);
                }
                // get camera property
                public float pgr_get_property(PropertyType pt )
                {
                    //Declare a Property struct. 
                    CameraProperty prop = pgr_cam.GetProperty( pt );
                    return (prop.absValue);
                }
        */
        //Enumerator: 
        //S100  100Mbits/sec. 
        //S200  200Mbits/sec. 
        //S400  400Mbits/sec. 
        //S480  480Mbits/sec.   //Only for USB2 cameras.

        //S800  800Mbits/sec. 
        //S1600  1600Mbits/sec. 
        //S3200  3200Mbits/sec. 
        //S5000  5000Mbits/sec. 
        //
        //Only for USB3 cameras. 
        // 
        //GigE_10Base_T   
        //GigE_100Base_T   
        //GigE_1000Base_T   
        //GigE_10000Base_T   
        //Fastest  The fastest speed available. 
        //Any  Any speed that is available.  
        //Unknown  Unknown interface. 

        public string pgr_BusSpeed(IManagedCamera _cam) //U3VCurrentSpeed  USB3=3　　QS:DeviceCurrentSpeed
        {
            if (_cam == null) return ("error");
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("DeviceCurrentSpeed");// U3VCurrentSpeed");
            if (iNode == null || !iNode.IsReadable)
            {
                Console.WriteLine("Unable to read node DeviceCurrentSpeed. Aborting...\n");
                return ("error");
            }

            //CameraInfo camInfo = pgr_cam.GetCameraInfo();
            return (iNode.Value.String);

        }

        public void pgr_DeviceReset(IManagedCamera _cam) //DeviceReset
        {
            if (_cam == null) return;
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            ICommand iNode = nodeMap.GetNode<ICommand>("DeviceReset");
            if (iNode != null && iNode.IsAvailable)
            {
                iNode.Execute();
            }
            else
            {
                Console.WriteLine("Unable to reset device (node retrieval). Aborting...\n");
            }
        }

        public double pgr_getTemperature(IManagedCamera _cam) //DeviceTemperature
        {
            return (pgr_getIFloat(_cam, "DeviceTemperature"));
        }
        //DeviceCurrentSpeed
        public string pgr_getDeviceCurrentSpeed(IManagedCamera _cam) //DeviceCurrentSpeed
        {
            string ans = "";
            if (_cam == null) return (ans);
            try
            {
                INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
                IEnum iNode = nodeMap.GetNode<IEnum>("DeviceCurrentSpeed");
                ans = iNode.Value.String;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message); ans = "Error";
            }
            return (ans);
        }
        //ChunkFrameCounter
        public long pgr_getIInteger(IManagedCamera _cam, string st, long ans = -990) //IInteger  ans:default value
        {
            if (_cam == null || st == "") return (ans);

            try
            {
                //ans = _cam.AcquisitionFrameRate.Value;
                INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
                IInteger iIntegerNode = nodeMap.GetNode<IInteger>(st);
                if (iIntegerNode == null || !iIntegerNode.IsReadable)
                {
                    Console.WriteLine("Unable to read node {0}. Aborting...\n", st);
                    return (ans);
                }
                ans = iIntegerNode.Value ;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -997;
            }
            catch (System.NullReferenceException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -998;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -999;
            }
            return (ans);
        }
        public long pgr_getStreamIInteger(IManagedCamera _cam, string st, long ans = -990) //IInteger  ans:default value
        {
            if (_cam == null || st == "") return (ans);

            try
            {               
                INodeMap sNodeMap = _cam.GetTLStreamNodeMap();
                IInteger iIntegerNode = sNodeMap.GetNode<IInteger>(st);
                if (iIntegerNode == null || !iIntegerNode.IsReadable)
                {
                    Console.WriteLine("Unable to read node {0}. Aborting...\n", st);
                    return (ans);
                }
                ans = iIntegerNode.Value;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -997;
            }
            catch (System.NullReferenceException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -998;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -999;
            }
            return (ans);
        }

        //Controls the acquisition rate (in Hertz) at which the frames are captured.
        public double pgr_getIFloat(IManagedCamera _cam, string st, double ans=-990.0) //IFloat  ans:default value
        {          
            if (_cam == null || st == "") return (ans);

            try
            {
                //ans = _cam.AcquisitionFrameRate.Value;
                INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
                IFloat iFloatNode = nodeMap.GetNode<IFloat>(st);
                if (iFloatNode == null || !iFloatNode.IsReadable)
                {
                    Console.WriteLine("Unable to read node {0}. Aborting...\n", st);
                    return (ans);
                }
                ans = iFloatNode.Value;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -997;
            }
            catch (System.NullReferenceException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -998;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
                ans = -999;
            }
            return (ans);
        }
        public long pgr_getSDFC(IManagedCamera _cam, string st) //StreamDroppedFrameCount  TransferQueueOverflowCount
        {
            return pgr_getStreamIInteger(_cam, st);
        }
        public bool pgr_setIFloat(IManagedCamera _cam, string st, double val) //AcquisitionFrameRate
        {
            bool ans = false;
            if (_cam == null || st == "") return (ans);

            try
            {
                //ans = _cam.AcquisitionFrameRate.Value;
                INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
                IFloat iFloatNode = nodeMap.GetNode<IFloat>(st);
                if (iFloatNode == null || !iFloatNode.IsReadable)
                {
                    Console.WriteLine("Unable to read node {0}. Aborting...\n", st);
                    return (ans);
                }
                iFloatNode.Value = val;
                ans = true;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Error({1}): {0}", ex.Message, st);
            }
            return (ans);
        }
        public double pgr_getARFrameRate(IManagedCamera _cam) //AcquisitionFrameRate  AcquisitionResultingFrameRate
        {
            return (pgr_getIFloat(_cam, "AcquisitionResultingFrameRate",0.0));
        }
        public double pgr_getFrameRate(IManagedCamera _cam) //AcquisitionFrameRate
        {
            return (pgr_getIFloat(_cam, "AcquisitionFrameRate"));
        }
        public bool pgr_setFrameRate(IManagedCamera _cam, double fr) //AcquisitionFrameRate
        {
            return (pgr_setIFloat(_cam, "AcquisitionFrameRate", fr));
        }
        //pgrExposureCompensation
        public double pgr_getEV(IManagedCamera _cam) //pgrExposureCompensation
        {
            return (pgr_getIFloat(_cam, "pgrExposureCompensation"));
        }
        public bool pgr_setEV(IManagedCamera _cam, double ev) //pgrExposureCompensation
        {
            return (pgr_setIFloat(_cam, "pgrExposureCompensation", ev));
        }
        public void pgr_setGainAuto(IManagedCamera _cam) //GainAuto  0:off 1:once 2:continuous
        {
            if (_cam == null) return;
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("GainAuto");
            if (iNode != null && iNode.IsAvailable)
            {
                iNode.Value = 2; //continuous
            }
            else
            {
                Console.WriteLine("Unable to set gain auto. Aborting...\n");
            }
        }
        public void pgr_setGainAutoOff(IManagedCamera _cam) //GainAuto  0:off 1:once 2:continuous
        {
            if (_cam == null) return;
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("GainAuto");
            if (iNode != null && iNode.IsAvailable)
            {
                iNode.Value = 0; //off
            }
            else
            {
                Console.WriteLine("Unable to set gain auto off. Aborting...\n");
            }
        }
        public double pgr_getGain(IManagedCamera _cam) //Gain
        {
            return (pgr_getIFloat(_cam, "Gain"));
        }

        public void pgr_setExposureAuto(IManagedCamera _cam) //ExposureAuto  0:off 1:once 2:continuous
        {
            if (_cam == null) return;
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("ExposureAuto");
            if (iNode != null && iNode.IsAvailable)
            {
                iNode.Value = 2; //continuous
            }
            else
            {
                Console.WriteLine("Unable to set gain auto. Aborting...\n");
            }
        }
        public void pgr_setExposureAutoOff(IManagedCamera _cam) //GainAuto  0:off 1:once 2:continuous
        {
            if (_cam == null) return;
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("ExposureAuto");
            if (iNode != null && iNode.IsAvailable)
            {
                iNode.Value = 0; //off
            }
            else
            {
                Console.WriteLine("Unable to set gain auto off. Aborting...\n");
            }
        }
        public double pgr_getExposureTime(IManagedCamera _cam) //ExposureTime
        {
            return (pgr_getIFloat(_cam, "ExposureTime"));
        }

        public uint pgr_PixelClockFreq(IManagedCamera _cam)
        {
            if (_cam == null) return (0);
            //const uint k_addr = 0x1AF0;
            uint regVal = 0;//= _cam.ReadRegister(k_addr);
            return (regVal);
        }
        // Power on the camera
        public void pgr_PowerOnCamera(IManagedCamera _cam)
        {/*
            const uint k_cameraPower = 0x610;
            const uint k_powerVal = 0x80000000;
            _cam.WriteRegister(k_cameraPower, k_powerVal);

            const Int32 k_millisecondsToSleep = 100;
            uint regVal = 0;

            // Wait for camera to complete power-up
            do
            {
                System.Threading.Thread.Sleep(k_millisecondsToSleep);
                regVal = _cam.ReadRegister(k_cameraPower);
            } while ((regVal & k_powerVal) == 0);  
            */
        }
        // Power off the camera
        public void pgr_PowerOffCamera(IManagedCamera _cam)
        {/*
            const uint k_cameraPower = 0x610;
            const uint k_powerVal = 0x00000000;
            _cam.WriteRegister(k_cameraPower, k_powerVal);

            const Int32 k_millisecondsToSleep = 100;
            uint regVal = 0;

            // Wait for camera to complete power-down
            do
            {
                System.Threading.Thread.Sleep(k_millisecondsToSleep);
                regVal = _cam.ReadRegister(k_cameraPower);
            } while (regVal != 0);
            */
        }
        //
        // Memory Set 1
        public void pgr_SetMemory1(IManagedCamera _cam)
        {
            if (_cam == null) return;
            //UserSetSelector  //UserSetLoad
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("UserSetSelector");
            iNode.Value = 1;// Memory1
            if (iNode.Value != 1)
            {
                Console.WriteLine("Memory1 != 1 . Aborting...\n");
                return;
            }

            ICommand iNode2 = nodeMap.GetNode<ICommand>("DeviceReset");
            if (iNode2 != null && iNode2.IsAvailable)
            {
                iNode2.Execute();
            }
            else
            {
                Console.WriteLine("Unable to execute set memory (node retrieval). Aborting...\n");
            }
        }

        // 通常撮像
        // fps=50
        public void pgr_Normal_settings()
        {
            if (pgr_cam == null) return;
            pgr_SetMemory1(pgr_cam);
            pgr_setFrameRate(pgr_cam, appSettings.Framerate);
            pgr_setEV(pgr_cam, appSettings.ExposureValue);
            //pgr_setBrightness((float)0.0);
            //pgr_setShutter((float)appSettings.Exposure);
        }
        // 保存後撮像
        // for fish2  fps=1
        public void pgr_PostSave_settings()
        {
            if (pgr_cam == null) return;
            pgr_setFrameRate(pgr_cam, (double)1.0);//fps 1
            //pgr_setShutter((float)1000);
            pgr_setGainAuto(pgr_cam);
            pgr_setExposureAuto(pgr_cam);
        }


        // This function prints the device information of the camera from the
        // transport layer; please see NodeMapInfo_CSharp example for more
        // in-depth comments on printing device information from the nodemap.
        static int PrintDeviceInfo2(INodeMap nodeMap)
        {
            int result = 0;

            try
            {
                Console.WriteLine("\n*** DEVICE INFORMATION ***\n");

                ICategory category = nodeMap.GetNode<ICategory>("DeviceInformation");
                if (category != null && category.IsReadable)
                {
                    for (int i = 0; i < category.Children.Length; i++)
                    {
                        Console.WriteLine(
                            "{0}: {1}",
                            category.Children[i].Name,
                            (category.Children[i].IsReadable ? category.Children[i].ToString()
                             : "Node not available"));
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("Device control information not available.");
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }
    }
}



