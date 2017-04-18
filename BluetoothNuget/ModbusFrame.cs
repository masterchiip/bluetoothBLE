using System;
using System.Collections.Generic;
using System.Linq;

namespace BluetoothNuget
{
	public class ModbusFrame
	{

		#region Parameters
		//In total there have to be maximum 256 bytes

		//Address of the Modbus Slave Device
		public byte SlaveAddress { get; set; }

		/// <summary>
		/// The Address of the selected register
		/// </summary>
		public byte[] RegisterAddress { get; set; }

		/// <summary>
		/// Gets or sets the number of registers.
		/// </summary>
		/// <value>Number of the registers that we want to read.</value>
		public byte[] NumberOfRegisters { get; set; }

		/// <summary>
		/// Gets or sets the number bytes to follow.
		/// By default they have to be the same as the number of registers that we request.
		/// </summary>
		/// <value>The variable number of bytes of the response data for the Read Functions</value>
		private byte NumberBytesToFollow { get; set; }

		public byte FunctionCode { get; set; }

		public byte ExceptionCode { get; set; }

		public string ExceptionTitle { get; set; }

		public string ExceptionDescription { get; set; }

		public ModbusFunctionType FunctionType { get; set; }

		public ModbusFrameType FrameType { get; set; }

		public byte[] DataToWrite { get; set; }

		public byte[] DataReceived { get; set; }

		public byte[] ValueWritten { get; set; }

		public byte[] CRC { get; set; }

		private byte[] PacketInBytes { get; set; }

		public byte[] BytePacketToSend { get; set; }

		public bool IsRequestFrame { get; set; }

		public ModbusFrameState State { get; set; }


		private Dictionary<ModbusFunctionType, byte> functionCodesRequests
		{
			get
			{
				return new Dictionary<ModbusFunctionType, byte>
				{
					{ModbusFunctionType.ReadCoil,01},
					{ModbusFunctionType.WriteCoil,05},
					{ModbusFunctionType.ReadDigitalInput,02},
					{ModbusFunctionType.ReadInputRegister,04},
					{ModbusFunctionType.ReadHoldingRegister,03},
					{ModbusFunctionType.WriteHoldingRegister,06},
				};
			}
		}

		private Dictionary<byte, ModbusFunctionType> functionCodesExceptions
		{
			get
			{
				return new Dictionary<byte, ModbusFunctionType>
				{
					{129, ModbusFunctionType.ReadCoil},
					{133, ModbusFunctionType.WriteCoil},
					{130, ModbusFunctionType.ReadDigitalInput},
					{132, ModbusFunctionType.ReadInputRegister},
					{131, ModbusFunctionType.ReadHoldingRegister},
					{134, ModbusFunctionType.WriteHoldingRegister},
				};
			}
		}

		private Dictionary<ModbusFunctionType, ModbusFrameType> functionsFrameTypes
		{
			get
			{
				return new Dictionary<ModbusFunctionType, ModbusFrameType>
				{
					{ModbusFunctionType.ReadCoil,ModbusFrameType.RequestRead},
					{ModbusFunctionType.WriteCoil,ModbusFrameType.RequestWrite},
					{ModbusFunctionType.ReadDigitalInput,ModbusFrameType.RequestRead},
					{ModbusFunctionType.ReadInputRegister,ModbusFrameType.RequestRead},
					{ModbusFunctionType.ReadHoldingRegister,ModbusFrameType.RequestRead},
					{ModbusFunctionType.WriteHoldingRegister,ModbusFrameType.RequestWrite},
				};
			}

		}

		private Dictionary<byte, Tuple<string, string>> exceptionCodes
		{
			get
			{
				return new Dictionary<byte, Tuple<string, string>>
				{
					{01,new Tuple<string, string>("Illegal Function","The function code received in the query is not an allowable action for the slave.  This may be because the function code is only applicable to newer devices, and was not implemented in the unit selected.  It could also indicate that the slave is in the wrong state to process a request of this type, for example because it is unconfigured and is being asked to return register values. If a Poll Program Complete command was issued, this code indicates that no program function preceded it.")},
					{02,new Tuple<string, string>("Illegal Data Address","The data address received in the query is not an allowable address for the slave. More specifically, the combination of reference number and transfer length is invalid. For a controller with 100 registers, a request with offset 96 and length 4 would succeed, a request with offset 96 and length 5 will generate exception 02.")},
					{03,new Tuple<string, string>("Illegal Data Value","A value contained in the query data field is not an allowable value for the slave.  This indicates a fault in the structure of remainder of a complex request, such as that the implied length is incorrect. It specifically does NOT mean that a data item submitted for storage in a register has a value outside the expectation of the application program, since the MODBUS protocol is unaware of the significance of any particular value of any particular register.")},
					{04,new Tuple<string, string>("Slave Device Failure","An unrecoverable error occurred while the slave was attempting to perform the requested action.")},
					{05,new Tuple<string, string>("Acknowledge","Specialized use in conjunction with programming commands. The slave has accepted the request and is processing it, but a long duration of time will be required to do so.  This response is returned to prevent a timeout error from occurring in the master. The master can next issue a Poll Program Complete message to determine if processing is completed.")},
					{06,new Tuple<string, string>("Slave Device Busy","Specialized use in conjunction with programming commands.The slave is engaged in processing a long-duration program command.  The master should retransmit the message later when the slave is free.")},
					{07,new Tuple<string, string>("Negative Acknowledge","The slave cannot perform the program function received in the query. This code is returned for an unsuccessful programming request using function code 13 or 14 decimal. The master should request diagnostic or error information from the slave.")},
					{08,new Tuple<string, string>("Memory Parity Error","Specialized use in conjunction with function codes 20 and 21 and reference type 6, to indicate that the extended file area failed to pass a consistency check. The slave attempted to read extended memory or record file, but detected a parity error in memory. The master can retry the request, but service may be required on the slave device.")},
					{10,new Tuple<string, string>("Gateway Path Unavailable","Specialized use in conjunction with gateways, indicates that the gateway was unable to allocate an internal communication path from the input port to the output port for processing the request. Usually means the gateway is misconfigured or overloaded.")},
					{11,new Tuple<string, string>("Gateway Target Device Failed to Respond","Specialized use in conjunction with gateways, indicates that no response was obtained from the target device. Usually means that the device is not present on the network.")},
				};
			}
		}

		#endregion Parameters

		#region Constructors

		public ModbusFrame()
		{
			this.State = ModbusFrameState.Empty;
		}

		/// <summary>
		/// Initializes a new instance of the ModbusFrame class
		/// based on the informations given in input by the user.
		/// It can be used to the Request to Read or Write
		/// </summary>
		/// <param name="slaveAddress">Slave address.</param>
		/// <param name="ft">Ft.</param>
		/// <param name="registerAddr">Register address.</param>
		/// <param name="nrRegisters">Nr registers.</param>
		/// <param name="dataToWrite">Data to write.</param>
		/// <param name="request">If set to <c>true</c> request.</param>
		public ModbusFrame(byte slaveAddress, ModbusFunctionType ft, byte[] registerAddr, byte[] nrRegisters, byte[] dataToWrite, bool request = true)
		{
			this.SlaveAddress = slaveAddress;
			this.FunctionType = ft;
			this.FunctionCode = functionCodesRequests[ft];
			this.RegisterAddress = registerAddr;
			this.NumberOfRegisters = nrRegisters;
			this.DataToWrite = dataToWrite;
			this.IsRequestFrame = request;
			this.PacketInBytes = BuildBytesFrame();
			this.CRC = ModRTU_CRC();
			this.BuildPacketToSend();
			this.SetFrameType();
			this.State = ModbusFrameState.Correct;
		}

		/// <summary>
		/// Initializes a new instance of the ModbusFrame class
		/// based on the recevied data from the device, so it has 
		/// to be interpreted.
		/// </summary>
		/// <param name="receivedData">Received data.</param>
		public ModbusFrame(byte[] receivedData)
		{
			
			this.FunctionCode = receivedData[1];
			if (this.FunctionCode > 16)
			{
				this.FunctionType = functionCodesExceptions[this.FunctionCode];
			}
			else
			{
				this.FunctionType = functionCodesRequests.Where(kvp => kvp.Value == this.FunctionCode).Select(kvp => kvp.Key).FirstOrDefault();
			}


			//If the response is an exception I create the Modbus Exception Frame
			if (this.FunctionCode == 129 || this.FunctionCode == 130 || this.FunctionCode == 131 ||
			   this.FunctionCode == 132 || this.FunctionCode == 133 || this.FunctionCode == 134)
			{
				this.SlaveAddress = receivedData[0];
				this.FrameType = ModbusFrameType.ResponseException;
				this.ExceptionCode = receivedData[2];
				this.ExceptionTitle = exceptionCodes[this.ExceptionCode].Item1;
				this.ExceptionDescription = exceptionCodes[this.ExceptionCode].Item2;
				this.CRC = new byte[] { receivedData[receivedData.Length - 2], receivedData[receivedData.Length - 1] };
			}
			//If the response is for a Write Function
			else if (this.FunctionType == ModbusFunctionType.WriteCoil || this.FunctionType == ModbusFunctionType.WriteHoldingRegister)
			{
				this.FrameType = ModbusFrameType.ResponseRequest;
				this.SlaveAddress = receivedData[0];
				this.RegisterAddress = new byte[] { receivedData[2], receivedData[3] };
				this.ValueWritten = new byte[] { receivedData[4], receivedData[5] };
				this.CRC = new byte[] { receivedData[receivedData.Length - 2], receivedData[receivedData.Length - 1] };
			}
			//If the response is for a Read Function
			else
			{
				this.FrameType = ModbusFrameType.ResponseRequest;
				this.SlaveAddress = receivedData[0];
				this.NumberBytesToFollow = receivedData[2];
				this.DataReceived = new byte[this.NumberBytesToFollow];
				for (int i = 0; i < this.NumberBytesToFollow; i++)
				{
					this.DataReceived[i] = receivedData[3 + i];
				}
				this.CRC = new byte[] { receivedData[receivedData.Length - 2], receivedData[receivedData.Length - 1] };
			}

			this.State = ModbusFrameState.Correct;
		}

		#endregion Constructors

		#region Methods

		/// <summary>
		/// Sets the type of the frame.
		/// </summary>
		private void SetFrameType()
		{
			if (this.IsRequestFrame == true)
			{
				this.FrameType = this.functionsFrameTypes[this.FunctionType];
			}
			else
			{
				this.FrameType = ModbusFrameType.ResponseRequest;
			}
		}

		/// <summary>
		/// Builds the packet to send.
		/// </summary>
		private void BuildPacketToSend()
		{
			//Let's add the CRC code to our packet
			var combined = this.PacketInBytes.Concat(this.CRC).ToArray();
			this.BytePacketToSend = combined;
		}

		/// <summary>
		/// Decides wich parameter to pass to the Array Builder 
		/// based on the type of ModbusFrame and ModbusFunction
		/// </summary>
		/// <returns>The bytes frame.</returns>
		private byte[] BuildBytesFrame()
		{
			//Here we manage the Request Frames
			if (this.FrameType == ModbusFrameType.RequestRead || this.FrameType == ModbusFrameType.RequestWrite)
			{
				//Here we have the Write cases
				if (this.FunctionType == ModbusFunctionType.WriteCoil || this.FunctionType == ModbusFunctionType.WriteHoldingRegister)
				{
					return BuildBytesArray(this.SlaveAddress, this.FunctionCode, this.RegisterAddress, this.DataToWrite);
				}
				//Here we have the Read cases
				else
				{
					return BuildBytesArray(this.SlaveAddress, this.FunctionCode, this.RegisterAddress, this.NumberOfRegisters);
				}
			}
			//Here we manage the Response Frames
			else
			{
				//For the Write Request
				if (this.FunctionType == ModbusFunctionType.WriteHoldingRegister || this.FunctionType == ModbusFunctionType.WriteCoil)
				{
					return BuildBytesArray(this.SlaveAddress, this.FunctionCode, this.RegisterAddress, this.ValueWritten);
				}
				//For the Read Request
				else
				{
					return BuildBytesArray(this.SlaveAddress, this.FunctionCode, this.NumberOfRegisters, this.DataReceived);
				}
			}
		}

		/// <summary>
		/// Compute the CRC 16 for the Modbus RTU
		/// </summary>
		/// <returns>The rtu crc.</returns>
		private byte[] ModRTU_CRC()
		{
			UInt16 crc = 0xFFFF;

			for (int pos = 0; pos < this.PacketInBytes.Length; pos++)
			{
				crc ^= (UInt16)this.PacketInBytes[pos];          // XOR byte into least sig. byte of crc

				for (int i = 8; i != 0; i--)
				{    // Loop over each bit
					if ((crc & 0x0001) != 0)
					{      // If the LSB is set
						crc >>= 1;                    // Shift right and XOR 0xA001
						crc ^= 0xA001;
					}
					else                            // Else LSB is not set
						crc >>= 1;                    // Just shift right
				}
			}

			byte[] crcValues = BitConverter.GetBytes(crc);
			return crcValues;
		}


		/// <summary>
		/// Builds an array of bytes with all the objects that it receives
		/// It is useful because it creates the array of bytes needed to send to the device
		/// </summary>
		/// <returns>The array of bytes</returns>
		/// <param name="args">Arguments.</param>
		private byte[] BuildBytesArray(params object[] args)
		{
			List<byte> listOfBytes = new List<byte>();
			foreach (object ob in args)
			{
				if (ob is byte[])
				{
					byte[] list = (byte[])ob;
					foreach (byte b in list)
					{
						listOfBytes.Add(b);
					}
				}
				else
				{
					listOfBytes.Add((byte)ob);
				}
			}
			return listOfBytes.ToArray();
		}

		#endregion Methods


		#region Public Methods
		public override string ToString()
		{
			if (this.FrameType == ModbusFrameType.ResponseException)
			{
				return string.Format("[ModbusFrame: SlaveAddress={0}, FunctionType={1}, FrameType={2}, ExceptionCode={3}, ExceptionTitle={4}, ExceptionDescription={5}, CRC={6}]", this.SlaveAddress, this.FunctionType, this.FrameType, this.ExceptionCode, this.ExceptionTitle, this.ExceptionDescription, BitConverter.ToString(this.CRC));
			}
			else if (this.FrameType == ModbusFrameType.RequestRead || this.FrameType == ModbusFrameType.RequestWrite)
			{
				return string.Format("[ModbusFrame: SlaveAddress={0}, FunctionType={1}, FrameType={2}, CRC={3}]", this.SlaveAddress, this.FunctionType, this.FrameType, BitConverter.ToString(this.CRC));
			}
			else
			{
				if (this.FunctionType == ModbusFunctionType.WriteHoldingRegister || this.FunctionType == ModbusFunctionType.WriteCoil)
				{
					return string.Format("[ModbusFrame: SlaveAddress={0}, FunctionCode={1}, RegisterAddress={2}, DataWritten={3}, CRC={4}]", this.SlaveAddress, this.FunctionCode, BitConverter.ToString(this.RegisterAddress), BitConverter.ToString(this.ValueWritten), BitConverter.ToString(this.CRC));
				}
				else
				{
					return string.Format("[ModbusFrame: SlaveAddress={0}, NumberBytesToFollow={1}, FunctionCode={2}, FrameType={3}, DataReceived={4}, CRC={5}]", this.SlaveAddress, this.NumberBytesToFollow, this.FunctionCode, this.FrameType, BitConverter.ToString(this.DataReceived), BitConverter.ToString(this.CRC));
				}
			}
		}
		#endregion Public Methods
	}
}
