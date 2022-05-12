﻿using System.Windows.Input;
using De.Hochstaetter.Fronius.Contracts;
using De.Hochstaetter.Fronius.Models.Gen24.Settings;
using De.Hochstaetter.Fronius.Services;
using De.Hochstaetter.FroniusMonitor.Wpf.Commands;

namespace De.Hochstaetter.FroniusMonitor.ViewModels
{
    public class SelfConsumptionOptimizationViewModel : ViewModelBase
    {
        private readonly IWebClientService webClientService;
        private Gen24BatterySettings oldSettings = null!;

        public SelfConsumptionOptimizationViewModel(IWebClientService webClientService)
        {
            this.webClientService = webClientService;
        }

        private Gen24BatterySettings settings = null!;

        public Gen24BatterySettings Settings
        {
            get => settings;
            set => Set(ref settings, value);
        }

        private double logGridPower;

        public double LogGridPower
        {
            get => logGridPower;
            set => Set(ref logGridPower, value, UpdateGridPower);
        }

        private bool isFeedIn;

        public bool IsFeedIn
        {
            get => isFeedIn;
            set => Set(ref isFeedIn, value, UpdateGridPower);
        }

        private byte socMin;

        public byte SocMin
        {
            get => socMin;
            set => Set(ref socMin, value, () =>
            {
                Settings.SocMin = value;

                if (SocMax < value)
                {
                    Settings.SocMax = SocMax = value;
                }
            });
        }

        private byte socMax;

        public byte SocMax
        {
            get => socMax;
            set => Set(ref socMax, value, () =>
            {
                Settings.SocMax = value;

                if (SocMin > value)
                {
                    Settings.SocMin = SocMin = value;
                }
            });
        }

        private ICommand? undoCommand;
        public ICommand UndoCommand => undoCommand ??= new NoParameterCommand(Revert);

        private ICommand? applyCommand;
        public ICommand ApplyCommand => applyCommand ??= new NoParameterCommand(Apply);

        internal override async Task OnInitialize()
        {
            await base.OnInitialize().ConfigureAwait(false);
            oldSettings = await webClientService.ReadGen24Entity<Gen24BatterySettings>("config/batteries").ConfigureAwait(false);
            Revert();
        }

        private void UpdateGridPower()
        {
            Settings.RequestedGridPower = (int)Math.Round(Math.Pow(10, LogGridPower) * (IsFeedIn ? -1 : 1), MidpointRounding.AwayFromZero);
        }

        private void Revert()
        {
            Settings = (Gen24BatterySettings)oldSettings.Clone();
            socMin = Settings.SocMin ?? 5;
            socMax = Settings.SocMax ?? 100;
            isFeedIn = Settings.RequestedGridPower < 0;
            NotifyOfPropertyChange(nameof(IsFeedIn));
            NotifyOfPropertyChange(nameof(SocMin));
            NotifyOfPropertyChange(nameof(SocMax));
            LogGridPower = Math.Log10(Math.Abs(Settings.RequestedGridPower ?? .0000001));
        }

        private async void Apply()
        {
            var updateToken = webClientService.GetUpdateToken(Settings, oldSettings);
            var result = await ((WebClientService)webClientService).GetFroniusJsonResponse("config/batteries", updateToken).ConfigureAwait(false);
            oldSettings = Settings;
            Revert();
        }
    }
}