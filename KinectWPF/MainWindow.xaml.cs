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
        private static KinectSensor sensor;
        // Color
        private WriteableBitmap colorBitmap;
        private byte[] colorPixels;
        // Profundidad
        private DepthImagePixel[] depthPixels;
        private WriteableBitmap depthBitmap;
        private byte[] depthBufferPixels;
        // Esqueleto
        private static double espesorArticulacion = 2;
        private static double espesorCentroCuerpo = 10;
        private static double espesorRectangulos = 10;
        private static Brush brushCentroPunto = Brushes.Blue;
        private static Brush brushHueso = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private static Brush brushInfrarojo = Brushes.Yellow;
        private static Pen lapizHueso = new Pen(Brushes.Green, 6);
        private static Pen lapizInfrarojoHueso = new Pen(Brushes.Gray, 1);
        private static DrawingGroup grupoDibujo;
        private static DrawingImage imagenEsqueleto;

        private static int RenderWidth { get { return sensor.ColorStream.FrameWidth; } }
        private static int RenderHeight { get { return sensor.ColorStream.FrameHeight; } }


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
                colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];
                // Brush que pintara el bitmap
                colorBitmap = new WriteableBitmap(sensor.ColorStream.FrameWidth, sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
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
                // Grupo de Dibujo
                grupoDibujo = new DrawingGroup();
                // Imagen del esqueleto
                imagenEsqueleto = new DrawingImage(grupoDibujo);
                // Destino de imagen
                imgEsqueleto.Source = imagenEsqueleto;
                // Manejador de eventos de esqueleto
                sensor.SkeletonFrameReady += SkeletoStreamReady;
                // Calibración
                sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                //sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;

                // Iniciar el sensor
                sensor.Start();
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
                    for (int i = 0; i < depthPixels.Length; ++i)
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
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, sensor.ColorStream.FrameWidth, sensor.ColorStream.FrameHeight));
                if (esqueletos.Length != 0)
                {
                    foreach (Skeleton esqueleto in esqueletos)
                    {
                        if (esqueleto.TrackingState == SkeletonTrackingState.Tracked)
                            dibujarHuesosArticulaciones(esqueleto, dc);
                        else if (esqueleto.TrackingState == SkeletonTrackingState.PositionOnly)
                            dc.DrawEllipse(brushCentroPunto, null, SkeletonPointToScreen(esqueleto.Position), espesorCentroCuerpo, espesorCentroCuerpo);
                    }
                }
                // prevent drawing outside of our render area
                grupoDibujo.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        
        private void dibujarHuesosArticulaciones(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Torso
            dibujarHueso(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            dibujarHueso(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            dibujarHueso(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            dibujarHueso(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            dibujarHueso(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            dibujarHueso(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            dibujarHueso(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);
            // Brazo izquierdo
            dibujarHueso(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            dibujarHueso(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            dibujarHueso(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);
            // Brazo derecho
            dibujarHueso(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            dibujarHueso(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            dibujarHueso(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);
            // Pierna izquierda
            dibujarHueso(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            dibujarHueso(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            dibujarHueso(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);
            // Pierna derecha
            dibujarHueso(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            dibujarHueso(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            dibujarHueso(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
            // Articulaciones
            foreach (Joint articulacion in skeleton.Joints)
            {
                Brush drawBrush = null;
                if (articulacion.TrackingState == JointTrackingState.Tracked)
                    drawBrush = brushHueso;
                else if (articulacion.TrackingState == JointTrackingState.Inferred)
                    drawBrush = brushInfrarojo;

                if (drawBrush != null)
                    drawingContext.DrawEllipse(drawBrush, null, SkeletonPointToScreen(articulacion.Position), espesorArticulacion, espesorArticulacion);
            }
        }

        
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        
        private void dibujarHueso(Skeleton skeleton, DrawingContext drawingContext, JointType typoArticulacion0, JointType typoArticulacion1)
        {
            Joint joint0 = skeleton.Joints[typoArticulacion0];
            Joint joint1 = skeleton.Joints[typoArticulacion1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked || joint1.TrackingState == JointTrackingState.NotTracked)
                return;

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred && joint1.TrackingState == JointTrackingState.Inferred)
                return;

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = lapizInfrarojoHueso;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
                drawPen = lapizHueso;

            drawingContext.DrawLine(drawPen, SkeletonPointToScreen(joint0.Position), SkeletonPointToScreen(joint1.Position));
        }
    }
}
