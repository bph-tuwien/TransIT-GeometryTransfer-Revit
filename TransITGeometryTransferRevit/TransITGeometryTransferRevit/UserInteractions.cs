using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransITGeometryTransferRevit
{
    public class UserInteractions
    {

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
