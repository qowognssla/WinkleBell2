using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.WebUI;

namespace WinkleBell2
{
    class Arduino
    {
        private SuspendingEventHandler appSuspendEventHandler;
        private EventHandler<Object> appResumeEventHandler;

        private Dictionary<DeviceWatcher, string> mapDeviceWatchersToDeviceSelector;

        private Boolean isAllDevicesEnumerated;

        private Boolean watchersSuspended;
        private Boolean watchersStarted;

        DataWriter DataWriterObject = null;
        private CancellationTokenSource WriteCancellationTokenSource;
        private Object WriteCancelLock = new Object();

        private CancellationTokenSource ReadCancellationTokenSource;
        private Object ReadCancelLock = new Object();

        DataReader DataReaderObject = null;

        public ObservableCollection<DeviceListEntry> listOfDevices;

        public Arduino()
        {
            // Device List initialize();
            listOfDevices = new ObservableCollection<DeviceListEntry>();
            mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, String>();

            watchersStarted = false;
            watchersSuspended = false;
            isAllDevicesEnumerated = false;
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

        public void InitializeDeviceWatchers()
        {
            var deviceSelector = SerialDevice.GetDeviceSelector();
            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);
            AddDeviceWatcher(deviceWatcher, deviceSelector);
        }

        public void OnAppSuspension(Object sender, SuspendingEventArgs args)
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

        public void OnAppResume(Object sender, Object args)
        {
            if (watchersSuspended)
            {
                watchersSuspended = false;
                StartDeviceWatchers();
            }
        }

        public void StartDeviceWatchers()
        {
            watchersStarted = true;
            isAllDevicesEnumerated = false;

            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status != DeviceWatcherStatus.Started) && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Start();
                }
            }
        }

        public void StopDeviceWatchers()
        {
            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status == DeviceWatcherStatus.Started) || (deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Stop();
                }
            }
            ClearDeviceEntries();

            watchersStarted = false;
        }

        public void ClearDeviceEntries()
        {
            listOfDevices.Clear();
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
        
        private void AddDeviceWatcher(DeviceWatcher deviceWatcher, String deviceSelector)
        {
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(this.OnDeviceAdded);
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(this.OnDeviceRemoved);
            deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(this.OnDeviceEnumerationComplete);

            mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
        }

        private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
        }
        
        private void RemoveDeviceFromList(String deviceId)
        {
            var deviceEntry = FindDevice(deviceId);
            listOfDevices.Remove(deviceEntry);
        }

        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            RemoveDeviceFromList(deviceInformationUpdate.Id);
        }

        private void OnDeviceEnumerationComplete(DeviceWatcher sender, Object args)
        {
            isAllDevicesEnumerated = true;

            if (EventHandlerForDevice.Current.IsDeviceConnected)
            {
                SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);
            }
            else if (EventHandlerForDevice.Current.IsEnabledAutoReconnect && EventHandlerForDevice.Current.DeviceInformation != null)
            {
                //ButtonDisconnectFromDevice.Content = "Disconnect";
            }
        }

        private void SelectDeviceInList(String deviceIdToSelect)
        {
            //ConnectDevices.SelectedIndex = -1;

            for (int deviceListIndex = 0; deviceListIndex < listOfDevices.Count; deviceListIndex++)
            {
                if (listOfDevices[deviceListIndex].DeviceInformation.Id == deviceIdToSelect)
                {
                    //ConnectDevices.SelectedIndex = deviceListIndex;
                    break;
                }
            }
        }

        public void OnDeviceConnected(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            // Find and select our connected device
            if (isAllDevicesEnumerated)
            {
                SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);
            }

            if (EventHandlerForDevice.Current.Device.PortName != "")
            {
                EventHandlerForDevice.Current.Device.Parity = SerialParity.None;
                EventHandlerForDevice.Current.Device.StopBits = SerialStopBitCount.One;
                EventHandlerForDevice.Current.Device.Handshake = SerialHandshake.None;
                EventHandlerForDevice.Current.Device.DataBits = 8;
                EventHandlerForDevice.Current.Device.BaudRate = 115200;

                EventHandlerForDevice.Current.Device.ReadTimeout = new TimeSpan(10 * 10000);
            }
        }

        public void OnDeviceClosing(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
        }

        public async void Connect(DeviceListEntry device)
        {
            if (device != null)
            {
                EventHandlerForDevice.CreateNewEventHandlerForDevice();
                EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
                EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;
                Boolean openSuccess = await EventHandlerForDevice.Current.OpenDeviceAsync(device.DeviceInformation, device.DeviceSelector);
            }
        }

        public void Disconnect()
        {
            EventHandlerForDevice.Current.IsEnabledAutoReconnect = false;
            EventHandlerForDevice.Current.CloseDevice();
        }

        public async void WriteCommand(String str)
        {
            if (EventHandlerForDevice.Current.IsDeviceConnected)
            {
                try
                {
                    DataWriterObject = new DataWriter(EventHandlerForDevice.Current.Device.OutputStream);

                    DataWriterObject.WriteString(str);
                    await WriteAsync(WriteCancellationTokenSource.Token);
                }
                catch (OperationCanceledException /*exception*/)
                {
                    Debug.WriteLine("Write Exception");
                }
                finally
                {
                    DataWriterObject.DetachStream();
                    DataWriterObject = null;
                }
            }
            else
            {
                Debug.WriteLine("Write Command : Device not connected");
            }
        }
        private async Task WriteAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> storeAsyncTask;

            lock (WriteCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                storeAsyncTask = DataWriterObject.StoreAsync().AsTask(cancellationToken);
            }

            UInt32 bytesWritten = await storeAsyncTask;
        }
   
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 7;

            lock (ReadCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
                loadAsyncTask = DataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);
            }

            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                String temp = DataReaderObject.ReadString(bytesRead);
            }
        }

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
        private void CancelAllIoTasks()
        {
            CancelReadTask();
            CancelWriteTask();
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
        private void CancelWriteTask()
        {
            lock (WriteCancelLock)
            {
                if (WriteCancellationTokenSource != null)
                {
                    if (!WriteCancellationTokenSource.IsCancellationRequested)
                    {
                        WriteCancellationTokenSource.Cancel();

                        // Existing IO already has a local copy of the old cancellation token so this reset won't affect it
                        ResetWriteCancellationTokenSource();
                    }
                }

            }
        }
        public void ResetReadCancellationTokenSource()
        {
            ReadCancellationTokenSource = new CancellationTokenSource();
        }
        public void ResetWriteCancellationTokenSource()
        {
            WriteCancellationTokenSource = new CancellationTokenSource();
        }
    }
}
