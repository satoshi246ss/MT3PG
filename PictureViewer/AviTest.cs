using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using OpenCvSharp;
//using OpenCvSharp.Blob;


namespace MT3
{
    partial class Form1 //AviTest
    {
        private BackgroundWorker AviTestworker;
        private VideoCapture capture;
        Mat AviTest_image;
        double AviTest_fps = 4;
        //string fn = @"D:\image_data\20151018\20151018_175329_491_10.avi";
        //string AviTest_fn = @"20151018_224542_615_10.avi";

        public void AviTest_init()
        {
            AviTestworker = new BackgroundWorker();
            AviTestworker.WorkerReportsProgress = true;
            AviTestworker.WorkerSupportsCancellation = true;
            AviTestworker.DoWork += new DoWorkEventHandler(AviTestworker_DoWork);
            AviTestworker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);

            //capture = new VideoCapture(AviTest_fn);
            //Cv.NamedWindow("AviTest_window");
            //Cv.ShowImage("window", img);
            //Cv2.ImShow("dst", imgMatches); // for mat
        }

        public void AviTest_run()
        {
            AviTestworker.RunWorkerAsync(); 
        }

        private void AviTestworker_DoWork(object sender, DoWorkEventArgs e)
        {
            appSettings.TestMode = true;
            BackgroundWorker bw = (BackgroundWorker)sender;
            using (VideoCapture capture = new VideoCapture(appSettings.TestFname))
            {
                int interval = (int)(1000 / AviTest_fps);
                while ( capture.Read(AviTest_image) )
                {
                    if (bw.CancellationPending)
                    {
                        appSettings.TestMode = false;
                        e.Cancel = true;
                        break;
                    }

                    for (int i = 0; i < 200; ++i)
                    {
                        image_data_write(ref AviTest_image, i, 0, 0);
                    }

                    bw.ReportProgress(0, AviTest_image);
                    System.Threading.Thread.Sleep(interval);
                }
            }
            appSettings.TestMode = false;
        }

        public void image_data_write(ref Mat img, int x, int y, int val) // 遅い
        {
            if (x >= 0 && x < img.Width && y >= 0 && y < img.Height)
            {
                img.Set( y, x, val );
            }
        }

    }
}
