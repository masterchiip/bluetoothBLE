
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BluetoothNuget;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace BluetoothLETest
{
	public class SerialDriver
	{
		//private IDevice deviceBle;
		private ICharacteristic writeCharac;
		private ICharacteristic readCharac;
		private IDescriptor notifyDescriptor;

		private bool receivedBytes;
		private List<byte> receivedMessageData;
		private int sizeOfMessage;
		private const int TIMEOUT = 10000;
		private bool updatedFinalSize = false;
		CancellationTokenSource cts;

		public ModbusFrame frameToSend;
		public ModbusFrame frameReceived;


		public SerialDriver(IDevice bleDevice, ICharacteristic writeCharacteristic, ICharacteristic readCharacteristic, IDescriptor notifyDescriptor)
		{
			//this.deviceBle = bleDevice;
			this.writeCharac = writeCharacteristic;
			this.readCharac = readCharacteristic;
			this.notifyDescriptor = notifyDescriptor;
		}

		/// <summary>
		/// Sends the request message to the slave device
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="message">Message.</param>
		public async Task<TransmissionState> SendMessage(byte[] message)
		{
			bool succed = false;
			try
			{
				succed = await writeCharac.WriteAsync(message);
			}
			catch (System.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("Failed to WriteAsync Message: " + ex.Message.ToString());
				succed = await writeCharac.WriteAsync(message);
			}

			if (succed == true)
			{
				return TransmissionState.OK;
			}
			else
			{
				return TransmissionState.ErrorSendMessage;
			}
		}

		/// <summary>
		/// Handles the receiving of the slave's respose
		/// It implements even the timeout mechanisms
		/// </summary>
		/// <returns>The messages.</returns>
		/// <param name="timeOut">Time out.</param>
		public async Task<ReceivedData> ListenMessages()
		{
			cts = new CancellationTokenSource(TIMEOUT); // Set timeout
			receivedMessageData = new List<byte>(sizeOfMessage);
			receivedBytes = false;

			readCharac.ValueUpdated -= ReadCharac_ValueUpdated;
			readCharac.ValueUpdated += ReadCharac_ValueUpdated;

			if (readCharac.CanUpdate == true)
			{
				await readCharac.StartUpdatesAsync();
			}
			else
			{
				return new ReceivedData(receivedMessageData.ToArray(), TransmissionState.CharacteristicCantUpdate);
			}


			await Task.Run(() =>
				  {
					  while (!cts.Token.IsCancellationRequested)
					  {
						  if (receivedBytes == true)
						  {
							  break; //I've finished with the reading 
						  }
					  }

				  }, cts.Token);

			await readCharac.StopUpdatesAsync();


			if (receivedBytes == false)
			{
				return new ReceivedData(receivedMessageData.ToArray(), TransmissionState.TIMEOUT);
			}
			else
			{
				return new ReceivedData(receivedMessageData.ToArray(), TransmissionState.OK);
			}
		}

		/// <summary>
		/// This Task handles all the types of Modbus Functions 
		/// </summary>
		/// <returns>The operation.</returns>
		/// <param name="functionType">Function type.</param>
		/// <param name="slaveAddress">Slave address.</param>
		/// <param name="nrRegisters">Nr registers.</param>
		/// <param name="selectedRegisterAddress">Selected register address.</param>
		/// <param name="dataToWrite">Data to write.</param>
		public async Task<ReceivedData> ModbusOperation(ModbusFunctionType functionType, byte slaveAddress, byte[] nrRegisters, byte[] selectedRegisterAddress, byte[] dataToWrite)
		{
			frameToSend = new ModbusFrame();

			if (functionType == ModbusFunctionType.WriteCoil || functionType == ModbusFunctionType.WriteHoldingRegister)
			{
				frameToSend = new ModbusFrame(slaveAddress, functionType, selectedRegisterAddress, null, dataToWrite);
			}
			else
			{
				frameToSend = new ModbusFrame(slaveAddress, functionType, selectedRegisterAddress, nrRegisters, null);
			}


			//We send the message to the Slave Device
			TransmissionState b = await SendMessage(frameToSend.BytePacketToSend);
			sizeOfMessage = CalculateExpectedSize();



			ReceivedData responseData = new ReceivedData();
			//If the message was transmitted right, then proceed with the listening
			//of the response
			if (b == TransmissionState.OK)
			{
				System.Diagnostics.Debug.WriteLine("MessageSended: " + b.ToString());

				//TO-DO Implement the retry mechanism
				//int retries = 3;
				//responseData = await ListenMessages();

				responseData = await ListenMessages();
			}
			else
			{
				responseData.state = TransmissionState.ErrorSendMessage;
			}

			if (responseData.state == TransmissionState.OK)
			{
				frameReceived = new ModbusFrame(responseData.data);
			}
			else
			{
				frameReceived = new ModbusFrame();
			}

			return responseData;
		}

		private async Task<ReceivedData> ListenMessageNuget()
		{
			cts = new CancellationTokenSource(TIMEOUT); // Set timeout
			receivedMessageData = new List<byte>(sizeOfMessage);

			byte[] b = await notifyDescriptor.ReadAsync();
			receivedMessageData.AddRange(b);
			receivedBytes = true;

			if (receivedBytes == false)
			{
				return new ReceivedData(receivedMessageData.ToArray(), TransmissionState.TIMEOUT);
			}
			else
			{
				return new ReceivedData(receivedMessageData.ToArray(), TransmissionState.OK);
			}
		}

		/// <summary>
		/// Here we catch the data sended from the Slave Modbus Device
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		void ReadCharac_ValueUpdated(object sender, CharacteristicUpdatedEventArgs e)
		{
			if (readCharac.Value.Length > 0)
			{
				cts = new CancellationTokenSource(TIMEOUT); // Set timeout
				receivedMessageData.AddRange(readCharac.Value);

				if (receivedMessageData.Count >= 3)
				{
					if (receivedMessageData[1] > 16)
					{
						sizeOfMessage += 2; //because here we have an exception response
					}
					else if (frameToSend.FrameType == ModbusFrameType.RequestRead && updatedFinalSize == false)
					{
						sizeOfMessage += receivedMessageData[2] + 2; //2 because of the CRC bytes 
						updatedFinalSize = true;
					}
				}
				if (receivedMessageData.Count == sizeOfMessage)
				{
					receivedBytes = true;
					updatedFinalSize = false;
				}
			}
		}




		/// <summary>
		/// Calculates the expected size of the receving frame.
		/// </summary>
		/// <returns>The expected size.</returns>
		private int CalculateExpectedSize()
		{
			if (frameToSend.FrameType == ModbusFrameType.ResponseException)
			{
				return 5;
			}
			if (frameToSend.FunctionType == ModbusFunctionType.WriteCoil || frameToSend.FunctionType == ModbusFunctionType.WriteHoldingRegister)
			{
				return 8;
			}
			else
			{
				return 3;
			}
		}


		public TransmissionState Retry(int retries)
		{
			while (true)
			{
				try
				{
					//data = await ListenMessages(2000);
					break; // SUCCESS!
				}
				catch
				{
					retries -= 1;
					if (retries == 0)
					{
						throw new TransmissionException(TransmissionState.TIMEOUT);
					}
				}
			}
			return TransmissionState.ErrorSendMessage;
		}
	}
}
