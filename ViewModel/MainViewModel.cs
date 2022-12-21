using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Win32;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Replacket.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private double _progress;
        private double _time;
        private int _repeat;
        private string _packetData;
        private string _url;
        private bool _onTransmit;
        private bool _checkedNormalCheckBox;
        private OpenFileDialog _openFileDialog;
        private CancellationTokenSource _cancellationTokenSource;
        private LibPcapLiveDevice _selectedDevice;
        private ObservableCollection<LibPcapLiveDevice> _devices;

        public ICommand StartTransmitCommand { get; set; }

        public ICommand StopTransmitCommand { get; set; }

        public ICommand OpenFileDialogCommand { get; set; }

        public ICommand OnCheckNormalCheckBoxCommand { get; set; }

        public double Progress
        {
            get { return _progress; }
            set { Set(ref _progress, value); }
        }

        public int Repeat
        {
            get { return _repeat; }
            set { Set(ref _repeat, value); }
        }

        public double Time
        {
            get { return _time; }
            set { Set(ref _time, value); }
        }

        public string PacketData
        {
            get { return _packetData; }
            set { Set(ref _packetData, value); }
        }

        public bool CheckedNormalCheckBox
        {
            get { return _checkedNormalCheckBox; }
            set { Set(ref _checkedNormalCheckBox, value); }
        }

        public bool OnTransmit
        {
            get { return _onTransmit; }
            set { Set(ref _onTransmit, value); }
        }

        public string Url
        {
            get { return _url; }
            set
            {
                Set(ref _url, value);
            }
        }

        public LibPcapLiveDevice SelectedDevice
        {
            get { return _selectedDevice; }
            set { Set(ref _selectedDevice, value); }
        }

        public ObservableCollection<LibPcapLiveDevice> Devices
        {
            get { return _devices; }
            set { Set(ref _devices, value); }
        }

        public MainViewModel()
        {
            InitGui();
        }

        private void InitGui()
        {
            InitDeviceList();
            InitCommands();
            InitFileDialog();
            InitTextBlocks();
        }

        private void InitFileDialog()
        {
            _openFileDialog = new OpenFileDialog();
            _openFileDialog.Filter = "Pcap (*.pcap)|*.pcap;";
            _openFileDialog.Multiselect = false;
        }

        private void InitTextBlocks()
        {
            Time = 1;
            Repeat = 1;
        }

        private void InitCommands()
        {
            StartTransmitCommand = new RelayCommand(StartTransmit);
            StopTransmitCommand = new RelayCommand(StopTransmit);
            OpenFileDialogCommand = new RelayCommand(OpenFileDialog);
            OnCheckNormalCheckBoxCommand = new RelayCommand<bool>(OnCheckNormalCheckBox);
        }

        private void InitDeviceList()
        {
            LibPcapLiveDeviceList deviceList = LibPcapLiveDeviceList.Instance;
            Devices = new ObservableCollection<LibPcapLiveDevice>();
            foreach (LibPcapLiveDevice device in deviceList)
            {
                Devices.Add(device);
            }

            if (Devices.Count > 0)
            {
                SelectedDevice = deviceList.Where(device => device.Loopback).FirstOrDefault();
            }
        }

        public void OpenFileDialog()
        {
            if (_openFileDialog.ShowDialog() == true)
            {
                Url = _openFileDialog.FileName;
            }
        }

        public void OnCheckNormalCheckBox(bool normalBoxChecked)
        {
            CheckedNormalCheckBox = normalBoxChecked;
        }

        public void StartTransmit()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(async () => await SendPackets(_cancellationTokenSource.Token));
        }

        public async Task SendPackets(CancellationToken cancellationToken)
        {
            Progress = 0;
            OnTransmit = true;

            var pcapReader = new CaptureFileReaderDevice(Url);
            if (SelectedDevice == null)
            {
                Url = "Device Error";
                OnTransmit = false;
                return;
            }

            SelectedDevice.Open();

            try
            {
                IEnumerable<RawCapture> list = PcapDevice.GetSequence(pcapReader, false);
                int totalPackets = list.Count();

                for (int currentPlay = 0; currentPlay < Repeat; currentPlay++)
                {
                    DateTime lastTime = list.FirstOrDefault().Timeval.Date;

                    foreach (RawCapture capture in list)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            OnTransmit = false;
                            return;
                        }

                        PacketData = BitConverter.ToString(capture.GetPacket().Bytes);
                        SelectedDevice.SendPacket(capture.GetPacket());

                        if (CheckedNormalCheckBox)
                        {
                            var time = capture.Timeval.Date - lastTime;
                            await Task.Delay(time);
                            lastTime = capture.Timeval.Date;
                        }
                        else
                        {
                            await Task.Delay((int)TimeSpan.FromSeconds(Time).TotalMilliseconds);
                        }
                    }
                    Progress = currentPlay / (double)(Repeat) * 100;
                }
            }
            catch (PcapException)
            {
                Url = "File Error";
                OnTransmit = false;
            }

            Progress = 100;
            OnTransmit = false;
        }

        public void StopTransmit()
        {
            OnTransmit = false;
            _cancellationTokenSource.Cancel();
        }
    }
}
