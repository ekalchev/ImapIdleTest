using Microsoft.Win32;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImapIdleTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Test syncIdleTest = new Test(false);
        private Test asyncIdleTest = new Test(true);

        public bool StartFlashing
        {
            get { return (bool)GetValue(StartFlashingProperty); }
            set { SetValue(StartFlashingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StartFlashing.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StartFlashingProperty =
            DependencyProperty.Register("StartFlashing", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
            
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            StartNewTests();

            Application.Current.Dispatcher.InvokeAsync(async () => 
            {
                await Task.Delay(5000);
                syncIdleTest.Cancel();
                asyncIdleTest.Cancel();
            });
        }

        private void StartNewTests()
        {
            if (syncIdleTest != null)
            {
                syncIdleTest.Destroy();
                syncIdleTest.IdleCancellationFailed -= IdleTest_IdleCancellationFailed;
            }

            if (asyncIdleTest != null)
            {
                asyncIdleTest.Destroy();
                asyncIdleTest.IdleCancellationFailed -= IdleTest_IdleCancellationFailed;
            }

            syncIdleTest = new Test(false);
            asyncIdleTest = new Test(true);
            
            syncIdleTest.IdleCancellationFailed += IdleTest_IdleCancellationFailed;
            asyncIdleTest.IdleCancellationFailed += IdleTest_IdleCancellationFailed;
        }

        private void IdleTest_IdleCancellationFailed(object sender, EventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            this.WindowState = WindowState.Maximized;
            PlayFlashingAnimation();
        }

        private void PlayFlashingAnimation()
        {
            StartFlashing = true;
        }

        private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if(e.Mode == PowerModes.Resume)
            {
                Logger.Log("Computer woke up from sleep");

                syncIdleTest.Cancel();
                asyncIdleTest.Cancel();

                await Task.Delay(20 * 1000);

                StartNewTests();
            }
            else if(e.Mode == PowerModes.Suspend)
            {
                Logger.Log("Computer is going to sleep");
            }
        }
    }
}
