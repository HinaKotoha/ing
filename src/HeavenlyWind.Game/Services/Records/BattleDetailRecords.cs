﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sakuno.KanColle.Amatsukaze.Game.Models;
using Sakuno.KanColle.Amatsukaze.Game.Models.Battle;
using Sakuno.KanColle.Amatsukaze.Game.Models.Raw;
using Sakuno.KanColle.Amatsukaze.Game.Models.Raw.Battle;
using Sakuno.KanColle.Amatsukaze.Game.Parsers;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Sakuno.KanColle.Amatsukaze.Game.Services.Records
{
    class BattleDetailRecords : RecordsGroup
    {
        enum ParticipantFleetType { Main, Escort, SupportFire }

        public override string GroupName => "battle_detail";
        public override int Version => 4;

        string r_Filename;
        SQLiteConnection r_Connection;

        long? r_CurrentBattleID;

        internal BattleDetailRecords(SQLiteConnection rpConnection, int rpUserID) : base(rpConnection)
        {
            r_Filename = new FileInfo(Path.Combine(RecordService.Instance.RecordDirectory.FullName, rpUserID + "_Battle.db")).FullName;

            r_Connection = new SQLiteConnection($@"Data Source={r_Filename}; Page Size=8192", true).OpenAndReturn();

            using (var rCommand = r_Connection.CreateCommand())
            {
                rCommand.CommandText =
                    "PRAGMA journal_mode = DELETE; " +
                    "PRAGMA foreign_keys = ON;";

                rCommand.ExecuteNonQuery();
            }

            var rSortieFirstStageApis = new[]
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
            };
            DisposableObjects.Add(ApiService.Subscribe(rSortieFirstStageApis, ProcessSortieFirstStage));
            DisposableObjects.Add(ApiService.Subscribe("api_req_practice/battle", ProcessPracticeFirstStage));

            var rSecondStageApis = new[]
            {
                "api_req_practice/midnight_battle",
                "api_req_battle_midnight/battle",
                "api_req_combined_battle/midnight_battle",
                "api_req_combined_battle/ec_midnight_battle",
                "api_req_practice/midnight_battle",
            };
            DisposableObjects.Add(ApiService.Subscribe(rSecondStageApis, ProcessSecondStage));

            var rBattleResultApis = new[]
            {
                "api_req_sortie/battleresult",
                "api_req_combined_battle/battleresult",
                "api_req_practice/battle_result",
            };
            DisposableObjects.Add(ApiService.Subscribe(rBattleResultApis, ProcessResult));
        }

        protected override void CreateTable()
        {
            using (var rCommand = r_Connection.CreateCommand())
            {
                rCommand.CommandText = "CREATE TABLE IF NOT EXISTS battle(" +
                    "id INTEGER PRIMARY KEY NOT NULL, " +
                    "first BLOB NOT NULL, " +
                    "second BLOB, " +
                    "result BLOB);" +

                "CREATE TABLE IF NOT EXISTS participant_fleet_name(" +
                    "id INTEGER PRIMARY KEY, " +
                    "name TEXT NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS participant_fleet(" +
                    "battle INTEGER NOT NULL REFERENCES battle(id), " +
                    "id INTEGER NOT NULL, " +
                    "name INTEGER NOT NULL REFERENCES participant_fleet_name(id), " +
                    "PRIMARY KEY(battle, id)) WITHOUT ROWID;" +

                "CREATE TABLE IF NOT EXISTS participant(" +
                    "battle INTEGER NOT NULL REFERENCES battle(id), " +
                    "id INTEGER NOT NULL, " +
                    "ship INTEGER NOT NULL, " +
                    "level INTEGER NOT NULL, " +
                    "condition INTEGER NOT NULL, " +
                    "fuel INTEGER NOT NULL, " +
                    "bullet INTEGER NOT NULL, " +
                    "firepower INTEGER NOT NULL, " +
                    "torpedo INTEGER NOT NULL, " +
                    "aa INTEGER NOT NULL, " +
                    "armor INTEGER NOT NULL, " +
                    "evasion INTEGER NOT NULL, " +
                    "asw INTEGER NOT NULL, " +
                    "los INTEGER NOT NULL, " +
                    "luck INTEGER NOT NULL, " +
                    "range INTEGER NOT NULL, " +
                    "PRIMARY KEY(battle, id)) WITHOUT ROWID;" +
                "CREATE TABLE IF NOT EXISTS participant_slot(" +
                    "battle INTEGER NOT NULL REFERENCES battle(id), " +
                    "participant INTEGER NOT NULL, " +
                    "id INTEGER NOT NULL, " +
                    "equipment INTEGER NOT NULL, " +
                    "level INTEGER NOT NULL, " +
                    "plane_count INTEGER NOT NULL, " +
                    "PRIMARY KEY(battle, participant, id), " +
                    "FOREIGN KEY(battle, participant) REFERENCES participant(battle, id)) WITHOUT ROWID;" +

                "CREATE TABLE IF NOT EXISTS participant_heavily_damaged(" +
                    "battle INTEGER NOT NULL REFERENCES battle(id), " +
                    "id INTEGER NOT NULL, " +
                    "PRIMARY KEY(battle, id), " +
                    "FOREIGN KEY(battle, id) REFERENCES participant(battle, id)) WITHOUT ROWID;" +

                "CREATE TABLE IF NOT EXISTS practice_opponent(" +
                    "id INTEGER PRIMARY KEY NOT NULL, " +
                    "name TEXT NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS practice_opponent_comment(" +
                    "id INTEGER PRIMARY KEY NOT NULL, " +
                    "comment TEXT NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS practice_opponent_fleet(" +
                    "id INTEGER PRIMARY KEY NOT NULL, " +
                    "name TEXT NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS practice(" +
                    "id INTEGER PRIMARY KEY NOT NULL, " +
                    "opponent INTEGER NOT NULL, " +
                    "opponent_level INTEGER NOT NULL, " +
                    "opponent_experience INTEGER NOT NULL, " +
                    "opponent_rank INTEGER NOT NULL, " +
                    "opponent_comment INTEGER NOT NULL REFERENCES practice_opponent_comment(id), " +
                    "opponent_fleet INTEGER NOT NULL REFERENCES practice_opponent_fleet(id), " +
                    "rank INTEGER); " +

                "CREATE VIEW IF NOT EXISTS participant_hd_view AS " +
                    "SELECT participant_heavily_damaged.battle AS battle, group_concat(participant.ship) AS ships FROM participant_heavily_damaged " +
                    "JOIN participant participant ON participant_heavily_damaged.battle = participant.battle AND participant_heavily_damaged.id = participant.id " +
                    "GROUP BY participant_heavily_damaged.battle;";

                rCommand.ExecuteNonQuery();
            }

            using (var rCommand = Connection.CreateCommand())
            {
                rCommand.CommandText = "ATTACH @battle_detail_db AS battle_detail";
                rCommand.Parameters.AddWithValue("@battle_detail_db", r_Filename);
                rCommand.ExecuteNonQuery();
            }

            if (r_Connection != null)
            {
                r_Connection.Dispose();
                r_Connection = null;
            }
        }

        protected override void UpgradeFromOldVersionPreprocessStep(int rpOldVersion)
        {
            if (rpOldVersion == 1)
            {
                using (var rCommand = r_Connection.CreateCommand())
                {
                    rCommand.CommandText =
                        "ALTER TABLE participant RENAME TO participant_old;" +

                        "CREATE TABLE IF NOT EXISTS participant(battle INTEGER NOT NULL REFERENCES battle(id), id INTEGER NOT NULL, ship INTEGER NOT NULL, level INTEGER NOT NULL, condition INTEGER NOT NULL, fuel INTEGER NOT NULL, bullet INTEGER NOT NULL, firepower INTEGER NOT NULL, torpedo INTEGER NOT NULL, aa INTEGER NOT NULL, armor INTEGER NOT NULL, evasion INTEGER NOT NULL, asw INTEGER NOT NULL, los INTEGER NOT NULL, luck INTEGER NOT NULL, range INTEGER NOT NULL, PRIMARY KEY(battle, id)) WITHOUT ROWID;" +

                        "INSERT INTO participant(battle, id, ship, level, condition, fuel, bullet, firepower, torpedo, aa, armor, evasion, asw, los, luck, range) SELECT battle, id, ship, level, condition, -1 AS fuel, -1 AS bullet, firepower, torpedo, aa, armor, evasion, asw, los, luck, range FROM participant_old;" +

                        "DROP TABLE participant_old;";

                    rCommand.ExecuteNonQuery();
                }
            }
            if (rpOldVersion < 4)
            {
                using (var rCommand = r_Connection.CreateCommand())
                {
                    var rEquipmentIDs = string.Join(", ", KanColleGame.Current.MasterInfo.Equipment.Values.Where(r => r.IsPlane).Select(r => r.ID));
                    rCommand.CommandText = $"UPDATE participant_slot SET level = level << 4 WHERE equipment IN ({rEquipmentIDs}) AND level < 10;";
                    rCommand.ExecuteNonQuery();
                }
            }
        }

        byte[] CompressJson(JToken rpJsonToken)
        {
            var rMemoryStream = new MemoryStream();

            using (var rCompressStream = new DeflateStream(rMemoryStream, CompressionMode.Compress))
            using (var rStreamWriter = new StreamWriter(rCompressStream))
            using (var rJsonTextWriter = new JsonTextWriter(rStreamWriter))
                rpJsonToken.WriteTo(rJsonTextWriter);

            return rMemoryStream.ToArray();
        }

        void ProcessSortieFirstStage(ApiInfo rpInfo)
        {
            var rSortie = SortieInfo.Current;
            r_CurrentBattleID = BattleInfo.Current.ID;

            using (var rTransaction = Connection.BeginTransaction())
            using (var rCommand = Connection.CreateCommand())
            {
                rCommand.CommandText = "INSERT INTO battle_detail.battle(id, first) VALUES(@battle_id, @first);";
                rCommand.Parameters.AddWithValue("@battle_id", r_CurrentBattleID.Value);
                rCommand.Parameters.AddWithValue("@first", CompressJson(rpInfo.Json["api_data"]));

                rCommand.ExecuteNonQuery();

                ProcessParticipantFleet(rCommand, rSortie.Fleet, ParticipantFleetType.Main);
                if (rSortie.EscortFleet != null)
                    ProcessParticipantFleet(rCommand, rSortie.EscortFleet, ParticipantFleetType.Escort);

                var rData = rpInfo.Data as RawDay;
                if (rData != null && rData.SupportingFireType != 0)
                {
                    var rSupportFire = rData.SupportingFire;
                    var rFleetID = (rSupportFire.SupportShelling?.FleetID ?? rSupportFire.AerialSupport?.FleetID).Value;
                    ProcessParticipantFleet(rCommand, KanColleGame.Current.Port.Fleets[rFleetID], ParticipantFleetType.SupportFire);
                }

                rTransaction.Commit();
            }
        }
        void ProcessPracticeFirstStage(ApiInfo rpInfo)
        {
            var rParticipantFleet = KanColleGame.Current.Port.Fleets[int.Parse(rpInfo.Parameters["api_deck_id"])];
            var rPractice = KanColleGame.Current.Practice;
            var rOpponent = rPractice.Opponent;
            r_CurrentBattleID = rPractice.Battle.ID;

            using (var rTransaction = Connection.BeginTransaction())
            using (var rCommand = Connection.CreateCommand())
            {
                rCommand.CommandText = "INSERT OR IGNORE INTO practice_opponent(id, name) VALUES(@opponent_id, @opponent_name);" +
                    "INSERT OR IGNORE INTO practice_opponent_comment(id, comment) VALUES(@opponent_comment_id, @opponent_coment);" +
                    "INSERT OR IGNORE INTO practice_opponent_fleet(id, name) VALUES(@opponent_fleet_name_id, @opponent_fleet_name);" +
                    "INSERT INTO practice(id, opponent, opponent_level, opponent_experience, opponent_rank, opponent_comment, opponent_fleet) VALUES(@battle_id, @opponent_id, @opponent_level, @opponent_experience, @opponent_rank, @opponent_comment_id, @opponent_fleet_name_id);" +
                    "INSERT INTO battle_detail.battle(id, first) VALUES(@battle_id, @first);";
                rCommand.Parameters.AddWithValue("@opponent_id", rOpponent.RawData.ID);
                rCommand.Parameters.AddWithValue("@opponent_name", rOpponent.Name);
                rCommand.Parameters.AddWithValue("@opponent_comment_id", rOpponent.RawData.CommentID ?? -1);
                rCommand.Parameters.AddWithValue("@opponent_coment", rOpponent.Comment);
                rCommand.Parameters.AddWithValue("@opponent_fleet_name_id", rOpponent.RawData.FleetNameID ?? -1);
                rCommand.Parameters.AddWithValue("@opponent_fleet_name", rOpponent.FleetName);
                rCommand.Parameters.AddWithValue("@opponent_level", rOpponent.Level);
                rCommand.Parameters.AddWithValue("@opponent_experience", rOpponent.RawData.Experience[0]);
                rCommand.Parameters.AddWithValue("@opponent_rank", (int)rOpponent.Rank);
                rCommand.Parameters.AddWithValue("@battle_id", r_CurrentBattleID.Value);
                rCommand.Parameters.AddWithValue("@first", CompressJson(rpInfo.Json["api_data"]));
                rCommand.ExecuteNonQuery();

                ProcessParticipantFleet(rCommand, rParticipantFleet, ParticipantFleetType.Main);

                rTransaction.Commit();
            }
        }
        void ProcessSecondStage(ApiInfo rpInfo)
        {
            if (!r_CurrentBattleID.HasValue)
                return;

            using (var rCommand = Connection.CreateCommand())
            {
                rCommand.CommandText = "UPDATE battle_detail.battle SET second = @second WHERE id = @id;";
                rCommand.Parameters.AddWithValue("@id", r_CurrentBattleID.Value);
                rCommand.Parameters.AddWithValue("@second", CompressJson(rpInfo.Json["api_data"]));

                rCommand.ExecuteNonQuery();
            }
        }
        void ProcessResult(ApiInfo rpInfo)
        {
            if (!r_CurrentBattleID.HasValue)
                return;

            using (var rTransaction = Connection.BeginTransaction())
            {
                using (var rCommand = Connection.CreateCommand())
                {
                    rCommand.CommandText = "UPDATE battle_detail.battle SET result = @result WHERE id = @id;";
                    rCommand.Parameters.AddWithValue("@id", r_CurrentBattleID.Value);
                    rCommand.Parameters.AddWithValue("@result", CompressJson(rpInfo.Json["api_data"]));

                    if (rpInfo.Api == "api_req_practice/battle_result")
                    {
                        rCommand.CommandText += "UPDATE battle_detail.practice SET rank = @rank WHERE id = @id;";
                        rCommand.Parameters.AddWithValue("@rank", (int)((RawBattleResult)rpInfo.Data).Rank);
                    }

                    rCommand.ExecuteNonQuery();
                }

                var rStage = BattleInfo.Current.CurrentStage;
                ProcessHeavilyDamagedShip(rStage.FriendMain, ParticipantFleetType.Main);
                if (rStage.FriendEscort != null)
                    ProcessHeavilyDamagedShip(rStage.FriendEscort, ParticipantFleetType.Escort);

                rTransaction.Commit();
            }

            r_CurrentBattleID = null;
        }
        void ProcessHeavilyDamagedShip(IList<BattleParticipantSnapshot> rpParticipants, ParticipantFleetType rpType)
        {
            for (var i = 0; i < rpParticipants.Count; i++)
            {
                var rID = (int)rpType * 6 + i;
                var rState = rpParticipants[i].State;
                if (rState == BattleParticipantState.HeavilyDamaged || rState == BattleParticipantState.Sunk)
                    using (var rCommand = Connection.CreateCommand())
                    {
                        rCommand.CommandText = "INSERT INTO battle_detail.participant_heavily_damaged(battle, id) VALUES(@battle_id, @id);";
                        rCommand.Parameters.AddWithValue("@battle_id", r_CurrentBattleID.Value);
                        rCommand.Parameters.AddWithValue("@id", rID);

                        rCommand.ExecuteNonQuery();
                    }
            }
        }

        void ProcessParticipantFleet(SQLiteCommand command, Fleet rpFleet, ParticipantFleetType rpType)
        {
            var rFleetID = (int)rpType;

            command.CommandText =
                "INSERT OR IGNORE INTO battle_detail.participant_fleet_name(id, name) VALUES(@pf_id, @pf_name);" +
                "INSERT INTO battle_detail.participant_fleet(battle, id, name) VALUES(@battle_id, @fid, @pf_id);";
            command.Parameters.AddWithValue("@pf_id", rpFleet.RawData.NameID ?? -rpFleet.ID);
            command.Parameters.AddWithValue("@pf_name", rpFleet.Name);
            command.Parameters.AddWithValue("@fid", rFleetID);
            command.ExecuteNonQuery();

            for (var i = 0; i < rpFleet.Ships.Count; i++)
            {
                var rShip = rpFleet.Ships[i];
                var rID = rFleetID * 6 + i;

                command.CommandText =
                    "INSERT INTO battle_detail.participant(battle, id, ship, level, condition, fuel, bullet, firepower, torpedo, aa, armor, evasion, asw, los, luck, range) " +
                        "VALUES(@battle_id, @sid, @siid, @slv, @sc, @sfuel, @sbullet, @sfirepower, @storpedo, @saa, @sarmor, @sevasion, @sasw, @slos, @sluck, @srange);";

                command.Parameters.AddWithValue("@sid", rID);
                command.Parameters.AddWithValue("@siid", rShip.Info.ID);
                command.Parameters.AddWithValue("@slv", rShip.Level);
                command.Parameters.AddWithValue("@sc", rShip.Condition);
                command.Parameters.AddWithValue("@sfuel", rShip.Fuel.Current);
                command.Parameters.AddWithValue("@sbullet", rShip.Bullet.Current);
                command.Parameters.AddWithValue("@sfirepower", rShip.Status.FirepowerBase.Current);
                command.Parameters.AddWithValue("@storpedo", rShip.Status.TorpedoBase.Current);
                command.Parameters.AddWithValue("@saa", rShip.Status.AABase.Current);
                command.Parameters.AddWithValue("@sarmor", rShip.Status.ArmorBase.Current);
                command.Parameters.AddWithValue("@sevasion", rShip.Status.Evasion);
                command.Parameters.AddWithValue("@sasw", rShip.Status.ASW);
                command.Parameters.AddWithValue("@slos", rShip.Status.LoS);
                command.Parameters.AddWithValue("@sluck", rShip.Status.Luck);
                command.Parameters.AddWithValue("@srange", rShip.Range);
                command.ExecuteNonQuery();

                for (var j = 0; j < rShip.Slots.Count; j++)
                {
                    var rSlot = rShip.Slots[j];
                    if (!rSlot.HasEquipment)
                        break;

                    var rLevelAndProficiency = rSlot.Equipment.Level + (rSlot.Equipment.Proficiency << 4);
                    command.CommandText = "INSERT INTO battle_detail.participant_slot(battle, participant, id, equipment, level, plane_count) VALUES(@battle_id, @sid, @eid, @eeid, @elp, @epc);";
                    command.Parameters.AddWithValue("@eid", j);
                    command.Parameters.AddWithValue("@eeid", rSlot.Equipment.Info.ID);
                    command.Parameters.AddWithValue("@elp", rLevelAndProficiency);
                    command.Parameters.AddWithValue("@epc", rSlot.PlaneCount);
                    command.ExecuteNonQuery();
                }

                if (rShip.ExtraSlot != null)
                {
                    command.CommandText = "INSERT INTO battle_detail.participant_slot(battle, participant, id, equipment, level, plane_count) VALUES(@battle_id, @sid, -1, @exslot, 0, 0);";
                    command.Parameters.AddWithValue("@exslot", rShip.ExtraSlot.Equipment.Info.ID);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
