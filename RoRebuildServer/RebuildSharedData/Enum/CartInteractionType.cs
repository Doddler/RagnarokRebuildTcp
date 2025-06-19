using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum CartInteractionType
    {
        Unknown = 0,
        InventoryToCart,
        CartToInventory,
        CartToStorage,
        StorageToCart
    }
}
