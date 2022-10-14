using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace appSplash
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        public MessageWindow(string Name, string msgType, string msgText)
        {
            InitializeComponent();

            Title = Name;
            textBox.Text = msgText;

            switch (msgType)
            {
                case "Update":
                    button1.Content = "Sim";
                    button2.Content = "Não";
                    button1.Click += new RoutedEventHandler(Fcn_UpdateIssue);
                    button2.Click += new RoutedEventHandler(Fcn_UpdateIssue);
                    break;

                case "Error":
                    button1.Visibility = Visibility.Hidden;
                    button2.Content = "Ok";
                    button2.Click += new RoutedEventHandler(Fcn_IntegrityIssue);
                    break;
            }
        }

        private void Fcn_UpdateIssue(object sender, RoutedEventArgs e)
        {
            Hide();

            var mainForm = Application.Current.Windows.OfType<MainWindow>().First();
            if (sender == button1)
                mainForm.Fcn_UpdateProcess();
            else
                mainForm.Fcn_IntegrityCheck();
        }

        private void Fcn_IntegrityIssue(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Fcn_ClosingWindow(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
        }
    }
}
