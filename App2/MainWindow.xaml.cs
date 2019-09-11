#region Deklarasi Library yang digunakan
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.IO;
#endregion

namespace App2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Deklarasi variabel
        KinectSensor Sensing;
        private DrawingGroup GambarGrup;
        private DrawingImage GambarCitra;
        private const double KetebalanJoint = 7;
        private const float LebarRender = 480.0f;
        private const float TinggiRender = 640.0f;
        private const double KetebalanCepitan = 10;
        private readonly Pen PenaTulangSimpul = new Pen(Brushes.Black, 6);
        private readonly Pen PenaTulangJejak = new Pen(Brushes.Green, 6);
        private readonly Brush TitikPusatBrush = Brushes.Blue;
        private const double KetebalanPusatTubuh = 7;
        private readonly Brush BrushJointTerjejak = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush BrushJointSimpul = Brushes.Yellow;
        Brush BrushRangka = new SolidColorBrush(Colors.Red);
        Skeleton[] Skel = new Skeleton[0];
        Skeleton[] totalSkel = new Skeleton[6];
        private byte[] dataPixel { get; set; }
        public int IDSkelSekarang { get; set; }
        public bool ForceInfraredEmitterOff { get; set; }
        private WriteableBitmap depthBitmap;
        private DepthImagePixel[] depthPixels;
        private byte[] colorDepthPixels;
        DepthStreamViewer dataContext = new DepthStreamViewer();
        private int totalFrames;
        private int lastFrames;
        private DateTime lastTime;
        #endregion

        #region Jendela Utama
        public MainWindow()
        {
            InitializeComponent();
            Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.DataContext = dataContext;
            KinectSensor.KinectSensors.StatusChanged +=new EventHandler<StatusChangedEventArgs>(KinectSensors_StatusChanged);
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (KinectSensor.KinectSensors.Count > 0)
            {
                this.GambarGrup = new DrawingGroup();
                this.GambarCitra = new DrawingImage(this.GambarGrup);
                SkelView.Source = this.GambarCitra;
                this.Sensing = KinectSensor.KinectSensors.Where(item => item.Status == KinectStatus.Connected).FirstOrDefault();
                this.Sensing.ColorStream.Enable();
                this.Sensing.SkeletonStream.Enable();
                KinectSensors_StatusChanged(null, null);
                this.Sensing.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(Sensing_ColorFrameReady);
                this.Sensing.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(Sensing_SkeletonFrameReady);
                this.Sensing.Start();
            }
            else
            {
                MessageBox.Show("Kamera Kinect tidak terdeteksi\nCek kabel konektor");
                Application.Current.Shutdown();
                ForceInfraredEmitterOff = true;
                this.Sensing.Stop();
                return;
            }
            this.lblSudut.Content = string.Format("{0}",this.Sensing.ElevationAngle.ToString());
        }

        #endregion

        #region Depth RGB
        void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            this.Sensing = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
            if (null != this.Sensing)
            {
                InisialisasiCitraDepth(DepthImageFormat.Resolution640x480Fps30);
                this.Sensing.DepthFrameReady +=new EventHandler<DepthImageFrameReadyEventArgs>(Sensing_DepthFrameReady);
                try
                {
                    this.Sensing.Start();
                }
                catch (IOException)
                {
                    this.Sensing = null;
                }
            }
        }

        void InisialisasiCitraDepth(DepthImageFormat depthImgFormat)
        {
            this.Sensing.DepthStream.Enable(depthImgFormat);
            this.depthPixels = new DepthImagePixel[this.Sensing.DepthStream.FramePixelDataLength];
            this.colorDepthPixels = new byte[this.Sensing.DepthStream.FramePixelDataLength * sizeof(int)];
            this.depthBitmap = new WriteableBitmap(this.Sensing.DepthStream.FrameWidth, this.Sensing.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
            this.DepthViewImg.Source = this.depthBitmap;
        }

        void Sensing_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    KonversiDepthDataKeRGB(depthFrame.MinDepth, depthFrame.MaxDepth);
                    this.depthBitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                        this.colorDepthPixels,
                        this.depthBitmap.PixelWidth * sizeof(int),
                        0);
                    PerbaharuiBingkai();
                }
            }
        }

        void KonversiDepthDataKeRGB(int minDepth, int maxDepth)
        {
            int colorPixelIndex = 0;
            for (int i = 0; i < this.depthPixels.Length; ++i)
            {
                short depth = depthPixels[i].Depth;
                if (depth == 0)
                {
                    this.colorDepthPixels[colorPixelIndex++] = 0;
                    this.colorDepthPixels[colorPixelIndex++] = 255;
                    this.colorDepthPixels[colorPixelIndex++] = 255;
                }
                else
                {
                    this.colorDepthPixels[colorPixelIndex++] = (byte)depth;
                    this.colorDepthPixels[colorPixelIndex++] = (byte)(depth >= maxDepth ? 255 : depth >> 8);
                    this.colorDepthPixels[colorPixelIndex++] = (byte)(depth <= minDepth ? 255 : depth >> 10);
                }
                ++colorPixelIndex;
            }
            this.dataContext.MaxDepth = this.depthPixels.Max(p => p.Depth);
        }

        protected void PerbaharuiBingkai()
        {
            ++this.totalFrames;

            DateTime cur = DateTime.Now;
            var span = cur.Subtract(this.lastTime);
            if (span >= TimeSpan.FromSeconds(1))
            {
                int frameRate = (int)Math.Round((this.totalFrames - this.lastFrames) / span.TotalSeconds);
                this.dataContext.FrameRate = frameRate;
                this.lastFrames = this.totalFrames;
                this.lastTime = cur;
            }
        }
        #endregion

        #region Skeleton Tracking
        void Sensing_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame FrameCitra = e.OpenColorImageFrame())
            {
                if (FrameCitra == null)
                {
                    return;
                }
                else
                {
                    byte[] dataRGBku = new byte[FrameCitra.PixelDataLength];
                    FrameCitra.CopyPixelDataTo(dataRGBku);

                    this.dataPixel = new byte[FrameCitra.PixelDataLength];
                    FrameCitra.CopyPixelDataTo(this.dataPixel);
                    int langkah = FrameCitra.Width * FrameCitra.BytesPerPixel;
                    RGBDet.Source = BitmapSource.Create(
                        FrameCitra.Width, FrameCitra.Height,
                        96.0, 96.0, PixelFormats.Bgr32,
                        null, dataRGBku, FrameCitra.Width * 4);
                }
            }
        }

        private static void RenderUjung(Skeleton skeleton, DrawingContext KonteksGambar)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                KonteksGambar.DrawRectangle(Brushes.Red, null, new Rect(0, TinggiRender - KetebalanCepitan, LebarRender, KetebalanCepitan));
            }
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                KonteksGambar.DrawRectangle(Brushes.Red, null, new Rect(0, 0, KetebalanCepitan, TinggiRender));
            }
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                KonteksGambar.DrawRectangle(Brushes.Red, null, new Rect(0, 0, KetebalanCepitan, TinggiRender));
            }
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                KonteksGambar.DrawRectangle(Brushes.Red, null, new Rect(LebarRender - KetebalanCepitan, 0, KetebalanCepitan, TinggiRender));
            }
        }

        private Point SkeletonPointToScreen(SkeletonPoint SkelPoin)
        {
            DepthImagePoint PoinDep = this.Sensing.CoordinateMapper.MapSkeletonPointToDepthPoint(SkelPoin, DepthImageFormat.Resolution640x480Fps30);
            return new Point(PoinDep.X, PoinDep.Y);
        }

        void TambahGaris(Joint j1, Joint j2)
        {
            Line garisRangka = new Line();
            garisRangka.Stroke = BrushRangka;
            garisRangka.StrokeThickness = 5;
            //SkeletonPoint skelPoint = new SkeletonPoint();

            ColorImagePoint j1c = Sensing.MapSkeletonPointToColor(j1.Position, ColorImageFormat.RgbResolution640x480Fps30);
            garisRangka.X1 = j1c.X;
            garisRangka.Y1 = j1c.Y;

            ColorImagePoint j2c = Sensing.MapSkeletonPointToColor(j2.Position, ColorImageFormat.RgbResolution640x480Fps30);
            garisRangka.X2 = j2c.X;
            garisRangka.Y2 = j2c.Y;

            myCanvas.Children.Add(garisRangka);

        }

        private void GambarTulang(Skeleton skeleton, DrawingContext konteksGbr, JointType tipe0, JointType tipe1)
        {
            //Menggambar Skeleton di Canvas da Image RGB
            this.TambahGaris(skeleton.Joints[JointType.ShoulderLeft], skeleton.Joints[JointType.ElbowLeft]);
            this.TambahGaris(skeleton.Joints[JointType.ElbowLeft], skeleton.Joints[JointType.WristLeft]);
            this.TambahGaris(skeleton.Joints[JointType.WristLeft], skeleton.Joints[JointType.HandLeft]);

            this.TambahGaris(skeleton.Joints[JointType.ShoulderRight], skeleton.Joints[JointType.ElbowRight]);
            this.TambahGaris(skeleton.Joints[JointType.ElbowRight], skeleton.Joints[JointType.WristRight]);
            this.TambahGaris(skeleton.Joints[JointType.WristRight], skeleton.Joints[JointType.HandRight]);

            Joint J0 = skeleton.Joints[tipe0];
            Joint J1 = skeleton.Joints[tipe1];
            if (J0.TrackingState == JointTrackingState.NotTracked || J1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }
            if (J0.TrackingState == JointTrackingState.Inferred && J1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }
            Pen GbrPen = this.PenaTulangJejak;
            if (J0.TrackingState == JointTrackingState.Tracked && J1.TrackingState == JointTrackingState.Tracked)
            {
                GbrPen = this.PenaTulangJejak;
            }
            konteksGbr.DrawLine(GbrPen, this.SkeletonPointToScreen(J0.Position), this.SkeletonPointToScreen(J1.Position));
        }

        private void GambarTulangDanJoint(Skeleton skeleton, DrawingContext kontekGbr)
        {
            myCanvas.Children.Clear();
            this.GambarTulang(skeleton, kontekGbr, JointType.Head, JointType.ShoulderCenter);
            this.GambarTulang(skeleton, kontekGbr, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.GambarTulang(skeleton, kontekGbr, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.GambarTulang(skeleton, kontekGbr, JointType.ShoulderCenter, JointType.Spine);
            this.GambarTulang(skeleton, kontekGbr, JointType.Spine, JointType.HipCenter);
            this.GambarTulang(skeleton, kontekGbr, JointType.HipCenter, JointType.HipLeft);
            this.GambarTulang(skeleton, kontekGbr, JointType.HipCenter, JointType.HipRight);

            this.GambarTulang(skeleton, kontekGbr, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.GambarTulang(skeleton, kontekGbr, JointType.ElbowLeft, JointType.WristLeft);
            this.GambarTulang(skeleton, kontekGbr, JointType.WristLeft, JointType.HandLeft);

            this.GambarTulang(skeleton, kontekGbr, JointType.ShoulderRight, JointType.ElbowRight);
            this.GambarTulang(skeleton, kontekGbr, JointType.ElbowRight, JointType.WristRight);
            this.GambarTulang(skeleton, kontekGbr, JointType.WristRight, JointType.HandRight);

            this.GambarTulang(skeleton, kontekGbr, JointType.HipLeft, JointType.KneeLeft);
            this.GambarTulang(skeleton, kontekGbr, JointType.KneeLeft, JointType.AnkleLeft);
            this.GambarTulang(skeleton, kontekGbr, JointType.AnkleLeft, JointType.FootLeft);

            this.GambarTulang(skeleton, kontekGbr, JointType.HipRight, JointType.KneeRight);
            this.GambarTulang(skeleton, kontekGbr, JointType.KneeRight, JointType.AnkleRight);
            this.GambarTulang(skeleton, kontekGbr, JointType.AnkleRight, JointType.FootRight);

            foreach (Joint joint in skeleton.Joints)
            {
                Brush gbrBrush = null;
                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    gbrBrush = this.BrushJointTerjejak;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    gbrBrush = this.BrushJointSimpul;
                }
                if (gbrBrush != null)
                {
                    kontekGbr.DrawEllipse(gbrBrush, null, this.SkeletonPointToScreen(joint.Position), KetebalanJoint, KetebalanJoint);
                }
            }
        }

        void Sensing_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame SkelFrame = e.OpenSkeletonFrame())
            {
                if (SkelFrame == null)
                {
                    return;
                }
                Skel = new Skeleton[SkelFrame.SkeletonArrayLength];
                SkelFrame.CopySkeletonDataTo(Skel);
                SkelFrame.CopySkeletonDataTo(totalSkel);
                Skeleton firstSkel = (from trackskeleton in totalSkel
                                      where trackskeleton.TrackingState == SkeletonTrackingState.Tracked
                                      select trackskeleton).FirstOrDefault();
                Skeleton secondSkel = (from trackskeleton in totalSkel
                                       where trackskeleton.TrackingState == SkeletonTrackingState.Tracked
                                       select trackskeleton).FirstOrDefault();
                if (firstSkel == null)
                {
                    return;
                }
                if (secondSkel == null)
                {
                    return;
                }
                if (firstSkel.Joints[JointType.HandRight].TrackingState == JointTrackingState.Tracked)
                {
                    this.MapJointwithUIElement(firstSkel);
                }
                if (secondSkel.Joints[JointType.HandLeft].TrackingState == JointTrackingState.Tracked)
                {
                    this.MapJointwithUIElement(secondSkel);
                }
            }
            using (DrawingContext dc = this.GambarGrup.Open())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, LebarRender, TinggiRender));
                if (Skel.Length != 0)
                {
                    foreach (Skeleton skele in Skel)
                    {
                        RenderUjung(skele, dc);
                        if (skele.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.GambarTulangDanJoint(skele, dc);
                        }
                        else if (skele.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(this.TitikPusatBrush, null, this.SkeletonPointToScreen(skele.Position), KetebalanPusatTubuh, KetebalanPusatTubuh);
                        }
                    }
                }
                this.GambarGrup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, LebarRender, TinggiRender));
            }
        }

        private void JejakSkeletonDekat()
        {
            if (this.Sensing != null && this.Sensing.SkeletonStream != null)
            {
                if (!this.Sensing.SkeletonStream.AppChoosesSkeletons)
                {
                    this.Sensing.SkeletonStream.AppChoosesSkeletons = true;
                }

                float JarakTerdekat = 10000f;
                int IDTerdekat = 0;

                foreach (Skeleton skelt in this.Skel.Where(s => s.TrackingState != SkeletonTrackingState.NotTracked))
                {
                    if (skelt.Position.Z < JarakTerdekat)
                    {
                        IDTerdekat = skelt.TrackingId;
                        JarakTerdekat = skelt.Position.Z;
                    }
                }
                if (IDTerdekat > 0)
                {
                    this.Sensing.SkeletonStream.ChooseSkeletons(IDTerdekat);
                }
            }
        }
        #endregion

        #region Koordinat Tangan
        private void MapJointwithUIElement(Skeleton skeleton)
        {
            if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
            {
                foreach (Skeleton skelt in Skel)
                {
                    float KananZ = skeleton.Joints[JointType.HandRight].Position.Z * 100;
                    float KiriZ = skeleton.Joints[JointType.HandLeft].Position.Z * 100;

                    Point mappedPoint = this.PosisiSkala(skeleton.Joints[JointType.HandRight].Position);
                    PosisiKanan.Content = string.Format("X = {0}, Y = {1}, Z = {2}", mappedPoint.X, mappedPoint.Y, KananZ);
                    //PosisiKanan.Content = string.Format("X = {0}, Y = {1}", mappedPoint.X, mappedPoint.Y);
                    Point mappedPoint1 = this.PosisiSkala(skeleton.Joints[JointType.HandLeft].Position);
                    PosisiKiri.Content = string.Format("X = {0}, Y = {1}, Z = {2}", mappedPoint1.X, mappedPoint1.Y, KiriZ);
                    //PosisiKiri.Content = string.Format("X = {0}, Y = {1}", mappedPoint1.X, mappedPoint1.Y);

                    Canvas.SetLeft(LogoTanganKanan, mappedPoint.X);
                    Canvas.SetTop(LogoTanganKanan, mappedPoint.Y);
                    Canvas.SetLeft(LogoTanganKiri, mappedPoint1.X);
                    Canvas.SetTop(LogoTanganKiri, mappedPoint1.Y);
                }
            }
        }

        private Point PosisiSkala(SkeletonPoint skeletonPoint)
        {
            DepthImagePoint depthPoint = this.Sensing.CoordinateMapper.MapSkeletonPointToDepthPoint(skeletonPoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
        #endregion

        #region Ubah Sudut Kinect
        private void SetSudutKinect(int angleVal)
        {
            if (angleVal > this.Sensing.MinElevationAngle || angleVal < this.Sensing.MaxElevationAngle)
            {
                this.Sensing.ElevationAngle = angleVal;
            }
        }

        private int DapatkanSudutMax()
        {
            return this.Sensing.MaxElevationAngle;
        }

        private int DapatkanSudutMin()
        {
            return this.Sensing.MinElevationAngle;
        }

        private void motorSet(object sender, RoutedEventArgs e)
        {
            int angleVal;
            if (!int.TryParse(txtSetSudut.Text, out angleVal))
            {
                MessageBox.Show("Nilai sudut tidak valid");
                return;
            }
            if (this.Sensing.ElevationAngle == angleVal)
            {
                return;
            }
            if (angleVal > this.Sensing.MaxElevationAngle)
            {
                MessageBox.Show(string.Format("Kamera Kinect tidak bisa melebihi sudut {0}", this.Sensing.MaxElevationAngle));
            }
            if (angleVal < this.Sensing.MinElevationAngle)
            {
                MessageBox.Show(string.Format("Kamera Kinect tidak bisa kurang dari sudut {0}", this.Sensing.MinElevationAngle));
            }
            try
            {
                this.Sensing.ElevationAngle = angleVal;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally 
            {
                this.lblSudut.Content = string.Format("{0}", this.Sensing.ElevationAngle.ToString());
            }
        }

        private void motorTurun(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Sensing.ElevationAngle -= 3;
                this.lblSudut.Content = string.Format("{0}", this.Sensing.ElevationAngle.ToString());
            }
            catch(ArgumentOutOfRangeException aorExc)
            {
                MessageBox.Show(aorExc.Message);
            }
        }

        private void motorNaik(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Sensing.ElevationAngle += 3;
                this.lblSudut.Content = string.Format("{0}", this.Sensing.ElevationAngle.ToString());
            }
            catch (ArgumentOutOfRangeException aor)
            {
                MessageBox.Show(aor.Message);
            }
        }
        #endregion
    }
}
