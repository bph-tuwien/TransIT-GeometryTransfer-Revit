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
using Xbim.Ifc4.ProfileResource;
using Xbim.Common;
using Xbim.Ifc4.PresentationAppearanceResource;

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

        public void ExportDocumentToIfc(Document doc, string ifcExportPathFolder, string ifcExportPathFilename)
        {
            Transaction transaction = new Transaction(doc);
            {
                transaction.Start("create tunnel");

                var ifcOptions = new IFCExportOptions();
                ifcOptions.FileVersion = IFCVersion.IFC4;

                doc.Export(ifcExportPathFolder, ifcExportPathFilename, ifcOptions);

                transaction.Commit();
            }
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
            var ifcPostExportPathFilename = "TunnelExportRevit_post.ifc";
            var ifcExportPath = Path.Combine(ifcExportPathFolder, ifcExportPathFilename);
            var ifcPostExportPath = Path.Combine(ifcExportPathFolder, ifcPostExportPathFilename);
            var ifcExportTempPath = Path.Combine(ifcExportPathFolder, ifcExportPathFilename + "_temp");


            ExportDocumentToIfc(doc, ifcExportPathFolder, ifcExportPathFilename);



            // ##################################
            // BUMPING IFC VERSION FROM 4 TO 4X1
            // ################################

            // TODO: Do a schema upgrade instead of string replace

            using (var input = File.OpenText(ifcExportPath))
            using (var output = new StreamWriter(ifcExportTempPath))
            {
                string line;
                while (null != (line = input.ReadLine()))
                {
                    if (line.Equals("FILE_SCHEMA(('IFC4'));"))
                    {
                        output.WriteLine("FILE_SCHEMA(('IFC4X1'));");
                    }
                    else
                    {
                        output.WriteLine(line);
                    }
                }
            }

            File.Replace(ifcExportTempPath, ifcExportPath, null);


            // ##############
            // LOADING BACK IFC MODEL
            // ###############


            using (var model = IfcStore.Open(ifcExportPath))
            {


                // ##############
                // ADDING TUNNEL LINE AS AXIS REPRESENTATION TO THE WHOLE TUNNEL
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

                // #############################
                // DELETING OLD TUNNEL SECTIONS
                // #############################

                {
                    var ifcBuildingElementProxies = model.Instances.OfType<IfcBuildingElementProxy>();

                    List<IPersistEntity> entitiesToDelete = new List<IPersistEntity>();

                    foreach (var tunnelSection in ifcBuildingElementProxies)
                    {

                        entitiesToDelete.Add(
                            (((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement) as IfcAxis2Placement3D)
                            .Location);
                        entitiesToDelete.Add(
                            (((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement) as IfcAxis2Placement3D)
                            .Axis);
                        entitiesToDelete.Add(
                            (((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement) as IfcAxis2Placement3D)
                            .RefDirection);
                        entitiesToDelete.Add((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement);
                        entitiesToDelete.Add(tunnelSection.ObjectPlacement);



                        entitiesToDelete.AddRange(((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                            .MappingSource.MappedRepresentation.Items[0] as IfcPolygonalFaceSet).Faces);
                        entitiesToDelete.Add(((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                            .MappingSource.MappedRepresentation.Items[0] as IfcPolygonalFaceSet).Coordinates);
                        entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                            .MappingSource.MappedRepresentation.Items[0]);
                        entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                            .MappingSource.MappedRepresentation);
                        entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                            .MappingSource.MappingOrigin);
                        entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                            .MappingSource);
                        entitiesToDelete.Add(tunnelSection.Representation.Representations[0].Items[0]);
                        entitiesToDelete.Add(tunnelSection.Representation.Representations[0]);
                        entitiesToDelete.Add(tunnelSection.Representation);
                        entitiesToDelete.Add(tunnelSection);



                        var propertySets = tunnelSection.PropertySets;

                        foreach (var propertySet in propertySets)
                        {

                            

                            ;
                            entitiesToDelete.AddRange(propertySet.HasProperties);
                            entitiesToDelete.AddRange(propertySet.DefinesOccurrence);
                            entitiesToDelete.AddRange(propertySet.PropertySetDefinitions);
                            entitiesToDelete.Add(propertySet);

                        }


                        ;
                    }

                    





                    while (entitiesToDelete.Count > 0)
                    {
                        var entity = entitiesToDelete.First();
                        entitiesToDelete.RemoveAt(0);

                        using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                        {
                            try
                            {
                                entity.Model.Delete(entity);
                            }
                            catch (System.Exception e)
                            {
                                ;
                            }
                            ifcTransaction.Commit();
                        }
                    }


                    var ifcBuildingElementProxyTypes = model.Instances.OfType<IfcBuildingElementProxyType>();

                    foreach (var proxyType in ifcBuildingElementProxyTypes)
                    {
                        if (proxyType.RepresentationMaps.Count == 0)
                        {
                            entitiesToDelete.Add(proxyType);
                        }
                    }

                    var ifcIndexedColourMaps = model.Instances.OfType<IfcIndexedColourMap>();

                    foreach (var colourMap in ifcIndexedColourMaps)
                    {
                        if (colourMap.MappedTo == null)
                        {
                            entitiesToDelete.Add(colourMap);
                        }
                    }



                    var ifcPropertySets = model.Instances.OfType<IfcPropertySet>();

                    foreach (var propertySet in ifcPropertySets)
                    {
                        if (propertySet.HasProperties.Count == 0)
                        {
                            entitiesToDelete.Add(propertySet);
                        }
                    }

                    var ifcRelContainedInSpatialStructures = model.Instances.OfType<IfcRelContainedInSpatialStructure>();

                    foreach (var rel in ifcRelContainedInSpatialStructures)
                    {
                        if (rel.RelatedElements.Count == 0)
                        {
                            entitiesToDelete.Add(rel);
                        }
                    }

                    var ifcRelDefinesByType = model.Instances.OfType<IfcRelDefinesByType>();

                    foreach (var rel in ifcRelDefinesByType)
                    {
                        if (rel.RelatedObjects.Count == 0)
                        {
                            entitiesToDelete.Add(rel);
                        }
                    }

                    ;




                    while (entitiesToDelete.Count > 0)
                    {
                        var entity = entitiesToDelete.First();
                        entitiesToDelete.RemoveAt(0);

                        using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                        {
                            try
                            {
                                entity.Model.Delete(entity);
                            }
                            catch (System.Exception e)
                            {
                                ;
                            }
                            ifcTransaction.Commit();
                        }
                    }

                }










                // #############################
                // RECREATING TUNNEL SECTIONS
                // #############################
                using (var ifcTransaction = model.BeginTransaction("TransITGeometryTransferRevit.Commands.Dev.Execute"))
                {

                    FilteredElementCollector collectorRevitTunnelSection = new FilteredElementCollector(docfamily);
                    collectorRevitTunnelSection = collectorRevitTunnelSection.OfClass(typeof(FamilyInstance));
                    var queryRevitTunnelSection = from element in collectorRevitTunnelSection where element.Name.StartsWith("TunnelSection") select element;

                    var ifcBuildingElementProxies = new List<IfcBuildingElementProxy>();


                    foreach (var revitTunnelSection in queryRevitTunnelSection)
                    {

                        var revitTunnelSectionFamilyInstance = revitTunnelSection as FamilyInstance;







                        FilteredElementCollector collectorRevitSuperTunnelSection = new FilteredElementCollector(doc);
                        collectorRevitSuperTunnelSection = collectorRevitSuperTunnelSection.OfClass(typeof(FamilyInstance));
                        var queryRevitSuperTunnelSection = from element in collectorRevitSuperTunnelSection where element.Name.StartsWith("TunnelSection") select element;






                        var placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(revitTunnelSectionFamilyInstance);

                        var buildingElementProxy = model.Instances.New<IfcBuildingElementProxy>(b =>
                        {

                            Parameter sectionIDParam = revitTunnelSectionFamilyInstance.LookupParameter("SectionID");

                            //b.GlobalId = "TESTtestTESTtestTESTte";
                            //b.OwnerHistory = 
                            b.Name = revitTunnelSectionFamilyInstance.Symbol.FamilyName + ":" +
                                     revitTunnelSectionFamilyInstance.Symbol.Name + ":" +
                                     sectionIDParam.AsInteger();
                            //b.Description = 
                            b.ObjectType = revitTunnelSectionFamilyInstance.Symbol.FamilyName + ":" +
                                           revitTunnelSectionFamilyInstance.Symbol.Name;
                            b.ObjectPlacement = model.Instances.New<IfcLocalPlacement>(p =>
                            {

                                var objects = model.Instances.OfType<IfcBuildingStorey>();
                                var tunnelStorey = objects.First();

                                p.PlacementRelTo = tunnelStorey.ObjectPlacement;
                                p.RelativePlacement = model.Instances.New<IfcAxis2Placement3D>(t =>
                                {

                                    t.Location = model.Instances.New<IfcCartesianPoint>(cp =>
                                    {
                                        //var loc = revitTunnelSectionFamilyInstance.Location as LocationPoint;

                                        //cp.X = loc.Point.X;
                                        //cp.Y = loc.Point.Y;
                                        //cp.Z = loc.Point.Z;

                                        var originPoint = docfamily.GetElement(placePointIds[0]) as ReferencePoint;

                                        cp.X = originPoint.Position.X * Constants.FeetToMillimeter;
                                        cp.Y = originPoint.Position.Y * Constants.FeetToMillimeter;
                                        cp.Z = originPoint.Position.Z * Constants.FeetToMillimeter;

                                        //cp.X = 0;
                                        //cp.Y = 0;
                                        //cp.Z = 0;

                                        //cp.X = 975585;
                                        //cp.Y = 56777;
                                        //cp.Z = 353345;

                                    });

                                    t.Axis = model.Instances.New<IfcDirection>(d =>
                                    {
                                        var originPoint = docfamily.GetElement(placePointIds[0]) as ReferencePoint;
                                        var upPoint = docfamily.GetElement(placePointIds[2]) as ReferencePoint;

                                        var upDir = upPoint.Position - originPoint.Position;


                                        //d.X = upDir.X;
                                        //d.Y = upDir.Y;
                                        //d.Z = upDir.Z;

                                        d.X = 0;
                                        d.Y = 0;
                                        d.Z = 1;

                                    });

                                    t.RefDirection = model.Instances.New<IfcDirection>(d =>
                                    {
                                        var originPoint = docfamily.GetElement(placePointIds[0]) as ReferencePoint;
                                        var endPoint = docfamily.GetElement(placePointIds[1]) as ReferencePoint;

                                        var forwardDir = endPoint.Position - originPoint.Position;


                                        //d.X = forwardDir.X;
                                        //d.Y = forwardDir.Y;
                                        //d.Z = forwardDir.Z;

                                        d.X = 1;
                                        d.Y = 0;
                                        d.Z = 0;

                                    });


                                });
                            });







                            var p0 = docfamily.GetElement(placePointIds[0]) as ReferencePoint;
                            var p1 = docfamily.GetElement(placePointIds[1]) as ReferencePoint;

                            var xyz0 = new XYZ(0, 0, 0);
                            //var xyz0 = p0.Position;
                            var xyz1 = p1.Position - p0.Position;
                            //var xyz1 = new XYZ((p1.Position - p0.Position).GetLength(),0,0);
                            //var xyz1 = p1.Position;


                            var revitTunnelSectionLine = Line.CreateBound(xyz0, xyz1);
                            var revitTunnelSectionLineCurveArray = new CurveArray();
                            revitTunnelSectionLineCurveArray.Append(revitTunnelSectionLine);
                            var ifcTunnelSectionLine = revitTunnelSectionLineCurveArray.ToIfcIndexedPolyCurve(false, model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);


                            var revitTunnelParts = new List<FamilyInstance>();


                            FilteredElementCollector collectorRevitTunnelPart = new FilteredElementCollector(docfamily);
                            collectorRevitTunnelPart = collectorRevitTunnelPart.OfClass(typeof(FamilyInstance));

                            foreach (FamilyInstance revitTunnelPart in collectorRevitTunnelPart)
                            {

                                if (revitTunnelPart.SuperComponent != null && revitTunnelSection.Id.ToString() == revitTunnelPart.SuperComponent.Id.ToString())
                                {
                                    revitTunnelParts.Add(revitTunnelPart);
                                }
                            }




                            b.Representation = model.Instances.New<IfcProductDefinitionShape>(def =>
                            {

                                var sweep = model.Instances.New<IfcSectionedSolidHorizontal>(s =>
                                {
                                    s.Directrix = ifcTunnelSectionLine;
                                    // TODO: temp cross sections


                                    var revitTunnelProfileFamily = revitTunnelParts[0].Symbol.Family;
                                    var revitTunnelProfileDocument = docfamily.EditFamily(revitTunnelProfileFamily);

                                    FilteredElementCollector collectorRevitTunnelProfile = new FilteredElementCollector(revitTunnelProfileDocument);
                                    collectorRevitTunnelProfile = collectorRevitTunnelProfile.OfClass(typeof(CurveElement));

                                    CurveArray profileCurveArray = new CurveArray();

                                    foreach (ModelArc modelArc in collectorRevitTunnelProfile)
                                    {
                                        var geometryArc = modelArc.GeometryCurve;
                                        if (geometryArc is Curve curve)
                                        {
                                            profileCurveArray.Append(curve);
                                        }

                                    }

                                    var profileIfcIndexedPolyCurve1 = profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);


                                    var transform1 = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(new XYZ(0, 1, 0), new XYZ(0, 0, 0)));
                                    var transform2 = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(new XYZ(1, 0, 0), new XYZ(0, 0, 0)));
                                    var transform3 = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(new XYZ(0, 0, 1), new XYZ(0, 0, 0)));
                                    var transform = transform2;
                                    //var transform = Transform.Identity;

                                    var profileIfcIndexedPolyCurve2 = profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);

                                    ;

                                    s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                    {
                                        p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                        p.ProfileName = "TestCustomProfile1";
                                        p.OuterCurve = profileIfcIndexedPolyCurve1;
                                    }
                                    ));
                                    //s.CrossSections.Add(s.CrossSections[0]);
                                    s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                {
                                    p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                    p.ProfileName = "TestCustomProfile2";
                                    p.OuterCurve = profileIfcIndexedPolyCurve2;

                                }
                                    ));

                                    s.CrossSectionPositions.Add(model.Instances.New<IfcDistanceExpression>(d =>
                                    {
                                        d.DistanceAlong = 0;
                                        d.OffsetLateral = 0;
                                        d.OffsetVertical = 0;
                                        d.OffsetLongitudinal = 0;
                                        d.AlongHorizontal = false;
                                    }
                                    ));

                                    s.CrossSectionPositions.Add(model.Instances.New<IfcDistanceExpression>(d =>
                                    {
                                        // Calculating line lenght
                                        IfcCartesianPointList3D pointList = ifcTunnelSectionLine.Points as IfcCartesianPointList3D;
                                        var coordList = pointList.CoordList;
                                        var x0 = new XYZ(coordList[0][0], coordList[0][1], coordList[0][2]);
                                        var x1 = new XYZ(coordList[1][0], coordList[1][1], coordList[1][2]);
                                        var length = (x1 - x0).GetLength() - 0.00000000001;


                                        //d.DistanceAlong = ifcTunnelSectionLine.ToCurve().Length - 1;
                                        d.DistanceAlong = length;
                                        //d.DistanceAlong = ifcTunnelSectionLine.;
                                        //d.DistanceAlong = 50000;
                                        d.OffsetLateral = 0;
                                        d.OffsetVertical = 0;
                                        d.OffsetLongitudinal = 0;
                                        d.AlongHorizontal = false;
                                    }
                                    ));

                                    s.FixedAxisVertical = false;
                                });


                                def.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                {

                                    rep.ContextOfItems = GetModelRepresentationContext(model);
                                    rep.RepresentationIdentifier = "Body";
                                    rep.RepresentationType = "AdvancedSweptSolid";
                                    rep.Items.Add(sweep);

                                }));

                            });


                            //b.Tag =
                            //b.PredefinedType = 




                        });


                    }

                    ifcTransaction.Commit();

                }




                // ##############
                // ADDING PROFILES AS PROFILE REPRESENTATION
                // ###############

                {

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
                                Parameter sectionIDParam = revitTunnelSectionFamilyInstance.LookupParameter("SectionID");

                                if (ifcBuildingElementProxy.Name.ToString().EndsWith(sectionIDParam.AsInteger().ToString()))
                                {

                                    FilteredElementCollector collectorRevitTunnelPart = new FilteredElementCollector(doc);
                                    collectorRevitTunnelPart = collectorRevitTunnelPart.OfClass(typeof(FamilyInstance));

                                    var ifcTunnelSectionProfiles = new List<IfcIndexedPolyCurve>();
                                    var revitTunnelParts = new List<FamilyInstance>();

                                    var ifcTransform = ifcBuildingElementProxy.ObjectPlacement.ToMatrix3D();
                                    ifcTransform.Invert();

                                    foreach (FamilyInstance revitTunnelPart in collectorRevitTunnelPart)
                                    {

                                        if (revitTunnelPart.SuperComponent != null && revitTunnelSection.Id.ToString() == revitTunnelPart.SuperComponent.Id.ToString())
                                        {
                                            revitTunnelParts.Add(revitTunnelPart);


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





                                    // ##########################
                                    // TUNNEL SECTION TUNNEL LINE
                                    // ##########################


                                    ;

                                    var p0 = revitTunnelParts[0].Location as LocationPoint;
                                    var p1 = revitTunnelParts[1].Location as LocationPoint;

                                    var revitTunnelSectionLine = Line.CreateBound(p0.Point, p1.Point);
                                    var revitTunnelSectionLineCurveArray = new CurveArray();
                                    revitTunnelSectionLineCurveArray.Append(revitTunnelSectionLine);
                                    var ifcTunnelSectionLine = revitTunnelSectionLineCurveArray.ToIfcIndexedPolyCurve(false, model, Transform.Identity, ifcTransform, Constants.FeetToMillimeter);

                                    ifcBuildingElementProxy.Representation.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                    {
                                        rep.ContextOfItems = GetModelRepresentationContext(model);
                                        rep.RepresentationIdentifier = "Axis";
                                        rep.RepresentationType = "Curve3D";
                                        rep.Items.Add(ifcTunnelSectionLine);
                                    }
                                    ));


                                    // ############################
                                    // TUNNEL SECTION BODY REPRESENTATION
                                    // ############################



                                    var sweep = model.Instances.New<IfcSectionedSolidHorizontal>(s =>
                                    {
                                        s.Directrix = ifcTunnelSectionLine;
                                        // TODO: temp cross sections


                                        var revitTunnelProfileFamily = revitTunnelParts[0].Symbol.Family;
                                        var revitTunnelProfileDocument = doc.EditFamily(revitTunnelProfileFamily);

                                        FilteredElementCollector collectorRevitTunnelProfile = new FilteredElementCollector(revitTunnelProfileDocument);
                                        collectorRevitTunnelProfile = collectorRevitTunnelProfile.OfClass(typeof(CurveElement));

                                        CurveArray profileCurveArray = new CurveArray();

                                        foreach (ModelArc modelArc in collectorRevitTunnelProfile)
                                        {
                                            var geometryArc = modelArc.GeometryCurve;
                                            if (geometryArc is Curve curve)
                                            {
                                                profileCurveArray.Append(curve);
                                            }

                                        }

                                        var profileIfcIndexedPolyCurve1 = profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);


                                        var transform1 = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(new XYZ(0, 1, 0), new XYZ(0, 0, 0)));
                                        var transform2 = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(new XYZ(1, 0, 0), new XYZ(0, 0, 0)));
                                        var transform3 = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(new XYZ(0, 0, 1), new XYZ(0, 0, 0)));
                                        var transform = transform2;
                                        //var transform = Transform.Identity;

                                        var profileIfcIndexedPolyCurve2 = profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);

                                        ;

                                        s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                        {
                                            p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                            p.ProfileName = "TestCustomProfile1";
                                            p.OuterCurve = profileIfcIndexedPolyCurve1;
                                        }
                                        ));
                                        //s.CrossSections.Add(s.CrossSections[0]);
                                        s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                            {
                                                p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                                p.ProfileName = "TestCustomProfile2";
                                                p.OuterCurve = profileIfcIndexedPolyCurve2;

                                            }
                                            ));

                                        s.CrossSectionPositions.Add(model.Instances.New<IfcDistanceExpression>(d =>
                                        {
                                            d.DistanceAlong = 0;
                                            d.OffsetLateral = 0;
                                            d.OffsetVertical = 0;
                                            d.OffsetLongitudinal = 0;
                                            d.AlongHorizontal = false;
                                        }
                                        ));

                                        s.CrossSectionPositions.Add(model.Instances.New<IfcDistanceExpression>(d =>
                                        {
                                            // Calculating line lenght
                                            IfcCartesianPointList3D pointList = ifcTunnelSectionLine.Points as IfcCartesianPointList3D;
                                            var coordList = pointList.CoordList;
                                            var x0 = new XYZ(coordList[0][0], coordList[0][1], coordList[0][2]);
                                            var x1 = new XYZ(coordList[1][0], coordList[1][1], coordList[1][2]);
                                            var length = (x1 - x0).GetLength() - 0.00000000001;


                                            //d.DistanceAlong = ifcTunnelSectionLine.ToCurve().Length - 1;
                                            d.DistanceAlong = length;
                                            //d.DistanceAlong = ifcTunnelSectionLine.;
                                            //d.DistanceAlong = 50000;
                                            d.OffsetLateral = 0;
                                            d.OffsetVertical = 0;
                                            d.OffsetLongitudinal = 0;
                                            d.AlongHorizontal = false;
                                        }
                                        ));

                                        s.FixedAxisVertical = false;
                                    }
                                    );


                                    // TODO: Fix this
                                    ifcBuildingElementProxy.Representation.Representations[0].RepresentationType = "AdvancedSweptSolid";
                                    ifcBuildingElementProxy.Representation.Representations[0].Items[0] = sweep;

                                    // TODO: Remove original MappedRepresentation

                                    ;

                                }


                            }

                        }







                        ifcTransaction.Commit();
                    }

                }




                model.SaveAs(ifcPostExportPath);

            }











            return Result.Succeeded;

        }

    }
}
