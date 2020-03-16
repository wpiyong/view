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
using System.Windows.Shapes;

namespace gDiamondViewer.View
{
    /// <summary>
    /// Interaction logic for PhosParamatersWindow.xaml
    /// </summary>
    public partial class PhosParamatersWindow : Window
    {
        int _videoMode;
        public int VideoMode {
            get
            {
                return _videoMode;
            }
            set
            {
                if(_videoMode == value)
                {
                    return;
                }
                _videoMode = value;
            }
        }

        public PhosParamatersWindow()
        {
            InitializeComponent();

            txtDelay.Text = Properties.Settings.Default.PhosCamDelay.ToString();
            txtNumImages.Text = Properties.Settings.Default.PhosImageCount.ToString();
            txtSpectroDelay.Text = Properties.Settings.Default.PhosSpectDelay.ToString();
            txtNumSpectra.Text = Properties.Settings.Default.PhosSpectraCount.ToString();
            txtPhosGain.Text = Properties.Settings.Default.GainPhos.ToString();
            txtPhosShutter.Text = Properties.Settings.Default.ShutterTimePhos.ToString();
            VideoMode = Properties.Settings.Default.VideoMode;

            this.DataContext = this;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double delay = Convert.ToDouble(txtDelay.Text);
                uint count = Convert.ToUInt32(txtNumImages.Text);

                long s_delay = Convert.ToInt64(txtSpectroDelay.Text);
                uint s_count = Convert.ToUInt32(txtNumSpectra.Text);

                double gainPhos = Convert.ToDouble(txtPhosGain.Text);
                double shutterPhos = Convert.ToDouble(txtPhosShutter.Text);

                if (delay < 0 || delay > 10000)
                    //throw new Exception("Camera delay must be between 0 and 10000");

                if (count < 0 || count > 100)
                    throw new Exception("Image count must be between 0 and 100");

                if (s_delay < 0 || s_delay > 10000)
                    throw new Exception("Spectrometer delay must be between 0 and 10000");

                if (s_count < 0 || s_count > 100)
                    throw new Exception("Spectra count must be between 0 and 100");

                Properties.Settings.Default.GainPhos = gainPhos;
                Properties.Settings.Default.ShutterTimePhos = shutterPhos;

                Properties.Settings.Default.PhosCamDelay = delay;
                Properties.Settings.Default.PhosImageCount = count;
                Properties.Settings.Default.PhosSpectDelay = s_delay;
                Properties.Settings.Default.PhosSpectraCount = s_count;
                Properties.Settings.Default.VideoMode = VideoMode;
                Properties.Settings.Default.Save();
                DialogResult = true;
                this.Close();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
