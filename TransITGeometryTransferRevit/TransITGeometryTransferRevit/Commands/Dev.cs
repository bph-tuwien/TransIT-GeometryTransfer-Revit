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

using Xbim.Geometry.Engine.Interop;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.MeasureResource;
using Xbim.Common.Geometry;

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

            var queryTunnel = from element in collectorTunnel where element.Name.StartsWith("TunnelFamily") select element;
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

                    var ifcIndexedPolyCurve = tunnelLineGeometry.ToIfcIndexedPolyCurve(false, model, tunnelFamilyInstanceTotalTransform, XbimMatrix3D.Identity, Constants.FeetToMillimeter);


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
                // ADDING PROFILES AS PROFILE REPRESENTATION
                // ###############


                FilteredElementCollector collectorRevitTunnelSection = new FilteredElementCollector(doc);
                collectorRevitTunnelSection = collectorRevitTunnelSection.OfClass(typeof(FamilyInstance));
                var queryRevitTunnelSection = from element in collectorRevitTunnelSection where element.Name.StartsWith("TunnelSection") select element;


                using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                {
                    var ifcBuildingElementProxies = model.Instances.OfType<IfcBuildingElementProxy>();


                    foreach (var revitTunnelSection in queryRevitTunnelSection)
                    {

                        var revitTunnelSectionFamilyInstance = revitTunnelSection as FamilyInstance;

                        foreach (var ifcBuildingElementProxy in ifcBuildingElementProxies)
                        {

                            // Matching Revit and IFC tunnel sections
                            if (ifcBuildingElementProxy.Name.ToString().EndsWith(revitTunnelSection.Id.ToString()))
                            {

                                FilteredElementCollector collectorRevitTunnelPart = new FilteredElementCollector(doc);
                                collectorRevitTunnelPart = collectorRevitTunnelPart.OfClass(typeof(FamilyInstance));

                                var ifcTunnelSectionProfiles = new List<IfcIndexedPolyCurve>();

                                foreach (FamilyInstance revitTunnelPart in collectorRevitTunnelPart)
                                {

                                    if (revitTunnelPart.SuperComponent != null && revitTunnelSection.Id.ToString() == revitTunnelPart.SuperComponent.Id.ToString())
                                    {


                                        Options gOptions = new Options();
                                        gOptions.ComputeReferences = true;
                                        gOptions.DetailLevel = ViewDetailLevel.Undefined;
                                        gOptions.IncludeNonVisibleObjects = false;

                                        GeometryElement geomElem = revitTunnelPart.get_Geometry(gOptions);
                                        GeometryInstance geomInst = geomElem.First() as GeometryInstance;
                                        GeometryElement gInstGeom = geomInst.GetInstanceGeometry();


                                        CurveArray profileCurveArray = new CurveArray();

                                        foreach (var part in gInstGeom)
                                        {
                                            if (part is Curve curve)
                                            {
                                                profileCurveArray.Append(curve);
                                            }


                                        }

                                        var ifcTransform = ifcBuildingElementProxy.ObjectPlacement.ToMatrix3D();
                                        ifcTransform.Invert();

                                        var profileIfcIndexedPolyCurve = profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, ifcTransform, Constants.FeetToMillimeter);
                                        //var profileIfcIndexedPolyCurve = profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity, 1);
                                        ifcTunnelSectionProfiles.Add(profileIfcIndexedPolyCurve);

                                    }

                                }


                                ifcBuildingElementProxy.Representation.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                {
                                    rep.ContextOfItems = GetModelRepresentationContext(model);
                                    rep.RepresentationIdentifier = "Profile";
                                    rep.RepresentationType = "Curve3D";
                                    //rep.Items.Add(ifcTunnelSectionProfiles[0]);
                                    rep.Items.AddRange(ifcTunnelSectionProfiles);

                                }
                                ));

                                


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
