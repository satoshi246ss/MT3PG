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

namespace MT3
{
    partial class Form1 //PointGreyCamera
    {
        double pgr_time_pre = 0;
        double pgr_time_now = 0;
        double pgr_frame_rate = 0;
        double pgr_frame_rate_pre = 0;
        double alpha_pgr_frame_rate = 0.99;
        uint pgr_image_expo;
        uint pgr_image_gain;
        ulong pgr_image_frame_count;
        bool pgr_post_save = false;

        // Retrieve singleton reference to system object
        ManagedSystem system;
        //ManagedBusManager busMgr ;
        ManagedCamera pgr_cam;
        // Retrieve list of cameras from the system
        ManagedCameraList camList;
        ImageEventListener imageEventListener = null;

        //private FlyCapture2Managed.Gui.CameraControlDialog m_camCtlDlg;
        private ManagedImage m_rawImage;
        private ManagedImage m_processedImage;
        private AutoResetEvent m_grabThreadExited;



        // This class defines the properties, parameters, and the event handler itself.
        // Take a moment to notice what parts of the class are mandatory, and
        // what have been added for demonstration purposes. First, any class
        // used to define image event handlers must inherit from ManagedImageEventHandler.
        // Second, the method signature of OnImageEvent() must also be
        // consistent and follow the override keyword. Everything else,
        // including the constructor, properties, body of OnImageEvent(), and
        // other functions, is particular to the example.
        class ImageEventListener : ManagedImageEventHandler
        {
            private string deviceSerialNumber;

            public const int NumImages = 10;
            public int imageCnt;
            IManagedImageProcessor processor;

            public ImageData ELimgdata { get; private set; }

            // The constructor retrieves the serial number and initializes the
            // image counter to 0.
            public ImageEventListener(IManagedCamera cam)//, ImageData _imageData)
            {
                // Initialize image counter to 0
                imageCnt = 0;
                ELimgdata = _imageData;
                // Retrieve device serial number
                INodeMap nodeMap = cam.GetTLDeviceNodeMap();

                deviceSerialNumber = "";

                IString iDeviceSerialNumber = nodeMap.GetNode<IString>("DeviceSerialNumber");
                if (iDeviceSerialNumber != null && iDeviceSerialNumber.IsReadable)
                {
                    deviceSerialNumber = iDeviceSerialNumber.Value;
                }

                //
                // Create ImageProcessor instance for post processing images
                //
                processor = new ManagedImageProcessor();

                //
                // Set default image processor color processing method
                //
                // *** NOTES ***
                // By default, if no specific color processing algorithm is set, the image
                // processor will default to NEAREST_NEIGHBOR method.
                //
                processor.SetColorProcessing(ColorProcessingAlgorithm.HQ_LINEAR);
            }

            // This method defines an image event. In it, the image that
            // triggered the event is converted and saved before incrementing
            // the count. Please see Acquisition_CSharp example for more
            // in-depth comments on the acquisition of images.
            override protected void OnImageEvent(ManagedImage image)
            {
                //画像データをバッファにコピー
                lock (this)
                {
                    unsafe
                    {
                        CopyMemory(ELimgdata.img.Data, image.DataPtr, (int)ELimgdata.img.Total());
                    }
                }
                //imgdata_push_FIFO();

                /*
                if (imageCnt < NumImages)
                {
                    Console.WriteLine("Image event occurred...");

                    if (image.IsIncomplete)
                    {
                        Console.WriteLine("Image incomplete with image status {0}...\n", image.ImageStatus);
                    }
                    else
                    {
                        // Convert image
                        using (IManagedImage convertedImage = processor.Convert(image, PixelFormatEnums.Mono8))//37ms
                        {
                            // Print image information
                            Console.WriteLine(
                                "Grabbed image {0}, width = {1}, height = {2}",
                                imageCnt,
                                convertedImage.Width,
                                convertedImage.Height);

                            // Create unique filename in order to save file
                            String filename = "ImageEvents-CSharp-";
                            if (deviceSerialNumber != "")
                            {
                                filename = filename + deviceSerialNumber + "-";
                            }
                            filename = filename + imageCnt + ".jpg";

                            // Save image
                            convertedImage.Save(filename);//44ms

                            Console.WriteLine("Image saved at {0}\n", filename);

                            // Increment image counter
                            imageCnt++;
                        }
                    }
                */
                // Must manually release the image to prevent buffers on the camera stream from filling up
                image.Release();
                }
            }
            /// <summary>
            /// コールバック関数
            /// </summary>
            /// <param name="capacity">PGRコールバック</param>
            void OnImageGrabbed(ManagedImage pgr_image)
            {
                //Console.WriteLine("Grabbed image {0} - {1}.{2}", imageCnt++, pgr_image.timeStamp.cycleSeconds, pgr_image.timeStamp.cycleCount);

                //画像データをバッファにコピー
                lock (this)
                {
                    unsafe
                    {
                        //CopyMemory(imgdata.img.Data, pgr_image.DataPtr, (int)imgdata.img.Total());
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
                ////detect(); // これをはずすと160fps
                ////imgdata_push_FIFO();

                ulong timestamp; //TimeStamp timestamp;
                lock (this)
                {
                    ///pgr_image_expo = (uint)((pgr_image.imageMetadata.embeddedShutter & 65535) * 4.883); // 1 count = 4.88268 ms
                    ///pgr_image_gain = pgr_image.imageMetadata.embeddedGain & 65535;
                    //pgr_image_frame_count = pgr_image.FrameID;// .imageMetadata.embeddedFrameCounter;
                    timestamp = pgr_image.TimeStamp;
                }
                //frame_id = (int)pgr_image_frame_count;
                ///pgr_time_now = timestamp.cycleSeconds + timestamp.cycleCount/8000.0 ; // count 8kHz, 1394 cycle timer
                ///pgr_frame_rate = pgr_image.f alpha_pgr_frame_rate * pgr_frame_rate_pre + (1 - alpha_pgr_frame_rate) * 1/(pgr_time_now - pgr_time_pre);
                //pgr_frame_rate_pre = pgr_frame_rate;
                //pgr_time_pre = pgr_time_now;

                pgr_image.Release(); // Must manually release the image to prevent buffers on the camera stream from filling up
            }


            // This function configures the example to execute image events by
            // preparing and registering an image event.
            int ConfigureImageEvents(IManagedCamera cam, ref ImageEventListener imageEventListener)
            {
                int result = 0;

                Console.WriteLine("\n\n*** CONFIGURING IMAGE EVENTS ***\n");

                try
                {
                    //
                    // Create image event
                    //
                    // *** NOTES ***
                    // The class has been constructed to accept a managed camera
                    // in order to allow the saving of images with the device
                    // serial number.
                    //
                    imageEventListener = new ImageEventListener(cam);

                    //
                    // Register image event handler
                    //
                    // *** NOTES ***
                    // Image events are registered to cameras. If there are
                    // multiple cameras, each camera must have the image events
                    // registered to it separately. Also, multiple image events may
                    // be registered to a single camera.
                    //
                    // *** LATER ***
                    // Image events must be unregistered manually. This must be
                    // done prior to releasing the system and while the image
                    // events are still in scope.
                    //
                    cam.RegisterEventHandler(imageEventListener);
                }
                catch (SpinnakerException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                    result = -1;
                }

                return result;
            }

            // This function waits for the appropriate amount of images. Notice
            // that whereas most examples actively retrieve images, the acquisition
            // of images is handled passively in this example.
            int WaitForImages(ref ImageEventListener imageEventListener)
            {
                int result = 0;

                try
                {
                    //
                    // Wait for images
                    //
                    // *** NOTES ***
                    // In order to passively capture images using image events and
                    // automatic polling, the main thread sleeps in increments of
                    // 200 ms until 10 images have been acquired and saved.
                    //
                    const int sleepDuration = 1;

                    while (imageEventListener.imageCnt < ImageEventListener.NumImages)
                    {
                        Console.WriteLine("\t//");
                        Console.WriteLine("\t// Sleeping for {0} time slices. Grabbing images...", sleepDuration);
                        Console.WriteLine("\t//");

                        Thread.Sleep(sleepDuration);
                    }
                }
                catch (SpinnakerException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                    result = -1;
                }

                return result;
            }

            // This functions resets the example by unregistering the image event handler.
            int ResetImageEvents(IManagedCamera cam, ref ImageEventListener imageEventListener)
            {
                int result = 0;

                try
                {
                    //
                    // Unregister image event handler
                    //
                    // *** NOTES ***
                    // It is important to unregister all image event handlers from all
                    // cameras they are registered to.
                    //
                    cam.UnregisterEventHandler(imageEventListener);

                    Console.WriteLine("Image events unregistered...\n");
                }
                catch (SpinnakerException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                    result = -1;
                }

                return result;
            }

            // This function prints the device information of the camera from the
            // transport layer; please see NodeMapInfo_CSharp example for more
            // in-depth comments on printing device information from the nodemap.
            static int PrintDeviceInfo(INodeMap nodeMap)
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

            // This function passively waits for images by calling WaitForImages().
            // Notice that this function is much shorter than the AcquireImages()
            // function of other examples. This is because most of the code has
            // been moved to the image event's OnImageEvent() method.
            int AcquireImages(
                IManagedCamera cam,
                INodeMap nodeMap,
                INodeMap nodeMapTLDevice,
                ref ImageEventListener imageEventListener)
            {
                int result = 0;

                Console.WriteLine("\n*** IMAGE ACQUISITION ***\n");

                try
                {
                    // Set acquisition mode to continuous
                    IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
                    if (iAcquisitionMode == null || !iAcquisitionMode.IsReadable || !iAcquisitionMode.IsWritable)
                    {
                        Console.WriteLine("Unable to set acquisition mode to continuous (node retrieval). Aborting...\n");
                        return -1;
                    }

                    IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");
                    if (iAcquisitionModeContinuous == null || !iAcquisitionModeContinuous.IsReadable)
                    {
                        Console.WriteLine(
                            "Unable to get acquisition mode to continuous (enum entry retrieval). Aborting...\n");
                        return -1;
                    }

                    iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;

                    Console.WriteLine("Acquisition mode set to continuous...");

                    // Begin acquiring images
                    cam.BeginAcquisition();

                    Console.WriteLine("Acquiring images...");

                    // Retrieve images using image event handler
                    result = WaitForImages(ref imageEventListener);

                    // End acquisition
                    cam.EndAcquisition();
                }
                catch (SpinnakerException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                    result = -1;
                }

                return result;
            }

            // This function acts as the body of the example; please see
            // NodeMapInfo_CSharp example for more in-depth comments on setting up
            // cameras.
            int RunSingleCamera(IManagedCamera cam)
            {
                int result = 0;
                int err = 0;

                try
                {
                    // Retrieve TL device nodemap and print device information
                    INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();

                    result = PrintDeviceInfo(nodeMapTLDevice);

                    // Initialize camera
                    cam.Init();

                    // Retrieve GenICam nodemap
                    INodeMap nodeMap = cam.GetNodeMap();

                    // Configure image event handlers
                    ImageEventListener imageEventListener = null;

                    err = ConfigureImageEvents(cam, ref imageEventListener);
                    if (err < 0)
                    {
                        return err;
                    }

                    // Acquire images using the image event handler
                    result = result | AcquireImages(cam, nodeMap, nodeMapTLDevice, ref imageEventListener);

                    // Reset image event handlers
                    result = result | ResetImageEvents(cam, ref imageEventListener);

                    // Deinitialize camera
                    cam.DeInit();
                }
                catch (SpinnakerException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                    result = -1;
                }

                return result;
            }
        }

        static void PgrPrintBuildInfo( ManagedSystem system )
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
        void OnImageGrabbed(ManagedImage pgr_image)
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
                pgr_image_frame_count = pgr_image.FrameID;// .imageMetadata.embeddedFrameCounter;
                timestamp = pgr_image.TimeStamp;
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
                //return -1;
            }

            pgr_cam = (ManagedCamera) camList.First();// new ManagedCamera();

            m_rawImage = new ManagedImage();
            m_processedImage = new ManagedImage();
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
        } */

        public int pgr_OpenCamera()//(object sender, EventArgs e)
        {
            //CameraSelectionDialog camSlnDlg = new CameraSelectionDialog();
            //bool retVal = camSlnDlg.ShowModal();
            //ManagedPGRGuid[] selectedGuids = camSlnDlg.GetSelectedCameraGuids();
            //ManagedPGRGuid guidToUse = selectedGuids[0];

            ///busMgr = new ManagedBusManager();
            ///busMgr.RescanBus();

            int result = 0;
            int err = 0;
            try
            {
                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = pgr_cam.GetTLDeviceNodeMap();

                result = PrintDeviceInfo(nodeMapTLDevice);

                // Initialize camera
                pgr_cam.Init();

                // Retrieve GenICam nodemap
                INodeMap nodeMap = pgr_cam.GetNodeMap();

                // Configure image event handlers
                imageEventListener = null;

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
        }
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

        public string pgr_BusSpeed(ManagedCamera _cam) //U3VCurrentSpeed  USB3=3
        {
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IEnum iNode = nodeMap.GetNode<IEnum>("U3VCurrentSpeed");

            //CameraInfo camInfo = pgr_cam.GetCameraInfo();
            return (iNode.Value.String);

        }

        public void pgr_DeviceReset(ManagedCamera _cam) //DeviceReset
        {
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

        public double pgr_Temperature(SpinnakerNET.ManagedCamera _cam) //DeviceTemperature
        {
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IFloat iFloatNode = nodeMap.GetNode<IFloat>("DeviceTemperature");
            //const uint k_addr = 0x82C;
            //const uint k_eVal = 0xFFF;
            //uint regVal = _cam.ReadRegister(k_addr);
            return (iFloatNode.Value);
        }
        //DeviceCurrentSpeed
        public string pgr_getDeviceCurrentSpeed(ManagedCamera _cam) //DeviceCurrentSpeed
        {
            string ans ="";
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
        }        //Controls the acquisition rate (in Hertz) at which the frames are captured.
        public double pgr_getFrameRate(ManagedCamera _cam) //AcquisitionFrameRate
        {
            double ans = -999;
            try
            {
                ans = _cam.AcquisitionFrameRate.Value;
                //INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
                //IFloat iFloatNode = nodeMap.GetNode<IFloat>("AcquisitionFrameRate");
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
            return (ans);
        }
        public bool pgr_setFrameRate(ManagedCamera _cam, double fr) //AcquisitionFrameRate
        {
            bool ans = true;
            try
            {
                _cam.AcquisitionFrameRate.Value = fr;
                //INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
                //IFloat iFloatNode = nodeMap.GetNode<IFloat>("AcquisitionFrameRate");
                //iFloatNode.Value = fr;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                ans = false;
            }
            return (ans);
        }
        //pgrExposureCompensation
        public double pgr_getEV(ManagedCamera _cam) //pgrExposureCompensation
        {
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IFloat iFloatNode = nodeMap.GetNode<IFloat>("pgrExposureCompensation");
            return (iFloatNode.Value);
        }
        public double pgr_setEV(ManagedCamera _cam, double ev) //pgrExposureCompensation
        {
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IFloat iFloatNode = nodeMap.GetNode<IFloat>("AcquisitionFrameRate");
            iFloatNode.Value = ev;
            return (iFloatNode.Value);
        }
        public void pgr_setGainAuto(ManagedCamera _cam) //GainAuto  0:off 1:once 2:continuous
        {
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
        public void pgr_setGainAutoOff(ManagedCamera _cam) //GainAuto  0:off 1:once 2:continuous
        {
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
        public double pgr_getGain(ManagedCamera _cam) //Gain
        {
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IFloat iFloatNode = nodeMap.GetNode<IFloat>("Gain");
            return (iFloatNode.Value);
        }

        public void pgr_setExposureAuto(ManagedCamera _cam) //ExposureAuto  0:off 1:once 2:continuous
        {
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
        public void pgr_setExposureAutoOff(ManagedCamera _cam) //GainAuto  0:off 1:once 2:continuous
        {
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
        public double pgr_getExposureTime(ManagedCamera _cam) //ExposureTime
        {
            INodeMap nodeMap = _cam.GetTLDeviceNodeMap();
            IFloat iFloatNode = nodeMap.GetNode<IFloat>("ExposureTime");
            return (iFloatNode.Value);
        }

        public uint pgr_PixelClockFreq(ManagedCamera _cam)
        {
            //const uint k_addr = 0x1AF0;
            uint regVal = 0;//= _cam.ReadRegister(k_addr);
            return (regVal);
        }
        // Power on the camera
        public void pgr_PowerOnCamera(ManagedCamera _cam)
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
        public void pgr_PowerOffCamera(ManagedCamera _cam)
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
        public void pgr_SetMemory1(ManagedCamera _cam)
        {
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
            pgr_setFrameRate(pgr_cam, (double)1.0);//fps 1
            //pgr_setShutter((float)1000);
            pgr_setGainAuto(pgr_cam);
            pgr_setExposureAuto(pgr_cam);
        }

        // This function configures the example to execute image events by
        // preparing and registering an image event.
        int ConfigureImageEvents(IManagedCamera cam, ref ImageEventListener imageEventListener)
        {
            int result = 0;

            Console.WriteLine("\n\n*** CONFIGURING IMAGE EVENTS ***\n");

            try
            {
                //
                // Create image event
                //
                // *** NOTES ***
                // The class has been constructed to accept a managed camera
                // in order to allow the saving of images with the device
                // serial number.
                //
                imageEventListener = new ImageEventListener(cam);

                //
                // Register image event handler
                //
                // *** NOTES ***
                // Image events are registered to cameras. If there are
                // multiple cameras, each camera must have the image events
                // registered to it separately. Also, multiple image events may
                // be registered to a single camera.
                //
                // *** LATER ***
                // Image events must be unregistered manually. This must be
                // done prior to releasing the system and while the image
                // events are still in scope.
                //
                cam.RegisterEventHandler(imageEventListener);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }

        // This function waits for the appropriate amount of images. Notice
        // that whereas most examples actively retrieve images, the acquisition
        // of images is handled passively in this example.
        int WaitForImages(ref ImageEventListener imageEventListener)
        {
            int result = 0;

            try
            {
                //
                // Wait for images
                //
                // *** NOTES ***
                // In order to passively capture images using image events and
                // automatic polling, the main thread sleeps in increments of
                // 200 ms until 10 images have been acquired and saved.
                //
                const int sleepDuration = 1;// 200;

                while (imageEventListener.imageCnt < ImageEventListener.NumImages)
                {
                    Console.WriteLine("\t//");
                    Console.WriteLine("\t// Sleeping for {0} time slices. Grabbing images...", sleepDuration);
                    Console.WriteLine("\t//");

                    Thread.Sleep(sleepDuration);
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }

        // This functions resets the example by unregistering the image event handler.
        int ResetImageEvents(IManagedCamera cam, ref ImageEventListener imageEventListener)
        {
            int result = 0;

            try
            {
                //
                // Unregister image event handler
                //
                // *** NOTES ***
                // It is important to unregister all image event handlers from all
                // cameras they are registered to.
                //
                cam.UnregisterEventHandler(imageEventListener);

                Console.WriteLine("Image events unregistered...\n");
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }

        // This function prints the device information of the camera from the
        // transport layer; please see NodeMapInfo_CSharp example for more
        // in-depth comments on printing device information from the nodemap.
        static int PrintDeviceInfo(INodeMap nodeMap)
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
        // This function passively waits for images by calling WaitForImages().
        // Notice that this function is much shorter than the AcquireImages()
        // function of other examples. This is because most of the code has
        // been moved to the image event's OnImageEvent() method.
        int AcquireImages(
            IManagedCamera cam,
            INodeMap nodeMap,
            INodeMap nodeMapTLDevice,
            ref ImageEventListener imageEventListener)
        {
            int result = 0;

            Console.WriteLine("\n*** IMAGE ACQUISITION ***\n");

            try
            {
                // Set acquisition mode to continuous
                IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
                if (iAcquisitionMode == null || !iAcquisitionMode.IsReadable || !iAcquisitionMode.IsWritable)
                {
                    Console.WriteLine("Unable to set acquisition mode to continuous (node retrieval). Aborting...\n");
                    return -1;
                }

                IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");
                if (iAcquisitionModeContinuous == null || !iAcquisitionModeContinuous.IsReadable)
                {
                    Console.WriteLine(
                        "Unable to get acquisition mode to continuous (enum entry retrieval). Aborting...\n");
                    return -1;
                }

                iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;

                Console.WriteLine("Acquisition mode set to continuous...");

                // Begin acquiring images
                cam.BeginAcquisition();

                Console.WriteLine("Acquiring images...");

                // Retrieve images using image event handler
                result = WaitForImages(ref imageEventListener);

                // End acquisition
                cam.EndAcquisition();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;
        }

        int RunSingleCamera(IManagedCamera cam) //ManagedPGRGuid guid)
        {
            int result = 0;
            int err = 0;

            try
            {
                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();

                result = PrintDeviceInfo(nodeMapTLDevice);

                // Initialize camera
                cam.Init();

                // Retrieve GenICam nodemap
                INodeMap nodeMap = cam.GetNodeMap();

                // Configure image event handlers
                ImageEventListener imageEventListener = null;

                err = ConfigureImageEvents(cam, ref imageEventListener);
                if (err < 0)
                {
                    return err;
                }

                // Acquire images using the image event handler
                result = result | AcquireImages(cam, nodeMap, nodeMapTLDevice, ref imageEventListener);

                // Reset image event handlers
                result = result | ResetImageEvents(cam, ref imageEventListener);

                // Deinitialize camera
                cam.DeInit();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            return result;

            // Connect to a camera
            ///pgr_cam.Connect(guid);

            //pgr_PowerOnCamera(pgr_cam);

            // Get the camera information
            ///CameraInfo camInfo = pgr_cam.GetCameraInfo();
            ///PrintCameraInfo(camInfo);

            // 通常撮像
            ///pgr_Normal_settings();

            // Get embedded image info from camera
            ///EmbeddedImageInfo embeddedInfo = pgr_cam.GetEmbeddedImageInfo();

            // Enable timestamp collection	
            //if (embeddedInfo.timestamp.available == true)
            //{
            //    embeddedInfo.timestamp.onOff = true;
            //}
            // Enable exposure collection	
            //if (embeddedInfo.exposure.available == true)
            //{
            //    embeddedInfo.exposure.onOff = true;
            //}
            // Enable shutter collection	
            //if (embeddedInfo.shutter.available == true)
            //{
            //    embeddedInfo.shutter.onOff = true;
            //}
            // Enable gain collection	
            //if (embeddedInfo.gain.available == true)
            //{
            //    embeddedInfo.gain.onOff = true;
            //}
            // Enable frameCounter collection	
            //if (embeddedInfo.frameCounter.available == true)
            //{
            //    embeddedInfo.frameCounter.onOff = true;
            //}

            // Set embedded image info to camera
            ///pgr_cam.SetEmbeddedImageInfo(embeddedInfo);

            ///CameraProperty frameRateProp = pgr_cam.GetProperty(PropertyType.FrameRate);
            ///Console.WriteLine( frameRateProp.absValue );

            ///FC2Config fc2conf = pgr_cam.GetConfiguration();
            ///Console.WriteLine(fc2conf.isochBusSpeed);

            // Start capturing images
            ///pgr_cam.StartCapture(OnImageGrabbed);
        }


        /// <summary>
        /// 画像表示ルーチン
        /// </summary>
        /// <param name="capacity">画像表示用タイマールーチン</param>
        //   private void ids_timerDisplay_Tick(object sender, EventArgs e)
        //   {
        //uEye.Camera Camera = sender as uEye.Camera;
        //      if (this.States == STOP || cam == null) return;

        // IDS　表示ルーチン（多分高速）
        //Int32 s32MemID;
        //if (cam.Memory.GetActive(out s32MemID) == uEye.Defines.Status.SUCCESS && cam.IsOpened)
        //{
        //    cam.Display.DisplayImage.Set(s32MemID, u32DisplayID, uEye.Defines.DisplayRenderMode.Normal);//FitToWindow);
        //}
        //   }
        /*
              /// <summary>
                /// シャッターモードをローリングシャッターに設定
                /// </summary>
                public void set_uEye_Rolling_shutter_mode()
                {
                    if (cam.DeviceFeature.ShutterMode.IsSupported(uEye.Defines.Shuttermode.Rolling))
                    {
                        cam.DeviceFeature.ShutterMode.Set(uEye.Defines.Shuttermode.Rolling);
                    }

                }
                /// <summary>
                /// シャッターモードをグローバルシャッターに設定
                /// </summary>
                public void set_uEye_Global_shutter_mode()
                {
                    if (cam.DeviceFeature.ShutterMode.IsSupported(uEye.Defines.Shuttermode.Global))
                    {
                        cam.DeviceFeature.ShutterMode.Set(uEye.Defines.Shuttermode.Global);
                    }

                }
                public void set_PixelFormat_mono8()
                {
                    // PixelFormat MONO 8
                    uEye.Defines.ColorMode mode;
                    cam.PixelFormat.Get(out mode);
                    cam.PixelFormat.Set(uEye.Defines.ColorMode.MONO8);
                    cam.PixelFormat.Get(out mode);
                }        //
                public void set_PixelFormat_mono16()
                {
                    // PixelFormat MONO 16
                    uEye.Defines.ColorMode mode;
                    cam.PixelFormat.Get(out mode);
                    cam.PixelFormat.Set(uEye.Defines.ColorMode.MONO16);
                    cam.PixelFormat.Get(out mode);
                }        //
                // 毎フレーム呼び出し use:2015/9 
                // IDS event追加
                private void onFrameEvent(object sender, EventArgs e)
                {
                    sw.Start();
                    Int32 s32MemID;
                    cam.Memory.GetActive(out s32MemID);
                    System.IntPtr ptr;

                    //画像データをバッファにコピー
                    cam.Memory.Lock(s32MemID);
                    cam.Information.GetImageInfo(s32MemID, out imageInfo);
                    cam.Memory.ToIntPtr(s32MemID, out ptr);
                    CopyMemory(imgdata.img.ImageDataOrigin, ptr, imgdata.img.ImageSize);
                    ///CopyMemory(img_dmk.ImageDataOrigin, ptr, img_dmk.ImageSize);
                    ///Cv.Copy(img_dmk, imgdata.img);
                    if (ueye_frame_number == 0) ueye_frame_number = imageInfo.FrameNumber; //frame number初期値
                    elapsed0 = sw.ElapsedTicks; // 0.1ms

                    //3  Cv.Copy(img_dmk3, imgdata.img);
                    //Cv.Copy(img_dmk, imgdata.img);

                    //img_dmk3.ImageData = ptr;
                    //Cv.CvtColor(img_dmk3, imgdata.img, ColorConversionCodes.BgrToGray); // 遅い er:2.6%
                    //Cv.Split(img_dmk3, imgdata.img, null,null,null); // er:1.1%
                    cam.Memory.Unlock(s32MemID);

                    ++frame_id;
                    detect();
                    imgdata_push_FIFO();
                }

                // IDS event追加
                private void onFrameEvent1(object sender, EventArgs e)
                {
                    uEye.Camera Camera = sender as uEye.Camera;

                    Int32 s32MemID;
                    Camera.Memory.GetActive(out s32MemID);

                    Camera.Display.DisplayImage.Set(s32MemID, u32DisplayID, uEye.Defines.DisplayRenderMode.FitToWindow);
                    ++id;
                }
 
                // 毎フレーム呼び出し(120fr/s)
                // IDS event追加
                private void onFrameEvent2(object sender, EventArgs e)
                {
                    sw.Start();
                    double framerate0 = 0, framerate1 = 0;//, alfa_fr = 0.99;
                    Int32 s32MemID;
                    cam.Memory.GetActive(out s32MemID);
                    try
                    {
                        System.IntPtr ptr;
                        cam.Memory.Lock(s32MemID);
                        cam.Information.GetImageInfo(s32MemID, out imageInfo);
                        cam.Memory.ToIntPtr(s32MemID, out ptr);
                        CopyMemory(img_dmk3.ImageDataOrigin, ptr, img_dmk3.ImageSize);
                        Cv.CvtColor(img_dmk3, img_dmk, ColorConversionCodes.BgrToGray);
                        cam.Memory.Unlock(s32MemID);

                        //Cv.Copy(img_dmk, img2, null);

                        ++id;
                        elapsed0 = sw.ElapsedTicks;

                        // 保存用データをキューへ
                        if (ImgSaveFlag == TRUE)
                        {
                            double min_val, max_val;
                            CvPoint min_loc, max_loc;
                            int size = 15;
                            int size2x = size / 2;
                            int size2y = size / 2;
                            //int num    = 0;
                            double sigma = 3;

                            // 位置検出
                            Cv.Smooth(img_dmk, img2, SmoothType.Gaussian, size, 0, sigma, 0);
                            CvRect rect = new CvRect(1, 1, appSettings.Width - 2, appSettings.Height - 2);
                            Cv.SetImageROI(img2, rect);
                            Cv.MinMaxLoc(img2, out  min_val, out  max_val, out  min_loc, out  max_loc, null);
                            Cv.ResetImageROI(img2);
                            max_loc.X += 1; // 基準点が(1,1)のため＋１
                            max_loc.Y += 1;

                            double m00, m10, m01;
                            if (max_loc.X - size2x < 0) size2x = max_loc.X;
                            if (max_loc.Y - size2y < 0) size2y = max_loc.Y;
                            if (max_loc.X + size2x >= appSettings.Width ) size2x = appSettings.Width  - max_loc.X - 1;
                            if (max_loc.Y + size2y >= appSettings.Height) size2y = appSettings.Height - max_loc.Y - 1;
                            rect = new CvRect(max_loc.X - size2x, max_loc.Y - size2y, size, size);
                            CvMoments moments;
                            Cv.SetImageROI(img2, rect);
                            Cv.Moments(img2, out moments, false);
                            Cv.ResetImageROI(img2);
                            m00 = Cv.GetSpatialMoment(moments, 0, 0);
                            m10 = Cv.GetSpatialMoment(moments, 1, 0);
                            m01 = Cv.GetSpatialMoment(moments, 0, 1);
                            gx = max_loc.X - size2x + m10 / m00;
                            gy = max_loc.Y - size2y + m01 / m00;

                            //    Pid_Data_Send();
                            elapsed1 = sw.ElapsedTicks;
                            imgdata_push_FIFO();
                        }
                    }
                    catch (Exception ex)
                    {
                        //匿名デリゲートで表示する
                        this.Invoke(new dlgSetString(ShowRText), new object[] { richTextBox1, ex.ToString() });
                        System.Diagnostics.Trace.WriteLine(ex.Message);
                    }
                    finally
                    {
                    }
                    elapsed2 = sw.ElapsedTicks; sw.Stop(); sw.Reset();
                    // 処理速度
                    //  Double dFramerate;
                    //  cam.Timing.Framerate.GetCurrentFps(out dFramerate);
                    //  toolStripStatusLabelFramerate.Text = "Fps: " + dFramerate.ToString("00.00");

                    //framerate0 = alfa_fr * framerate1 + (1 - alfa_fr) * (Stopwatch.Frequency / (double)elapsed2);
                    //  framerate0 = ++id_fr / (this.icImagingControl1.ReferenceTimeCurrent - this.icImagingControl1.ReferenceTimeStart);
                    framerate1 = framerate0;

                    double sf = (double)Stopwatch.Frequency / 1000; //msec
                    fr_str = String.Format("ID:{0,5:D1} L0:{1,4:F2} L1:{2,4:F2} L2:{3,4:F2} fr:{4,5:F1}", id, elapsed0 / sf, elapsed1 / sf, elapsed2 / sf, framerate0);
                }
                */
    }
}

