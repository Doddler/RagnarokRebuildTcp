namespace RebuildSharedData.Enum
{
    public enum SkillValidationResult
    {
        Failure,
        Success,
        InvalidTarget,
        OverlappingAreaOfEffect,
        NoLineOfSight,
        IncorrectWeapon,
        IncorrectAmmunition,
        InsufficientSp,
        CannotCreateMore,
        InsufficientItemCount,
        InsufficientZeny,
        UnusableWhileHidden,
        CannotTargetBossMonster,
        ItemAlreadyStolen,
        MemoLocationInvalid,
        MemoLocationUnwalkable,
        TooFarAway,
        TooClose,
        MustBeStandingInWater,
        MissingRequiredItem,
        SkillNotKnown,
        CannotTeleportHere,
        TeleportBlocked,
        CartRequired,
        TrapTooClose,
        TargetImmuneToEffect,
        TargetAreaOccupied,
        TargetStateIgnoresEffect,

        //vending gets its own skill errors here as the client can both notify the player of failure and re-open the shop without additional messages
        VendFailedTooCloseToNpc,
        VendFailedGenericError, 
        VendFailedNameNotValid,
        VendFailedItemsNotPreset,
        VendFailedTooManyItems,
        VendFailedInvalidPrice,
    }
}
