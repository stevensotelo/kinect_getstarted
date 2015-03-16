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
