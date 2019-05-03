using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Cvb;
using System.Runtime.InteropServices;



namespace WindowsFormsApplication2
{
    public partial class Form1 : Form
    {
        string InputData = String.Empty;
        delegate void SetTextCallback(string text); // Khai bao delegate SetTextCallBack voi tham so string
        private ICapture capture;
        private bool saveToFile;
        Mat originalImg;

        public Form1()
        {
            InitializeComponent();
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived);
            Run();
        }
        //Image<Emgu.CV.Structure.Bgr, Byte> img;
        private void Run()
        {
            try
            {
                capture = new VideoCapture(0);

                
            }
            catch (Exception Ex)
            {
                MessageBox.Show(Ex.Message);
                return;

            }
            Application.Idle += ProcessFrame;

        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            //String str = String.Format("withBall.jpg");

            //originalImg = CvInvoke.Imread(str)


            originalImg = capture.QueryFrame();
            Image<Bgr, Byte> outputImg = originalImg.ToImage<Bgr, Byte>();




            int imgWidth = originalImg.Width;
            int imgHeight = originalImg.Height;


            UMat grayImg = new UMat();

            //Convert RBG to Gray
            CvInvoke.CvtColor(originalImg, grayImg, ColorConversion.Bgr2Gray);

            //use image pyr to remove noise
            UMat pyrDown = new UMat();
            CvInvoke.PyrDown(grayImg, pyrDown);
            CvInvoke.PyrUp(pyrDown, grayImg);


            UMat binaryImg = new UMat();

            //Find Potiential Plate Region
            CvInvoke.Threshold(grayImg, binaryImg, 200, 255, ThresholdType.BinaryInv);

            Image<Gray, Byte> binaryImgG = binaryImg.ToImage<Gray, Byte>();


            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();

            int[,] hierachy = CvInvoke.FindContourTree(binaryImgG, contours, ChainApproxMethod.ChainApproxNone);

            int maxArea =0;
            int maxAreaContourIndex = 0;

            for (int idx = 0; idx < contours.Size; idx++)
            {

                //bool isChild = isChildContour(hierachy, idx);

                int numberOfChildren = GetNumberOfChildren(hierachy, idx);
                using (VectorOfPoint contour = contours[idx])
                {

                    if ( (numberOfChildren > 3))
                    {
                        

                        if(CvInvoke.ContourArea(contour) > maxArea)
                        {
                            maxAreaContourIndex = idx;
                        }

                    }
                }
            }


            Image<Gray, Byte> mask1 = new Image<Gray, Byte>(imgWidth, imgHeight);
            CvInvoke.DrawContours(mask1, contours, maxAreaContourIndex, new MCvScalar(255), -1);

            int openingFactor1 = 100;
            Image<Gray, Byte> plateMask = new Image<Gray, Byte>(imgWidth, imgHeight);

            plateMask = mask1.Erode(openingFactor1);
            plateMask = plateMask.Dilate(openingFactor1);

            CvBlobs blobs = new CvBlobs();
            CvBlobDetector blob_detector = new CvBlobDetector();
            
            //blobs.FilterByArea(10000, 1000000);
            blob_detector.Detect(plateMask, blobs);

            foreach (CvBlob blob in blobs.Values)
            {
                Rectangle r = blob.BoundingBox;

                outputImg.Draw(r, new Bgr(0, 255, 255), 4);
            }

            Image<Gray, Byte> invBinaryImgG = binaryImg.ToImage<Gray, Byte>();
            CvInvoke.BitwiseNot(invBinaryImgG, invBinaryImgG);


            Image<Gray, Byte> mask3 = plateMask.Clone();
            CvInvoke.BitwiseAnd(plateMask, invBinaryImgG, mask3);


            blob_detector.Detect(mask3, blobs);

            int patternSize = 20;
            int ballSize = 60;
            int tolerance = 10;

            int patternHigh = patternSize + tolerance;
            int patternLow = patternSize - tolerance;

            int ballHigh = ballSize + tolerance*2;
            int ballLow = ballSize - tolerance*2;

            blobs.FilterByArea(patternLow* patternLow, ballHigh* ballHigh);

            List<PointF> patternPoints = new List<PointF>();
            PointF ballPoint = new PointF();
            int numberOfPatternPointFound = 0;

            foreach (CvBlob blob in blobs.Values)
            {
                Rectangle r = blob.BoundingBox;

                if((r.Height > patternLow) && (r.Height < patternHigh) &&
                    (r.Width > patternLow) && (r.Width < patternHigh))
                {
                    outputImg.Draw(new CircleF(blob.Centroid, 2), new Bgr(0, 0, 255), 2);
                    patternPoints.Add(blob.Centroid);
                    numberOfPatternPointFound++;
                }

                if ((r.Height > ballLow) && (r.Height < ballHigh) &&
                    (r.Width > ballLow) && (r.Width < ballHigh))
                {
                    outputImg.Draw(new CircleF(blob.Centroid, 5), new Bgr(0, 0, 255), 5);
                    ballPoint = blob.Centroid;
                }
               

                
            }

            label14.Text = String.Format("{0}", numberOfPatternPointFound);
            List<PointF> sortedPatternPoints = new List<PointF>();
            // 1 for TopLeft - 2 for Top Right - 3 for Bottom Right - 4 for Bottom Left
            List<int> pointType = new List<int>(); ;

            PointF centerPoint = new PointF();
            foreach (PointF patternPoint in patternPoints)
            {
                centerPoint.X += patternPoint.X;
                centerPoint.Y += patternPoint.Y;
            }
            centerPoint.X /= numberOfPatternPointFound;
            centerPoint.Y /= numberOfPatternPointFound;
            
            x_position.Text = ballPoint.X.ToString();
            y_position.Text = ballPoint.Y.ToString();
            foreach (PointF patternPoint in patternPoints)
            {
                if ((patternPoint.X < centerPoint.X) && (patternPoint.Y < centerPoint.Y))
                {
                    sortedPatternPoints.Add(patternPoint);
                    pointType.Add(1);

                }
                else if ((patternPoint.X > centerPoint.X) && (patternPoint.Y < centerPoint.Y))
                {
                    sortedPatternPoints.Add(patternPoint);
                    pointType.Add(2);

                }
                else if ((patternPoint.X > centerPoint.X) && (patternPoint.Y > centerPoint.Y))
                {
                    sortedPatternPoints.Add(patternPoint);
                    pointType.Add(3);

                }
                else if ((patternPoint.X < centerPoint.X) && (patternPoint.Y > centerPoint.Y))
                {
                    sortedPatternPoints.Add(patternPoint);
                    pointType.Add(4);

                }
            }

            int id = 0 ;
            foreach (PointF patternPoint in sortedPatternPoints)
            {
                
                CvInvoke.PutText(outputImg,
                        String.Format("{0}", pointType[id++]),
                        new System.Drawing.Point((int)patternPoint.X, (int)patternPoint.Y),
                        FontFace.HersheyComplex,
                        1.0,
                        new Bgr(0, 255, 0).MCvScalar);
            }


              
            imageBox1.Image = outputImg;










        }

        private static int GetNumberOfChildren(int[,] hierachy, int idx)
        {
            //first child
            idx = hierachy[idx, 2];
            if (idx < 0)
                return 0;

            int count = 1;
            while (hierachy[idx, 0] > 0)
            {
                count++;
                idx = hierachy[idx, 0];
            }
            return count;
        }

        private static bool isChildContour(int[,] hierachy, int idx)
        {
            //first child
            idx = hierachy[idx, 2];
            if (idx < 0)
                return true;
            else
                return false;

           
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.DataSource = SerialPort.GetPortNames();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                serialPort1.PortName = comboBox1.Text;
                serialPort1.Open();
            }
            else
            {
                serialPort1.Close();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                button1.Text = ("Connect");
                label11.Text = ("Disconnected");
                label11.ForeColor = Color.Red;

            }
            else
            {
                button1.Text = ("Disconnect");
                label11.Text = ("Connected");
                label11.ForeColor = Color.Green;
            }
            
        }
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            InputData = serialPort1.ReadExisting();
            if (InputData != String.Empty)
            {
                // textbox1 = InputData; // ko dc dung nhu the nay vi kahc threads.

                SetText(InputData);
            }
        }
        private void SetText(string text)

        {

            if (this.textBox13.InvokeRequired)

            {

                SetTextCallback d = new SetTextCallback(SetText); // khởi tạo 1 delegate mới gọi đến SetText

                this.Invoke(d, new object[] { text });

            }

            else
            {
                this.textBox13.Text += text;
                textBox13.SelectionStart = textBox13.Text.Length;
                textBox13.ScrollToCaret();
            }

        }

        private void imageBox1_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            Mat saveImg = capture.QueryFrame();
                       
            saveImg.Save("D:/Project/NCKH/git/ballandplaterepo/saveImage.jpg");

        }

        private void groupBox7_Enter(object sender, EventArgs e)
        {

        }

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void imageBox2_Click(object sender, EventArgs e)
        {

        }

        private void textBox13_TextChanged(object sender, EventArgs e)
        {

        }

        private void label14_Click(object sender, EventArgs e)
        {

        }

        private void imageBox2_Click_1(object sender, EventArgs e)
        {

        }

        private void label14_Click_1(object sender, EventArgs e)
        {

        }

        private void label15_Click(object sender, EventArgs e)
        {

        }

        private void label18_Click(object sender, EventArgs e)
        {

        }
    }
}


