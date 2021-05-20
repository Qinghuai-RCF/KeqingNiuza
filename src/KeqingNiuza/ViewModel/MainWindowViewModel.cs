using HandyControl.Controls;
using HandyControl.Tools.Extension;
using KeqingNiuza.CloudBackup;
using KeqingNiuza.Model;
using KeqingNiuza.Service;
using KeqingNiuza.View;
using KeqingNiuza.Wish;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using static KeqingNiuza.Common.Const;

namespace KeqingNiuza.ViewModel
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindowViewModel()
        {
            bool first = false;
            _viewContentList = new List<object>();
            Directory.CreateDirectory(".\\UserData");
            if (File.Exists("UserData\\Config.json"))
            {
                try
                {
                    LoadConfig();
                    ChangeViewContent("WishSummaryView");
                }
                catch (Exception ex)
                {
                    ViewContent = new ErrorView(ex);
                    Log.OutputLog(LogType.Warning, "MainWindowViewModel constructor", ex);
                }
            }
            else
            {
                UserDataList = new List<UserData>();
                ViewContent = new WelcomeView();
                first = true;
            }
            if (File.Exists("resource\\ShowUpdateLog"))
            {
                ViewContent = new UpdateLogView();
                File.Delete("resource\\ShowUpdateLog");
            }
            _timer = new System.Timers.Timer(500);
            _timer.AutoReset = false;
            _timer.Elapsed += LoadCloudAccount;
#if !DEBUG||TestCDN
            if (!first)
            {
                _timer.Elapsed += TestUpdate;
            }
#endif
            _viewContentList = new List<object>();
            _timer.Start();
        }



        private readonly List<object> _viewContentList;

        public static List<WishData> WishDataList;

        private readonly System.Timers.Timer _timer;

        private bool IsAutoUpdate => Properties.Settings.Default.IsAutoUpdate;

        #region ControlProperty


        private object _ViewContent;
        public object ViewContent
        {
            get { return _ViewContent; }
            set
            {
                _ViewContent = value;
                OnPropertyChanged();
            }
        }


        private string _LoadWishDataProgress;
        public string LoadWishDataProgress
        {
            get { return _LoadWishDataProgress; }
            set
            {
                _LoadWishDataProgress = value;
                OnPropertyChanged();
            }
        }


        private List<UserData> _UserDataList;
        public List<UserData> UserDataList
        {
            get { return _UserDataList; }
            set
            {
                _UserDataList = value;
                OnPropertyChanged();
            }
        }

        public static UserData GetSelectedUserData()
        {
            return _SelectedUserData;
        }


        private static UserData _SelectedUserData;
        public UserData SelectedUserData
        {
            get { return _SelectedUserData; }
            set
            {
                _SelectedUserData = value;
                SelectedUserData_Changed();
                OnPropertyChanged();
            }
        }

        private void SelectedUserData_Changed()
        {
            try
            {
                WishDataList = LocalWishLogLoader.Load(SelectedUserData.WishLogFile);
            }
            catch (Exception ex)
            {
                Growl.Warning(ex.Message);
                Log.OutputLog(LogType.Warning, "SelectedUserData_Changed", ex);
            }
        }

        private CloudClient _CloudClient;
        public CloudClient CloudClient
        {
            get { return _CloudClient; }
            set
            {
                _CloudClient = value;
                OnPropertyChanged();
            }
        }


        #endregion




        private async void TestUpdate(object sender, System.Timers.ElapsedEventArgs e)
        {
            var updater = new Updater();
            bool callUpdate = false;
            bool showMessage = false;
            try
            {
                var updateInfo = await updater.GetUpdateInfo();
                if (updateInfo.IsNeedUpdate)
                {
                    // 设置为自动更新，或者仅修订号不同，则自动下载
                    if (IsAutoUpdate || updateInfo.IsAutoUpdate)
                    {
                        await updater.PrepareUpdateFiles();
                        callUpdate = true;
                        Log.OutputLog(LogType.Info, "Update files prepare finished");
                        if (!updateInfo.IsAutoUpdate)
                        {
                            showMessage = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.OutputLog(LogType.Error, "TestUpdate_AllFile", ex);
            }
            try
            {
                var result = await updater.UpdateResourceFiles();
                if (result)
                {
                    callUpdate = true;
                }
            }
            catch (Exception ex)
            {
                Log.OutputLog(LogType.Info, "TestUpdate_Resource", ex);
            }
            if (callUpdate)
            {
                updater.CallUpdateWhenExit();
            }
            if (showMessage)
            {
                Growl.Success("更新文件准备完毕");
            }
        }


        private void LoadConfig()
        {
            var setting = JsonSerializer.Deserialize<Config>(File.ReadAllText("UserData\\Config.json"), JsonOptions);
            UserDataList = setting.UserDataList ?? new List<UserData>();
            SelectedUserData = UserDataList.Find(x => x.Uid == setting.LatestUid) ?? UserDataList.First();
        }


        public void SaveConfig()
        {
            if (SelectedUserData == null)
            {
                return;
            }
            var setting = new Config()
            {
                LatestUid = SelectedUserData.Uid,
                UserDataList = UserDataList.Distinct().OrderBy(x => x.Uid).ToList(),
            };
            File.WriteAllText("UserData\\Config.json", JsonSerializer.Serialize(setting, JsonOptions));
        }


        public async void LoadCloudAccount(object sender, System.Timers.ElapsedEventArgs e)
        {
            await Task.Run(() =>
            {
                if (File.Exists("UserData\\Account"))
                {
                    try
                    {
                        CloudClient = CloudClient.GetClientFromEncryption();
                    }
                    catch (Exception ex)
                    {
                        File.Delete("UserData\\Account");
                        Growl.Warning("无法解密云备份账户文件，已删除");
                        Log.OutputLog(LogType.Error, "DecrypeCloudClient", ex);
                    }
                }
            });
        }

        public void AddNewUid()
        {
            SelectedUserData = new UserData
            {
                Uid = 0
            };
        }


        public async Task UpdateWishData()
        {
            try
            {
                bool skipLoadGenshinLogFile = false;
                if (SelectedUserData != null)
                {
                    if (!string.IsNullOrEmpty(SelectedUserData.Url))
                    {
                        if (await IsUrlTimeout(SelectedUserData.Url))
                        {
                            skipLoadGenshinLogFile = true;
                            await LoadDataFromUrl(SelectedUserData.Url);
                            ReloadViewContent();
                            ChangeViewContent("WishSummaryView");
                        }
                        else
                        {
                            Growl.Warning("原 Url 已过期");
                        }
                    }
                }
                if (!skipLoadGenshinLogFile)
                {
                    var url = GenshinLogLoader.FindUrlFromLogFile();
                    await LoadDataFromUrl(url);
                    ReloadViewContent();
                    ChangeViewContent("WishSummaryView");
                }
                LoadWishDataProgress = "加载完成";
            }
            catch (Exception ex)
            {
                Growl.Warning(ex.Message);
                LoadWishDataProgress = "加载过程中遇到错误";
                Log.OutputLog(LogType.Error, "UpdateWishData", ex);
            }
        }


        /// <summary>
        /// 检测url是否过期
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task<bool> IsUrlTimeout(string url)
        {
            try
            {
                var exporter = new WishLogExporter(url);
                var uid = await exporter.GetUidByUrl();
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message == "authkey timeout")
                {
                    return false;
                }
                else
                {
                    throw ex;
                }
            }
        }

        private async Task LoadDataFromUrl(string url)
        {
            var exporter = new WishLogExporter(url);
            exporter.ProgressChanged += WishLoadExporter_ProgressChanged;
            var uid = await exporter.GetUidByUrl();
            var userData = UserDataList.Find(x => x.Uid == uid);
            List<WishData> oldList, newList;
            if (userData == null)
            {
                oldList = new List<WishData>();
                newList = await exporter.GetAllLog();
                userData = new UserData()
                {
                    Uid = uid,
                    Url = url,
                    LastUpdateTime = DateTime.Now
                };
                UserDataList.Add(userData);
            }
            else
            {
                if (File.Exists(userData?.WishLogFile))
                {
                    oldList = LocalWishLogLoader.Load(userData.WishLogFile);
                    newList = await exporter.GetAllLog(lastId: oldList.Last().Id);
                }
                else
                {
                    oldList = new List<WishData>();
                    newList = await exporter.GetAllLog();
                }
                userData.Url = url;
                userData.LastUpdateTime = DateTime.Now;
            }
            var list = newList.Union(oldList).ToList();
            File.WriteAllText(userData.WishLogFile, JsonSerializer.Serialize(list, JsonOptions));
            SelectedUserData = userData;
            SaveConfig();
        }

        private void WishLoadExporter_ProgressChanged(object sender, string e)
        {
            LoadWishDataProgress = e;
        }


        /// <summary>
        /// 导出Excel文件
        /// </summary>
        public void ExportExcelFile()
        {
            if (SelectedUserData == null)
            {
                Growl.Info("请选择Uid");
                return;
            }
            if (!File.Exists(SelectedUserData.WishLogFile))
            {
                Growl.Warning("祈愿记录文件不存在");
                return;
            }
            var json = File.ReadAllText(SelectedUserData.WishLogFile);
            var data = JsonSerializer.Deserialize<List<WishData>>(json, JsonOptions);
            var analyzer = new PieChartAnalyzer(data);
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = $"原神祈愿记录_{SelectedUserData.Uid}.xlsx",
                Filter = "Excel worksheets|*.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    analyzer.ExportExcelFile(saveFileDialog.FileName);
                    Growl.Success("导出成功");
                }
                catch (Exception ex)
                {
                    Growl.Error(ex.Message);
                    Log.OutputLog(LogType.Error, "ExportExcelFile", ex);
                }
            }
        }


        /// <summary>
        /// 以压缩包形式备份用户文件
        /// </summary>
        /// <returns></returns>
        public async Task CloudBackupFileArchive()
        {
            try
            {
                await CloudClient.BackupFileArchive();
                Growl.Success("备份成功");
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                Log.OutputLog(LogType.Warning, "CloudBackupFileArchive", ex);
            }
        }


        public async Task CloudRestoreFileArchive()
        {
            try
            {
                await CloudClient.RestoreFileArchive();
                LoadConfig();
                ChangeViewContent("WishSummaryView");
                Growl.Success("还原成功");
            }
            catch (Exception ex)
            {
                Growl.Warning(ex.Message);
                Log.OutputLog(LogType.Warning, "CloudBackupFileArchive", ex);
            }
        }


        /// <summary>
        /// 更换页面内容
        /// </summary>
        /// <param name="className">页面类名</param>
        public void ChangeViewContent(string className)
        {
            var assembly = Assembly.GetAssembly(GetType());
            var type = assembly.GetType("KeqingNiuza.View." + className);
            if (ViewContent?.GetType().Name != type.Name)
            {
                if (_viewContentList.Any(x => x.GetType().Name == type.Name))
                {
                    ViewContent = _viewContentList.First(x => x.GetType().Name == type.Name);
                }
                else
                {
                    try
                    {
                        ViewContent = assembly.CreateInstance(type.FullName);
                        _viewContentList.Add(ViewContent);
                    }
                    catch (Exception ex)
                    {
                        ViewContent = new ErrorView(ex);
                        Log.OutputLog(LogType.Warning, "ChangeViewContent", ex);
                    }
                }
            }
        }


        /// <summary>
        /// 重新加载内容页面
        /// </summary>
        public void ReloadViewContent()
        {
            var type = ViewContent.GetType();
            try
            {
                ViewContent = type.Assembly.CreateInstance(type.FullName);
                foreach (var content in _viewContentList)
                {
                    var d = content as MidiView;
                    d?.ViewModel?.Dispose();
                }
                _viewContentList.Clear();
                _viewContentList.Add(ViewContent);

            }
            catch (Exception ex)
            {
                ViewContent = new ErrorView(ex);
                Log.OutputLog(LogType.Warning, "ReloadViewContent", ex);
            }

        }



        public async Task ChangeAvatar()
        {
            var result = await Dialog.Show(new ChangeAvatarDialog()).Initialize<ChangeAvatarDialog>(x => { }).GetResultAsync<string>();
            if (!string.IsNullOrEmpty(result))
            {
                if (SelectedUserData != null)
                {
                    SelectedUserData.Avatar = result;
                }
                else
                {
                    Growl.Info("请先加载数据");
                }
            }
        }

        public void ChangeUid(object userData)
        {
            SelectedUserData = userData as UserData;
            ReloadViewContent();
        }



    }
}
