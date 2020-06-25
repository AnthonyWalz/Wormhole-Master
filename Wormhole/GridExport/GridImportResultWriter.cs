using Torch.Commands;

namespace Wormhole.GridExport
{

    public class GridImportResultWriter
    {

        public static void WriteResult(CommandContext Context, GridImportResult result)
        {

            switch (result)
            {

                case GridImportResult.FILE_NOT_FOUND:
                    Context.Respond("The requested File was not found!");
                    break;

                case GridImportResult.NO_GRIDS_IN_FILE:
                    Context.Respond("There arent any Grids in your file to import!");
                    break;

                case GridImportResult.UNKNOWN_ERROR:
                    Context.Respond("An unknown error occured while importing the file. Check your logs for more information!");
                    break;

                case GridImportResult.NO_GRIDS_IN_BLUEPRINT:
                    Context.Respond("No grids in blueprint!");
                    break;

                case GridImportResult.NO_FREE_SPACE_AVAILABLE:
                    Context.Respond("No free space available!");
                    break;

                case GridImportResult.NOT_COMPATIBLE:
                    Context.Respond("The File to be imported does not seem to be compatible with the server!");
                    break;

                case GridImportResult.POTENTIAL_BLOCKED_SPACE:
                    Context.Respond("There are potentially other grids in the way. If you are certain is free you can set 'force' to true!");
                    break;
            }
        }
    }
}
