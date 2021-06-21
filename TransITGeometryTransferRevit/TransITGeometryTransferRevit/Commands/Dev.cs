using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;

using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.RepresentationResource;

using TransITGeometryTransferRevit.Ifc.GeometryResource;


namespace TransITGeometryTransferRevit.Commands
{

    /// <summary>
    /// The class containing the callable Revit commands.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Dev : IExternalCommand
    {

        /// <summary>
        /// Dev command class
        /// </summary>
        /// <param name="commandData"></param>
        /// <param name="message"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;


            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfClass(typeof(DirectShape));

            var query = from element in collector where element.Name.StartsWith("TunnelLine") select element;
            List<Element> result = query.ToList<Element>();

            // TODO: nul check
            DirectShape tunnelLineShape = result[0] as DirectShape;
            var asd = tunnelLineShape.get_Geometry(new Autodesk.Revit.DB.Options());

            return Result.Succeeded;

        }

    }
}
