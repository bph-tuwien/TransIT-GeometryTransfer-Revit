using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TransITGeometryTransferRevit
{
    /// <summary>
    /// This class handles the user interactions like prompts and processing selection results.
    /// </summary>
    public class UserInteractions
    {

        /// <summary>
        /// Prompts the user for opening an IFC file.
        /// </summary>
        /// <returns>Return the absolute path to the IFC file</returns>
        public static string PromptIfcFileOpenDialog()
        {
            FileOpenDialog fileOpenDialog = new FileOpenDialog("IFC file (*.ifc)|*.ifc");
            fileOpenDialog.Title = ("Select IFC file to import");
            fileOpenDialog.Show();
            ModelPath selectedModelPath = fileOpenDialog.GetSelectedModelPath();
            fileOpenDialog.Dispose();

            return ModelPathUtils.ConvertModelPathToUserVisiblePath(selectedModelPath);
        }

        /// <summary>
        /// Prompts the user for saving an IFC file.
        /// </summary>
        /// <returns>Return the absolute path to the IFC file</returns>
        public static string PromptIfcFileSaveDialog()
        {
            FileSaveDialog fileSaveDialog = new FileSaveDialog("IFC file (*.ifc)|*.ifc");
            fileSaveDialog.Title = ("Select destination path to export to");
            fileSaveDialog.Show();
            ModelPath selectedModelPath = fileSaveDialog.GetSelectedModelPath();
            fileSaveDialog.Dispose();

            return ModelPathUtils.ConvertModelPathToUserVisiblePath(selectedModelPath);
        }

    }
}
