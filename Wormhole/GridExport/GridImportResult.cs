namespace Wormhole.GridExport
{

    public enum GridImportResult
    {
        OK,
        NO_GRIDS_IN_FILE,
        UNKNOWN_ERROR,
        NO_GRIDS_IN_BLUEPRINT,
        NO_FREE_SPACE_AVAILABLE,
        NOT_COMPATIBLE,
        POTENTIAL_BLOCKED_SPACE,
        FILE_NOT_FOUND,
    }
}
