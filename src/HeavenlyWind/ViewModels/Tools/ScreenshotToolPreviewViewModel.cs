﻿using Sakuno.KanColle.Amatsukaze.Services;
using Sakuno.UserInterface;
using Sakuno.UserInterface.Commands;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Sakuno.KanColle.Amatsukaze.ViewModels.Tools
{
    class ScreenshotToolPreviewViewModel : ModelBase
    {
        internal OverviewScreenshotToolViewModel Owner { get; }

        public BitmapSource Screenshot { get; private set; }

        public ICommand TakeScreenshotCommand { get; private set; }

        public ScreenshotToolPreviewViewModel(OverviewScreenshotToolViewModel rpOwner)
        {
            Owner = rpOwner;

            TakeScreenshotCommand = new DelegatedCommand(async () =>
            {
                Screenshot = await ScreenshotService.Instance.TakePartialScreenshot(ScreenshotToolViewModel.Regions[Owner.Type]);
                OnPropertyChanged(nameof(Screenshot));
            });
        }
    }
}
