using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KinectWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor sensor;
        // Color
        private WriteableBitmap colorBitmap;
        private byte[] colorPixels;
        // Profundidad
        private DepthImagePixel[] depthPixels;
        private WriteableBitmap depthBitmap;
        private byte[] depthBufferPixels;
        // Esqueleto
        private double espesorArticulacion = 2;
        private double espesorCentroCuerpo = 10;
        private double espesorRectangulos = 10;
        private Brush brushCentroPunto = Brushes.Blue;
        private Brush brushHueso = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private Brush brushInfrarojo = Brushes.Yellow;
        private Pen lapizHueso = new Pen(Brushes.Green, 6);
        private Pen lapizInfrarojoHueso = new Pen(Brushes.Gray, 1);
        private DrawingGroup grupoDibujo;
        private DrawingImage imagen;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void initKinect()
        {
            //Enumarcion del sensor
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }
            if (sensor != null)
            {
                //Configurar el sensor
                // Habilitar la accion de recibir frames de color
                sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                // Habilitar la accion de recibir frames de profundidad
                sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                //Habilitar el seguimiento del cuerpo
                sensor.SkeletonStream.Enable();

                // Color                
                // Tamaño de espacio de los pixel a recibir
                colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                // Brush que pintara el bitmap
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                // Configurar la imagen a mostrar
                imgStreamColor.Source = colorBitmap;
                // Manejador de eventos del color stream
                sensor.ColorFrameReady += SensorColorFrameReady;

                // Profundidad
                // Tamaño de espacio de los pixeles de profundidad
                depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];
                // Tamaño de espacio de los pixel a recibir
                depthBufferPixels = new byte[sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                // Brush para pintar pixeles
                depthBitmap = new WriteableBitmap(sensor.DepthStream.FrameWidth, sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                // Configurar la imagen a mostrar
                imgDeth.Source = depthBitmap;
                // Manejador del evento de profundidad
                sensor.DepthFrameReady += SensorDepthFrameReady;

                // Esqueleto
                // Manejador de eventos de esqueleto
                sensor.SkeletonFrameReady += SkeletoStreamReady;

                // Iniciar el sensor
                try
                {
                    sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }

        private static void RenderBordes(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        private void SkeletoStreamReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] esqueletos = new Skeleton[0];
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    esqueletos = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(esqueletos);
                }
            }
            using (DrawingContext dc = grupoDibujo.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, sensor.ColorStream.FrameWidth,sensor.ColorStream.FrameHeight));

                if (esqueletos.Length != 0)
                {
                    foreach (Skeleton esqueleto in esqueletos)
                    {
                        RenderBordes(esqueleto, dc);

                        if (esqueleto.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(esqueleto, dc);
                        }
                        else if (esqueleto.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(esqueleto.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                grupoDibujo.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

       

        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copiar pixeles a memoria temporal
                    depthFrame.CopyDepthImagePixelDataTo(depthPixels);

                    // Maximo y minimo de profundidad
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convercion de profundidad a RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        // Obtener la profundidad del pixel
                        short depth = depthPixels[i].Depth;
                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);
                        // Escribir en azul
                        depthBufferPixels[colorPixelIndex++] = intensity;
                        // Escribir en verde
                        depthBufferPixels[colorPixelIndex++] = intensity;
                        // Escribir en rojo                        
                        depthBufferPixels[colorPixelIndex++] = intensity;
                        // No se escribe en el alfa para no alterarlo
                        ++colorPixelIndex;
                    }
                    // Dibujar en la imagen
                    depthBitmap.WritePixels(new Int32Rect(0, 0, depthBitmap.PixelWidth, depthBitmap.PixelHeight), depthBufferPixels, depthBitmap.PixelWidth * sizeof(int), 0);
                }
            }
        }

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copiar los pixeles a un array temporal
                    colorFrame.CopyPixelDataTo(colorPixels);
                    // Escribir pixel en bitmap actual
                    colorBitmap.WritePixels(new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight), colorPixels, colorBitmap.PixelWidth * sizeof(int), 0);
                }
            }
        }

        private void cmdActivar_Click(object sender, RoutedEventArgs e)
        {
            if (cmdActivar.Content.ToString() == "Activar Kinect")
            {
                initKinect();
                cmdActivar.Content = "Detener Kinect";
            }
            else
            {
                if (sensor != null)
                    sensor.Stop();
                cmdActivar.Content = "Activar Kinect";
            }
        }



    }
}
