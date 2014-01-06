using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Phone.Shell;
using System.Windows.Threading;
using System.Windows.Controls;


namespace AlmaEsami
{
    class LoadingHandler
    {
        private ProgressIndicator progressIndicator;
        private DispatcherTimer timer;
        private System.Windows.DependencyObject window;
        private string defaultError;
        
        public LoadingHandler(System.Windows.DependencyObject window, ProgressIndicator progressIndicator, DispatcherTimer dispatcherTimer, string defaultError = null)
        {
            this.window = window;
            this.defaultError = defaultError;

            SystemTray.SetIsVisible(this.window, true);
            SystemTray.SetOpacity(this.window, 0);
            //setting up progress bar
            this.progressIndicator = progressIndicator;
            this.progressIndicator.IsVisible = false; //hidden until BeginLoading is called
            this.progressIndicator.Text = "Attendere...";
            this.progressIndicator.Value = 0;
            this.progressIndicator.IsIndeterminate = false;
            SystemTray.SetProgressIndicator(this.window, progressIndicator);

            //timer needed from progressIndicator
            timer = dispatcherTimer;
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += new EventHandler(timer_Tick);
        }

        private void timer_Tick(object Sender, EventArgs e)
        {
            window.Dispatcher.BeginInvoke(() =>
            {
                if (progressIndicator.Value >= 1)
                    progressIndicator.Value = 0;
                else
                    progressIndicator.Value += 0.1;
            });
        }

        public void BeginLoading(Button btn = null)
        {
            window.Dispatcher.BeginInvoke(() =>
            {
                progressIndicator.IsVisible = true;
                progressIndicator.Value = 0;
                timer.Start();
                if (btn != null)
                    btn.IsEnabled = false;
            });
        }

        public void EndLoading(Button btn = null)
        {
            window.Dispatcher.BeginInvoke(() =>
            {
                timer.Stop();
                progressIndicator.IsVisible = false;
                progressIndicator.Value = 0;
                if (btn != null)
                    btn.IsEnabled = true;
            });
        }

        public string ErrorHandler(string errorDetail, Button btn = null)
        {
            EndLoading(btn);
            return defaultError + errorDetail;
        }

    }
}
