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

        /// <summary>
        /// Exporting the given Revit document to IFC version 4 to the given folder and file name.
        /// </summary>
        /// <param name="doc">The Revit document to export</param>
        /// <param name="ifcExportPathFolder">Path to the output folder</param>
        /// <param name="ifcExportPathFilename">File name of the exported IFC model</param>
        public void ExportDocumentToIfc(Document doc, string ifcExportPathFolder, string ifcExportPathFilename)
        {
            Transaction transaction = new Transaction(doc);
            {
                transaction.Start("Exporting document to IFC");

                var ifcOptions = new IFCExportOptions
                {
                    FileVersion = IFCVersion.IFC4
                };

                doc.Export(ifcExportPathFolder, ifcExportPathFilename, ifcOptions);

                transaction.Commit();
            }
        }

        /// <summary>
        /// Bumping the given IFC4 file's version to IFC4X1 to get access to new IFC entities. The given IFC file will
        /// be replaced with the new one.
        /// </summary>
        /// <param name="ifcExportPath">The original IFC4 file</param>
        /// <param name="ifcExportTempPath">A filepath to use as temp for the file replace</param>
        public void BumpIFCVersionTo4X1(string ifcExportPath, string ifcExportTempPath)
        {
            // TODO: Find a better way of schema upgrade instead of string replace

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
        }

        /// <summary>
        /// Generic function to find elements by type and name.
        /// </summary>
        /// <typeparam name="T">The type on the elements to find</typeparam>
        /// <param name="doc">The Revit document to search in</param>
        /// <param name="startsWith">String to filter elements by String.StartsWith. Leave it empty for no filtering</param>
        /// <returns></returns>
        public T[] GetElements<T>(Document doc, string startsWith) where T : class
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfClass(typeof(T));

            var query = from element in collector where element.Name.StartsWith(startsWith) select element;
            List<Element> elements = query.ToList<Element>();

            List<T> result = new List<T>();

            foreach (var elem in elements)
            {
                result.Add(elem as T);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Adding Tunnel Line as Axis representation to the Tunnel.
        /// </summary>
        /// <param name="doc">The Revit document that contains the Tunnel as a FamilyInstance</param>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        public void AddTunnelLineAsAxisRepresentation(Document doc, string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {

                using (var ifcTransaction = model.BeginTransaction(
                                        "TransITGeometryTransferRevit.Commands.Dev.AddTunnelLineAsAxisRepresentation"))
                {

                    FamilyInstance tunnelFamilyInstance = GetElements<FamilyInstance>(doc, "TunnelFamily").First();
                    var fam = tunnelFamilyInstance.Symbol.Family;
                    var docfamily = doc.EditFamily(fam);
                    var tunnelFamilyInstanceTotalTransform = tunnelFamilyInstance.GetTotalTransform();

                    DirectShape tunnelLineShape = GetElements<DirectShape>(docfamily, "TunnelLine").First();
                    GeometryElement tunnelLineGeometry = tunnelLineShape.get_Geometry(new Options());

                    var ifcIndexedPolyCurve = tunnelLineGeometry.ToIfcIndexedPolyCurve(false, model, tunnelFamilyInstanceTotalTransform,
                                                                                       XbimMatrix3D.Identity, Constants.FeetToMillimeter);

                    var tunnelBuilding = model.Instances.OfType<IfcBuilding>().First();

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


                    ifcTransaction.Commit();
                }
                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Deleting Tunnel Section IfcBuildingElementProxies and their child entities
        /// </summary>
        /// <param name="ifcFilePath">The IFC file's filepath to do the removal in</param>
        public void DeleteTunnelSections(string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                var ifcBuildingElementProxies = model.Instances.OfType<IfcBuildingElementProxy>();
                List<IPersistEntity> entitiesToDelete = new List<IPersistEntity>();


                // Local function to delete entities from the entitiesToDelete List
                void DeleteEntities()
                {
                    while (entitiesToDelete.Count > 0)
                    {
                        var entity = entitiesToDelete.First();
                        entitiesToDelete.RemoveAt(0);

                        using (var ifcTransaction = model.BeginTransaction(
                                                    "TransITGeometryTransferRevit.Commands.Dev.DeleteTunnelSections"))
                        {
                            try
                            {
                                entity.Model.Delete(entity);
                            }
                            catch (System.Exception e)
                            {
                                // Hide exceptions about already deleted entities
                            }
                            ifcTransaction.Commit();
                        }
                    }
                }


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
                        entitiesToDelete.AddRange(propertySet.HasProperties);
                        entitiesToDelete.AddRange(propertySet.DefinesOccurrence);
                        entitiesToDelete.AddRange(propertySet.PropertySetDefinitions);
                        entitiesToDelete.Add(propertySet);
                    }
                }


                DeleteEntities();

                // Second run of collecting entities to delete, but this time based on empty references. (this way it's
                // easier to find entities that were part of the tunnel sections)

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

                DeleteEntities();

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Converts a Revit Profile Family to IfcIndexedPolyCurve.
        /// </summary>
        /// <param name="tunnelProfileDocument">The Revit family document containing the Tunnel profile</param>
        /// <param name="model">The Ifc model to create the IfcIndexedPolyCurve in</param>
        /// <returns>The Profile as IfcIndexedPolyCurve</returns>
        public IfcIndexedPolyCurve RevitProfileDocumentToIfcIndexedPolyCurve(Document tunnelProfileDocument, IfcStore model)
        {

            FilteredElementCollector collectorRevitTunnelProfile = new FilteredElementCollector(tunnelProfileDocument);
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

            return profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity,
                                                           Constants.FeetToMillimeter);
        }

        /// <summary>
        /// Recreating Tunnel Sections based on Revit FamilyInstances.
        /// </summary>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        /// <param name="tunnelFamilyDocument">Tunnel Family Document containing the tunnel</param>
        public void RecreateTunnelSectionsInIFC(string ifcFilePath, Document tunnelFamilyDocument)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                using (var ifcTransaction = model.BeginTransaction(
                                            "TransITGeometryTransferRevit.Commands.Dev.RecreatingTunnelSectionsInIFC"))
                {

                    var revitTunnelSections = GetElements<FamilyInstance>(tunnelFamilyDocument, "TunnelSection");
                    var ifcBuildingElementProxies = new List<IfcBuildingElementProxy>();


                    foreach (var revitTunnelSection in revitTunnelSections)
                    {
                        var placementPointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(revitTunnelSection);

                        var buildingElementProxy = model.Instances.New<IfcBuildingElementProxy>(b =>
                        {
                            Parameter sectionIDParam = revitTunnelSection.LookupParameter("SectionID");

                            //b.GlobalId = 
                            //b.OwnerHistory = 
                            b.Name = revitTunnelSection.Symbol.FamilyName + ":" +
                                         revitTunnelSection.Symbol.Name + ":" +
                                         sectionIDParam.AsInteger();
                            //b.Description = 
                            b.ObjectType = revitTunnelSection.Symbol.FamilyName + ":" +
                                               revitTunnelSection.Symbol.Name;
                            b.ObjectPlacement = model.Instances.New<IfcLocalPlacement>(p =>
                            {
                                var tunnelStorey = model.Instances.OfType<IfcBuildingStorey>().First();

                                p.PlacementRelTo = tunnelStorey.ObjectPlacement;
                                p.RelativePlacement = model.Instances.New<IfcAxis2Placement3D>(t =>
                                {

                                    t.Location = model.Instances.New<IfcCartesianPoint>(cp =>
                                    {
                                        var originPoint = tunnelFamilyDocument.GetElement(placementPointIds[0]) as ReferencePoint;

                                        cp.X = originPoint.Position.X * Constants.FeetToMillimeter;
                                        cp.Y = originPoint.Position.Y * Constants.FeetToMillimeter;
                                        cp.Z = originPoint.Position.Z * Constants.FeetToMillimeter;
                                    });

                                    t.Axis = model.Instances.New<IfcDirection>(d =>
                                    {
                                        d.X = 0;
                                        d.Y = 0;
                                        d.Z = 1;
                                    });

                                    t.RefDirection = model.Instances.New<IfcDirection>(d =>
                                    {
                                        d.X = 1;
                                        d.Y = 0;
                                        d.Z = 0;
                                    });
                                });
                            });

                            var p0 = tunnelFamilyDocument.GetElement(placementPointIds[0]) as ReferencePoint;
                            var p1 = tunnelFamilyDocument.GetElement(placementPointIds[1]) as ReferencePoint;

                            var xyz0 = new XYZ(0, 0, 0);
                            var xyz1 = p1.Position - p0.Position;

                            var revitTunnelSectionLine = Line.CreateBound(xyz0, xyz1);
                            var revitTunnelSectionLineCurveArray = new CurveArray();
                            revitTunnelSectionLineCurveArray.Append(revitTunnelSectionLine);

                            var ifcTunnelSectionLine = revitTunnelSectionLineCurveArray.ToIfcIndexedPolyCurve(false,
                                        model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);


                            var revitTunnelParts = new List<FamilyInstance>();


                            var revitTunnelProfiles = GetElements<FamilyInstance>(tunnelFamilyDocument, "");


                            foreach (FamilyInstance revitTunnelProfile in revitTunnelProfiles)
                            {

                                if (revitTunnelProfile.SuperComponent != null &&
                                    revitTunnelSection.Id.ToString() == revitTunnelProfile.SuperComponent.Id.ToString())
                                {
                                    revitTunnelParts.Add(revitTunnelProfile);
                                }
                            }


                            b.Representation = model.Instances.New<IfcProductDefinitionShape>(def =>
                            {

                                var sweep = model.Instances.New<IfcSectionedSolidHorizontal>(s =>
                                {
                                    s.Directrix = ifcTunnelSectionLine;

                                    var revitTunnelProfileFamily1 = revitTunnelParts[0].Symbol.Family;
                                    var revitTunnelProfileDocument1 = tunnelFamilyDocument.EditFamily(revitTunnelProfileFamily1);
                                    var profileIfcIndexedPolyCurve1 = RevitProfileDocumentToIfcIndexedPolyCurve(revitTunnelProfileDocument1, model);

                                    var revitTunnelProfileFamily2 = revitTunnelParts[1].Symbol.Family;
                                    var revitTunnelProfileDocument2 = tunnelFamilyDocument.EditFamily(revitTunnelProfileFamily2);
                                    var profileIfcIndexedPolyCurve2 = RevitProfileDocumentToIfcIndexedPolyCurve(revitTunnelProfileDocument2, model);


                                    s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                    {
                                        p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                        p.ProfileName = "Profile1";
                                        p.OuterCurve = profileIfcIndexedPolyCurve1;
                                    }
                                    ));
                                    s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                    {
                                        p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                        p.ProfileName = "Profile2";
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
                                        // Decrement the length of the tunnel by a small amount so the sweep shows up
                                        // Could be a FZK Viewer specific issue and solution
                                        var length = (x1 - x0).GetLength() - 0.00000000001;


                                        d.DistanceAlong = length;
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

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Adds Tunnel Section Profiles as Profile rpresentation to the Tunnel Sections.
        /// </summary>
        /// <param name="doc">The Revit Document containing the Tunnel FamilyInstance</param>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        public void AddTunnelSectionProfilesAsProfileRepresentation(Document doc, string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                var revitTunnelSections = GetElements<FamilyInstance>(doc, "TunnelSection");

                using (var ifcTransaction = model.BeginTransaction(
                            "TransITGeometryTransferRevit.Commands.Dev.AddTunnelSectionProfilesAsProfileRepresentation"))
                {
                    var ifcTunnelSections = model.Instances.OfType<IfcBuildingElementProxy>();

                    foreach (var revitTunnelSection in revitTunnelSections)
                    {

                        var revitTunnelSectionFamilyInstance = revitTunnelSection as FamilyInstance;

                        foreach (var ifcTunnelSection in ifcTunnelSections)
                        {

                            // Matching Revit and IFC tunnel sections
                            Parameter sectionIDParam = revitTunnelSectionFamilyInstance.LookupParameter("SectionID");

                            if (ifcTunnelSection.Name.ToString().EndsWith(sectionIDParam.AsInteger().ToString()))
                            {

                                var allFamilyInstances = GetElements<FamilyInstance>(doc, "");

                                var ifcTunnelSectionProfiles = new List<IfcIndexedPolyCurve>();

                                var ifcTransform = ifcTunnelSection.ObjectPlacement.ToMatrix3D();
                                ifcTransform.Invert();

                                foreach (FamilyInstance instance in allFamilyInstances)
                                {
                                    // Finding the parts of the Tunnel Section
                                    if (instance.SuperComponent != null && 
                                        revitTunnelSection.Id.ToString() == instance.SuperComponent.Id.ToString())
                                    {
                                        var revitTunnelSectionProfile = instance;


                                        Options options = new Options();
                                        options.ComputeReferences = true;
                                        options.DetailLevel = ViewDetailLevel.Undefined;
                                        options.IncludeNonVisibleObjects = false;

                                        GeometryElement geoElement = revitTunnelSectionProfile.get_Geometry(options);
                                        GeometryInstance geoInstance = geoElement.First() as GeometryInstance;
                                        GeometryElement instanceGeometry = geoInstance.GetInstanceGeometry();

                                        CurveArray profileCurveArray = new CurveArray();

                                        foreach (var part in instanceGeometry)
                                        {
                                            if (part is Curve curve)
                                            {
                                                profileCurveArray.Append(curve);
                                            }
                                        }

                                        var profileIfcIndexedPolyCurve = profileCurveArray.ToIfcIndexedPolyCurve(true,
                                                    model, Transform.Identity, ifcTransform, Constants.FeetToMillimeter);

                                        ifcTunnelSectionProfiles.Add(profileIfcIndexedPolyCurve);

                                    }
                                }


                                ifcTunnelSection.Representation.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                {
                                    rep.ContextOfItems = GetModelRepresentationContext(model);
                                    rep.RepresentationIdentifier = "Profile";
                                    rep.RepresentationType = "Curve3D";
                                    rep.Items.AddRange(ifcTunnelSectionProfiles);
                                }
                                ));
                            }
                        }
                    }

                    ifcTransaction.Commit();
                }

                model.SaveAs(ifcFilePath);
            }
        }


        public void AddTunnelSectionLinesAsAxisRepresentation(Document doc, string ifcExportPath)
        {
            using (var model = IfcStore.Open(ifcExportPath))
            {



                // ##############
                // ADDING PROFILES AS PROFILE REPRESENTATION
                // ###############

                {


                    var queryRevitTunnelSection = GetElements<FamilyInstance>(doc, "TunnelSection");



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


                                    var collectorRevitTunnelPart = GetElements<FamilyInstance>(doc, "");


                                    var revitTunnelParts = new List<FamilyInstance>();

                                    var ifcTransform = ifcBuildingElementProxy.ObjectPlacement.ToMatrix3D();
                                    ifcTransform.Invert();

                                    foreach (FamilyInstance revitTunnelPart in collectorRevitTunnelPart)
                                    {
                                        if (revitTunnelPart.SuperComponent != null && revitTunnelSection.Id.ToString() == revitTunnelPart.SuperComponent.Id.ToString())
                                        {
                                            revitTunnelParts.Add(revitTunnelPart);

                                        }
                                    }


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

                                }


                            }

                        }


                        ifcTransaction.Commit();
                    }

                }

                model.SaveAs(ifcExportPath);

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


            FamilyInstance tunnelFamilyInstance = GetElements<FamilyInstance>(doc, "TunnelFamily").First();
            var fam = tunnelFamilyInstance.Symbol.Family;
            var tunnelFamilyDocument = doc.EditFamily(fam);

            var tunnelFamilyInstanceTotalTransform = tunnelFamilyInstance.GetTotalTransform();



            // TODO: Make this a user prompt
            var ifcExportPathFolder = "Y:/RevitTunnel/RevitExportTest";
            var ifcExportPathFilename = "TunnelExportRevit.ifc";
            var ifcPostExportPathFilename = "TunnelExportRevit_post.ifc";
            var ifcExportPath = Path.Combine(ifcExportPathFolder, ifcExportPathFilename);
            var ifcPostExportPath = Path.Combine(ifcExportPathFolder, ifcPostExportPathFilename);
            var ifcExportTempPath = Path.Combine(ifcExportPathFolder, ifcExportPathFilename + "_temp");


            ExportDocumentToIfc(doc, ifcExportPathFolder, ifcExportPathFilename);
            BumpIFCVersionTo4X1(ifcExportPath, ifcExportTempPath);
            AddTunnelLineAsAxisRepresentation(doc, ifcExportPath);
            DeleteTunnelSections(ifcExportPath);
            RecreateTunnelSectionsInIFC(ifcExportPath, tunnelFamilyDocument);
            AddTunnelSectionProfilesAsProfileRepresentation(doc, ifcExportPath);
            AddTunnelSectionLinesAsAxisRepresentation(doc, ifcExportPath);











            return Result.Succeeded;

        }

    }
}
