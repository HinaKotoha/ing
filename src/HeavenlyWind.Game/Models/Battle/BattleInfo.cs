using Sakuno.KanColle.Amatsukaze.Game.Models.Battle.Stages;
using Sakuno.KanColle.Amatsukaze.Game.Models.Raw;
using Sakuno.KanColle.Amatsukaze.Game.Models.Raw.Battle;
using Sakuno.KanColle.Amatsukaze.Game.Parsers;
using Sakuno.KanColle.Amatsukaze.Game.Services;
using System;
using System.Linq;

namespace Sakuno.KanColle.Amatsukaze.Game.Models.Battle
{
    public class BattleInfo : ModelBase
    {
        internal static BattleInfo Current { get; private set; }

        public long ID { get; }

        public bool IsInitialized { get; private set; }

        public bool IsBossBattle { get; }
        public bool IsPractice { get; }

        public BattleParticipants Participants { get; } = new BattleParticipants();

        public BattleStage CurrentStage { get; private set; }
        public BattleStage First { get; private set; }
        public BattleStage Second { get; private set; }

        public Formation FriendFormation { get; private set; }
        public Formation EnemyFormation { get; private set; }
        public EngagementForm EngagementForm { get; private set; }

        public AerialCombat AerialCombat { get; } = new AerialCombat();

        public BattleResult Result { get; } = new BattleResult();

        public bool IsSupportFleetReady { get; private set; }
        public bool IsLandBaseAerialSupportReady { get; }

        static BattleInfo()
        {
            ApiService.Subscribe("api_port/port", _ => Current = null);

            var rFirstStages = new[]
            {
                "api_req_sortie/battle",
                "api_req_battle_midnight/sp_midnight",
                "api_req_sortie/airbattle",
                "api_req_sortie/ld_airbattle",
                "api_req_sortie/night_to_day",
                "api_req_sortie/ld_shooting",
                "api_req_combined_battle/airbattle",
                "api_req_combined_battle/battle",
                "api_req_combined_battle/battle_water",
                "api_req_combined_battle/sp_midnight",
                "api_req_combined_battle/ld_airbattle",
                "api_req_combined_battle/ec_battle",
                "api_req_combined_battle/ec_night_to_day",
                "api_req_combined_battle/each_battle",
                "api_req_combined_battle/each_battle_water",
                "api_req_combined_battle/ld_shooting",

                "api_req_practice/battle",
            };
            ApiService.Subscribe(rFirstStages, r => Current?.ProcessFirstStage(r));

            var rSecondStages = new[]
            {
                "api_req_battle_midnight/battle",
                "api_req_combined_battle/midnight_battle",
                "api_req_combined_battle/midnight_battle",
                "api_req_combined_battle/ec_midnight_battle",

                "api_req_practice/midnight_battle",
            };
            ApiService.Subscribe(rSecondStages, r => Current?.ProcessSecondStage(r));
        }
        internal BattleInfo(long rpTimestamp, RawMapExploration rpData)
        {
            Current = this;

            ID = rpTimestamp;

            var rSortie = SortieInfo.Current;
            Participants.FriendMain = rSortie.MainShips;
            Participants.FriendEscort = rSortie.EscortShips;

            foreach (FriendShip rShip in rSortie.MainShips)
                rShip.Reset();
            if (rSortie.EscortShips != null)
                foreach (FriendShip rShip in rSortie.EscortShips)
                    rShip.Reset();

            CurrentStage = new FakeStage(this);
            OnPropertyChanged(nameof(CurrentStage));

            IsBossBattle = rpData.NodeEventType == SortieEventType.BossBattle;

            var battleType = (BattleType)rpData.NodeEventSubType;
            if (battleType == BattleType.Normal || battleType == BattleType.NightOnly || battleType == BattleType.EnemyCombinedFleet)
            {
                var rSupportFleets = KanColleGame.Current.Port.Fleets
                    .Where(r => r.ExpeditionStatus.Expedition != null && !r.ExpeditionStatus.Expedition.CanReturn)
                    .Select(r => r.ExpeditionStatus.Expedition)
                    .SingleOrDefault(r => r.MapArea.ID == rSortie.Map.MasterInfo.AreaID && r.Time == (!IsBossBattle ? 15 : 30));

                IsSupportFleetReady = rSupportFleets != null;
            }

            if (rSortie.LandBaseAerialSupportRequests != null)
            {
                var rNodeUniqueID = MapService.Instance.GetNodeUniqueID(rSortie.Map.ID, rpData.Node);
                if (rNodeUniqueID.HasValue && Array.IndexOf(rSortie.LandBaseAerialSupportRequests, rNodeUniqueID.Value) != -1)
                    IsLandBaseAerialSupportReady = true;
            }
        }
        internal BattleInfo(long rpTimestamp, Fleet rpParticipantFleet)
        {
            Current = this;

            ID = rpTimestamp;

            IsPractice = true;

            Participants.FriendMain = rpParticipantFleet.Ships.Select(r => new FriendShip(r)).ToList<IParticipant>();
        }

        void ProcessFirstStage(ApiInfo rpInfo)
        {
            SetEnemy((RawBattleBase)rpInfo.Data);
            SetFormationAndEngagementForm(rpInfo);

            switch (rpInfo.Api)
            {
                case "api_req_sortie/battle":
                case "api_req_practice/battle":
                    First = new DayNormalStage(this, rpInfo);
                    break;

                case "api_req_battle_midnight/sp_midnight": First = new NightOnlyStage(this, rpInfo); break;

                case "api_req_sortie/airbattle":
                case "api_req_combined_battle/airbattle":
                    First = new AerialCombatStage(this, rpInfo);
                    break;

                case "api_req_sortie/ld_airbattle":
                case "api_req_combined_battle/ld_airbattle":
                    First = new AerialAttackStage(this, rpInfo);
                    break;

                case "api_req_sortie/ld_shooting":
                case "api_req_combined_battle/ld_shooting":
                    First = new EnemyAmbushStage(this, rpInfo);
                    break;

                case "api_req_sortie/night_to_day": First = new NightToDayStage(this, rpInfo); break;

                case "api_req_combined_battle/battle": First = new FriendCombinedFleetCTFDayNormalStage(this, rpInfo); break;
                case "api_req_combined_battle/battle_water": First = new FriendCombinedFleetSTFDayNormalStage(this, rpInfo); break;

                case "api_req_combined_battle/sp_midnight": First = new FriendCombinedFleetNightOnlyStage(this, rpInfo); break;

                case "api_req_combined_battle/ec_battle": First = new EnemyCombinedFleetDayStage(this, rpInfo); break;
                case "api_req_combined_battle/ec_night_to_day": First = new EnemyCombinedFleetNightToDayStage(this, rpInfo); break;

                case "api_req_combined_battle/each_battle": First = new CombinedFleetCTFDayNormalStage(this, rpInfo); break;
                case "api_req_combined_battle/each_battle_water": First = new CombinedFleetSTFDayNormalStage(this, rpInfo); break;
            }

            First.Process(rpInfo);
            First.Postprocess();
            Result.Update(First, Second);

            IsInitialized = true;

            CurrentStage = First;
            OnPropertyChanged(nameof(First));
            OnPropertyChanged(nameof(CurrentStage));
            OnPropertyChanged(nameof(AerialCombat));
            OnPropertyChanged(nameof(IsInitialized));
        }
        void SetEnemy(RawBattleBase rpData)
        {
            Participants.EnemyMain = rpData.EnemyShipTypeIDs.Select((r, i) =>
            {
                var rLevel = rpData.EnemyShipLevels[i];
                var rEquipment = rpData.EnemyEquipment[i];

                return new EnemyShip(r, rLevel, rEquipment);
            }).ToArray<IParticipant>();

            var rEnemyCombinedFleetData = rpData as IRawEnemyCombinedFleet;
            if (rEnemyCombinedFleetData == null)
                return;

            Participants.EnemyEscort = rEnemyCombinedFleetData.EnemyEscortShipTypeIDs.Select((r, i) =>
            {
                var rLevel = rEnemyCombinedFleetData.EnemyEscortShipLevels[i];
                var rEquipment = rEnemyCombinedFleetData.EnemyEscortShipEquipment[i];

                return new EnemyShip(r, rLevel, rEquipment);
            }).ToArray<IParticipant>();
        }
        void SetFormationAndEngagementForm(ApiInfo rpInfo)
        {
            var rFormationRawData = rpInfo.Data as IRawFormationAndEngagementForm;

            FriendFormation = (Formation)rFormationRawData.FormationAndEngagementForm[0];
            EnemyFormation = (Formation)rFormationRawData.FormationAndEngagementForm[1];
            EngagementForm = (EngagementForm)rFormationRawData.FormationAndEngagementForm[2];

            OnPropertyChanged(nameof(FriendFormation));
            OnPropertyChanged(nameof(EnemyFormation));
            OnPropertyChanged(nameof(EngagementForm));
        }

        void ProcessSecondStage(ApiInfo rpInfo)
        {
            switch (rpInfo.Api)
            {
                case "api_req_battle_midnight/battle":
                case "api_req_practice/midnight_battle":
                    Second = new NightNormalStage(this, rpInfo);
                    break;

                case "api_req_combined_battle/midnight_battle": Second = new FriendCombinedFleetNightNormalStage(this, rpInfo); break;

                case "api_req_combined_battle/ec_midnight_battle": Second = new EnemyCombinedFleetNightStage(this, rpInfo); break;
            }

            Second.Process(rpInfo, First);
            InheritFromPreviousStage(Second);
            Second.Postprocess();
            Result.Update(First, Second);

            CurrentStage = Second;
            OnPropertyChanged(nameof(Second));
            OnPropertyChanged(nameof(CurrentStage));
        }
        void InheritFromPreviousStage(BattleStage rpStage)
        {
            if (rpStage.FriendEscort == null)
                for (var i = 0; i < rpStage.FriendMain.Count; i++)
                {
                    rpStage.FriendMain[i].DamageGivenToOpponent += CurrentStage.FriendMain[i].DamageGivenToOpponent;
                    rpStage.FriendMain[i].Inaccurate = CurrentStage.FriendMain[i].Inaccurate;
                }

            for (var i = 0; i < rpStage.Enemy.Count; i++)
                rpStage.Enemy[i].DamageGivenToOpponent += CurrentStage.Enemy[i].DamageGivenToOpponent;

            if (rpStage.FriendEscort != null)
                for (var i = 0; i < rpStage.FriendEscort.Count; i++)
                {
                    rpStage.FriendEscort[i].DamageGivenToOpponent += CurrentStage.FriendEscort[i].DamageGivenToOpponent;
                    rpStage.FriendEscort[i].Inaccurate = CurrentStage.FriendEscort[i].Inaccurate;
                }
        }
    }
}
