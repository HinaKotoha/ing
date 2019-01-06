﻿using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sakuno.ING.Composition;
using Sakuno.ING.Data;
using Sakuno.ING.Game.Logger.Entities;
using Sakuno.ING.Game.Models;

namespace Sakuno.ING.Game.Logger
{
    [Export(typeof(Logger), LazyCreate = false)]
    public class Logger
    {
        private readonly IDataService dataService;
        private readonly NavalBase navalBase;

        private ShipCreationEntity shipCreation;
        private BuildingDockId lastBuildingDock;

        public Logger(IDataService dataService, GameProvider provider, NavalBase navalBase)
        {
            this.dataService = dataService;
            this.navalBase = navalBase;

            provider.EquipmentCreated += (t, m) =>
            {
                using (var context = CreateContext())
                {
                    context.EquipmentCreationTable.Add(new EquipmentCreationEntity
                    {
                        TimeStamp = t,
                        Consumption = m.Consumption,
                        EquipmentCreated = m.SelectedEquipentInfoId,
                        IsSuccess = m.IsSuccess,
                        AdmiralLevel = this.navalBase.Admiral.Leveling.Level,
                        Secretary = this.navalBase.Secretary.Info.Id,
                        SecretaryLevel = this.navalBase.Secretary.Leveling.Level
                    });
                    context.SaveChanges();
                }
            };

            provider.ShipCreated += (t, m) =>
            {
                shipCreation = new ShipCreationEntity
                {
                    TimeStamp = t,
                    Consumption = m.Consumption,
                    IsLSC = m.IsLSC,
                    AdmiralLevel = this.navalBase.Admiral.Leveling.Level,
                    Secretary = this.navalBase.Secretary.Info.Id,
                    SecretaryLevel = this.navalBase.Secretary.Leveling.Level
                };
                lastBuildingDock = m.BuildingDockId;
            };

            provider.BuildingDockUpdated += (t, m) =>
            {
                if (shipCreation != null)
                    using (var context = CreateContext())
                    {
                        shipCreation.ShipBuilt = m.Single(x => x.Id == lastBuildingDock).BuiltShipId.Value;
                        shipCreation.EmptyDockCount = navalBase.BuildingDocks.Count(x => x.State == BuildingDockState.Empty);
                        context.ShipCreationTable.Add(shipCreation);
                        shipCreation = null;
                        lastBuildingDock = default;
                        context.SaveChanges();
                    }
            };

            provider.ExpeditionCompleted += (t, m) =>
            {
                using (var context = CreateContext())
                {
                    context.ExpeditionCompletionTable.Add(new ExpeditionCompletionEntity
                    {
                        TimeStamp = t,
                        ExpeditionId = this.navalBase.Fleets[m.FleetId].Expedition.Id,
                        ExpeditionName = m.ExpeditionName,
                        Result = m.Result,
                        MaterialsAcquired = m.MaterialsAcquired,
                        RewardItem1 = m.RewardItem1,
                        RewardItem2 = m.RewardItem2
                    });
                    context.SaveChanges();
                }
            };

#if DEBUG
            using (var context = CreateContext())
                context.Database.Migrate();
#endif

            provider.AdmiralUpdated += (t, m) =>
            {
                if (PlayerLoaded)
                    using (var context = CreateContext())
                        context.Database.Migrate();
            };
        }

        public bool PlayerLoaded
#if DEBUG
            => true;
#else
            => navalBase.Admiral != null;
#endif

        public LoggerContext CreateContext()
            => new LoggerContext(dataService.ConfigureDbContext<LoggerContext>
            (
                navalBase.Admiral?.Id.ToString() ??
#if DEBUG
                    "0",
#else
                    throw new System.InvalidOperationException("Game not loaded"),
#endif
                "logs"
            ));
    }
}
