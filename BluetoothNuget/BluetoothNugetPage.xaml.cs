using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Acr.UserDialogs;
using BluetoothLETest;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using Xamarin.Forms;

namespace BluetoothNuget
{
	public partial class BluetoothNugetPage : ContentPage, INotifyPropertyChanged
	{

		#region Enums

		public enum WriteState
		{
			Wait,
			Write,
			ReWrite,
		}

		public enum ActionType
		{
			WriteOnly,
			WriteAndRead,
		}

		#endregion

		#region Constants and Fields

		private static readonly String serviceUUID = "0003cdd0-0000-1000-8000-00805f9b0131";
		private static readonly String writeUUID = "0003cdd2-0000-1000-8000-00805f9b0131";
		private static readonly String readUUID = "0003cdd1-0000-1000-8000-00805f9b0131";

		private int _headerLenght = 2;
		private int _chunkNumPos = 0;
		private int _payloadLengthPos = 1;
		private int _totalChunkNum = 5;
		private int _chunckLength = 20;

		private int _sleepTime = 10;
		private int _timeToKill = 5000;

		private byte[] _writeBuffer;
		private int _fileOffset = 0;
		private int _writeByteNum = 0;
		private IAdapter _bluetoothAdapter;
		private IDevice _device;
		private ICharacteristic _write;
		private ICharacteristic _read;
		private IDescriptor _notifyDescriptor;
		private CancellationTokenSource _cts;
		private string _path = string.Empty;
		private Stream _result;
		private List<byte> _response = new List<byte>();
		private DateTime _lastRecievedTime;
		private byte _chunckNumber = 0;
		private WriteState _state;
		private int _version = 0;

		private const ActionType _action = ActionType.WriteAndRead;

		private SerialDriver serialDriver;

		#endregion

		#region Events

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#region Properties

		public bool IsSearchEnabled
		{
			get;
			set;
		}

		public bool IsConnectEnabled
		{
			get;
			set;
		}

		public bool IsDisconnectEnabled
		{
			get;
			set;
		}


		private int PayloadLength
		{
			get
			{
				return _chunckLength * _totalChunkNum - _headerLenght;
			}
		}

		public string DeviceFoundName
		{
			get;
			set;
		}

		public string DeviceConnected
		{
			get;
			set;
		}

		public string DeviceFoundDistance
		{
			get;
			set;
		}

		#endregion

		#region Commands

		public ICommand SearchCommand { protected set; get; }

		public ICommand ConnectCommand { protected set; get; }

		public ICommand DisconnectCommand { protected set; get; }

		#endregion

		public BluetoothNugetPage()
		{
			InitializeComponent();

			this.BindingContext = this;

			_cts = new CancellationTokenSource();

			this._device = null;

			_bluetoothAdapter = CrossBluetoothLE.Current.Adapter;
			_bluetoothAdapter.ScanTimeout = 8000;
			_bluetoothAdapter.DeviceDiscovered += OnDeviceDiscovered;
			_bluetoothAdapter.DeviceDisconnected += OnDeviceDisconnected;
			_bluetoothAdapter.DeviceConnected += OnDeviceConnected;
			_bluetoothAdapter.ScanTimeoutElapsed += (sender, e) =>
			{
				_device = null;
				IsSearchEnabled = true;
				OnPropertyChanged(nameof(IsSearchEnabled));

				UserDialogs.Instance.Toast("Search Timeout");
			};

			IsSearchEnabled = true;
			OnPropertyChanged(nameof(IsSearchEnabled));

			IsConnectEnabled = false;
			OnPropertyChanged(nameof(IsConnectEnabled));


			this.SearchCommand = new Command((arg) =>
				{
					_device = null;

					IsSearchEnabled = false;
					OnPropertyChanged(nameof(IsSearchEnabled));

					if (_bluetoothAdapter.IsScanning)
					{
						_bluetoothAdapter.StopScanningForDevicesAsync().ContinueWith((o) =>
						{
							_bluetoothAdapter.StartScanningForDevicesAsync();
						});
					}
					else
					{
						_bluetoothAdapter.StartScanningForDevicesAsync();
					}
				}, (arg) => IsSearchEnabled);

			this.ConnectCommand = new Command((arg) =>
				{
					TryConnect();
				}, (arg) => IsConnectEnabled);

			this.DisconnectCommand = new Command((arg) =>
			{
				_bluetoothAdapter.DisconnectDeviceAsync(_device);
				IsDisconnectEnabled = false;
				IsSearchEnabled = true;
				IsConnectEnabled = false;
				DeviceFoundName = "";
				DeviceConnected = "";
				DeviceFoundDistance = "";
				OnPropertyChanged(nameof(DeviceFoundName));
				OnPropertyChanged(nameof(DeviceConnected));
				OnPropertyChanged(nameof(DeviceFoundDistance));
				OnPropertyChanged(nameof(IsDisconnectEnabled));
				OnPropertyChanged(nameof(IsSearchEnabled));
				OnPropertyChanged(nameof(IsConnectEnabled));


			}, (arg) => IsDisconnectEnabled);


		}

		private void Handle_ClickedSearch(object sender, System.EventArgs e)
		{
			_device = null;

			IsSearchEnabled = false;
			OnPropertyChanged(nameof(IsSearchEnabled));

			if (_bluetoothAdapter.IsScanning)
			{
				_bluetoothAdapter.StopScanningForDevicesAsync().ContinueWith((o) =>
				{
					_bluetoothAdapter.StartScanningForDevicesAsync();
				});
			}
			else
			{
				_bluetoothAdapter.StartScanningForDevicesAsync();
			}
		}


		#region Methods

		protected T[] SubArray<T>(T[] data, int index, int length)
		{
			T[] result = new T[length];
			Array.Copy(data, index, result, 0, length);
			return result;
		}

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this,
					new PropertyChangedEventArgs(propertyName));
			}
		}

		void Handle_ClickedConnect(object sender, System.EventArgs e)
		{
			TryConnect();
		}

		void Handle_ClickedDisconnect(object sender, System.EventArgs e)
		{
			_bluetoothAdapter.DisconnectDeviceAsync(_device);
			IsDisconnectEnabled = false;
			IsSearchEnabled = true;
			IsConnectEnabled = false;
			DeviceFoundName = "";
			DeviceConnected = "";
			DeviceFoundDistance = "";
			OnPropertyChanged(nameof(DeviceFoundName));
			OnPropertyChanged(nameof(DeviceConnected));
			OnPropertyChanged(nameof(DeviceFoundDistance));
			OnPropertyChanged(nameof(IsDisconnectEnabled));
			OnPropertyChanged(nameof(IsSearchEnabled));
			OnPropertyChanged(nameof(IsConnectEnabled));
		}

		public async void Handle_Clicked(object sender, System.EventArgs e)
		{
			if (serialDriver != null)
			{
				UserDialogs.Instance.ShowLoading("Sending Data");
				ReceivedData respsonseData = new ReceivedData();

				Button btnSender = (Button)sender as Button;
				if (btnSender.Text == "Read Coil")
				{
					byte[] regAdr = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry1.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry1.Text))[0] };
					byte[] nrReg = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry1.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry1.Text))[0] };
					respsonseData = await serialDriver.ModbusOperation(ModbusFunctionType.ReadCoil, 01, nrReg, regAdr, null);
				}
				else if (btnSender.Text == "Read Input Status")
				{
					byte[] regAdr = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry2.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry2.Text))[0] };
					byte[] nrReg = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry2.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry2.Text))[0] };
					respsonseData = await serialDriver.ModbusOperation(ModbusFunctionType.ReadDigitalInput, 01, nrReg, regAdr, null);
				}
				else if (btnSender.Text == "Read Input Registers")
				{
					byte[] regAdr = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry3.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry3.Text))[0] };
					byte[] nrReg = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry3.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry3.Text))[0] };
					respsonseData = await serialDriver.ModbusOperation(ModbusFunctionType.ReadInputRegister, 01, nrReg, regAdr, null);
				}
				else if (btnSender.Text == "Read Holding Registers")
				{
					byte[] regAdr = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry4.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry4.Text))[0] };
					byte[] nrReg = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry4.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(NrRegistersEntry4.Text))[0] };
					respsonseData = await serialDriver.ModbusOperation(ModbusFunctionType.ReadHoldingRegister, 01, nrReg, regAdr, null);
				}
				else if (btnSender.Text == "Write Coil")
				{
					byte[] regAdr = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry5.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry5.Text))[0] };
					byte[] data = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(DataEntry1.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(DataEntry1.Text))[0] };
					respsonseData = await serialDriver.ModbusOperation(ModbusFunctionType.WriteCoil, 01, null, regAdr, data);
				}
				else
				{
					byte[] regAdr = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry6.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(RegisterEntry6.Text))[0] };
					byte[] data = new byte[] { BitConverter.GetBytes(Convert.ToUInt16(DataEntry2.Text))[1], BitConverter.GetBytes(Convert.ToUInt16(DataEntry2.Text))[0] };
					respsonseData = await serialDriver.ModbusOperation(ModbusFunctionType.WriteHoldingRegister, 01, null, regAdr, data);
				}


				UserDialogs.Instance.HideLoading();


				if (respsonseData.state == TransmissionState.OK)
				{
					DisplayAlert("Function Operation", serialDriver.frameReceived.ToString(), "ok");
				}
				else
				{
					DisplayAlert("Error", "State of the result of the transmission: " + respsonseData.state.ToString(), "ok");
				}
			}
			else
			{
				DisplayAlert("Attention", "Connect to a device first!", "ok");
			}

		}


		protected async void TryConnect()
		{
			if (_device != null)
			{
				IsConnectEnabled = false;
				OnPropertyChanged(nameof(IsConnectEnabled));

				UserDialogs.Instance.ShowLoading("Connecting to device");

				try
				{
					await _bluetoothAdapter.ConnectToDeviceAsync(_device);
				}
				catch (DeviceConnectionException ex)
				{
					UserDialogs.Instance.ShowError("Could not connect to the Device! Error: " + ex.Message.ToString());
				}
				catch (Exception ex)
				{
					UserDialogs.Instance.ShowError("Could not connect to the Device! Error: " + ex.Message.ToString());
				}

				UserDialogs.Instance.HideLoading();
			}
		}

		protected async Task StopTalk()
		{
			if (_read != null)
			{
				try
				{
					await _read.StopUpdatesAsync();
					_read.ValueUpdated -= OnValueUpdated;
				}
				catch
				{

				}
			}

			if (_device != null)
			{
				_bluetoothAdapter.DisconnectDeviceAsync(_device);
			}

			//IsConnectEnabled = true;
			//OnPropertyChanged(nameof(IsConnectEnabled));
		}

		#endregion Methods

		#region Event Handlers

		protected void OnDeviceDiscovered(object sender, DeviceEventArgs e)
		{

			if (!String.IsNullOrWhiteSpace(e.Device.Name) && e.Device.Name.StartsWith("USR-BLE100"))
			{
				DeviceFoundName = e.Device.Name;
				DeviceFoundDistance = e.Device.Rssi.ToString();
				OnPropertyChanged(nameof(DeviceFoundName));
				OnPropertyChanged(nameof(DeviceFoundDistance));

				_device = e.Device;
				_bluetoothAdapter.StopScanningForDevicesAsync();

				IsConnectEnabled = true;
				OnPropertyChanged(nameof(IsConnectEnabled));

				UserDialogs.Instance.Toast("Device Found");
			}

		}

		private async void OnDeviceConnected(object sender, DeviceEventArgs e)
		{
			_read = null;
			_write = null;

			UserDialogs.Instance.ShowLoading("Getting informations");
			var service = await _device.GetServiceAsync(Guid.Parse(serviceUUID));
			if (service != null)
			{
				_read = await service.GetCharacteristicAsync(Guid.Parse(readUUID));
				_write = await service.GetCharacteristicAsync(Guid.Parse(writeUUID));
				IList<IDescriptor> descriptors = (IList<IDescriptor>) await _read.GetDescriptorsAsync();
				_notifyDescriptor = descriptors[0];


				//_writeBuffer = new byte[PayloadLength + _headerLenght];

				if (_read != null && _write != null)
				{
					serialDriver = new SerialDriver(_device, _write, _read, _notifyDescriptor);

					UserDialogs.Instance.HideLoading();
					DeviceConnected = "CONNECTED";
					IsDisconnectEnabled = true;
					OnPropertyChanged(nameof(DeviceConnected));
					OnPropertyChanged(nameof(IsDisconnectEnabled));
				}
				else
				{
					UserDialogs.Instance.HideLoading();
					UserDialogs.Instance.ShowError("Unable to get characteristic");
				}
			}
		}
		
		private async void OnDeviceDisconnected(object sender, DeviceEventArgs e)
		{
			_cts.Cancel();
			await StopTalk();
			UserDialogs.Instance.Toast("Device Disconnected");
		}

		protected void OnValueUpdated(object sender, EventArgs e)
		{
			_response.AddRange(_read.Value);
			_lastRecievedTime = DateTime.Now;

			if (_response.Count > _headerLenght)
			{
				if (_response[_chunkNumPos] == _chunckNumber)
				{
					var dim = (int)_response[_payloadLengthPos];
					if (_response.Count == dim + _headerLenght)
					{
						if (_result != null && _result.CanWrite)
						{
							for (int i = _headerLenght; i < _response.Count; i++)
								_result.WriteByte(_response[i]);

							_chunckNumber++;
							_state = WriteState.Write;
						}
					}

					if (_response.Count > dim + _headerLenght)
					{
						_state = WriteState.ReWrite;
						System.Diagnostics.Debug.WriteLine("FATALITY !!!!!!\n_response.Count > dim + headerLenght");
					}
				}
				else
				{
					if (_state == WriteState.Wait)
					{
						System.Diagnostics.Debug.WriteLine("FATALITY !!!!!!");
						_state = WriteState.ReWrite;
					}
				}
			}
		}

		#endregion

	}
}