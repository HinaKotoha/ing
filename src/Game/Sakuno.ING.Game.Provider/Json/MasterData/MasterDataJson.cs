﻿using System.Collections.Generic;
using Sakuno.ING.Game.Models.MasterData;

namespace Sakuno.ING.Game.Json.MasterData
{
    internal class MasterDataJson
    {
        public ShipInfoJson[] api_mst_ship;
        public List<EquipmentInfoId> api_mst_equip_exslot;
        public EquipmentInExtraSlotJson[] api_mst_equip_exslot_ship;
        public ShipTypeInfoJson[] api_mst_stype;
        public List<EquipmentTypeInfoJson> api_mst_slotitem_equiptype;
        public List<EquipmentInfoJson> api_mst_slotitem;
        public FurnitureInfoJson[] api_mst_furniture;
        public UseItemJson[] api_mst_useitem;
        public MapAreaJson[] api_mst_maparea;
        public MapInfoJson[] api_mst_mapinfo;
        public List<MapBgmInfoJson> api_mst_mapbgm;
        public ExpeditionInfoJson[] api_mst_mission;
        public List<ShipUpgradeJson> api_mst_shipupgrade;
        public BgmInfoJson[] api_mst_bgm;
        public ShipEquipLimitationJson[] api_mst_equip_ship;
    }
    internal class EquipmentInExtraSlotJson
    {
        public EquipmentInfoId api_slotitem_id;
        public List<ShipInfoId> api_ship_ids;
    }
    internal class ShipUpgradeJson
    {
        public int api_id;
        public ShipInfoId? api_current_ship_id;
        public ShipInfoId api_original_ship_id;
        public int api_drawing_count;
        public int api_catapult_count;
        public int api_report_count;
    }
}
