namespace De.Hochstaetter.FroniusShared.Models;

public class SolarComponent : BindableBase
{
    private Gen24Sensors? gen24Sensors;
    public Gen24Sensors? Gen24Sensors
    {
        get => gen24Sensors;
        set => Set(ref gen24Sensors, value);
    }

    private Gen24Config? gen24Config;

    public Gen24Config? Gen24Config
    {
        get => gen24Config;
        set => Set(ref gen24Config, value);
    }
}
