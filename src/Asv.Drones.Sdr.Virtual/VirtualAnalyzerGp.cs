using System.ComponentModel.Composition;
using Asv.Common;
using Asv.Drones.Sdr.Core;
using Asv.Drones.Sdr.Core.Mavlink;
using Asv.Mavlink;
using Asv.Mavlink.V2.AsvSdr;
using Asv.Mavlink.Vehicle;

namespace Asv.Drones.Sdr.Virtual;

/// <summary>
/// The VirtualAnalyzerGp class implements the IAnalyzerGp interface and serves as a virtual analyzer.
/// </summary>
[ExportAnalyzer(AsvSdrCustomMode.AsvSdrCustomModeGp, "Virtual")]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VirtualAnalyzerGp : IAnalyzerGp
{
    private readonly ISdrMavlinkService _device;

    /// <summary>
    /// The signal overflow indicator for the given float value.
    /// </summary>
    private readonly RxValue<float> _signalOverflowIndicator;

    private readonly NormalRandom _random;

    /// <summary>
    /// This class represents a virtual analyzer for GP (General Purpose) signals.
    /// </summary>
    [ImportingConstructor]
    public VirtualAnalyzerGp(ISdrMavlinkService device)
    {
        _device = device;
        _random = new NormalRandom();
        _signalOverflowIndicator = new RxValue<float>(Single.NaN);
    }

    /// <summary>
    /// Represents an indicator of signal overflow.
    /// </summary>
    /// <value>
    /// The signal overflow indicator value.
    /// </value>
    public IRxValue<float> SignalOverflowIndicator => _signalOverflowIndicator;

    /// <summary>
    /// Initializes the system with the specified parameters.
    /// </summary>
    /// <param name="frequencyHz">The frequency in Hz.</param>
    /// <param name="refPower">The reference power.</param>
    /// <param name="calibration">The calibration provider.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Init(ulong frequencyHz, float refPower, ICalibrationProvider calibration, CancellationToken cancel)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fills the given payload with the specified data.
    /// </summary>
    /// <param name="payload">The payload to be filled.</param>
    public void Fill(AsvSdrRecordDataGpPayload payload)
    {
        var position = MavlinkTypesHelper.FromInt32ToGeoPoint(payload.GnssLat,payload.GnssLon,payload.GnssAlt);
        var llz = new GeoPoint(_device.Server.Params[SimulationParams.SimLlzLat],_device.Server.Params[SimulationParams.SimLlzLon],_device.Server.Params[SimulationParams.SimLlzAlt]);
        var gp = new GeoPoint(_device.Server.Params[SimulationParams.SimGpLat],_device.Server.Params[SimulationParams.SimGpLon],_device.Server.Params[SimulationParams.SimGpAlt]);
        var threshold = new GeoPoint(_device.Server.Params[SimulationParams.SimTrhLat],_device.Server.Params[SimulationParams.SimTrhLon],_device.Server.Params[SimulationParams.SimTrhAlt]);
        var gpAngle = (float)_device.Server.Params[SimulationParams.SimGpAngle];
        var gpUpperWidthMin = (float)_device.Server.Params[SimulationParams.SimGpUpperWidthMin];
        var gpLowerWidthMin = (float)_device.Server.Params[SimulationParams.SimGpLowerWidthMin];
        // calculate aiming point 
        var loc0 = llz.SetAltitude(0);
        var glide0 = gp.SetAltitude(0);
        var end0 = threshold.SetAltitude(0);
        var aimingPoint = GeoMath.IntersectionLineAndPerpendicularFromPoint(loc0, end0, glide0).SetAltitude(gp.Altitude);
        // calculate distance to gp
        var position0 = position.SetAltitude(0);
        var locProjectionPoint = GeoMath.IntersectionLineAndPerpendicularFromPoint(loc0, end0, position0);
        var distanceToGp = locProjectionPoint.DistanceTo(aimingPoint.SetAltitude(0));
        // calculate real height
        var realHeight = position.Altitude - gp.Altitude;
        // calculate ref angle
        var refAngle = GeoMath.RadiansToDegrees(Math.Atan(realHeight / distanceToGp));
        // calculate ddm per degree
        var halfSectorAngle = 0.0;
        if (refAngle > gpAngle)
        {
            halfSectorAngle = gpUpperWidthMin / 60.0 / gpAngle;
        }
        else
        {
            halfSectorAngle = gpLowerWidthMin / 60.0 / gpAngle;
        }
        const double GlideHalfSectorDdm = 0.0875;
        var ddmPerDegree = GlideHalfSectorDdm / (halfSectorAngle * gpAngle);
        // if upper gp must be positive
        var refDdm90_150 = (gpAngle - refAngle) * ddmPerDegree;

        if (refDdm90_150 > 0.24)
        {
            refDdm90_150 = 0.24;
        }

        if (refDdm90_150 < -0.24)
        {
            refDdm90_150 = -0.24;
        }
        
        var ddmSd = (float)_device.Server.Params[SimulationParams.SimDdmSd];
        var sdm = 0.8;
        var ddm90150WithRandom = refDdm90_150 + (_random.NextDouble() - 0.5) * ddmSd;
        var am90 = (sdm - ddm90150WithRandom) / 2.0;
        var am150 = (sdm + ddm90150WithRandom) / 2.0;
        payload.CrsAm90 = (float)am90;
        payload.TotalAm90 = (float)am90;
        payload.ClrAm90 = (float)am90;
        
        payload.CrsAm150 = (float)am150;
        payload.TotalAm150 = (float)am150;
        payload.ClrAm150 = (float)am150;

        var maxClrDbm = -40;
        var minClrDbm = -80;
        var maxCrsDbm = -60;
        var minCrsDbm = -100;
        
        
        
    }

    /// <summary>
    /// Releases the resources used by the object.
    /// </summary>
    public void Dispose()
    {
        _signalOverflowIndicator.Dispose();
    }
}