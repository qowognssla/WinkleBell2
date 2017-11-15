using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WinkleBell2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SuspendingEventHandler appSuspendEventHandler;
        private EventHandler<Object> appResumeEventHandler;

        private ObservableCollection<DeviceListEntry> listOfDevices;

        private Dictionary<DeviceWatcher, string> mapDeviceWatchersToDeviceSelector;
        private Boolean watchersSuspended;
        private Boolean watchersStarted;

        private Boolean isAllDevicesEnumerated;

        private bool isActive = false;
        private bool PlayisActive = false;
        public static MainPage Current;

        private CancellationTokenSource ReadCancellationTokenSource;
        private CancellationTokenSource WriteCancellationTokenSource;
        private Object WriteCancelLock = new Object();
        private Object ReadCancelLock = new Object();
        DataWriter DataWriteObject = null;
        DataReader DataReaderObject = null;

        private MediaPlayer[] Sounds;

        public MainPage()
        {
            this.InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            Current = this;

            listOfDevices = new ObservableCollection<DeviceListEntry>();
            mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, String>();
            watchersStarted = false;
            watchersSuspended = false;
            isAllDevicesEnumerated = false;


            Uri pathUri = new Uri("ms-appx:///Assets/Drum/" + ((TextBlock)mediaCombobox.SelectedItem).Text + ".mp3");
            mediaPlayer.Source = MediaSource.CreateFromUri(pathUri);
            Uri pathUri2 = new Uri("ms-appx:///Assets/Drum/" + ((TextBlock)mediaCombobox1.SelectedItem).Text + ".mp3");
            mediaPlayer2.Source = MediaSource.CreateFromUri(pathUri2);
            Uri pathUri3 = new Uri("ms-appx:///Assets/Drum/" + ((TextBlock)mediaCombobox2.SelectedItem).Text + ".mp3");
            mediaPlayer3.Source = MediaSource.CreateFromUri(pathUri3);

            mediaPlayer.MediaPlayer.Volume = 0.4;
            mediaPlayer.MediaPlayer.IsLoopingEnabled = true;
            mediaPlayer.MediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged;

            mediaPlayer2.MediaPlayer.Volume = 0.4;
            mediaPlayer2.MediaPlayer.IsLoopingEnabled = true;
            mediaPlayer2.MediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged2;

            mediaPlayer3.MediaPlayer.Volume = 0.4;
            mediaPlayer3.MediaPlayer.IsLoopingEnabled = true;
            mediaPlayer3.MediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged3;
        }

        private void MediaPlayer_VolumeChanged(MediaPlayer sender, object args)
        {
            if (sender.Volume == 0)
            {
                sender.Pause();
                sender.PlaybackSession.Position = new TimeSpan(0);
            }
            else if (sender.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
            {
                sender.PlaybackSession.Position = new TimeSpan(0);
                sender.Play();
            }
        }
        private void MediaPlayer_VolumeChanged2(MediaPlayer sender, object args)
        {
            if (sender.Volume == 0)
            {
                sender.Pause();
                sender.PlaybackSession.Position = new TimeSpan(0);
            }
            else if (sender.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
            {
                sender.PlaybackSession.Position = new TimeSpan(0);
                sender.Play();
            }
        }
        private void MediaPlayer_VolumeChanged3(MediaPlayer sender, object args)
        {
            if (sender.Volume == 0)
            {
                sender.Pause();
                sender.PlaybackSession.Position = new TimeSpan(0);
            }
            else if (sender.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
            {
                sender.PlaybackSession.Position = new TimeSpan(0);
                sender.Play();
            }
        }


        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            if (EventHandlerForDevice.Current.IsDeviceConnected || (EventHandlerForDevice.Current.IsEnabledAutoReconnect && EventHandlerForDevice.Current.DeviceInformation != null))
            {
                UpdateConnectDisconnectButtonsAndList(false);

                EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
                EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;
            }
            else
            {
                UpdateConnectDisconnectButtonsAndList(true);
            }

            StartHandlingAppEvents();

            InitializeDeviceWatchers();
            StartDeviceWatchers();

            DeviceListSource.Source = listOfDevices;

            Sounds = new MediaPlayer[16];

            string Mode = ((TextBlock)SoundModeCombo.SelectedItem).Text;

            for (int i = 0; i < Sounds.Length; i++)
            {
                Sounds[i] = new MediaPlayer();
                Uri uri = new Uri("ms-appx:///Assets/" + Mode + "/sound" + i + ".mp3");
                Sounds[i].Source = MediaSource.CreateFromUri(uri);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs eventArgs)
        {
            StopDeviceWatchers();
            StopHandlingAppEvents();

            EventHandlerForDevice.Current.OnDeviceConnected = null;
            EventHandlerForDevice.Current.OnDeviceClose = null;
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;

            if ((string)Btn.Content == "Start")
            {
                PlayisActive = true;
                Btn.Content = "Stop";

                mediaPlayer.MediaPlayer.Play();
                mediaPlayer2.MediaPlayer.Play();
                mediaPlayer3.MediaPlayer.Play();
            }
            else
            {
                PlayisActive = false;
                Btn.Content = "Start";

                mediaPlayer.MediaPlayer.Pause();
                mediaPlayer2.MediaPlayer.Pause();
                mediaPlayer3.MediaPlayer.Pause();
                mediaPlayer.MediaPlayer.PlaybackSession.Position = new TimeSpan(0);
                mediaPlayer2.MediaPlayer.PlaybackSession.Position = new TimeSpan(0);
                mediaPlayer3.MediaPlayer.PlaybackSession.Position = new TimeSpan(0);
            }
        }

        private void PlayingSound(int Index)
        {
            try
            {
                Sounds[Index].PlaybackSession.Position = new TimeSpan(0);
                Sounds[Index].Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }

        private void mediaCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = sender as ComboBox;

            if (mediaPlayer != null && mediaPlayer2 != null && mediaPlayer3 != null)
            {
                if (box.Name == "mediaCombobox")
                {
                    Uri pathUri = new Uri("ms-appx:///Assets/Drum/" + ((TextBlock)mediaCombobox.SelectedItem).Text + ".mp3");
                    mediaPlayer.Source = MediaSource.CreateFromUri(pathUri);
                }
                if (box.Name == "mediaCombobox2")
                {
                    Uri pathUri2 = new Uri("ms-appx:///Assets/Drum/" + ((TextBlock)mediaCombobox1.SelectedItem).Text + ".mp3");
                    mediaPlayer2.Source = MediaSource.CreateFromUri(pathUri2);
                }
                if (box.Name == "mediaCombobox3")
                {
                    Uri pathUri3 = new Uri("ms-appx:///Assets/Drum/" + ((TextBlock)mediaCombobox2.SelectedItem).Text + ".mp3");
                    mediaPlayer3.Source = MediaSource.CreateFromUri(pathUri3);
                }
            }
        }

        private void SetButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //WriteButton_Click();
            PlayingSound(0);
        }

        private async void ConnectBtn_Clicked(Object sender, RoutedEventArgs eventArgs)
        {
            var selection = ConnectDevices.SelectedItems;
            DeviceListEntry entry = null;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;

                if (entry != null)
                {
                    EventHandlerForDevice.CreateNewEventHandlerForDevice();
                    EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
                    EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;
                    Boolean openSuccess = await EventHandlerForDevice.Current.OpenDeviceAsync(entry.DeviceInformation, entry.DeviceSelector);
                    UpdateConnectDisconnectButtonsAndList(!openSuccess);
                }
            }
        }

        private void DisconnectBtn_Clicked(Object sender, RoutedEventArgs eventArgs)
        {
            isActive = false;
            var selection = ConnectDevices.SelectedItems;
            DeviceListEntry entry = null;

            EventHandlerForDevice.Current.IsEnabledAutoReconnect = false;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;

                if (entry != null)
                {
                    EventHandlerForDevice.Current.CloseDevice();
                }
            }
            UpdateConnectDisconnectButtonsAndList(true);
        }


        private void InitializeDeviceWatchers()
        {
            var deviceSelector = SerialDevice.GetDeviceSelector();
            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);
            AddDeviceWatcher(deviceWatcher, deviceSelector);
        }

        private void StartHandlingAppEvents()
        {
            appSuspendEventHandler = new SuspendingEventHandler(this.OnAppSuspension);
            appResumeEventHandler = new EventHandler<Object>(this.OnAppResume);

            App.Current.Suspending += appSuspendEventHandler;
            App.Current.Resuming += appResumeEventHandler;
        }

        private void StopHandlingAppEvents()
        {
            App.Current.Suspending -= appSuspendEventHandler;
            App.Current.Resuming -= appResumeEventHandler;
        }

        private void AddDeviceWatcher(DeviceWatcher deviceWatcher, String deviceSelector)
        {
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(this.OnDeviceAdded);
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(this.OnDeviceRemoved);
            deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(this.OnDeviceEnumerationComplete);

            mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
        }

        private void StartDeviceWatchers()
        {
            watchersStarted = true;
            isAllDevicesEnumerated = false;

            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status != DeviceWatcherStatus.Started)
                    && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Start();
                }
            }
        }

        private void StopDeviceWatchers()
        {
            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status == DeviceWatcherStatus.Started)
                    || (deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Stop();
                }
            }
            ClearDeviceEntries();

            watchersStarted = false;
        }

        private void AddDeviceToList(DeviceInformation deviceInformation, String deviceSelector)
        {
            var match = FindDevice(deviceInformation.Id);
            if (match == null)
            {
                match = new DeviceListEntry(deviceInformation, deviceSelector);
                listOfDevices.Add(match);
            }
        }

        private void RemoveDeviceFromList(String deviceId)
        {
            var deviceEntry = FindDevice(deviceId);
            listOfDevices.Remove(deviceEntry);
        }

        private void ClearDeviceEntries()
        {
            listOfDevices.Clear();
        }

        private DeviceListEntry FindDevice(String deviceId)
        {
            if (deviceId != null)
            {
                foreach (DeviceListEntry entry in listOfDevices)
                {
                    if (entry.DeviceInformation.Id == deviceId)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        private void OnAppSuspension(Object sender, SuspendingEventArgs args)
        {
            if (watchersStarted)
            {
                watchersSuspended = true;
                StopDeviceWatchers();
            }
            else
            {
                watchersSuspended = false;
            }
        }

        private void OnAppResume(Object sender, Object args)
        {
            if (watchersSuspended)
            {
                watchersSuspended = false;
                StartDeviceWatchers();
            }
        }

        private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            await Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {


                    RemoveDeviceFromList(deviceInformationUpdate.Id);
                }));
        }

        private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            await Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
                }));
        }
        private async void OnDeviceEnumerationComplete(DeviceWatcher sender, Object args)
        {
            await Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    isAllDevicesEnumerated = true;

                    if (EventHandlerForDevice.Current.IsDeviceConnected)
                    {
                        SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

                        ButtonDisconnectFromDevice.Content = "Disconnect";

                    }
                    else if (EventHandlerForDevice.Current.IsEnabledAutoReconnect && EventHandlerForDevice.Current.DeviceInformation != null)
                    {
                        ButtonDisconnectFromDevice.Content = "Disconnect";
                    }
                }));
        }

        private void OnDeviceConnected(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            // Find and select our connected device
            if (isAllDevicesEnumerated)
            {
                SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);
                ButtonDisconnectFromDevice.Content = "Disconnect";
            }

            if (EventHandlerForDevice.Current.Device.PortName != "")
            {
                EventHandlerForDevice.Current.Device.Parity = SerialParity.None;
                EventHandlerForDevice.Current.Device.StopBits = SerialStopBitCount.One;
                EventHandlerForDevice.Current.Device.Handshake = SerialHandshake.None;
                EventHandlerForDevice.Current.Device.DataBits = 8;
                EventHandlerForDevice.Current.Device.BaudRate = 115200;
                ResetReadCancellationTokenSource();
                ResetWriteCancellationTokenSource();

                isActive = true;
                EventHandlerForDevice.Current.Device.ReadTimeout = new System.TimeSpan(10 * 10000);
                ReadButton_Click();
            }
        }
        private async void OnDeviceClosing(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            await Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    if (ButtonDisconnectFromDevice.IsEnabled && EventHandlerForDevice.Current.IsEnabledAutoReconnect)
                    {
                        ButtonDisconnectFromDevice.Content = "Disconnect";
                    }
                }));
        }

        private void SelectDeviceInList(String deviceIdToSelect)
        {
            ConnectDevices.SelectedIndex = -1;

            for (int deviceListIndex = 0; deviceListIndex < listOfDevices.Count; deviceListIndex++)
            {
                if (listOfDevices[deviceListIndex].DeviceInformation.Id == deviceIdToSelect)
                {
                    ConnectDevices.SelectedIndex = deviceListIndex;
                    break;
                }
            }
        }

        private void UpdateConnectDisconnectButtonsAndList(Boolean enableConnectButton)
        {
            ButtonConnectToDevice.IsEnabled = enableConnectButton;
            ButtonDisconnectFromDevice.IsEnabled = !ButtonConnectToDevice.IsEnabled;
           // SetBtn.IsEnabled = !ButtonConnectToDevice.IsEnabled;
            ConnectDevices.IsEnabled = ButtonConnectToDevice.IsEnabled;
        }

        /////////////////////////////////////
        // Read Write Page

        public void Dispose()
        {
            if (ReadCancellationTokenSource != null)
            {
                ReadCancellationTokenSource.Dispose();
                ReadCancellationTokenSource = null;
            }

            if (WriteCancellationTokenSource != null)
            {
                WriteCancellationTokenSource.Dispose();
                WriteCancellationTokenSource = null;
            }
        }
        async private void ReadButton_Click()
        {
            if (EventHandlerForDevice.Current.IsDeviceConnected)
            {
                try
                {
                    DataReaderObject = new DataReader(EventHandlerForDevice.Current.Device.InputStream);

                    while (isActive) { 
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception.Message.ToString());
                }
                finally
                {
                    DataReaderObject.DetachStream();
                    DataReaderObject = null;
                }
            }

        }
        private async Task ReadAsync(CancellationToken cancellationToken)
        {

            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            lock (ReadCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
                loadAsyncTask = DataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);
            }
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                var Str = DataReaderObject.ReadString(bytesRead);
                Debug.Write(Str);
                PlayingSound(CheckReadString(Str));
            }
        }

        private void CancelReadTask()
        {
            lock (ReadCancelLock)
            {
                if (ReadCancellationTokenSource != null)
                {
                    if (!ReadCancellationTokenSource.IsCancellationRequested)
                    {
                        ReadCancellationTokenSource.Cancel();

                        // Existing IO already has a local copy of the old cancellation token so this reset won't affect it
                        ResetReadCancellationTokenSource();
                    }
                }
            }
        }

        private void ResetReadCancellationTokenSource()
        {
            // Create a new cancellation token source so that can cancel all the tokens again
            ReadCancellationTokenSource = new CancellationTokenSource();

            // Hook the cancellation callback (called whenever Task.cancel is called)
            ReadCancellationTokenSource.Token.Register(() => NotifyReadCancelingTask());
        }
        private void NotifyReadCancelingTask()
        {

        }
        private async void WriteButton_Click()
        {

            EventHandlerForDevice.Current.Device.ReadTimeout = new System.TimeSpan(10 * 10000);
            if (EventHandlerForDevice.Current.IsDeviceConnected)
            {
                try
                {
                    DataWriteObject = new DataWriter(EventHandlerForDevice.Current.Device.OutputStream);
                    await WriteAsync(WriteCancellationTokenSource.Token);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception.Message.ToString());
                }
                finally
                {
                    DataWriteObject.DetachStream();
                    DataWriteObject = null;

                }
            }
        }
        private async Task WriteAsync(CancellationToken cancellationToken)
        {

            Task<UInt32> storeAsyncTask;
            /*
                        if ((WriteBytesInputValue.Text.Length != 0))
                        {
                            char[] buffer = new char[WriteBytesInputValue.Text.Length];
                            WriteBytesInputValue.Text.CopyTo(0, buffer, 0, WriteBytesInputValue.Text.Length);
                            String InputString = new string(buffer);
                            DataWriteObject.WriteString(InputString);
                            WriteBytesInputValue.Text = "";

                            lock (WriteCancelLock)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                storeAsyncTask = DataWriteObject.StoreAsync().AsTask(cancellationToken);
                            }

                            UInt32 bytesWritten = await storeAsyncTask;
                            if (bytesWritten > 0)
                            {
                                Debug.Write(InputString.Substring(0, (int)bytesWritten) + '\n');
                            }
                        }
                        */
        }
        private void CancelWriteTask()
        {
            lock (WriteCancelLock)
            {
                if (WriteCancellationTokenSource != null)
                {
                    if (!WriteCancellationTokenSource.IsCancellationRequested)
                    {
                        WriteCancellationTokenSource.Cancel();
                        ResetWriteCancellationTokenSource();
                    }
                }
            }
        }
        private void ResetWriteCancellationTokenSource()
        {
            WriteCancellationTokenSource = new CancellationTokenSource();
            WriteCancellationTokenSource.Token.Register(() => NotifyWriteCancelingTask());
        }
        private void NotifyWriteCancelingTask()
        {
        }

        private int CheckReadString(string str)
        {
            Debug.WriteLine(str);
            if (str.Contains("15"))
                return 15;
            else if (str.Contains("14"))
                return 14;
            else if (str.Contains("13"))
                return 13;
            else if (str.Contains("12"))
                return 12;
            else if (str.Contains("11"))
                return 11;
            else if (str.Contains("10"))
                return 10;
            else if (str.Contains("9"))
                return 9;
            else if (str.Contains("8"))
                return 8;
            else if (str.Contains("7"))
                return 7;
            else if (str.Contains("6"))
                return 6;
            else if (str.Contains("5"))
                return 5;
            else if (str.Contains("4"))
                return 4;
            else if (str.Contains("3"))
                return 3;
            else if (str.Contains("2"))
                return 2;
            else if (str.Contains("1"))
                return 1;
            else if (str.Contains("0"))
                return 0;
        }

        private void SoundModeCombo_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {

        }

        private void SoundModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string Mode = ((TextBlock)SoundModeCombo.SelectedItem).Text;

            if(Sounds != null)
            {
                for (int i = 0; i < Sounds.Length; i++)
                {
                    Uri uri = new Uri("ms-appx:///Assets/" + Mode + "/sound" + i + ".mp3");
                    Sounds[i].Source = MediaSource.CreateFromUri(uri);
                }
            }
        }
    }
}
