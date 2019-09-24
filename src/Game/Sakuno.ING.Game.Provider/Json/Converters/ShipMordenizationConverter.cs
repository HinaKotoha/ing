﻿using System;
using Sakuno.ING.Game.Models;

namespace Sakuno.ING.Game.Json.Converters
{
    internal class ShipMordenizationConverter : IntArrayConverterBase<ShipMordenizationStatus>
    {
        protected override int RequiredCount => 2;

        protected override ShipMordenizationStatus ConvertValue(ReadOnlySpan<int> array)
            => new ShipMordenizationStatus
            (
                min: array[0],
                max: array[1]
            );
    }
}
