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
using TransITGeometryTransferRevit.Revit;
using TransITGeometryTransferRevit.Ifc;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometricConstraintResource;
using TransITGeometryTransferRevit.Ifc.RepresentationResource;

namespace TransITGeometryTransferRevit.Commands
{

    /// <summary>
    /// The class containing the callable Revit commands.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Dev : IExternalCommand
    {

        public static IfcGeometricRepresentationContext GetAnnotationRepresentationContext(IfcStore model)
        {
            var objects = model.Instances.OfType<IfcGeometricRepresentationSubContext>();

            foreach (IfcGeometricRepresentationSubContext rep in objects)
            {
                if (rep.ContextIdentifier != null && rep.ContextIdentifier.Value == "Annotation")
                {
                    return rep;
                }
            }
            
            var annotationRep = IfcGeometricRepresentationContextExtension.MakeSimple(model, "Annotation");

            return annotationRep;
        }


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


            // ###############
            // GETTING TUNNEL LINE STUFF
            // #############

            var docfamily = FamilyUtils.GetFamilyDocumentByName(doc, "TunnelFamily");


            FilteredElementCollector collector = new FilteredElementCollector(docfamily);
            collector = collector.OfClass(typeof(DirectShape));

            var query = from element in collector where element.Name.StartsWith("TunnelLine") select element;
            List<Element> result = query.ToList<Element>();

            // TODO: nul check
            DirectShape tunnelLineShape = result[0] as DirectShape;
            GeometryElement tunnelLineGeometry = tunnelLineShape.get_Geometry(new Options());


            ;




            //FilteredElementCollector collector = new FilteredElementCollector(doc);
            //collector = collector.OfClass(typeof(DirectShape));

            //var query = from element in collector where element.Name.StartsWith("TunnelLine") select element;
            //List<Element> result = query.ToList<Element>();

            //// TODO: nul check
            //DirectShape tunnelLineShape = result[0] as DirectShape;
            //GeometryElement tunnelLineGeometry = tunnelLineShape.get_Geometry(new Options());



            // ##################
            // INITIAL EXPORT OF TUNNEL TO IFC
            // ###############

            var ifcExportPathFolder = "Y:/RevitTunnel/RevitExportTest";
            var ifcExportPathFilename = "TunnelExportRevit.ifc";


            Transaction transaction = new Transaction(doc);
            {
                transaction.Start("create tunnel");

                var ifcOptions = new IFCExportOptions();
                ifcOptions.FileVersion = IFCVersion.IFC4;

                doc.Export(ifcExportPathFolder, ifcExportPathFilename, ifcOptions);

                transaction.Commit();
            }




            // ##############
            // LOADING BACK IFC MODEL
            // ###############


            using (var model = IfcStore.Open(Path.Combine(ifcExportPathFolder, ifcExportPathFilename)))
            {

                using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                {
                    var objects = model.Instances.OfType<IfcBuildingStorey>();
                    var tunnelStorey = objects.First();

                    var ifcIndexedPolyCurve = tunnelLineGeometry.ToIfcIndexedPolyCurve(false, model);



                    var curveSet = model.Instances.New<IfcGeometricCurveSet>(cs =>
                    {
                        cs.Elements.Add(ifcIndexedPolyCurve);
                    });






                    var shape = model.Instances.New<IfcShapeRepresentation>();
                    var rep = GetAnnotationRepresentationContext(model);
                    shape.ContextOfItems = rep;
                    shape.RepresentationType = "Annotation2D";
                    shape.RepresentationIdentifier = "Annotation";
                    shape.Items.Add(curveSet);

                    List<IfcShapeRepresentation> representations = new List<IfcShapeRepresentation>();
                    representations.Add(shape);




                    var annotation = model.Instances.New<IfcAnnotation>();
                    annotation.Name = "TunnelLine";
                    annotation.Description = "";

                    var prodDef = model.Instances.New<IfcProductDefinitionShape>();
                    prodDef.Representations.AddRange(representations);
                    annotation.Representation = prodDef;

                    var localPlacement = model.Instances.New<IfcLocalPlacement>();

                    var relativePlacement = model.Instances.New<IfcAxis2Placement3D>();
                    var origin =  model.Instances.New<IfcCartesianPoint>(p =>
                    {
                        p.X = 0;
                        p.Y = 0;
                        p.Z = 0;
                    });


                    relativePlacement.Location = origin;
                    relativePlacement.RefDirection = model.Instances.New<IfcDirection>(dir =>
                    {
                        dir.X = 1;
                        dir.Y = 0;
                        dir.Z = 0;
                    });
                    relativePlacement.Axis = model.Instances.New<IfcDirection>(dir =>
                    {
                        dir.X = 0;
                        dir.Y = 0;
                        dir.Z = 1;
                    });

                    localPlacement.RelativePlacement = relativePlacement;

                    //Adding relPlacementto IfcBuilding
                    var ifc_building = model.Instances.OfType<IfcBuilding>().FirstOrDefault();
                    if (ifc_building != null)
                    {
                        localPlacement.PlacementRelTo = ifc_building.ObjectPlacement;
                    }
                    annotation.ObjectPlacement = localPlacement;






                    ;

                    ifcTransaction.Commit();
                }


                var ifcPostExportPathFilename = "TunnelExportRevit_post.ifc";
                model.SaveAs(Path.Combine(ifcExportPathFolder, ifcPostExportPathFilename));

            }



            







                return Result.Succeeded;

        }

    }
}
