using System.Diagnostics;

namespace De.Hochstaetter.FroniusShared.Models;

public class SolarSystem : BindableBase, ISolarSystem
{
    private readonly SettingsShared settings;
    private Lazy<IList<SmartMeterCalibrationHistoryItem>>? historyLazy;

    public SolarSystem(SettingsShared settings)
    {
        this.settings = settings;
    }

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

    public Task ReadCalibrationHistory() => Task.Run(() =>
    {
        // Initialize history only when ReadCalibrationHistory is called for the first time.
        historyLazy ??= new Lazy<IList<SmartMeterCalibrationHistoryItem>>(() =>
        {
            if (!string.IsNullOrWhiteSpace(settings.DriftFileName) && File.Exists(settings.DriftFileName))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(List<SmartMeterCalibrationHistoryItem>));
                    using var stream = new FileStream(settings.DriftFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return serializer.Deserialize(stream) as IList<SmartMeterCalibrationHistoryItem> ?? new List<SmartMeterCalibrationHistoryItem>();
                }
                catch (Exception)
                {
                    return new List<SmartMeterCalibrationHistoryItem>();
                }
            }
            return new List<SmartMeterCalibrationHistoryItem>();
        });
    });

    public async Task WriteCalibrationHistoryItem(double consumedEnergyOffset, double producedEnergyOffset)
    {
        await ReadCalibrationHistory(); // Ensure the list is initialized before proceeding.
        Debug.Assert(historyLazy != null, nameof(historyLazy) + " != null");

        if (string.IsNullOrWhiteSpace(settings.DriftFileName))
        {
            return;
        }

        var directoryName = Path.GetDirectoryName(settings.DriftFileName);

        if (!Directory.Exists(Path.GetDirectoryName(settings.DriftFileName)))
        {
            try
            {
                Directory.CreateDirectory(directoryName!);
            }
            catch
            {
                return;
            }
        }

        // TODO: remove
        if (IoC.Get<IDataCollectionService>().HomeAutomationSystem is not { } homeAutomationSystem)
            return;

        var newItem = new SmartMeterCalibrationHistoryItem
        {
            CalibrationDate = DateTime.UtcNow,
            ConsumedOffset = consumedEnergyOffset,
            ProducedOffset = producedEnergyOffset,

            // TODO: this should be taken from the primary inverter, remove homeAutomationSystem
            EnergyRealConsumed = homeAutomationSystem.Gen24Sensors?.PrimaryPowerMeter?.EnergyActiveConsumed ?? double.NaN,
            EnergyRealProduced = homeAutomationSystem.Gen24Sensors?.PrimaryPowerMeter?.EnergyActiveProduced ?? double.NaN
        };

        historyLazy.Value.Add(newItem);

        if (historyLazy.Value.Count > 50)
        {
            historyLazy.Value.RemoveAt(0);
        }

        var serializer = new XmlSerializer(typeof(List<SmartMeterCalibrationHistoryItem>));
        await using var stream = new FileStream(settings.DriftFileName, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            IndentChars = new string(' ', 3),
            NewLineChars = Environment.NewLine,
            Async = true
        });
        serializer.Serialize(writer, historyLazy.Value as List<SmartMeterCalibrationHistoryItem> ?? historyLazy.Value.ToList());
    }

    public IList<SmartMeterCalibrationHistoryItem> SmartMeterHistory
    {
        get
        {
            if (historyLazy == null)
            {
                throw new InvalidOperationException("Calibration History has not been initialized yet.");
            }

            return historyLazy.Value;
        }
    }

    private static double consumedFactor = 1;
    private static int oldSmartMeterHistoryCountConsumed;

    private double GetConsumedFactor()
    {
        if (oldSmartMeterHistoryCountConsumed != SmartMeterHistory.Count)
        {
            var consumed = (IReadOnlyList<SmartMeterCalibrationHistoryItem>)SmartMeterHistory.Where(item => double.IsFinite(item.ConsumedOffset)).ToList();
            consumedFactor = CalculateSmartMeterFactor(consumed, false);
            oldSmartMeterHistoryCountConsumed = SmartMeterHistory.Count;
        }

        return consumedFactor;
    }

    private double producedFactor = 1;
    private static int oldSmartMeterHistoryCountProduced;

    private double GetProducedFactor()
    {
        if (oldSmartMeterHistoryCountProduced != SmartMeterHistory.Count)
        {
            var produced = (IReadOnlyList<SmartMeterCalibrationHistoryItem>)SmartMeterHistory.Where(item => double.IsFinite(item.ProducedOffset)).ToList();
            producedFactor = CalculateSmartMeterFactor(produced, true);
            oldSmartMeterHistoryCountProduced = SmartMeterHistory.Count;
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
