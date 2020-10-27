using System;
using System.Windows;

using Windows.Media.Capture;

namespace MediaCaptureWpfCustomControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            MediaCapture mediaCaptureMgr = new MediaCapture();
            MyCaptureElement.SetCaptureAsync(mediaCaptureMgr);
           // await mediaCaptureMgr.StartPreviewAsync();
        }
    }
}
