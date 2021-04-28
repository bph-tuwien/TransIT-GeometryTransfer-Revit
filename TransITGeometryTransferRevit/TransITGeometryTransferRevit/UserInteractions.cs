using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransITGeometryTransferRevit
{
    /// <summary>
    /// This class handles the user interactions like prompts and processing selection results.
    /// </summary>
    public class UserInteractions
    {

        /// <summary>
        /// Prompts the user for an IFC file.
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

    }
}
