using System;
namespace BluetoothNuget
{
	/// <summary>
	/// Transmission state.
	/// </summary>
	public enum TransmissionState
	{
		/// <summary>
		/// The ok.
		/// </summary>
		OK,

		/// <summary>
		/// The timeout.
		/// </summary>
		TIMEOUT,

		/// <summary>
		/// The error crc.
		/// </summary>
		ErrorCRC,

		/// <summary>
		/// The error send message.
		/// </summary>
		ErrorSendMessage,

		/// <summary>
		/// The error receive message.
		/// </summary>
		ErrorReceiveMessage,

		/// <summary>
		/// The none.
		/// </summary>
		NONE,

		/// <summary>
		/// The characteristic doesn't offer the update service
		/// </summary>
		CharacteristicCantUpdate
	}
}
