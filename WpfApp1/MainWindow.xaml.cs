using System;
using System.Collections.Generic;
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
using System.IO;
using NAudio;
using NAudio.FileFormats.Mp3;
using NAudio.Wave;
using NAudio.Utils;
using System.Text;
using System.Runtime.InteropServices;
using Spectrogram;
using System.Windows.Threading;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {

        //Track track;
        string file_path = "";
        string tmp_path = @"temp.wav";
        string ttf_path = @"tempImg\\hal";
        int nbChannel;
        double sampleRate;
        int bitRate;
        double duration;
        int globIdx;
        double ymin;
        double ymax;
        double xmin;
        double xmax;
        double maxF;

        DispatcherTimer timer;
        float mwheelStep = 0;
        bool isFft;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            ymin = plotCanvas.Height;
            ymax = 0;
            xmin = 0;
            xmax = plotCanvas.Width;
            duration = 100;
            maxF = 20000;
            isFft = false;
            timer = new DispatcherTimer();
            timer.Tick += timer_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(500);
        }
        void timer_Tick(object sender, EventArgs e)
        {
            (sender as DispatcherTimer).Stop();
            Console.WriteLine("Scrolling stopped (" + mwheelStep + ")");
            mwheelStep = 0;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            updateAxis();
            globIdx = 0;
            if (Directory.Exists(@"tempImg"))
                Directory.Delete(@"tempImg", true);
            MouseWheel += MainWindow_MouseWheel;
        }

        void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            mwheelStep += e.Delta;
            maxF += mwheelStep * 500 / 120;
            maxF = (maxF < 500) ? 500 : maxF;
            maxF = (maxF > 25000) ? 25000 : maxF;
            if(isFft) FFTProc();
            timer.Stop();
            timer.Start();
        }

        void FFTProc()
        {
            try
            {
                using (NAudio.Wave.Mp3FileReader m3f = new NAudio.Wave.Mp3FileReader(file_path))
                {
                    var c = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(m3f);
                    using (var wav = new WaveOut())
                    {
                        var cc = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(m3f);
                        var inputstream = new NAudio.Wave.BlockAlignReductionStream(cc);
                        var Length = inputstream.Length;
                        bitRate = inputstream.WaveFormat.BitsPerSample;
                        nbChannel = inputstream.WaveFormat.Channels;
                        sampleRate = inputstream.WaveFormat.SampleRate;
                        using (WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(m3f))
                        {
                            this.Cursor = Cursors.Wait;

                            WaveFileWriter.CreateWaveFile(tmp_path, pcmStream);
                            long length = new FileInfo(tmp_path).Length;
                            duration = length / sampleRate / nbChannel / (bitRate / 8);

                            Console.WriteLine(nbChannel.ToString());
                            Console.WriteLine(sampleRate.ToString());
                            Console.WriteLine(duration.ToString());

                            mp3info.Text = nbChannel.ToString() + " channels,   " + sampleRate.ToString() + " Hz";

                            (double[] audio, int SampleRate) = ReadWavMono(tmp_path);

                            int fftSize = 16384;
                            int targetWidthPx = (Int32)plotCanvas.Width;
                            int stepSize = audio.Length / targetWidthPx;
                            var sg = new SpectrogramGenerator(SampleRate, fftSize: 4096, stepSize, maxFreq: maxF);
                            sg.Colormap = Colormap.Magma;
                            sg.Add(audio);
                            createDirectory();
                            string path = ttf_path + globIdx.ToString() + ".png";
                            globIdx++;
                            sg.SaveImage(path, intensity: 5, dB: true);
                            //sg.GetBitmap();
                            BitmapImage theImage = new BitmapImage(new Uri(path, UriKind.Relative));
                            //BitmapImage theImage = sg.GetBitmap() as BitmapImage;
                            ImageBrush myImageBrush = new ImageBrush(theImage);

                            plotCanvas.Background = myImageBrush.Clone();
                            updateAxis();
                            isFft = true;
                        }
                        this.Cursor = Cursors.Arrow;
                    }
                }
                //inputstream = new NAudio.Wave.BlockAlignReductionStream(cc);
                //                    waveOut.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        } 

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var od = new Microsoft.Win32.OpenFileDialog();
            if (od.ShowDialog() == true)
            {
                plotCanvas.Background = null;
                file_path = od.FileName;
                FFTProc();
            }
        }
        void createDirectory()
        {
            if (!Directory.Exists(@"tempImg"))
                Directory.CreateDirectory(@"tempImg");
        }
        public void updateAxis()
        {
            double bWidth = xmax - xmin;
            double bHeight = plotCanvas.Height;
            plotCanvas.Children.Clear();
            SolidColorBrush whitebrush = new SolidColorBrush();
            whitebrush.Color = Colors.White;
            SolidColorBrush redbrush = new SolidColorBrush();
            redbrush.Color = Colors.Red;
            int nAx;
            int yStep = 5000;
            if (maxF < 10000) yStep = 1000;
            if (maxF < 1000) yStep = 500;
            nAx = (int)maxF / yStep;
            int yStepCan =(int)yStep * (int)bHeight / (int)maxF;
            for (int i = 0; i < nAx+1; i++)
            {
                Line l = new Line();
                l.StrokeThickness = 2;
                l.Stroke = whitebrush;
                l.X1 = xmin - 7;
                l.Y1 = ymin - yStepCan * i;
                l.X2 = xmin + 3;
                l.Y2 = ymin - yStepCan * i;
                plotCanvas.Children.Add(l);

                Label legY = new Label();
                legY.Content = (i * yStep).ToString();
                legY.Width = 150;
                legY.Height = 50;
                legY.Margin = new Thickness(-60, ymin - yStepCan * i - 17, 0, 0);
                if (i == 0)
                    legY.Margin = new Thickness(-30, ymin - yStepCan * i - 17, 0, 0);
                legY.Foreground = whitebrush;
                legY.FontSize = 16;
                plotCanvas.Children.Add(legY);
            }

            Label legYx = new Label();
            legYx.Content = maxF.ToString();
            legYx.Width = 150;
            legYx.Height = 50;
            legYx.Margin = new Thickness(-60, -17, 0, 0);
            legYx.Foreground = redbrush;
            legYx.FontSize = 16;
            plotCanvas.Children.Add(legYx);

            int nBar = (Int16)duration / 30;
            double wStep = 30 * plotCanvas.Width / duration;

            for(int i = 0; i < nBar + 1; i++)
            {
                Line l = new Line();
                l.StrokeThickness = 2;
                l.Stroke = whitebrush;
                l.X1 = wStep*i;
                l.Y1 = ymin + 7;
                l.X2 = wStep*i;
                l.Y2 = ymin - 3;
                plotCanvas.Children.Add(l);

                Label legX = new Label();
                legX.Width = 150;
                legX.Height = 50;
                string min = (i / 2).ToString();
                string sec = (i % 2)==0 ? "0":"30";
                legX.Content =min+" M "+sec+" S";
                legX.Foreground = whitebrush;
                legX.FontSize = 16;
                legX.Margin = new Thickness(wStep*i-30, ymin+6, 0, 0);
                plotCanvas.Children.Add(legX);
            }

            Label legend = new Label();
            legend.Width = 150;
            legend.Height = 50;
            legend.Content = 00;
            legend.Foreground = redbrush;
            (int mi, int s) = getMSfromDur(duration);
            legend.Content = mi.ToString() + " M " + s.ToString() + " S";
            legend.FontSize = 16;
            legend.Margin = new Thickness(plotCanvas.Width- 30, plotCanvas.Height+ 6, 0, 0);
            plotCanvas.Children.Add(legend);
        }

        (int min, int sec) getMSfromDur(double dur)
        {
            int m=0;
            int s=0;
            m = (int)dur / 60;
            s = (int)dur - m * 60;
            return (m, s);
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        (double[] audio, int sampleRate) ReadWavMono(string filePath, double multiplier = 16_000)
        {
            using (var afr = new NAudio.Wave.AudioFileReader(filePath))
            {
                int sampleRate = afr.WaveFormat.SampleRate;
                int bytesPerSample = afr.WaveFormat.BitsPerSample / 8;
                int sampleCount = (int)(afr.Length / bytesPerSample);
                int channelCount = afr.WaveFormat.Channels;
                var audio = new List<double>(sampleCount);
                var buffer = new float[sampleRate * channelCount];
                int samplesRead = 0;
                while ((samplesRead = afr.Read(buffer, 0, buffer.Length)) > 0)
                    audio.AddRange(buffer.Take(samplesRead).Select(x => x * multiplier));
                return (audio.ToArray(), sampleRate);
            }

        }
        private void Window_Closed(object sender, EventArgs e)
        {
            
        }
        

    }
}
