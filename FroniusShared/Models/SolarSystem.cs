namespace De.Hochstaetter.FroniusShared.Models;

public class SolarSystem : BindableBase
{
    private Gen24PowerFlow? sitePowerFlow;

    public Gen24PowerFlow? SitePowerFlow
    {
        get => sitePowerFlow;
        set => Set(ref sitePowerFlow, value, () =>
        {
            NotifyOfPropertyChange(nameof(GridPowerCorrected));
            NotifyOfPropertyChange(nameof(LoadPowerCorrected));
        });
    }

    public double? LoadPowerCorrected => SitePowerFlow?.LoadPower + SitePowerFlow?.GridPower - GridPowerCorrected;

    public double? GridPowerCorrected => SitePowerFlow?.GridPower * (SitePowerFlow?.GridPower < 0 ? GetProducedFactor() : GetConsumedFactor());

    private static readonly IList<SmartMeterCalibrationHistoryItem> history = IoC.TryGet<IDataCollectionService>()?.SmartMeterHistory!;

    private static double consumedFactor = 1;
    private static int oldSmartMeterHistoryCountConsumed;

    private static double GetConsumedFactor()
    {
        if (oldSmartMeterHistoryCountConsumed != history.Count)
        {
            var consumed = (IReadOnlyList<SmartMeterCalibrationHistoryItem>)history.Where(item => double.IsFinite(item.ConsumedOffset)).ToList();
            consumedFactor = CalculateSmartMeterFactor(consumed, false);
            oldSmartMeterHistoryCountConsumed = history.Count;
        }

        return consumedFactor;
    }

    private static double producedFactor = 1;
    private static int oldSmartMeterHistoryCountProduced;

    private static double GetProducedFactor()
    {
        if (oldSmartMeterHistoryCountProduced != history.Count)
        {
            var produced = (IReadOnlyList<SmartMeterCalibrationHistoryItem>)history.Where(item => double.IsFinite(item.ProducedOffset)).ToList();
            producedFactor = CalculateSmartMeterFactor(produced, true);
            oldSmartMeterHistoryCountProduced = history.Count;
        }

        return producedFactor;
    }

    private static double CalculateSmartMeterFactor(IReadOnlyList<SmartMeterCalibrationHistoryItem> list, bool isProduced)
    {
        if (list.Count < 2)
        {
            return 1.0;
        }

        var first = list[0];
        var last = list[^1];
        var rawEnergy = (isProduced ? last.EnergyRealProduced : last.EnergyRealConsumed) - (isProduced ? first.EnergyRealProduced : first.EnergyRealConsumed);
        var offsetEnergy = (isProduced ? last.ProducedOffset : last.ConsumedOffset) - (isProduced ? first.ProducedOffset : first.ConsumedOffset);
        return (rawEnergy + offsetEnergy) / rawEnergy;
    }
}
