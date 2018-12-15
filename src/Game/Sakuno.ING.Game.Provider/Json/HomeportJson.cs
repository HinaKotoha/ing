﻿using Sakuno.ING.Game.Models.Knowledge;

namespace Sakuno.ING.Game.Json
{
    internal class HomeportJson
    {
        public MaterialJsonArray api_material;
        public FleetJson[] api_deck_port;
        public AdmiralJson api_basic;
        public RepairingDockJson[] api_ndock;
        public ShipJson[] api_ship;
        public KnownCombinedFleet api_combined_flag;
        internal class EventObject
        {
            public bool api_m_flag2;
        }
        public EventObject api_event_object;
    }
}
