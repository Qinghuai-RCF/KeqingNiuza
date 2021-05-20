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
using KeqingNiuza.ViewModel;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HandyControl.Data;
using KeqingNiuza.View;
using System.IO;
using KeqingNiuza.Service;

namespace KeqingNiuza.View
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {

        public MainWindowViewModel ViewModel { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            DataContext = ViewModel;
        }


        private void Window_Main_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WindowState = Properties.Settings.Default.IsWindowMaximized ? WindowState.Maximized : WindowState.Normal;
            }
            catch (Exception ex)
            {
                WindowState = WindowState.Normal;
                Log.OutputLog(LogType.Warning, "Window_Main_Loaded", ex);
            }
            InitSideMenuChecked();
        }

        private void InitSideMenuChecked()
        {
            if (ViewModel.ViewContent is WishSummaryView)
            {
                SideMenu_WishSummaryView.IsChecked = true;
            }
        }

        private void Window_Main_Closed(object sender, EventArgs e)
        {
            ViewModel.SaveConfig();
            Properties.Settings.Default.IsWindowMaximized = WindowState == WindowState.Maximized;
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Maxmize_Click(object sender, RoutedEventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Normal:
                    WindowState = WindowState.Maximized;
                    break;
                case WindowState.Minimized:
                    break;
                case WindowState.Maximized:
                    WindowState = WindowState.Normal;
                    break;
            }
        }

        private void Button_Uid_Click(object sender, RoutedEventArgs e)
        {
            Popup_Uid.IsOpen = true;
        }

        private async void Button_Cloud_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CloudClient == null)
            {
                var client = await Dialog.Show(new CloudLoginDialog()).Initialize<CloudLoginDialog>(x => { }).GetResultAsync<CloudBackup.CloudClient>();
                if (client != null)
                {
                    ViewModel.CloudClient = client;
                    Popup_Cloud.IsOpen = true;
                    Growl.Success("登录成功");
                }
            }
            else
            {
                Popup_Cloud.IsOpen = true;
            }
        }

        private void Button_Logout_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists("UserData\\Account"))
            {
                File.Delete("UserData\\Account");
            }
            ViewModel.CloudClient = null;
            Popup_Cloud.IsOpen = false;
            Growl.Success("已删除保存的账号和密码");
        }


        private async void Button_Load_Click(object sender, RoutedEventArgs e)
        {
            Button_Load.IsEnabled = false;
            await ViewModel.UpdateWishData();
            Button_Load.IsEnabled = true;
        }



        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ExportExcelFile();
        }


        private async void Button_Backup_Click(object sender, RoutedEventArgs e)
        {
            Button_Backup.IsEnabled = false;
            await ViewModel.CloudBackupFileArchive();
            Button_Backup.IsEnabled = true;
        }

        private async void Button_Restore_Click(object sender, RoutedEventArgs e)
        {
            Button_Restore.IsEnabled = false;
            await ViewModel.CloudRestoreFileArchive();
            Button_Restore.IsEnabled = true;
        }

        private void RadioButton_SideMenu_Click(object sender, RoutedEventArgs e)
        {
            var radioButton = sender as RadioButton;
            ViewModel.ChangeViewContent(radioButton.Tag as string);
        }


        private async void Ellipse_Avatar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Popup_Uid.IsOpen = false;
            await ViewModel.ChangeAvatar();
        }

        private void Button_ChangeUid_Click(object sender, RoutedEventArgs e)
        {
            Grid_UserData.Visibility = Visibility.Visible;
        }

        private void Window_Main_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Normal:
                    BorderThickness = new Thickness(0);
                    break;
                case WindowState.Minimized:
                    BorderThickness = new Thickness(0);
                    break;
                case WindowState.Maximized:
                    BorderThickness = new Thickness(7);
                    break;
                default:
                    break;
            }
        }

        private void ListView_UserData_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel.ChangeUid(ListView_UserData.SelectedItem);
            Popup_Uid.IsOpen = false;
        }

        private void Popup_Uid_Closed(object sender, EventArgs e)
        {
            Grid_UserData.Visibility = Visibility.Collapsed;
            ViewModel.LoadWishDataProgress = null;
        }

        private void Button_AddUid_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddNewUid();
            Grid_UserData.Visibility = Visibility.Collapsed;
        }
    }
}
