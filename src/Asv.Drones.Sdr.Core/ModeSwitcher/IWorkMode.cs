using Asv.Common;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using System;

namespace Asv.Drones.Sdr.Core
{
    /// <summary>
    /// Defines an interface for working modes with support for IDisposable.
    /// </summary>
    public interface IWorkMode: IDisposable
    {
        /// <summary>
        /// Gets the signal overflow indicator.
        /// </summary>
        IRxValue<float> SignalOverflowIndicator { get; }
        
        /// <summary>
        /// Gets the mode of the AsvSdrCustom.
        /// </summary>
        AsvSdrCustomMode Mode { get; }
        
        /// <summary>
        /// Gets the frequency in Hertz.
        /// </summary>
        ulong FrequencyHz { get; }
        
        /// <summary>
        /// Initializes the working mode with the specified frequency, reference power, calibration provider.
        /// </summary>
        /// <param name="frequencyHz">The operating frequency in hertz.</param>
        /// <param name="refPower">The reference power.</param>
        /// <param name="calibration">The calibration provider.</param>
        /// <param name="cancel">Cancellation token to cancel operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration,
                  CancellationToken cancel);
        
        /// <summary>
        /// Reads payload data related to the provided index and writer record ID.
        /// </summary>
        /// <param name="writerRecordId">The ID of the writer record.</param>
        /// <param name="dataIndex">The index of the data to be read.</param>
        /// <param name="payload">The payload from which to read the data.</param>
        void ReadData(Guid writerRecordId, uint dataIndex, IPayload payload);
    }
}