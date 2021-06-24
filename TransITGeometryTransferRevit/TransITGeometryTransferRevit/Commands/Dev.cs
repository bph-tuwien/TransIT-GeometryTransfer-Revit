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
using Xbim.Ifc4.SharedBldgElements;

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

        public static IfcGeometricRepresentationContext GetModelRepresentationContext(IfcStore model)
        {
            foreach (IfcGeometricRepresentationContext rep in model.Instances.OfType<IfcGeometricRepresentationContext>())
            {
                if (rep is IfcGeometricRepresentationSubContext)
                {
                    continue;
                }

                if (rep.ContextType != null && rep.ContextType.Value == "Model")
                {
                    return rep;
                }
            }
            return null;
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

            //var docfamily = FamilyUtils.GetFamilyDocumentByName(doc, "TunnelFamily");

            FilteredElementCollector collectorTunnel = new FilteredElementCollector(doc);
            collectorTunnel = collectorTunnel.OfClass(typeof(FamilyInstance));

            var queryTunnel = from element in collectorTunnel where element.Name.Equals("TunnelFamily") select element;
            List<Element> resultTunnel = queryTunnel.ToList<Element>();


            FamilyInstance tunnelFamilyInstance = queryTunnel.First() as FamilyInstance;
            var fam = tunnelFamilyInstance.Symbol.Family;
            var docfamily = doc.EditFamily(fam);



            var tunnelFamilyInstanceTotalTransform = tunnelFamilyInstance.GetTotalTransform();




            ;




            FilteredElementCollector collector = new FilteredElementCollector(docfamily);
            collector = collector.OfClass(typeof(DirectShape));

            var query = from element in collector where element.Name.StartsWith("TunnelLine") select element;
            List<Element> result = query.ToList<Element>();

            // TODO: nul check
            DirectShape tunnelLineShape = result[0] as DirectShape;
            GeometryElement tunnelLineGeometry = tunnelLineShape.get_Geometry(new Options());

            //tunnelLineGeometry = tunnelLineGeometry.GetTransformed

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

                // ##############
                // ADDING TUNNEL LINE AS AXIS REPRESENTATION
                // ###############


                using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                {
                    var objects = model.Instances.OfType<IfcBuildingStorey>();
                    var tunnelStorey = objects.First();

                    var ifcIndexedPolyCurve = tunnelLineGeometry.ToIfcIndexedPolyCurve(false, model, tunnelFamilyInstanceTotalTransform, Constants.FeetToMillimeter);


                    var objects2 = model.Instances.OfType<IfcBuilding>();
                    var tunnelBuilding = objects2.First();

                    tunnelBuilding.Representation = model.Instances.New<IfcProductDefinitionShape>(def =>
                    {
                        def.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                        {
                            rep.ContextOfItems = GetModelRepresentationContext(model);
                            rep.RepresentationIdentifier = "Axis";
                            rep.RepresentationType = "Curve3D";
                            rep.Items.Add(ifcIndexedPolyCurve);

                        }
                        ));
                    });




                    ;

                    ifcTransaction.Commit();
                }


                // ##############
                // REPLACING MAPPED BREP TUNNEL SECTION GEOMETRIES
                // ###############


                FilteredElementCollector collectorTunnelSection = new FilteredElementCollector(doc);
                collectorTunnelSection = collectorTunnelSection.OfClass(typeof(FamilyInstance));

                var queryTunnelSection = from element in collectorTunnelSection where element.Name.StartsWith("TunnelSection") select element;
                List<Element> resultTunnelSection = queryTunnelSection.ToList<Element>();


                

                ;


                using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                {
                    var objects = model.Instances.OfType<IfcBuildingElementProxy>();


                    foreach (var tunnelSection in queryTunnelSection)
                    { 

                        foreach (var buildingElementProxy in objects)
                        {

                            if (buildingElementProxy.Name.ToString().EndsWith(tunnelSection.Id.ToString()))
                            {
                                
                                

                                ;

                                FilteredElementCollector collectorTunnelSection2 = new FilteredElementCollector(doc);
                                collectorTunnelSection2 = collectorTunnelSection2.OfClass(typeof(FamilyInstance));

                                foreach (FamilyInstance tunnelPart in collectorTunnelSection2)
                                {

                                    if (tunnelPart.SuperComponent != null && tunnelSection.Id.ToString() == tunnelPart.SuperComponent.Id.ToString())
                                    {

                                        ;

                                    }


                                }

                            }


                        }

                    }







                    ifcTransaction.Commit();
                }






                var ifcPostExportPathFilename = "TunnelExportRevit_post.ifc";
                model.SaveAs(Path.Combine(ifcExportPathFolder, ifcPostExportPathFilename));

            }



            







                return Result.Succeeded;

        }

    }
}
