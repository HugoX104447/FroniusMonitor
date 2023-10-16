namespace De.Hochstaetter.FroniusShared.Contracts;

public interface ISolarSystem
{
    public Gen24PowerFlow? SitePowerFlow { get; set; }

    public double? LoadPowerCorrected { get; }
    public double? GridPowerCorrected { get; }
    IList<SmartMeterCalibrationHistoryItem> SmartMeterHistory { get; }

    Task ReadCalibrationHistory();
    Task WriteCalibrationHistoryItem(double consumedEnergyOffsetWattHours, double producedEnergyOffsetWattHours);
}
